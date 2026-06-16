// <copyright file="KeyVaultSecretResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Security.KeyVault.Secrets;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.KeyVault.Tests;

/// <summary>
/// Tests <see cref="KeyVaultSecretResolver"/>'s reference parsing, scheme dispatch and value flow with an injected
/// stub <see cref="SecretClient"/> — Azure has no local emulator, and the only Azure-specific surface (the
/// <c>GetSecretAsync</c> call) is exercised through the stub, which records what it was asked for.
/// </summary>
[TestClass]
public sealed class KeyVaultSecretResolverTests
{
    [TestMethod]
    public async Task Resolves_a_secret_expanding_a_bare_vault_name_and_pinning_the_version()
    {
        Uri? capturedVault = null;
        var stub = new StubSecretClient("super-secret");
        var resolver = new KeyVaultSecretResolver(uri =>
        {
            capturedVault = uri;
            return stub;
        });

        using SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse("keyvault://myvault/db-password#v3"), default);

        material.Reveal().ShouldBe("super-secret");
        capturedVault.ShouldBe(new Uri("https://myvault.vault.azure.net"));
        stub.RequestedName.ShouldBe("db-password");
        stub.RequestedVersion.ShouldBe("v3");
    }

    [TestMethod]
    public async Task A_full_host_locator_is_used_verbatim_and_the_current_version_is_read()
    {
        Uri? capturedVault = null;
        var stub = new StubSecretClient("v");
        var resolver = new KeyVaultSecretResolver(uri =>
        {
            capturedVault = uri;
            return stub;
        });

        using SecretMaterial _ = await resolver.ResolveAsync(SecretRef.Parse("keyvault://corp.vault.example.com/api-key"), default);

        capturedVault.ShouldBe(new Uri("https://corp.vault.example.com"));
        stub.RequestedName.ShouldBe("api-key");
        stub.RequestedVersion.ShouldBeNull();
    }

    [TestMethod]
    public void CanResolve_admits_only_the_keyvault_scheme()
    {
        var resolver = new KeyVaultSecretResolver(_ => new StubSecretClient("v"));
        resolver.CanResolve(SecretScheme.KeyVault).ShouldBeTrue();
        resolver.CanResolve(SecretScheme.AwsSecretsManager).ShouldBeFalse();
        resolver.CanResolve(SecretScheme.HashiCorpVault).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_non_keyvault_reference_is_rejected()
    {
        var resolver = new KeyVaultSecretResolver(_ => new StubSecretClient("v"));
        await Should.ThrowAsync<SecretResolutionException>(async () =>
            await resolver.ResolveAsync(SecretRef.Parse("env://SOME_VAR"), default));
    }

    [TestMethod]
    public async Task A_locator_without_a_secret_name_is_rejected()
    {
        var resolver = new KeyVaultSecretResolver(_ => new StubSecretClient("v"));
        await Should.ThrowAsync<SecretResolutionException>(async () =>
            await resolver.ResolveAsync(SecretRef.Parse("keyvault://justvault"), default));
    }

    private sealed class StubSecretClient(string value) : SecretClient
    {
        public string? RequestedName { get; private set; }

        public string? RequestedVersion { get; private set; }

        public override Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default)
        {
            this.RequestedName = name;
            this.RequestedVersion = version;

            // The resolver reads only Response.Value, so the raw response is irrelevant here.
            return Task.FromResult(Response.FromValue(new KeyVaultSecret(name, value), null!));
        }
    }
}