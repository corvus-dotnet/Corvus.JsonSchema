// <copyright file="IApiTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Abstraction for sending API requests and receiving responses.
/// </summary>
/// <remarks>
/// <para>
/// Transport implementations should be provided in their own assemblies
/// so that consumers only pull in the transports they need. For example,
/// <c>Corvus.Text.Json.OpenApi.HttpTransport</c> provides an
/// <see cref="HttpClient"/>-based implementation.
/// </para>
/// <para>
/// The transport is a low-level HTTP/messaging pipe. All OpenAPI semantics
/// (path template resolution, parameter serialization, response parsing) are
/// handled by the generated request and response types. The transport only
/// needs to:
/// </para>
/// <list type="number">
/// <item><description>Call <c>WriteResolvedPath</c> / <c>WriteQueryString</c> /
/// <c>WriteHeaders</c> on the request to construct the URI and headers.</description></item>
/// <item><description>Send the HTTP request (optionally writing a body).</description></item>
/// <item><description>Call <c>TResponse.CreateAsync</c> to parse the response.</description></item>
/// </list>
/// </remarks>
public interface IApiTransport : IAsyncDisposable
{
    /// <summary>
    /// Sends a typed API request with no body and returns a typed response.
    /// </summary>
    /// <typeparam name="TRequest">The generated request type for this operation.</typeparam>
    /// <typeparam name="TResponse">The generated response type for this operation.</typeparam>
    /// <param name="request">The request, passed by <c>in</c> reference.
    /// The transport must consume all request data (path, query, headers) synchronously
    /// before any async I/O.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A <typeparamref name="TResponse"/> containing the parsed, typed response.
    /// The caller must dispose the response when finished.
    /// </returns>
    ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>;

    /// <summary>
    /// Sends a typed API request with a typed body and returns a typed response.
    /// </summary>
    /// <typeparam name="TRequest">The generated request type for this operation.</typeparam>
    /// <typeparam name="TBody">The type of the request body. Must be a value type
    /// implementing <see cref="IJsonElement{TBody}"/> so the transport can write it
    /// directly to its output stream via WriteTo.</typeparam>
    /// <typeparam name="TResponse">The generated response type for this operation.</typeparam>
    /// <param name="request">The request, passed by <c>in</c> reference.</param>
    /// <param name="body">The request body. Passed by <c>in</c> reference; the transport
    /// copies the small struct synchronously before any async I/O.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A <typeparamref name="TResponse"/> containing the parsed, typed response.
    /// The caller must dispose the response when finished.
    /// </returns>
    ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(
        in TRequest request,
        in TBody body,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse>;

    /// <summary>
    /// Sends a typed API request with a raw stream body and returns a typed response.
    /// </summary>
    /// <typeparam name="TRequest">The generated request type for this operation.</typeparam>
    /// <typeparam name="TResponse">The generated response type for this operation.</typeparam>
    /// <param name="request">The request, passed by <c>in</c> reference.</param>
    /// <param name="body">The request body as a raw stream (e.g. for
    /// <c>application/octet-stream</c> content).</param>
    /// <param name="contentType">The content type to set on the request
    /// (e.g. <c>application/octet-stream</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A <typeparamref name="TResponse"/> containing the parsed, typed response.
    /// The caller must dispose the response when finished.
    /// </returns>
    ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        Stream body,
        string contentType,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>;
}