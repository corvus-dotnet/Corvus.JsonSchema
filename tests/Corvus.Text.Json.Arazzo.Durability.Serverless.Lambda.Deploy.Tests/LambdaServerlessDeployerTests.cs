// <copyright file="LambdaServerlessDeployerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.IO.Compression;
using Amazon.Lambda;
using Amazon.Runtime;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy.Tests;

/// <summary>
/// Exercises the pure, offline parts of <see cref="LambdaServerlessDeployer"/>: the deployment-zip packaging, the
/// deterministic function-name construction, the runtime-identifier-to-architecture mapping, and the constructor
/// null-guards. The create/update AWS call sequence is proven by a later LocalStack integration test.
/// </summary>
[TestClass]
public sealed class LambdaServerlessDeployerTests
{
    [TestMethod]
    public void BuildDeploymentZip_has_a_single_executable_bootstrap_entry_carrying_the_binary()
    {
        byte[] bootstrap = [0x7F, (byte)'E', (byte)'L', (byte)'F', 1, 2, 3, 4, 5];

        byte[] zip = LambdaServerlessDeployer.BuildDeploymentZip(bootstrap);

        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        archive.Entries.Count.ShouldBe(1);

        ZipArchiveEntry entry = archive.Entries[0];
        entry.FullName.ShouldBe("bootstrap");

        // The high 16 bits of the external attributes carry the Unix file mode; it must decode to 0100755.
        int unixMode = (entry.ExternalAttributes >> 16) & 0xFFFF;
        unixMode.ShouldBe(Convert.ToInt32("100755", 8));

        // Belt and braces: the owner-execute bit is set.
        (unixMode & Convert.ToInt32("100", 8)).ShouldNotBe(0);

        using Stream entryStream = entry.Open();
        using var read = new MemoryStream();
        entryStream.CopyTo(read);
        read.ToArray().ShouldBe(bootstrap);
    }

    [TestMethod]
    public void FunctionName_is_deterministic_and_sanitizes_separators_to_hyphens()
    {
        var request = new ServerlessDeployRequest("orders/create", 3, "prod:eu", "linux-x64", ReadOnlyMemory<byte>.Empty);

        string name = LambdaServerlessDeployer.FunctionName(request);

        name.ShouldBe("arazzo-fn-orders-create-v3-prod-eu-linux-x64");
        name.ShouldBe(LambdaServerlessDeployer.FunctionName(request));
        name.Length.ShouldBeLessThanOrEqualTo(64);
        name.ShouldNotContain("/");
        name.ShouldNotContain(":");
    }

    [TestMethod]
    public void FunctionName_stays_within_64_chars_and_stays_unique_for_over_long_inputs()
    {
        var r1 = new ServerlessDeployRequest(new string('a', 80), 1, "production-environment", "linux-arm64", ReadOnlyMemory<byte>.Empty);
        var r2 = new ServerlessDeployRequest(new string('b', 80), 1, "production-environment", "linux-arm64", ReadOnlyMemory<byte>.Empty);

        string n1 = LambdaServerlessDeployer.FunctionName(r1);
        string n2 = LambdaServerlessDeployer.FunctionName(r2);

        n1.Length.ShouldBeLessThanOrEqualTo(64);
        n2.Length.ShouldBeLessThanOrEqualTo(64);
        n1.ShouldBe(LambdaServerlessDeployer.FunctionName(r1));
        n1.ShouldNotBe(n2);
    }

    [TestMethod]
    public void Architecture_maps_x64_to_X86_64_and_arm64_to_Arm64()
    {
        LambdaServerlessDeployer.Architecture("linux-x64").ShouldBe(Amazon.Lambda.Architecture.X86_64);
        LambdaServerlessDeployer.Architecture("win-x64").ShouldBe(Amazon.Lambda.Architecture.X86_64);
        LambdaServerlessDeployer.Architecture("linux-arm64").ShouldBe(Amazon.Lambda.Architecture.Arm64);
    }

    [TestMethod]
    public void Ctor_rejects_a_null_lambda_client()
    {
        Should.Throw<ArgumentNullException>(() =>
            new LambdaServerlessDeployer(null!, ValidOptions()));
    }

    [TestMethod]
    public void Ctor_rejects_null_options()
    {
        using var client = new AmazonLambdaClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonLambdaConfig { ServiceURL = "http://localhost:4566", AuthenticationRegion = "us-east-1" });

        Should.Throw<ArgumentNullException>(() =>
            new LambdaServerlessDeployer(client, null!));
    }

    private static LambdaDeployerOptions ValidOptions()
        => new() { ExecutionRoleArn = "arn:aws:iam::000000000000:role/lambda-role" };
}