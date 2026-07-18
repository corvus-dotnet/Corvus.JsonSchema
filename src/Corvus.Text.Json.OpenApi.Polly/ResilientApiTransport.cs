// <copyright file="ResilientApiTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;
using global::Polly;

namespace Corvus.Text.Json.OpenApi.Polly;

/// <summary>
/// A decorator that wraps every operation of an <see cref="IApiTransport"/> in a Polly
/// <see cref="ResiliencePipeline"/> (retry, circuit-breaker, timeout, rate-limiter, hedging, etc.). The
/// HTTP-client analogue of <c>Corvus.Text.Json.AsyncApi.Polly.PollyResilienceMiddleware</c>.
/// </summary>
/// <remarks>
/// <para>
/// The whole <c>SendAsync</c> call is executed through the pipeline, so a workflow step's operation gains
/// the pipeline's resilience without the executor knowing about it. This composes with — and is orthogonal
/// to — Arazzo's declarative <c>onFailure</c>/<c>retry</c> actions: the pipeline governs transport-level
/// retries/breaking, while the step actions govern workflow control flow.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
///     .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3, BackoffType = DelayBackoffType.Exponential })
///     .AddCircuitBreaker(new CircuitBreakerStrategyOptions())
///     .Build();
///
/// IApiTransport transport = new ResilientApiTransport(rawTransport, pipeline);
/// </code>
/// </para>
/// <para>
/// Each operation passes its state explicitly to <see cref="ResiliencePipeline.ExecuteAsync{TResult, TState}(Func{TState, CancellationToken, ValueTask{TResult}}, TState, CancellationToken)"/>
/// with a static callback, so no per-call closure is allocated.
/// </para>
/// </remarks>
public sealed class ResilientApiTransport : IApiTransport
{
    private readonly IApiTransport inner;
    private readonly ResiliencePipeline pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientApiTransport"/> class.
    /// </summary>
    /// <param name="inner">The transport to decorate.</param>
    /// <param name="pipeline">The Polly resilience pipeline applied around each operation.</param>
    public ResilientApiTransport(IApiTransport inner, ResiliencePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(pipeline);
        this.inner = inner;
        this.pipeline = pipeline;
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => this.pipeline.ExecuteAsync(
            static (state, token) => state.inner.SendAsync<TRequest, TResponse>(in state.request, token),
            (inner: this.inner, request),
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(
        in TRequest request,
        in TBody body,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse>
        => this.pipeline.ExecuteAsync(
            static (state, token) => state.inner.SendAsync<TRequest, TBody, TResponse>(in state.request, in state.body, token),
            (inner: this.inner, request, body),
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        Stream body,
        string contentType,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => this.pipeline.ExecuteAsync(
            static (state, token) => state.inner.SendAsync<TRequest, TResponse>(in state.request, state.body, state.contentType, token),
            (inner: this.inner, request, body, contentType),
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        Func<Stream, CancellationToken, ValueTask> bodyWriter,
        string contentType,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => this.pipeline.ExecuteAsync(
            static (state, token) => state.inner.SendAsync<TRequest, TResponse>(in state.request, state.bodyWriter, state.contentType, token),
            (inner: this.inner, request, bodyWriter, contentType),
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => this.inner.DisposeAsync();
}