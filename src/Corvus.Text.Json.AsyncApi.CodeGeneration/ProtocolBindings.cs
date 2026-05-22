// <copyright file="ProtocolBindings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Protocol-specific binding configuration extracted from an AsyncAPI specification.
/// </summary>
/// <remarks>
/// <para>
/// AsyncAPI defines bindings at four levels: server, channel, operation, and message.
/// Each level can contain protocol-specific properties (e.g., Kafka consumer group,
/// AMQP routing key, MQTT QoS). This type aggregates bindings discovered during
/// code generation.
/// </para>
/// </remarks>
public sealed class ProtocolBindings
{
    /// <summary>
    /// Gets or sets the Kafka-specific bindings, if present.
    /// </summary>
    public KafkaBindings? Kafka { get; set; }

    /// <summary>
    /// Gets or sets the AMQP-specific bindings, if present.
    /// </summary>
    public AmqpBindings? Amqp { get; set; }

    /// <summary>
    /// Gets or sets the MQTT-specific bindings, if present.
    /// </summary>
    public MqttBindings? Mqtt { get; set; }
}

/// <summary>
/// Kafka protocol binding configuration.
/// </summary>
public sealed class KafkaBindings
{
    /// <summary>
    /// Gets or sets the consumer group ID (channel binding).
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the partition key schema pointer (message binding).
    /// </summary>
    public string? PartitionKeySchemaPointer { get; set; }

    /// <summary>
    /// Gets or sets the client ID (server binding).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the topic configuration (channel binding).
    /// </summary>
    public KafkaTopicConfig? TopicConfig { get; set; }
}

/// <summary>
/// Kafka topic configuration from channel bindings.
/// </summary>
public sealed class KafkaTopicConfig
{
    /// <summary>
    /// Gets or sets the number of partitions.
    /// </summary>
    public int? Partitions { get; set; }

    /// <summary>
    /// Gets or sets the replication factor.
    /// </summary>
    public int? Replicas { get; set; }

    /// <summary>
    /// Gets or sets the retention period in milliseconds.
    /// </summary>
    public long? RetentionMs { get; set; }

    /// <summary>
    /// Gets or sets the cleanup policy (e.g., "delete", "compact").
    /// </summary>
    public string? CleanupPolicy { get; set; }
}

/// <summary>
/// AMQP protocol binding configuration.
/// </summary>
public sealed class AmqpBindings
{
    /// <summary>
    /// Gets or sets the exchange name (channel binding).
    /// </summary>
    public string? Exchange { get; set; }

    /// <summary>
    /// Gets or sets the exchange type (e.g., "topic", "direct", "fanout", "headers").
    /// </summary>
    public string? ExchangeType { get; set; }

    /// <summary>
    /// Gets or sets the queue name (channel binding).
    /// </summary>
    public string? Queue { get; set; }

    /// <summary>
    /// Gets or sets the routing key (operation binding).
    /// </summary>
    public string? RoutingKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue is durable.
    /// </summary>
    public bool? Durable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue auto-deletes.
    /// </summary>
    public bool? AutoDelete { get; set; }

    /// <summary>
    /// Gets or sets the acknowledgment mode (operation binding).
    /// </summary>
    public int? Ack { get; set; }
}

/// <summary>
/// MQTT protocol binding configuration.
/// </summary>
public sealed class MqttBindings
{
    /// <summary>
    /// Gets or sets the Quality of Service level (0, 1, or 2).
    /// </summary>
    public int? Qos { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message should be retained.
    /// </summary>
    public bool? Retain { get; set; }

    /// <summary>
    /// Gets or sets the last will topic (server binding).
    /// </summary>
    public string? LastWillTopic { get; set; }

    /// <summary>
    /// Gets or sets the last will message (server binding).
    /// </summary>
    public string? LastWillMessage { get; set; }

    /// <summary>
    /// Gets or sets the last will QoS (server binding).
    /// </summary>
    public int? LastWillQos { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the last will should be retained (server binding).
    /// </summary>
    public bool? LastWillRetain { get; set; }

    /// <summary>
    /// Gets or sets the keep-alive interval in seconds (server binding).
    /// </summary>
    public int? KeepAlive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether clean session is enabled (server binding).
    /// </summary>
    public bool? CleanSession { get; set; }
}