// <copyright file="AzureFunctionsServerlessDeployerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Storage.Blobs;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Tests;

/// <summary>
/// Unit proofs of the <see cref="AzureFunctionsServerlessDeployer"/>'s pure logic: the deterministic package blob name and
/// the constructor guards. The live deploy mechanism (upload to a real blob store, the run-from-package URL, and running
/// the uploaded package) is the Azurite and run-to-completion proofs.
/// </summary>
[TestClass]
public sealed class AzureFunctionsServerlessDeployerTests
{
    [TestMethod]
    public void PackageBlobName_is_deterministic_and_sanitizes_separators_and_lowercases()
    {
        var request = new ServerlessDeployRequest("Adopt.Pet/Flow", 3, "Prod Env", "linux-x64", ReadOnlyMemory<byte>.Empty);

        string first = AzureFunctionsServerlessDeployer.PackageBlobName(request);
        string second = AzureFunctionsServerlessDeployer.PackageBlobName(request);

        // Each of base/env/rid is lowercased and any character outside [a-z0-9-_] becomes '-'; the version is a v{n} segment.
        first.ShouldBe("adopt-pet-flow-v3-prod-env-linux-x64.zip");
        second.ShouldBe(first);
    }

    [TestMethod]
    public void PackageBlobName_distinguishes_targets_that_differ_only_by_version_or_environment()
    {
        var v1 = new ServerlessDeployRequest("check", 1, "isolated", "linux-x64", ReadOnlyMemory<byte>.Empty);
        var v2 = new ServerlessDeployRequest("check", 2, "isolated", "linux-x64", ReadOnlyMemory<byte>.Empty);
        var otherEnv = new ServerlessDeployRequest("check", 1, "staging", "linux-x64", ReadOnlyMemory<byte>.Empty);

        AzureFunctionsServerlessDeployer.PackageBlobName(v1).ShouldBe("check-v1-isolated-linux-x64.zip");
        AzureFunctionsServerlessDeployer.PackageBlobName(v2).ShouldNotBe(AzureFunctionsServerlessDeployer.PackageBlobName(v1));
        AzureFunctionsServerlessDeployer.PackageBlobName(otherEnv).ShouldNotBe(AzureFunctionsServerlessDeployer.PackageBlobName(v1));
    }

    [TestMethod]
    public void Ctor_rejects_a_null_package_container()
    {
        Should.Throw<ArgumentNullException>(() => new AzureFunctionsServerlessDeployer(null!, new NoOpConfigurator(), new AzureFunctionsDeployerOptions()));
    }

    [TestMethod]
    public void Ctor_rejects_a_null_configurator()
    {
        BlobContainerClient container = new(new Uri("https://example.blob.core.windows.net/packages"));
        Should.Throw<ArgumentNullException>(() => new AzureFunctionsServerlessDeployer(container, null!, new AzureFunctionsDeployerOptions()));
    }

    [TestMethod]
    public void Ctor_rejects_null_options()
    {
        BlobContainerClient container = new(new Uri("https://example.blob.core.windows.net/packages"));
        Should.Throw<ArgumentNullException>(() => new AzureFunctionsServerlessDeployer(container, new NoOpConfigurator(), null!));
    }

    private sealed class NoOpConfigurator : IFunctionAppConfigurator
    {
        public ValueTask<Uri> ApplyRunFromPackageAsync(ServerlessDeployRequest request, Uri packageUrl, IReadOnlyDictionary<string, string> appSettings, CancellationToken cancellationToken)
            => ValueTask.FromResult(new Uri("https://example.net/"));
    }
}