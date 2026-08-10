// <copyright file="MessageDeliveryContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Carries transport metadata for a delivered message.
/// </summary>
/// <remarks>
/// <para>
/// The context and everything it exposes are valid only for the duration of the handler
/// invocation it is passed to. Transports hand out memory that they recycle once the
/// handler returns (for example, RabbitMQ reuses the network-frame buffer backing
/// <c>BasicDeliverEventArgs.Body</c>), so a stored context silently reads reused
/// buffers. Copy anything you need to keep before the handler returns.
/// </para>
/// </remarks>
public readonly struct MessageDeliveryContext
{
    /// <summary>
    /// Gets the subscribed channel as UTF-8 bytes.
    /// </summary>
    /// <remarks>
    /// On broker transports this views the memory the subscriber passed to the subscribe call,
    /// which is why that buffer must remain valid and unmodified for the subscription's lifetime.
    /// </remarks>
    public ReadOnlyMemory<byte> ChannelUtf8 { get; init; }

    /// <summary>Gets the message headers.</summary>
    public Corvus.Text.Json.JsonElement Headers { get; init; }

    /// <summary>
    /// Gets the transport-native message, when available.
    /// </summary>
    /// <remarks>
    /// Valid only for the duration of the handler invocation; the transport may recycle
    /// the buffers it references once the handler returns. Do not store it.
    /// </remarks>
    public object? NativeMessage { get; init; }
}

// End of file.