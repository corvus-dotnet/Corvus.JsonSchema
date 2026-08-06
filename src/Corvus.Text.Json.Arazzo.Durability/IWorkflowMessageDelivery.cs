// <copyright file="IWorkflowMessageDelivery.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Delivers a message that arrived on a channel to every suspended run awaiting it, and resumes them.
/// </summary>
/// <remarks>
/// <para>
/// This exists because a message handler should not know how its runner reaches durable state. The two ways differ in
/// what they need to scope the delivery — a store-backed worker takes the runner's environment, while the runner API
/// resolves that server-side from the machine principal and instead needs the versions the runner has baked — and
/// neither belongs in a handler that only wants to say "this decision arrived, resume whoever was waiting for it".
/// </para>
/// <para>
/// It is also what keeps the dependency pointing the right way. The system-workflow handlers live in a library the
/// control-plane server references; without this seam, moving them onto the runner API would make the control plane
/// transitively depend on the runner's client, which is the separation ADR 0065 exists to create.
/// </para>
/// <para>
/// The scoping and the resumer are bound into the implementation rather than passed per call, because they are
/// properties of the runner rather than of the message.
/// </para>
/// </remarks>
public interface IWorkflowMessageDelivery
{
    /// <summary>Delivers a message to every suspended run awaiting it.</summary>
    /// <param name="channel">The channel the message arrived on.</param>
    /// <param name="correlationId">The message's correlation token, or <see langword="null"/> to match every run
    /// awaiting the channel whatever correlation each awaits.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs resumed. Zero is not always routine: a handler that expected exactly one waiting run
    /// should treat it as an anomaly and say so.</returns>
    ValueTask<int> DeliverAsync(string channel, string? correlationId, JsonElement payload, CancellationToken cancellationToken);
}