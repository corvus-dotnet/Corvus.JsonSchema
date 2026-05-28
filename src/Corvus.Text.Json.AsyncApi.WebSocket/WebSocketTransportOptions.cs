// <copyright file="WebSocketTransportOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.WebSocket;

/// <summary>
/// Configuration options for the <see cref="WebSocketMessageTransport"/>.
/// </summary>
public sealed class WebSocketTransportOptions : ITransportOptions
{
    /// <summary>
    /// Gets or sets the WebSocket server URI (e.g., "ws://localhost:8080/ws").
    /// </summary>
    public string ServerUri { get; set; } = "ws://localhost:8080/ws";

    /// <summary>
    /// Gets or sets the receive buffer size in bytes.
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    /// Gets or sets the reconnect delay when the connection drops.
    /// </summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum number of reconnect attempts before giving up.
    /// Zero means unlimited.
    /// </summary>
    public int MaxReconnectAttempts { get; set; }

    /// <summary>
    /// Gets or sets the dead-letter channel suffix.
    /// </summary>
    public string DeadLetterSuffix { get; set; } = "/dead-letter";

    /// <inheritdoc/>
    public IMessageErrorPolicy? ErrorPolicy { get; set; }

    /// <inheritdoc/>
    public MessageHandlerMiddleware? HandlerMiddleware { get; set; }

    /// <inheritdoc/>
    public ProcessingLoopHeartbeat? Heartbeat { get; set; }
}