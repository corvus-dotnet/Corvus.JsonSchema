// <copyright file="IMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Abstraction for message transport (publish/subscribe).
/// </summary>
/// <remarks>
/// <para>
/// Implementations include in-memory (for testing), Kafka, AMQP, etc.
/// Generated producers and consumers depend on this interface.
/// </para>
/// <para>
/// The transport is a low-level messaging pipe. All AsyncAPI semantics
/// (channel addressing, message naming, payload serialization) are
/// handled by the generated producer/consumer types. The transport only
/// needs to:
/// </para>
/// <list type="number">
/// <item><description>Serialize the typed payload via
/// <c>WriteTo(Utf8JsonWriter)</c> into its output buffer.</description></item>
/// <item><description>Deliver bytes to the broker or in-memory channel.</description></item>
/// <item><description>Parse incoming bytes into typed payloads via
/// <c>ParsedJsonDocument&lt;T&gt;.Parse()</c> for consumers.</description></item>
/// </list>
/// <para>
/// Channel addresses are passed as <see cref="ReadOnlyMemory{T}"/> of UTF-8 bytes.
/// Transport implementations that need a <see langword="string"/> for their broker API
/// should convert at the outermost boundary via <c>Encoding.UTF8.GetString()</c>.
/// This keeps the entire hot path in the UTF-8 domain with zero intermediate string
/// allocations.
/// </para>
/// </remarks>
public interface IMessageTransport : IAsyncDisposable
{
    /// <summary>
    /// Publishes a typed message payload to the specified channel.
    /// </summary>
    /// <typeparam name="TPayload">The payload type. Must implement
    /// <see cref="IJsonElement{TPayload}"/> so the transport can serialize it
    /// directly via <c>WriteTo(Utf8JsonWriter)</c>.</typeparam>
    /// <param name="channelUtf8">The channel address as UTF-8 bytes. The memory
    /// remains valid until this method completes.</param>
    /// <param name="payload">The message payload, passed by <c>in</c> reference.
    /// The transport must consume the payload synchronously before any async I/O.</param>
    /// <param name="headers">Optional message headers. When not <c>default</c>,
    /// the transport should deliver these alongside the payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask PublishAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        in TPayload payload,
        in JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>;

    /// <summary>
    /// Publishes a typed message payload to the specified channel with protocol-specific metadata.
    /// </summary>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <param name="channelUtf8">The channel address as UTF-8 bytes.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="context">The message context containing bindings and content type.</param>
    /// <param name="headers">Optional message headers.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask PublishAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        in TPayload payload,
        in MessageContext context,
        in JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        return PublishAsync(channelUtf8, in payload, in headers, cancellationToken);
    }

    /// <summary>
    /// Sends a request message and waits for a correlated reply.
    /// </summary>
    /// <typeparam name="TRequest">The request payload type.</typeparam>
    /// <typeparam name="TReply">The expected reply payload type.</typeparam>
    /// <param name="requestChannelUtf8">The channel to send the request on, as UTF-8 bytes.</param>
    /// <param name="replyChannelUtf8">The channel to listen for the reply on, as UTF-8 bytes.</param>
    /// <param name="request">The request payload.</param>
    /// <param name="correlationIdUtf8">A correlation identifier linking request to reply, as UTF-8 bytes.
    /// The memory must remain valid until this method completes. For GUIDs, use
    /// <c>Guid.TryFormat(Span&lt;byte&gt;, out _, "D")</c> to format directly to a <c>byte[36]</c>
    /// without allocating an intermediate string.</param>
    /// <param name="headers">Optional message headers.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reply payload and headers.</returns>
    ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>;

    /// <summary>
    /// Sends a request message and waits for a correlated reply, with protocol-specific metadata.
    /// </summary>
    /// <typeparam name="TRequest">The request payload type.</typeparam>
    /// <typeparam name="TReply">The expected reply payload type.</typeparam>
    /// <param name="requestChannelUtf8">The channel to send the request on, as UTF-8 bytes.</param>
    /// <param name="replyChannelUtf8">The channel to listen for the reply on, as UTF-8 bytes.</param>
    /// <param name="request">The request payload.</param>
    /// <param name="correlationIdUtf8">A correlation identifier linking request to reply, as UTF-8 bytes.</param>
    /// <param name="context">The message context containing bindings and content type.</param>
    /// <param name="headers">Optional message headers.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reply payload and headers.</returns>
    ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
        ReadOnlyMemory<byte> requestChannelUtf8,
        ReadOnlyMemory<byte> replyChannelUtf8,
        TRequest request,
        ReadOnlyMemory<byte> correlationIdUtf8,
        in MessageContext context,
        JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>
    {
        return RequestAsync<TRequest, TReply>(requestChannelUtf8, replyChannelUtf8, request, correlationIdUtf8, headers, cancellationToken);
    }

    /// <summary>
    /// Subscribes to messages on the specified channel, delivering typed payloads.
    /// </summary>
    /// <typeparam name="TPayload">The payload type. The transport parses incoming
    /// bytes into this type before invoking the handler.</typeparam>
    /// <param name="channelUtf8">The channel address as UTF-8 bytes.</param>
    /// <param name="handler">The message handler delegate receiving typed payloads
    /// and optional headers.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>;

    /// <summary>
    /// Subscribes to messages on the specified channel with protocol-specific metadata.
    /// </summary>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <param name="channelUtf8">The channel address as UTF-8 bytes.</param>
    /// <param name="handler">The message handler delegate.</param>
    /// <param name="context">The message context containing bindings and content type.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SubscribeAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        in MessageContext context,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>
    {
        return SubscribeAsync(channelUtf8, handler, cancellationToken);
    }

    /// <summary>
    /// Unsubscribes from messages on the specified channel.
    /// </summary>
    /// <param name="channelUtf8">The channel address as UTF-8 bytes.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask UnsubscribeAsync(
        ReadOnlyMemory<byte> channelUtf8,
        CancellationToken cancellationToken = default);
}