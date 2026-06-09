// <copyright file="MockApiTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// An <see cref="IApiTransport"/> that returns scripted responses instead of performing I/O, so a
/// workflow (or any generated client) can be exercised against deterministic outcomes with no real
/// endpoint. Responses are matched by HTTP method + path template; a route may script a sequence of
/// responses (to drive retry/branch paths), the last of which repeats. Every call is recorded for
/// call-path assertions.
/// </summary>
public sealed class MockApiTransport : IApiTransport
{
    private readonly Dictionary<RouteKey, Queue<MockResponse>> routes = new();
    private readonly List<MockApiRequest> requests = [];
    private readonly int defaultStatusCode;
    private TimeSpan responseDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockApiTransport"/> class.
    /// </summary>
    /// <param name="defaultStatusCode">The status code returned when no route matches a request.</param>
    public MockApiTransport(int defaultStatusCode = 404) => this.defaultStatusCode = defaultStatusCode;

    /// <summary>Gets the requests observed so far, in order — the workflow's call path.</summary>
    public IReadOnlyList<MockApiRequest> Requests => this.requests;

    /// <summary>
    /// Sets an artificial delay applied before every response is produced. The delay honours the call's
    /// cancellation token, so a step whose <c>timeout</c> elapses first observes an
    /// <see cref="OperationCanceledException"/> — used to exercise step timeouts.
    /// </summary>
    /// <param name="delay">The delay to apply before each response.</param>
    /// <returns>This instance, for chaining.</returns>
    public MockApiTransport SetResponseDelay(TimeSpan delay)
    {
        this.responseDelay = delay;
        return this;
    }

    /// <summary>
    /// Scripts the response for a route (method + path template), replacing any previously scripted
    /// responses for that route.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathTemplate">The operation path template (e.g. <c>/pets/{petId}</c>).</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="jsonBody">The JSON response body.</param>
    /// <param name="contentType">The response content type.</param>
    /// <param name="headers">Optional response headers.</param>
    /// <returns>This instance, for chaining.</returns>
    public MockApiTransport SetResponse(
        OperationMethod method,
        string pathTemplate,
        int statusCode,
        string jsonBody,
        string contentType = "application/json",
        IReadOnlyDictionary<string, string>? headers = null)
        => this.SetResponse(method, pathTemplate, statusCode, Encoding.UTF8.GetBytes(jsonBody), contentType, headers);

    /// <summary>
    /// Scripts the response for a route, replacing any previously scripted responses for it.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathTemplate">The operation path template.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="body">The response body bytes.</param>
    /// <param name="contentType">The response content type.</param>
    /// <param name="headers">Optional response headers.</param>
    /// <returns>This instance, for chaining.</returns>
    public MockApiTransport SetResponse(
        OperationMethod method,
        string pathTemplate,
        int statusCode,
        byte[] body,
        string contentType = "application/json",
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(pathTemplate);
        ArgumentNullException.ThrowIfNull(body);
        this.routes[new RouteKey(method, pathTemplate)] = new Queue<MockResponse>([new MockResponse(statusCode, body, contentType, headers)]);
        return this;
    }

    /// <summary>
    /// Appends a response to a route's scripted sequence (the last response repeats once exhausted).
    /// Use to drive a step that is retried or revisited.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathTemplate">The operation path template.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="jsonBody">The JSON response body.</param>
    /// <param name="contentType">The response content type.</param>
    /// <param name="headers">Optional response headers.</param>
    /// <returns>This instance, for chaining.</returns>
    public MockApiTransport EnqueueResponse(
        OperationMethod method,
        string pathTemplate,
        int statusCode,
        string jsonBody,
        string contentType = "application/json",
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(pathTemplate);
        ArgumentNullException.ThrowIfNull(jsonBody);
        if (!this.routes.TryGetValue(new RouteKey(method, pathTemplate), out Queue<MockResponse>? queue))
        {
            queue = new Queue<MockResponse>();
            this.routes[new RouteKey(method, pathTemplate)] = queue;
        }

        queue.Enqueue(new MockResponse(statusCode, Encoding.UTF8.GetBytes(jsonBody), contentType, headers));
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => this.Respond<TRequest, TResponse>(in request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse>
        => this.Respond<TRequest, TResponse>(in request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => this.Respond<TRequest, TResponse>(in request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
        => this.Respond<TRequest, TResponse>(in request, cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;

    private ValueTask<TResponse> Respond<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        string template = Encoding.UTF8.GetString(TRequest.PathTemplateUtf8);
        this.requests.Add(new MockApiRequest(TRequest.Method, ResolvePath(in request)));

        MockResponse response = this.Match(TRequest.Method, template);
        if (this.responseDelay > TimeSpan.Zero)
        {
            return RespondAfterDelayAsync<TResponse>(this.responseDelay, response, cancellationToken);
        }

        var stream = new MemoryStream(response.Body, writable: false);
        IResponseHeaders? headers = response.Headers is null ? null : new DictionaryResponseHeaders(response.Headers);
        return TResponse.CreateAsync(response.StatusCode, stream, response.ContentType, headers, cancellationToken: cancellationToken);
    }

    private static async ValueTask<TResponse> RespondAfterDelayAsync<TResponse>(TimeSpan delay, MockResponse response, CancellationToken cancellationToken)
        where TResponse : struct, IApiResponse<TResponse>
    {
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        var stream = new MemoryStream(response.Body, writable: false);
        IResponseHeaders? headers = response.Headers is null ? null : new DictionaryResponseHeaders(response.Headers);
        return await TResponse.CreateAsync(response.StatusCode, stream, response.ContentType, headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string ResolvePath<TRequest>(in TRequest request)
        where TRequest : struct, IApiRequest<TRequest>
    {
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

    private MockResponse Match(OperationMethod method, string template)
    {
        if (this.routes.TryGetValue(new RouteKey(method, template), out Queue<MockResponse>? queue) && queue.Count > 0)
        {
            return queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        }

        return new MockResponse(this.defaultStatusCode, [], "application/json", null);
    }

    private readonly record struct RouteKey(OperationMethod Method, string PathTemplate);

    private readonly record struct MockResponse(int StatusCode, byte[] Body, string ContentType, IReadOnlyDictionary<string, string>? Headers);

    private sealed class DictionaryResponseHeaders(IReadOnlyDictionary<string, string> headers) : IResponseHeaders
    {
        public bool TryGetValue(string name, out string? value) => headers.TryGetValue(name, out value);
    }
}