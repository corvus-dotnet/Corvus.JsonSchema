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
/// processing after the <see cref="MessageHandlerMiddleware"/> has exhausted all
/// retry attempts. The transport invokes this policy and acts on the returned
/// <see cref="MessageErrorAction"/>.
/// </para>
/// <para>
/// This interface is injected per-transport at construction time. Implementations
/// can differentiate behaviour by inspecting the <see cref="MessageErrorContext.Channel"/>
/// and <see cref="MessageErrorContext.ErrorKind"/>.
/// </para>
/// </remarks>
public interface IMessageErrorPolicy
{
    /// <summary>
    /// Determines what action to take in response to a terminal error during message processing.
    /// </summary>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="context">Context about the message and error kind.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The <see cref="MessageErrorAction"/> indicating how to proceed.</returns>
    ValueTask<MessageErrorAction> HandleErrorAsync(
        Exception exception,
        MessageErrorContext context,
        CancellationToken cancellationToken = default);
}