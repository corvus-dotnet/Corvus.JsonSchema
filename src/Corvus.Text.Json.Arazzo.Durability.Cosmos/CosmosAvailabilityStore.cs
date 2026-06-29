// <copyright file="CosmosAvailabilityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="IAvailabilityStore"/> (design §7.8): the availability matrix (which workflow
/// versions are available in which environments) persisted as documents in an <c>availability</c> container. Each entry
/// is one document whose id is a deterministic, opaque hash of its composite key
/// (<c>baseWorkflowId</c>, <c>versionNumber</c>, <c>environment</c>) and which is its own partition key, so make / get /
/// withdraw are single-partition point operations and a duplicate make-available collides on the id (surfacing as a
/// <see cref="HttpStatusCode.Conflict"/> the idempotent create resolves by re-reading). The entry's
/// <see cref="AvailabilityEntry"/> document is held verbatim as a nested <c>doc</c> field; documents are written and read
/// through the Cosmos <em>stream</em> APIs (no SDK serializer), so persistence flows through Corvus.Text.Json.
/// </summary>
/// <remarks>
/// <para><strong>Two list axes, both natively ordered.</strong> The envelope projects the queryable key parts
/// (<c>baseWorkflowId</c>, <c>versionNumber</c>, <c>environment</c>) plus a single collapsed <c>sortKey</c>
/// (<c>{baseWorkflowId}{versionNumber:D10}</c>) so each axis is a <em>single-property</em> cross-partition
/// <c>ORDER BY</c>: <see cref="ListByVersionAsync"/> orders by <c>environment</c> within a fixed
/// (workflow, version); <see cref="ListByEnvironmentAsync"/> orders by the collapsed <c>sortKey</c>, which makes the
/// (workflow id, numeric version) total order a plain string sort. This mirrors how the catalog store avoids a Cosmos
/// composite index, so the default indexing policy suffices — no indexing-policy change in <c>ProvisionAsync</c>.</para>
/// <para>One extra row is fetched as a look-ahead to detect a further page (<c>MaxItemCount = pageSize + 1</c>); the
/// keyset seek uses a strict <c>&gt;</c> on the axis ordering key derived from the continuation cursor.</para>
/// </remarks>
public sealed class CosmosAvailabilityStore : IAvailabilityStore, IAsyncDisposable
{
    private const string ContainerId = "availability";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosAvailabilityStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
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
    public static ValueTask<CosmosAvailabilityStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosAvailabilityStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured Cosmos client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosAvailabilityStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosAvailabilityStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask<(ParsedJsonDocument<AvailabilityEntry> Entry, bool Created)> MakeAvailableAsync(string baseWorkflowId, int versionNumber, string environment, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(actor);

        string itemId = ItemId(baseWorkflowId, versionNumber, environment);

        // Idempotent: if the version is already available in the environment, return the existing entry unchanged.
        byte[]? existing = await this.ReadDocumentAsync(itemId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return (PersistedJson.ToPooledDocument<AvailabilityEntry>(existing), false);
        }

        using ParsedJsonDocument<AvailabilityEntry> draft = AvailabilityEntry.Draft(baseWorkflowId, versionNumber, environment);
        byte[] json = AvailabilitySerialization.SerializeNew(draft.RootElement, actor, this.timeProvider.GetUtcNow(), NewEtag());

        bool conflict;
        using (Stream stream = EnvelopeStream(itemId, baseWorkflowId, versionNumber, environment, json, out ParsedJsonDocument<AvailabilityEntry> document))
        {
            try
            {
                using ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(itemId), cancellationToken: cancellationToken).ConfigureAwait(false);
                conflict = response.StatusCode == HttpStatusCode.Conflict;
                if (!conflict)
                {
                    response.EnsureSuccessStatusCode();
                    return (document, true);
                }
            }
            catch
            {
                document.Dispose();
                throw;
            }

            // A concurrent make-available already created the entry; the built document is unused, so dispose it (exactly
            // once — outside the catch) and re-read the winner below.
            document.Dispose();
        }

        // The create lost a race; re-read and return the existing entry unchanged (idempotent).
        byte[]? raced = conflict ? await this.ReadDocumentAsync(itemId, cancellationToken).ConfigureAwait(false) : null;
        return raced is not null
            ? (PersistedJson.ToPooledDocument<AvailabilityEntry>(raced), false)
            : throw new InvalidOperationException("The availability entry create conflicted but could not be re-read.");
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityEntry>?> GetAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        byte[]? json = await this.ReadDocumentAsync(ItemId(baseWorkflowId, versionNumber, environment), cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<AvailabilityEntry>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WithdrawAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        string itemId = ItemId(baseWorkflowId, versionNumber, environment);
        using ResponseMessage response = await this.container.DeleteItemStreamAsync(itemId, new PartitionKey(itemId), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByVersionAsync(string baseWorkflowId, int versionNumber, int limit, Corvus.Text.Json.Arazzo.Durability.JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        // Single-property cross-partition ORDER BY on environment within a fixed (workflow, version) — the default
        // indexing policy serves it. The cursor seeks strictly past the last environment already returned.
        QueryDefinition query = hasCursor
            ? new QueryDefinition("SELECT c.doc FROM c WHERE c.baseWorkflowId = @b AND c.versionNumber = @v AND c.environment > @ce ORDER BY c.environment")
                .WithParameter("@b", baseWorkflowId)
                .WithParameter("@v", versionNumber)
                .WithParameter("@ce", cursor.Environment)
            : new QueryDefinition("SELECT c.doc FROM c WHERE c.baseWorkflowId = @b AND c.versionNumber = @v ORDER BY c.environment")
                .WithParameter("@b", baseWorkflowId)
                .WithParameter("@v", versionNumber);

        var docs = new PooledDocumentList<AvailabilityEntry>(pageSize);
        try
        {
            bool hasMore = false;
            string lastEnvironment = string.Empty;
            await foreach (ReadOnlyMemory<byte> json in this.QueryDocumentsAsync(query, pageSize + 1, cancellationToken).ConfigureAwait(false))
            {
                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                ParsedJsonDocument<AvailabilityEntry> doc = PersistedJson.ToPooledDocument<AvailabilityEntry>(json.Span);
                lastEnvironment = doc.RootElement.EnvironmentValue;
                docs.Add(doc);
            }

            return hasMore
                ? AvailabilityPage.Create(docs, baseWorkflowId, versionNumber, lastEnvironment)
                : AvailabilityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByEnvironmentAsync(string environment, int limit, Corvus.Text.Json.Arazzo.Durability.JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        // The (baseWorkflowId, numeric versionNumber) total order is collapsed into one orderable string (sortKey), so
        // this is a single-property cross-partition ORDER BY — no Cosmos composite index needed (mirrors the catalog
        // store). The cursor seeks strictly past the last (workflow, version) already returned.
        QueryDefinition query = hasCursor
            ? new QueryDefinition("SELECT c.doc FROM c WHERE c.environment = @e AND c.sortKey > @after ORDER BY c.sortKey")
                .WithParameter("@e", environment)
                .WithParameter("@after", SortKey(cursor.BaseWorkflowId, cursor.VersionNumber))
            : new QueryDefinition("SELECT c.doc FROM c WHERE c.environment = @e ORDER BY c.sortKey")
                .WithParameter("@e", environment);

        var docs = new PooledDocumentList<AvailabilityEntry>(pageSize);
        try
        {
            bool hasMore = false;
            string lastBaseWorkflowId = string.Empty;
            int lastVersionNumber = 0;
            await foreach (ReadOnlyMemory<byte> json in this.QueryDocumentsAsync(query, pageSize + 1, cancellationToken).ConfigureAwait(false))
            {
                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                ParsedJsonDocument<AvailabilityEntry> doc = PersistedJson.ToPooledDocument<AvailabilityEntry>(json.Span);
                lastBaseWorkflowId = doc.RootElement.BaseWorkflowIdValue;
                lastVersionNumber = doc.RootElement.VersionNumberValue;
                docs.Add(doc);
            }

            return hasMore
                ? AvailabilityPage.Create(docs, lastBaseWorkflowId, lastVersionNumber, environment)
                : AvailabilityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
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

    // A deterministic, opaque, Cosmos-id-safe document id from the composite key. The id is also the partition key, so
    // make / get / withdraw are single-partition point operations and a duplicate (workflow, version, environment) make
    // collides on the id and surfaces as a 409 — mirroring the relational backends' composite-primary-key uniqueness.
    private static string ItemId(string baseWorkflowId, int versionNumber, string environment)
    {
        string discriminator = string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId} {versionNumber} {environment}");
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(discriminator), hash);
        return "avail-" + Convert.ToHexStringLower(hash);
    }

    // Collapses (baseWorkflowId, numeric versionNumber) into one orderable string so the by-environment list axis is a
    // single-property ORDER BY: the zero-padded version width keeps the numeric order (2 before 10) under a string sort.
    private static string SortKey(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}{versionNumber:D10}");

    // Serializes the {id, pk, baseWorkflowId, versionNumber, environment, sortKey, doc} envelope into a pooled stream and
    // builds the caller's pooled return document from the same source bytes. The entry doc is embedded verbatim (no SDK
    // serializer); the key parts and the collapsed sortKey are projected so the two list axes can ORDER BY them
    // server-side. On any failure building the stream, the return document is disposed before the exception escapes.
    private static Stream EnvelopeStream(string id, string baseWorkflowId, int versionNumber, string environment, byte[] doc, out ParsedJsonDocument<AvailabilityEntry> document)
    {
        document = PersistedJson.ToPooledDocument<AvailabilityEntry>(doc);
        try
        {
            return CosmosJson.WriteToStream(
                (Id: id, BaseWorkflowId: baseWorkflowId, VersionNumber: versionNumber, Environment: environment, SortKey: SortKey(baseWorkflowId, versionNumber), Doc: doc),
                static (Utf8JsonWriter writer, in (string Id, string BaseWorkflowId, int VersionNumber, string Environment, string SortKey, byte[] Doc) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Id);
                    writer.WriteString("baseWorkflowId"u8, c.BaseWorkflowId);
                    writer.WriteNumber("versionNumber"u8, c.VersionNumber);
                    writer.WriteString("environment"u8, c.Environment);
                    writer.WriteString("sortKey"u8, c.SortKey);

                    // The entry document is itself JSON, so embed it verbatim as a nested value — no base64 wrap (which
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

        // The default indexing policy indexes all paths, which serves both list axes: each is a single-property ORDER BY
        // (environment for by-version; the collapsed sortKey for by-environment), so no composite index is required.
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(ContainerId, "/pk"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosAvailabilityStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosAvailabilityStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    private static bool TryDecodeCursor(Corvus.Text.Json.Arazzo.Durability.JsonString pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        cursor = default;
        if (!pageToken.IsNotUndefined())
        {
            return false;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        return AvailabilityContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
    }

    // Point-reads the entry document's embedded raw UTF-8 bytes (its envelope's `doc` value) for the id, copied out of
    // the response page (the bytes outlive the response), or null if the id is absent.
    private async ValueTask<byte[]?> ReadDocumentAsync(string itemId, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.container.ReadItemStreamAsync(itemId, new PartitionKey(itemId), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse page = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
        ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(page.Memory, DocProperty);
        return doc.IsEmpty ? null : doc.ToArray();
    }

    // Yields the embedded entry document's raw UTF-8 bytes (a slice into the pooled response page) for each result — no
    // base64 decode. The slice is valid only for the duration of the consumer's iteration step; the caller parses each
    // into a pooled document it owns within that step. One extra row beyond the page is fetched as a look-ahead.
    private async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryDocumentsAsync(QueryDefinition query, int maxItemCount, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var options = new QueryRequestOptions { MaxItemCount = maxItemCount };
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