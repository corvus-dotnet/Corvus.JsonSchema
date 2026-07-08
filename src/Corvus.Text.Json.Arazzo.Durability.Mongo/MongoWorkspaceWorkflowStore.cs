// <copyright file="MongoWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IWorkspaceWorkflowStore"/> (workflow-designer design §4.1): designer working copies
/// persisted as documents so a working copy survives a restart. Each working copy is stored as its
/// <see cref="WorkspaceWorkflow"/> document in a binary <c>doc</c> field, keyed by its server-minted <c>id</c>
/// (globally unique, so the id alone is the <c>_id</c>); its etag is mirrored into a queryable field as well as carried
/// inside the document, for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// Reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory over the
/// single document for an id (a working copy outside reach is reported as absent, non-disclosing), and per row in keyset
/// order for the list. Because every query is on the <c>_id</c> (the automatic primary index), the store needs no
/// secondary index and so no provisioning step: the operational user needs only <c>readWrite</c>. The driver pools
/// connections internally, so the store is naturally concurrent. The document is carried bytes-to-bytes (#803): rows
/// bind the raw JSON as a BSON binary and read it straight back, never a per-op re-parse into BSON.
/// </remarks>
public sealed class MongoWorkspaceWorkflowStore : IWorkspaceWorkflowStore, IAsyncDisposable
{
    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> workingCopies;

    private MongoWorkspaceWorkflowStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.workingCopies = database.GetCollection<BsonDocument>("workspaceWorkflows");
    }

    /// <summary>Opens the store for operation against a database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoWorkspaceWorkflowStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoWorkspaceWorkflowStore>(new MongoWorkspaceWorkflowStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoWorkspaceWorkflowStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoWorkspaceWorkflowStore>(new MongoWorkspaceWorkflowStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AddAsync(WorkspaceWorkflow draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // The durable backend mints its own opaque id (the reference in-memory store's ids are creation-sequential; the
        // id is opaque to clients either way). The id is pure ASCII, so Mongo's default (simple binary) string ordering
        // over the _id sorts it ordinally, matching the keyset pager's id compare.
        string id = "wc-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = WorkspaceWorkflowSerialization.SerializeNew(draft, id, actor, this.timeProvider.GetUtcNow(), etag);
        var document = new BsonDocument
        {
            ["_id"] = id,
            ["etag"] = etag.Value!,
            ["doc"] = new BsonBinaryData(json),
        };
        await this.workingCopies.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
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
    public async ValueTask<WorkspaceWorkflowPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
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

        // Keyset seek past the cursor id in _id order — an indexed range scan over the automatic primary key, not a
        // collection load (the id is globally unique, so it is the whole total order and the tie-breaker is empty). A
        // matching ascending sort makes _id both the seek key and the stable total order, so the page boundary is the row
        // key we hand back.
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = hasCursor ? b.Gt("_id", cursor.Id) : b.Empty;
        SortDefinition<BsonDocument> sort = Builders<BsonDocument>.Sort.Ascending("_id");

        var docs = new PooledDocumentList<WorkspaceWorkflow>(pageSize);
        bool hasMore = false;
        try
        {
            // Reach (§14.2) is a per-row predicate applied in memory as we stream; the cursor is consumed only until the
            // page fills, so we read ≈ pageSize / selectivity rows rather than the whole collection. A FURTHER visible row
            // beyond the page is the signal to emit a continuation token — the row key of the last *included* working copy.
            using IAsyncCursor<BsonDocument> mongoCursor = await this.workingCopies.Find(filter).Sort(sort).ToCursorAsync(cancellationToken).ConfigureAwait(false);
            string lastId = string.Empty;
            bool stop = false;
            while (!stop && await mongoCursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (BsonDocument document in mongoCursor.Current)
                {
                    byte[] json = document["doc"].AsBsonBinaryData.Bytes;
                    ParsedJsonDocument<WorkspaceWorkflow> cand = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
                    bool kept = false;
                    try
                    {
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
                            stop = true;
                            break;
                        }

                        docs.Add(cand);
                        kept = true;
                        lastId = document["_id"].AsString;
                    }
                    finally
                    {
                        if (!kept)
                        {
                            cand.Dispose();
                        }
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

        byte[] json = WorkspaceWorkflowSerialization.SerializeUpdated(existing, id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var update = Builders<BsonDocument>.Update
            .Set("etag", WorkspaceWorkflowSerialization.EtagOf(json).Value!)
            .Set("doc", new BsonBinaryData(json)); // the id, provenance, and tags are immutable → _id unchanged
        await this.workingCopies.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            update,
            options: null,
            cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
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

        await this.workingCopies.DeleteOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient && this.client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return default;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Finds the single working copy with the given id the caller's reach for the verb admits, returning its bytes (the id
    // is the sole key — the _id — so a scalar lookup suffices). A working copy outside reach is invisible (non-disclosing).
    private async ValueTask<byte[]?> FindForManagementAsync(string id, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        BsonDocument? document = await this.workingCopies
            .Find(Builders<BsonDocument>.Filter.Eq("_id", id))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        byte[] json = document["doc"].AsBsonBinaryData.Bytes;
        using ParsedJsonDocument<WorkspaceWorkflow> candidate = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        return context.Admits(verb, candidate.RootElement.ManagementTagsValue) ? json : null;
    }
}