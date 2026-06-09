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
}