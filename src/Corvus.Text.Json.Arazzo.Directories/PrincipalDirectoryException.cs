// <copyright file="PrincipalDirectoryException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// The base of every principal-directory adapter failure (Keycloak/Entra ID/Google/Okta/SCIM): thrown when the
/// adapter cannot complete a request against its backend (a non-success status, or a missing / malformed response).
/// Catching this type lets a consumer treat "the directory is unreachable or misconfigured" uniformly across
/// adapters — the control plane's merged grantee search degrades to observed results on it, and an explicit
/// directory search maps it to a 502 problem instead of an unhandled 500. Messages name only non-sensitive detail
/// (status code / shape), never credentials or tokens, so they are safe to log.
/// </summary>
public class PrincipalDirectoryException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PrincipalDirectoryException"/> class.</summary>
    /// <param name="message">A non-sensitive description of the failure.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public PrincipalDirectoryException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}