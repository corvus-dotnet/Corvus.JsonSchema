// <copyright file="DefaultMessageErrorPolicy.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// A default implementation of <see cref="IMessageErrorPolicy"/> that returns a
/// configurable action for terminal failures.
/// </summary>
/// <remarks>
/// <para>
/// Retry logic is handled by the <see cref="MessageHandlerMiddleware"/> (e.g., Polly).
/// This policy only decides what to do with messages that have permanently failed
/// (after all middleware retries are exhausted).
/// </para>
/// <para>
/// Different actions can be configured per <see cref="MessageErrorKind"/>. By default,
/// deserialization errors are dead-lettered, handler errors are dead-lettered, and
/// transport errors abort the subscription.
/// </para>
/// </remarks>
public sealed class DefaultMessageErrorPolicy : IMessageErrorPolicy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMessageErrorPolicy"/> class
    /// with default settings: dead-letter handler/deserialization failures, abort on transport errors.
    /// </summary>
    public DefaultMessageErrorPolicy()
        : this(MessageErrorAction.DeadLetter, MessageErrorAction.DeadLetter, MessageErrorAction.Abort)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMessageErrorPolicy"/> class.
    /// </summary>
    /// <param name="deserializationAction">The action to take when deserialization fails.</param>
    /// <param name="handlerAction">The action to take when the handler throws
    /// (after resilience middleware is exhausted).</param>
    /// <param name="transportAction">The action to take on transport connectivity errors.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any action is not a valid <see cref="MessageErrorAction"/> value.
    /// </exception>
    public DefaultMessageErrorPolicy(
        MessageErrorAction deserializationAction,
        MessageErrorAction handlerAction,
        MessageErrorAction transportAction)
    {
        this.DeserializationAction = deserializationAction;
        this.HandlerAction = handlerAction;
        this.TransportAction = transportAction;
    }

    /// <summary>
    /// Gets the action taken on deserialization errors.
    /// </summary>
    public MessageErrorAction DeserializationAction { get; }

    /// <summary>
    /// Gets the action taken on handler errors (after middleware retries exhausted).
    /// </summary>
    public MessageErrorAction HandlerAction { get; }

    /// <summary>
    /// Gets the action taken on transport connectivity errors.
    /// </summary>
    public MessageErrorAction TransportAction { get; }

    /// <inheritdoc/>
    public ValueTask<MessageErrorAction> HandleErrorAsync(
        Exception exception,
        MessageErrorContext context,
        CancellationToken cancellationToken = default)
    {
        MessageErrorAction action = context.ErrorKind switch
        {
            MessageErrorKind.Deserialization => this.DeserializationAction,
            MessageErrorKind.Handler => this.HandlerAction,
            MessageErrorKind.Transport => this.TransportAction,
            _ => MessageErrorAction.Skip,
        };

        return new ValueTask<MessageErrorAction>(action);
    }
}