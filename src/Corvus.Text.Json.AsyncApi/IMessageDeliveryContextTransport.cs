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
    /// The <see cref="MessageDeliveryContext"/> passed to <paramref name="handler"/> is valid
    /// only for the duration of that invocation; transports may recycle the buffers it
    /// references once the handler returns.
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
}

// End of file.