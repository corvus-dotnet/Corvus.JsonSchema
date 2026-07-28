// <copyright file="ServerlessLiveExecutionLocalStackTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy;
using Corvus.Text.Json.Arazzo.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.LocalStack;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The automated proof that a version's real native-AOT bootstrap <b>executes under a live AWS Lambda runtime</b>, which
/// ADR 0055 asserts and ADR 0060 says must be verified, not assumed. It compiles a real executor to a native binary in
/// the build container, deploys it with the production <see cref="LambdaServerlessDeployer"/> to LocalStack (the local
/// AWS analogy, ADR 0060), and invokes it. The invocation carries a probe payload with no <c>runId</c>, so the deployed
/// function runs to the point of parsing the invocation and reports the expected error — proving the whole chain
/// (compile, sign-derive, deploy, and execute the AOT binary under Lambda's own runtime, reaching the Arazzo invocation
/// handler). A run that advances to completion through the checkpoint surface is the companion proof.
/// </summary>
/// <remarks>
/// This is opt-in (<c>[TestCategory("integration")][TestCategory("docker")]</c>): it needs a container runtime,
/// the <c>arazzo-aot-builder</c> image, and the local package feed the runtime graph restores from. It skips unless
/// <c>ARAZZO_AOT_LOCAL_FEED</c> and <c>ARAZZO_AOT_RUNTIME_VERSION</c> are set. Under rootless podman, LocalStack needs the
/// podman socket and prebuilt images to execute a function (ADR 0060): set <c>ARAZZO_LOCALSTACK_DOCKER_SOCK</c> to the
/// host socket path (default <c>/var/run/docker.sock</c> suits Docker CI).
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class ServerlessLiveExecutionLocalStackTests
{
    // A minimal source-calling workflow: one GET on the "echo" source. The same shape the demo deploys, so the native
    // binary carries genuine workflow, transport-binding, and JSON logic rather than a trivial stub.
    private const string WorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Serverless Check", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "echo", "url": "./echo.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "serverless-check",
              "steps": [
                {
                  "stepId": "callEcho",
                  "operationId": "echo",
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "status": "$response.body#/status" }
                }
              ],
              "outputs": { "status": "$steps.callEcho.outputs.status" }
            }
          ]
        }
        """;

    private const string EchoOpenApi = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Echo API", "version": "1.0.0" },
          "paths": {
            "/demo/echo": {
              "get": {
                "operationId": "echo",
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": { "application/json": { "schema": { "type": "object", "properties": { "status": { "type": "string" } } } } }
                  }
                }
              }
            }
          }
        }
        """;

    [TestMethod]
    public async Task The_deployed_native_bootstrap_executes_under_localstack_and_reaches_the_invocation_handler()
    {
        string? feed = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_LOCAL_FEED");
        string? runtimeVersion = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_RUNTIME_VERSION");
        if (string.IsNullOrEmpty(feed) || string.IsNullOrEmpty(runtimeVersion))
        {
            Assert.Inconclusive("Set ARAZZO_AOT_LOCAL_FEED (the local package feed path) and ARAZZO_AOT_RUNTIME_VERSION (its Corvus runtime version), and build the arazzo-aot-builder image, to run this proof.");
            return;
        }

        string image = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_IMAGE") ?? "arazzo-aot-builder:net10";
        string dockerSock = System.Environment.GetEnvironmentVariable("ARAZZO_LOCALSTACK_DOCKER_SOCK") ?? "/var/run/docker.sock";

        // 1. Compile the real executor to a native binary in the build container (the same path catalog-add + the build
        // worker drive). A build failure surfaces the provider's progress, not a bare null.
        byte[] nativeBinary = await BuildNativeBootstrapAsync(feed, runtimeVersion, image);

        // 2. Run LocalStack 4.9.2 (the SAME image the demo pins) with the container runtime it needs to spawn the Lambda
        // exec container, and prebuilt images so it bakes the code in via a podman build rather than the unreliable
        // runtime put_archive under rootless podman (ADR 0060).
        LocalStackContainer localstack = new LocalStackBuilder()
            .WithImage("localstack/localstack:4.9.2")
            .WithBindMount(dockerSock, "/var/run/docker.sock")
            .WithEnvironment("DOCKER_HOST", "unix:///var/run/docker.sock")
            .WithEnvironment("LAMBDA_PREBUILD_IMAGES", "1")
            .WithEnvironment("LAMBDA_RUNTIME_ENVIRONMENT_TIMEOUT", "90")
            .Build();
        await localstack.StartAsync();
        try
        {
            using var client = new AmazonLambdaClient(
                new BasicAWSCredentials("test", "test"),
                new AmazonLambdaConfig { ServiceURL = localstack.GetConnectionString(), AuthenticationRegion = "us-east-1" });

            // 3. Deploy the real binary with the production deployer (CreateFunction on provided.al2023, wait for Active).
            var deployer = new LambdaServerlessDeployer(client, new LambdaDeployerOptions { ExecutionRoleArn = "arn:aws:iam::000000000000:role/lambda-role" });
            ServerlessDeployResult deploy = await deployer.DeployAsync(
                new ServerlessDeployRequest("serverless-check", 1, "isolated", "linux-x64", nativeBinary),
                default);
            deploy.Succeeded.ShouldBeTrue(deploy.Log);

            // The one deployed function (its name is the deployer's concern; the invoke is by name, not Function URL, so
            // the test does not depend on LocalStack being on host :4566).
            ListFunctionsResponse functions = await client.ListFunctionsAsync(new ListFunctionsRequest());
            string functionName = functions.Functions.ShouldHaveSingleItem().FunctionName;

            // 4. Invoke it. A probe with no runId makes the function run through to parsing the invocation and fault with
            // the documented error, which proves the native binary executed under Lambda's runtime and reached the
            // Arazzo handler — the exact "verified, not assumed" ADR 0060 names.
            InvokeResponse invoke = await client.InvokeAsync(new InvokeRequest
            {
                FunctionName = functionName,
                InvocationType = InvocationType.RequestResponse,
                Payload = """{"probe":true}""",
            });

            string payload = invoke.Payload is null ? string.Empty : Encoding.UTF8.GetString(invoke.Payload.ToArray());
            invoke.FunctionError.ShouldNotBeNullOrEmpty($"the deployed bootstrap did not fault as expected; payload: {payload}");

            // The Arazzo handler's ArgumentException for the missing runId. Assert on the exception message ("runId"),
            // which survives native-AOT symbol stripping, rather than a stack-frame name, which does not.
            payload.ShouldContain("runId", customMessage: $"the fault did not reach the Arazzo invocation handler; payload: {payload}");
        }
        finally
        {
            await localstack.DisposeAsync();
        }
    }

    private static async Task<byte[]> BuildNativeBootstrapAsync(string feed, string runtimeVersion, string image)
    {
        var buildLog = new List<string>();
        WorkflowExecutorArtifact? executor = new WorkflowExecutorProvider(durable: true, progress: buildLog.Add).BuildExecutor(
            Encoding.UTF8.GetBytes(WorkflowJson),
            [new("echo", Encoding.UTF8.GetBytes(EchoOpenApi))],
            "live-execution-hash");
        executor.ShouldNotBeNull($"the executor did not build. Provider progress:\n{string.Join("\n", buildLog)}");

        AssembledHostApp hostApp = new AotHostAppAssembler().Assemble(
            executor!.Value.Assembly,
            executor.Value.Manifest,
            "linux-x64",
            new AotHostAppOptions
            {
                RuntimePackageVersion = runtimeVersion,
                FeedSources =
                [
                    ("local", "/work/local-packages"),
                    ("nuget.org", "https://api.nuget.org/v3/index.json"),
                    ("dotnet-eng", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json"),
                    ("dotnet-libraries", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries/nuget/v3/index.json"),
                ],
            });

        var builder = new ContainerWorkflowAotBuilder(new ContainerAotBuilderOptions
        {
            ContainerImage = image,
            ReadOnlyMounts = [(feed, "/work/local-packages")],
        });

        AotBuildResult result = await builder.BuildAsync(hostApp, default);
        result.Succeeded.ShouldBeTrue($"AOT build failed. Log:\n{result.Log}");
        return result.NativeBinary.ToArray();
    }
}