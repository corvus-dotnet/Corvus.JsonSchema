// <copyright file="InMemoryWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The in-memory reference implementation of <see cref="IWorkflowStateStore"/>. It keeps checkpoints in a
/// dictionary keyed by the run's full <see cref="WorkflowRunAddress"/> — the <c>(environment, runId)</c>
/// composite primary key of ADR 0065 decision 9, so the same run id in two environments names two distinct
/// runs — with a monotonic version as the etag and an in-process advisory lease, so the whole durability
/// mechanism is unit-testable with no external store — exactly as <c>InMemoryMessageTransport</c> does for
/// AsyncAPI. It is the reference against which the shared store-conformance suite runs, and is also usable
/// for a real single-process run that does not need to survive a host restart.
/// </summary>
public sealed class InMemoryWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IWorkflowDispatchIndex, IWorkflowLeaseAdministration, ISupportsRowSecurityFilter
{
    private readonly Dictionary<WorkflowRunAddress, Entry> entries = [];
    private readonly Dictionary<WorkflowRunAddress, LeaseRecord> leases = [];
    private readonly TimeProvider timeProvider;
    private readonly Lock gate = new();
    private long version;
    private long leaseToken;

    /// <summary>Initializes a new instance of the <see cref="InMemoryWorkflowStateStore"/> class.</summary>
    /// <param name="timeProvider">The time source used for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryWorkflowStateStore(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public bool SupportsRowSecurityFilter => true;

    // Takes the filter by reference so a query's criteria travel as a context instead of being captured, which keeps
    // every Snapshot predicate static.
    private delegate bool EntryPredicate<TContext>(in TContext context, in WorkflowRunAddress address, in Entry entry);

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunAddress address,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            bool exists = this.entries.TryGetValue(address, out Entry current);
            if (exists)
            {
                if (expected.IsNone || current.Etag.Value != expected.Value)
                {
                    throw new WorkflowConflictException(address, expected);
                }
            }
            else if (!expected.IsNone)
            {
                throw new WorkflowConflictException(address, expected);
            }

            var newEtag = new WorkflowEtag((++this.version).ToString(System.Globalization.CultureInfo.InvariantCulture));
            this.entries[address] = new Entry(checkpointUtf8.ToArray(), newEtag, index);
            return ValueTask.FromResult(newEtag);
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            return this.entries.TryGetValue(address, out Entry current)
                ? ValueTask.FromResult<WorkflowCheckpoint?>(new WorkflowCheckpoint(current.Checkpoint, current.Etag))
                : ValueTask.FromResult<WorkflowCheckpoint?>(null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunAddress address, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            bool held = this.leases.TryGetValue(address, out LeaseRecord existing);
            if (held && existing.ExpiresAt > now && existing.Owner != owner)
            {
                return ValueTask.FromResult<WorkflowLease?>(null);
            }

            string token = (++this.leaseToken).ToString(System.Globalization.CultureInfo.InvariantCulture);
            DateTimeOffset expiresAt = now + ttl;

            // The epoch counts this run's grants, so it comes from the record the run's previous grant left behind rather
            // than from anything this process holds (ADR 0065 §6).
            long epoch = held ? existing.Epoch + 1 : 1;
            this.leases[address] = new LeaseRecord(owner, token, expiresAt, epoch);
            return ValueTask.FromResult<WorkflowLease?>(new WorkflowLease(address, owner, token, expiresAt, epoch));
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowLease?> TryExtendLeaseAsync(WorkflowLease lease, TimeSpan extension, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(extension, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            if (!this.leases.TryGetValue(lease.Address, out LeaseRecord existing)
                || existing.Token != lease.Token
                || existing.Owner != lease.Owner
                || existing.ExpiresAt <= now)
            {
                return ValueTask.FromResult<WorkflowLease?>(null);
            }

            // The epoch reported is the stored one, never the presented one: the caller states a claim and the store
            // answers with the grant's own epoch, which is what the §6 rules are then decided against.
            if (extension == TimeSpan.Zero)
            {
                return ValueTask.FromResult<WorkflowLease?>(new WorkflowLease(lease.Address, existing.Owner, existing.Token, existing.ExpiresAt, existing.Epoch));
            }

            DateTimeOffset expiresAt = now + extension;
            this.leases[lease.Address] = existing with { ExpiresAt = expiresAt };
            return ValueTask.FromResult<WorkflowLease?>(new WorkflowLease(lease.Address, existing.Owner, existing.Token, expiresAt, existing.Epoch));
        }
    }

    /// <inheritdoc/>
    public ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            // Expired in place rather than removed, so the run's epoch survives its holder handing it back — the ordinary
            // way a grant ends, and the one a counter kept only alongside a live lease would forget. Expiring at exactly
            // now makes the run acquirable at once: every reader tests ExpiresAt > now.
            if (this.leases.TryGetValue(lease.Address, out LeaseRecord existing) && existing.Token == lease.Token)
            {
                this.leases[lease.Address] = existing with { ExpiresAt = this.timeProvider.GetUtcNow() };
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> ExpireLeasesForOwnerAsync(string owner, string? environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            // Collect first, then expire — never mutate the dictionary mid-enumeration. Expiring in place (ExpiresAt = now)
            // rather than removing keeps a record that a lease existed, and makes the run reclaimable now: HasLiveLease and
            // AcquireLease both test ExpiresAt > now, which is false at exactly now. The scope is the address's
            // environment: revocation withdraws an environment, so a runner keeping others keeps its leases there
            // (ADR 0065 decision 9); a null environment fences the owner everywhere.
            List<WorkflowRunAddress>? toExpire = null;
            foreach ((WorkflowRunAddress address, LeaseRecord record) in this.leases)
            {
                if (record.Owner == owner
                    && record.ExpiresAt > now
                    && (environment is null || string.Equals(address.Environment, environment, StringComparison.Ordinal)))
                {
                    (toExpire ??= []).Add(address);
                }
            }

            if (toExpire is null)
            {
                return ValueTask.FromResult(0);
            }

            foreach (WorkflowRunAddress address in toExpire)
            {
                this.leases[address] = this.leases[address] with { ExpiresAt = now };
            }

            return ValueTask.FromResult(toExpire.Count);
        }
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            this.entries.Remove(address);
            this.leases.Remove(address);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunAddress> QueryDueAsync(DateTimeOffset before, CancellationToken cancellationToken)
        => this.QueryDueAsync(before, null, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunAddress> QueryDueAsync(DateTimeOffset before, string? runnerEnvironment, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        foreach (WorkflowRunAddress address in this.Snapshot(
            new DueFilter(before, runnerEnvironment),
            static (in DueFilter f, in WorkflowRunAddress a, in Entry e) =>
                e.Index.Status == WorkflowRunStatus.Suspended && e.Index.DueAt is { } due && due <= f.Before
                && MatchesEnvironment(a.Environment, f.RunnerEnvironment)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return address;
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunAddress> QueryAwaitingAsync(string channel, string? correlationId, CancellationToken cancellationToken)
        => this.QueryAwaitingAsync(channel, correlationId, null, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunAddress> QueryAwaitingAsync(string channel, string? correlationId, string? runnerEnvironment, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        await Task.CompletedTask.ConfigureAwait(false);
        foreach (WorkflowRunAddress address in this.Snapshot(
            new AwaitingFilter(channel, correlationId, runnerEnvironment),
            static (in AwaitingFilter f, in WorkflowRunAddress a, in Entry e) =>
                e.Index.Status == WorkflowRunStatus.Suspended
                && e.Index.AwaitingChannel == f.Channel
                && (f.CorrelationId is null || e.Index.AwaitingCorrelationId is null || e.Index.AwaitingCorrelationId == f.CorrelationId)
                && MatchesEnvironment(a.Environment, f.RunnerEnvironment)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return address;
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        WorkflowRunAddress? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

        // Keyset page by ascending address (environment, then run id). Rather than materialise + LINQ-sort the whole
        // matching set, keep only the Limit+1 SMALLEST past-cursor matches in a capped, insertion-sorted buffer — the
        // in-memory analogue of ORDER BY (environment, run-id) LIMIT Limit+1. One bounded List instead of the
        // Where/OrderBy/Select iterator+closure chain; the +1 row detects "more remain" (and seeds the next-page
        // token). Paginate trims it and mints the token.
        int cap = query.Limit + 1;
        var top = new List<WorkflowRunListing>(cap);
        lock (this.gate)
        {
            foreach (KeyValuePair<WorkflowRunAddress, Entry> kvp in this.entries)
            {
                WorkflowRunIndexEntry index = kvp.Value.Index;
                WorkflowRunAddress address = kvp.Key;

                if (!Matches(query, address, index)
                    || (after is { } cursor && WorkflowRunAddress.Compare(address, cursor) <= 0))
                {
                    continue;
                }

                var listing = new WorkflowRunListing(address, index);
                if (top.Count < cap)
                {
                    InsertSorted(top, listing);
                }
                else if (WorkflowRunAddress.Compare(address, top[cap - 1].Address) < 0)
                {
                    top.RemoveAt(cap - 1);
                    InsertSorted(top, listing);
                }
            }
        }

        return ValueTask.FromResult(WorkflowContinuationToken.Paginate(top, query.Limit));
    }

    /// <inheritdoc/>
    public ValueTask<(int Count, bool Capped)> CountAsync(WorkflowQuery query, int cap, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Bounded scan: count matches with the SAME filter the list applies (so the §14.4 reach cannot drift) and
        // stop the moment the cap is exceeded — never materialising listings, no sort.
        int count = 0;
        lock (this.gate)
        {
            foreach (KeyValuePair<WorkflowRunAddress, Entry> kvp in this.entries)
            {
                if (Matches(query, kvp.Key, kvp.Value.Index) && ++count > cap)
                {
                    return ValueTask.FromResult((cap, true));
                }
            }
        }

        return ValueTask.FromResult((count, false));
    }

    // The shared visibility filter (run-id point lookup / status / workflow / draft-exclusion / timestamps /
    // correlation / tags / §14.4 security reach), WITHOUT the keyset cursor: QueryAsync adds the cursor for paging,
    // CountAsync scans with just this. Both share the one predicate so the reach filter cannot drift between list
    // and count.
    // §18: draft runs never surface on an unfiltered visibility query — a caller must name the reserved $draft
    // workflow id explicitly (the debug-run surface does; the runs listing never does). #896: schedule runs (the
    // reserved $schedule kind) are hidden the same way — internal scheduler machinery, not operator-facing runs.
    // ADR 0065 §9 (C4): naming a RUN id is at least as explicit as naming the reserved workflow id, so the point
    // lookup the management client resolves a bare id through sees the reserved kinds too; the reach filter still
    // applies in full.
    private static bool Matches(in WorkflowQuery query, in WorkflowRunAddress address, in WorkflowRunIndexEntry index)
        => !((query.RunId is { } id && !string.Equals(address.RunId.Value, id, StringComparison.Ordinal))
            || (query.Status is { } status && index.Status != status)
            || (query.WorkflowId is { } workflowId && index.WorkflowId != workflowId)
            || (query.WorkflowId is null && query.RunId is null && string.Equals(index.WorkflowId, DraftRuns.RunWorkflowId, StringComparison.Ordinal))
            || (query.WorkflowId is null && query.RunId is null && string.Equals(index.WorkflowId, ScheduleHostedWorkflow.ScheduleWorkflowId, StringComparison.Ordinal))
            || (query.CreatedAfter is { } createdAfter && index.CreatedAt < createdAfter)
            || (query.CreatedBefore is { } createdBefore && index.CreatedAt >= createdBefore)
            || (query.UpdatedAfter is { } updatedAfter && index.UpdatedAt < updatedAfter)
            || (query.UpdatedBefore is { } updatedBefore && index.UpdatedAt >= updatedBefore)
            || (query.CorrelationId is { } correlationId && index.CorrelationId != correlationId)
            || !query.Tags.AllContainedIn(index.Tags)
            || !(query.Security?.IsSatisfiedBy(index.SecurityTags) ?? true));

    // Inserts a listing into the capped buffer at its ascending-address position (linear from the end — the buffer is
    // Limit+1 small and stays within its preallocated capacity, so no backing array reallocates).
    private static void InsertSorted(List<WorkflowRunListing> buffer, WorkflowRunListing listing)
    {
        int i = buffer.Count;
        while (i > 0 && WorkflowRunAddress.Compare(buffer[i - 1].Address, listing.Address) > 0)
        {
            i--;
        }

        buffer.Insert(i, listing);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunAddress> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, CancellationToken cancellationToken)
        => this.QueryClaimableAsync(hostedWorkflowIds, null, now, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunAddress> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, string? runnerEnvironment, DateTimeOffset now, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostedWorkflowIds);
        await Task.CompletedTask.ConfigureAwait(false);

        // A plain scan rather than Where/Select/ToList: this is the dispatch claim path and it runs under the gate, so
        // the display class, two delegates and two iterators that shape cost are paid on every claim query.
        var claimable = new List<WorkflowRunAddress>();
        lock (this.gate)
        {
            foreach (KeyValuePair<WorkflowRunAddress, Entry> kvp in this.entries)
            {
                WorkflowRunIndexEntry index = kvp.Value.Index;
                if (!hostedWorkflowIds.Contains(index.WorkflowId)
                    || !MatchesEnvironment(kvp.Key.Environment, runnerEnvironment))
                {
                    continue;
                }

                // §18: a paused (or faulted) run the control plane marked resume-claimable via RequestResumeAsync. It
                // stays Suspended/Faulted (never re-labelled Pending), and its marker is cleared on the claiming
                // runner's first checkpoint. Gated on the stopped status so a terminal run that somehow retained the
                // marker is never surfaced (nor perpetually re-loaded and rejected by the dispatcher).
                if (index.Status == WorkflowRunStatus.Pending
                    || (index.Status == WorkflowRunStatus.Running && !this.HasLiveLease(kvp.Key, now))
                    || (index.ResumeRequestedAt is not null
                        && index.Status is WorkflowRunStatus.Suspended or WorkflowRunStatus.Faulted))
                {
                    claimable.Add(kvp.Key);
                }
            }
        }

        foreach (WorkflowRunAddress address in claimable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return address;
        }
    }

    // §5.5 dispatch env-match: a real runner (non-null runnerEnvironment) claims a run only when it is pinned to EXACTLY
    // its environment — a run pinned elsewhere is never claimed (the credential boundary). The environment comes from
    // the run's ADDRESS (ADR 0065 decision 9), so it is never absent. A runner always declares its environment (the
    // WorkflowDispatcher rejects an unscoped one), so this is the dispatch rule. A null runnerEnvironment is the
    // env-agnostic base overload (list all claimable regardless of environment) — a diagnostics / pre-pinning
    // primitive, never a runner.
    private static bool MatchesEnvironment(string runEnvironment, string? runnerEnvironment)
        => runnerEnvironment is null || string.Equals(runEnvironment, runnerEnvironment, StringComparison.Ordinal);

    // Must be called while holding the gate.
    private bool HasLiveLease(in WorkflowRunAddress address, DateTimeOffset now)
        => this.leases.TryGetValue(address, out LeaseRecord lease) && lease.ExpiresAt > now;

    // The filter travels as a context rather than being captured, so the predicate stays static and one query
    // allocates the result list and nothing else.
    private List<WorkflowRunAddress> Snapshot<TContext>(in TContext context, EntryPredicate<TContext> matches)
    {
        var addresses = new List<WorkflowRunAddress>();
        lock (this.gate)
        {
            foreach (KeyValuePair<WorkflowRunAddress, Entry> kvp in this.entries)
            {
                Entry entry = kvp.Value;
                WorkflowRunAddress address = kvp.Key;
                if (matches(in context, in address, in entry))
                {
                    addresses.Add(address);
                }
            }
        }

        return addresses;
    }

    private readonly record struct Entry(byte[] Checkpoint, WorkflowEtag Etag, WorkflowRunIndexEntry Index);

    private readonly record struct DueFilter(DateTimeOffset Before, string? RunnerEnvironment);

    private readonly record struct AwaitingFilter(string Channel, string? CorrelationId, string? RunnerEnvironment);

    private readonly record struct LeaseRecord(string Owner, string Token, DateTimeOffset ExpiresAt, long Epoch);
}