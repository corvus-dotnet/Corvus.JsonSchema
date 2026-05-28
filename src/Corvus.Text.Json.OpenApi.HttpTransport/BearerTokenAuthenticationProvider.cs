// <copyright file="BearerTokenAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// An <see cref="IHttpAuthenticationProvider"/> that sets the
/// <c>Authorization: Bearer {token}</c> header on each request.
/// </summary>
/// <remarks>
/// <para>
/// The token is obtained from a factory delegate, which allows
/// callers to provide a static token, fetch from a cache, or
/// perform an on-demand refresh.
/// </para>
/// <para>
/// This provider is suitable for OAuth 2.0 Bearer tokens,
/// OpenID Connect tokens, and any custom scheme that uses the
/// Bearer token format.
/// </para>
/// </remarks>
public sealed class BearerTokenAuthenticationProvider : IHttpAuthenticationProvider
{
    private readonly Func<CancellationToken, ValueTask<string>> tokenFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BearerTokenAuthenticationProvider"/> class
    /// with a static token value.
    /// </summary>
    /// <param name="token">The bearer token to include in each request.</param>
    public BearerTokenAuthenticationProvider(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        this.tokenFactory = _ => new ValueTask<string>(token);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BearerTokenAuthenticationProvider"/> class
    /// with an asynchronous token factory.
    /// </summary>
    /// <param name="tokenFactory">A delegate that returns the current bearer token.
    /// This is called once per request, allowing token refresh, caching, or
    /// on-demand acquisition.</param>
    public BearerTokenAuthenticationProvider(
        Func<CancellationToken, ValueTask<string>> tokenFactory)
    {
        ArgumentNullException.ThrowIfNull(tokenFactory);
        this.tokenFactory = tokenFactory;
    }

    /// <inheritdoc/>
    public async ValueTask AuthenticateAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string token = await this.tokenFactory(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}