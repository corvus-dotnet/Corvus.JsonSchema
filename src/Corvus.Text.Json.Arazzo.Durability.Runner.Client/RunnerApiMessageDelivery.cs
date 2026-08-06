// <copyright file="RunnerApiMessageDelivery.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// Delivers messages over the runner API, for a runner that holds no store credential (ADR 0065).
/// </summary>
/// <remarks>
/// <para>
/// No environment is named. The control plane intersects the candidate set with the environments an administrator bound
/// this runner's machine principal to, so the scoping a store-backed delivery applies runner-side is enforced
/// server-side here. That is the difference between cooperation and enforcement: a runner cannot deliver into an
/// environment it was never authorized for, however it asks.
/// </para>
/// <para>
/// The hosted set is asked for per delivery rather than bound at construction, because it moves: a version published
/// while the runner is up must become deliverable without a restart. It comes from the runner's shared
/// <see cref="RunnerHostedVersions"/>, so a delivery and a dispatch always agree about what this runner has baked, and
/// the resolution is not repeated per message.
/// </para>
/// <para>
/// The payload never leaves the runner. The API answers which runs a message can resume, and the runner hands its own
/// copy to each of them, so the control plane learns that a message arrived on a channel and not what it said.
/// </para>
/// </remarks>
/// <param name="worker">The runner-API worker that claims and resumes the awaiting runs.</param>
/// <param name="hostedVersions">The runner's shared answer to which versions it has baked.</param>
/// <param name="resumer">The resumer that re-enters a run's generated executor.</param>
public sealed class RunnerApiMessageDelivery(RunnerApiWorker worker, RunnerHostedVersions hostedVersions, WorkflowResumer resumer)
    : IWorkflowMessageDelivery
{
    /// <inheritdoc/>
    public async ValueTask<int> DeliverAsync(string channel, string? correlationId, JsonElement payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(hostedVersions);
        ArgumentNullException.ThrowIfNull(resumer);

        IReadOnlyList<string> hosted = await hostedVersions.GetAsync(cancellationToken).ConfigureAwait(false);
        if (hosted.Count == 0)
        {
            // Bound to nothing, or nothing baked: there is no run this runner could resume, and claiming would only
            // take work it cannot advance. The same answer a pending or revoked runner gets everywhere else.
            return 0;
        }

        return await worker.DeliverMessageAsync(channel, correlationId, payload, hosted, resumer, cancellationToken).ConfigureAwait(false);
    }
}