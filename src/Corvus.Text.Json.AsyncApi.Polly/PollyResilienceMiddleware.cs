// <copyright file="PollyResilienceMiddleware.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Polly;

namespace Corvus.Text.Json.AsyncApi.Polly;

/// <summary>
/// Creates <see cref="MessageHandlerMiddleware"/> instances backed by Polly
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
    /// <returns>A <see cref="MessageHandlerMiddleware"/> that can be assigned
    /// to <see cref="ITransportOptions.HandlerMiddleware"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pipeline"/> is <see langword="null"/>.</exception>
    public static MessageHandlerMiddleware Create(ResiliencePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        return new PipelineMiddleware(pipeline);
    }

    // Both bridges pass the transport's (operation, state) pair through Polly's own generic
    // state channel, so a middleware-wrapped dispatch allocates no closure at any layer.
    private sealed class PipelineMiddleware(ResiliencePipeline pipeline) : MessageHandlerMiddleware
    {
        public override ValueTask InvokeAsync<TState>(
            Func<TState, CancellationToken, ValueTask> operation,
            TState state,
            CancellationToken cancellationToken)
        {
            return pipeline.ExecuteAsync(
                static (s, ct) => s.operation(s.state, ct),
                (operation, state),
                cancellationToken);
        }

        public override ValueTask<TResult> InvokeAsync<TState, TResult>(
            Func<TState, CancellationToken, ValueTask<TResult>> operation,
            TState state,
            CancellationToken cancellationToken)
        {
            return pipeline.ExecuteAsync(
                static (s, ct) => s.operation(s.state, ct),
                (operation, state),
                cancellationToken);
        }
    }
}