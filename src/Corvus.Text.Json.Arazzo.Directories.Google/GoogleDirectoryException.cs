// <copyright file="GoogleDirectoryException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories.Google;

/// <summary>
/// Thrown when <see cref="GooglePrincipalDirectory"/> cannot complete a request against the Google Admin SDK Directory
/// API or the token endpoint — a non-success status, a missing / malformed response, or an unusable service-account key.
/// The message names only non-sensitive detail (status code / shape), never the private key or access token, so it is
/// safe to log.
/// </summary>
public sealed class GoogleDirectoryException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GoogleDirectoryException"/> class.</summary>
    /// <param name="reason">A non-sensitive description of the failure.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public GoogleDirectoryException(string reason, Exception? innerException = null)
        : base($"Could not complete the Google Directory request: {reason}", innerException)
    {
    }
}