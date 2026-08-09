// <copyright file="CosmosSourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="ISourceCredentialStore"/> (design §13): source credential bindings —
/// references and non-sensitive metadata only, never secret material — persisted as documents in a small
/// <c>workflow_credentials</c> container. Each binding is one document, partitioned by (SourceName, Environment) so a
/// source/environment's small candidate set is a single-partition read, and identified within that partition by a
/// discriminator over its immutable management/usage tag sets so tenant-/workflow-scoped bindings for the same
/// source/environment coexist while an exact duplicate is rejected. The binding's <see cref="SourceCredentialBinding"/>
/// document is held verbatim as a base64 field and its store-owned etag travels inside it; documents are written and
/// read through the Cosmos <em>stream</em> APIs (no SDK serializer), so persistence flows through Corvus.Text.Json.
/// </summary>
/// <remarks>
/// Management point reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) and the usage
/// path by label-superset — applied in memory over the bounded candidate set for a (sourceName, environment), since a
/// deployment keeps those reach-disjoint; list/count push the management reach into the query as an <c>EXISTS</c> over
/// the envelope's <c>securityTags</c> mirror (the same predicate, applied by Cosmos), so out-of-reach rows never leave
/// the store. The document id is a deterministic, opaque hash of the tag discriminator, so a duplicate (sourceName,
/// environment, tags) create collides on the item id and surfaces as a <see cref="HttpStatusCode.Conflict"/>; the
/// discriminator itself is stored on the envelope (<c>tags</c>) so later updates and deletes keep addressing the row
/// after a re-tag changes the document's management tags.
/// </remarks>
public sealed class CosmosSourceCredentialStore : ISourceCredentialStore, IAsyncDisposable
{
    private const string ContainerId = "workflow_credentials";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();
    private static readonly byte[] TagsProperty = "tags"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosSourceCredentialStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
    {
        this.client = client;
        this.container = container;
        this.timeProvider = timeProvider;
        this.ownsClient = ownsClient;
    }

    /// <summary>The Cosmos client options the store relies on (none; it uses the stream APIs + Corvus.Text.Json).</summary>
    /// <returns>The Cosmos client options used by the connection-string overloads.</returns>
    public static CosmosClientOptions CreateClientOptions() => new();

    /// <summary>Provisions the store's database and container over the given connection string.</summary>
    /// <param name="connectionString">An Azure Cosmos DB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        using var client = new CosmosClient(connectionString, CreateClientOptions());
        await ProvisionAsync(client, databaseName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the store's database and container over a caller-supplied client.</summary>
    /// <param name="client">A configured Cosmos client (the caller retains ownership and must dispose it).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (idempotent).</returns>
    public static ValueTask PrepareAsync(CosmosClient client, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return ProvisionAsync(client, databaseName, cancellationToken);
    }

    /// <summary>Opens the store for operation against an already-provisioned database and container.</summary>
    /// <param name="connectionString">An Azure Cosmos DB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<CosmosSourceCredentialStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosSourceCredentialStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured Cosmos client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosSourceCredentialStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosSourceCredentialStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialBinding draft, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "scred-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        string partition = PartitionKey(draft.SourceNameValue, draft.EnvironmentValue);
        string tags = SourceCredentialKey.Discriminator(draft.ManagementTagsValue, draft.UsageTagsValue);

        // The document id within the partition is a deterministic, opaque hash of the tag discriminator, so a duplicate
        // (sourceName, environment, tags) create collides on the item id and Cosmos returns a 409 — mirroring the
        // relational backends' composite-primary-key uniqueness.
        string itemId = ItemId(tags);
        byte[] json = SourceCredentialSerialization.SerializeNew(id, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());

        using Stream stream = EnvelopeStream(itemId, partition, tags, json, out ParsedJsonDocument<SourceCredentialBinding> document);
        try
        {
            using ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(partition), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ThrowHelper.ThrowSourceCredentialAlreadyExists(draft.SourceNameValue, draft.EnvironmentValue);
            }

            response.EnsureSuccessStatusCode();
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? json, _) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<SourceCredentialPage> ListAsync(AccessContext context, int limit, Corvus.Text.Json.Arazzo.Durability.JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string SourceName, string Environment, string TieBreaker) cursor = (string.Empty, string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = SourceCredentialContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // Keyset seek in the stable total order (sourceName, environment, discriminator), with the §14.2 management
        // read reach pushed into the query as an EXISTS over the envelope's securityTags mirror (the same predicate
        // context.Admits evaluates, but applied by Cosmos), so out-of-reach rows never leave the store. Only the first
        // two key parts are orderable server-side (the discriminator tie-break needs a composite index Cosmos is not
        // provisioned with), so the query seeks past the cursor's (sourceName, environment), re-includes the cursor's
        // exact pair, and the in-memory comparison below skips the bounded few same-pair rows at or before the cursor;
        // the cross-partition ORDER BY needs a composite index on (sourceName, environment) (a deployment concern).
        // Streaming stops once a further admitted row beyond the page is seen, so we read only as far as the page
        // (plus one) requires, never the whole container.
        var conditions = new List<string>(2);
        var parameters = new List<(string Name, string Value)>();
        if (hasCursor)
        {
            conditions.Add("(c.sourceName > @s OR (c.sourceName = @s AND c.environment >= @e))");
            parameters.Add(("@s", cursor.SourceName));
            parameters.Add(("@e", cursor.Environment));
        }

        AppendReachCondition(conditions, parameters, context);

        var sql = new StringBuilder("SELECT c.doc, c.tags FROM c");
        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" ORDER BY c.sourceName, c.environment");
        var query = new QueryDefinition(sql.ToString());
        foreach ((string name, string value) in parameters)
        {
            query = query.WithParameter(name, value);
        }

        var docs = new PooledDocumentList<SourceCredentialBinding>(pageSize);
        bool hasMore = false;
        try
        {
            string lastSource = string.Empty, lastEnv = string.Empty, lastTie = string.Empty;
            await foreach (ReadOnlyMemory<byte> element in this.QueryElementsAsync(query, partition: null, cancellationToken).ConfigureAwait(false))
            {
                ReadOnlyMemory<byte> json = CosmosJson.GetRawValue(element, DocProperty);
                if (json.IsEmpty)
                {
                    continue;
                }

                ParsedJsonDocument<SourceCredentialBinding> cand = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json.Span);
                bool kept = false;
                try
                {
                    string source = cand.RootElement.SourceNameValue;
                    string environment = cand.RootElement.EnvironmentValue;
                    string tie = CosmosJson.GetString(element, TagsProperty) ?? string.Empty;

                    // The server seek re-includes the cursor's exact (sourceName, environment), so skip any row at or before
                    // the cursor in the full (sourceName, environment, discriminator) order — the discriminator tie-break is
                    // resolved here since it is not a server-orderable property.
                    if (hasCursor && CompareKey(source, environment, tie, cursor.SourceName, cursor.Environment, cursor.TieBreaker) <= 0)
                    {
                        continue;
                    }

                    if (docs.Count == pageSize)
                    {
                        hasMore = true;
                        break;
                    }

                    docs.Add(cand);
                    kept = true;
                    lastSource = source;
                    lastEnv = environment;
                    lastTie = tie;
                }
                finally
                {
                    if (!kept)
                    {
                        cand.Dispose();
                    }
                }
            }

            return hasMore ? SourceCredentialPage.Create(docs, lastSource, lastEnv, lastTie) : SourceCredentialPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialBinding draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? tags) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        string partition = PartitionKey(sourceName, environment);
        byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());

        // The envelope is rebuilt from the updated document under the FROZEN create-time discriminator (the row's
        // storage identity), so its securityTags mirror follows a re-tag while the item id stays put.
        using Stream stream = EnvelopeStream(ItemId(tags!), partition, tags!, json, out ParsedJsonDocument<SourceCredentialBinding> document);
        try
        {
            using ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, ItemId(tags!), new PartitionKey(partition), cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? tags) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            SourceCredentialSerialization.EnsureEtag($"{sourceName}@{environment}", expectedEtag, SourceCredentialSerialization.EtagOf(existing));
        }

        using ResponseMessage response = await this.container.DeleteItemStreamAsync(ItemId(tags!), new PartitionKey(PartitionKey(sourceName, environment)), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(AccessContext context, int cap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var conditions = new List<string>(1);
        var parameters = new List<(string Name, string Value)>();
        AppendReachCondition(conditions, parameters, context);

        // Bounded count: Cosmos has no bounded server-side COUNT, so read at most cap + 1 admitted row markers and
        // count them client-side; the (cap + 1)th trips Capped. Same reach predicate as the list
        // (AppendReachCondition) — the count can never drift from the list it annotates.
        var sql = new StringBuilder("SELECT VALUE 1 FROM c");
        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" OFFSET 0 LIMIT @cap");
        var query = new QueryDefinition(sql.ToString()).WithParameter("@cap", cap + 1);
        foreach ((string name, string value) in parameters)
        {
            query = query.WithParameter(name, value);
        }

        int total = 0;
        using FeedIterator iterator = this.container.GetItemQueryStreamIterator(query);
        while (iterator.HasMoreResults)
        {
            using ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse page = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
            total += CosmosJson.ReadDocuments(page.Memory).Count;
        }

        return total > cap ? (cap, true) : (total, false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> ResolveForUsageAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        string partition = PartitionKey(sourceName, environment);
        var query = new QueryDefinition("SELECT c.doc FROM c");
        await foreach (ReadOnlyMemory<byte> json in this.QueryDocumentsAsync(query, partition, cancellationToken).ConfigureAwait(false))
        {
            ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json.Span);
            if (candidate.RootElement.IsUsableBy(runTags))
            {
                return candidate;
            }

            candidate.Dispose();
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<CredentialSourceAccess> EvaluateSourceAccessAsync(string sourceName, SecurityTagSet tags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);

        // Bindings for a source span every environment (each its own partition), so this is a cross-partition query
        // scoped to the source name; every value is bound as a parameter (no concatenation).
        var query = new QueryDefinition("SELECT c.doc FROM c WHERE c.sourceName = @s").WithParameter("@s", sourceName);
        bool any = false;
        await foreach (ReadOnlyMemory<byte> json in this.QueryDocumentsAsync(query, partition: null, cancellationToken).ConfigureAwait(false))
        {
            any = true;
            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json.Span);
            if (candidate.RootElement.IsUsableBy(tags))
            {
                return CredentialSourceAccess.Granted;
            }
        }

        return any ? CredentialSourceAccess.Denied : CredentialSourceAccess.Unconfigured;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient)
        {
            this.client.Dispose();
        }

        return default;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // The partition key for a (sourceName, environment): the candidate set for a single source/environment is one
    // partition, so management lookups, usage resolution, and update/delete are single-partition reads. The unit
    // separator cannot appear in either component, so the join is unambiguous.
    private static string PartitionKey(string sourceName, string environment) => sourceName + '\u001f' + environment;

    // A deterministic, opaque, Cosmos-id-safe document id from the tag discriminator (which may contain control
    // characters unsuitable for an item id). Two bindings whose tags differ get different ids and so coexist within the
    // partition; an exact-duplicate create collides on the id and surfaces as a 409.
    private static string ItemId(string discriminator)
    {
        return CosmosItemId.Compose("scred-", discriminator);
    }

    // Serializes the {id, pk, sourceName, environment, tags, securityTags, doc} envelope into a pooled stream and
    // builds the caller's pooled return document from the same binding bytes. The binding doc is embedded verbatim (no
    // SDK serializer); sourceName/environment are projected so EvaluateSourceAccessAsync can query by source across
    // partitions; tags carries the FROZEN create-time discriminator so updates/deletes keep addressing the row after a
    // re-tag; securityTags mirrors the document's CURRENT management tags (never the usage tags — reach is a
    // management concern) queryably so the §14.2 reach predicate can be pushed into list/count. On any failure
    // building the stream, the return document is disposed before the exception escapes.
    private static Stream EnvelopeStream(string id, string partition, string discriminator, byte[] doc, out ParsedJsonDocument<SourceCredentialBinding> document)
    {
        document = PersistedJson.ToPooledDocument<SourceCredentialBinding>(doc);
        try
        {
            return CosmosJson.WriteToStream(
                (Id: id, Partition: partition, Source: document.RootElement.SourceNameValue, Environment: document.RootElement.EnvironmentValue, Discriminator: discriminator, Tags: document.RootElement.ManagementTagsValue, Doc: doc),
                static (Utf8JsonWriter writer, in (string Id, string Partition, string Source, string Environment, string Discriminator, SecurityTagSet Tags, byte[] Doc) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Partition);
                    writer.WriteString("sourceName"u8, c.Source);
                    writer.WriteString("environment"u8, c.Environment);
                    writer.WriteString("tags"u8, c.Discriminator);
                    writer.WritePropertyName("securityTags"u8);
                    writer.WriteStartArray();
                    foreach (SecurityTag tag in c.Tags)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("k"u8, tag.Key);
                        writer.WriteString("v"u8, tag.Value);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();

                    // The binding document is itself JSON, so embed it verbatim as a nested value — no base64 wrap (which
                    // would be a spurious encode here + decode on read). It is valid JSON we produced, so skip validation.
                    writer.WritePropertyName("doc"u8);
                    writer.WriteRawValue(c.Doc, skipInputValidation: true);
                    writer.WriteEndObject();
                });
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    // Appends the §14.2 read-reach predicate: an EXISTS over the envelope's securityTags mirror (the same rules
    // context.Admits evaluates, translated by CosmosSecurityRuleEmitter). A null reach (unrestricted) adds nothing.
    // Shared by ListAsync and CountAsync so the count can never drift from the list it annotates.
    private static void AppendReachCondition(List<string> conditions, List<(string Name, string Value)> parameters, AccessContext context)
    {
        if (context.Reach(AccessVerb.Read) is not { } reach)
        {
            return;
        }

        int securityParam = 0;
        var emitter = new CosmosSecurityRuleEmitter("c.securityTags", "k", "v", value =>
        {
            string name = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
            parameters.Add((name, value));
            return name;
        });
        conditions.Add(reach.ToSqlPredicate(emitter));
    }

    private static async ValueTask ProvisionAsync(CosmosClient client, string databaseName, CancellationToken cancellationToken)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(ContainerId, "/pk"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosSourceCredentialStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosSourceCredentialStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    // Finds the single binding for (sourceName, environment) the caller's reach for the verb admits, returning its
    // bytes and its STORED tag discriminator (the document id seed, read back from the envelope rather than
    // recomputed — after a management re-tag the document's tags no longer derive the id). A binding outside reach is
    // invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(string sourceName, string environment, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        string partition = PartitionKey(sourceName, environment);
        var query = new QueryDefinition("SELECT c.doc, c.tags FROM c");
        await foreach (ReadOnlyMemory<byte> element in this.QueryElementsAsync(query, partition, cancellationToken).ConfigureAwait(false))
        {
            ReadOnlyMemory<byte> json = CosmosJson.GetRawValue(element, DocProperty);
            if (json.IsEmpty)
            {
                continue;
            }

            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json.Span);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                // The bytes outlive the response page (the caller may update/delete from them), so copy them out.
                return (json.ToArray(), CosmosJson.GetString(element, TagsProperty));
            }
        }

        return (null, null);
    }

    // Orders two bindings by the stable total key (sourceName, environment, discriminator), ordinally — matching the
    // Cosmos string ORDER BY — so the in-memory keyset skip past the cursor agrees with the server-side seek on the
    // first two parts and resolves the discriminator tie-break the query cannot express.
    private static int CompareKey(string sourceA, string envA, string tieA, string sourceB, string envB, string tieB)
    {
        int bySource = string.CompareOrdinal(sourceA, sourceB);
        if (bySource != 0)
        {
            return bySource;
        }

        int byEnv = string.CompareOrdinal(envA, envB);
        return byEnv != 0 ? byEnv : string.CompareOrdinal(tieA, tieB);
    }

    // Yields the embedded binding document's raw UTF-8 bytes (a slice into the pooled response page) for each result —
    // no base64 decode. The slice is valid only for the duration of the consumer's iteration step; a consumer that
    // keeps the bytes past it (e.g. for an update) copies them (ToArray), a transient consumer parses them in place.
    private async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryDocumentsAsync(QueryDefinition query, string? partition, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (ReadOnlyMemory<byte> element in this.QueryElementsAsync(query, partition, cancellationToken).ConfigureAwait(false))
        {
            ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(element, DocProperty);
            if (!doc.IsEmpty)
            {
                yield return doc;
            }
        }
    }

    // Yields each result envelope's raw element bytes (a slice into the pooled response page), so the consumer can
    // extract the embedded doc and any projected envelope properties (e.g. the stored tag discriminator) from the same
    // element. The slice is valid only for the duration of the consumer's iteration step; a consumer that keeps bytes
    // past it copies them (ToArray), a transient consumer parses them in place.
    private async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryElementsAsync(QueryDefinition query, string? partition, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        QueryRequestOptions? options = partition is null ? null : new QueryRequestOptions { PartitionKey = new PartitionKey(partition) };
        using FeedIterator iterator = this.container.GetItemQueryStreamIterator(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            using ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse page = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
            foreach (ReadOnlyMemory<byte> element in CosmosJson.ReadDocuments(page.Memory))
            {
                yield return element;
            }
        }
    }
}