// <copyright file="AsyncApiChannelDescriptor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Describes a generated AsyncAPI channel operation for a consumer of the code generator (for example,
/// the Arazzo workflow generator binding a channel step): the channel address, the operation action,
/// the generated producer class (for send operations), and the operation's messages with their resolved
/// .NET payload type names and producer publish methods.
/// </summary>
/// <param name="ChannelAddress">The channel address (with any <c>{parameter}</c> templates intact).</param>
/// <param name="Action">Whether the operation sends or receives on the channel.</param>
/// <param name="OperationName">The AsyncAPI operation name.</param>
/// <param name="ProducerClassName">The fully-qualified generated producer class for a send operation, or <see langword="null"/> for a receive operation.</param>
/// <param name="IsDynamicAddress">Whether the channel address is supplied at call time (no fixed address) rather than templated/static.</param>
/// <param name="ChannelParameters">The channel address parameter names (each becomes an argument of the producer publish method).</param>
/// <param name="Messages">The operation's messages.</param>
/// <param name="ReplyPayloadTypeName">For a send operation that declares a <c>reply</c> (request/reply), the fully-qualified generated .NET reply payload type; <see langword="null"/> for fire-and-forget send and for receive operations.</param>
public readonly record struct AsyncApiChannelDescriptor(
    string ChannelAddress,
    OperationAction Action,
    string OperationName,
    string? ProducerClassName,
    bool IsDynamicAddress,
    IReadOnlyList<string> ChannelParameters,
    IReadOnlyList<AsyncApiChannelMessageDescriptor> Messages,
    string? ReplyPayloadTypeName = null);

/// <summary>
/// Describes one message of an AsyncAPI channel operation.
/// </summary>
/// <param name="MessageName">The message name.</param>
/// <param name="PayloadTypeName">The fully-qualified generated .NET payload type, or <see langword="null"/> when the payload schema produced no named type.</param>
/// <param name="HeadersTypeName">The fully-qualified generated .NET headers type, or <see langword="null"/> when there are no typed headers.</param>
/// <param name="ContentType">The message content type, if declared.</param>
/// <param name="ProducerMethodName">The generated producer's publish method for this message (e.g. <c>PublishTurnOnOffAsync</c>), or <see langword="null"/> for a receive operation.</param>
/// <param name="RequestReplyMethodName">The generated producer's request/reply method for this message (e.g. <c>SendAndReceiveQueryAsync</c>) when the operation declares a <c>reply</c>; otherwise <see langword="null"/>.</param>
public readonly record struct AsyncApiChannelMessageDescriptor(
    string MessageName,
    string? PayloadTypeName,
    string? HeadersTypeName,
    string? ContentType,
    string? ProducerMethodName,
    string? RequestReplyMethodName = null);