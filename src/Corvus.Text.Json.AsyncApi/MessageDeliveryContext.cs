// <copyright file="MessageDeliveryContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Carries transport metadata for a delivered message.
/// </summary>
public readonly struct MessageDeliveryContext
{
    /// <summary>Gets the subscribed channel as UTF-8 bytes.</summary>
    public ReadOnlyMemory<byte> ChannelUtf8 { get; init; }

    /// <summary>Gets the message headers.</summary>
    public Corvus.Text.Json.JsonElement Headers { get; init; }

    /// <summary>Gets the transport-native message, when available.</summary>
    public object? NativeMessage { get; init; }
}

// End of file.