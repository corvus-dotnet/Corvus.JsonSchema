// <copyright file="IWorkflowDispatchIndex.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The store-as-queue dispatch capability: surfaces the runs a runner may claim and start. A sibling to
/// <see cref="IWorkflowWaitIndex"/> (which surfaces suspended runs due to resume), implemented by the state
/// store alongside it. There is no separate queue — the run record is the durable work item, and one
/// concurrency mechanism (CAS + leases) serves both fresh dispatch and resume.
/// </summary>
public interface IWorkflowDispatchIndex
{
    /// <summary>
    /// Finds runs a runner hosting <paramref name="hostedWorkflowIds"/> may claim: freshly created
    /// <see cref="WorkflowRunStatus.Pending"/> runs, and <see cref="WorkflowRunStatus.Running"/> runs whose
    /// lease has expired (orphans left by a crashed runner, reclaimed from the last checkpoint). Returning
    /// orphans is essential — otherwise a run interrupted mid-step would never be picked up again.
    /// </summary>
    /// <param name="hostedWorkflowIds">The versioned workflow ids (<c>{base}-v{n}</c>) the runner hosts; only runs for these are returned.</param>
    /// <param name="now">The current instant, used to decide whether a <see cref="WorkflowRunStatus.Running"/> run's lease has expired.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The ids of the claimable runs.</returns>
    IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, CancellationToken cancellationToken);
}