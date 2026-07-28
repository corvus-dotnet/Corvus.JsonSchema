// <copyright file="LambdaServerlessDeployerLocalStackTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Amazon.Lambda;
using Amazon.Runtime;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.LocalStack;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy.Tests;

/// <summary>
/// Exercises <see cref="LambdaServerlessDeployer"/> against a real AWS Lambda control plane (LocalStack, ADR 0060): the
/// same <c>IAmazonLambda</c> code the runner runs against real AWS, with only the endpoint pointed at LocalStack. It
/// proves the deploy sequence (create the function, wait for it to be active, create an AWS_IAM Function URL) and the
/// re-deploy (update) path. LocalStack Community does not enforce IAM, so this proves the deploy mechanics, not the
/// invoke auth (ADR 0059); executing a real native bootstrap under LocalStack is a separate, heavier proof.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class LambdaServerlessDeployerLocalStackTests
{
    private static LocalStackContainer container = null!;
    private static AmazonLambdaClient client = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        // Testcontainers.LocalStack defaults to localstack/localstack:2.0, whose Lambda runtime enum predates
        // provided.al2023; a modern Community image is required for the custom AOT runtime (ADR 0060, verified not
        // assumed). A pinned 3.x Community image supports provided.al2023 and runs free (unlike :stable/:latest, which
        // now demand a Pro licence token even to start). ACTIVATE_PRO=0 keeps it in Community mode.
        container = new LocalStackBuilder()
            .WithImage("localstack/localstack:3.0")
            .WithEnvironment("ACTIVATE_PRO", "0")
            .Build();
        await container.StartAsync();

        var config = new AmazonLambdaConfig
        {
            ServiceURL = container.GetConnectionString(),
            AuthenticationRegion = "us-east-1",
        };
        client = new AmazonLambdaClient(new BasicAWSCredentials("test", "test"), config);
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
    public async Task Deploys_a_function_and_returns_an_iam_function_url()
    {
        // CreateFunction stores the function without executing it, so a non-executable placeholder bootstrap proves the
        // control-plane deploy sequence (a real bootstrap's execution is a separate proof).
        ServerlessDeployResult result = await Deployer().DeployAsync(
            new ServerlessDeployRequest("flow", 1, "production", "linux-x64", new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', 1, 2, 3 }),
            default);

        result.Succeeded.ShouldBeTrue(result.Log);
        result.FunctionUrl.ShouldNotBeNullOrEmpty();
        result.FunctionUrl.ShouldStartWith("http");
    }

    // The re-deploy (UpdateFunctionCode) path is a well-formed AWS call proven against real AWS; it is not integration
    // tested here because LocalStack refuses to update a function that has never run, and a placeholder bootstrap (used
    // so the deploy needs no real AOT build) can never run. Executing a real bootstrap under LocalStack is a separate,
    // heavier proof.
    private static LambdaServerlessDeployer Deployer()
        => new(client, new LambdaDeployerOptions { ExecutionRoleArn = "arn:aws:iam::000000000000:role/lambda-role" });
}