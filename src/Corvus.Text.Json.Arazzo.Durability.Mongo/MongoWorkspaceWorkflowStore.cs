// <copyright file="MongoWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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
/// Point reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) in memory over the single
/// document for an id (a working copy outside reach is reported as absent, non-disclosing); list/count push the reach
/// into the query over the multikey-indexed <c>securityTags</c> mirror (the same predicate, applied by the server), so
/// out-of-reach rows never leave the store — which is why the store now has a <see cref="PrepareAsync(string, string, CancellationToken)"/>
/// provisioning step for that index. The driver pools connections internally, so the store is naturally concurrent.
/// The document is carried bytes-to-bytes (#803): rows bind the raw JSON as a BSON binary and read it straight back,
/// never a per-op re-parse into BSON.
/// </remarks>
public sealed class MongoWorkspaceWorkflowStore : IWorkspaceWorkflowStore, IAsyncDisposable
{
    // The §14.2 reach predicate translated to a Mongo filter over the securityTags mirror ({ k, v } array elements).
    // The emitter is immutable, so one instance serves every query.
    private static readonly MongoSecurityRuleEmitter SecurityEmitter = new("securityTags", "k", "v");

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

    /// <summary>Provisions the store's indexes over a connection string.</summary>
    /// <remarks>
    /// Creating indexes requires the <c>createIndex</c> privilege, so run this once at deploy/migration time, separately
    /// from the least-privileged user used to <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/>
    /// the store for operation. (The collection itself is created lazily on first write, so the operational user needs
    /// only <c>readWrite</c>.)
    /// </remarks>
    /// <param name="connectionString">A MongoDB connection string for a user permitted to create indexes.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the indexes exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var client = new MongoClient(connectionString);
        await using var store = new MongoWorkspaceWorkflowStore(client, databaseName, ownsClient: true, TimeProvider.System);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the store's indexes over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client permitted to create indexes.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the indexes exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(IMongoClient client, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        await using var store = new MongoWorkspaceWorkflowStore(client, databaseName, ownsClient: false, TimeProvider.System);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
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

            // The queryable mirror of the management tags the §14.2 list/count reach predicate evaluates server-side;
            // the tags are immutable on save, so the mirror never needs an update-side re-sync.
            ["securityTags"] = MongoSecurityTags.ToBson(draft.ManagementTagsValue),
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

        // The §14.2 read reach pushed into the query over the securityTags mirror (the same predicate context.Admits
        // evaluates, but applied by the server against the multikey index), so out-of-reach rows never leave the
        // store and the server-side limit is a true page bound.
        if (context.Reach(AccessVerb.Read) is { } reach)
        {
            filter = b.And(filter, reach.ToPredicate(SecurityEmitter));
        }

        SortDefinition<BsonDocument> sort = Builders<BsonDocument>.Sort.Ascending("_id");

        var docs = new PooledDocumentList<WorkspaceWorkflow>(pageSize);
        bool hasMore = false;
        try
        {
            // Every row the cursor yields is already admitted, so the loop is a pure page fill; the (pageSize + 1)th
            // row is the lookahead that signals a continuation token — the row key of the last *included* working copy.
            using IAsyncCursor<BsonDocument> mongoCursor = await this.workingCopies.Find(filter).Sort(sort).Limit(pageSize + 1).ToCursorAsync(cancellationToken).ConfigureAwait(false);
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
    public async ValueTask<(int Count, bool Capped)> CountAsync(AccessContext context, int cap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Empty;
        if (context.Reach(AccessVerb.Read) is { } reach)
        {
            filter = reach.ToPredicate(SecurityEmitter);
        }

        // A native bounded count: the same reach predicate the list pushes down, with the server told to stop counting
        // one row past the cap — the (cap + 1)th admitted row trips Capped.
        long total = await this.workingCopies.CountDocumentsAsync(
            filter,
            new CountOptions { Limit = cap + 1 },
            cancellationToken).ConfigureAwait(false);
        return total > cap ? (cap, true) : ((int)total, false);
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

    private async ValueTask EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        // Multikey index over the securityTags mirror so the pushed-down §14.2 reach predicate is an index lookup, not
        // a collection scan.
        var bySecurityTags = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("securityTags.k").Ascending("securityTags.v"));
        await this.workingCopies.Indexes.CreateOneAsync(bySecurityTags, options: null, cancellationToken).ConfigureAwait(false);
    }

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