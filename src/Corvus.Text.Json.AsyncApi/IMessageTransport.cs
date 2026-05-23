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
/// </remarks>
public interface IMessageTransport : IAsyncDisposable
{
    /// <summary>
    /// Publishes a typed message payload to the specified channel.
    /// </summary>
    /// <typeparam name="TPayload">The payload type. Must implement
    /// <see cref="IJsonElement{TPayload}"/> so the transport can serialize it
    /// directly via <c>WriteTo(Utf8JsonWriter)</c>.</typeparam>
    /// <param name="channel">The channel address.</param>
    /// <param name="payload">The message payload, passed by <c>in</c> reference.
    /// The transport must consume the payload synchronously before any async I/O.</param>
    /// <param name="headers">Optional message headers. When not <c>default</c>,
    /// the transport should deliver these alongside the payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask PublishAsync<TPayload>(
        string channel,
        in TPayload payload,
        in JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>;

    /// <summary>
    /// Sends a request message and waits for a correlated reply.
    /// </summary>
    /// <typeparam name="TRequest">The request payload type.</typeparam>
    /// <typeparam name="TReply">The expected reply payload type.</typeparam>
    /// <param name="requestChannel">The channel to send the request on.</param>
    /// <param name="replyChannel">The channel to listen for the reply on.</param>
    /// <param name="request">The request payload.</param>
    /// <param name="correlationId">A correlation identifier linking request to reply.</param>
    /// <param name="headers">Optional message headers.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The reply payload and headers.</returns>
    ValueTask<(TReply Payload, JsonElement Headers)> RequestAsync<TRequest, TReply>(
        string requestChannel,
        string replyChannel,
        in TRequest request,
        string correlationId,
        in JsonElement headers = default,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IJsonElement<TRequest>
        where TReply : struct, IJsonElement<TReply>;

    /// <summary>
    /// Subscribes to messages on the specified channel, delivering typed payloads.
    /// </summary>
    /// <typeparam name="TPayload">The payload type. The transport parses incoming
    /// bytes into this type before invoking the handler.</typeparam>
    /// <param name="channel">The channel address.</param>
    /// <param name="handler">The message handler delegate receiving typed payloads
    /// and optional headers.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SubscribeAsync<TPayload>(
        string channel,
        Func<TPayload, JsonElement, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>;

    /// <summary>
    /// Unsubscribes from messages on the specified channel.
    /// </summary>
    /// <param name="channel">The channel address.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask UnsubscribeAsync(
        string channel,
        CancellationToken cancellationToken = default);
}