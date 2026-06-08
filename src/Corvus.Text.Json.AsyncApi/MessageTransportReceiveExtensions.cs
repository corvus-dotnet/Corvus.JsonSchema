// <copyright file="MessageTransportReceiveExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// One-shot receive helpers over the strongly-typed subscriber.
/// </summary>
public static class MessageTransportReceiveExtensions
{
    /// <summary>
    /// Awaits a single message on a channel: subscribes with the strongly-typed subscriber, captures the
    /// first delivered message, then unsubscribes. The typed payload is delivered (and validated) through
    /// the transport's normal <c>SubscribeAsync</c> pipeline; it is cloned into <paramref name="workspace"/>
    /// while the delivery is in scope so the result outlives the subscription.
    /// </summary>
    /// <typeparam name="TPayload">The message payload type the subscriber parses into.</typeparam>
    /// <param name="transport">The message transport.</param>
    /// <param name="channelUtf8">The channel address as UTF-8 bytes.</param>
    /// <param name="workspace">The workspace that takes ownership of the returned payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The received payload, owned by <paramref name="workspace"/>.</returns>
    public static async ValueTask<JsonElement> ReceiveOneAsync<TPayload>(
        this IMessageTransport transport,
        ReadOnlyMemory<byte> channelUtf8,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(workspace);

        JsonElement received = default;

        // Continuations run inline on the delivering thread so the awaiting caller resumes there rather
        // than hopping to the thread pool (a pooled JsonWorkspace is thread-affine).
        var completion = new TaskCompletionSource();

        await transport.SubscribeAsync<TPayload>(
            channelUtf8,
            (payload, headers, ct) =>
            {
                // The delivered payload is only valid for the duration of the handler, so clone it into
                // the caller's workspace (the same lifetime primitive used for an HTTP response body).
                // JsonElement.From is the free implicit bind from any Corvus JSON value to a JsonElement.
                received = JsonElement.From(payload).CloneAsBuilder(workspace).RootElement;
                completion.TrySetResult();
                return default;
            },
            cancellationToken).ConfigureAwait(false);

        try
        {
            await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await transport.UnsubscribeAsync(channelUtf8, cancellationToken).ConfigureAwait(false);
        }

        return received;
    }
}