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

    /// <summary>
    /// The environment-scoped overload (design §5.5): as <see cref="QueryClaimableAsync(IReadOnlyCollection{string}, DateTimeOffset, CancellationToken)"/>,
    /// but additionally constrains claimable runs to those pinned to <strong>exactly</strong> <paramref name="runnerEnvironment"/>.
    /// A real runner (non-null <paramref name="runnerEnvironment"/>) never claims a run with no pinned environment, nor one
    /// pinned to a different environment — a run started against environment <em>E</em> is only ever dispatched to a runner
    /// serving <em>E</em> (the credential boundary). A <see langword="null"/> <paramref name="runnerEnvironment"/> is the
    /// env-agnostic base overload (the parameterless overload delegates to it): it lists all claimable runs regardless of
    /// environment and is not a runner — the <c>WorkflowDispatcher</c> rejects an unscoped runner, so every dispatch query is strict.
    /// </summary>
    /// <param name="hostedWorkflowIds">The versioned workflow ids the runner hosts; only runs for these are returned.</param>
    /// <param name="runnerEnvironment">The single environment a runner serves — a run is claimable only when pinned to exactly it; <see langword="null"/> is env-agnostic (the base overload, not a runner).</param>
    /// <param name="now">The current instant, used to decide whether a <see cref="WorkflowRunStatus.Running"/> run's lease has expired.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The ids of the claimable runs pinned to (or unpinned relative to) the runner's environment.</returns>
    /// <remarks>The default implementation ignores <paramref name="runnerEnvironment"/> and delegates to the unscoped
    /// overload (the pre-pinning behaviour); a backend overrides it with a native environment-filtered query.</remarks>
    IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, string? runnerEnvironment, DateTimeOffset now, CancellationToken cancellationToken)
        => this.QueryClaimableAsync(hostedWorkflowIds, now, cancellationToken);
}