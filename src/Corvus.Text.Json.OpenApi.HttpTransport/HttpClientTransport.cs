// <copyright file="HttpClientTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// An <see cref="IApiTransport"/> implementation backed by <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// The transport is a thin HTTP pipe. All OpenAPI semantics (path template resolution,
/// parameter serialization, response parsing) are handled by the generated request and
/// response types via the <see cref="IApiRequest{TSelf}"/> and
/// <see cref="IApiResponse{TSelf}"/> interfaces.
/// </para>
/// <para>
/// URI construction uses a thread-static <see cref="ArrayBufferWriter{T}"/> that is reused
/// across calls. The path, <c>?</c> separator, and query string are written sequentially —
/// no back-tracking or re-copying. A single <see cref="Encoding.UTF8"/> transcoding produces
/// the <see langword="string"/> required by <see cref="HttpRequestMessage"/>.
/// </para>
/// <para>
/// The final request URI is composed by the transport itself. The resolved operation path
/// (which always begins with <c>/</c>) is appended textually to
/// <see cref="HttpClient.BaseAddress"/>, preserving any path prefix the base address
/// carries — for example an API-gateway route such as <c>https://apim.example/inventory/</c>,
/// which yields <c>https://apim.example/inventory/pets</c>. Had resolution been left to
/// <see cref="HttpClient"/>, RFC 3986 §5.3 absolute-path-reference semantics would replace
/// the base address's entire path and silently drop the prefix. For a base address with no
/// path segment the composed URI is identical to the RFC 3986 resolution. If the client has
/// no <see cref="HttpClient.BaseAddress"/>, the request URI is left relative and
/// <see cref="HttpClient"/> raises its usual error.
/// </para>
/// <para>
/// The transport does not own the <see cref="HttpClient"/> by default.
/// Pass <c>disposeClient: true</c> to the constructor if the transport should dispose
/// the client when it is itself disposed.
/// </para>
/// </remarks>
public sealed class HttpClientTransport : IApiTransport
{
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_uriWriter;

    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_cookieWriter;

    private readonly HttpClient httpClient;
    private readonly IHttpAuthenticationProvider? authenticationProvider;
    private readonly bool disposeClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientTransport"/> class.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for sending requests.</param>
    /// <param name="authenticationProvider">An optional authentication provider that is
    /// called to mutate each <see cref="HttpRequestMessage"/> before it is sent. Pass
    /// <see langword="null"/> (the default) for unauthenticated requests.</param>
    /// <param name="disposeClient">
    /// <see langword="true"/> to dispose <paramref name="httpClient"/> when this transport
    /// is disposed; <see langword="false"/> (the default) to leave it to the caller.
    /// </param>
    public HttpClientTransport(
        HttpClient httpClient,
        IHttpAuthenticationProvider? authenticationProvider = null,
        bool disposeClient = false)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.authenticationProvider = authenticationProvider;
        this.disposeClient = disposeClient;
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        HttpRequestMessage httpRequest = BuildHttpRequest(in request);
        return SendCoreAsync<TResponse>(httpRequest, cancellationToken);
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
        HttpRequestMessage httpRequest = BuildHttpRequest(in request);
        httpRequest.Content = new JsonElementContent<TBody>(body);
        return SendCoreAsync<TResponse>(httpRequest, cancellationToken);
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
        HttpRequestMessage httpRequest = BuildHttpRequest(in request);
        httpRequest.Content = new StreamContent(body);
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return SendCoreAsync<TResponse>(httpRequest, cancellationToken);
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
        HttpRequestMessage httpRequest = BuildHttpRequest(in request);
        httpRequest.Content = new DelegatingContent(bodyWriter, contentType);
        return SendCoreAsync<TResponse>(httpRequest, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (this.disposeClient)
        {
            this.httpClient.Dispose();
        }

        return default;
    }

    private static HttpMethod MapMethod<TRequest>(OperationMethod method)
        where TRequest : struct, IApiRequest<TRequest>
    {
        if (method == OperationMethod.Custom)
        {
            ReadOnlySpan<byte> customName = TRequest.CustomMethodNameUtf8;
            return new HttpMethod(System.Text.Encoding.UTF8.GetString(customName));
        }

        return method switch
        {
            OperationMethod.Get => HttpMethod.Get,
            OperationMethod.Post => HttpMethod.Post,
            OperationMethod.Put => HttpMethod.Put,
            OperationMethod.Delete => HttpMethod.Delete,
            OperationMethod.Patch => HttpMethod.Patch,
            OperationMethod.Head => HttpMethod.Head,
            OperationMethod.Options => HttpMethod.Options,
            OperationMethod.Trace => HttpMethod.Trace,
            OperationMethod.Query => new HttpMethod("QUERY"),
            _ => new HttpMethod(method.ToString()),
        };
    }

    private static HttpRequestMessage BuildHttpRequest<TRequest>(in TRequest request)
        where TRequest : struct, IApiRequest<TRequest>
    {
        string relativePath = BuildUri(in request);

        // Use the string-accepting constructor to hold the relative operation URI.
        // SendCoreAsync composes the final absolute URI itself (see ApplyBaseAddress),
        // preserving any path prefix carried by HttpClient.BaseAddress — HttpClient's
        // own RFC 3986 resolution would replace the base path entirely, because
        // operation paths always begin with '/'.
        HttpRequestMessage httpRequest = new(MapMethod<TRequest>(TRequest.Method), relativePath);

        if (TRequest.HasHeaderParameters)
        {
            request.WriteHeaders(
                static (ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, HttpRequestHeaders headers) =>
                {
                    headers.TryAddWithoutValidation(
                        Encoding.UTF8.GetString(name),
                        Encoding.UTF8.GetString(value));
                },
                httpRequest.Headers);
        }

        if (TRequest.HasCookieParameters)
        {
            ArrayBufferWriter<byte> cookieWriter = t_cookieWriter ??= new(256);
            cookieWriter.Clear();
            int cookieBytes = request.WriteCookies(cookieWriter);

            if (cookieBytes > 0)
            {
                httpRequest.Headers.TryAddWithoutValidation(
                    "Cookie",
                    Encoding.UTF8.GetString(cookieWriter.WrittenSpan));
            }
        }

        return httpRequest;
    }

    private static string BuildUri<TRequest>(in TRequest request)
        where TRequest : struct, IApiRequest<TRequest>
    {
        // Reuse a thread-static writer: zero allocation for the writer and its
        // internal byte[] on steady-state calls. The only allocation is the final
        // string required by HttpRequestMessage.
        ArrayBufferWriter<byte> writer = t_uriWriter ??= new(512);
        writer.Clear();

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

            // Write '?' optimistically before the query string.
            writer.Write("?"u8);
            int queryBytes = request.WriteQueryString(writer);

            if (queryBytes == 0)
            {
                // All optional params were unset — discard the '?' by slicing.
                return Encoding.UTF8.GetString(writer.WrittenSpan[..pathEnd]);
            }
        }

        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private async ValueTask<TResponse> SendCoreAsync<TResponse>(
        HttpRequestMessage httpRequest,
        CancellationToken cancellationToken)
        where TResponse : struct, IApiResponse<TResponse>
    {
        this.ApplyBaseAddress(httpRequest);

        if (this.authenticationProvider is not null)
        {
            await this.authenticationProvider
                .AuthenticateAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);
        }

        HttpResponseMessage httpResponse = await this.httpClient
            .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            Stream contentStream = await httpResponse.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            string? contentType = httpResponse.Content.Headers.ContentType?.MediaType;

            return await TResponse.CreateAsync(
                (int)httpResponse.StatusCode,
                contentStream,
                contentType,
                new HttpResponseHeadersAdapter(httpResponse.Headers),
                new HttpResponseOwner(httpResponse),
                this,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            httpResponse.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Composes the final absolute request URI from <see cref="HttpClient.BaseAddress"/>
    /// and the relative operation URI produced by <see cref="BuildHttpRequest"/>,
    /// preserving any path prefix carried by the base address.
    /// </summary>
    /// <param name="httpRequest">The request whose <see cref="HttpRequestMessage.RequestUri"/>
    /// is rewritten in place.</param>
    /// <remarks>
    /// <para>
    /// Generated operation paths always begin with <c>/</c>, so leaving resolution to
    /// <see cref="HttpClient"/> would apply RFC 3986 §5.3 absolute-path-reference semantics
    /// and replace the base address's entire path — silently dropping a prefix such as an
    /// API-gateway route (<c>https://apim.example/inventory/</c>). Composing the URI
    /// textually — the base up to its path with any trailing <c>/</c> trimmed, followed by
    /// the operation path and query — keeps the prefix. For a base address with no path
    /// segment the result is identical to the RFC 3986 resolution.
    /// </para>
    /// <para>
    /// If the client has no <see cref="HttpClient.BaseAddress"/>, or the request URI is
    /// already absolute or does not begin with <c>/</c>, the request is left untouched and
    /// <see cref="HttpClient"/> behaves exactly as before.
    /// </para>
    /// </remarks>
    private void ApplyBaseAddress(HttpRequestMessage httpRequest)
    {
        if (this.httpClient.BaseAddress is Uri baseAddress
            && httpRequest.RequestUri is { IsAbsoluteUri: false } relativeUri
            && relativeUri.OriginalString.StartsWith('/'))
        {
            string basePart = baseAddress.GetLeftPart(UriPartial.Path).TrimEnd('/');
            httpRequest.RequestUri = new Uri(basePart + relativeUri.OriginalString, UriKind.Absolute);
        }
    }

    /// <summary>
    /// Custom <see cref="HttpContent"/> that writes the body directly from an
    /// <see cref="IJsonElement{T}"/> into the HTTP request stream via
    /// <see cref="Utf8JsonWriter"/>. No intermediate buffer is allocated.
    /// </summary>
    private sealed class JsonElementContent<T> : HttpContent
        where T : struct, IJsonElement<T>
    {
        private readonly T body;

        public JsonElementContent(in T body)
        {
            this.body = body;
            this.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8",
            };
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            System.Net.TransportContext? context)
        {
            using Utf8JsonWriter writer = new(stream);
            this.body.WriteTo(writer);
            writer.Flush();
            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>
    /// Custom <see cref="HttpContent"/> that writes the body via an async delegate,
    /// enabling zero-copy serialization for non-JSON body formats such as
    /// <c>application/x-www-form-urlencoded</c> and supporting async streaming from
    /// files, network resources, or other async sources.
    /// </summary>
    private sealed class DelegatingContent : HttpContent
    {
        private readonly Func<Stream, CancellationToken, ValueTask> bodyWriter;

        public DelegatingContent(Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType)
        {
            this.bodyWriter = bodyWriter;
            this.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            System.Net.TransportContext? context)
        {
            await this.bodyWriter(stream, CancellationToken.None).ConfigureAwait(false);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>
    /// Owns the <see cref="HttpResponseMessage"/> lifetime.
    /// Disposed when the response is disposed.
    /// </summary>
    private sealed class HttpResponseOwner(
        HttpResponseMessage response) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            response.Dispose();
            return default;
        }
    }

    /// <summary>
    /// Adapts <see cref="HttpResponseHeaders"/> to <see cref="IResponseHeaders"/>.
    /// </summary>
    private sealed class HttpResponseHeadersAdapter(
        HttpResponseHeaders headers) : IResponseHeaders
    {
        public bool TryGetValue(string headerName, out string? value)
        {
            if (headers.TryGetValues(headerName, out IEnumerable<string>? values))
            {
                // Per RFC 9110 §5.3, multiple values for the same header
                // are semantically equivalent to a single comma-separated value.
                value = string.Join(", ", values);
                return true;
            }

            value = null;
            return false;
        }
    }
}