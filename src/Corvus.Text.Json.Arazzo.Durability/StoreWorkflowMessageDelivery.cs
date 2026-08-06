// <copyright file="StoreWorkflowMessageDelivery.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Delivers messages over a <see cref="WorkflowWorker"/>, for a host that reaches the durable store directly.
/// </summary>
/// <remarks>
/// The environment is bound here rather than taken per message, and it is what stops a decision published on a shared
/// channel resuming a run in a different environment that happens to await the same one. A host with no environment to
/// name (an in-process one, where there is only ever the one) passes <see langword="null"/>.
/// </remarks>
/// <param name="worker">The worker that leases and resumes suspended runs over the store.</param>
/// <param name="resumer">The resumer that re-enters a run's generated executor.</param>
/// <param name="runnerEnvironment">The single environment this runner serves, or <see langword="null"/> for the
/// environment-agnostic form.</param>
public sealed class StoreWorkflowMessageDelivery(WorkflowWorker worker, WorkflowResumer resumer, string? runnerEnvironment)
    : IWorkflowMessageDelivery
{
    /// <inheritdoc/>
    public ValueTask<int> DeliverAsync(string channel, string? correlationId, JsonElement payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(resumer);

        return worker.DeliverMessageAsync(channel, correlationId, payload, resumer, runnerEnvironment, cancellationToken);
    }
}