// <copyright file="WorkflowWorker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Re-enters a generated <see cref="WorkflowRun"/> to advance it. The worker knows nothing about a specific
/// workflow's types; the host supplies a <paramref name="run"/>-to-executor adapter as a
/// <see cref="WorkflowResumer"/> (typically a small closure that parses <see cref="WorkflowRun.Inputs"/> into
/// the workflow's inputs type and calls its generated <c>ExecuteAsync(…, run)</c>).
/// </summary>
/// <param name="run">The loaded run to advance (already holding any delivered message).</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <returns>The outcome kind of the re-entry.</returns>
public delegate ValueTask<WorkflowRunResultKind> WorkflowResumer(WorkflowRun run, CancellationToken cancellationToken);

/// <summary>
/// The Tier-2 trigger loop (plan §9.4): it resumes suspended runs when their wait is satisfied — a due timer
/// or a delivered correlated message — by querying the store's <see cref="IWorkflowWaitIndex"/>, taking a
/// single-owner lease per run, loading the checkpoint, handing in any delivered message, and re-entering the
/// generated executor through a host-supplied <see cref="WorkflowResumer"/>.
/// </summary>
public sealed class WorkflowWorker
{
    private readonly IWorkflowStateStore store;
    private readonly IWorkflowWaitIndex index;
    private readonly TimeProvider timeProvider;
    private readonly string owner;
    private readonly TimeSpan leaseTtl;

    /// <summary>Initializes a new instance of the <see cref="WorkflowWorker"/> class.</summary>
    /// <param name="store">The state store; it must also implement <see cref="IWorkflowWaitIndex"/>.</param>
    /// <param name="owner">This worker's identity, used to take run leases.</param>
    /// <param name="timeProvider">The time source for due-timer evaluation and lease TTLs; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="leaseTtl">How long a run lease is held while the worker advances it; defaults to one minute.</param>
    /// <exception cref="ArgumentException">The store does not provide a wait index.</exception>
    public WorkflowWorker(IWorkflowStateStore store, string owner, TimeProvider? timeProvider = null, TimeSpan? leaseTtl = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(owner);
        this.store = store;
        this.index = store as IWorkflowWaitIndex
            ?? throw new ArgumentException("The state store must implement IWorkflowWaitIndex to drive a worker.", nameof(store));
        this.owner = owner;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.leaseTtl = leaseTtl ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>Resumes every suspended run whose durable timer is now due.</summary>
    /// <param name="resume">The adapter that re-enters the run's generated executor.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs this worker resumed (runs another worker held are skipped).</returns>
    public async ValueTask<int> ResumeDueTimersAsync(WorkflowResumer resume, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resume);

        int resumed = 0;
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await foreach (WorkflowRunId id in this.index.QueryDueAsync(now, cancellationToken).ConfigureAwait(false))
        {
            if (await this.TryResumeAsync(id, deliveredMessage: default, hasDelivered: false, resume, cancellationToken).ConfigureAwait(false))
            {
                resumed++;
            }
        }

        return resumed;
    }

    /// <summary>
    /// Delivers a message to every suspended run awaiting it on <paramref name="channel"/> (optionally
    /// matching <paramref name="correlationId"/>) and resumes them.
    /// </summary>
    /// <param name="channel">The channel the message arrived on.</param>
    /// <param name="correlationId">The correlation id of the message, or <see langword="null"/> to match runs awaiting the channel with no specific correlation.</param>
    /// <param name="payload">The message payload; it must stay alive until this call returns.</param>
    /// <param name="resume">The adapter that re-enters the run's generated executor.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs this worker resumed with the message.</returns>
    public async ValueTask<int> DeliverMessageAsync(string channel, string? correlationId, JsonElement payload, WorkflowResumer resume, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(resume);

        int resumed = 0;
        await foreach (WorkflowRunId id in this.index.QueryAwaitingAsync(channel, correlationId, cancellationToken).ConfigureAwait(false))
        {
            if (await this.TryResumeAsync(id, payload, hasDelivered: true, resume, cancellationToken).ConfigureAwait(false))
            {
                resumed++;
            }
        }

        return resumed;
    }

    private async ValueTask<bool> TryResumeAsync(WorkflowRunId id, JsonElement deliveredMessage, bool hasDelivered, WorkflowResumer resume, CancellationToken cancellationToken)
    {
        WorkflowLease? lease = await this.store.AcquireLeaseAsync(id, this.owner, this.leaseTtl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            // Another worker is advancing this run.
            return false;
        }

        try
        {
            using WorkflowRun? run = await WorkflowRun.ResumeAsync(this.store, id, this.timeProvider, cancellationToken).ConfigureAwait(false);
            if (run is null || run.Status != WorkflowRunStatus.Suspended)
            {
                // The run was completed, faulted, or deleted between the query and the lease.
                return false;
            }

            if (hasDelivered)
            {
                run.DeliverMessage(deliveredMessage);
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