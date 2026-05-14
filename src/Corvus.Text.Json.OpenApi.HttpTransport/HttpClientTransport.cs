// <copyright file="HttpClientTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// An <see cref="IApiTransport"/> implementation backed by <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// Response bodies are read into <see cref="ArrayPool{T}"/>-rented buffers so that
/// callers can parse them directly into <see cref="ParsedJsonDocument{T}"/> without
/// an intermediate copy.
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
    public async ValueTask<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using HttpRequestMessage httpRequest = BuildHttpRequest(request);

        HttpResponseMessage httpResponse = await this.httpClient
            .SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await ReadResponseAsync(httpResponse, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            httpResponse.Dispose();
        }
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

    private static HttpRequestMessage BuildHttpRequest(ApiRequest request)
    {
        string uri = request.Path;

        if (request.QueryParameters.Count > 0)
        {
            StringBuilder sb = new(uri);
            sb.Append('?');
            bool first = true;
            foreach (KeyValuePair<string, string> param in request.QueryParameters)
            {
                if (!first)
                {
                    sb.Append('&');
                }

                sb.Append(Uri.EscapeDataString(param.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(param.Value));
                first = false;
            }

            uri = sb.ToString();
        }

        HttpRequestMessage httpRequest = new(new HttpMethod(request.Method), uri);

        foreach (KeyValuePair<string, string> header in request.Headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Body is { } body)
        {
            httpRequest.Content = new ReadOnlyMemoryContent(body);
            if (request.ContentType is not null)
            {
                httpRequest.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(request.ContentType);
            }
        }

        return httpRequest;
    }

    private static async Task<ApiResponse> ReadResponseAsync(
        HttpResponseMessage httpResponse,
        CancellationToken cancellationToken)
    {
        int statusCode = (int)httpResponse.StatusCode;

        // Read response body into a rented buffer
        using Stream stream = await httpResponse.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        long? contentLength = httpResponse.Content.Headers.ContentLength;
        int initialCapacity = contentLength.HasValue && contentLength.Value <= int.MaxValue
            ? (int)contentLength.Value
            : 4096;

        byte[] rentedArray = ArrayPool<byte>.Shared.Rent(initialCapacity);
        int totalRead = 0;

        try
        {
            while (true)
            {
                if (totalRead == rentedArray.Length)
                {
                    // Grow the buffer
                    byte[] newArray = ArrayPool<byte>.Shared.Rent(rentedArray.Length * 2);
                    Buffer.BlockCopy(rentedArray, 0, newArray, 0, totalRead);
                    ArrayPool<byte>.Shared.Return(rentedArray);
                    rentedArray = newArray;
                }

                int bytesRead = await stream
                    .ReadAsync(rentedArray.AsMemory(totalRead), cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
            }

            // Extract response headers
            ImmutableDictionary<string, string>.Builder headers =
                ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, IEnumerable<string>> header in httpResponse.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in httpResponse.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            return new ApiResponse(
                statusCode,
                rentedArray.AsMemory(0, totalRead),
                rentedArray,
                headers.ToImmutable());
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rentedArray);
            throw;
        }
    }
}