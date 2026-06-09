// <copyright file="CrashingApiTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Tests.Fakes;

/// <summary>
/// An <see cref="IApiTransport"/> that delegates to an inner <see cref="MockApiTransport"/> but throws a raw
/// exception once a given number of calls have been made — simulating an uncontrolled process crash partway
/// through a workflow (as opposed to a controlled step failure), so a durability test can prove the last
/// checkpoint survives.
/// </summary>
public sealed class CrashingApiTransport(MockApiTransport inner, int crashOnCall) : IApiTransport
{
    private int calls;

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        this.Guard();
        return inner.SendAsync<TRequest, TResponse>(in request, cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, Corvus.Text.Json.Internal.IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse>
    {
        this.Guard();
        return inner.SendAsync<TRequest, TBody, TResponse>(in request, in body, cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        this.Guard();
        return inner.SendAsync<TRequest, TResponse>(in request, body, contentType, cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        this.Guard();
        return inner.SendAsync<TRequest, TResponse>(in request, bodyWriter, contentType, cancellationToken);
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private void Guard()
    {
        if (++this.calls >= crashOnCall)
        {
            throw new InvalidOperationException("Simulated process crash.");
        }
    }
}