// <copyright file="WorkflowManagementClient.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The default <see cref="IWorkflowManagementClient"/> over an <see cref="IWorkflowStateStore"/> (plan §11).
/// Visibility queries use the store's <see cref="IWorkflowWaitIndex"/> (the same index Tier 2 uses for wakeups);
/// control operations take a single-owner lease and write under optimistic concurrency. Resuming a faulted run
/// re-executes it through a host-supplied <see cref="WorkflowResumer"/> — the same adapter a
/// <see cref="WorkflowWorker"/> uses — so the run advances from its last checkpoint (the faulted step), and the
/// generated executor clears the fault on its next checkpoint.
/// </summary>
public sealed class WorkflowManagementClient : IWorkflowManagementClient
{
    private readonly IWorkflowStateStore store;
    private readonly IWorkflowWaitIndex? index;
    private readonly WorkflowResumer? resumer;
    private readonly TimeProvider timeProvider;
    private readonly string owner;
    private readonly TimeSpan leaseTtl;

    /// <summary>Initializes a new instance of the <see cref="WorkflowManagementClient"/> class.</summary>
    /// <param name="store">The state store. Visibility queries (<see cref="ListAsync"/>/<see cref="PurgeAsync"/>) require it to also implement <see cref="IWorkflowWaitIndex"/>.</param>
    /// <param name="owner">This client's identity, used to take run leases.</param>
    /// <param name="resumer">The adapter that re-enters a run's generated executor; required for <see cref="ResumeAsync"/>.</param>
    /// <param name="timeProvider">The time source for index timestamps and lease TTLs; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="leaseTtl">How long a lease is held during a control operation; defaults to one minute.</param>
    public WorkflowManagementClient(
        IWorkflowStateStore store,
        string owner,
        WorkflowResumer? resumer = null,
        TimeProvider? timeProvider = null,
        TimeSpan? leaseTtl = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(owner);
        this.store = store;
        this.index = store as IWorkflowWaitIndex;
        this.resumer = resumer;
        this.owner = owner;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.leaseTtl = leaseTtl ?? TimeSpan.FromMinutes(1);
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowRunPage> ListAsync(WorkflowQuery query, CancellationToken cancellationToken)
        => this.RequireIndex().QueryAsync(query, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunDetail?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (checkpoint is not { } cp)
        {
            return null;
        }

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(cp.Utf8);
        return new WorkflowRunDetail(state.RunId, state.WorkflowId, state.Status, state.Cursor, state.CreatedAt, state.Wait, state.Fault, cp.Etag);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ResumeAsync(WorkflowRunId id, ResumeOptions options, CancellationToken cancellationToken)
    {
        if (options.Mode != ResumeMode.RetryFaultedStep)
        {
            throw new NotSupportedException($"Resume mode '{options.Mode}' is not yet supported.");
        }

        if (this.resumer is null)
        {
            throw new InvalidOperationException("This management client was created without a resumer; ResumeAsync requires one.");
        }

        WorkflowLease? lease = await this.store.AcquireLeaseAsync(id, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            // Another owner (operator or worker) is acting on this run.
            return false;
        }

        try
        {
            using WorkflowRun? run = await WorkflowRun.ResumeAsync(this.store, id, this.timeProvider, cancellationToken).ConfigureAwait(false);
            if (run is null || run.Status != WorkflowRunStatus.Faulted)
            {
                // Only a faulted run is retriable; it may have been resumed, cancelled, or deleted meanwhile.
                return false;
            }

            // Re-enter the executor at the faulted step; its first checkpoint clears the fault and sets Running.
            await this.resumer(run, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            await this.store.ReleaseLeaseAsync(lease.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> CancelAsync(WorkflowRunId id, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);

        WorkflowLease? lease = await this.store.AcquireLeaseAsync(id, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return false;
        }

        try
        {
            WorkflowCheckpoint? checkpoint = await this.store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
            if (checkpoint is not { } cp)
            {
                return false;
            }

            byte[] updated;
            WorkflowRunIndexEntry indexEntry;
            using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(cp.Utf8))
            {
                if (state.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Cancelled)
                {
                    // Terminal already; nothing to cancel.
                    return false;
                }

                // Mark cancelled, clear any wait, but keep the fault record (if any) for post-mortem visibility.
                updated = WorkflowCheckpointSerializer.Serialize(
                    state.RunId,
                    state.WorkflowId,
                    WorkflowRunStatus.Cancelled,
                    state.Cursor,
                    state.CreatedAt,
                    state.RetryCounters,
                    state.CorrelationTokens,
                    state.Inputs,
                    state.StepOutputs,
                    state.Outputs,
                    wait: null,
                    fault: state.Fault);

                indexEntry = new WorkflowRunIndexEntry(
                    state.WorkflowId,
                    WorkflowRunStatus.Cancelled,
                    state.CreatedAt,
                    this.timeProvider.GetUtcNow(),
                    ErrorType: state.Fault?.Error);
            }

            await this.store.SaveAsync(id, updated, indexEntry, cp.Etag, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (WorkflowConflictException)
        {
            // A worker or another operator wrote concurrently; the caller can retry.
            return false;
        }
        finally
        {
            await this.store.ReleaseLeaseAsync(lease.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> PurgeAsync(WorkflowPurgeQuery query, CancellationToken cancellationToken)
    {
        IWorkflowWaitIndex waitIndex = this.RequireIndex();

        int purged = 0;
        foreach (WorkflowRunStatus status in new[] { WorkflowRunStatus.Completed, WorkflowRunStatus.Cancelled })
        {
            WorkflowRunPage page = await waitIndex.QueryAsync(new WorkflowQuery(status, null, query.Limit), cancellationToken).ConfigureAwait(false);
            foreach (WorkflowRunListing listing in page.Runs)
            {
                if (purged >= query.Limit)
                {
                    return purged;
                }

                if (listing.Index.UpdatedAt >= query.OlderThan)
                {
                    continue;
                }

                await this.store.DeleteAsync(listing.Id, cancellationToken).ConfigureAwait(false);
                purged++;
            }
        }

        return purged;
    }

    private IWorkflowWaitIndex RequireIndex()
        => this.index ?? throw new NotSupportedException("The state store must implement IWorkflowWaitIndex for visibility queries (ListAsync/PurgeAsync).");
}