// <copyright file="RunnerRunCoordinator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// The store-facing half of the runner API (ADR 0065): claiming a run, holding and releasing its lease, and checking
/// that a presented lease still authorises an operation on it. The handlers project its results into the generated
/// response models and add nothing of their own, so the rules that matter — what a runner may be offered, who owns a
/// lease, and what a stale one may still do — are decided in one place and are testable without a web host.
/// </summary>
/// <remarks>
/// <para>
/// Every method takes the authenticated machine principal rather than reading an owner from the request. That is the
/// whole of ADR 0065 decision 2 at this layer: the lease's owner is the principal, the store's predicate includes it,
/// and revocation expires leases by principal, so a compromised runner cannot rename itself into another's leases.
/// </para>
/// <para>
/// The same rule decides the grant's epoch. It is minted and persisted by the store, per run, and every operation
/// compares the epoch the caller presents against the one the store reports rather than believing it — the ADR 0065 §6
/// pair, an epoch above the current grant naming a grant this holder never held and one below the run's high-water
/// re-presenting a grant the run has moved past.
/// </para>
/// </remarks>
public sealed class RunnerRunCoordinator
{
    private readonly IWorkflowStateStore store;
    private readonly IWorkflowDispatchIndex index;
    private readonly IWorkflowWaitIndex waits;
    private readonly IRunnerEnvironmentBindings bindings;
    private readonly RunnerApiOptions options;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="RunnerRunCoordinator"/> class.</summary>
    /// <param name="store">The durable run store. The control plane owns it exclusively; no runner holds a credential for it.</param>
    /// <param name="bindings">Resolves the environments a machine principal may execute runs for.</param>
    /// <param name="options">The deployment's lease and body bounds; defaults are used when omitted.</param>
    /// <param name="timeProvider">The time source for claim queries; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="store"/> does not implement <see cref="IWorkflowDispatchIndex"/>, without which no run can be offered.</exception>
    public RunnerRunCoordinator(
        IWorkflowStateStore store,
        IRunnerEnvironmentBindings bindings,
        RunnerApiOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(bindings);

        this.store = store;
        this.index = store as IWorkflowDispatchIndex
            ?? throw new ArgumentException("The runner API's store must implement IWorkflowDispatchIndex; without it there is no claimable set to offer.", nameof(store));
        this.waits = store as IWorkflowWaitIndex
            ?? throw new ArgumentException("The runner API's store must implement IWorkflowWaitIndex; without it a suspended run's timer would never fire and a delivered message would reach nothing.", nameof(store));
        this.bindings = bindings;
        this.options = options ?? new RunnerApiOptions();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Takes the first claimable run the principal can execute, and its lease, in one operation.
    /// </summary>
    /// <param name="principal">The authenticated machine principal, which becomes the lease owner.</param>
    /// <param name="hostedVersions">The versioned workflow ids the runner has baked and can execute.</param>
    /// <param name="requestedLease">The lease duration the runner asked for, bounded by the deployment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The claimed run, or <see langword="null"/> when nothing is claimable — the common case for an idle runner, and not an error.</returns>
    public async ValueTask<ClaimedRunRecord?> TryClaimAsync(string principal, IReadOnlyCollection<string> hostedVersions, TimeSpan? requestedLease, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);
        ArgumentNullException.ThrowIfNull(hostedVersions);

        // The environment is resolved from the principal's bindings and never taken from the request, so a runner cannot
        // ask for another tenant's work. A principal bound to nothing is offered nothing, which is the correct answer for
        // one whose authorization is pending or revoked.
        RunnerBindings resolved = await this.bindings.ResolveAsync(principal, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> environments = resolved.Environments;
        if (resolved.Count == 0 || hostedVersions.Count == 0)
        {
            return null;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        TimeSpan lease = this.options.BoundLease(requestedLease);
        int considered = 0;

        // The claim re-checks each candidate's workflow id under the lease, and a runner may host a thousand versions,
        // so the membership test is built once per request rather than scanned per candidate.
        var hosted = new HashSet<string>(hostedVersions, StringComparer.Ordinal);

        foreach (string environment in environments)
        {
            await foreach (WorkflowRunId id in this.index.QueryClaimableAsync(hostedVersions, environment, now, cancellationToken).ConfigureAwait(false))
            {
                if (++considered > this.options.ClaimCandidates)
                {
                    // A busy queue must not let one request walk the whole index. The runner polls again.
                    return null;
                }

                WorkflowLease? acquired = await this.store.AcquireLeaseAsync(id, principal, lease, cancellationToken).ConfigureAwait(false);
                if (acquired is not { } held)
                {
                    // Another runner holds it.
                    continue;
                }

                ClaimedRunRecord? claimed = await this.ProjectClaimAsync(held, environment, hosted, cancellationToken).ConfigureAwait(false);
                if (claimed is not null)
                {
                    return claimed;
                }

                // The run changed under us between the index query and the lease (completed, suspended, deleted), so hand
                // it straight back rather than holding a run this runner will not advance.
                await this.store.ReleaseLeaseAsync(held, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <summary>
    /// Takes the runs whose durable timer is now due, and their leases, in one operation.
    /// </summary>
    /// <param name="principal">The authenticated machine principal, which becomes the lease owner.</param>
    /// <param name="hostedVersions">The versioned workflow ids the runner has baked and can execute.</param>
    /// <param name="limit">The most runs to claim, bounded by the deployment.</param>
    /// <param name="requestedLease">The lease duration the runner asked for, bounded by the deployment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The claimed runs, empty when no timer is due.</returns>
    /// <remarks>
    /// The cutoff is this server's clock rather than a request parameter. A runner naming its own cutoff would be
    /// asking to resume runs whose timers have not fired, and a runner with a fast clock would do so by accident.
    /// </remarks>
    public async ValueTask<IReadOnlyList<ClaimedRunRecord>> ClaimDueAsync(string principal, IReadOnlyCollection<string> hostedVersions, int? limit, TimeSpan? requestedLease, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);
        ArgumentNullException.ThrowIfNull(hostedVersions);

        if (await this.BeginSweepAsync(principal, hostedVersions, cancellationToken).ConfigureAwait(false) is not { } sweep)
        {
            return [];
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        TimeSpan lease = this.options.BoundLease(requestedLease);
        int wanted = this.options.BoundSweep(limit);

        // Sized from the bound, not grown into it: the loop returns the moment it reaches `wanted`, so that is the
        // exact ceiling and every grow-and-copy on the way there would be avoidable work.
        var claims = new List<ClaimedRunRecord>(wanted);

        foreach (string environment in sweep.Environments)
        {
            await foreach (WorkflowRunId id in this.waits.QueryDueAsync(now, environment, cancellationToken).ConfigureAwait(false))
            {
                if (claims.Count >= wanted)
                {
                    return claims;
                }

                await this.TryTakeWaitingAsync(id, environment, principal, sweep.Hosted, lease, claims, cancellationToken).ConfigureAwait(false);
            }
        }

        return claims;
    }

    /// <summary>
    /// Takes the runs awaiting a message on a channel, and their leases, in one operation.
    /// </summary>
    /// <param name="principal">The authenticated machine principal, which becomes the lease owner.</param>
    /// <param name="channel">The channel the message arrived on.</param>
    /// <param name="correlationId">The delivered message's correlation token, or <see langword="null"/> to match every run awaiting the channel, whatever correlation each awaits. Absence is a wildcard on either side: a run awaiting no particular correlation is likewise matched by any message.</param>
    /// <param name="hostedVersions">The versioned workflow ids the runner has baked and can execute.</param>
    /// <param name="limit">The most runs to claim, bounded by the deployment.</param>
    /// <param name="requestedLease">The lease duration the runner asked for, bounded by the deployment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The claimed runs, empty when nothing awaits that message.</returns>
    /// <remarks>
    /// The payload is not a parameter and never reaches the store. This answers which runs a message can resume, not
    /// what it said, so the runner keeps the only copy and hands it to each run itself.
    /// </remarks>
    public async ValueTask<IReadOnlyList<ClaimedRunRecord>> ClaimAwaitingAsync(string principal, string channel, string? correlationId, IReadOnlyCollection<string> hostedVersions, int? limit, TimeSpan? requestedLease, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentNullException.ThrowIfNull(hostedVersions);

        if (await this.BeginSweepAsync(principal, hostedVersions, cancellationToken).ConfigureAwait(false) is not { } sweep)
        {
            return [];
        }

        TimeSpan lease = this.options.BoundLease(requestedLease);
        int wanted = this.options.BoundSweep(limit);

        // Sized from the bound, not grown into it: the loop returns the moment it reaches `wanted`, so that is the
        // exact ceiling and every grow-and-copy on the way there would be avoidable work.
        var claims = new List<ClaimedRunRecord>(wanted);

        foreach (string environment in sweep.Environments)
        {
            await foreach (WorkflowRunId id in this.waits.QueryAwaitingAsync(channel, correlationId, environment, cancellationToken).ConfigureAwait(false))
            {
                if (claims.Count >= wanted)
                {
                    return claims;
                }

                await this.TryTakeWaitingAsync(id, environment, principal, sweep.Hosted, lease, claims, cancellationToken).ConfigureAwait(false);
            }
        }

        return claims;
    }

    /// <summary>
    /// Extends a lease the principal already holds.
    /// </summary>
    /// <param name="principal">The authenticated machine principal.</param>
    /// <param name="id">The run the lease is held on.</param>
    /// <param name="leaseToken">The presented <c>X-Arazzo-Lease</c> value.</param>
    /// <param name="requestedExtension">The extension the runner asked for, bounded by the deployment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The extended grant, or <see langword="null"/> when the presented lease is no longer current — expired,
    /// released, or revoked, and the run may already be held by another runner.</returns>
    public async ValueTask<RunnerLeaseGrant?> TryRenewAsync(string principal, WorkflowRunId id, string? leaseToken, TimeSpan? requestedExtension, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);

        if (!RunnerLeaseToken.TryParse(leaseToken, out long epoch, out string storeToken))
        {
            return null;
        }

        // Renewal re-checks the bindings, because an unchecked renewal is what would let a revoked runner keep a run
        // indefinitely: each extension pushes the expiry out, so the lease never lapses on its own and the ADR 0027
        // revocation fence has nothing left to bound it. Re-resolving here is what makes revocation take effect within
        // the resolver's cache window (ADR 0065 decision 2 caps that at thirty seconds) rather than never.
        if (!await this.StillBoundAsync(principal, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        WorkflowLease? extended = await this.store.TryExtendLeaseAsync(
            new WorkflowLease(id, principal, storeToken, default),
            this.options.BoundLease(requestedExtension),
            cancellationToken).ConfigureAwait(false);

        // The epoch reported is the store's, and the presented one has to match it. The epoch is the grant's rather than
        // the extension's — one is minted per grant, so renewing does not invalidate a checkpoint already written under
        // it — and the store keeps the same token across an extension, so the header value stays valid unchanged.
        //
        // The mismatch is caught after the extension rather than before it, which costs one round trip fewer and gives
        // away nothing: only the current holder gets past the store's own token-and-owner predicate at all, so what was
        // extended is a lease this caller genuinely holds and could have extended by presenting the epoch it was given.
        // What is refused is the epoch it claimed, not the lease it holds.
        return extended is { } lease && lease.Epoch == epoch ? new RunnerLeaseGrant(leaseToken!, lease.ExpiresAt, lease.Epoch) : null;
    }

    /// <summary>
    /// Hands a run back so another runner may claim it without waiting for the lease to expire.
    /// </summary>
    /// <param name="principal">The authenticated machine principal.</param>
    /// <param name="id">The run to release.</param>
    /// <param name="leaseToken">The presented <c>X-Arazzo-Lease</c> value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the principal no longer holds the run.</returns>
    /// <remarks>
    /// Releasing is checked before it is performed, because the store's release matches on the run and the token alone:
    /// without the check, a token belonging to another principal would release that principal's lease. The outcome is
    /// not reported, and deliberately so. The postcondition the operation promises — this principal does not hold this
    /// run — is true whether the lease was current, had already lapsed, or was never this principal's, and reporting
    /// which would tell a caller about a lease it does not hold. The presented epoch is not compared, for the same
    /// reason the bindings are not re-checked here: refusing a release would strand the run on a holder trying to hand
    /// the work back, and the token is already what proves the holder is this one.
    /// </remarks>
    public async ValueTask ReleaseAsync(string principal, WorkflowRunId id, string? leaseToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);

        if (!RunnerLeaseToken.TryParse(leaseToken, out _, out string storeToken))
        {
            return;
        }

        var presented = new WorkflowLease(id, principal, storeToken, default);
        if (await this.store.TryExtendLeaseAsync(presented, TimeSpan.Zero, cancellationToken).ConfigureAwait(false) is not null)
        {
            await this.store.ReleaseLeaseAsync(presented, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Checks that a presented lease still authorises operations on a run, without extending it.
    /// </summary>
    /// <param name="principal">The authenticated machine principal.</param>
    /// <param name="id">The run named in the request.</param>
    /// <param name="leaseToken">The presented <c>X-Arazzo-Lease</c> value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the principal currently holds the run's lease.</returns>
    /// <remarks>
    /// This is also the whole of the non-disclosure rule for checkpoint operations. A run outside the principal's
    /// bindings, a run held by someone else, and a run that does not exist all fail here identically, so the surface
    /// cannot be used to learn which of the three it was. Verifying does not renew: an operation performed under a lease
    /// must not keep it alive, or a crashed holder's run would never be reclaimed.
    /// </remarks>
    public async ValueTask<bool> HoldsLeaseAsync(string principal, WorkflowRunId id, string? leaseToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(principal);

        if (!RunnerLeaseToken.TryParse(leaseToken, out long epoch, out string storeToken))
        {
            return false;
        }

        // Both checkpoint operations gate on this, so it is where a revoked runner is stopped from continuing to read
        // tenant plaintext and overwrite run state under a lease it acquired while still authorized. A binding that has
        // gone away answers exactly as a lost lease does, which keeps the non-disclosure rule below intact: outside the
        // bindings, held by a peer, and absent remain indistinguishable.
        if (!await this.StillBoundAsync(principal, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        // A mismatched epoch answers as a lost lease does, and joins the same set: the caller learns that this lease
        // does not authorise this operation, never which of the reasons it was.
        WorkflowLease? current = await this.store.TryExtendLeaseAsync(
            new WorkflowLease(id, principal, storeToken, default),
            TimeSpan.Zero,
            cancellationToken).ConfigureAwait(false);

        return current is { } lease && lease.Epoch == epoch;
    }

    // Whether the principal is still bound to anything at all. An operation on a run it already holds does not need to
    // know WHICH environments it is bound to — the lease is already run-specific — only that its authorization has not
    // been withdrawn. Release deliberately does not consult this: refusing a release would strand the lease on a runner
    // trying to hand the work back, which is the one thing a revoked runner can still usefully do.
    private async ValueTask<bool> StillBoundAsync(string principal, CancellationToken cancellationToken)
        => (await this.bindings.ResolveAsync(principal, cancellationToken).ConfigureAwait(false)).Count > 0;

    // Confirms, under the lease, that the run is one this runner should have been offered, and projects what the claim
    // reports. The index query already constrains the candidate set, but it ran before the lease: the run may have been
    // completed, suspended, or deleted in between, and re-reading here is what makes the claim's answer true rather than
    // assumed.
    // Resolves what a sweep is allowed to look at, or null when it is allowed to look at nothing. A principal bound to
    // no environment is offered nothing, which is the correct answer for one whose authorization is pending or revoked,
    // and is the same rule the single-run claim applies.
    private async ValueTask<(IReadOnlyList<string> Environments, HashSet<string> Hosted)?> BeginSweepAsync(string principal, IReadOnlyCollection<string> hostedVersions, CancellationToken cancellationToken)
    {
        RunnerBindings resolved = await this.bindings.ResolveAsync(principal, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> environments = resolved.Environments;
        if (resolved.Count == 0 || hostedVersions.Count == 0)
        {
            return null;
        }

        // Built once per sweep rather than per candidate: a runner may host a thousand versions, and every candidate
        // is re-checked against them under its lease.
        return (environments, new HashSet<string>(hostedVersions, StringComparer.Ordinal));
    }

    // Leases one waiting candidate and keeps it only if it is still resumable under that lease. A run that changed
    // between the index query and the lease is handed straight back rather than held by a runner that will not advance
    // it, exactly as a dispatch claim does.
    private async ValueTask TryTakeWaitingAsync(WorkflowRunId id, string environment, string principal, HashSet<string> hosted, TimeSpan lease, List<ClaimedRunRecord> claims, CancellationToken cancellationToken)
    {
        WorkflowLease? acquired = await this.store.AcquireLeaseAsync(id, principal, lease, cancellationToken).ConfigureAwait(false);
        if (acquired is not { } held)
        {
            // Another runner is already advancing it.
            return;
        }

        if (await this.ProjectWaitingClaimAsync(held, environment, hosted, cancellationToken).ConfigureAwait(false) is { } claimed)
        {
            claims.Add(claimed);
            return;
        }

        await this.store.ReleaseLeaseAsync(held, cancellationToken).ConfigureAwait(false);
    }

    // A waiting run must still be Suspended to be resumable. That is a stricter predicate than a dispatch claim's, and
    // deliberately so: the wait index says a timer fired or a message matched, but the run may have been advanced,
    // completed, or faulted since, and resuming a run that is no longer waiting would re-enter it at a step it has
    // already left.
    private async ValueTask<ClaimedRunRecord?> ProjectWaitingClaimAsync(WorkflowLease held, string environment, HashSet<string> hostedVersions, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(held.RunId, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not { } row || !WorkflowCheckpointSerializer.TryProjectIndex(row.Utf8, out WorkflowRunIndexEntry entry))
        {
            return null;
        }

        if (entry.Status != WorkflowRunStatus.Suspended || entry.Environment != environment || !hostedVersions.Contains(entry.WorkflowId))
        {
            return null;
        }

        var grant = new RunnerLeaseGrant(RunnerLeaseToken.Issue(held.Epoch, held.Token), held.ExpiresAt, held.Epoch);
        return new ClaimedRunRecord(held.RunId, entry.WorkflowId, environment, grant);
    }

    private async ValueTask<ClaimedRunRecord?> ProjectClaimAsync(WorkflowLease held, string environment, HashSet<string> hostedVersions, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(held.RunId, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not { } row || !WorkflowCheckpointSerializer.TryProjectIndex(row.Utf8, out WorkflowRunIndexEntry entry))
        {
            return null;
        }

        // Claimable is a fresh run, an orphan whose holder crashed, or one the control plane marked resume-claimable
        // (design §18) — never a terminal one, whatever a lingering marker says.
        bool claimable = entry.Status is WorkflowRunStatus.Pending or WorkflowRunStatus.Running
            || (entry.ResumeRequestedAt is not null && entry.Status is WorkflowRunStatus.Suspended or WorkflowRunStatus.Faulted);
        if (!claimable || entry.Environment != environment || !hostedVersions.Contains(entry.WorkflowId))
        {
            return null;
        }

        var grant = new RunnerLeaseGrant(RunnerLeaseToken.Issue(held.Epoch, held.Token), held.ExpiresAt, held.Epoch);
        return new ClaimedRunRecord(held.RunId, entry.WorkflowId, environment, grant);
    }
}

/// <summary>A lease granted over one run.</summary>
/// <param name="Token">The <c>X-Arazzo-Lease</c> value to present on every subsequent operation for the run.</param>
/// <param name="ExpiresAt">When the lease lapses and the run becomes claimable again.</param>
/// <param name="Epoch">The grant's epoch, minted server-side and monotonic.</param>
public readonly record struct RunnerLeaseGrant(string Token, DateTimeOffset ExpiresAt, long Epoch);

/// <summary>A claimed run and the lease taken over it.</summary>
/// <param name="RunId">The claimed run.</param>
/// <param name="WorkflowId">The versioned workflow id the run executes, from the runner's own hosted versions.</param>
/// <param name="Environment">The environment the run is pinned to, resolved from the run rather than from the request.</param>
/// <param name="Lease">The lease grant.</param>
public readonly record struct ClaimedRunRecord(WorkflowRunId RunId, string WorkflowId, string Environment, RunnerLeaseGrant Lease);