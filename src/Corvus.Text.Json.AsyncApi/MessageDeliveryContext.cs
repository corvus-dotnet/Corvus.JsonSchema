// <copyright file="MessageDeliveryContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Runtime metadata for one delivered message.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="MessageContext"/>, which contains static contract and binding metadata used to configure a
/// subscription, this type describes the message currently being delivered to a handler.
/// </para>
/// <para>
/// Values are valid only while the transport is invoking the handler. In particular, <see cref="Headers"/> and
/// <see cref="NativeMessage"/> must not be retained after the handler returns. <see cref="NativeMessage"/> is an
/// optional transport-specific escape hatch and is <see langword="null"/> when the transport does not expose one.
/// </para>
/// </remarks>
public readonly struct MessageDeliveryContext
{
    /// <summary>
    /// Gets the channel address on which the message was delivered, encoded as UTF-8.
    /// </summary>
    public ReadOnlyMemory<byte> ChannelUtf8 { get; init; }

    /// <summary>
    /// Gets the protocol-neutral message headers, or an undefined value when there are no headers.
    /// </summary>
    public JsonElement Headers { get; init; }

    /// <summary>
    /// Gets the optional underlying transport message object.
    /// </summary>
    public object? NativeMessage { get; init; }
}