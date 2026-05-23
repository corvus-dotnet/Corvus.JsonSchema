// <copyright file="MessageErrorContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Provides context about an error that occurred during message processing.
/// </summary>
/// <remarks>
/// <para>
/// This is passed to <see cref="IMessageErrorPolicy.HandleErrorAsync"/> so that the
/// policy can make decisions based on the channel, the number of delivery attempts,
/// and any payload/header data available at the time of the error.
/// </para>
/// </remarks>
public readonly struct MessageErrorContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageErrorContext"/> struct.
    /// </summary>
    /// <param name="channel">The channel address on which the error occurred.</param>
    /// <param name="attemptNumber">The 1-based attempt number for this message delivery.</param>
    /// <param name="payload">The raw payload element, if available.</param>
    /// <param name="headers">The raw headers element, if available.</param>
    public MessageErrorContext(string channel, int attemptNumber, JsonElement payload = default, JsonElement headers = default)
    {
        this.Channel = channel;
        this.AttemptNumber = attemptNumber;
        this.Payload = payload;
        this.Headers = headers;
    }

    /// <summary>
    /// Gets the channel address on which the error occurred.
    /// </summary>
    public string Channel { get; }

    /// <summary>
    /// Gets the 1-based attempt number for this message delivery.
    /// </summary>
    /// <remarks>
    /// On the first delivery attempt this is 1. After a <see cref="MessageErrorAction.Retry"/>,
    /// this increments for each subsequent attempt.
    /// </remarks>
    public int AttemptNumber { get; }

    /// <summary>
    /// Gets the raw payload element, if available.
    /// </summary>
    /// <remarks>
    /// This may be <c>default</c> (undefined) if the error occurred before parsing.
    /// </remarks>
    public JsonElement Payload { get; }

    /// <summary>
    /// Gets the raw headers element, if available.
    /// </summary>
    /// <remarks>
    /// This may be <c>default</c> (undefined) if no headers were present.
    /// </remarks>
    public JsonElement Headers { get; }
}