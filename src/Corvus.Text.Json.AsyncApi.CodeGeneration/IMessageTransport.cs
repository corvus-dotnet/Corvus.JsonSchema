// <copyright file="IMessageTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Abstraction for message transport (publish/subscribe).
/// </summary>
/// <remarks>
/// <para>
/// Implementations include in-memory (for testing), Kafka, AMQP, etc.
/// Generated producers and consumers depend on this interface.
/// </para>
/// <para>
/// The generated code constrains TPayload to <c>struct, IJsonElement&lt;TPayload&gt;</c>
/// so that any Corvus-generated message type can be published directly.
/// </para>
/// </remarks>
public interface IMessageTransport : IAsyncDisposable
{
    /// <summary>
    /// Publishes a raw message to the specified channel.
    /// </summary>
    /// <param name="channel">The channel address.</param>
    /// <param name="payload">The UTF-8 encoded message payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask PublishAsync(
        string channel,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to messages on the specified channel.
    /// </summary>
    /// <param name="channel">The channel address.</param>
    /// <param name="handler">The message handler delegate.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SubscribeAsync(
        string channel,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from messages on the specified channel.
    /// </summary>
    /// <param name="channel">The channel address.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask UnsubscribeAsync(
        string channel,
        CancellationToken cancellationToken = default);
}