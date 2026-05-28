// <copyright file="MessageErrorAction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Specifies the action to take when an error occurs during message processing
/// after the resilience middleware has exhausted all retry attempts.
/// </summary>
public enum MessageErrorAction
{
    /// <summary>
    /// Skip the current message and continue consuming.
    /// </summary>
    Skip,

    /// <summary>
    /// Send the message to a dead-letter channel and continue consuming.
    /// </summary>
    DeadLetter,

    /// <summary>
    /// Abort the consumer (unsubscribe and stop processing).
    /// </summary>
    Abort,
}