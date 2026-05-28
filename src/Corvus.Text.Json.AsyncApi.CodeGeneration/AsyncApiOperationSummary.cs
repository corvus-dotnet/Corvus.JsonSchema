// <copyright file="AsyncApiOperationSummary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// A lightweight summary of an AsyncAPI operation for display purposes (e.g. the <c>show</c> command).
/// </summary>
/// <param name="ChannelAddress">The channel address template (e.g. <c>user/signedup</c>).</param>
/// <param name="Action">The operation action (<see cref="OperationAction.Send"/> or <see cref="OperationAction.Receive"/>).</param>
/// <param name="OperationId">The <c>operationId</c>, or <see langword="null"/> if not specified.</param>
/// <param name="Tags">The tags assigned to this operation.</param>
/// <param name="Summary">The operation summary text, or <see langword="null"/>.</param>
/// <param name="MessageCount">The number of messages associated with this operation.</param>
/// <param name="Protocol">The protocol of the channel's bound server, if known (e.g. <c>kafka</c>, <c>amqp</c>).</param>
public readonly record struct AsyncApiOperationSummary(
    string ChannelAddress,
    OperationAction Action,
    string? OperationId,
    string[] Tags,
    string? Summary,
    int MessageCount,
    string? Protocol);