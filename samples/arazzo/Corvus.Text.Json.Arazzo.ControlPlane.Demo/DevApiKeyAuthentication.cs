// <copyright file="DevApiKeyAuthentication.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo;

/// <summary>
/// Options for <see cref="DevApiKeyAuthenticationHandler"/>: a map of API key → space-delimited capability
/// scopes the key grants.
/// </summary>
public sealed class DevApiKeyOptions : AuthenticationSchemeOptions
{
    /// <summary>Gets the configured API keys mapped to the space-delimited scopes each grants.</summary>
    public IDictionary<string, string> Keys { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// A development authentication scheme for the demo: a caller presents an API key in the <c>X-Api-Key</c>
/// header, and a matching key authenticates them with that key's configured capability scopes (as a
/// <c>scope</c> claim the control-plane policies read). This is the demo's concrete strategy for the
/// per-deployment authentication seam — a production deployment swaps this for JWT bearer / OIDC / mTLS by
/// changing only the <c>AddAuthentication</c> call; the control-plane scope authorization is unchanged.
/// </summary>
public sealed class DevApiKeyAuthenticationHandler(IOptionsMonitor<DevApiKeyOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<DevApiKeyOptions>(options, logger, encoder)
{
    /// <summary>The scheme name.</summary>
    public const string SchemeName = "DevApiKey";

    /// <summary>The header carrying the API key.</summary>
    public const string ApiKeyHeader = "X-Api-Key";

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.Request.Headers.TryGetValue(ApiKeyHeader, out Microsoft.Extensions.Primitives.StringValues presented) || presented.Count == 0)
        {
            // No key presented — unauthenticated (the authorization layer will challenge if the endpoint needs a scope).
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!this.Options.Keys.TryGetValue(presented.ToString(), out string? scopes))
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown API key."));
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim("scope", scopes));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}