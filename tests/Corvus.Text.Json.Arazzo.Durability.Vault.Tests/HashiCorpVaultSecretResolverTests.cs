// <copyright file="HashiCorpVaultSecretResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;

namespace Corvus.Text.Json.Arazzo.Durability.Vault.Tests;

/// <summary>
/// Exercises <see cref="HashiCorpVaultSecretResolver"/> against a real HashiCorp Vault (dev-mode container): reading a
/// field of a KV v2 secret, the missing-field and missing-secret failures, and the required-field guard.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class HashiCorpVaultSecretResolverTests
{
    private const string RootToken = "root";
    private static IContainer container = null!;
    private static IVaultClient client = null!;
    private static HashiCorpVaultSecretResolver resolver = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new ContainerBuilder()
            .WithImage("hashicorp/vault:1.18")
            .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", RootToken)
            .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", "0.0.0.0:8200")
            .WithPortBinding(8200, true)
            .WithCommand("server", "-dev")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Vault server started"))
            .Build();
        await container.StartAsync();

        string address = $"http://{container.Hostname}:{container.GetMappedPublicPort(8200)}";
        IAuthMethodInfo auth = new TokenAuthMethodInfo(RootToken);
        client = new VaultClient(new VaultClientSettings(address, auth));

        // Seed a KV v2 secret with two fields under the default 'secret' mount.
        await client.V1.Secrets.KeyValue.V2.WriteSecretAsync(
            "petstore",
            new Dictionary<string, object> { ["apikey"] = "the-api-key", ["username"] = "petstore-svc" },
            mountPoint: "secret");

        resolver = new HashiCorpVaultSecretResolver(client);
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task Resolves_a_named_field_of_a_kv_v2_secret()
    {
        using SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse("vault://secret/petstore#apikey"), default);

        material.Reveal().ShouldBe("the-api-key");
    }

    [TestMethod]
    public async Task Resolves_a_different_field_of_the_same_secret()
    {
        using SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse("vault://secret/petstore#username"), default);

        material.Reveal().ShouldBe("petstore-svc");
    }

    [TestMethod]
    public async Task A_reference_without_a_field_throws()
    {
        await Should.ThrowAsync<SecretResolutionException>(async () =>
            await resolver.ResolveAsync(SecretRef.Parse("vault://secret/petstore"), default));
    }

    [TestMethod]
    public async Task A_missing_field_throws_a_resolution_exception()
    {
        await Should.ThrowAsync<SecretResolutionException>(async () =>
            await resolver.ResolveAsync(SecretRef.Parse("vault://secret/petstore#nonesuch"), default));
    }

    [TestMethod]
    public async Task A_missing_secret_throws_a_resolution_exception()
    {
        await Should.ThrowAsync<SecretResolutionException>(async () =>
            await resolver.ResolveAsync(SecretRef.Parse("vault://secret/does-not-exist#apikey"), default));
    }
}