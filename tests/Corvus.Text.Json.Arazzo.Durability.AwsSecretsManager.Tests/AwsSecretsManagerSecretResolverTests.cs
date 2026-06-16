// <copyright file="AwsSecretsManagerSecretResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.LocalStack;

namespace Corvus.Text.Json.Arazzo.Durability.AwsSecretsManager.Tests;

/// <summary>
/// Exercises <see cref="AwsSecretsManagerSecretResolver"/> against a real AWS Secrets Manager (LocalStack): a string
/// secret, a binary secret, a version-pinned read, and the not-found failure.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AwsSecretsManagerSecretResolverTests
{
    private static LocalStackContainer container = null!;
    private static AmazonSecretsManagerClient client = null!;
    private static AwsSecretsManagerSecretResolver resolver = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new LocalStackBuilder().Build();
        await container.StartAsync();

        var config = new AmazonSecretsManagerConfig
        {
            ServiceURL = container.GetConnectionString(),
            AuthenticationRegion = "us-east-1",
        };
        client = new AmazonSecretsManagerClient(new BasicAWSCredentials("test", "test"), config);
        resolver = new AwsSecretsManagerSecretResolver(client);
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        client?.Dispose();
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task Resolves_a_string_secret_at_its_current_version()
    {
        await client.CreateSecretAsync(new CreateSecretRequest { Name = "petstore/apikey", SecretString = "the-api-key" });

        using SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse("awssm://petstore/apikey"), default);

        material.Reveal().ShouldBe("the-api-key");
    }

    [TestMethod]
    public async Task Resolves_a_binary_secret()
    {
        byte[] secret = [1, 2, 3, 4, 5];
        await client.CreateSecretAsync(new CreateSecretRequest { Name = "billing/cert", SecretBinary = new MemoryStream(secret) });

        using SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse("awssm://billing/cert"), default);

        material.Utf8.ToArray().ShouldBe(secret);
    }

    [TestMethod]
    public async Task A_pinned_version_reads_that_version_not_the_current_one()
    {
        CreateSecretResponse created = await client.CreateSecretAsync(new CreateSecretRequest { Name = "rotating/token", SecretString = "v1-value" });
        await client.PutSecretValueAsync(new PutSecretValueRequest { SecretId = "rotating/token", SecretString = "v2-value" });

        using SecretMaterial pinned = await resolver.ResolveAsync(SecretRef.Parse($"awssm://rotating/token#{created.VersionId}"), default);
        pinned.Reveal().ShouldBe("v1-value");

        using SecretMaterial current = await resolver.ResolveAsync(SecretRef.Parse("awssm://rotating/token"), default);
        current.Reveal().ShouldBe("v2-value");
    }

    [TestMethod]
    public async Task A_missing_secret_throws_a_resolution_exception()
    {
        await Should.ThrowAsync<SecretResolutionException>(async () =>
            await resolver.ResolveAsync(SecretRef.Parse("awssm://does-not-exist"), default));
    }
}
