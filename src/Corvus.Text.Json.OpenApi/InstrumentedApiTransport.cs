// <copyright file="InstrumentedApiTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A decorator that adds OpenTelemetry-compliant distributed tracing and metrics to any
/// <see cref="IApiTransport"/>. The HTTP-client analogue of
/// <c>Corvus.Text.Json.AsyncApi.InstrumentedMessageTransport</c>.
/// </summary>
/// <remarks>
/// <para>
/// Wrap any transport with this decorator to gain, per operation, a <see cref="ActivityKind.Client"/> span
/// named <c>{method} {route}</c> with HTTP semantic tags (<c>http.request.method</c>, <c>url.template</c>,
/// <c>http.response.status_code</c>), plus a request-duration histogram and a request counter — so a
/// workflow's step spans correlate end-to-end with the operations they invoke. The span nests under the
/// ambient <see cref="Activity.Current"/> (the workflow step), so no explicit context is threaded.
/// </para>
/// <para>
/// Unlike the AsyncAPI decorator, this does not inject W3C trace context: the outbound request headers are
/// written inside the underlying HTTP transport, whose own client instrumentation (e.g. <see cref="HttpClient"/>)
/// performs downstream <c>traceparent</c> propagation. All instrumentation is zero-cost when no listener is
/// attached.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// IApiTransport raw = new HttpApiTransport(httpClient, baseUri);
/// IApiTransport transport = new InstrumentedApiTransport(raw);
/// </code>
/// </para>
/// </remarks>
public sealed class InstrumentedApiTransport : IApiTransport
{
    private readonly IApiTransport inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentedApiTransport"/> class.
    /// </summary>
    /// <param name="inner">The transport to decorate with instrumentation.</param>
    public InstrumentedApiTransport(IApiTransport inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.inner = inner;
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        TRequest requestCopy = request;
        return this.InstrumentAsync<TRequest, TResponse>(
            ct => this.inner.SendAsync<TRequest, TResponse>(in requestCopy, ct),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(
        in TRequest request,
        in TBody body,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse>
    {
        TRequest requestCopy = request;
        TBody bodyCopy = body;
        return this.InstrumentAsync<TRequest, TResponse>(
            ct => this.inner.SendAsync<TRequest, TBody, TResponse>(in requestCopy, in bodyCopy, ct),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        Stream body,
        string contentType,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        TRequest requestCopy = request;
        return this.InstrumentAsync<TRequest, TResponse>(
            ct => this.inner.SendAsync<TRequest, TResponse>(in requestCopy, body, contentType, ct),
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
    {
        TRequest requestCopy = request;
        return this.InstrumentAsync<TRequest, TResponse>(
            ct => this.inner.SendAsync<TRequest, TResponse>(in requestCopy, bodyWriter, contentType, ct),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => this.inner.DisposeAsync();

    private async ValueTask<TResponse> InstrumentAsync<TRequest, TResponse>(
        Func<CancellationToken, ValueTask<TResponse>> send,
        CancellationToken cancellationToken)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        string method = MethodName<TRequest>();
        string route = Encoding.UTF8.GetString(TRequest.PathTemplateUtf8);

        using Activity? activity = OpenApiTelemetry.ActivitySource.StartActivity(
            $"{method} {route}",
            ActivityKind.Client);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("http.request.method", method);
            activity.SetTag("url.template", route);
        }

        long startTimestamp = Stopwatch.GetTimestamp();
        int statusCode = 0;
        string? errorType = null;
        try
        {
            TResponse response = await send(cancellationToken).ConfigureAwait(false);
            statusCode = response.StatusCode;

            if (statusCode >= 400)
            {
                errorType = statusCode.ToString(CultureInfo.InvariantCulture);
            }

            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag("http.response.status_code", statusCode);
                if (errorType is not null)
                {
                    activity.SetStatus(ActivityStatusCode.Error);
                    activity.SetTag("error.type", errorType);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            errorType = ex.GetType().FullName;
            if (activity is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.SetTag("error.type", errorType);
            }

            throw;
        }
        finally
        {
            var tags = new TagList
            {
                { "http.request.method", method },
                { "url.template", route },
            };

            if (statusCode != 0)
            {
                tags.Add("http.response.status_code", statusCode);
            }

            if (errorType is not null)
            {
                tags.Add("error.type", errorType);
            }

            OpenApiTelemetry.RequestDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
            OpenApiTelemetry.Requests.Add(1, tags);
        }
    }

    private static string MethodName<TRequest>()
        where TRequest : struct, IApiRequest<TRequest>
        => TRequest.Method == OperationMethod.Custom && !TRequest.CustomMethodNameUtf8.IsEmpty
            ? Encoding.UTF8.GetString(TRequest.CustomMethodNameUtf8)
            : TRequest.Method.ToString().ToUpperInvariant();
}