// <copyright file="EntraIdDirectoryException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.EntraId;

/// <summary>
/// Thrown when <see cref="EntraIdPrincipalDirectory"/> cannot complete a request against Microsoft Graph or the identity
/// platform token endpoint — a non-success status, or a missing / malformed response. The message names only
/// non-sensitive detail (status code / shape), never the client secret or access token, so it is safe to log.
/// </summary>
public sealed class EntraIdDirectoryException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="EntraIdDirectoryException"/> class.</summary>
    /// <param name="reason">A non-sensitive description of the failure.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public EntraIdDirectoryException(string reason, Exception? innerException = null)
        : base($"Could not complete the Microsoft Graph request: {reason}", innerException)
    {
    }
}