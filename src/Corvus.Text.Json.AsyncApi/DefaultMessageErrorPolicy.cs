// <copyright file="DefaultMessageErrorPolicy.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// A default implementation of <see cref="IMessageErrorPolicy"/> with configurable
/// retry limits and a fallback action after retries are exhausted.
/// </summary>
/// <remarks>
/// <para>
/// The default policy allows up to <see cref="MaxRetries"/> retry attempts per message.
/// If retries are exhausted, the <see cref="ExhaustedAction"/> is taken (which defaults
/// to <see cref="MessageErrorAction.Skip"/>).
/// </para>
/// </remarks>
public sealed class DefaultMessageErrorPolicy : IMessageErrorPolicy
{
    /// <summary>
    /// The default maximum number of retry attempts.
    /// </summary>
    public const int DefaultMaxRetries = 3;

    /// <summary>
    /// The default action when retries are exhausted.
    /// </summary>
    public const MessageErrorAction DefaultExhaustedAction = MessageErrorAction.Skip;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMessageErrorPolicy"/> class
    /// with default settings (3 retries, then skip).
    /// </summary>
    public DefaultMessageErrorPolicy()
        : this(DefaultMaxRetries, DefaultExhaustedAction)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMessageErrorPolicy"/> class.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retry attempts per message.
    /// Set to 0 to disable retries (proceed directly to <paramref name="exhaustedAction"/>).</param>
    /// <param name="exhaustedAction">The action to take after retries are exhausted.
    /// Must be <see cref="MessageErrorAction.Skip"/>, <see cref="MessageErrorAction.Abort"/>,
    /// or <see cref="MessageErrorAction.DeadLetter"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxRetries"/> is negative, or <paramref name="exhaustedAction"/>
    /// is <see cref="MessageErrorAction.Retry"/>.
    /// </exception>
    public DefaultMessageErrorPolicy(int maxRetries, MessageErrorAction exhaustedAction)
    {
        if (maxRetries < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(maxRetries));
        }

        if (exhaustedAction == MessageErrorAction.Retry)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(exhaustedAction));
        }

        this.MaxRetries = maxRetries;
        this.ExhaustedAction = exhaustedAction;
    }

    /// <summary>
    /// Gets the maximum number of retry attempts per message.
    /// </summary>
    public int MaxRetries { get; }

    /// <summary>
    /// Gets the action to take after retries are exhausted.
    /// </summary>
    public MessageErrorAction ExhaustedAction { get; }

    /// <inheritdoc/>
    public ValueTask<MessageErrorAction> HandleErrorAsync(
        Exception exception,
        MessageErrorContext context,
        CancellationToken cancellationToken = default)
    {
        // AttemptNumber is 1-based; after MaxRetries retries, attempt == MaxRetries + 1
        MessageErrorAction action = context.AttemptNumber <= this.MaxRetries
            ? MessageErrorAction.Retry
            : this.ExhaustedAction;

        return new ValueTask<MessageErrorAction>(action);
    }
}