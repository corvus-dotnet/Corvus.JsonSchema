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

    private static HttpRequestMessage BuildHttpRequest<TRequest>(in TRequest request)
        where TRequest : struct, IApiRequest<TRequest>
    {
        string uri = BuildUri(in request);
        HttpRequestMessage httpRequest = new(MapMethod(TRequest.Method), uri);

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

        return httpRequest;
    }

    private static string BuildUri<TRequest>(in TRequest request)
        where TRequest : struct, IApiRequest<TRequest>
    {
        ArrayBufferWriter<byte> writer = new(256);

        if (TRequest.HasPathParameters)
        {
            request.WriteResolvedPath(writer);
        }
        else
        {
            ReadOnlySpan<byte> template = TRequest.PathTemplateUtf8;
            writer.Write(template);
        }

        if (TRequest.HasQueryParameters)
        {
            int pathLength = writer.WrittenCount;
            int queryBytes = request.WriteQueryString(writer);
            if (queryBytes > 0)
            {
                // Insert '?' between path and query.
                // We wrote path bytes, then query bytes were appended.
                // Need to insert the '?' separator. Since IBufferWriter
                // doesn't support insert, we build path + "?" + query.
                byte[] combined = new byte[pathLength + 1 + queryBytes];
                writer.WrittenSpan[..pathLength].CopyTo(combined);
                combined[pathLength] = (byte)'?';
                writer.WrittenSpan[pathLength..].CopyTo(combined.AsSpan(pathLength + 1));
                return Encoding.UTF8.GetString(combined);
            }
        }

        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private async ValueTask<TResponse> SendCoreAsync<TResponse>(
        HttpRequestMessage httpRequest,
        CancellationToken cancellationToken)
        where TResponse : struct, IApiResponse<TResponse>
    {
        HttpResponseMessage httpResponse = await this.httpClient
            .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            Stream contentStream = await httpResponse.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            return await TResponse.CreateAsync(
                (int)httpResponse.StatusCode,
                contentStream,
                new HttpResponseOwner(httpResponse),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            httpResponse.Dispose();
            throw;
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
}