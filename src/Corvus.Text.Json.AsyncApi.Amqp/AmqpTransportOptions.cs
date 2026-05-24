// <copyright file="AmqpTransportOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.Amqp;

/// <summary>
/// Configuration options for the <see cref="AmqpMessageTransport"/>.
/// </summary>
public sealed class AmqpTransportOptions : ITransportOptions
{
    /// <summary>
    /// Gets or sets the AMQP connection URI (e.g., "amqp://guest:guest@localhost:5672/").
    /// </summary>
    public string ConnectionUri { get; set; } = "amqp://guest:guest@localhost:5672/";

    /// <summary>
    /// Gets or sets the exchange name to publish messages to.
    /// When empty, uses the default direct exchange.
    /// </summary>
    public string ExchangeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exchange type (e.g., "direct", "topic", "fanout", "headers").
    /// </summary>
    public string ExchangeType { get; set; } = "topic";

    /// <summary>
    /// Gets or sets a value indicating whether the exchange should be declared as durable.
    /// </summary>
    public bool ExchangeDurable { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether queues should be declared as durable.
    /// </summary>
    public bool QueueDurable { get; set; } = true;

    /// <summary>
    /// Gets or sets the consumer prefetch count (quality of service).
    /// </summary>
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>
    /// Gets or sets the dead-letter exchange name.
    /// </summary>
    public string DeadLetterExchange { get; set; } = "corvus.dead-letter";

    /// <summary>
    /// Gets or sets the dead-letter routing key suffix.
    /// </summary>
    public string DeadLetterRoutingKeySuffix { get; set; } = ".dead-letter";

    /// <summary>
    /// Gets or sets the consumer tag prefix for identifying consumers.
    /// </summary>
    public string ConsumerTagPrefix { get; set; } = "corvus-asyncapi";

    /// <inheritdoc/>
    public IMessageErrorPolicy? ErrorPolicy { get; set; }

    /// <inheritdoc/>
    public MessageHandlerMiddleware? HandlerMiddleware { get; set; }
}