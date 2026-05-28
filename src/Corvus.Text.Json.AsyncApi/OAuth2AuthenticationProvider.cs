// <copyright file="OAuth2AuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Authentication provider for OAuth 2.0 security schemes.
/// </summary>
/// <remarks>
/// <para>
/// Populates the <see cref="MessageAuthenticationContext.Credentials"/> dictionary
/// with <c>access_token</c>, and optionally <c>token_type</c> and <c>scopes</c> entries.
/// Supports dynamic token acquisition via a factory for scenarios where tokens
/// need to be refreshed (client_credentials, authorization_code flows).
/// </para>
/// </remarks>
public sealed class OAuth2AuthenticationProvider : IMessageAuthenticationProvider
{
    private readonly Func<CancellationToken, ValueTask<string>> accessTokenFactory;
    private readonly string tokenType;
    private readonly string? scopes;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuth2AuthenticationProvider"/> class
    /// with a static access token.
    /// </summary>
    /// <param name="accessToken">The OAuth 2.0 access token.</param>
    /// <param name="tokenType">The token type (default: "Bearer").</param>
    /// <param name="scopes">The requested scopes (space-separated).</param>
    public OAuth2AuthenticationProvider(string accessToken, string tokenType = "Bearer", string? scopes = null)
    {
        this.accessTokenFactory = _ => new ValueTask<string>(accessToken);
        this.tokenType = tokenType;
        this.scopes = scopes;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuth2AuthenticationProvider"/> class
    /// with a dynamic token factory.
    /// </summary>
    /// <param name="accessTokenFactory">A factory that acquires or refreshes the access token.</param>
    /// <param name="tokenType">The token type (default: "Bearer").</param>
    /// <param name="scopes">The requested scopes (space-separated).</param>
    public OAuth2AuthenticationProvider(
        Func<CancellationToken, ValueTask<string>> accessTokenFactory,
        string tokenType = "Bearer",
        string? scopes = null)
    {
        this.accessTokenFactory = accessTokenFactory;
        this.tokenType = tokenType;
        this.scopes = scopes;
    }

    /// <inheritdoc/>
    public async ValueTask AuthenticateAsync(
        MessageAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        string token = await this.accessTokenFactory(cancellationToken).ConfigureAwait(false);
        context.Credentials["access_token"] = token;
        context.Credentials["token_type"] = this.tokenType;

        if (this.scopes is not null)
        {
            context.Credentials["scopes"] = this.scopes;
        }
    }
}