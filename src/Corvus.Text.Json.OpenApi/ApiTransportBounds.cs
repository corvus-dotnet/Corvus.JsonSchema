// <copyright file="ApiTransportBounds.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Applies a request timeout and a response size bound to a transport, whoever built it.
/// </summary>
public static class ApiTransportBounds
{
    /// <summary>
    /// Bounds a transport.
    /// </summary>
    /// <param name="transport">The transport to bound.</param>
    /// <param name="requestTimeout">The longest a single request may take, or <see langword="null"/> for no bound.</param>
    /// <param name="maxResponseLength">The largest response body, in bytes, to read, or <see langword="null"/> for no bound.</param>
    /// <returns>
    /// The bounded transport, which replaces <paramref name="transport"/>: dispose it and not the original. A transport
    /// that implements <see cref="IBoundableApiTransport"/> bounds itself, and so applies both bounds. Any other
    /// transport is given the timeout alone, since nothing outside a transport can see the body it reads. That suits the
    /// transports which have no network body to bound, such as a mock or a simulator.
    /// </returns>
    public static IApiTransport Apply(IApiTransport transport, TimeSpan? requestTimeout, long? maxResponseLength)
    {
        ArgumentNullException.ThrowIfNull(transport);

        if (requestTimeout is null && maxResponseLength is null)
        {
            return transport;
        }

        if (transport is IBoundableApiTransport boundable)
        {
            return boundable.WithBounds(requestTimeout, maxResponseLength);
        }

        return requestTimeout is { } timeout ? new TimeoutApiTransport(transport, timeout) : transport;
    }

    /// <summary>
    /// Bounds any transport in time by cancelling the send when the timeout elapses.
    /// </summary>
    private sealed class TimeoutApiTransport(IApiTransport inner, TimeSpan timeout) : IApiTransport
    {
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
        {
            CancellationTokenSource source = this.Start(cancellationToken);
            try
            {
                return this.AwaitAsync(inner.SendAsync<TRequest, TResponse>(request, source.Token), source, cancellationToken);
            }
            catch
            {
                source.Dispose();
                throw;
            }
        }

        public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TBody : struct, IJsonElement<TBody>
            where TResponse : struct, IApiResponse<TResponse>
        {
            CancellationTokenSource source = this.Start(cancellationToken);
            try
            {
                return this.AwaitAsync(inner.SendAsync<TRequest, TBody, TResponse>(request, body, source.Token), source, cancellationToken);
            }
            catch
            {
                source.Dispose();
                throw;
            }
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
        {
            CancellationTokenSource source = this.Start(cancellationToken);
            try
            {
                return this.AwaitAsync(inner.SendAsync<TRequest, TResponse>(request, body, contentType, source.Token), source, cancellationToken);
            }
            catch
            {
                source.Dispose();
                throw;
            }
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
        {
            CancellationTokenSource source = this.Start(cancellationToken);
            try
            {
                return this.AwaitAsync(inner.SendAsync<TRequest, TResponse>(request, bodyWriter, contentType, source.Token), source, cancellationToken);
            }
            catch
            {
                source.Dispose();
                throw;
            }
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();

        // Linked and disposed with the request, never pooled: a response may keep the token it was created with, and a
        // pooled source would let it observe a later request's timeout.
        private CancellationTokenSource Start(CancellationToken cancellationToken)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            source.CancelAfter(timeout);
            return source;
        }

        private async ValueTask<TResponse> AwaitAsync<TResponse>(ValueTask<TResponse> pending, CancellationTokenSource source, CancellationToken cancellationToken)
            where TResponse : struct, IApiResponse<TResponse>
        {
            try
            {
                return await pending.ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (source.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw ThrowHelper.GetApiTransportTimeoutException(timeout, ex);
            }
            finally
            {
                source.Dispose();
            }
        }
    }
}