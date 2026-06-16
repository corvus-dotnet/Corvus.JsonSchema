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
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) and the usage path by
/// label-superset — applied in memory over the candidate set for a (sourceName, environment), since a deployment keeps
/// those reach-disjoint. The document id is a deterministic, opaque hash of the tag discriminator, so a duplicate
/// (sourceName, environment, tags) create collides on the item id and surfaces as a <see cref="HttpStatusCode.Conflict"/>.
/// </remarks>
public sealed class CosmosSourceCredentialStore : ISourceCredentialStore, IAsyncDisposable
{
    private const string ContainerId = "workflow_credentials";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();

    // The single-partition candidate set for a (sourceName, environment) is read with this projection and ordered in
    // memory; the binding document and its tag discriminator are the two fields each store operation needs.
    private static readonly IComparer<ParsedJsonDocument<SourceCredentialBinding>> BySourceThenEnvironment =
        Comparer<ParsedJsonDocument<SourceCredentialBinding>>.Create(static (a, b) =>
        {
            int bySource = string.CompareOrdinal(a.RootElement.SourceNameValue, b.RootElement.SourceNameValue);
            return bySource != 0 ? bySource : string.CompareOrdinal(a.RootElement.EnvironmentValue, b.RootElement.EnvironmentValue);
        });

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
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialDefinition definition, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "scred-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        string partition = PartitionKey(definition.SourceName, definition.Environment);
        string tags = SourceCredentialKey.Discriminator(definition.ManagementTags, definition.UsageTags);

        // The document id within the partition is a deterministic, opaque hash of the tag discriminator, so a duplicate
        // (sourceName, environment, tags) create collides on the item id and Cosmos returns a 409 — mirroring the
        // relational backends' composite-primary-key uniqueness.
        string itemId = ItemId(tags);
        byte[] json = SourceCredentialSerialization.SerializeNew(id, definition, actor, this.timeProvider.GetUtcNow(), NewEtag());

        using MemoryStream stream = EnvelopeStream(itemId, partition, json, out ParsedJsonDocument<SourceCredentialBinding> document);
        try
        {
            using ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(partition), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException($"A source credential binding for '{definition.SourceName}@{definition.Environment}' with those security tags already exists.");
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
    public async ValueTask<PooledDocumentList<SourceCredentialBinding>> ListAsync(AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var docs = new PooledDocumentList<SourceCredentialBinding>();
        try
        {
            var query = new QueryDefinition("SELECT c.doc FROM c");
            await foreach (ReadOnlyMemory<byte> json in this.QueryDocumentsAsync(query, partition: null, cancellationToken).ConfigureAwait(false))
            {
                using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json.Span);
                if (context.Admits(AccessVerb.Read, candidate.RootElement.ManagementTagsValue))
                {
                    docs.Add(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json.Span));
                }
            }

            docs.Sort(BySourceThenEnvironment);
            return docs;
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialDefinition definition, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? tags) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        string partition = PartitionKey(sourceName, environment);
        byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), NewEtag());

        using MemoryStream stream = EnvelopeStream(ItemId(tags!), partition, json, out ParsedJsonDocument<SourceCredentialBinding> document);
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
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(discriminator), hash);
        return "scred-" + Convert.ToHexStringLower(hash);
    }

    // Serializes the {id, pk, sourceName, environment, doc:base64} envelope into a pooled stream and builds the
    // caller's pooled return document from the same binding bytes. The binding doc is base64'd verbatim (no SDK
    // serializer); sourceName/environment are projected so EvaluateSourceAccessAsync can query by source across
    // partitions. On any failure building the stream, the return document is disposed before the exception escapes.
    private static MemoryStream EnvelopeStream(string id, string partition, byte[] doc, out ParsedJsonDocument<SourceCredentialBinding> document)
    {
        document = PersistedJson.ToPooledDocument<SourceCredentialBinding>(doc);
        try
        {
            return CosmosJson.WriteToStream(
                (Id: id, Partition: partition, Source: document.RootElement.SourceNameValue, Environment: document.RootElement.EnvironmentValue, Doc: doc),
                static (Utf8JsonWriter writer, in (string Id, string Partition, string Source, string Environment, byte[] Doc) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Partition);
                    writer.WriteString("sourceName"u8, c.Source);
                    writer.WriteString("environment"u8, c.Environment);

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
    // bytes and its tag discriminator (the document id seed). A binding outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(string sourceName, string environment, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        string partition = PartitionKey(sourceName, environment);
        var query = new QueryDefinition("SELECT c.doc, c.tags FROM c");
        await foreach (ReadOnlyMemory<byte> json in this.QueryDocumentsAsync(query, partition, cancellationToken).ConfigureAwait(false))
        {
            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json.Span);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                // The bytes outlive the response page (the caller may update/delete from them), so copy them out.
                return (json.ToArray(), ItemIdSeedFor(candidate.RootElement));
            }
        }

        return (null, null);
    }

    // The tag discriminator (the item-id seed) recomputed from a candidate's own immutable tags — the projection only
    // carried the doc, and the binding holds its management/usage tags, so the discriminator is reconstructed rather
    // than re-read from the envelope's tags field.
    private static string ItemIdSeedFor(SourceCredentialBinding binding)
        => SourceCredentialKey.Discriminator(binding.ManagementTagsValue, binding.UsageTagsValue);

    // Yields the embedded binding document's raw UTF-8 bytes (a slice into the pooled response page) for each result —
    // no base64 decode. The slice is valid only for the duration of the consumer's iteration step; a consumer that
    // keeps the bytes past it (e.g. for an update) copies them (ToArray), a transient consumer parses them in place.
    private async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryDocumentsAsync(QueryDefinition query, string? partition, [EnumeratorCancellation] CancellationToken cancellationToken)
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
                ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(element, DocProperty);
                if (!doc.IsEmpty)
                {
                    yield return doc;
                }
            }
        }
    }
}