// <copyright file="AwsSecretsManagerSecretResolverBuilderExtensionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.AwsSecretsManager.Tests;

/// <summary>Tests that <see cref="AwsSecretsManagerSecretResolverBuilderExtensions.AddAwsSecretsManager"/> registers the <c>awssm://</c> scheme on the builder.</summary>
[TestClass]
public sealed class AwsSecretsManagerSecretResolverBuilderExtensionsTests
{
    [TestMethod]
    public void AddAwsSecretsManager_registers_a_resolver_for_the_awssm_scheme()
    {
        // Constructing the client makes no network call; CanResolve never touches it.
        using var client = new AmazonSecretsManagerClient(new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1);

        ISecretResolver resolver = new SecretResolverBuilder()
            .AddEnvironmentAndFile()
            .AddAwsSecretsManager(client)
            .Build();

        resolver.CanResolve(SecretScheme.AwsSecretsManager).ShouldBeTrue();
        resolver.CanResolve(SecretScheme.File).ShouldBeTrue();
    }

    [TestMethod]
    public void AddAwsSecretsManager_rejects_a_null_client()
    {
        Should.Throw<ArgumentNullException>(() => new SecretResolverBuilder().AddAwsSecretsManager(null!));
    }
}