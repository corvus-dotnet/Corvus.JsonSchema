// <copyright file="CosmosSourceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="ISourceStore"/> (design §7.6): registered sources persisted as
/// documents in a small <c>workflow_sources</c> container. Each source is one document, partitioned by its
/// name so a name's small candidate set is a single-partition read, and identified within that partition by a
/// discriminator over its immutable management tags so reach-isolated sources that share a name coexist while an
/// exact duplicate is rejected. The source's <see cref="RegisteredSource"/> document is held verbatim as a nested
/// field and its store-owned etag travels inside it; documents are written and read through the Cosmos <em>stream</em>
/// APIs (no SDK serializer), so persistence flows through Corvus.Text.Json.
/// </summary>
/// <remarks>
/// Management point reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) in memory over
/// the bounded candidate set for a name, since a deployment keeps those reach-disjoint; list/count push the reach into
/// the query as an <c>EXISTS</c> over the envelope's <c>securityTags</c> mirror (the same predicate, applied by Cosmos),
/// so out-of-reach rows never leave the store. The document id is a deterministic, opaque hash of the tag
/// discriminator, so a duplicate (name, tags) create collides on the item id and surfaces as a
/// <see cref="HttpStatusCode.Conflict"/>; the discriminator itself is stored on the envelope (<c>tags</c>) so later
/// updates and deletes keep addressing the row after a re-tag changes the document's tags.
/// </remarks>
public sealed class CosmosSourceStore : ISourceStore, IAsyncDisposable
{
    private const string ContainerId = "workflow_sources";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();
    private static readonly byte[] TagsProperty = "tags"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosSourceStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
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
    public static ValueTask<CosmosSourceStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosSourceStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured Cosmos client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosSourceStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosSourceStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>> AddAsync(RegisteredSource draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string partition = draft.NameValue;
        string tags = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);

        // The document id within the partition is a deterministic, opaque hash of the tag discriminator, so a duplicate
        // (name, tags) create collides on the item id and Cosmos returns a 409 — mirroring the relational backends'
        // composite-primary-key uniqueness.
        string itemId = ItemId(tags);
        byte[] json = SourceSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), NewEtag());

        using Stream stream = EnvelopeStream(itemId, partition, tags, json, out ParsedJsonDocument<RegisteredSource> document);
        try
        {
            using ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(partition), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ThrowHelper.ThrowSourceAlreadyExists(draft.NameValue);
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
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? json, _) = await this.FindForManagementAsync(name, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<RegisteredSource>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<SourcePage> ListAsync(AccessContext context, int limit, Corvus.Text.Json.Arazzo.Durability.JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Name, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = SourceContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // Keyset seek in the stable total order (name, discriminator), with the §14.2 read reach pushed into the query
        // as an EXISTS over the envelope's securityTags mirror (the same predicate context.Admits evaluates, but applied
        // by Cosmos), so out-of-reach rows never leave the store. Only the name is orderable server-side (the
        // discriminator tie-break needs a composite index Cosmos is not provisioned with), so the query seeks to the
        // cursor's name, re-includes its exact name, and the in-memory comparison below skips the bounded few same-name
        // rows at or before the cursor; the cross-partition ORDER BY needs an index on name (a deployment concern).
        // Streaming stops once a further admitted row beyond the page is seen.
        var conditions = new List<string>(2);
        var parameters = new List<(string Name, string Value)>();
        if (hasCursor)
        {
            conditions.Add("c.name >= @n");
            parameters.Add(("@n", cursor.Name));
        }

        AppendReachCondition(conditions, parameters, context);

        var sql = new StringBuilder("SELECT c.doc, c.tags FROM c");
        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" ORDER BY c.name");
        var query = new QueryDefinition(sql.ToString());
        foreach ((string name, string value) in parameters)
        {
            query = query.WithParameter(name, value);
        }

        var docs = new PooledDocumentList<RegisteredSource>(pageSize);
        bool hasMore = false;
        try
        {
            string lastName = string.Empty, lastTie = string.Empty;
            await foreach (ReadOnlyMemory<byte> element in this.QueryElementsAsync(query, partition: null, cancellationToken).ConfigureAwait(false))
            {
                ReadOnlyMemory<byte> json = CosmosJson.GetRawValue(element, DocProperty);
                if (json.IsEmpty)
                {
                    continue;
                }

                ParsedJsonDocument<RegisteredSource> cand = PersistedJson.ToPooledDocument<RegisteredSource>(json.Span);
                bool kept = false;
                try
                {
                    string name = cand.RootElement.NameValue;
                    string tie = CosmosJson.GetString(element, TagsProperty) ?? string.Empty;

                    // The server seek re-includes the cursor's exact name, so skip any row at or before the cursor in the
                    // full (name, discriminator) order — the discriminator tie-break is resolved here since it is not a
                    // server-orderable property.
                    if (hasCursor && CompareKey(name, tie, cursor.Name, cursor.TieBreaker) <= 0)
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
                    lastName = name;
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

            return hasMore ? SourcePage.Create(docs, lastName, lastTie) : SourcePage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> UpdateAsync(string name, RegisteredSource draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? tags) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());

        // The envelope is rebuilt from the updated document under the FROZEN create-time discriminator (the row's
        // storage identity), so its securityTags mirror follows a re-tag while the item id stays put.
        using Stream stream = EnvelopeStream(ItemId(tags!), name, tags!, json, out ParsedJsonDocument<RegisteredSource> document);
        try
        {
            using ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, ItemId(tags!), new PartitionKey(name), cancellationToken: cancellationToken).ConfigureAwait(false);
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
    public async ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? tags) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            SourceSerialization.EnsureEtag(name, expectedEtag, SourceSerialization.EtagOf(existing));
        }

        using ResponseMessage response = await this.container.DeleteItemStreamAsync(ItemId(tags!), new PartitionKey(name), cancellationToken: cancellationToken).ConfigureAwait(false);
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
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient)
        {
            this.client.Dispose();
        }

        return default;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // A deterministic, opaque, Cosmos-id-safe document id from the tag discriminator (which may contain control
    // characters unsuitable for an item id). Two sources whose tags differ get different ids and so coexist within
    // the partition; an exact-duplicate create collides on the id and surfaces as a 409.
    private static string ItemId(string discriminator)
    {
        return CosmosItemId.Compose("src-", discriminator);
    }

    // Serializes the {id, pk, name, tags, securityTags, doc} envelope into a pooled stream and builds the caller's
    // pooled return document from the same source bytes. The source doc is embedded verbatim (no SDK serializer);
    // name is projected so ListAsync can ORDER BY it server-side; tags carries the FROZEN create-time discriminator
    // so updates/deletes keep addressing the row after a re-tag; securityTags mirrors the document's CURRENT
    // management tags queryably so the §14.2 reach predicate can be pushed into list/count. On any failure building
    // the stream, the return document is disposed before the exception escapes.
    private static Stream EnvelopeStream(string id, string partition, string discriminator, byte[] doc, out ParsedJsonDocument<RegisteredSource> document)
    {
        document = PersistedJson.ToPooledDocument<RegisteredSource>(doc);
        try
        {
            return CosmosJson.WriteToStream(
                (Id: id, Partition: partition, Name: document.RootElement.NameValue, Discriminator: discriminator, Tags: document.RootElement.ManagementTagsValue, Doc: doc),
                static (Utf8JsonWriter writer, in (string Id, string Partition, string Name, string Discriminator, SecurityTagSet Tags, byte[] Doc) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Partition);
                    writer.WriteString("name"u8, c.Name);
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

                    // The source document is itself JSON, so embed it verbatim as a nested value — no base64 wrap
                    // (which would be a spurious encode here + decode on read). It is valid JSON we produced, so skip
                    // validation.
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

    private static CosmosSourceStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosSourceStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    // Orders two sources by the stable total key (name, discriminator), ordinally — matching the Cosmos string
    // ORDER BY — so the in-memory keyset skip past the cursor agrees with the server-side seek on the name and resolves
    // the discriminator tie-break the query cannot express.
    private static int CompareKey(string nameA, string tieA, string nameB, string tieB)
    {
        int byName = string.CompareOrdinal(nameA, nameB);
        return byName != 0 ? byName : string.CompareOrdinal(tieA, tieB);
    }

    // Finds the single source named `name` the caller's reach for the verb admits, returning its bytes and its STORED
    // tag discriminator (the document id seed, read back from the envelope rather than recomputed — after a re-tag the
    // document's tags no longer derive the id). A source outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT c.doc, c.tags FROM c");
        await foreach (ReadOnlyMemory<byte> element in this.QueryElementsAsync(query, name, cancellationToken).ConfigureAwait(false))
        {
            ReadOnlyMemory<byte> json = CosmosJson.GetRawValue(element, DocProperty);
            if (json.IsEmpty)
            {
                continue;
            }

            using ParsedJsonDocument<RegisteredSource> candidate = PersistedJson.ToPooledDocument<RegisteredSource>(json.Span);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                // The bytes outlive the response page (the caller may update/delete from them), so copy them out.
                return (json.ToArray(), CosmosJson.GetString(element, TagsProperty));
            }
        }

        return (null, null);
    }

    // Yields each result envelope's raw element bytes (a slice into the pooled response page), so the consumer can
    // extract the embedded doc and any projected envelope properties (e.g. the stored tag discriminator) from the same
    // element. The slice is valid only for the duration of the consumer's iteration step; a consumer that keeps bytes
    // past it (e.g. for an update) copies them (ToArray), a transient consumer parses them in place.
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