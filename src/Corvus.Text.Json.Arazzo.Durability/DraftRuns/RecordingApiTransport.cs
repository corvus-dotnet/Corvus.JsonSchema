// <copyright file="RecordingApiTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An <see cref="IApiTransport"/> decorator that records a metadata-only exchange — the HTTP method, the
/// resolved pre-auth request path, and the response status code — for every call it forwards to the wrapped
/// transport (workflow-designer design §18 slice 3e-2a). It records no request or response body and no
/// headers, so a <em>real</em> host-executed debug run yields the same <c>SimulationTrace</c>-shaped record
/// the designer's debug dock already renders for a simulated run, without recording bodies (the ratified §18
/// body posture: bodies are a later per-environment opt-in). Wrap the credential-aware transport the runner
/// binds for a draft run — the decorator sees only the resolved operation path, never the authenticated
/// request the inner transport builds.
/// </summary>
/// <remarks>
/// <para>
/// The path is composed from the generated request itself (<c>WriteResolvedPath</c> / <c>PathTemplateUtf8</c>
/// then <c>WriteQueryString</c>), exactly as <c>HttpClientTransport</c> and <c>MockApiTransport</c> compose
/// it, and <em>before</em> the call is forwarded to the inner transport that applies authentication — so the
/// recorded path is the pre-auth operation path and never carries a signed query string or credential (the
/// §13.5 pre-auth invariant, here by construction: the decorator never touches the authenticated
/// <see cref="System.Net.Http.HttpRequestMessage"/> the inner transport builds).
/// </para>
/// <para>
/// Recorded exchanges are appended under a lock and <see cref="Exchanges"/> exposes them in order. A durable
/// workflow run issues its API calls sequentially — Arazzo 1.x has no parallel steps — so an exchange is
/// appended once its status is known and the order reflects the run's call path.
/// </para>
/// <para>
/// The decorator does not own the wrapped transport in the sense of altering its lifetime beyond forwarding:
/// <see cref="DisposeAsync"/> disposes the inner transport (the resumer disposes the bound transport per run),
/// but the recorded <see cref="Exchanges"/> remain readable afterwards so a trace can be assembled once the
/// run has finished.
/// </para>
/// </remarks>
public sealed class RecordingApiTransport : IApiTransport
{
    private readonly IApiTransport inner;
    private readonly List<RecordedApiExchange> exchanges = [];
    private readonly Lock gate = new();

    /// <summary>Initializes a new instance of the <see cref="RecordingApiTransport"/> class.</summary>
    /// <param name="inner">The transport each call is forwarded to (typically the credential-aware transport
    /// the runner binds for the draft run's source).</param>
    public RecordingApiTransport(IApiTransport inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.inner = inner;
    }

    /// <summary>Gets a snapshot of the exchanges recorded so far, in call order.</summary>
    public IReadOnlyList<RecordedApiExchange> Exchanges
    {
        get
        {
            lock (this.gate)
            {
                return this.exchanges.ToArray();
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        string path = ResolvePath(in request);
        return this.RecordAsync<TResponse>(TRequest.Method, path, this.inner.SendAsync<TRequest, TResponse>(in request, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse>
    {
        // The body is forwarded untouched and is never inspected or recorded (the no-bodies posture).
        string path = ResolvePath(in request);
        return this.RecordAsync<TResponse>(TRequest.Method, path, this.inner.SendAsync<TRequest, TBody, TResponse>(in request, in body, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        string path = ResolvePath(in request);
        return this.RecordAsync<TResponse>(TRequest.Method, path, this.inner.SendAsync<TRequest, TResponse>(in request, body, contentType, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        string path = ResolvePath(in request);
        return this.RecordAsync<TResponse>(TRequest.Method, path, this.inner.SendAsync<TRequest, TResponse>(in request, bodyWriter, contentType, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => this.inner.DisposeAsync();

    private static string ResolvePath<TRequest>(in TRequest request)
        where TRequest : struct, IApiRequest<TRequest>
    {
        // Compose the resolved operation path exactly as HttpClientTransport.BuildUri / MockApiTransport.ResolvePath
        // do: the resolved path template, plus the query string (with the leading '?') when any query parameter is set.
        // This runs synchronously before the call is forwarded, so nothing the inner transport does to authenticate
        // the request can leak into the recorded path.
        var writer = new ArrayBufferWriter<byte>(256);
        if (TRequest.HasPathParameters)
        {
            request.WriteResolvedPath(writer);
        }
        else
        {
            writer.Write(TRequest.PathTemplateUtf8);
        }

        if (TRequest.HasQueryParameters)
        {
            int pathEnd = writer.WrittenCount;
            writer.Write("?"u8);
            if (request.WriteQueryString(writer) == 0)
            {
                return Encoding.UTF8.GetString(writer.WrittenSpan[..pathEnd]);
            }
        }

        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private async ValueTask<TResponse> RecordAsync<TResponse>(OperationMethod method, string path, ValueTask<TResponse> pending)
        where TResponse : struct, IApiResponse<TResponse>
    {
        TResponse response = await pending.ConfigureAwait(false);
        lock (this.gate)
        {
            this.exchanges.Add(new RecordedApiExchange(method, path, response.StatusCode));
        }

        return response;
    }
}