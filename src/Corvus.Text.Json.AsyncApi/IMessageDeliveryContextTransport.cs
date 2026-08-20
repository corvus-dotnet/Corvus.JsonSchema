// <copyright file="IMessageDeliveryContextTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Optional message-transport capability that exposes delivery metadata.
/// </summary>
/// <remarks>
/// This is separate from <see cref="IMessageTransport"/> so existing transport
/// implementations remain source-compatible and the legacy subscription API is
/// not changed or adapted through an allocating closure.
/// </remarks>
public interface IMessageDeliveryContextTransport : IMessageTransport
{
    /// <summary>Subscribes while exposing transport delivery metadata.</summary>
    /// <remarks>
    /// <para>
    /// The <see cref="MessageDeliveryContext"/> passed to <paramref name="handler"/> is valid
    /// only for the duration of that invocation; transports may recycle the buffers it
    /// references once the handler returns.
    /// </para>
    /// <para>
    /// The <paramref name="channelUtf8"/> memory must remain valid and unmodified for the
    /// lifetime of the subscription: broker transports retain it and hand it to every delivery
    /// as <see cref="MessageDeliveryContext.ChannelUtf8"/>. Do not subscribe with a pooled or
    /// reused buffer. The in-memory testing transport hands each delivery an equal-content copy
    /// rather than the retained buffer, so compare the context's channel by content, never by
    /// memory identity.
    /// </para>
    /// </remarks>
    ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>;

    /// <summary>
    /// Subscribes while exposing transport delivery metadata, with protocol-specific metadata.
    /// </summary>
    /// <remarks>
    /// The counterpart of the <see cref="MessageContext"/> overload of <c>SubscribeAsync</c>: a
    /// delivery-context consumer declared with channel or operation bindings is subscribed with
    /// them, exactly as a plain consumer is. The default implementation drops the context and
    /// forwards, so a transport that has no use for bindings is unaffected and one that honors
    /// them can override.
    /// </remarks>
    /// <typeparam name="TPayload">The deserialized message payload type.</typeparam>
    /// <param name="channelUtf8">The channel address as UTF-8 bytes.</param>
    /// <param name="handler">The handler invoked with each payload and its delivery context.</param>
    /// <param name="context">The channel and operation bindings the specification declared.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        in MessageContext context,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        return SubscribeWithDeliveryContextAsync(channelUtf8, handler, cancellationToken);
    }

    /// <summary>
    /// Subscribes a responder to a request channel while exposing transport delivery metadata — the
    /// delivery-context counterpart of
    /// <see cref="IMessageTransport.SubscribeReplyAsync{TRequest, TReply}(ReadOnlyMemory{byte}, Func{TRequest, JsonElement, CancellationToken, ValueTask{TReply}}, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For every request delivered on <paramref name="channelUtf8"/> the transport parses the typed
    /// request, invokes <paramref name="handler"/> with the request payload and its delivery context to
    /// obtain the reply payload, and publishes that reply to the request's reply-to address correlated
    /// to the request — exactly as <c>SubscribeReplyAsync</c> does, except the handler also receives the
    /// native delivery metadata (headers, channel, and the transport-specific native message) instead of
    /// just headers.
    /// </para>
    /// <para>
    /// The <see cref="MessageDeliveryContext"/> passed to <paramref name="handler"/> is valid only for
    /// the duration of that invocation, and the reply the handler returns is published before the
    /// handler call completes — the same lifetime rule <c>SubscribeWithDeliveryContextAsync</c> applies
    /// to plain consumers applies here.
    /// </para>
    /// <para>
    /// This is an optional capability layered on an already-optional capability: a transport that
    /// implements <see cref="IMessageDeliveryContextTransport"/> is not required to also support
    /// request/reply responders, and one that supports plain responders via
    /// <c>SubscribeReplyAsync</c> is not required to additionally expose their delivery context. The
    /// default implementation throws <see cref="NotSupportedException"/>, matching
    /// <c>SubscribeReplyAsync</c>'s own default.
    /// </para>
    /// </remarks>
    /// <typeparam name="TRequest">The request payload type the responder parses into.</typeparam>
    /// <typeparam name="TReply">The reply payload type the handler returns.</typeparam>
    /// <param name="channelUtf8">The request channel address as UTF-8 bytes.</param>
    /// <param name="handler">The handler invoked with each request payload and its delivery context, returning the reply payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SubscribeReplyWithDeliveryContextAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> handler,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
        => throw new NotSupportedException(
            "This transport does not support request/reply responders with delivery context (SubscribeReplyWithDeliveryContextAsync).");

    /// <summary>
    /// Subscribes a delivery-context-aware responder to a request channel, with protocol-specific metadata.
    /// </summary>
    /// <remarks>
    /// The counterpart of the <see cref="MessageContext"/> overload of <c>SubscribeReplyAsync</c>: a
    /// delivery-context responder declared with channel or operation bindings is subscribed with them,
    /// exactly as a plain responder is. The default implementation drops the context and forwards, so a
    /// transport that has no use for bindings is unaffected and one that already implements the
    /// delivery-context responder capability keeps working unchanged.
    /// </remarks>
    /// <typeparam name="TRequest">The request payload type the responder parses into.</typeparam>
    /// <typeparam name="TReply">The reply payload type the handler returns.</typeparam>
    /// <param name="channelUtf8">The request channel address as UTF-8 bytes.</param>
    /// <param name="handler">The handler invoked with each request payload and its delivery context, returning the reply payload.</param>
    /// <param name="context">The channel and operation bindings the specification declared.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SubscribeReplyWithDeliveryContextAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TRequest, MessageDeliveryContext, CancellationToken, ValueTask<TReply>> handler,
        in MessageContext context,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        return SubscribeReplyWithDeliveryContextAsync<TRequest, TReply>(channelUtf8, handler, cancellationToken);
    }
}

// End of file.