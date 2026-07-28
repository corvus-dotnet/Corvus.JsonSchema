// <copyright file="MongoWorkflowDeploymentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IWorkflowDeploymentStore"/> — serverless workflow deployments (ADR 0055). Each deployment is a
/// document keyed by its target-derived id holding its <see cref="WorkflowDeployment"/> Corvus.Text.Json schema document as a
/// binary field; the filterable fields (status, target tuple) and the creation timestamp are mirrored into BSON fields so the
/// list query can filter and order oldest-first server-side. The id is derived from the target tuple, so
/// <see cref="EnqueueAsync"/> is an idempotent <c>ReplaceOne</c> upsert, and the etag travels inside the document. Mirrors
/// <see cref="MongoAvailabilityRequestStore"/>, keyed by the target-derived id rather than a random id.
/// </summary>
/// <remarks>The claim is an atomic <c>FindOneAndUpdate</c> conditioned on <c>status = Queued</c>: it transitions exactly one
/// oldest queued deployment to Deploying, so two workers never claim the same deployment (on a lost race it retries the
/// next-oldest).</remarks>
public sealed class MongoWorkflowDeploymentStore : IWorkflowDeploymentStore, IAsyncDisposable
{
    // The bounded number of oldest-Queued candidates a single claim will race for before giving up; each lost race means a
    // concurrent worker won that candidate, so under real contention a claim succeeds well within this budget.
    private const int MaxClaimAttempts = 64;

    // Oldest-first (creation order): by createdAt then id, as the server-side sort.
    private static readonly SortDefinition<BsonDocument> OldestFirst =
        Builders<BsonDocument>.Sort.Ascending("createdAt").Ascending("_id");

    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> deployments;

    private MongoWorkflowDeploymentStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.deployments = database.GetCollection<BsonDocument>("workflow_deployments");
    }

    /// <summary>Opens the store for operation against a database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoWorkflowDeploymentStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoWorkflowDeploymentStore>(new MongoWorkflowDeploymentStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoWorkflowDeploymentStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoWorkflowDeploymentStore>(new MongoWorkflowDeploymentStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>> EnqueueAsync(WorkflowDeployment draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);
        string id = WorkflowDeployment.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = WorkflowDeploymentSerialization.SerializeNew(id, draft, now, etag);
        var document = new BsonDocument
        {
            ["_id"] = id,
            ["baseWorkflowId"] = draft.BaseWorkflowIdValue,
            ["versionNumber"] = draft.VersionNumberValue,
            ["environment"] = draft.EnvironmentValue,
            ["runtimeIdentifier"] = draft.RuntimeIdentifierValue,
            ["status"] = WorkflowDeploymentStatusNames.Queued,
            ["createdAt"] = now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            ["leaseExpiresAt"] = BsonNull.Value,
            ["doc"] = new BsonBinaryData(json),
        };

        // Idempotent per target: the id is derived from the tuple, so a repeated enqueue replaces the same document — an
        // upsert that resets the deployment to Queued (SerializeNew omits startedAt/completedAt/failureReason/functionUrl/
        // claimedBy). A (re-)enqueue resets the deployment to Queued, so its lease is cleared (BsonNull); a Queued deployment
        // holds no lease.
        await this.deployments.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), document, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkflowDeployment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<WorkflowDeployment>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> ClaimNextQueuedAsync(string claimedBy, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);
        var b = Builders<BsonDocument>.Filter;

        // One `now` guards the lease-expiry filter and stamps the new lease/startedAt, so the claim is evaluated at a single
        // instant. Lease/created timestamps are the ISO-8601 round-trip ("o") of a UTC instant, so a lexicographic (BSON
        // string) compare is chronological (the same property the createdAt ordering relies on).
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        string nowText = now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

        // A deployment is claimable when it is Queued, or it is an orphaned Deploying whose lease has expired (ADR 0056). The
        // lease is a top-level string field: BsonNull for a Queued deployment, so a $lte string compare (which brackets by
        // type) does not match null — the explicit null branch covers it.
        FilterDefinition<BsonDocument> claimable = b.Or(
            b.Eq("status", WorkflowDeploymentStatusNames.Queued),
            b.And(
                b.Eq("status", WorkflowDeploymentStatusNames.Deploying),
                b.Or(b.Eq("leaseExpiresAt", BsonNull.Value), b.Lte("leaseExpiresAt", nowText))));

        for (int attempt = 0; attempt < MaxClaimAttempts; attempt++)
        {
            // Find the oldest claimable deployment (server-side (createdAt, _id) order).
            BsonDocument? oldest = await this.deployments
                .Find(claimable)
                .Sort(OldestFirst)
                .Limit(1)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (oldest is null)
            {
                return null; // nothing claimable
            }

            string id = oldest["_id"].AsString;
            byte[] document = oldest["doc"].AsBsonBinaryData.Bytes;
            DateTimeOffset leaseExpiresAt = now + leaseTtl;
            using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(document.AsMemory());
            WorkflowEtag etag = NewEtag();
            byte[] claimed = WorkflowDeploymentSerialization.SerializeClaimed(current.RootElement, claimedBy, now, leaseExpiresAt, etag);

            // Atomic compare-and-set: FindOneAndUpdate conditioned on the same claimable predicate transitions exactly this id
            // to Deploying in one server operation, stamping the new lease. Two workers racing the same id: only the first
            // matches (it bumps the lease into the future), so the other's filter no longer matches and it retries the
            // next-oldest — two workers never claim the same deployment, and a reclaim of an orphan supersedes the crashed
            // worker.
            UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
                .Set("status", WorkflowDeploymentStatusNames.Deploying)
                .Set("leaseExpiresAt", leaseExpiresAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture))
                .Set("doc", new BsonBinaryData(claimed));
            BsonDocument? updated = await this.deployments
                .FindOneAndUpdateAsync(
                    b.And(b.Eq("_id", id), claimable),
                    update,
                    new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
                    cancellationToken)
                .ConfigureAwait(false);
            if (updated is not null)
            {
                return PersistedJson.ToPooledDocument<WorkflowDeployment>(claimed);
            }

            // Lost the race for this id (a concurrent worker claimed it) — retry with the next-oldest.
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> CompleteAsync(string id, WorkflowDeploymentCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(doc.AsMemory());
        if (!current.RootElement.HasStatus(WorkflowDeploymentStatus.Deploying))
        {
            ThrowHelper.ThrowWorkflowDeploymentNotDeployingForCompletion(id);
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = WorkflowDeploymentSerialization.SerializeCompletion(current.RootElement, id, expectedEtag, completion, this.timeProvider.GetUtcNow(), etag);
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("status", WorkflowDeploymentStatusNames.ToWire(completion.Status))
            .Set("doc", new BsonBinaryData(json));
        await this.deployments.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), update, options: null, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkflowDeployment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> RenewLeaseAsync(string id, WorkflowEtag expectedEtag, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        // Parse the existing document NON-COPYING; it must be Deploying, then the etag is checked (a reclaim would have moved
        // it, so a stale expected etag conflicts) and the lease-extended record written. The lease is mirrored to the
        // top-level field the claim filters on and rides inside the embedded doc.
        using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(doc.AsMemory());
        if (!current.RootElement.HasStatus(WorkflowDeploymentStatus.Deploying))
        {
            ThrowHelper.ThrowWorkflowDeploymentNotDeployingForLeaseRenewal(id);
        }

        DateTimeOffset leaseExpiresAt = this.timeProvider.GetUtcNow() + leaseTtl;
        WorkflowEtag etag = NewEtag();
        byte[] json = WorkflowDeploymentSerialization.SerializeRenewal(current.RootElement, id, expectedEtag, leaseExpiresAt, etag);
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("leaseExpiresAt", leaseExpiresAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture))
            .Set("doc", new BsonBinaryData(json));
        await this.deployments.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), update, options: null, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkflowDeployment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<WorkflowDeployment>> ListAsync(WorkflowDeploymentQuery query, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = BuildFilter(query);

        var list = new PooledDocumentList<WorkflowDeployment>();
        List<BsonDocument> documents = await this.deployments.Find(filter).Sort(OldestFirst).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            list.Add(ParsedJsonDocument<WorkflowDeployment>.Parse(document["doc"].AsBsonBinaryData.Bytes.AsMemory()));
        }

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsDeployedAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        // A native indexed point read on the derived id (the target tuple maps to a unique _id) — never a scan. Project only
        // the status mirror so no document is materialised.
        string id = WorkflowDeployment.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        BsonDocument? projected = await this.deployments
            .Find(Builders<BsonDocument>.Filter.Eq("_id", id))
            .Project<BsonDocument>(Builders<BsonDocument>.Projection.Include("status"))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return projected is not null && string.Equals(projected["status"].AsString, WorkflowDeploymentStatusNames.Deployed, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowDeploymentPage> ListAsync(WorkflowDeploymentQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : WorkflowDeploymentPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the Mongo filter needs (the leaf) only here —
        // createdAt as the ISO-8601 "o" form the createdAt field stores (reconstructed from the token's UTC ticks).
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(WorkflowDeploymentContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (WorkflowDeploymentContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
                {
                    cursorCreatedAt = new DateTime(cursorTicks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
                    cursorId = Encoding.UTF8.GetString(cursorIdUtf8);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        var b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = BuildFilter(query);

        if (cursorCreatedAt is not null)
        {
            // Keyset seek strictly past (createdAt, id): BSON compares strings by bytes, so the ISO createdAt order is
            // chronological and _id is byte-ordinal — the same total order OldestFirst / the in-memory pager uses.
            filter &= b.Or(
                b.Gt("createdAt", cursorCreatedAt),
                b.And(b.Eq("createdAt", cursorCreatedAt), b.Gt("_id", cursorId)));
        }

        // The (createdAt, _id) sort + Limit(n+1) bounds the read to one page + 1 (lookahead) — never the whole queue.
        var page = new PooledDocumentList<WorkflowDeployment>(pageSize);
        try
        {
            List<BsonDocument> documents = await this.deployments
                .Find(filter)
                .Sort(OldestFirst)
                .Limit(pageSize + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasMore = documents.Count > pageSize;
            int take = hasMore ? pageSize : documents.Count;
            for (int i = 0; i < take; i++)
            {
                page.Add(ParsedJsonDocument<WorkflowDeployment>.Parse(documents[i]["doc"].AsBsonBinaryData.Bytes.AsMemory()));
            }

            if (!hasMore)
            {
                return WorkflowDeploymentPage.Create(page);
            }

            WorkflowDeployment last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return WorkflowDeploymentPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(WorkflowDeploymentQuery query, int cap, CancellationToken cancellationToken)
    {
        // Native bounded count: the same BuildFilter as the list, with CountOptions.Limit = cap + 1 so the server stops
        // counting one past the cap; the (cap+1)th match trips Capped — never a full count of the whole queue.
        FilterDefinition<BsonDocument> filter = BuildFilter(query);
        long total = await this.deployments.CountDocumentsAsync(filter, new CountOptions { Limit = cap + 1 }, cancellationToken).ConfigureAwait(false);
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

    // Builds the shared list filter (status / target tuple); a null criterion adds nothing.
    private static FilterDefinition<BsonDocument> BuildFilter(WorkflowDeploymentQuery query)
    {
        var b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Empty;
        if (query.Status is { } status)
        {
            filter &= b.Eq("status", WorkflowDeploymentStatusNames.ToWire(status));
        }

        if (query.BaseWorkflowId is { } baseWorkflowId)
        {
            filter &= b.Eq("baseWorkflowId", baseWorkflowId);
        }

        if (query.VersionNumber is { } versionNumber)
        {
            filter &= b.Eq("versionNumber", versionNumber);
        }

        if (query.Environment is { } environment)
        {
            filter &= b.Eq("environment", environment);
        }

        if (query.RuntimeIdentifier is { } runtimeIdentifier)
        {
            filter &= b.Eq("runtimeIdentifier", runtimeIdentifier);
        }

        return filter;
    }

    private async ValueTask<byte[]?> DocumentAsync(string id, CancellationToken cancellationToken)
    {
        BsonDocument? document = await this.deployments.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["doc"].AsBsonBinaryData.Bytes;
    }
}