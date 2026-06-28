// <copyright file="CosmosSourceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Corvus.Runtime.InteropServices;
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
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory
/// over the candidate set for a name, since a deployment keeps those reach-disjoint. The document id is a deterministic,
/// opaque hash of the tag discriminator, so a duplicate (name, tags) create collides on the item id and surfaces as a
/// <see cref="HttpStatusCode.Conflict"/>.
/// </remarks>
public sealed class CosmosSourceStore : ISourceStore, IAsyncDisposable
{
    private const string ContainerId = "workflow_sources";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();

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

        using Stream stream = EnvelopeStream(itemId, partition, json, out ParsedJsonDocument<RegisteredSource> document);
        try
        {
            using ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(partition), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException($"A source named '{draft.NameValue}' with those security tags already exists.");
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

        // Keyset seek in the stable total order (name, discriminator). Cosmos cannot push the security-reach predicate —
        // reach is a per-row check applied in memory as we stream — and the discriminator is not a stored envelope
        // property (it is recomputed from each source's own immutable tags), so only the name is seekable/orderable
        // server-side. The query seeks to the cursor's name and re-includes its exact name so the in-memory tie-breaker
        // comparison can skip rows up to and including it; the cross-partition ORDER BY needs an index on name (a
        // deployment concern). Streaming stops once a further visible row beyond the page is seen.
        QueryDefinition query = hasCursor
            ? new QueryDefinition("SELECT c.doc FROM c WHERE c.name >= @n ORDER BY c.name").WithParameter("@n", cursor.Name)
            : new QueryDefinition("SELECT c.doc FROM c ORDER BY c.name");

        var docs = new PooledDocumentList<RegisteredSource>(pageSize);
        bool hasMore = false;
        try
        {
            string lastName = string.Empty, lastTie = string.Empty;
            await foreach (ReadOnlyMemory<byte> json in this.QueryDocumentsAsync(query, partition: null, cancellationToken).ConfigureAwait(false))
            {
                ParsedJsonDocument<RegisteredSource> cand = PersistedJson.ToPooledDocument<RegisteredSource>(json.Span);
                bool kept = false;
                try
                {
                    string name = cand.RootElement.NameValue;
                    string tie = TieBreakerFor(cand.RootElement);

                    // The server seek re-includes the cursor's exact name, so skip any row at or before the cursor in the
                    // full (name, discriminator) order — the discriminator tie-break is resolved here since it is not a
                    // stored, orderable property.
                    if (hasCursor && CompareKey(name, tie, cursor.Name, cursor.TieBreaker) <= 0)
                    {
                        continue;
                    }

                    SecurityTagSet tags = cand.RootElement.ManagementTags.IsNotUndefined()
                        ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(cand.RootElement.ManagementTags).Memory)
                        : SecurityTagSet.Empty;
                    if (!context.Admits(AccessVerb.Read, tags))
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

        using Stream stream = EnvelopeStream(ItemId(tags!), name, json, out ParsedJsonDocument<RegisteredSource> document);
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
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(discriminator), hash);
        return "src-" + Convert.ToHexStringLower(hash);
    }

    // Serializes the {id, pk, name, doc} envelope into a pooled stream and builds the caller's pooled return document
    // from the same source bytes. The source doc is embedded verbatim (no SDK serializer); name is projected
    // so ListAsync can ORDER BY it server-side. On any failure building the stream, the return document is disposed
    // before the exception escapes.
    private static Stream EnvelopeStream(string id, string partition, byte[] doc, out ParsedJsonDocument<RegisteredSource> document)
    {
        document = PersistedJson.ToPooledDocument<RegisteredSource>(doc);
        try
        {
            return CosmosJson.WriteToStream(
                (Id: id, Partition: partition, Name: document.RootElement.NameValue, Doc: doc),
                static (Utf8JsonWriter writer, in (string Id, string Partition, string Name, byte[] Doc) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Partition);
                    writer.WriteString("name"u8, c.Name);

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

    // The tag discriminator (the item-id seed and keyset tie-breaker) recomputed from a candidate's own immutable tags —
    // the projection only carried the doc, and the source holds its management tags, so the discriminator is
    // reconstructed rather than re-read from the envelope.
    private static string TieBreakerFor(RegisteredSource source)
        => SourceCredentialKey.CanonicalTags(source.ManagementTagsValue);

    // Orders two sources by the stable total key (name, discriminator), ordinally — matching the Cosmos string
    // ORDER BY — so the in-memory keyset skip past the cursor agrees with the server-side seek on the name and resolves
    // the discriminator tie-break the query cannot express.
    private static int CompareKey(string nameA, string tieA, string nameB, string tieB)
    {
        int byName = string.CompareOrdinal(nameA, nameB);
        return byName != 0 ? byName : string.CompareOrdinal(tieA, tieB);
    }

    // Finds the single source named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the document id seed). A source outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT c.doc FROM c");
        await foreach (ReadOnlyMemory<byte> json in this.QueryDocumentsAsync(query, name, cancellationToken).ConfigureAwait(false))
        {
            using ParsedJsonDocument<RegisteredSource> candidate = PersistedJson.ToPooledDocument<RegisteredSource>(json.Span);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                // The bytes outlive the response page (the caller may update/delete from them), so copy them out.
                return (json.ToArray(), TieBreakerFor(candidate.RootElement));
            }
        }

        return (null, null);
    }

    // Yields the embedded source document's raw UTF-8 bytes (a slice into the pooled response page) for each result
    // — no base64 decode. The slice is valid only for the duration of the consumer's iteration step; a consumer that
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