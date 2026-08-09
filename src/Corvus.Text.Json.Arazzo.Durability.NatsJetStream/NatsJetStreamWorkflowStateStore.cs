// <copyright file="NatsJetStreamWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Globalization;
using Corvus.Text.Json.Internal;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream key/value-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/>.
/// Each run's value is an envelope (the projected index header plus the opaque checkpoint); the KV entry's
/// native revision is the optimistic-concurrency token, and the single-owner lease lives in a second bucket
/// guarded by the same compare-and-set on revision.
/// </summary>
/// <remarks>
/// Wait/visibility queries scan the bucket's keys and filter on the index header. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class NatsJetStreamWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IWorkflowDispatchIndex, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const string RunsBucket = "arazzo_runs";
    private const string LeasesBucket = "arazzo_leases";

    private static readonly byte[] EmptyLabelValue = [];

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore runs;
    private readonly INatsKVStore leases;
    private readonly INatsKVStore labels;
    private readonly NatsSecurityLabelIndex labelIndex;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamWorkflowStateStore(NatsConnection? ownedConnection, INatsKVStore runs, INatsKVStore leases, INatsKVStore labels, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.runs = runs;
        this.leases = leases;
        this.labels = labels;
        this.labelIndex = new NatsSecurityLabelIndex(labels);
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public bool SupportsRowSecurityFilter => true;

    /// <summary>
    /// Provisions the store's key/value buckets. Creating a KV bucket creates a JetStream stream, which
    /// requires stream-management permissions, so run this once at deploy/migration time, separately from the
    /// least-privileged account used to <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> the store for operation (which needs only
    /// get/put/delete on the buckets' subjects).
    /// </summary>
    /// <param name="url">A NATS server URL for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the buckets exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await using var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(RunsBucket), cancellationToken).ConfigureAwait(false);
        await kv.CreateStoreAsync(new NatsKVConfig(LeasesBucket), cancellationToken).ConfigureAwait(false);
        await kv.CreateStoreAsync(new NatsKVConfig(NatsSecurityLabels.RunsLabelBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation, binding to its already-provisioned key/value buckets.</summary>
    /// <remarks>
    /// This creates no streams/buckets, so it is safe to use a least-privileged account granted only
    /// get/put/delete on the buckets' subjects. Call <see cref="PrepareAsync(string, CancellationToken)"/> once beforehand — with a
    /// stream-management account — to create the buckets.
    /// </remarks>
    /// <param name="url">A NATS server URL (e.g. <c>nats://localhost:4222</c>).</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamWorkflowStateStore> ConnectAsync(
        string url,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore runs = await kv.GetStoreAsync(RunsBucket, cancellationToken).ConfigureAwait(false);
            INatsKVStore leases = await kv.GetStoreAsync(LeasesBucket, cancellationToken).ConfigureAwait(false);
            INatsKVStore labels = await kv.GetStoreAsync(NatsSecurityLabels.RunsLabelBucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamWorkflowStateStore(connection, runs, leases, labels, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Provisions the store's key/value buckets over a caller-supplied connection.</summary>
    /// <remarks>
    /// Supply a connection the caller configured (for example with a creds file, nkey, or token) so
    /// provisioning runs under a deliberate, stream-management-capable account. The caller retains ownership.
    /// </remarks>
    /// <param name="connection">A NATS connection for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the buckets exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(RunsBucket), cancellationToken).ConfigureAwait(false);
        await kv.CreateStoreAsync(new NatsKVConfig(LeasesBucket), cancellationToken).ConfigureAwait(false);
        await kv.CreateStoreAsync(new NatsKVConfig(NatsSecurityLabels.RunsLabelBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store over a caller-supplied connection (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a connection the caller configured — for example with a least-privileged operational account
    /// (get/put/delete on the buckets' subjects) — so the store runs under a least-privileged principal. This
    /// creates no buckets; call <see cref="PrepareAsync(INatsConnection, CancellationToken)"/> once beforehand.
    /// </remarks>
    /// <param name="connection">A NATS connection.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied connection).</returns>
    public static async ValueTask<NatsJetStreamWorkflowStateStore> ConnectAsync(
        INatsConnection connection,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore runs = await kv.GetStoreAsync(RunsBucket, cancellationToken).ConfigureAwait(false);
        INatsKVStore leases = await kv.GetStoreAsync(LeasesBucket, cancellationToken).ConfigureAwait(false);
        INatsKVStore labels = await kv.GetStoreAsync(NatsSecurityLabels.RunsLabelBucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamWorkflowStateStore(ownedConnection: null, runs, leases, labels, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
        => this.SaveCoreAsync(id, Envelope.Encode(index, checkpointUtf8.Span), index.SecurityTags, expected, cancellationToken);

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunId id, byte[] value, SecurityTagSet securityTags, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        // §14.4 label entries, in the ordering that keeps an imprecise index safe: entries are ADDED before the
        // envelope becomes visible and REMOVED after it stops carrying the old tags, so an interrupted save can
        // only leave a stale entry — which the exact reach evaluation discards — never a visible run with no
        // entry, which would hide it from a narrowed query. KV cannot read one field of an envelope, so the
        // previous entries come from the label bucket itself: a subject-filtered key listing on the run's own id
        // token, transferring a handful of keys and never the checkpoint. That reverse lookup also makes the diff
        // self-healing — entries a crashed or conflicted save stranded are swept by the next successful one. A
        // run's security tags are fixed at creation, so the common checkpoint path finds both differences empty.
        HashSet<string> desired = NatsSecurityLabels.EntryKeysFor(securityTags, id.Value);
        HashSet<string> previous = expected.IsNone
            ? []
            : await this.ReadLabelEntryKeysAsync(id.Value, cancellationToken).ConfigureAwait(false);

        foreach (string entryKey in desired)
        {
            if (!previous.Contains(entryKey))
            {
                await this.labels.PutAsync(entryKey, EmptyLabelValue, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        ulong revision;
        try
        {
            revision = expected.IsNone
                ? await this.runs.CreateAsync(id.Value, value, cancellationToken: cancellationToken).ConfigureAwait(false)
                : await this.runs.UpdateAsync(id.Value, value, ulong.Parse(expected.Value!, CultureInfo.InvariantCulture), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVException)
        {
            // The entries added above stay behind as stale — removal here could race the writer that won.
            throw new WorkflowConflictException(id, expected);
        }

        foreach (string entryKey in previous)
        {
            if (!desired.Contains(entryKey))
            {
                await this.PurgeAsync(this.labels, entryKey, cancellationToken).ConfigureAwait(false);
            }
        }

        return new WorkflowEtag(revision.ToString(CultureInfo.InvariantCulture));
    }

    // The label entries the run's id currently occupies, via the reverse subject filters on its id token.
    private async ValueTask<HashSet<string>> ReadLabelEntryKeysAsync(string rowId, CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        await foreach (string entryKey in this.labels.GetKeysAsync(NatsSecurityLabels.RowFilters(rowId), cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            keys.Add(entryKey);
        }

        return keys;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(this.runs, id.Value, cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } value })
        {
            return null;
        }

        byte[] checkpoint = Envelope.DecodeCheckpoint(value);
        return new WorkflowCheckpoint(checkpoint, new WorkflowEtag(entry.Value.Revision.ToString(CultureInfo.InvariantCulture)));
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + ttl;
        string token = Guid.NewGuid().ToString("N");

        // The epoch is advanced from the stored entry rather than supplied, so it counts this run's grants across every
        // instance and every restart (ADR 0065 §6). The entry's own revision is what makes read-then-advance atomic: a
        // concurrent grant moves it, and the conditional update below is refused rather than reusing the epoch it read.
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(this.leases, id.Value, cancellationToken).ConfigureAwait(false);
        try
        {
            if (entry is not { Value: { } current })
            {
                await this.leases.CreateAsync(id.Value, LeaseCodec.Encode(owner, token, expiresAt.ToUnixTimeMilliseconds(), 1), cancellationToken: cancellationToken).ConfigureAwait(false);
                return new WorkflowLease(id, owner, token, expiresAt, 1);
            }

            (string currentOwner, _, long currentExpiresAt, long currentEpoch) = LeaseCodec.Decode(current);
            if (currentExpiresAt > now.ToUnixTimeMilliseconds() && currentOwner != owner)
            {
                return null;
            }

            long granted = currentEpoch + 1;
            byte[] value = LeaseCodec.Encode(owner, token, expiresAt.ToUnixTimeMilliseconds(), granted);
            await this.leases.UpdateAsync(id.Value, value, entry.Value.Revision, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new WorkflowLease(id, owner, token, expiresAt, granted);
        }
        catch (NatsKVException)
        {
            // Another worker created or advanced the lease concurrently.
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> TryExtendLeaseAsync(WorkflowLease lease, TimeSpan extension, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(extension, TimeSpan.Zero);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(this.leases, lease.RunId.Value, cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } current })
        {
            return null;
        }

        // The three ways a presented lease stops being current — superseded, stolen, and lapsed (an administrative fence
        // expires in place, so it lands on the third).
        (string currentOwner, string currentToken, long currentExpiresAt, long currentEpoch) = LeaseCodec.Decode(current);
        if (currentToken != lease.Token || currentOwner != lease.Owner || currentExpiresAt <= now.ToUnixTimeMilliseconds())
        {
            return null;
        }

        // Either way the epoch comes back from the stored entry, never from the presented lease: an extension does not
        // mint one, so what the caller gets is the grant's own epoch.
        if (extension == TimeSpan.Zero)
        {
            return new WorkflowLease(lease.RunId, lease.Owner, lease.Token, DateTimeOffset.FromUnixTimeMilliseconds(currentExpiresAt), currentEpoch);
        }

        DateTimeOffset extendedTo = now + extension;
        byte[] value = LeaseCodec.Encode(lease.Owner, lease.Token, extendedTo.ToUnixTimeMilliseconds(), currentEpoch);
        try
        {
            await this.leases.UpdateAsync(lease.RunId.Value, value, entry.Value.Revision, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new WorkflowLease(lease.RunId, lease.Owner, lease.Token, extendedTo, currentEpoch);
        }
        catch (NatsKVException)
        {
            // The lease was released or advanced between the read and the update.
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(this.leases, lease.RunId.Value, cancellationToken).ConfigureAwait(false);
        if (entry is { Value: { } current })
        {
            (string owner, string token, _, long epoch) = LeaseCodec.Decode(current);
            if (token == lease.Token)
            {
                // Expired in place rather than deleted, so the run's epoch survives its holder handing it back — the
                // ordinary way a grant ends, and the one a counter kept only alongside a live lease would forget. The
                // entry is re-acquirable at once: every reader tests expiresAt > now. DeleteAsync still purges it with
                // the run.
                byte[] released = LeaseCodec.Encode(owner, token, this.timeProvider.GetUtcNow().ToUnixTimeMilliseconds(), epoch);
                await this.leases.UpdateAsync(lease.RunId.Value, released, entry.Value.Revision, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        // Read the run's label entries while it still exists, then drop the run before its entries — the §14.4
        // ordering: an interrupted delete leaves a stale entry, harmless when nothing loads behind it, whereas
        // dropping entries first would strand a still-visible run.
        HashSet<string> entryKeys = await this.ReadLabelEntryKeysAsync(id.Value, cancellationToken).ConfigureAwait(false);

        await this.PurgeAsync(this.runs, id.Value, cancellationToken).ConfigureAwait(false);
        await this.PurgeAsync(this.leases, id.Value, cancellationToken).ConfigureAwait(false);

        foreach (string entryKey in entryKeys)
        {
            await this.PurgeAsync(this.labels, entryKey, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, CancellationToken cancellationToken) => this.QueryDueAsync(before, null, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, string? runnerEnvironment, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long cutoff = before.ToUnixTimeMilliseconds();
        await foreach ((WorkflowRunId runId, WorkflowRunIndexEntry index) in this.ScanAsync(candidates: null, cancellationToken).ConfigureAwait(false))
        {
            // §5.5 environment-scoped timer-resume: a real runner (non-null runnerEnvironment) resumes a due run only when
            // it is pinned to EXACTLY that environment; an unpinned or differently-pinned run is skipped. A null
            // runnerEnvironment is the env-agnostic base overload that surfaces every due run regardless of environment.
            if (!MatchesEnvironment(index.Environment, runnerEnvironment))
            {
                continue;
            }

            if (index.Status == WorkflowRunStatus.Suspended && index.DueAt is { } due && due.ToUnixTimeMilliseconds() <= cutoff)
            {
                yield return runId;
            }
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, CancellationToken cancellationToken) => this.QueryAwaitingAsync(channel, correlationId, null, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, string? runnerEnvironment, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        await foreach ((WorkflowRunId runId, WorkflowRunIndexEntry index) in this.ScanAsync(candidates: null, cancellationToken).ConfigureAwait(false))
        {
            // §5.5 environment-scoped signal-resume: a real runner (non-null runnerEnvironment) resumes an awaiting run only when
            // it is pinned to EXACTLY that environment; an unpinned or differently-pinned run is skipped. A null
            // runnerEnvironment is the env-agnostic base overload that surfaces every awaiting run regardless of environment.
            if (!MatchesEnvironment(index.Environment, runnerEnvironment))
            {
                continue;
            }

            if (index.Status == WorkflowRunStatus.Suspended
                && index.AwaitingChannel == channel
                && (correlationId is null || index.AwaitingCorrelationId is null || index.AwaitingCorrelationId == correlationId))
            {
                yield return runId;
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

        var hosted = new HashSet<string>(hostedWorkflowIds);
        await foreach ((WorkflowRunId runId, WorkflowRunIndexEntry entry) in this.ScanAsync(candidates: null, cancellationToken).ConfigureAwait(false))
        {
            if (!hosted.Contains(entry.WorkflowId))
            {
                continue;
            }

            // §5.5 environment-scoped dispatch: a run pinned to an environment is claimable only by a runner
            // serving it; an unpinned run or an unscoped dispatcher matches anything.
            if (!MatchesEnvironment(entry.Environment, runnerEnvironment))
            {
                continue;
            }

            // §18: a paused (or faulted) run the control plane marked resume-claimable (ResumeRequestedAt present) also
            // surfaces here, so a separate runner can claim and advance it; the marker is cleared on its first checkpoint.
            if (entry.ResumeRequestedAt is not null
                && (entry.Status == WorkflowRunStatus.Suspended || entry.Status == WorkflowRunStatus.Faulted))
            {
                yield return runId;
                continue;
            }

            if (entry.Status == WorkflowRunStatus.Pending)
            {
                yield return runId;
                continue;
            }

            if (entry.Status == WorkflowRunStatus.Running)
            {
                NatsKVEntry<byte[]>? leaseEntry = await this.TryGetAsync(this.leases, runId.Value, cancellationToken).ConfigureAwait(false);
                bool live = false;
                if (leaseEntry is { Value: { } value })
                {
                    (_, _, long expiresAt, _) = LeaseCodec.Decode(value);
                    live = expiresAt > now.ToUnixTimeMilliseconds();
                }

                if (!live)
                {
                    yield return runId;
                }
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        // The KV bucket has no server-side ordering, so collect matches, sort by run id, and keyset-page here.
        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        string? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

        // §14.4: narrow to the candidate runs the label bucket admits before reading anything. A null answer
        // means the index could not narrow and the key scan runs as it always did; an empty one means no run
        // qualifies, so the page is empty without a single envelope read.
        IReadOnlySet<string>? candidates = await this.ResolveReachCandidatesAsync(query.Security, cancellationToken).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return WorkflowContinuationToken.Paginate([], query.Limit);
        }

        var listings = new List<WorkflowRunListing>();
        await foreach ((WorkflowRunId runId, WorkflowRunIndexEntry index) in this.ScanAsync(candidates, cancellationToken).ConfigureAwait(false))
        {
            if (Matches(query, index)
                && (after is null || string.CompareOrdinal(runId.Value, after) > 0))
            {
                listings.Add(new WorkflowRunListing(runId, index));
            }
        }

        listings.Sort(static (a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
        if (listings.Count > query.Limit + 1)
        {
            listings = listings.GetRange(0, query.Limit + 1);
        }

        return WorkflowContinuationToken.Paginate(listings, query.Limit);
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(WorkflowQuery query, int cap, CancellationToken cancellationToken)
    {
        // Same narrowing as the list, so a count can never consider a run the list would not have shown (§14.4).
        IReadOnlySet<string>? candidates = await this.ResolveReachCandidatesAsync(query.Security, cancellationToken).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return (0, false);
        }

        // Bounded scan: count matches with the SAME filter the list applies (so the §14.2 reach cannot drift), stopping
        // one past the cap. A count is order-independent, so no sort or keyset paging.
        int count = 0;
        await foreach ((WorkflowRunId _, WorkflowRunIndexEntry index) in this.ScanAsync(candidates, cancellationToken).ConfigureAwait(false))
        {
            if (Matches(query, index) && ++count > cap)
            {
                return (cap, true);
            }
        }

        return (count, false);
    }

    // The shared visibility filter (status / workflow / draft-exclusion / timestamps / correlation / tags / §14.2
    // security reach), WITHOUT the keyset cursor: QueryAsync adds the cursor + sorts, CountAsync scans with just this.
    // Both share the one predicate so the reach filter cannot drift.
    private static bool Matches(in WorkflowQuery query, in WorkflowRunIndexEntry index)
        => (query.Status is not { } status || index.Status == status)
            && (query.WorkflowId is not { } workflowId || index.WorkflowId == workflowId)

            // §18: an unfiltered visibility query never surfaces draft runs — a caller must name the reserved $draft id.
            // #896: schedule runs (the reserved $schedule kind) are hidden the same way.
            && (query.WorkflowId is not null || !string.Equals(index.WorkflowId, DraftRuns.RunWorkflowId, StringComparison.Ordinal))
            && (query.WorkflowId is not null || !string.Equals(index.WorkflowId, ScheduleHostedWorkflow.ScheduleWorkflowId, StringComparison.Ordinal))
            && (query.CreatedAfter is not { } createdAfter || index.CreatedAt >= createdAfter)
            && (query.CreatedBefore is not { } createdBefore || index.CreatedAt < createdBefore)
            && (query.UpdatedAfter is not { } updatedAfter || index.UpdatedAt >= updatedAfter)
            && (query.UpdatedBefore is not { } updatedBefore || index.UpdatedAt < updatedBefore)
            && (query.CorrelationId is not { } cid || index.CorrelationId == cid)
            && query.Tags.AllContainedIn(index.Tags)

            // Row-security reach (§14.2): the exact evaluation over the run's persisted tags. The query first
            // narrows to the candidates the label bucket admits (§14.4) and this check then decides each one —
            // an imprecise plan costs throughput, never reach.
            && (query.Security?.IsSatisfiedBy(index.SecurityTags) ?? true);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownedConnection is not null)
        {
            await this.ownedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Resolves the reach to the candidate run ids the label bucket admits (§14.4). Null means "the index could
    // not narrow this rule", which is a legitimate answer — the exact evaluation downstream still decides what
    // the principal sees, so an un-narrowable rule costs throughput and never widens reach.
    private ValueTask<IReadOnlySet<string>?> ResolveReachCandidatesAsync(SecurityFilter? security, CancellationToken cancellationToken)
        => security is null
            ? new ValueTask<IReadOnlySet<string>?>((IReadOnlySet<string>?)null)
            : SecurityLabelQueryResolver.ResolveAsync(
                security.ToPredicate(SecurityLabelQueryEmitter.Instance), this.labelIndex, cancellationToken);

    // Streams the run entries a query should consider. With no candidate set this is the key scan it always
    // was; with one, the candidate ids are fetched directly, so runs outside the principal's reach are never
    // read — and a stale label entry resolves to an id whose envelope is gone, which the existing absent-entry
    // skip discards.
    private async IAsyncEnumerable<(WorkflowRunId Id, WorkflowRunIndexEntry Index)> ScanAsync(IReadOnlySet<string>? candidates, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (candidates is not null)
        {
            foreach (string id in candidates)
            {
                NatsKVEntry<byte[]>? candidateEntry = await this.TryGetAsync(this.runs, id, cancellationToken).ConfigureAwait(false);
                if (candidateEntry is { Value: { } candidateValue })
                {
                    yield return (new WorkflowRunId(id), Envelope.DecodeIndex(candidateValue));
                }
            }

            yield break;
        }

        await foreach (string key in this.runs.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(this.runs, key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } value })
            {
                yield return (new WorkflowRunId(key), Envelope.DecodeIndex(value));
            }
        }
    }

    private async ValueTask<NatsKVEntry<byte[]>?> TryGetAsync(INatsKVStore store, string key, CancellationToken cancellationToken)
    {
        try
        {
            return await store.GetEntryAsync<byte[]>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
            return null;
        }
        catch (NatsKVKeyDeletedException)
        {
            return null;
        }
    }

    private async ValueTask PurgeAsync(INatsKVStore store, string key, CancellationToken cancellationToken)
    {
        try
        {
            await store.PurgeAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }

    // §5.5: an unscoped dispatcher (null runnerEnvironment) or an unpinned run (null entry environment) matches
    // anything; otherwise the run's pinned environment must equal the runner's.
    // §5.5: a real runner (non-null runnerEnvironment) claims a run only when pinned to EXACTLY its environment (an
    // unpinned or differently-pinned run is never claimed); a null runnerEnvironment is the env-agnostic base overload
    // (list all claimable regardless of environment), never a runner.
    private static bool MatchesEnvironment(string? runEnvironment, string? runnerEnvironment)
        => runnerEnvironment is null || (runEnvironment is not null && string.Equals(runEnvironment, runnerEnvironment, StringComparison.Ordinal));

    private static class Envelope
    {
        private const int HeaderBufferSize = 512;

        public static byte[] Encode(in WorkflowRunIndexEntry index, ReadOnlySpan<byte> checkpoint)
        {
            // Serialize the index header through the pooled writer cache (not a fresh ArrayBufferWriter) — this is the
            // run-state write hotpath. The owned `result` (length-prefixed header + checkpoint) is the form the KV
            // driver demands; the header scratch is pooled.
            using JsonWorkspace workspace = JsonWorkspace.Create();
            Utf8JsonWriter writer = workspace.RentWriterAndBuffer(HeaderBufferSize, out IByteBufferWriter headerBuffer);
            try
            {
                writer.WriteStartObject();
                writer.WriteString("status", index.Status.ToString());
                writer.WriteString("workflowId", index.WorkflowId);
                writer.WriteNumber("createdAt", index.CreatedAt.ToUnixTimeMilliseconds());
                writer.WriteNumber("updatedAt", index.UpdatedAt.ToUnixTimeMilliseconds());
                if (index.DueAt is { } due)
                {
                    writer.WriteNumber("dueAt", due.ToUnixTimeMilliseconds());
                }

                if (index.AwaitingChannel is { } channel)
                {
                    writer.WriteString("awaitingChannel", channel);
                }

                if (index.AwaitingCorrelationId is { } correlationId)
                {
                    writer.WriteString("awaitingCorrelationId", correlationId);
                }

                if (index.ErrorType is { } errorType)
                {
                    writer.WriteString("errorType", errorType);
                }

                if (index.CorrelationId is { } runCorrelationId)
                {
                    writer.WriteString("correlationId", runCorrelationId);
                }

                if (index.Environment is { } environment)
                {
                    writer.WriteString("environment", environment);
                }

                if (index.ResumeRequestedAt is { } resume)
                {
                    writer.WriteNumber("resumeRequestedAt", resume.ToUnixTimeMilliseconds());
                }

                if (!index.Tags.IsEmpty)
                {
                    writer.WritePropertyName("tags");
                    index.Tags.WriteTo(writer);
                }

                if (!index.SecurityTags.IsEmpty)
                {
                    writer.WritePropertyName("securityTags"u8);
                    index.SecurityTags.WriteTo(writer);
                }

                writer.WriteEndObject();
                writer.Flush();

                ReadOnlySpan<byte> header = headerBuffer.WrittenSpan;
                var result = new byte[4 + header.Length + checkpoint.Length];
                BinaryPrimitives.WriteInt32LittleEndian(result, header.Length);
                header.CopyTo(result.AsSpan(4));
                checkpoint.CopyTo(result.AsSpan(4 + header.Length));
                return result;
            }
            finally
            {
                workspace.ReturnWriterAndBuffer(writer, headerBuffer);
            }
        }

        public static byte[] DecodeCheckpoint(byte[] value)
        {
            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(value);
            return value.AsSpan(4 + headerLength).ToArray();
        }

        public static WorkflowRunIndexEntry DecodeIndex(byte[] value)
        {
            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(value);
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(value.AsMemory(4, headerLength));
            JsonElement root = document.RootElement;
            return new WorkflowRunIndexEntry(
                root.GetProperty("workflowId"u8).GetString()!,
                Enum.Parse<WorkflowRunStatus>(root.GetProperty("status"u8).GetString()!),
                DateTimeOffset.FromUnixTimeMilliseconds(root.GetProperty("createdAt"u8).GetInt64()),
                DateTimeOffset.FromUnixTimeMilliseconds(root.GetProperty("updatedAt"u8).GetInt64()),
                root.TryGetProperty("dueAt"u8, out JsonElement dueAt) ? DateTimeOffset.FromUnixTimeMilliseconds(dueAt.GetInt64()) : null,
                root.TryGetProperty("awaitingChannel"u8, out JsonElement channel) ? channel.GetString() : null,
                root.TryGetProperty("awaitingCorrelationId"u8, out JsonElement correlationId) ? correlationId.GetString() : null,
                root.TryGetProperty("errorType"u8, out JsonElement errorType) ? errorType.GetString() : null,
                root.TryGetProperty("correlationId"u8, out JsonElement queryCorrelationId) ? queryCorrelationId.GetString() : null,
                DecodeTags(root),
                DecodeSecurityTags(root),
                root.TryGetProperty("environment"u8, out JsonElement environment) ? environment.GetString() : null,
                root.TryGetProperty("resumeRequestedAt"u8, out JsonElement resumeRequestedAt) ? DateTimeOffset.FromUnixTimeMilliseconds(resumeRequestedAt.GetInt64()) : null);
        }

        private static TagSet DecodeTags(JsonElement root)
            => root.TryGetProperty("tags"u8, out JsonElement tags) ? TagSet.CopyFrom(tags) : default;

        private static SecurityTagSet DecodeSecurityTags(JsonElement root)
            => root.TryGetProperty("securityTags"u8, out JsonElement securityTags) ? SecurityTagSet.CopyFrom(securityTags) : default;
    }

    private static class LeaseCodec
    {
        public static byte[] Encode(string owner, string token, long expiresAt, long epoch)
            => PersistedJson.ToArray(
                (owner, token, expiresAt, epoch),
                static (Utf8JsonWriter writer, in (string Owner, string Token, long ExpiresAt, long Epoch) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("owner", c.Owner);
                    writer.WriteString("token", c.Token);
                    writer.WriteNumber("expiresAt", c.ExpiresAt);
                    writer.WriteNumber("epoch", c.Epoch);
                    writer.WriteEndObject();
                });

        public static (string Owner, string Token, long ExpiresAt, long Epoch) Decode(byte[] value)
        {
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(value);
            JsonElement root = document.RootElement;
            return (
                root.GetProperty("owner"u8).GetString()!,
                root.GetProperty("token"u8).GetString()!,
                root.GetProperty("expiresAt"u8).GetInt64(),
                root.GetProperty("epoch"u8).GetInt64());
        }
    }
}