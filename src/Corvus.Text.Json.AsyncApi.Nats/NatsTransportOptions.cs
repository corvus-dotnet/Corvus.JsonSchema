// <copyright file="NatsTransportOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.Nats;

/// <summary>
/// JetStream message delivery policy.
/// </summary>
public enum DeliverPolicy
{
    /// <summary>
    /// Deliver all available messages from the stream.
    /// </summary>
    All,

    /// <summary>
    /// Deliver only new messages published after subscription.
    /// </summary>
    New,

    /// <summary>
    /// Deliver messages from a specific sequence number.
    /// </summary>
    ByStartSequence,

    /// <summary>
    /// Deliver messages from a specific start time.
    /// </summary>
    ByStartTime,

    /// <summary>
    /// Deliver only the last message.
    /// </summary>
    Last,

    /// <summary>
    /// Deliver the last message for each subject.
    /// </summary>
    LastPerSubject,
}

/// <summary>
/// JetStream stream storage type.
/// </summary>
public enum StorageType
{
    /// <summary>
    /// Store messages in memory (faster, not durable across restarts).
    /// </summary>
    Memory,

    /// <summary>
    /// Store messages on disk (durable across restarts).
    /// </summary>
    File,
}

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

    /// <summary>
    /// Gets or sets a value indicating whether to use NATS JetStream for persistent messaging.
    /// When false (default), uses core NATS (no persistence).
    /// When true, uses JetStream streams and durable consumers.
    /// </summary>
    public bool UseJetStream { get; set; }

    /// <summary>
    /// Gets or sets the JetStream stream name.
    /// If null, stream name is derived from the subject name.
    /// </summary>
    public string? StreamName { get; set; }

    /// <summary>
    /// Gets or sets the durable consumer name.
    /// Multiple consumers with the same name share message processing (competing consumers).
    /// </summary>
    public string ConsumerName { get; set; } = "default-consumer";

    /// <summary>
    /// Gets or sets the message delivery policy.
    /// </summary>
    public DeliverPolicy DeliverPolicy { get; set; } = DeliverPolicy.All;

    /// <summary>
    /// Gets or sets the acknowledgement wait timeout.
    /// If a message is not acknowledged within this time, it will be redelivered.
    /// </summary>
    public TimeSpan AckWait { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before dead-lettering.
    /// </summary>
    public int MaxDeliver { get; set; } = 5;

    /// <summary>
    /// Gets or sets the stream storage type.
    /// </summary>
    public StorageType StorageType { get; set; } = StorageType.File;

    /// <inheritdoc/>
    public IMessageErrorPolicy? ErrorPolicy { get; set; }

    /// <inheritdoc/>
    public MessageHandlerMiddleware? HandlerMiddleware { get; set; }

    /// <inheritdoc/>
    public ProcessingLoopHeartbeat? Heartbeat { get; set; }
}