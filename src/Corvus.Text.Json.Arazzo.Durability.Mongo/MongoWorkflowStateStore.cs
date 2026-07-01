// <copyright file="MongoWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/>. Each run is a
/// document holding the opaque checkpoint (binary) plus the projected index fields; optimistic concurrency
/// maps to a version field (a conditional replace) and the single-owner lease to a small leases collection (a
/// conditional upsert that collides on a held lease).
/// </summary>
/// <remarks>
/// The driver pools connections internally, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, string, CancellationToken)"/>.
/// </remarks>
public sealed class MongoWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IWorkflowDispatchIndex, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const string PendingStatus = nameof(WorkflowRunStatus.Pending);
    private const string RunningStatus = nameof(WorkflowRunStatus.Running);

    private readonly IMongoClient client;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;
    private readonly IMongoCollection<BsonDocument> runs;
    private readonly IMongoCollection<BsonDocument> leases;

    private MongoWorkflowStateStore(IMongoClient client, string databaseName, TimeProvider timeProvider, bool ownsClient)
    {
        this.client = client;
        this.timeProvider = timeProvider;
        this.ownsClient = ownsClient;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.runs = database.GetCollection<BsonDocument>("workflow_runs");
        this.leases = database.GetCollection<BsonDocument>("workflow_leases");
    }

    /// <summary>
    /// Provisions the store's indexes. Creating indexes requires the <c>createIndex</c> privilege, so run this
    /// once at deploy/migration time, separately from the least-privileged user used to
    /// <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/> the store for operation. (Collections themselves are created lazily on first
    /// write, so the operational user needs only <c>readWrite</c>.)
    /// </summary>
    /// <param name="connectionString">A MongoDB connection string for a user permitted to create indexes.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the indexes exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var client = new MongoClient(connectionString);
        await using var store = new MongoWorkflowStateStore(client, databaseName, TimeProvider.System, ownsClient: true);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned database.</summary>
    /// <remarks>
    /// This creates no indexes, so it is safe to use a least-privileged operational user granted only
    /// <c>readWrite</c> on the database. Call <see cref="PrepareAsync(string, string, CancellationToken)"/> once beforehand to create the indexes.
    /// </remarks>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoWorkflowStateStore> ConnectAsync(
        string connectionString,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoWorkflowStateStore>(new MongoWorkflowStateStore(client, databaseName, timeProvider ?? TimeProvider.System, ownsClient: true));
    }

    /// <summary>Provisions the store's indexes over a caller-supplied client.</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example one whose <c>MongoClientSettings</c> use an
    /// OIDC/managed-identity or AWS-IAM credential — so provisioning runs under a deliberate credential rather
    /// than one embedded in a connection string. The caller retains ownership of the client.
    /// </remarks>
    /// <param name="client">A configured MongoDB client permitted to create indexes.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the indexes exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(
        IMongoClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        await using var store = new MongoWorkflowStateStore(client, databaseName, TimeProvider.System, ownsClient: false);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a least-privileged (<c>readWrite</c>)
    /// OIDC/managed-identity credential — so the store runs under a least-privileged principal. This creates
    /// no indexes; call <see cref="PrepareAsync(IMongoClient, string, CancellationToken)"/> once beforehand.
    /// </remarks>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoWorkflowStateStore> ConnectAsync(
        IMongoClient client,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoWorkflowStateStore>(new MongoWorkflowStateStore(client, databaseName, timeProvider ?? TimeProvider.System, ownsClient: false));
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
        => this.SaveCoreAsync(id, checkpointUtf8.ToArray(), index, expected, cancellationToken);

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunId id, byte[] checkpoint, WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        if (expected.IsNone)
        {
            BsonDocument document = BuildDocument(id, checkpoint, index, version: 1);
            try
            {
                await this.runs.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                throw new WorkflowConflictException(id, expected);
            }

            return new WorkflowEtag("1");
        }

        long expectedVersion = long.Parse(expected.Value!, CultureInfo.InvariantCulture);
        BsonDocument replacement = BuildDocument(id, checkpoint, index, expectedVersion + 1);
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", id.Value),
            Builders<BsonDocument>.Filter.Eq("version", expectedVersion));
        ReplaceOneResult result = await this.runs.ReplaceOneAsync(filter, replacement, options: (ReplaceOptions?)null, cancellationToken).ConfigureAwait(false);
        if (result.MatchedCount == 0)
        {
            throw new WorkflowConflictException(id, expected);
        }

        return new WorkflowEtag((expectedVersion + 1).ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", id.Value);
        BsonDocument? document = await this.runs.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        byte[] checkpoint = document["checkpoint"].AsBsonBinaryData.Bytes;
        var etag = new WorkflowEtag(document["version"].AsInt64.ToString(CultureInfo.InvariantCulture));
        return new WorkflowCheckpoint(checkpoint, etag);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + ttl;
        string token = Guid.NewGuid().ToString("N");

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", id.Value),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Lte("expiresAt", now.ToUnixTimeMilliseconds()),
                Builders<BsonDocument>.Filter.Eq("owner", owner)));
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("owner", owner)
            .Set("token", token)
            .Set("expiresAt", expiresAt.ToUnixTimeMilliseconds());
        try
        {
            await this.leases.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
            return new WorkflowLease(id, owner, token, expiresAt);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // A lease already exists and is held by another owner (the conditional upsert collided on _id).
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", lease.RunId.Value),
            Builders<BsonDocument>.Filter.Eq("token", lease.Token));
        await this.leases.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", id.Value);
        await this.runs.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        await this.leases.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("status", SuspendedStatus),
            Builders<BsonDocument>.Filter.Ne<BsonValue>("dueAt", BsonNull.Value),
            Builders<BsonDocument>.Filter.Lte("dueAt", before.ToUnixTimeMilliseconds()));
        using IAsyncCursor<BsonDocument> cursor = await this.runs.Find(filter).Project(IdOnly).ToCursorAsync(cancellationToken).ConfigureAwait(false);
        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (BsonDocument document in cursor.Current)
            {
                yield return new WorkflowRunId(document["_id"].AsString);
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.And(
            b.Eq("status", SuspendedStatus),
            b.Eq("awaitingChannel", channel));
        if (correlationId is not null)
        {
            filter = b.And(filter, b.Or(
                b.Eq<BsonValue>("awaitingCorrelationId", BsonNull.Value),
                b.Eq("awaitingCorrelationId", correlationId)));
        }

        using IAsyncCursor<BsonDocument> cursor = await this.runs.Find(filter).Project(IdOnly).ToCursorAsync(cancellationToken).ConfigureAwait(false);
        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (BsonDocument document in cursor.Current)
            {
                yield return new WorkflowRunId(document["_id"].AsString);
            }
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, CancellationToken cancellationToken)
        => this.QueryClaimableAsync(hostedWorkflowIds, null, now, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, string? runnerEnvironment, DateTimeOffset now, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostedWorkflowIds);
        if (hostedWorkflowIds.Count == 0)
        {
            yield break;
        }

        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.And(
            b.In("status", new[] { PendingStatus, RunningStatus }),
            b.In("workflowId", hostedWorkflowIds));

        // §5.5 environment-scoped dispatch: a run pinned to an environment is claimable only by a runner serving it; an
        // unpinned run (environment absent/null) or an unscoped dispatcher (runnerEnvironment is null) matches anything.
        if (runnerEnvironment is not null)
        {
            filter = b.And(filter, b.Or(
                b.Eq<BsonValue>("environment", BsonNull.Value),
                b.Exists("environment", false),
                b.Eq("environment", runnerEnvironment)));
        }

        // Buffer the candidates so we can run the leases query without yielding inside the cursor.
        var candidates = new List<(string Id, string Status)>();
        bool anyRunning = false;
        using (IAsyncCursor<BsonDocument> cursor = await this.runs.Find(filter).Project(IdAndStatus).ToCursorAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    string status = document["status"].AsString;
                    if (status == RunningStatus)
                    {
                        anyRunning = true;
                    }

                    candidates.Add((document["_id"].AsString, status));
                }
            }
        }

        var heldRunIds = new HashSet<string>();
        if (anyRunning)
        {
            FilterDefinition<BsonDocument> leaseFilter = Builders<BsonDocument>.Filter.Gt("expiresAt", now.ToUnixTimeMilliseconds());
            using IAsyncCursor<BsonDocument> leaseCursor = await this.leases.Find(leaseFilter).Project(IdOnly).ToCursorAsync(cancellationToken).ConfigureAwait(false);
            while (await leaseCursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (BsonDocument document in leaseCursor.Current)
                {
                    heldRunIds.Add(document["_id"].AsString);
                }
            }
        }

        foreach ((string id, string status) in candidates)
        {
            if (status == PendingStatus || (status == RunningStatus && !heldRunIds.Contains(id)))
            {
                yield return new WorkflowRunId(id);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Empty;
        if (query.Status is { } status)
        {
            filter = b.And(filter, b.Eq("status", status.ToString()));
        }

        if (query.WorkflowId is { } workflowId)
        {
            filter = b.And(filter, b.Eq("workflowId", workflowId));
        }

        if (query.CreatedAfter is { } createdAfter)
        {
            filter = b.And(filter, b.Gte("createdAt", createdAfter.ToUnixTimeMilliseconds()));
        }

        if (query.CreatedBefore is { } createdBefore)
        {
            filter = b.And(filter, b.Lt("createdAt", createdBefore.ToUnixTimeMilliseconds()));
        }

        if (query.UpdatedAfter is { } updatedAfter)
        {
            filter = b.And(filter, b.Gte("updatedAt", updatedAfter.ToUnixTimeMilliseconds()));
        }

        if (query.UpdatedBefore is { } updatedBefore)
        {
            filter = b.And(filter, b.Lt("updatedAt", updatedBefore.ToUnixTimeMilliseconds()));
        }

        if (query.CorrelationId is { } cid)
        {
            filter = b.And(filter, b.Eq("correlationId", cid));
        }

        if (!query.Tags.IsEmpty)
        {
            // $all = contains every queried tag; the needle is materialized to strings only here, at the BSON
            // filter-builder leaf the driver requires — the stored row tags are never materialized.
            filter = b.And(filter, b.All("tags", query.Tags.ToList()));
        }

        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            if (WorkflowContinuationToken.Decode(tokenUtf8.Span) is { } after)
            {
                filter = b.And(filter, b.Gt("_id", after));
            }
        }

        // Row-security reach (§14.2) is applied in process over the embedded securityTags array (see the class
        // remarks), so the server-side Limit is dropped when a reach filter is present: stream the _id-ordered
        // cursor and take Limit+1 *matching* rows, preserving keyset paging.
        var listings = new List<WorkflowRunListing>(query.Limit + 1);
        IFindFluent<BsonDocument, BsonDocument> find = this.runs.Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("_id"));
        if (query.Security is null)
        {
            find = find.Limit(query.Limit + 1);
        }

        using IAsyncCursor<BsonDocument> cursor = await find.ToCursorAsync(cancellationToken).ConfigureAwait(false);
        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (BsonDocument document in cursor.Current)
            {
                SecurityTagSet securityTags = ReadSecurityTags(document);
                if (query.Security is { } security && !security.IsSatisfiedBy(securityTags))
                {
                    continue;
                }

                string? correlationId = document["correlationId"].IsBsonNull ? null : document["correlationId"].AsString;
                TagSet tags = MongoTags.Read(document);
                var entry = new WorkflowRunIndexEntry(
                    document["workflowId"].AsString,
                    Enum.Parse<WorkflowRunStatus>(document["status"].AsString),
                    DateTimeOffset.FromUnixTimeMilliseconds(document["createdAt"].AsInt64),
                    DateTimeOffset.FromUnixTimeMilliseconds(document["updatedAt"].AsInt64),
                    document["dueAt"].IsBsonNull ? null : DateTimeOffset.FromUnixTimeMilliseconds(document["dueAt"].AsInt64),
                    document["awaitingChannel"].IsBsonNull ? null : document["awaitingChannel"].AsString,
                    document["awaitingCorrelationId"].IsBsonNull ? null : document["awaitingCorrelationId"].AsString,
                    document["errorType"].IsBsonNull ? null : document["errorType"].AsString,
                    CorrelationId: correlationId,
                    Tags: tags,
                    SecurityTags: securityTags,
                    Environment: document.TryGetValue("environment", out BsonValue environment) && !environment.IsBsonNull ? environment.AsString : null);
                listings.Add(new WorkflowRunListing(new WorkflowRunId(document["_id"].AsString), entry));
                if (listings.Count > query.Limit)
                {
                    return WorkflowContinuationToken.Paginate(listings, query.Limit);
                }
            }
        }

        return WorkflowContinuationToken.Paginate(listings, query.Limit);
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

    private static SecurityTagSet ReadSecurityTags(BsonDocument document)
        => MongoSecurityTags.Read(document);

    private static readonly ProjectionDefinition<BsonDocument> IdOnly = Builders<BsonDocument>.Projection.Include("_id");

    private static readonly ProjectionDefinition<BsonDocument> IdAndStatus = Builders<BsonDocument>.Projection.Include("_id").Include("status");

    private static BsonDocument BuildDocument(WorkflowRunId id, byte[] checkpoint, in WorkflowRunIndexEntry index, long version)
    {
        var document = new BsonDocument
        {
            ["_id"] = id.Value,
            ["checkpoint"] = new BsonBinaryData(checkpoint),
            ["version"] = version,
            ["status"] = index.Status.ToString(),
            ["workflowId"] = index.WorkflowId,
            ["createdAt"] = index.CreatedAt.ToUnixTimeMilliseconds(),
            ["updatedAt"] = index.UpdatedAt.ToUnixTimeMilliseconds(),
            ["dueAt"] = index.DueAt is { } due ? due.ToUnixTimeMilliseconds() : BsonNull.Value,
            ["awaitingChannel"] = (BsonValue?)index.AwaitingChannel ?? BsonNull.Value,
            ["awaitingCorrelationId"] = (BsonValue?)index.AwaitingCorrelationId ?? BsonNull.Value,
            ["errorType"] = (BsonValue?)index.ErrorType ?? BsonNull.Value,
            ["correlationId"] = (BsonValue?)index.CorrelationId ?? BsonNull.Value,
            ["tags"] = index.Tags.IsEmpty ? BsonNull.Value : new BsonArray(index.Tags.ToList()),
            ["securityTags"] = MongoSecurityTags.ToBson(index.SecurityTags),
        };

        // §5.5 run→environment pinning: index the environment only when set, so the field is absent (matches anything)
        // for a run created before pinning — see the environment-scoped claimable predicate.
        if (index.Environment is { } environment)
        {
            document["environment"] = environment;
        }

        return document;
    }

    private async ValueTask EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var due = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status").Ascending("dueAt"));
        var awaiting = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status").Ascending("awaitingChannel").Ascending("awaitingCorrelationId"));
        await this.runs.Indexes.CreateManyAsync([due, awaiting], cancellationToken).ConfigureAwait(false);
    }
}