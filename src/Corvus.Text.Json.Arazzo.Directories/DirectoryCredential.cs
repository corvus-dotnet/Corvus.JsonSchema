// <copyright file="DirectoryCredential.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// How a directory adapter authenticates to its directory (design §16.5.4): a non-secret connection is configured in
/// plain config, and the credential itself is a <see cref="SecretRef"/> resolved at runtime through the deployment's
/// <see cref="ISecretResolver"/> — the same <strong>§13 secret boundary</strong> source credentials use, so there is no
/// new secret store and the control plane holds only the reference, never the material. The credential is a bind
/// password, an OAuth2 client secret, an API token, or a service-account key, depending on the provider.
/// </summary>
/// <param name="Reference">The reference to the secret material (e.g. <c>keyvault://…</c>, <c>vault://…</c>, <c>env://…</c>).</param>
public readonly record struct DirectoryCredential(SecretRef Reference)
{
    /// <summary>Parses a directory credential from a secret-reference string (e.g. <c>keyvault://app/dir-client-secret</c>).</summary>
    /// <param name="reference">The secret-reference string.</param>
    /// <returns>The credential.</returns>
    /// <exception cref="FormatException"><paramref name="reference"/> is not a valid secret reference.</exception>
    public static DirectoryCredential Parse(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new DirectoryCredential(SecretRef.Parse(reference));
    }

    /// <summary>
    /// Resolves the credential to its UTF-8 string value (bind password / client secret / API token) through
    /// <paramref name="resolver"/>, scrubbing the underlying material before returning. The caller uses the returned
    /// string immediately (an LDAP bind, an OAuth2 token mint) and lets it fall out of scope.
    /// </summary>
    /// <param name="resolver">The deployment's secret resolver.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The secret as a string.</returns>
    public async ValueTask<string> ResolveStringAsync(ISecretResolver resolver, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        using SecretMaterial material = await resolver.ResolveAsync(this.Reference, cancellationToken).ConfigureAwait(false);
        return material.Reveal();
    }

    /// <summary>Resolves the credential to its raw UTF-8 bytes (e.g. a service-account JSON key) through <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The deployment's secret resolver.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The secret bytes (the caller owns the returned array).</returns>
    public async ValueTask<byte[]> ResolveBytesAsync(ISecretResolver resolver, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        using SecretMaterial material = await resolver.ResolveAsync(this.Reference, cancellationToken).ConfigureAwait(false);
        return material.Utf8.ToArray();
    }
}