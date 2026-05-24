// <copyright file="NatsTransportOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.Nats;

/// <summary>
/// Configuration options for the <see cref="NatsMessageTransport"/>.
/// </summary>
public sealed class NatsTransportOptions : ITransportOptions
{
    /// <summary>
    /// Gets or sets the NATS server URL (e.g., "nats://localhost:4222").
    /// </summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// Gets or sets the connection name for diagnostics.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the dead-letter subject suffix.
    /// </summary>
    public string DeadLetterSuffix { get; set; } = ".dead-letter";

    /// <summary>
    /// Gets or sets the reply inbox prefix.
    /// </summary>
    public string InboxPrefix { get; set; } = "_INBOX";

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public IMessageErrorPolicy? ErrorPolicy { get; set; }

    /// <inheritdoc/>
    public MessageHandlerMiddleware? HandlerMiddleware { get; set; }
}