// <copyright file="MessageTransportReceiveExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.ExceptionServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// One-shot receive helpers over the strongly-typed subscriber.
/// </summary>
public static class MessageTransportReceiveExtensions
{
    /// <summary>
    /// Awaits a single message on a channel: subscribes with the strongly-typed subscriber, invokes
    /// <paramref name="onMessage"/> with the first delivered message, then unsubscribes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delivered payload is valid only for the duration of <paramref name="onMessage"/> — the
    /// transport recycles its parse buffer once the handler returns. A caller that needs the message (or
    /// values projected from it) to outlive the receive must copy what it needs into its own workspace
    /// <em>inside</em> the handler. Running the projection in-handler lets a caller copy only the values
    /// it actually uses rather than cloning the whole message first.
    /// </para>
    /// <para>
    /// The handler runs inline on the delivering thread (continuations are not forced asynchronous), so a
    /// thread-affine pooled <see cref="JsonWorkspace"/> the handler writes into stays on its owning thread.
    /// An exception thrown by the handler is captured and re-thrown to the awaiting caller after the
    /// subscription is torn down.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPayload">The message payload type the subscriber parses into.</typeparam>
    /// <param name="transport">The message transport.</param>
    /// <param name="channelUtf8">The channel address as UTF-8 bytes.</param>
    /// <param name="onMessage">The handler invoked with the first delivered payload and its headers while the payload is live.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once a message has been handled and the subscription removed.</returns>
    public static async ValueTask ReceiveOneAsync<TPayload>(
        this IMessageTransport transport,
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, ValueTask> onMessage,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(onMessage);

        // Continuations run inline on the delivering thread so the awaiting caller resumes there rather
        // than hopping to the thread pool (a pooled JsonWorkspace the handler writes into is thread-affine).
        var completion = new TaskCompletionSource();
        ExceptionDispatchInfo? failure = null;

        await transport.SubscribeAsync<TPayload>(
            channelUtf8,
            async (payload, headers, ct) =>
            {
                try
                {
                    await onMessage(payload, headers).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    completion.TrySetResult();
                }
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

        failure?.Throw();
    }
}