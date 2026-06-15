// <copyright file="SecretResolutionException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Thrown when an <see cref="ISecretResolver"/> cannot dereference a <see cref="SecretRef"/> — the scheme is not
/// configured, the secret does not exist, or the store rejected the read. The message deliberately names only the
/// <em>reference</em> (never any secret material) so it is safe to log.
/// </summary>
public sealed class SecretResolutionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SecretResolutionException"/> class.</summary>
    /// <param name="reference">The reference that could not be resolved.</param>
    /// <param name="reason">A non-sensitive description of why resolution failed.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public SecretResolutionException(SecretRef reference, string reason, Exception? innerException = null)
        : base($"Could not resolve secret reference '{reference}': {reason}", innerException)
        => this.Reference = reference;

    /// <summary>Gets the reference that could not be resolved.</summary>
    public SecretRef Reference { get; }
}