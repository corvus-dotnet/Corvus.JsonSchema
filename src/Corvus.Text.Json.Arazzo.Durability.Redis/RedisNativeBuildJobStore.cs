// <copyright file="RedisNativeBuildJobStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="INativeBuildJobStore"/> — Native-AOT build jobs (ADR 0055) persisted for a distributed host.
/// Each job is stored verbatim as its <see cref="NativeBuildJob"/> Corvus.Text.Json document under a per-record key, with a
/// single set holding every job id for enumeration and a keyset sorted-set index for oldest-first paging. The id is derived
/// from the target tuple, so <see cref="EnqueueAsync"/> is an idempotent overwrite, and the etag travels inside the document.
/// Mirrors <see cref="RedisAvailabilityRequestStore"/>, keyed by the target-derived id rather than a random id.
/// </summary>
/// <remarks>The claim walks the keyset index oldest-first and transitions the first Queued job to Building under an optimistic
/// compare-and-set (a Redis transaction conditioned on the record's bytes being unchanged); a lost CAS means a concurrent
/// worker claimed it, so the claim retries the next-oldest — two workers never claim the same job.</remarks>
public sealed class RedisNativeBuildJobStore : INativeBuildJobStore, IAsyncDisposable
{
    private const string JobPrefix = "arazzo:nativebuildjob:";
    private const string JobIndexKey = "arazzo:nativebuildjobs";

    // A keyset index for native oldest-first paging: a single zero-scored sorted set whose members are
    // "{createdAtIso}\0{id}", so ZRANGEBYLEX returns them in (createdAt, id) order — the same total order the in-memory
    // pager uses. The unpaged ListAsync(query) still uses the flat id set above; this index feeds the paged seam and the claim.
    private const string JobByCreatedKey = "arazzo:nativebuildjobs:bycreated";

    private const char MemberSeparator = '\0';

    // Singleton comparer (created once) for the client-side snapshot ordering, since the flat id set is unordered:
    // oldest-first by creation instant then id.
    private static readonly IComparer<ParsedJsonDocument<NativeBuildJob>> ByCreatedThenId =
        Comparer<ParsedJsonDocument<NativeBuildJob>>.Create(static (a, b) =>
        {
            if (a.RootElement.CreatedAtValue != b.RootElement.CreatedAtValue)
            {
                return a.RootElement.CreatedAtValue.CompareTo(b.RootElement.CreatedAtValue);
            }

            // Tiebreak on id string-free: compare the JSON values' UTF-8 bytes (no id string is realised per compare).
            using UnescapedUtf8JsonString aId = a.RootElement.Id.GetUtf8String();
            using UnescapedUtf8JsonString bId = b.RootElement.Id.GetUtf8String();
            return aId.Span.SequenceCompareTo(bId.Span);
        });

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisNativeBuildJobStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.timeProvider = timeProvider;
        this.ownsConnection = ownsConnection;
    }

    /// <summary>Verifies the store can be reached; Redis needs no schema provisioning.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once connectivity is confirmed.</returns>
    public static async ValueTask PrepareAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        await using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
    }

    /// <summary>Opens a native-build-job store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisNativeBuildJobStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisNativeBuildJobStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates a native-build-job store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisNativeBuildJobStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisNativeBuildJobStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>> EnqueueAsync(NativeBuildJob draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        string id = NativeBuildJob.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // A re-enqueue resets createdAt (SerializeNew stamps a fresh instant), so retire any stale keyset member the prior
        // enqueue left (a member embeds the createdAt) before adding the new one — otherwise the id would linger in the
        // index under its old instant. Unlike the availability store (whose createdAt is immutable), this store maintains it.
        using (Lease<byte>? existing = await this.database.StringGetLeaseAsync(JobPrefix + id).ConfigureAwait(false))
        {
            if (existing is { Length: > 0 })
            {
                using ParsedJsonDocument<NativeBuildJob> prior = ParsedJsonDocument<NativeBuildJob>.Parse(existing.Memory);
                await this.database.SortedSetRemoveAsync(JobByCreatedKey, KeysetMember(prior.RootElement.CreatedAtValue, id)).ConfigureAwait(false);
            }
        }

        // Serialize once into the pooled buffer the returned document owns; bind its exact bytes as the RedisValue (a
        // ReadOnlyMemory<byte> carries the precise length, so there is no GC document array and no second copy). The
        // document is returned on success, disposed on failure.
        ParsedJsonDocument<NativeBuildJob> doc = NativeBuildJobSerialization.SerializeNewDoc(id, draft, now, etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await this.database.StringSetAsync(JobPrefix + id, utf8).ConfigureAwait(false);
            await this.database.SetAddAsync(JobIndexKey, id).ConfigureAwait(false);
            await this.database.SortedSetAddAsync(JobByCreatedKey, KeysetMember(now, id), 0).ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        // Read into a pooled lease (no GC read array); the returned document must own its buffer, so copy the lease span
        // into an owned pooled document — the lease returns to the pool here.
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(JobPrefix + id).ConfigureAwait(false);
        return lease is { Length: > 0 } ? PersistedJson.ToPooledDocument<NativeBuildJob>(lease.Span) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> ClaimNextQueuedAsync(string claimedBy, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);
        cancellationToken.ThrowIfCancellationRequested();

        // One `now` guards the lease-expiry test (which orphaned Building jobs are reclaimable) and stamps the new
        // lease/startedAt, so the claim is evaluated at a single instant.
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Walk the keyset index oldest-first (ZRANGEBYLEX returns members in (createdAt, id) order). For each CLAIMABLE
        // candidate (Queued, or an orphaned Building whose lease has expired — ADR 0056), transition it to Building under an
        // optimistic CAS: a Redis transaction whose condition is that the record's bytes are still the ones we read. Two
        // workers racing the same record: only the first commits (it changes the value), so the other's condition fails and
        // it retries the next-oldest — two workers never claim the same job.
        RedisValue[] members = await this.database.SortedSetRangeByValueAsync(JobByCreatedKey).ConfigureAwait(false);
        foreach (RedisValue value in members)
        {
            string member = (string)value!;
            int sep = member.IndexOf(MemberSeparator);
            string id = sep < 0 ? member : member[(sep + 1)..];
            RedisValue currentValue = await this.database.StringGetAsync(JobPrefix + id).ConfigureAwait(false);
            if (currentValue.IsNullOrEmpty)
            {
                continue; // indexed but record gone — skip
            }

            byte[] currentBytes = ((byte[]?)currentValue)!; // non-empty (guarded above), so the RedisValue holds bytes
            using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(currentBytes.AsMemory());
            if (!current.RootElement.IsClaimable(now))
            {
                continue; // completed, or Building with a live lease — the claim never changes createdAt/id, so the member stays valid
            }

            WorkflowEtag etag = NewEtag();
            byte[] claimed = NativeBuildJobSerialization.SerializeClaimed(current.RootElement, claimedBy, now, now + leaseTtl, etag);
            ITransaction transaction = this.database.CreateTransaction();
            transaction.AddCondition(Condition.StringEqual(JobPrefix + id, currentValue));
            _ = transaction.StringSetAsync(JobPrefix + id, claimed);
            if (await transaction.ExecuteAsync().ConfigureAwait(false))
            {
                return PersistedJson.ToPooledDocument<NativeBuildJob>(claimed);
            }

            // Lost the race for this record (a concurrent claim changed it) — retry the next-oldest.
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> CompleteAsync(string id, NativeBuildJobCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        // Read the existing document into a pooled lease (no GC read array) and parse it NON-COPYING over the lease (the
        // lease stays alive through the synchronous state + etag check + merge).
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(JobPrefix + id).ConfigureAwait(false);
        if (lease is not { Length: > 0 })
        {
            return null;
        }

        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(lease.Memory);
        if (!current.RootElement.HasStatus(NativeBuildJobStatus.Building))
        {
            ThrowHelper.ThrowNativeBuildJobNotBuildingForCompletion(id);
        }

        WorkflowEtag etag = NewEtag();

        // Serialize the completed result into the pooled buffer the returned document owns and bind its exact bytes (no GC
        // array, no second copy); return on success, dispose on a write failure.
        ParsedJsonDocument<NativeBuildJob> updated = NativeBuildJobSerialization.SerializeCompletionDoc(current.RootElement, id, expectedEtag, completion, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await this.database.StringSetAsync(JobPrefix + id, utf8).ConfigureAwait(false);
            return updated;
        }
        catch
        {
            updated.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> RenewLeaseAsync(string id, WorkflowEtag expectedEtag, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        // Read the existing document into a pooled lease (no GC read array) and parse it NON-COPYING (the lease stays alive
        // through the synchronous state + etag check + merge). The etag is the ownership token: a reclaim would have moved it,
        // so a stale expected etag conflicts.
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(JobPrefix + id).ConfigureAwait(false);
        if (lease is not { Length: > 0 })
        {
            return null;
        }

        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(lease.Memory);
        if (!current.RootElement.HasStatus(NativeBuildJobStatus.Building))
        {
            ThrowHelper.ThrowNativeBuildJobNotBuildingForLeaseRenewal(id);
        }

        DateTimeOffset leaseExpiresAt = this.timeProvider.GetUtcNow() + leaseTtl;
        WorkflowEtag etag = NewEtag();
        ParsedJsonDocument<NativeBuildJob> updated = NativeBuildJobSerialization.SerializeRenewalDoc(current.RootElement, id, expectedEtag, leaseExpiresAt, etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await this.database.StringSetAsync(JobPrefix + id, utf8).ConfigureAwait(false);
            return updated;
        }
        catch
        {
            updated.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<NativeBuildJob>> ListAsync(NativeBuildJobQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var list = new PooledDocumentList<NativeBuildJob>();
        foreach (RedisValue member in await this.database.SetMembersAsync(JobIndexKey).ConfigureAwait(false))
        {
            // Read each candidate into a pooled lease (no GC read array); a list document must own its buffer, so copy the
            // lease span into an owned pooled document — the lease returns to the pool here.
            using Lease<byte>? lease = await this.database.StringGetLeaseAsync(JobPrefix + (string)member!).ConfigureAwait(false);
            if (lease is not { Length: > 0 })
            {
                continue;
            }

            ParsedJsonDocument<NativeBuildJob> document = PersistedJson.ToPooledDocument<NativeBuildJob>(lease.Span);
            if (query.Matches(document.RootElement))
            {
                list.Add(document);
            }
            else
            {
                document.Dispose();
            }
        }

        list.Sort(ByCreatedThenId);
        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsTargetReadyAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);
        cancellationToken.ThrowIfCancellationRequested();

        // A native point read by the derived key (the target tuple maps to a unique record key) — never a scan.
        string id = NativeBuildJob.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(JobPrefix + id).ConfigureAwait(false);
        if (lease is not { Length: > 0 })
        {
            return false;
        }

        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(lease.Memory);
        return current.RootElement.HasStatus(NativeBuildJobStatus.Ready);
    }

    /// <inheritdoc/>
    public async ValueTask<NativeBuildJobPage> ListAsync(NativeBuildJobQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : NativeBuildJobPage.DefaultPageSize;

        // Decode the keyset cursor to the index member the previous page ended at ("{createdAtIso}\0{id}"); the id reifies
        // to a string only here (the leaf). Undefined token = first page.
        string? cursorMember = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(NativeBuildJobContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (NativeBuildJobContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
                {
                    cursorMember = KeysetMember(new DateTimeOffset(cursorTicks, TimeSpan.Zero), Encoding.UTF8.GetString(cursorIdUtf8));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // ZRANGEBYLEX returns the members in (createdAt, id) order (small strings, no docs); only the page's matching
        // documents are then fetched. The status/target filters live in the document, so we walk the order and fetch +
        // filter until the page fills (reads ~ pageSize / selectivity), never every job's document.
        RedisValue[] members = await this.database.SortedSetRangeByValueAsync(JobByCreatedKey).ConfigureAwait(false);
        var page = new PooledDocumentList<NativeBuildJob>(pageSize);
        try
        {
            bool hasMore = false;
            foreach (RedisValue value in members)
            {
                string member = (string)value!;
                if (cursorMember is not null && string.CompareOrdinal(member, cursorMember) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                int sep = member.IndexOf(MemberSeparator);
                string id = sep < 0 ? member : member[(sep + 1)..];
                using Lease<byte>? lease = await this.database.StringGetLeaseAsync(JobPrefix + id).ConfigureAwait(false);
                if (lease is not { Length: > 0 })
                {
                    continue; // indexed but doc gone — skip
                }

                ParsedJsonDocument<NativeBuildJob> document = PersistedJson.ToPooledDocument<NativeBuildJob>(lease.Span);
                if (!query.Matches(document.RootElement))
                {
                    document.Dispose();
                    continue;
                }

                if (page.Count == pageSize)
                {
                    hasMore = true; // one matching row beyond the page → a next page exists
                    document.Dispose();
                    break;
                }

                page.Add(document);
            }

            if (!hasMore)
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
        cancellationToken.ThrowIfCancellationRequested();

        // Bounded scan: read + parse each candidate only to apply the same Matches predicate as the list, disposing it
        // immediately (no PooledDocumentList grows); stop once a (cap+1)th match is seen and report Capped.
        int count = 0;
        foreach (RedisValue member in await this.database.SetMembersAsync(JobIndexKey).ConfigureAwait(false))
        {
            using Lease<byte>? lease = await this.database.StringGetLeaseAsync(JobPrefix + (string)member!).ConfigureAwait(false);
            if (lease is not { Length: > 0 })
            {
                continue;
            }

            using ParsedJsonDocument<NativeBuildJob> document = PersistedJson.ToPooledDocument<NativeBuildJob>(lease.Span);
            if (query.Matches(document.RootElement) && ++count > cap)
            {
                return (cap, true);
            }
        }

        return (count, false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // The keyset index member for a job: "{createdAtIso}\0{id}", with createdAt as the fixed-width ISO-8601 "o" UTC form so
    // a lexicographic (ZRANGEBYLEX) / ordinal compare of members is (createdAt asc, id asc) — the in-memory order.
    private static string KeysetMember(DateTimeOffset createdAt, string id)
        => $"{createdAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)}{MemberSeparator}{id}";
}