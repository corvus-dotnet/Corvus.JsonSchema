// <copyright file="TracingApiTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Tests.Fakes;

/// <summary>
/// An <see cref="IApiTransport"/> decorator that starts a child <see cref="Activity"/> around each call —
/// standing in for the span a real OpenAPI/HTTP client emits per request (which inherits the ambient trace
/// context). A telemetry test uses it to prove a step's outbound request rides the run's trace, so the
/// originating workflow can be found from the downstream request's telemetry.
/// </summary>
public sealed class TracingApiTransport(MockApiTransport inner) : IApiTransport
{
    /// <summary>The activity source name the emitted per-request spans are published on.</summary>
    public const string SourceName = "Test.Http";

    /// <summary>The operation name of the emitted per-request spans.</summary>
    public const string SpanName = "http.send";

    private static readonly ActivitySource Source = new(SourceName);

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        using Activity? span = Source.StartActivity(SpanName);
        return inner.SendAsync<TRequest, TResponse>(in request, cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, Corvus.Text.Json.Internal.IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse>
    {
        using Activity? span = Source.StartActivity(SpanName);
        return inner.SendAsync<TRequest, TBody, TResponse>(in request, in body, cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        using Activity? span = Source.StartActivity(SpanName);
        return inner.SendAsync<TRequest, TResponse>(in request, body, contentType, cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        using Activity? span = Source.StartActivity(SpanName);
        return inner.SendAsync<TRequest, TResponse>(in request, bodyWriter, contentType, cancellationToken);
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
