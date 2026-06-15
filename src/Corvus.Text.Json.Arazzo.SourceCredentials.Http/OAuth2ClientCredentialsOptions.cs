// <copyright file="OAuth2ClientCredentialsOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>How the client credentials are presented to the OAuth 2.0 token endpoint.</summary>
public enum OAuth2ClientAuthentication
{
    /// <summary>Send <c>client_id</c>/<c>client_secret</c> in the request body (the most widely supported form).</summary>
    RequestBody,

    /// <summary>Send the credentials as an HTTP Basic <c>Authorization</c> header (RFC 6749 §2.3.1 preferred form).</summary>
    BasicAuthenticationHeader,
}

/// <summary>
/// The configuration for the OAuth 2.0 client-credentials flow (design §13): the token endpoint and the resolved
/// client credentials, plus the refresh tuning. The <see cref="ClientSecret"/> is live secret material — it is
/// retained in memory only (so the access token can be refreshed) and is never persisted or logged.
/// </summary>
public sealed class OAuth2ClientCredentialsOptions
{
    /// <summary>Gets the token endpoint URI.</summary>
    public required Uri TokenEndpoint { get; init; }

    /// <summary>Gets the client id (non-secret).</summary>
    public required string ClientId { get; init; }

    /// <summary>Gets the resolved client secret (live secret material; memory-only).</summary>
    public required string ClientSecret { get; init; }

    /// <summary>Gets the space-delimited scopes to request, or <see langword="null"/> for none.</summary>
    public string? Scope { get; init; }

    /// <summary>Gets how the client credentials are presented to the token endpoint.</summary>
    public OAuth2ClientAuthentication ClientAuthentication { get; init; } = OAuth2ClientAuthentication.RequestBody;

    /// <summary>Gets how far ahead of expiry the access token is proactively refreshed (default 60s). The hot path never
    /// uses a token within this window of expiring.</summary>
    public TimeSpan RefreshSkew { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets the lifetime assumed when the token response omits <c>expires_in</c> (default 5 minutes).</summary>
    public TimeSpan DefaultTokenLifetime { get; init; } = TimeSpan.FromMinutes(5);
}