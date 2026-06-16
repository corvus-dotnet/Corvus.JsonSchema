// <copyright file="KeyVaultSecretResolverBuilderExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.KeyVault;

/// <summary>
/// Registers the Azure Key Vault (<c>keyvault://</c>) resolver on a <see cref="SecretResolverBuilder"/>. This lives in
/// the Key Vault resolver assembly because only it can reference the Azure SDK; the core builder cannot.
/// </summary>
public static class KeyVaultSecretResolverBuilderExtensions
{
    /// <summary>Registers a <see cref="KeyVaultSecretResolver"/> for the <c>keyvault://</c> scheme.</summary>
    /// <param name="builder">The resolver builder.</param>
    /// <param name="credential">The credential the per-vault clients authenticate with — supply a least-privileged,
    /// secrets-<em>get</em>-only identity.</param>
    /// <returns>The builder, for chaining.</returns>
    public static SecretResolverBuilder AddKeyVault(this SecretResolverBuilder builder, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(credential);
        return builder.Add(new KeyVaultSecretResolver(credential));
    }

    /// <summary>Registers a <see cref="KeyVaultSecretResolver"/> over a caller-supplied client factory (the seam tests
    /// use to inject a stub <see cref="SecretClient"/>).</summary>
    /// <param name="builder">The resolver builder.</param>
    /// <param name="clientFactory">Builds a <see cref="SecretClient"/> for a vault URI; invoked once per distinct vault.</param>
    /// <returns>The builder, for chaining.</returns>
    public static SecretResolverBuilder AddKeyVault(this SecretResolverBuilder builder, Func<Uri, SecretClient> clientFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(clientFactory);
        return builder.Add(new KeyVaultSecretResolver(clientFactory));
    }
}