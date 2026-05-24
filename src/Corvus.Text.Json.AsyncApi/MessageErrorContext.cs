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
/// policy can make decisions based on the channel, error kind, and any payload/header
/// data available at the time of the error.
/// </para>
/// </remarks>
public readonly struct MessageErrorContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageErrorContext"/> struct.
    /// </summary>
    /// <param name="channel">The channel address on which the error occurred.</param>
    /// <param name="errorKind">The kind of error that occurred.</param>
    /// <param name="payload">The raw payload element, if available.</param>
    /// <param name="headers">The raw headers element, if available.</param>
    public MessageErrorContext(ReadOnlyMemory<byte> channel, MessageErrorKind errorKind, JsonElement payload = default, JsonElement headers = default)
    {
        this.Channel = channel;
        this.ErrorKind = errorKind;
        this.Payload = payload;
        this.Headers = headers;
    }

    /// <summary>
    /// Gets the channel address (UTF-8) on which the error occurred.
    /// </summary>
    public ReadOnlyMemory<byte> Channel { get; }

    /// <summary>
    /// Gets the kind of error that occurred.
    /// </summary>
    public MessageErrorKind ErrorKind { get; }

    /// <summary>
    /// Gets the raw payload element, if available.
    /// </summary>
    /// <remarks>
    /// This may be <c>default</c> (undefined) if the error occurred before parsing
    /// (e.g., a <see cref="MessageErrorKind.Deserialization"/> failure).
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