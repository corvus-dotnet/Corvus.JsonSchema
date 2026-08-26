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
/// <para>
/// Retried attempts re-send the request body. JSON element bodies re-serialize on
/// every attempt, and write callbacks are invoked once per attempt, so both must be
/// re-invocable (a callback must write the same content each time it runs). A
/// seekable <see cref="Stream"/> body is rewound to its entry position before each
/// attempt. A non-seekable stream body can only be retried while it is still
/// unconsumed (for example after a connection was refused); once an attempt has read
/// from it, a retry fails with <see cref="InvalidOperationException"/> rather than
/// silently sending a truncated body.
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
    {
        if (body.CanSeek)
        {
            // Rewind to the entry position before every attempt, so a retry
            // re-sends the bytes a failed attempt already consumed.
            long position = body.Position;
            return this.pipeline.ExecuteAsync(
                static (state, token) =>
                {
                    if (state.body.Position != state.position)
                    {
                        state.body.Seek(state.position, SeekOrigin.Begin);
                    }

                    return state.inner.SendAsync<TRequest, TResponse>(in state.request, state.body, state.contentType, token);
                },
                (inner: this.inner, request, body, contentType, position),
                cancellationToken);
        }

        // A non-seekable body cannot be replayed. Track consumption so an attempt
        // that never read the body (connection refused, breaker open, rate-limited)
        // can still retry, while one that consumed bytes fails with a clear error
        // instead of silently sending a truncated body.
        ConsumptionTrackingStream tracking = new(body);
        return this.pipeline.ExecuteAsync(
            static (state, token) =>
            {
                state.tracking.ThrowIfConsumed();
                return state.inner.SendAsync<TRequest, TResponse>(in state.request, state.tracking, state.contentType, token);
            },
            (inner: this.inner, request, tracking, contentType),
            cancellationToken);
    }

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

    /// <summary>
    /// A forward-only wrapper over a non-seekable request body that records whether
    /// any bytes have been read, so a retry can distinguish an untouched body (safe
    /// to send) from a partially consumed one (must fail). Disposal does not dispose
    /// the wrapped stream; its lifetime belongs to the caller.
    /// </summary>
    private sealed class ConsumptionTrackingStream : Stream
    {
        private readonly Stream inner;
        private long bytesRead;

        public ConsumptionTrackingStream(Stream inner)
        {
            this.inner = inner;
        }

        public override bool CanRead => this.inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => this.inner.Length;

        public override long Position
        {
            get => this.inner.Position;
            set => throw new NotSupportedException();
        }

        public void ThrowIfConsumed()
        {
            if (this.bytesRead > 0)
            {
                ThrowHelper.ThrowNonSeekableBodyConsumed();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = this.inner.Read(buffer, offset, count);
            this.bytesRead += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            int read = this.inner.Read(buffer);
            this.bytesRead += read;
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int read = await this.inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
            this.bytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await this.inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            this.bytesRead += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}