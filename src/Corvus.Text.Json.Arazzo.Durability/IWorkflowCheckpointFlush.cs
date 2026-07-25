// <copyright file="IWorkflowCheckpointFlush.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An optional capability of an <see cref="IWorkflowCheckpointStore"/> whose checkpoint writes are deferred
/// (fire-and-forget), letting a host force the pending writes to complete and confirm the terminal one landed
/// before it reports an outcome. A store that commits each checkpoint synchronously — every database backend —
/// does not implement this; the serverless function-side HTTP store does, so its interim checkpoints can be
/// fire-and-forget while the run's final state is still durably confirmed. Negotiated with
/// <c>store is IWorkflowCheckpointFlush</c>, in the same spirit as <c>store is IWorkflowWaitIndex</c>.
/// </summary>
public interface IWorkflowCheckpointFlush
{
    /// <summary>
    /// Completes any deferred checkpoint writes and confirms the most recent (terminal) one committed durably.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the terminal checkpoint is durable.</returns>
    /// <exception cref="InvalidOperationException">The terminal checkpoint did not commit, so the run's outcome must not be reported — the run stays claimable for re-invocation.</exception>
    ValueTask FlushAsync(CancellationToken cancellationToken);
}