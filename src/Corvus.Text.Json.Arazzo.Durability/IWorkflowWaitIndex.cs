// <copyright file="IWorkflowWaitIndex.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The optional capability a richer store adds on top of <see cref="IWorkflowStateStore"/> (plan §10): an
/// index that finds suspended runs which are now resumable — a <em>due</em> timer or an <em>awaited</em>
/// correlated message — plus the operator visibility query. A worker negotiates it with
/// <c>store is IWorkflowWaitIndex</c>; a blob-only store can implement just the core and delegate timers to
/// scheduled messages.
/// </summary>
/// <remarks>
/// The index is fed by the <see cref="WorkflowRunIndexEntry"/> the runtime projects on every save, so a
/// backend never parses the checkpoint. The same index serves the Tier-2 worker wakeups <em>and</em> the
/// §11 control-plane queries.
/// </remarks>
public interface IWorkflowWaitIndex
{
    /// <summary>Finds suspended runs whose durable timer is due at or before <paramref name="before"/>.</summary>
    /// <param name="before">The cutoff instant (typically "now").</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The ids of the due runs.</returns>
    IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, CancellationToken cancellationToken);

    /// <summary>
    /// The environment-scoped overload (design §5.5): as <see cref="QueryDueAsync(DateTimeOffset, CancellationToken)"/>,
    /// but additionally constrains due runs to those pinned to <strong>exactly</strong> <paramref name="runnerEnvironment"/>.
    /// A real runner (non-null <paramref name="runnerEnvironment"/>) never resumes a run pinned to a different environment,
    /// nor an unpinned one — a run started against environment <em>E</em> is only ever resumed by a runner serving <em>E</em>,
    /// exactly as dispatch is scoped (see <see cref="IWorkflowDispatchIndex.QueryClaimableAsync(IReadOnlyCollection{string}, string?, DateTimeOffset, CancellationToken)"/>).
    /// A <see langword="null"/> <paramref name="runnerEnvironment"/> is the env-agnostic base overload: an in-process host
    /// that owns the whole store resumes every due run regardless of environment (deliberately unscoped).
    /// </summary>
    /// <param name="before">The cutoff instant (typically "now").</param>
    /// <param name="runnerEnvironment">The single environment a runner serves — a due run resumes only when pinned to exactly it; <see langword="null"/> is env-agnostic (the base overload, not a runner).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The ids of the due runs pinned to the runner's environment (all due runs when <paramref name="runnerEnvironment"/> is <see langword="null"/>).</returns>
    /// <remarks>The default implementation ignores <paramref name="runnerEnvironment"/> and delegates to the unscoped
    /// overload (the pre-pinning behaviour); a backend overrides it with a native environment-filtered query.</remarks>
    IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, string? runnerEnvironment, CancellationToken cancellationToken)
        => this.QueryDueAsync(before, cancellationToken);

    /// <summary>Finds suspended runs awaiting a message on a channel (optionally for a specific correlation id).</summary>
    /// <param name="channel">The channel a message was delivered on.</param>
    /// <param name="correlationId">The correlation id of the delivered message, or <see langword="null"/> to match runs awaiting the channel with no specific correlation.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The ids of the runs the message can resume.</returns>
    IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, CancellationToken cancellationToken);

    /// <summary>Runs an operator visibility query (plan §11).</summary>
    /// <param name="query">The query (status / workflow-id filters and a limit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A page of matching runs.</returns>
    ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken);

    /// <summary>Counts the runs matching a visibility query, <b>bounded</b> at <paramref name="cap"/> — the count-API
    /// contract: return the exact number when it is at or below the cap, or <c>(cap, Capped: true)</c> once more than
    /// <paramref name="cap"/> match, so a badge/footer can render "<c>N</c>" or "<c>N+</c>" without materialising rows
    /// and without paying to count an unbounded set. Honours the query's <see cref="WorkflowQuery.Security"/> reach
    /// exactly as <see cref="QueryAsync"/> does (same predicate, so it cannot drift).</summary>
    /// <param name="query">The query; its <see cref="WorkflowQuery.Limit"/> and <see cref="WorkflowQuery.ContinuationToken"/> are ignored (the count is over the whole matching set, bounded by <paramref name="cap"/>).</param>
    /// <param name="cap">The upper bound: counting stops once <paramref name="cap"/> is exceeded.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bounded count and whether it was capped.</returns>
    /// <remarks>The default counts a single bounded page (<c>Limit = cap + 1</c>); a backend that can count natively
    /// (a relational <c>COUNT</c> over a <c>LIMIT cap+1</c> sub-select, a bounded scan) overrides it to avoid
    /// materialising the listings. The default still honours reach because it flows the same <see cref="WorkflowQuery.Security"/> through <see cref="QueryAsync"/>.</remarks>
    async ValueTask<(int Count, bool Capped)> CountAsync(WorkflowQuery query, int cap, CancellationToken cancellationToken)
    {
        using WorkflowRunPage page = await this.QueryAsync(query with { Limit = cap + 1, ContinuationToken = default }, cancellationToken).ConfigureAwait(false);
        int count = page.Runs.Count;
        return count > cap ? (cap, true) : (count, false);
    }
}