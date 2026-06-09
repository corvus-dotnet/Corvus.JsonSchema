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

    /// <summary>
    /// Awaits a single request on a channel and replies to it once: subscribes with the request/reply
    /// responder (<see cref="IMessageTransport.SubscribeReplyAsync{TRequest, TReply}"/>), invokes
    /// <paramref name="onRequest"/> with the first delivered request to obtain the reply, lets the transport
    /// publish that reply correlated to the request, then unsubscribes — the responder counterpart of
    /// <see cref="ReceiveOneAsync{TPayload}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delivered request is valid only for the duration of <paramref name="onRequest"/>; the reply it
    /// returns is serialized by the transport synchronously as the handler completes (before the request and
    /// the caller's workspace are recycled), so the reply may reference the live request, the workflow
    /// inputs, or prior step outputs without being copied. A caller that needs values projected from the
    /// request to outlive the receive must copy them into its own workspace <em>inside</em> the handler.
    /// </para>
    /// <para>
    /// The handler runs inline on the delivering thread (continuations are not forced asynchronous), so a
    /// thread-affine pooled <see cref="JsonWorkspace"/> the handler writes into stays on its owning thread.
    /// An exception thrown by the handler is captured and re-thrown to the awaiting caller after the
    /// subscription is torn down; the transport still publishes whatever reply the responder produced.
    /// </para>
    /// </remarks>
    /// <typeparam name="TRequest">The request payload type the responder parses into.</typeparam>
    /// <typeparam name="TReply">The reply payload type the handler returns.</typeparam>
    /// <param name="transport">The message transport.</param>
    /// <param name="channelUtf8">The request channel address as UTF-8 bytes.</param>
    /// <param name="onRequest">The handler invoked with the first delivered request and its headers, returning the reply payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once a request has been handled and the subscription removed.</returns>
    public static async ValueTask ReceiveOneAndReplyAsync<TRequest, TReply>(
        this IMessageTransport transport,
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, JsonElement, ValueTask<TReply>> onRequest,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(onRequest);

        // Continuations run inline on the delivering thread (as in ReceiveOneAsync) so the awaiting caller —
        // and the workflow output-building that follows it — resumes there rather than hopping to the thread
        // pool (a pooled JsonWorkspace the responder writes into is thread-affine). The transport publishes
        // the reply on its own channel after the handler returns, so it is unaffected by the unsubscribe.
        var completion = new TaskCompletionSource();
        ExceptionDispatchInfo? failure = null;

        await transport.SubscribeReplyAsync<TRequest, TReply>(
            channelUtf8,
            async (request, headers, ct) =>
            {
                try
                {
                    return await onRequest(request, headers).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Capture the failure for the awaiting workflow, then let it propagate to the transport
                    // so its error policy suppresses the reply rather than publishing an Undefined default
                    // — a failed responder sends no reply (the requester times out), like a failed consumer.
                    failure = ExceptionDispatchInfo.Capture(ex);
                    throw;
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