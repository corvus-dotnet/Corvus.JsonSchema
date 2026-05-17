// <copyright file="IApiResponse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Represents a strongly-typed API response generated from an OpenAPI specification.
/// </summary>
/// <typeparam name="TSelf">The concrete response type (CRTP pattern).</typeparam>
/// <remarks>
/// <para>
/// Generated code produces a per-operation struct implementing this interface.
/// The struct provides discriminated accessors for each declared response status code,
/// parsing the response body lazily into the corresponding schema-generated type.
/// </para>
/// <para>
/// For example, a <c>ListPetsResponse</c> might expose:
/// <code>
/// public bool TryGetOk(out Pets result) { ... }    // 200
/// public bool TryGetDefault(out Error result) { ... } // default
/// </code>
/// </para>
/// <para>
/// The response owns the underlying stream and parsed document memory.
/// Always dispose via <see cref="IAsyncDisposable.DisposeAsync"/>.
/// </para>
/// </remarks>
public interface IApiResponse<TSelf> : IAsyncDisposable
    where TSelf : struct, IApiResponse<TSelf>
{
    /// <summary>
    /// Gets the HTTP status code of the response.
    /// </summary>
    int StatusCode { get; }

    /// <summary>
    /// Gets a value indicating whether the response has a success status code (2xx).
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    /// Creates a response instance from the transport's raw output.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="contentStream">
    /// The response content stream. The response takes ownership and will dispose it.
    /// </param>
    /// <param name="contentType">
    /// The value of the <c>Content-Type</c> response header, or <see langword="null"/>
    /// if no content type was provided. Used to select the correct parsing strategy
    /// when a status code can return multiple content types (e.g. JSON or text/plain).
    /// </param>
    /// <param name="responseHeaders">
    /// Optional response headers from the transport. Generated response types
    /// extract typed header values from this provider.
    /// </param>
    /// <param name="owner">
    /// An optional disposable that owns transport resources (e.g. the HTTP response message).
    /// Will be disposed when the response is disposed.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task producing the typed response.</returns>
    /// <remarks>
    /// <para>
    /// The generated implementation parses the stream into the correct schema-generated
    /// type based on the status code, using <c>ParsedJsonDocument&lt;T&gt;.ParseAsync</c>
    /// for zero-copy stream parsing.
    /// </para>
    /// </remarks>
    static abstract ValueTask<TSelf> CreateAsync(
        int statusCode,
        Stream contentStream,
        string? contentType = null,
        IResponseHeaders? responseHeaders = null,
        IAsyncDisposable? owner = null,
        CancellationToken cancellationToken = default);
}