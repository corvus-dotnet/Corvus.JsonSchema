// <copyright file="ApiResponse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Represents an API response returned by an <see cref="IApiTransport"/>.
/// </summary>
/// <remarks>
/// <para>
/// The response provides a <see cref="ContentStream"/> for direct parsing into
/// V5 types via <see cref="ParsedJsonDocument{T}"/>. No intermediate buffer is
/// allocated — the caller parses directly from the transport's response stream.
/// </para>
/// <para>
/// Always dispose the response when finished. Disposal releases the underlying
/// transport resources (e.g. HTTP response message, connection back to pool).
/// </para>
/// </remarks>
public sealed class ApiResponse : IAsyncDisposable
{
    private readonly IAsyncDisposable? owner;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiResponse"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="contentStream">The response content stream.</param>
    /// <param name="owner">
    /// An optional disposable that owns the underlying transport resources.
    /// Will be disposed when this response is disposed.
    /// </param>
    public ApiResponse(int statusCode, Stream contentStream, IAsyncDisposable? owner = null)
    {
        this.StatusCode = statusCode;
        this.ContentStream = contentStream;
        this.owner = owner;
    }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the response content stream for direct parsing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parse the stream directly into a V5 type:
    /// <code>
    /// using var doc = await ParsedJsonDocument&lt;MyType&gt;.ParseAsync(response.ContentStream, ct);
    /// </code>
    /// </para>
    /// </remarks>
    public Stream ContentStream { get; }

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
            throw new ApiException(this.StatusCode);
        }

        return this;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!this.disposed)
        {
            this.disposed = true;
#if NET
            await this.ContentStream.DisposeAsync().ConfigureAwait(false);
#else
            this.ContentStream.Dispose();
#endif
            if (this.owner is not null)
            {
                await this.owner.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}