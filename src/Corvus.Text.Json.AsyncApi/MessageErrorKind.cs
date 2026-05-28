// <copyright file="MessageErrorKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Identifies the phase of message processing at which an error occurred.
/// </summary>
public enum MessageErrorKind
{
    /// <summary>
    /// The message could not be deserialized into the target type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This typically indicates malformed JSON or a schema mismatch.
    /// The <see cref="MessageErrorContext.Payload"/> will be <c>default</c>
    /// because the payload could not be parsed.
    /// </para>
    /// </remarks>
    Deserialization,

    /// <summary>
    /// The message handler threw an exception after the resilience middleware
    /// exhausted all retry attempts.
    /// </summary>
    Handler,

    /// <summary>
    /// A transport-level connectivity error occurred during consumption
    /// (e.g., connection dropped, broker unavailable).
    /// </summary>
    Transport,
}