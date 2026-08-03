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
/// Every method takes the authenticated machine principal rather than reading an owner from the request. That is the
/// whole of ADR 0065 decision 2 at this layer: the lease's owner is the principal, the store's predicate includes it,
/// and revocation expires leases by principal, so a compromised runner cannot rename itself into another's leases.
/// </remarks>
public sealed class RunnerRunCoordinator
{
    private readonly IWorkflowStateStore store;
    private readonly IWorkflowDispatchIndex index;
    private readonly IRunnerEnvironmentBindings bindings;
    private readonly IRunnerLeaseEpochSource epochs;
    private readonly RunnerApiOptions options;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="RunnerRunCoordinator"/> class.</summary>
    /// <param name="store">The durable run store. The control plane owns it exclusively; no runner holds a credential for it.</param>
    /// <param name="bindings">Resolves the environments a machine principal may execute runs for.</param>
    /// <param name="options">The deployment's lease and body bounds; defaults are used when omitted.</param>
    /// <param name="epochs">Mints the epoch each lease grant carries; defaults to <see cref="MonotonicRunnerLeaseEpochSource"/>.</param>
    /// <param name="timeProvider">The time source for claim queries; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="store"/> does not implement <see cref="IWorkflowDispatchIndex"/>, without which no run can be offered.</exception>
    public RunnerRunCoordinator(
        IWorkflowStateStore store,
        IRunnerEnvironmentBindings bindings,
        RunnerApiOptions? options = null,
        IRunnerLeaseEpochSource? epochs = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(bindings);

        this.store = store;
        this.index = store as IWorkflowDispatchIndex
            ?? throw new ArgumentException("The runner API's store must implement IWorkflowDispatchIndex; without it there is no claimable set to offer.", nameof(store));
        this.bindings = bindings;
        this.options = options ?? new RunnerApiOptions();
        this.epochs = epochs ?? new MonotonicRunnerLeaseEpochSource(timeProvider);
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
        IReadOnlyList<string> environments = await this.bindings.ResolveAsync(principal, cancellationToken).ConfigureAwait(false);
        if (environments.Count == 0 || hostedVersions.Count == 0)
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

        WorkflowLease? extended = await this.store.TryExtendLeaseAsync(
            new WorkflowLease(id, principal, storeToken, default),
            this.options.BoundLease(requestedExtension),
            cancellationToken).ConfigureAwait(false);

        // The epoch is the grant's, not the extension's: one is minted per grant, so renewing does not invalidate a
        // checkpoint already written under it. The store keeps the same token across an extension, so the presented
        // header value stays valid and is echoed unchanged.
        return extended is { } lease ? new RunnerLeaseGrant(leaseToken!, lease.ExpiresAt, epoch) : null;
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
    /// which would tell a caller about a lease it does not hold.
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

        if (!RunnerLeaseToken.TryParse(leaseToken, out _, out string storeToken))
        {
            return false;
        }

        return await this.store.TryExtendLeaseAsync(
            new WorkflowLease(id, principal, storeToken, default),
            TimeSpan.Zero,
            cancellationToken).ConfigureAwait(false) is not null;
    }

    // Confirms, under the lease, that the run is one this runner should have been offered, and projects what the claim
    // reports. The index query already constrains the candidate set, but it ran before the lease: the run may have been
    // completed, suspended, or deleted in between, and re-reading here is what makes the claim's answer true rather than
    // assumed.
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

        long epoch = this.epochs.NextEpoch(held.RunId);
        var grant = new RunnerLeaseGrant(RunnerLeaseToken.Issue(epoch, held.Token), held.ExpiresAt, epoch);
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