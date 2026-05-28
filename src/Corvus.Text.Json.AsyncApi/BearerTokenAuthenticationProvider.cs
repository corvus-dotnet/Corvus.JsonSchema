// <copyright file="BearerTokenAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Authentication provider for HTTP bearer token security schemes.
/// </summary>
/// <remarks>
/// <para>
/// Populates the <see cref="MessageAuthenticationContext.Credentials"/> dictionary
/// with a <c>token</c> entry. Supports both static tokens and dynamic token factories
/// for scenarios where tokens expire and need refreshing.
/// </para>
/// </remarks>
public sealed class BearerTokenAuthenticationProvider : IMessageAuthenticationProvider
{
    private readonly Func<CancellationToken, ValueTask<string>> tokenFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BearerTokenAuthenticationProvider"/> class
    /// with a static bearer token.
    /// </summary>
    /// <param name="token">The bearer token.</param>
    public BearerTokenAuthenticationProvider(string token)
    {
        this.tokenFactory = _ => new ValueTask<string>(token);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BearerTokenAuthenticationProvider"/> class
    /// with a dynamic token factory for token refresh scenarios.
    /// </summary>
    /// <param name="tokenFactory">A factory that produces a fresh bearer token.</param>
    public BearerTokenAuthenticationProvider(Func<CancellationToken, ValueTask<string>> tokenFactory)
    {
        this.tokenFactory = tokenFactory;
    }

    /// <inheritdoc/>
    public async ValueTask AuthenticateAsync(
        MessageAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        string token = await this.tokenFactory(cancellationToken).ConfigureAwait(false);
        context.Credentials["token"] = token;
    }
}