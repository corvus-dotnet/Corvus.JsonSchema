// <copyright file="WorkflowDispatcher.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Claims and starts runs from the store-as-queue dispatch index — the new-run and orphan-reclaim counterpart
/// to <see cref="WorkflowWorker"/> (which resumes suspended runs from the wait index). A runner polls
/// <see cref="DispatchClaimableAsync"/> for the versions it hosts; each claimable run is leased (CAS, so a
/// run another runner holds is skipped), loaded, and driven through the same <see cref="WorkflowResumer"/>
/// that resume uses — a fresh <see cref="WorkflowRunStatus.Pending"/> run starts at cursor 0, an orphaned
/// <see cref="WorkflowRunStatus.Running"/> run re-enters at its last checkpoint.
/// </summary>
public sealed class WorkflowDispatcher
{
    private readonly IWorkflowStateStore store;
    private readonly IWorkflowDispatchIndex index;
    private readonly TimeProvider timeProvider;
    private readonly string owner;
    private readonly TimeSpan leaseTtl;

    /// <summary>Initializes a new instance of the <see cref="WorkflowDispatcher"/> class.</summary>
    /// <param name="store">The state store; must also implement <see cref="IWorkflowDispatchIndex"/>.</param>
    /// <param name="owner">This runner's opaque identity, used as the lease owner.</param>
    /// <param name="timeProvider">The time source for lease TTLs and orphan detection; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="leaseTtl">How long a per-run lease is held; defaults to one minute.</param>
    /// <exception cref="ArgumentException">The store does not implement <see cref="IWorkflowDispatchIndex"/>.</exception>
    public WorkflowDispatcher(IWorkflowStateStore store, string owner, TimeProvider? timeProvider = null, TimeSpan? leaseTtl = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(owner);
        this.store = store;
        this.index = store as IWorkflowDispatchIndex
            ?? throw new ArgumentException("The state store must implement IWorkflowDispatchIndex to drive a dispatcher.", nameof(store));
        this.owner = owner;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.leaseTtl = leaseTtl ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Claims and runs every currently-claimable run for the hosted versions, returning how many ran. A run
    /// another runner holds (live lease) is skipped; a run that changed state between the query and the lease
    /// (already completed, suspended, etc.) is skipped.
    /// </summary>
    /// <param name="hostedWorkflowIds">The versioned workflow ids this runner hosts.</param>
    /// <param name="resume">The resumer that resolves the run's executor and runs it (typically a hosted-workflow resumer).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs dispatched.</returns>
    public async ValueTask<int> DispatchClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, WorkflowResumer resume, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostedWorkflowIds);
        ArgumentNullException.ThrowIfNull(resume);

        int dispatched = 0;
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await foreach (WorkflowRunId id in this.index.QueryClaimableAsync(hostedWorkflowIds, now, cancellationToken).ConfigureAwait(false))
        {
            if (await this.TryDispatchAsync(id, resume, cancellationToken).ConfigureAwait(false))
            {
                dispatched++;
            }
        }

        return dispatched;
    }

    private async ValueTask<bool> TryDispatchAsync(WorkflowRunId id, WorkflowResumer resume, CancellationToken cancellationToken)
    {
        WorkflowLease? lease = await this.store.AcquireLeaseAsync(id, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            // Another runner holds the run.
            return false;
        }

        try
        {
            using WorkflowRun? run = await WorkflowRun.ResumeAsync(this.store, id, this.timeProvider, cancellationToken).ConfigureAwait(false);

            // Re-check under the lease: the run may have been claimed, completed, suspended, or deleted between
            // the index query and the lease. Only fresh (Pending) and orphaned (Running) runs are dispatchable.
            if (run is null || run.Status is not (WorkflowRunStatus.Pending or WorkflowRunStatus.Running))
            {
                return false;
            }

            await resume(run, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            await this.store.ReleaseLeaseAsync(lease.Value, cancellationToken).ConfigureAwait(false);
        }
    }
}