// <copyright file="MessageContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Carries protocol-specific metadata from a generated producer/consumer to the transport.
/// </summary>
/// <remarks>
/// <para>
/// This struct bundles channel, operation, and message bindings as raw JSON bytes.
/// Transport implementations (e.g., Kafka, AMQP) inspect the relevant bindings
/// to configure delivery semantics such as partition keys, exchanges, QoS levels, etc.
/// </para>
/// <para>
/// Generated producers construct a <see cref="MessageContext"/> from static constants
/// and pass it to the transport on every publish or subscribe call.
/// </para>
/// </remarks>
public readonly struct MessageContext
{
    /// <summary>
    /// Gets the content type for the message (e.g., <c>application/json</c>).
    /// </summary>
    /// <remarks>
    /// When <c>null</c>, the default content type from the AsyncAPI specification applies.
    /// </remarks>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the channel-level bindings as raw UTF-8 JSON bytes, or empty if no channel bindings are present.
    /// </summary>
    public ReadOnlyMemory<byte> ChannelBindingsJson { get; init; }

    /// <summary>
    /// Gets the operation-level bindings as raw UTF-8 JSON bytes, or empty if no operation bindings are present.
    /// </summary>
    public ReadOnlyMemory<byte> OperationBindingsJson { get; init; }

    /// <summary>
    /// Gets the message-level bindings as raw UTF-8 JSON bytes, or empty if no message bindings are present.
    /// </summary>
    public ReadOnlyMemory<byte> MessageBindingsJson { get; init; }

    /// <summary>
    /// Gets a value indicating whether any bindings are present at any level.
    /// </summary>
    public bool HasBindings =>
        ChannelBindingsJson.Length > 0 ||
        OperationBindingsJson.Length > 0 ||
        MessageBindingsJson.Length > 0;
}