// <copyright file="KafkaTransportOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Confluent.Kafka;

namespace Corvus.Text.Json.AsyncApi.Kafka;

/// <summary>
/// Configuration options for the <see cref="KafkaMessageTransport"/>.
/// </summary>
public sealed class KafkaTransportOptions : ITransportOptions
{
    /// <summary>
    /// Gets or sets the Kafka bootstrap servers (e.g., "localhost:9092").
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Gets or sets the consumer group ID for subscriptions.
    /// </summary>
    public string GroupId { get; set; } = "corvus-asyncapi";

    /// <summary>
    /// Gets or sets the auto-offset reset behavior for new consumer groups.
    /// </summary>
    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Earliest;

    /// <summary>
    /// Gets or sets the dead-letter topic suffix. When a message fails processing,
    /// it is published to <c>{originalTopic}{DeadLetterSuffix}</c>.
    /// </summary>
    public string DeadLetterSuffix { get; set; } = ".dead-letter";

    /// <summary>
    /// Gets or sets the message delivery timeout in milliseconds.
    /// </summary>
    public int MessageTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the consumer poll timeout in milliseconds.
    /// </summary>
    public int PollTimeoutMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets additional producer configuration.
    /// </summary>
    public ProducerConfig? ProducerConfig { get; set; }

    /// <summary>
    /// Gets or sets additional consumer configuration.
    /// </summary>
    public ConsumerConfig? ConsumerConfig { get; set; }

    /// <inheritdoc/>
    public IMessageErrorPolicy? ErrorPolicy { get; set; }

    /// <inheritdoc/>
    public MessageHandlerMiddleware? HandlerMiddleware { get; set; }
}