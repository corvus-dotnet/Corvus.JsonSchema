// <copyright file="OktaDirectoryException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.Okta;

/// <summary>
/// Thrown when <see cref="OktaPrincipalDirectory"/> cannot complete a request against the Okta Management API — a
/// non-success status, or a missing / malformed response. The message names only non-sensitive detail (status code /
/// shape), never the API token, so it is safe to log.
/// </summary>
public sealed class OktaDirectoryException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="OktaDirectoryException"/> class.</summary>
    /// <param name="reason">A non-sensitive description of the failure.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public OktaDirectoryException(string reason, Exception? innerException = null)
        : base($"Could not complete the Okta Management API request: {reason}", innerException)
    {
    }
}