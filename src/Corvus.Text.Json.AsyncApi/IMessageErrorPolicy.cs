// <copyright file="IMessageErrorPolicy.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Defines the error-handling policy for message consumers.
/// </summary>
/// <remarks>
/// <para>
/// Implementations decide what happens when an exception is thrown during message
/// processing (validation failure, handler exception, deserialization error, etc.).
/// The consumer invokes this policy and acts on the returned <see cref="MessageErrorAction"/>.
/// </para>
/// </remarks>
public interface IMessageErrorPolicy
{
    /// <summary>
    /// Determines what action to take in response to an error during message processing.
    /// </summary>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="context">Context about the message and delivery attempt.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The <see cref="MessageErrorAction"/> indicating how to proceed.</returns>
    ValueTask<MessageErrorAction> HandleErrorAsync(
        Exception exception,
        MessageErrorContext context,
        CancellationToken cancellationToken = default);
}