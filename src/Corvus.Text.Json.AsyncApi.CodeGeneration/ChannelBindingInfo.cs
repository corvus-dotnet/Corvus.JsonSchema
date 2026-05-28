// <copyright file="ChannelBindingInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.AsyncApi30;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Aggregated binding information for a channel and its associated operations.
/// </summary>
/// <remarks>
/// <para>
/// This type holds strongly-typed binding objects extracted from the AsyncAPI
/// document. Use the protocol-specific properties on each bindings object
/// (e.g., <c>ChannelBindings.Kafka</c>, <c>OperationBindings.Amqp</c>) to
/// access binding configuration.
/// </para>
/// </remarks>
public readonly struct ChannelBindingInfo
{
    /// <summary>
    /// Gets the channel-level bindings object, or <c>default</c> if no channel bindings are present.
    /// </summary>
    public AsyncApiDocument.ChannelBindingsObject ChannelBindings { get; init; }

    /// <summary>
    /// Gets the operation-level bindings object, or <c>default</c> if no operation bindings are present.
    /// </summary>
    public AsyncApiDocument.OperationBindingsObject OperationBindings { get; init; }

    /// <summary>
    /// Gets the message-level bindings for the first message on the channel,
    /// or <c>default</c> if no message bindings are present.
    /// </summary>
    public AsyncApiDocument.MessageBindingsObject MessageBindings { get; init; }

    /// <summary>
    /// Gets a value indicating whether any bindings are present at any level.
    /// </summary>
    public bool HasBindings =>
        ChannelBindings.IsNotUndefined() ||
        OperationBindings.IsNotUndefined() ||
        MessageBindings.IsNotUndefined();
}