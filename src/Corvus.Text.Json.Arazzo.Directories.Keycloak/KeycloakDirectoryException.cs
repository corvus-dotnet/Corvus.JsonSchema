// <copyright file="KeycloakDirectoryException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.Keycloak;

/// <summary>
/// Thrown when <see cref="KeycloakPrincipalDirectory"/> cannot complete a request against the Keycloak Admin REST API —
/// the token endpoint or an Admin resource returned a non-success status, or a response was missing / malformed. The
/// message names only non-sensitive detail (status code / shape), never the admin credential or access token, so it is
/// safe to log.
/// </summary>
public sealed class KeycloakDirectoryException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="KeycloakDirectoryException"/> class.</summary>
    /// <param name="reason">A non-sensitive description of the failure.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public KeycloakDirectoryException(string reason, Exception? innerException = null)
        : base($"Could not complete the Keycloak Admin request: {reason}", innerException)
    {
    }
}