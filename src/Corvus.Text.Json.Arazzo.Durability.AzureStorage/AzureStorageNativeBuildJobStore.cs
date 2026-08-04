// <copyright file="AzureStorageNativeBuildJobStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Publishing;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="INativeBuildJobStore"/> — Native-AOT build jobs (ADR 0055). Each job is one Table
/// entity holding its <see cref="NativeBuildJob"/> schema document in a binary <c>Doc</c> property, keyed by the (encoded)
/// target-derived id under a constant partition so a record is a point read by (PartitionKey, RowKey). The filterable fields
/// (status, target tuple, creation instant) are mirrored into entity columns so <see cref="ListAsync(NativeBuildJobQuery, CancellationToken)"/>
/// can apply a server-side filter; ordering is client-side (oldest first) because Table queries are unordered. The id is
/// derived from the target tuple, so <see cref="EnqueueAsync"/> is an idempotent replace upsert, and the etag travels inside
/// the document. Works against Azure Storage and the Azurite emulator. Mirrors <see cref="AzureStorageAvailabilityRequestStore"/>,
/// keyed by the target-derived id rather than a random id.
/// </summary>
/// <remarks>The claim finds the oldest Queued entity, then transitions it to Building under an <c>If-Match</c> on the entity
/// ETag; a concurrent claim that already advanced the entity fails the ETag check (412) and the claim retries the next-oldest,
/// so two workers never claim the same job.</remarks>
public sealed class AzureStorageNativeBuildJobStore : INativeBuildJobStore
{
    private const string JobsTable = "arazzoNativeBuildJobs";
    private const string JobPartition = "job";
    private const string DocumentColumn = "Doc";
    private const string BaseWorkflowIdColumn = "BaseWorkflowId";
    private const string VersionNumberColumn = "VersionNumber";
    private const string EnvironmentColumn = "Environment";
    private const string RuntimeIdentifierColumn = "RuntimeIdentifier";
    private const string StatusColumn = "Status";
    private const string CreatedAtColumn = "CreatedAt";
    private const string LeaseExpiresAtColumn = "LeaseExpiresAt";

    // The bounded number of oldest-Queued candidates a single claim will race for before giving up; each lost race means a
    // concurrent worker won that candidate, so under real contention a claim succeeds well within this budget.
    private const int MaxClaimAttempts = 64;

    // The shared client-side ordering for ListAsync: oldest first by creation instant, then id (Table queries are
    // unordered, so the snapshot is sorted after the (filtered) read).
    private static readonly IComparer<ParsedJsonDocument<NativeBuildJob>> OldestFirst =
        Comparer<ParsedJsonDocument<NativeBuildJob>>.Create(static (a, b) =>
        {
            int byCreated = a.RootElement.CreatedAtValue.CompareTo(b.RootElement.CreatedAtValue);
            if (byCreated != 0)
            {
                return byCreated;
            }

            // Tiebreak on id string-free: compare the JSON values' UTF-8 bytes (no id string is realised per compare).
            using UnescapedUtf8JsonString aId = a.RootElement.Id.GetUtf8String();
            using UnescapedUtf8JsonString bId = b.RootElement.Id.GetUtf8String();
            return aId.Span.SequenceCompareTo(bId.Span);
        });

    private readonly TableClient jobs;
    private readonly TimeProvider timeProvider;

    private AzureStorageNativeBuildJobStore(TableClient jobs, TimeProvider timeProvider)
    {
        this.jobs = jobs;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the native-build-jobs table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the native-build-jobs table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(JobsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageNativeBuildJobStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageNativeBuildJobStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageNativeBuildJobStore>(
            new AzureStorageNativeBuildJobStore(tableService.GetTableClient(JobsTable), timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>> EnqueueAsync(NativeBuildJob draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);
        string id = NativeBuildJob.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = NativeBuildJobSerialization.SerializeNew(id, draft, now, etag);
        var entity = new TableEntity(JobPartition, Enc(id))
        {
            [BaseWorkflowIdColumn] = draft.BaseWorkflowIdValue,
            [VersionNumberColumn] = draft.VersionNumberValue,
            [EnvironmentColumn] = draft.EnvironmentValue,
            [RuntimeIdentifierColumn] = draft.RuntimeIdentifierValue,
            [StatusColumn] = NativeBuildJobStatusNames.Queued,
            [CreatedAtColumn] = now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            [DocumentColumn] = json,
        };

        // Idempotent per target: the id is derived from the tuple, so a repeated enqueue replaces the same (PartitionKey,
        // RowKey) — an upsert that resets the job to Queued (SerializeNew omits startedAt/completedAt/failureReason/claimedBy).
        await this.jobs.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<NativeBuildJob>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<NativeBuildJob>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> ClaimNextQueuedAsync(string claimedBy, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);

        // One `now` guards the lease-expiry filter/re-check and stamps the new lease/startedAt, so the claim is evaluated at a
        // single instant. Lease/created timestamps are the ISO-8601 round-trip ("o") of a UTC instant, so a Table string
        // compare is chronological (the same property the CreatedAt ordering relies on).
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        string nowText = now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

        // Find the CLAIMABLE candidates' (createdAt, id) over a RowKey + CreatedAt projection (no Doc read) with the state
        // filtered server-side, then order oldest-first client-side (Table queries are unordered). A candidate is Queued, or
        // an orphaned Building whose lease has expired (ADR 0056 — a Building entity always carries a LeaseExpiresAt).
        string filter = TableClient.CreateQueryFilter(
            $"(Status eq {NativeBuildJobStatusNames.Queued}) or (Status eq {NativeBuildJobStatusNames.Building} and LeaseExpiresAt le {nowText})");
        var keys = new List<(string CreatedAt, string Id)>();
        await foreach (TableEntity entity in this.jobs
            .QueryAsync<TableEntity>(filter, select: ["RowKey", CreatedAtColumn], cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            if (entity.GetString(CreatedAtColumn) is { } createdAt)
            {
                keys.Add((createdAt, Dec(entity.RowKey)));
            }
        }

        keys.Sort(static (a, b) =>
        {
            int byCreated = string.CompareOrdinal(a.CreatedAt, b.CreatedAt);
            return byCreated != 0 ? byCreated : string.CompareOrdinal(a.Id, b.Id);
        });

        // Try the oldest claimable candidate first, transitioning it to Building under an If-Match on the loaded entity ETag;
        // on a 412 (a concurrent claim already advanced it) retry the next-oldest, so two workers never claim the same job and
        // a reclaim of an orphan supersedes the crashed worker.
        int attempts = 0;
        foreach ((string _, string id) in keys)
        {
            if (attempts++ >= MaxClaimAttempts)
            {
                break;
            }

            NullableResponse<TableEntity> response = await this.jobs
                .GetEntityIfExistsAsync<TableEntity>(JobPartition, Enc(id), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!response.HasValue)
            {
                continue; // gone since the projection read
            }

            TableEntity entity = response.Value!;
            if (entity.GetBinary(DocumentColumn) is not { } doc)
            {
                continue; // no document — skip
            }

            using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(doc.AsMemory());
            if (!current.RootElement.IsClaimable(now))
            {
                continue; // no longer claimable (concurrently claimed with a live lease, or completed) — try the next
            }

            WorkflowEtag etag = NewEtag();
            DateTimeOffset leaseExpiresAt = now + leaseTtl;
            byte[] claimed = NativeBuildJobSerialization.SerializeClaimed(current.RootElement, claimedBy, now, leaseExpiresAt, etag);

            // The immutable target columns carry through from the loaded document so the replaced entity keeps them; Status,
            // the LeaseExpiresAt mirror the claim filters on, and Doc change on the claim.
            var updated = new TableEntity(JobPartition, Enc(id))
            {
                [StatusColumn] = NativeBuildJobStatusNames.Building,
                [DocumentColumn] = claimed,
                [BaseWorkflowIdColumn] = current.RootElement.BaseWorkflowIdValue,
                [VersionNumberColumn] = current.RootElement.VersionNumberValue,
                [EnvironmentColumn] = current.RootElement.EnvironmentValue,
                [RuntimeIdentifierColumn] = current.RootElement.RuntimeIdentifierValue,
                [CreatedAtColumn] = current.RootElement.CreatedAtValue.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
                [LeaseExpiresAtColumn] = leaseExpiresAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            };

            try
            {
                await this.jobs.UpdateEntityAsync(updated, entity.ETag, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
                return PersistedJson.ToPooledDocument<NativeBuildJob>(claimed);
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                // The entity was advanced concurrently (a competing claim) — retry the next-oldest.
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> CompleteAsync(string id, NativeBuildJobCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(doc.AsMemory());
        if (!current.RootElement.HasStatus(NativeBuildJobStatus.Building))
        {
            ThrowHelper.ThrowNativeBuildJobNotBuildingForCompletion(id);
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = NativeBuildJobSerialization.SerializeCompletion(current.RootElement, id, expectedEtag, completion, this.timeProvider.GetUtcNow(), etag);

        // The immutable target columns carry through from the loaded document so the replaced entity keeps them; only Status
        // and Doc change on a completion.
        var entity = new TableEntity(JobPartition, Enc(id))
        {
            [StatusColumn] = NativeBuildJobStatusNames.ToWire(completion.Status),
            [DocumentColumn] = json,
            [BaseWorkflowIdColumn] = current.RootElement.BaseWorkflowIdValue,
            [VersionNumberColumn] = current.RootElement.VersionNumberValue,
            [EnvironmentColumn] = current.RootElement.EnvironmentValue,
            [RuntimeIdentifierColumn] = current.RootElement.RuntimeIdentifierValue,
            [CreatedAtColumn] = current.RootElement.CreatedAtValue.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
        };

        await this.jobs.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<NativeBuildJob>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> RenewLeaseAsync(string id, WorkflowEtag expectedEtag, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        // Parse the existing document NON-COPYING; it must be Building, then the etag is checked (a reclaim would have moved
        // it, so a stale expected etag conflicts) and the lease-extended record written. The lease is mirrored to the
        // LeaseExpiresAt entity column the claim filters on and rides inside the Doc.
        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(doc.AsMemory());
        if (!current.RootElement.HasStatus(NativeBuildJobStatus.Building))
        {
            ThrowHelper.ThrowNativeBuildJobNotBuildingForLeaseRenewal(id);
        }

        DateTimeOffset leaseExpiresAt = this.timeProvider.GetUtcNow() + leaseTtl;
        WorkflowEtag etag = NewEtag();
        byte[] json = NativeBuildJobSerialization.SerializeRenewal(current.RootElement, id, expectedEtag, leaseExpiresAt, etag);

        // The immutable target columns and the Building status carry through; only the LeaseExpiresAt mirror and Doc change.
        var entity = new TableEntity(JobPartition, Enc(id))
        {
            [StatusColumn] = NativeBuildJobStatusNames.Building,
            [DocumentColumn] = json,
            [BaseWorkflowIdColumn] = current.RootElement.BaseWorkflowIdValue,
            [VersionNumberColumn] = current.RootElement.VersionNumberValue,
            [EnvironmentColumn] = current.RootElement.EnvironmentValue,
            [RuntimeIdentifierColumn] = current.RootElement.RuntimeIdentifierValue,
            [CreatedAtColumn] = current.RootElement.CreatedAtValue.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            [LeaseExpiresAtColumn] = leaseExpiresAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
        };

        await this.jobs.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<NativeBuildJob>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<NativeBuildJob>> ListAsync(NativeBuildJobQuery query, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<NativeBuildJob>();
        string? filter = BuildFilter(query);
        await foreach (TableEntity entity in this.jobs.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary(DocumentColumn) is { } bytes)
            {
                list.Add(ParsedJsonDocument<NativeBuildJob>.Parse(bytes.AsMemory()));
            }
        }

        list.Sort(OldestFirst);
        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsTargetReadyAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        // A native point read by (PartitionKey, RowKey) on the derived id, projecting only the Status mirror — no Doc read.
        string id = NativeBuildJob.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        NullableResponse<TableEntity> response = await this.jobs
            .GetEntityIfExistsAsync<TableEntity>(JobPartition, Enc(id), select: [StatusColumn], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.HasValue
            && response.Value!.GetString(StatusColumn) is { } status
            && string.Equals(status, NativeBuildJobStatusNames.Ready, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public async ValueTask<NativeBuildJobPage> ListAsync(NativeBuildJobQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : NativeBuildJobPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to strings (the leaf) only here — createdAt as the ISO-8601 "o"
        // form the CreatedAt column stores (reconstructed from the token's UTC ticks), id as text. Undefined = first page.
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(NativeBuildJobContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (NativeBuildJobContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
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

        // Table Storage has no server-side ORDER BY and the RowKey is Base64(id) (not the (createdAt, id) keyset order), so
        // the order is materialised client-side — but over a PROJECTION of just the keyset fields (RowKey + CreatedAt, no
        // Doc), with the filter applied server-side (those are entity columns). Only the page's documents are then point-read
        // — never every job's Doc. createdAt is the fixed-width ISO "o" form (ordinal == chronological) and id is recovered
        // from RowKey so the compare is byte-ordinal == the in-memory pager's.
        string? filter = BuildFilter(query);
        var keys = new List<(string CreatedAt, string Id)>();
        await foreach (TableEntity entity in this.jobs
            .QueryAsync<TableEntity>(filter, select: ["RowKey", CreatedAtColumn], cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            if (entity.GetString(CreatedAtColumn) is { } createdAt)
            {
                keys.Add((createdAt, Dec(entity.RowKey)));
            }
        }

        keys.Sort(static (a, b) =>
        {
            int byCreated = string.CompareOrdinal(a.CreatedAt, b.CreatedAt);
            return byCreated != 0 ? byCreated : string.CompareOrdinal(a.Id, b.Id);
        });

        // Keyset skip past the cursor, then take one id beyond the page (lookahead) — all in memory over the projection.
        var pageIds = new List<string>(pageSize + 1);
        foreach ((string createdAt, string id) in keys)
        {
            if (cursorCreatedAt is not null)
            {
                int byCreated = string.CompareOrdinal(createdAt, cursorCreatedAt);
                bool after = byCreated > 0 || (byCreated == 0 && string.CompareOrdinal(id, cursorId) > 0);
                if (!after)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }
            }

            pageIds.Add(id);
            if (pageIds.Count > pageSize)
            {
                break; // one id beyond the page → a next page exists
            }
        }

        bool hasMore = pageIds.Count > pageSize;
        int take = hasMore ? pageSize : pageIds.Count;
        var page = new PooledDocumentList<NativeBuildJob>(take);
        try
        {
            for (int i = 0; i < take; i++)
            {
                byte[]? doc = await this.DocumentAsync(pageIds[i], cancellationToken).ConfigureAwait(false);
                if (doc is not null)
                {
                    page.Add(ParsedJsonDocument<NativeBuildJob>.Parse(doc.AsMemory()));
                }
            }

            if (!hasMore || page.Count == 0)
            {
                return NativeBuildJobPage.Create(page);
            }

            NativeBuildJob last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return NativeBuildJobPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(NativeBuildJobQuery query, int cap, CancellationToken cancellationToken)
    {
        // Server-side filter (the same BuildFilter as the list) over a RowKey-only projection — no Doc is read. Table
        // Storage has no COUNT, so bound the first page to cap + 1 and stop there; the (cap+1)th row trips Capped.
        string? filter = BuildFilter(query);
        int count = 0;
        await foreach (TableEntity entity in this.jobs
            .QueryAsync<TableEntity>(filter, maxPerPage: cap + 1, select: ["RowKey"], cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            if (++count > cap)
            {
                return (cap, true);
            }
        }

        return (count, false);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static string Enc(string value)
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    // The inverse of Enc: recovers a job id from its Base64 RowKey so the keyset order can be computed from the projection
    // without reading documents. Only ever applied to RowKeys this store wrote (round-trips exactly).
    private static string Dec(string rowKey)
        => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(rowKey));

    // Builds the OData filter for the optional query criteria (status / target tuple); null when the query is empty so the
    // read is an unfiltered scan of the single partition.
    private static string? BuildFilter(NativeBuildJobQuery query)
    {
        // The column name must be a LITERAL part of the OData filter; only the value is an interpolation hole.
        // TableClient.CreateQueryFilter quotes every hole, so a hole for the column name would emit 'Status' eq 'x'
        // (a quoted literal, not a property reference) — an invalid query condition.
        var conditions = new List<string>(5);
        if (query.Status is { } status)
        {
            conditions.Add(TableClient.CreateQueryFilter($"Status eq {NativeBuildJobStatusNames.ToWire(status)}"));
        }

        if (query.BaseWorkflowId is { } baseWorkflowId)
        {
            conditions.Add(TableClient.CreateQueryFilter($"BaseWorkflowId eq {baseWorkflowId}"));
        }

        if (query.VersionNumber is { } versionNumber)
        {
            conditions.Add(TableClient.CreateQueryFilter($"VersionNumber eq {versionNumber}"));
        }

        if (query.Environment is { } environment)
        {
            conditions.Add(TableClient.CreateQueryFilter($"Environment eq {environment}"));
        }

        if (query.RuntimeIdentifier is { } runtimeIdentifier)
        {
            conditions.Add(TableClient.CreateQueryFilter($"RuntimeIdentifier eq {runtimeIdentifier}"));
        }

        return conditions.Count == 0 ? null : string.Join(" and ", conditions);
    }

    // The record is a point read by (PartitionKey, RowKey); a missing entity surfaces as a null document to the caller.
    private async ValueTask<byte[]?> DocumentAsync(string id, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> response = await this.jobs
            .GetEntityIfExistsAsync<TableEntity>(JobPartition, Enc(id), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.HasValue ? response.Value!.GetBinary(DocumentColumn) : null;
    }
}