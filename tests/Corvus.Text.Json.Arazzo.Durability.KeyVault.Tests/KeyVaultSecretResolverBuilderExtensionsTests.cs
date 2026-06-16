// <copyright file="KeyVaultSecretResolverBuilderExtensionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.KeyVault.Tests;

/// <summary>Tests that <see cref="KeyVaultSecretResolverBuilderExtensions.AddKeyVault(SecretResolverBuilder, Func{Uri, Azure.Security.KeyVault.Secrets.SecretClient})"/> registers the <c>keyvault://</c> scheme on the builder.</summary>
[TestClass]
public sealed class KeyVaultSecretResolverBuilderExtensionsTests
{
    [TestMethod]
    public void AddKeyVault_registers_a_resolver_for_the_keyvault_scheme()
    {
        // The factory is only invoked at resolve time, so composition does not touch the Azure SDK.
        ISecretResolver resolver = new SecretResolverBuilder()
            .AddEnvironmentAndFile()
            .AddKeyVault(_ => null!)
            .Build();

        resolver.CanResolve(SecretScheme.KeyVault).ShouldBeTrue();
        resolver.CanResolve(SecretScheme.Environment).ShouldBeTrue();
    }

    [TestMethod]
    public void AddKeyVault_rejects_a_null_builder()
    {
        Should.Throw<ArgumentNullException>(() => ((SecretResolverBuilder)null!).AddKeyVault(_ => null!));
    }
}