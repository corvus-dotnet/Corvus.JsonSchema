// <copyright file="ScimDirectoryException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.Scim;

/// <summary>
/// Thrown when <see cref="ScimPrincipalDirectory"/> cannot complete a request against the SCIM 2.0 service provider — the
/// endpoint returned a non-success status, or a response was missing / malformed. The message names only non-sensitive
/// detail (status code / shape), never the bearer token, so it is safe to log.
/// </summary>
public sealed class ScimDirectoryException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ScimDirectoryException"/> class.</summary>
    /// <param name="reason">A non-sensitive description of the failure.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public ScimDirectoryException(string reason, Exception? innerException = null)
        : base($"Could not complete the SCIM 2.0 request: {reason}", innerException)
    {
    }
}