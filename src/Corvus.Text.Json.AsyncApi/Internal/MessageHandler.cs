// <copyright file="MessageHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi.Internal;

/// <summary>
/// Stores one subscription's legacy or metadata-aware callback without an
/// adapter delegate on the delivery path.
/// </summary>
/// <typeparam name="TPayload">The deserialized message payload type.</typeparam>
/// <remarks>
/// This is infrastructure for <see cref="IMessageTransport"/> implementations.
/// Application code should not use it directly.
/// </remarks>
public readonly struct MessageHandler<TPayload>
    where TPayload : struct, IJsonElement<TPayload>
{
    private readonly Func<TPayload, Corvus.Text.Json.JsonElement, CancellationToken, ValueTask>? legacyHandler;
    private readonly Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask>? contextHandler;

    private MessageHandler(
        Func<TPayload, Corvus.Text.Json.JsonElement, CancellationToken, ValueTask>? legacyHandler,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask>? contextHandler)
    {
        this.legacyHandler = legacyHandler;
        this.contextHandler = contextHandler;
    }

    /// <summary>
    /// Gets a value indicating whether the stored callback consumes a
    /// <see cref="MessageDeliveryContext"/> (and therefore needs the transport's
    /// native message on delivery).
    /// </summary>
    public bool UsesDeliveryContext => this.contextHandler is not null;

    /// <summary>
    /// Creates a handler wrapping a legacy (payload and headers) callback.
    /// </summary>
    /// <param name="handler">The legacy callback.</param>
    /// <returns>The wrapping handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public static MessageHandler<TPayload> WithoutDeliveryContext(
        Func<TPayload, Corvus.Text.Json.JsonElement, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new(handler, null);
    }

    /// <summary>
    /// Creates a handler wrapping a delivery-context-aware callback.
    /// </summary>
    /// <param name="handler">The delivery-context-aware callback.</param>
    /// <returns>The wrapping handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public static MessageHandler<TPayload> WithDeliveryContext(
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new(null, handler);
    }

    /// <summary>
    /// Invokes the stored callback for one delivered message.
    /// </summary>
    /// <param name="payload">The deserialized message payload.</param>
    /// <param name="channelUtf8">The channel the message arrived on, as UTF-8 bytes.</param>
    /// <param name="headers">The message headers.</param>
    /// <param name="nativeMessage">The transport's native message representation, or
    /// <see langword="null"/> when the callback does not consume delivery context.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the callback has run.</returns>
    /// <exception cref="InvalidOperationException">This instance is <see langword="default"/> and
    /// holds no callback. Create instances with <see cref="WithoutDeliveryContext"/> or
    /// <see cref="WithDeliveryContext"/>.</exception>
    public ValueTask Invoke(
        TPayload payload,
        ReadOnlyMemory<byte> channelUtf8,
        Corvus.Text.Json.JsonElement headers,
        object? nativeMessage,
        CancellationToken cancellationToken)
    {
        if (this.contextHandler is null)
        {
            if (this.legacyHandler is null)
            {
                throw new InvalidOperationException(
                    "This MessageHandler holds no callback because it was default-constructed. Create instances with WithoutDeliveryContext or WithDeliveryContext.");
            }

            return this.legacyHandler(payload, headers, cancellationToken);
        }

        return this.contextHandler(payload, new MessageDeliveryContext
        {
            ChannelUtf8 = channelUtf8,
            Headers = headers,
            NativeMessage = nativeMessage,
        }, cancellationToken);
    }
}

// End of file.