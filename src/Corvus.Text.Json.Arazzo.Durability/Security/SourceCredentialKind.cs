// <copyright file="SourceCredentialKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The HTTP auth scheme a <see cref="SourceCredentialBinding"/> resolves its secret material into (design §13).
/// This is non-sensitive metadata: it tells the runner how to build an <c>IHttpAuthenticationProvider</c> from the
/// dereferenced secret(s), without the binding itself carrying any secret.
/// </summary>
public enum SourceCredentialKind
{
    /// <summary>An API key applied as a header or query parameter (one secret, named <c>value</c>).</summary>
    ApiKey,

    /// <summary>A bearer token applied as <c>Authorization: Bearer ...</c> (one secret, named <c>value</c>).</summary>
    Bearer,

    /// <summary>HTTP basic auth (a non-secret <c>username</c> in config and a secret named <c>password</c>).</summary>
    Basic,

    /// <summary>OAuth 2.0 client-credentials flow (a secret named <c>clientSecret</c>; token endpoint/scopes in config).</summary>
    OAuth2ClientCredentials,
}

/// <summary>Maps <see cref="SourceCredentialKind"/> to and from its persisted JSON token.</summary>
public static class SourceCredentialKindExtensions
{
    /// <summary>Gets the persisted JSON token (the <c>authKind</c> enum value) for a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The JSON token.</returns>
    public static string ToJsonToken(this SourceCredentialKind kind) => kind switch
    {
        SourceCredentialKind.ApiKey => "apiKey",
        SourceCredentialKind.Bearer => "bearer",
        SourceCredentialKind.Basic => "basic",
        SourceCredentialKind.OAuth2ClientCredentials => "oauth2ClientCredentials",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown source credential kind."),
    };

    /// <summary>Parses the persisted JSON token (the <c>authKind</c> enum value) to a kind.</summary>
    /// <param name="token">The JSON token.</param>
    /// <returns>The kind.</returns>
    public static SourceCredentialKind Parse(string token) => token switch
    {
        "apiKey" => SourceCredentialKind.ApiKey,
        "bearer" => SourceCredentialKind.Bearer,
        "basic" => SourceCredentialKind.Basic,
        "oauth2ClientCredentials" => SourceCredentialKind.OAuth2ClientCredentials,
        _ => throw new ArgumentException($"Unknown source credential auth kind '{token}'.", nameof(token)),
    };
}