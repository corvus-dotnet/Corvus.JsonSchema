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
    /// Gets or sets the largest message, in bytes, the transport will receive, or <see langword="null"/> for no
    /// limit, which is the default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A WebSocket message is a run of frames ending in one marked final, and the transport buffers the whole message
    /// before dispatching it. With no limit, a peer that never sends the final frame grows that buffer until the
    /// process runs out of memory. The other transports sit behind a broker that bounds a message itself; a WebSocket
    /// peer is whoever answered the connection, so a client of a peer it does not control should set this.
    /// </para>
    /// <para>
    /// The limit is on the message as received (the envelope: channel, headers and payload together), counted across
    /// its frames. A message that would exceed it is not read to its end. The transport closes the connection with
    /// <see cref="System.Net.WebSockets.WebSocketCloseStatus.MessageTooBig"/> and fails every request awaiting a reply
    /// with <see cref="WebSocketMessageTooLargeException"/>, since the connection that would have carried the reply
    /// is gone.
    /// </para>
    /// </remarks>
    public long? MaxMessageSize { get; set; }

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