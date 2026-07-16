// <copyright file="CosmosWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="IWorkspaceWorkflowStore"/> (workflow-designer design §4.1): designer working
/// copies persisted as documents in a small <c>workflow_working_copies</c> container so a working copy survives a
/// restart. Each working copy is one document, keyed by its server-minted <c>id</c> alone — the id is globally unique
/// (<c>wc-</c> plus a GUID), so it is both the Cosmos item id and the partition key, and each working copy is its own
/// single-document partition (there is no name/tags composite key as the deployment-environment store has). The working
/// copy's <see cref="WorkspaceWorkflow"/> document is held verbatim as a nested field and its store-owned etag travels
/// inside it; documents are written and read through the Cosmos <em>stream</em> APIs (no SDK serializer), so persistence
/// flows through Corvus.Text.Json.
/// </summary>
/// <remarks>
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory
/// over the single document for an id (a working copy outside reach is reported as absent, non-disclosing), and per row
/// in keyset order for the list. Because the id is the whole key, a management lookup is a point read
/// (<see cref="Container.ReadItemStreamAsync"/>) and the list keyset-seeks server-side on the id. Optimistic concurrency
/// is the etag carried inside the document, checked in process by <see cref="WorkspaceWorkflowSerialization"/> (a stale
/// save/delete throws <see cref="WorkspaceWorkflowConflictException"/>) rather than a Cosmos <c>If-Match</c> — mirroring
/// the sibling backends, so the shared conformance contract holds. The document is carried bytes-to-bytes (#803):
/// documents flow through <see cref="ParsedJsonDocument{T}"/> and the pooled stream APIs
/// (<see cref="CosmosJson.WriteToStream{TContext}(in TContext, PersistedJson.WriteCallback{TContext})"/> /
/// <see cref="CosmosJson.ReadAllAsync"/>), never a per-op detached clone.
/// </remarks>
public sealed class CosmosWorkspaceWorkflowStore : IWorkspaceWorkflowStore, IAsyncDisposable
{
    private const string ContainerId = "workflow_working_copies";

    private static readonly byte[] DocProperty = "doc"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosWorkspaceWorkflowStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
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
    public static ValueTask<CosmosWorkspaceWorkflowStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosWorkspaceWorkflowStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured Cosmos client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosWorkspaceWorkflowStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosWorkspaceWorkflowStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AddAsync(WorkspaceWorkflow draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // The durable backend mints its own opaque, globally unique id; it is Cosmos-id-safe (it holds only ASCII
        // lowercase hex), so it doubles as the item id and the single-document partition key with no discriminator hash.
        // A create does not reach-check (the deployment has already stamped the creator's tenant tag onto the draft).
        string id = "wc-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        byte[] json = WorkspaceWorkflowSerialization.SerializeNew(draft, id, actor, this.timeProvider.GetUtcNow(), NewEtag());

        using Stream stream = EnvelopeStream(id, json, out ParsedJsonDocument<WorkspaceWorkflow> document);
        try
        {
            using ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
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
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> GetAsync(string id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        byte[]? json = await this.FindForManagementAsync(id, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceWorkflowPage> ListAsync(AccessContext context, int limit, Corvus.Text.Json.Arazzo.Durability.JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Id, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = WorkspaceWorkflowContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // Keyset seek in the id total order — the id is globally unique, so it is the whole key (the tie-breaker is
        // empty) and is fully seekable/orderable server-side (it is both the item id and the partition key). The query
        // seeks strictly past the cursor id; reach cannot be pushed to Cosmos so it is a per-row check applied in memory
        // as we stream, and streaming stops once a further visible row beyond the page is seen.
        QueryDefinition query = hasCursor
            ? new QueryDefinition("SELECT c.doc FROM c WHERE c.pk > @id ORDER BY c.pk").WithParameter("@id", cursor.Id)
            : new QueryDefinition("SELECT c.doc FROM c ORDER BY c.pk");

        var docs = new PooledDocumentList<WorkspaceWorkflow>(pageSize);
        bool hasMore = false;
        try
        {
            string lastId = string.Empty;
            await foreach (ReadOnlyMemory<byte> json in this.QueryDocumentsAsync(query, cancellationToken).ConfigureAwait(false))
            {
                ParsedJsonDocument<WorkspaceWorkflow> cand = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json.Span);
                bool kept = false;
                try
                {
                    string id = cand.RootElement.IdValue;
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
                    lastId = id;
                }
                finally
                {
                    if (!kept)
                    {
                        cand.Dispose();
                    }
                }
            }

            return hasMore
                ? WorkspaceWorkflowPage.Create(docs, lastId, string.Empty)
                : WorkspaceWorkflowPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> UpdateAsync(string id, WorkspaceWorkflow draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        byte[]? existing = await this.FindForManagementAsync(id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        // The optimistic-concurrency check is the etag carried inside the document (verified in process here, throwing
        // WorkspaceWorkflowConflictException on a stale save) — not a Cosmos If-Match — so every backend conflicts the
        // same way. The id, provenance, tags, and created-* audit are immutable → the item id and partition are unchanged.
        byte[] json = WorkspaceWorkflowSerialization.SerializeUpdated(existing, id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());

        using Stream stream = EnvelopeStream(id, json, out ParsedJsonDocument<WorkspaceWorkflow> document);
        try
        {
            using ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, id, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
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
    public async ValueTask<bool> DeleteAsync(string id, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        byte[]? existing = await this.FindForManagementAsync(id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            WorkspaceWorkflowSerialization.EnsureEtag(id, expectedEtag, WorkspaceWorkflowSerialization.EtagOf(existing));
        }

        using ResponseMessage response = await this.container.DeleteItemStreamAsync(id, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
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

    // Serializes the {id, pk, doc} envelope into a pooled stream and builds the caller's pooled return document from the
    // same working-copy bytes. The working copy's document is embedded verbatim (no SDK serializer); the id is the item
    // id and (as pk) the partition key ListAsync seeks/orders on. On any failure building the stream, the return document
    // is disposed before the exception escapes.
    private static Stream EnvelopeStream(string id, byte[] doc, out ParsedJsonDocument<WorkspaceWorkflow> document)
    {
        document = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(doc);
        try
        {
            return CosmosJson.WriteToStream(
                (Id: id, Doc: doc),
                static (Utf8JsonWriter writer, in (string Id, byte[] Doc) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("id"u8, c.Id);
                    writer.WriteString("pk"u8, c.Id);

                    // The working-copy document is itself JSON, so embed it verbatim as a nested value — no base64 wrap
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

    private static CosmosWorkspaceWorkflowStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosWorkspaceWorkflowStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    // Finds the single working copy with the given id the caller's reach for the verb admits, returning its bytes (the id
    // is the whole key, so a point read on the id partition suffices — a NotFound status is the absent case, not an
    // exception). A working copy outside reach is invisible (non-disclosing).
    private async ValueTask<byte[]?> FindForManagementAsync(string id, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.container.ReadItemStreamAsync(id, new PartitionKey(id), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
        ReadOnlyMemory<byte> doc = CosmosJson.GetRawValue(payload.Memory, DocProperty);
        if (doc.IsEmpty)
        {
            return null;
        }

        using ParsedJsonDocument<WorkspaceWorkflow> candidate = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(doc.Span);
        if (!context.Admits(verb, candidate.RootElement.ManagementTagsValue))
        {
            return null;
        }

        // The bytes outlive the pooled response payload (the caller may update/delete from them), so copy them out.
        return doc.ToArray();
    }

    // Yields the embedded working-copy document's raw UTF-8 bytes (a slice into the pooled response page) for each result
    // — no base64 decode. The slice is valid only for the duration of the consumer's iteration step; the list consumer
    // parses it in place (a kept row's ownership transfers to the page as a fresh pooled document).
    private async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryDocumentsAsync(QueryDefinition query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using FeedIterator iterator = this.container.GetItemQueryStreamIterator(query);
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