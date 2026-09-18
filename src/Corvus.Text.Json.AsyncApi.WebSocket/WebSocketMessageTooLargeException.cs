// <copyright file="WebSocketMessageTooLargeException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.WebSocket;

/// <summary>
/// Thrown to a request awaiting its reply when the transport received a message larger than
/// <see cref="WebSocketTransportOptions.MaxMessageSize"/> and closed the connection over it.
/// </summary>
public sealed class WebSocketMessageTooLargeException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="WebSocketMessageTooLargeException"/> class.</summary>
    /// <param name="maxMessageSize">The limit the message exceeded, in bytes.</param>
    public WebSocketMessageTooLargeException(long maxMessageSize)
        : base($"The WebSocket peer sent a message larger than the {maxMessageSize} byte limit, and the connection was closed.")
    {
        this.MaxMessageSize = maxMessageSize;
    }

    /// <summary>Gets the limit the message exceeded, in bytes.</summary>
    public long MaxMessageSize { get; }
}