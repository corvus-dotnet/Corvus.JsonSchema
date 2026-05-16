// <copyright file="IHttpAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// Provides HTTP-level authentication for API requests.
/// </summary>
/// <remarks>
/// <para>
/// Implementations mutate the <see cref="HttpRequestMessage"/> directly —
/// for example, setting the <c>Authorization</c> header, adding an API key
/// query parameter, or attaching a cookie.
/// </para>
/// <para>
/// The provider is called by <see cref="HttpClientTransport"/> after the
/// request URI, headers, and body have been built from the generated
/// request type, but before the request is sent.
/// </para>
/// <para>
/// For operations that require no authentication, either omit the provider
/// (pass <see langword="null"/> to the transport constructor) or use a no-op
/// implementation.
/// </para>
/// </remarks>
public interface IHttpAuthenticationProvider
{
    /// <summary>
    /// Applies authentication to the outgoing HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request to authenticate. Implementations
    /// may add headers, modify the URI, or attach cookies as needed.</param>
    /// <param name="cancellationToken">A cancellation token that can be used
    /// to cancel the authentication operation (e.g. if a token refresh is
    /// required).</param>
    /// <returns>A task that completes when authentication has been applied.</returns>
    ValueTask AuthenticateAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}