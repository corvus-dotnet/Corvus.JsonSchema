// <copyright file="KafkaCommitStrategy.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.Kafka;

/// <summary>
/// Controls how the Kafka transport acknowledges successfully handled messages.
/// Both strategies preserve at-least-once delivery; they differ only in how wide the
/// redelivery window is after a crash.
/// </summary>
public enum KafkaCommitStrategy
{
    /// <summary>
    /// The offset is stored in memory after each successfully handled message and flushed to
    /// the broker by the client's auto-commit interval and on close. After a crash, up to one
    /// auto-commit interval (five seconds by default) of already-handled messages redelivers.
    /// </summary>
    Windowed,

    /// <summary>
    /// Every handled message commits its offset to the broker synchronously before the next
    /// message is consumed. The narrowest redelivery window, at the cost of a broker round
    /// trip per message.
    /// </summary>
    PerMessage,
}