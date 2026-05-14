// <copyright file="ApiResponse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Represents an API response returned by an <see cref="IApiTransport"/>.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Body"/> bytes may be backed by a rented array. Always call
/// <see cref="Dispose"/> when finished. Generated client code parses the body
/// directly into <see cref="ParsedJsonDocument{T}"/> for zero-copy deserialization.
/// </para>
/// </remarks>
public sealed class ApiResponse : IDisposable
{
    private byte[]? rentedArray;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiResponse"/> class with
    /// caller-owned body bytes.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body bytes.</param>
    /// <param name="headers">The response headers.</param>
    public ApiResponse(
        int statusCode,
        ReadOnlyMemory<byte> body,
        ImmutableDictionary<string, string>? headers = null)
    {
        this.StatusCode = statusCode;
        this.Body = body;
        this.Headers = headers ?? ImmutableDictionary<string, string>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiResponse"/> class with
    /// a rented buffer that will be returned on <see cref="Dispose"/>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body as a memory slice into <paramref name="rentedArray"/>.</param>
    /// <param name="rentedArray">The rented array to return to <see cref="ArrayPool{T}.Shared"/>.</param>
    /// <param name="headers">The response headers.</param>
    public ApiResponse(
        int statusCode,
        ReadOnlyMemory<byte> body,
        byte[] rentedArray,
        ImmutableDictionary<string, string>? headers = null)
    {
        this.StatusCode = statusCode;
        this.Body = body;
        this.rentedArray = rentedArray;
        this.Headers = headers ?? ImmutableDictionary<string, string>.Empty;
    }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the response body bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Body { get; }

    /// <summary>
    /// Gets the response headers.
    /// </summary>
    public ImmutableDictionary<string, string> Headers { get; }

    /// <summary>
    /// Gets a value indicating whether the response has a success status code (2xx).
    /// </summary>
    public bool IsSuccess => this.StatusCode >= 200 && this.StatusCode < 300;

    /// <summary>
    /// Throws an <see cref="ApiException"/> if the response does not have a success
    /// status code.
    /// </summary>
    /// <returns>This response, for fluent chaining.</returns>
    /// <exception cref="ApiException">The status code is not in the 2xx range.</exception>
    public ApiResponse EnsureSuccess()
    {
        if (!this.IsSuccess)
        {
            throw new ApiException(this.StatusCode, this.Body);
        }

        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
            if (this.rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(this.rentedArray);
                this.rentedArray = null;
            }
        }
    }
}