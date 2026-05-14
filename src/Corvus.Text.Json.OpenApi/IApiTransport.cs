// <copyright file="IApiTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
/// The transport returns raw response bytes in an <see cref="ApiResponse"/>.
/// Generated client code parses these bytes directly into V5 types via
/// <see cref="ParsedJsonDocument{T}"/> — no
/// intermediate object model, no deserialization step.
/// </para>
/// </remarks>
public interface IApiTransport : IAsyncDisposable
{
    /// <summary>
    /// Sends an API request and returns the raw response.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// An <see cref="ApiResponse"/> containing the status code, headers, and raw body bytes.
    /// The caller must dispose the response when finished.
    /// </returns>
    ValueTask<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken = default);
}