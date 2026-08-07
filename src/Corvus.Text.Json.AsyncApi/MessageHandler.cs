// <copyright file="MessageHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Stores one subscription's legacy or metadata-aware callback without an
/// adapter delegate on the delivery path.
/// </summary>
internal readonly struct MessageHandler<TPayload>
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

    public bool UsesDeliveryContext => this.contextHandler is not null;

    public static MessageHandler<TPayload> Legacy(
        Func<TPayload, Corvus.Text.Json.JsonElement, CancellationToken, ValueTask> handler)
        => new(handler, null);

    public static MessageHandler<TPayload> WithDeliveryContext(
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler)
        => new(null, handler);

    public ValueTask Invoke(
        TPayload payload,
        ReadOnlyMemory<byte> channelUtf8,
        Corvus.Text.Json.JsonElement headers,
        object? nativeMessage,
        CancellationToken cancellationToken)
    {
        if (this.contextHandler is null)
        {
            return this.legacyHandler!(payload, headers, cancellationToken);
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
