// <copyright file="HttpClientTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// An <see cref="IApiTransport"/> implementation backed by <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// Request bodies are written directly into the HTTP stream via
/// WriteTo — no intermediate buffer is allocated.
/// Response bodies are returned as a <see cref="Stream"/> for direct parsing
/// into <see cref="ParsedJsonDocument{T}"/>.
/// </para>
/// <para>
/// The transport does not own the <see cref="HttpClient"/> by default.
/// Pass <c>disposeClient: true</c> to the constructor if the transport should dispose
/// the client when it is itself disposed.
/// </para>
/// </remarks>
public sealed class HttpClientTransport : IApiTransport
{
    private readonly HttpClient httpClient;
    private readonly bool disposeClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientTransport"/> class.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for sending requests.</param>
    /// <param name="disposeClient">
    /// <see langword="true"/> to dispose <paramref name="httpClient"/> when this transport
    /// is disposed; <see langword="false"/> (the default) to leave it to the caller.
    /// </param>
    public HttpClientTransport(HttpClient httpClient, bool disposeClient = false)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.disposeClient = disposeClient;
    }

    /// <inheritdoc/>
    public ValueTask<ApiResponse> SendAsync(
        in ApiRequest request,
        CancellationToken cancellationToken = default)
    {
        HttpRequestMessage httpRequest = BuildHttpRequest(in request);
        return SendCoreAsync(httpRequest, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ApiResponse> SendAsync<TBody>(
        in ApiRequest request,
        in TBody body,
        CancellationToken cancellationToken = default)
        where TBody : struct, IJsonElement<TBody>
    {
        HttpRequestMessage httpRequest = BuildHttpRequest(in request);
        httpRequest.Content = new JsonElementContent<TBody>(body);
        return SendCoreAsync(httpRequest, cancellationToken);
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

    private static HttpMethod MapMethod(OperationMethod method) =>
        method switch
        {
            OperationMethod.Get => HttpMethod.Get,
            OperationMethod.Post => HttpMethod.Post,
            OperationMethod.Put => HttpMethod.Put,
            OperationMethod.Delete => HttpMethod.Delete,
            OperationMethod.Patch => HttpMethod.Patch,
            OperationMethod.Head => HttpMethod.Head,
            OperationMethod.Options => HttpMethod.Options,
            OperationMethod.Trace => HttpMethod.Trace,
            _ => new HttpMethod(method.ToString()),
        };

    private static HttpRequestMessage BuildHttpRequest(in ApiRequest request)
    {
        string uri = request.Path;

        ReadOnlySpan<KeyValuePair<string, string>> queryParams = request.QueryParameters;
        if (queryParams.Length > 0)
        {
            uri = BuildUriWithQuery(uri, queryParams);
        }

        HttpRequestMessage httpRequest = new(MapMethod(request.Method), uri);

        foreach (KeyValuePair<string, string> header in request.Headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return httpRequest;
    }

    private async ValueTask<ApiResponse> SendCoreAsync(
        HttpRequestMessage httpRequest,
        CancellationToken cancellationToken)
    {
        // Use ResponseHeadersRead so the content stream is available immediately
        // without buffering the entire response body.
        HttpResponseMessage httpResponse = await this.httpClient
            .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            Stream contentStream = await httpResponse.Content
#if NET
                .ReadAsStreamAsync(cancellationToken)
#else
                .ReadAsStreamAsync()
#endif
                .ConfigureAwait(false);

            return new ApiResponse(
                (int)httpResponse.StatusCode,
                contentStream,
                new HttpResponseOwner(httpRequest, httpResponse));
        }
        catch
        {
            httpResponse.Dispose();
            httpRequest.Dispose();
            throw;
        }
    }

    private static string BuildUriWithQuery(
        string basePath,
        ReadOnlySpan<KeyValuePair<string, string>> parameters)
    {
        DefaultInterpolatedStringHandler handler = new(0, 0, null, stackalloc char[256]);
        handler.AppendLiteral(basePath);
        handler.AppendLiteral("?");

        bool first = true;
        foreach (KeyValuePair<string, string> param in parameters)
        {
            if (!first)
            {
                handler.AppendLiteral("&");
            }

            handler.AppendFormatted(System.Uri.EscapeDataString(param.Key));
            handler.AppendLiteral("=");
            handler.AppendFormatted(System.Uri.EscapeDataString(param.Value));
            first = false;
        }

        return handler.ToStringAndClear();
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
    /// Owns the <see cref="HttpRequestMessage"/> and <see cref="HttpResponseMessage"/>
    /// lifetime. Disposed when the <see cref="ApiResponse"/> is disposed.
    /// </summary>
    private sealed class HttpResponseOwner(
        HttpRequestMessage request,
        HttpResponseMessage response) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            response.Dispose();
            request.Dispose();
            return default;
        }
    }
}