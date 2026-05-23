// <copyright file="MessageErrorAction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Specifies the action to take when an error occurs during message processing.
/// </summary>
public enum MessageErrorAction
{
    /// <summary>
    /// Skip the current message and continue consuming.
    /// </summary>
    Skip,

    /// <summary>
    /// Abort the consumer (unsubscribe and stop processing).
    /// </summary>
    Abort,

    /// <summary>
    /// Retry processing the current message.
    /// </summary>
    Retry,

    /// <summary>
    /// Send the message to a dead-letter channel and continue consuming.
    /// </summary>
    DeadLetter,
}