// <copyright file="PollyResilienceMiddleware.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Polly;

namespace Corvus.Text.Json.AsyncApi.Polly;

/// <summary>
/// Creates <see cref="MessageHandlerMiddleware"/> delegates backed by Polly
/// <see cref="ResiliencePipeline"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Use this class to wrap message handler invocations with Polly resilience strategies
/// (retry, circuit-breaker, timeout, rate-limiter, hedging, etc.).
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var pipeline = new ResiliencePipelineBuilder()
///     .AddRetry(new RetryStrategyOptions
///     {
///         MaxRetryAttempts = 3,
///         BackoffType = DelayBackoffType.Exponential,
///     })
///     .AddCircuitBreaker(new CircuitBreakerStrategyOptions())
///     .Build();
///
/// var options = new NatsTransportOptions
/// {
///     Url = "nats://localhost:4222",
///     HandlerMiddleware = PollyResilienceMiddleware.Create(pipeline),
/// };
/// </code>
/// </para>
/// </remarks>
public static class PollyResilienceMiddleware
{
    /// <summary>
    /// Creates a <see cref="MessageHandlerMiddleware"/> that wraps handler execution
    /// with the specified <see cref="ResiliencePipeline"/>.
    /// </summary>
    /// <param name="pipeline">The Polly resilience pipeline to apply. This pipeline
    /// controls retry, circuit-breaker, timeout, and other resilience strategies.</param>
    /// <returns>A <see cref="MessageHandlerMiddleware"/> delegate that can be assigned
    /// to <see cref="ITransportOptions.HandlerMiddleware"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pipeline"/> is <see langword="null"/>.</exception>
    public static MessageHandlerMiddleware Create(ResiliencePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        return async (operation, cancellationToken) =>
        {
            await pipeline.ExecuteAsync(
                static async (state, ct) =>
                {
                    await state(ct).ConfigureAwait(false);
                },
                operation,
                cancellationToken).ConfigureAwait(false);
        };
    }
}