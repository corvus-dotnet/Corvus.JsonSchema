// <copyright file="HashiCorpVaultSecretResolverBuilderExtensionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace Corvus.Text.Json.Arazzo.Durability.Vault.Tests;

/// <summary>Tests that <see cref="HashiCorpVaultSecretResolverBuilderExtensions.AddHashiCorpVault"/> registers the <c>vault://</c> scheme on the builder.</summary>
[TestClass]
public sealed class HashiCorpVaultSecretResolverBuilderExtensionsTests
{
    [TestMethod]
    public void AddHashiCorpVault_registers_a_resolver_for_the_vault_scheme()
    {
        // Constructing the client makes no network call; CanResolve never touches it.
        var client = new VaultClient(new VaultClientSettings("http://localhost:8200", new TokenAuthMethodInfo("root")));

        ISecretResolver resolver = new SecretResolverBuilder()
            .AddEnvironmentAndFile()
            .AddHashiCorpVault(client)
            .Build();

        resolver.CanResolve(SecretScheme.HashiCorpVault).ShouldBeTrue();
        resolver.CanResolve(SecretScheme.Environment).ShouldBeTrue();
    }

    [TestMethod]
    public void AddHashiCorpVault_rejects_a_null_client()
    {
        Should.Throw<ArgumentNullException>(() => new SecretResolverBuilder().AddHashiCorpVault(null!));
    }
}