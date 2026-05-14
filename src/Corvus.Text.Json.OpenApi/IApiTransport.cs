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
/// The transport returns an <see cref="ApiResponse"/> that provides a
/// <see cref="ApiResponse.ContentStream"/> for direct parsing. Generated
/// client code parses the stream into V5 types via
/// <see cref="ParsedJsonDocument{T}.ParseAsync(Stream, JsonDocumentOptions, CancellationToken)"/>
/// — no intermediate buffer, no deserialization step.
/// </para>
/// <para>
/// For requests with a body, the generic
/// <see cref="SendAsync{TBody}(in ApiRequest, in TBody, CancellationToken)"/>
/// overload accepts the typed body and writes it directly into the transport's
/// output stream. The body is never materialized into an intermediate buffer.
/// </para>
/// </remarks>
public interface IApiTransport : IAsyncDisposable
{
    /// <summary>
    /// Sends an API request with no body and returns the response.
    /// </summary>
    /// <param name="request">The request metadata. Passed by <c>in</c> reference;
    /// the transport must consume all request data synchronously before any async I/O.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// An <see cref="ApiResponse"/> containing the status code and content stream.
    /// The caller must dispose the response when finished.
    /// </returns>
    ValueTask<ApiResponse> SendAsync(
        in ApiRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an API request with a typed body and returns the response.
    /// </summary>
    /// <typeparam name="TBody">The type of the request body. Must be a value type
    /// implementing <see cref="IJsonElement{TBody}"/> so the transport can write it
    /// directly to its output stream via WriteTo.</typeparam>
    /// <param name="request">The request metadata. Passed by <c>in</c> reference.</param>
    /// <param name="body">The request body. Passed by <c>in</c> reference; the transport
    /// copies the small struct synchronously before any async I/O.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// An <see cref="ApiResponse"/> containing the status code and content stream.
    /// The caller must dispose the response when finished.
    /// </returns>
    ValueTask<ApiResponse> SendAsync<TBody>(
        in ApiRequest request,
        in TBody body,
        CancellationToken cancellationToken = default)
        where TBody : struct, IJsonElement<TBody>;
}