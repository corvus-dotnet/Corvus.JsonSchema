// <copyright file="OAuth2TokenException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http;

/// <summary>
/// Thrown when the OAuth 2.0 client-credentials token endpoint cannot be exchanged for an access token — the endpoint
/// returned a non-success status, or the response was missing/malformed. The message names only non-sensitive detail
/// (status code / shape), never the credentials or token, so it is safe to log.
/// </summary>
public sealed class OAuth2TokenException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="OAuth2TokenException"/> class.</summary>
    /// <param name="reason">A non-sensitive description of the failure.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public OAuth2TokenException(string reason, Exception? innerException = null)
        : base($"Could not obtain an OAuth 2.0 client-credentials access token: {reason}", innerException)
    {
    }
}