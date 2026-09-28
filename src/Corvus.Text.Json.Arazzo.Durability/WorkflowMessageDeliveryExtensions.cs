// <copyright file="WorkflowMessageDeliveryExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Delivery for a message that a run is expected to be awaiting.
/// </summary>
public static class WorkflowMessageDeliveryExtensions
{
    /// <summary>
    /// The pauses between attempts. They sum to under twenty seconds, which keeps the whole wait inside a broker's
    /// acknowledgement deadline (thirty seconds for the NATS transport's default): the message is acknowledged only
    /// after its handler returns, so a longer wait would have the broker redeliver it while this one is still waiting.
    /// </summary>
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(4),
    ];

    /// <summary>
    /// Delivers a message that a run is expected to be awaiting, trying again for a bounded window while no run awaits it
    /// yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A run sends the message that prompts a reply and then suspends to await the reply. The reply can arrive in that
    /// gap: an administrator who decides an access request at once, or a verifier that answers within milliseconds,
    /// publishes before the run's wait is registered. Delivered once, it resumes nothing, and the run then waits for a
    /// reply that has already gone. Trying again closes the gap, and it stays at-least-once, because the broker
    /// acknowledges the message only after the handler returns.
    /// </para>
    /// <para>
    /// The window is bounded because a message with no run to resume at all is possible too, and it must not be held
    /// for ever. When the window closes with nothing resumed this returns zero, and the caller reports it as it always
    /// has.
    /// </para>
    /// </remarks>
    /// <param name="delivery">The delivery.</param>
    /// <param name="channel">The channel the message arrived on.</param>
    /// <param name="correlationId">The message's correlation token, or <see langword="null"/> to match every run
    /// awaiting the channel.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="timeProvider">The time source for the pauses between attempts.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs resumed, or zero when none awaited the message within the window.</returns>
    public static async ValueTask<int> DeliverToAwaitingRunAsync(
        this IWorkflowMessageDelivery delivery,
        string channel,
        string? correlationId,
        JsonElement payload,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(timeProvider);

        int resumed = await delivery.DeliverAsync(channel, correlationId, payload, cancellationToken).ConfigureAwait(false);
        for (int i = 0; resumed == 0 && i < RetryDelays.Length; i++)
        {
            await Task.Delay(RetryDelays[i], timeProvider, cancellationToken).ConfigureAwait(false);
            resumed = await delivery.DeliverAsync(channel, correlationId, payload, cancellationToken).ConfigureAwait(false);
        }

        return resumed;
    }
}