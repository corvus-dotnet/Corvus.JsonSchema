// <copyright file="HashiCorpVaultSecretResolverBuilderExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using VaultSharp;

namespace Corvus.Text.Json.Arazzo.Durability.Vault;

/// <summary>
/// Registers the HashiCorp Vault (<c>vault://</c>) resolver on a <see cref="SecretResolverBuilder"/>. This lives in the
/// Vault resolver assembly because only it can reference VaultSharp; the core builder cannot.
/// </summary>
public static class HashiCorpVaultSecretResolverBuilderExtensions
{
    /// <summary>Registers a <see cref="HashiCorpVaultSecretResolver"/> for the <c>vault://</c> scheme.</summary>
    /// <param name="builder">The resolver builder.</param>
    /// <param name="client">The Vault client (caller-configured address + a least-privileged auth method with read
    /// access to the path).</param>
    /// <returns>The builder, for chaining.</returns>
    public static SecretResolverBuilder AddHashiCorpVault(this SecretResolverBuilder builder, IVaultClient client)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(client);
        return builder.Add(new HashiCorpVaultSecretResolver(client));
    }
}