// <copyright file="IMessageDeliveryContextTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Optional message-transport capability that exposes delivery metadata.
/// </summary>
/// <remarks>
/// This is separate from <see cref="IMessageTransport"/> so existing transport
/// implementations remain source-compatible and the legacy subscription API is
/// not changed or adapted through an allocating closure.
/// </remarks>
public interface IMessageDeliveryContextTransport : IMessageTransport
{
    /// <summary>Subscribes while exposing transport delivery metadata.</summary>
    ValueTask SubscribeWithDeliveryContextAsync<TPayload>(
        ReadOnlyMemory<byte> channelUtf8,
        Func<TPayload, MessageDeliveryContext, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
        where TPayload : struct, IJsonElement<TPayload>;
}

// End of file.