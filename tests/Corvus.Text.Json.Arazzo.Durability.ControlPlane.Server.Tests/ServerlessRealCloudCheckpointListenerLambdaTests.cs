// <copyright file="ServerlessRealCloudCheckpointListenerLambdaTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.AzureStorage;
using Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.LocalStack;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The AWS Lambda counterpart to <see cref="ServerlessRealCloudCheckpointListenerTests"/>: the real AWS Lambda runtime
/// advances a seeded run to completion through the same <b>publicly deployed, token-authenticated checkpoint listener</b>
/// and its shared Azure Storage store (ADR 0062), proving the mechanism is genuinely vendor-neutral. It compiles the
/// shared <c>serverless-check</c> workflow's real native-AOT bootstrap, deploys it to LocalStack (the local AWS analogy,
/// ADR 0060), seeds a Pending run into the listener's shared store, and invokes the function with a run-scoped bearer
/// token the runner mints. The Lambda loads its checkpoint from the listener over public HTTPS (presenting the token),
/// calls its <c>echo</c> source (served by the listener), and saves the advance back; the listener validates the token
/// and terminates the save into the shared store, where the run reloads <c>Completed</c> with <c>callEcho</c>'s
/// <c>{ "status": "ok" }</c>. The listener is the same one the Azure gates use — one cloud-agnostic checkpoint surface for
/// both vendors.
/// </summary>
/// <remarks>
/// Opt-in (<c>[TestCategory("integration")][TestCategory("docker")][TestCategory("azure")]</c>). It needs a container
/// runtime, the <c>arazzo-aot-builder</c> image, the local package feed, and a pre-deployed listener: it skips unless
/// <c>ARAZZO_AOT_LOCAL_FEED</c>, <c>ARAZZO_AOT_RUNTIME_VERSION</c>, <c>ARAZZO_CHECKPOINT_LISTENER_URL</c>,
/// <c>ARAZZO_CHECKPOINT_SECRET</c>, and <c>ARAZZO_CHECKPOINT_STORAGE</c> are all set. Under rootless podman, LocalStack
/// needs the podman socket (via <c>ARAZZO_LOCALSTACK_DOCKER_SOCK</c>, default <c>/var/run/docker.sock</c>) and prebuilt
/// images (ADR 0060). No endpoint, secret, or storage identifier is baked into source.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
[TestCategory("azure")]
public sealed class ServerlessRealCloudCheckpointListenerLambdaTests
{
    [TestMethod]
    public async Task The_real_lambda_runtime_runs_a_seeded_run_to_completion_through_the_public_token_authenticated_listener()
    {
        string? feed = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_LOCAL_FEED");
        string? runtimeVersion = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_RUNTIME_VERSION");
        string? listenerUrl = System.Environment.GetEnvironmentVariable("ARAZZO_CHECKPOINT_LISTENER_URL");
        string? secretBase64 = System.Environment.GetEnvironmentVariable("ARAZZO_CHECKPOINT_SECRET");
        string? storageConnection = System.Environment.GetEnvironmentVariable("ARAZZO_CHECKPOINT_STORAGE");
        if (string.IsNullOrEmpty(feed) || string.IsNullOrEmpty(runtimeVersion) || string.IsNullOrEmpty(listenerUrl)
            || string.IsNullOrEmpty(secretBase64) || string.IsNullOrEmpty(storageConnection))
        {
            Assert.Inconclusive("Set ARAZZO_AOT_LOCAL_FEED, ARAZZO_AOT_RUNTIME_VERSION, ARAZZO_CHECKPOINT_LISTENER_URL, ARAZZO_CHECKPOINT_SECRET, and ARAZZO_CHECKPOINT_STORAGE (a deployed listener + its shared store) to run this real-cloud proof.");
            return;
        }

        string image = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_IMAGE") ?? "arazzo-aot-builder:net10";
        string dockerSock = System.Environment.GetEnvironmentVariable("ARAZZO_LOCALSTACK_DOCKER_SOCK") ?? "/var/run/docker.sock";
        byte[] checkpointSecret = Convert.FromBase64String(secretBase64);

        // The source base URL is the listener root (its '/demo/echo' path is appended); the checkpoint base URL adds a
        // trailing slash so the function-side store's relative 'runs/{id}/checkpoint' resolves against it.
        string sourceBaseUrl = listenerUrl.TrimEnd('/');
        string checkpointBaseUrl = sourceBaseUrl + "/";

        // Compile the real native-AOT bootstrap for Lambda.
        byte[] nativeBinary = await ServerlessLiveExecutionSupport.BuildDeployArtifactAsync(ServerlessTarget.AwsLambda, feed, runtimeVersion, image);

        // The store is shared with the deployed listener: seeding here and the listener's checkpoint saves reach the same
        // Azure Storage account. PrepareAsync is idempotent (the listener already provisioned it). The store needs no disposal.
        await AzureStorageWorkflowStateStore.PrepareAsync(storageConnection);
        AzureStorageWorkflowStateStore store = await AzureStorageWorkflowStateStore.ConnectAsync(storageConnection);
        WorkflowRunId runId = await ServerlessLiveExecutionSupport.SeedPendingRunAsync(store);
        string checkpointToken = ServerlessLiveExecutionSupport.IssueCheckpointToken(checkpointSecret, runId);

        // LocalStack 4.9.2 with prebuilt images (ADR 0060). The spawned Lambda container reaches the public listener over
        // its normal outbound internet route, so no host-gateway flag is needed (unlike the local-Kestrel LocalStack gate).
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

            // Deploy the real binary, baking the echo source's base URL (the deployed listener) into the transport binder.
            var deployer = new LambdaServerlessDeployer(client, new LambdaDeployerOptions
            {
                ExecutionRoleArn = "arn:aws:iam::000000000000:role/lambda-role",
                FunctionEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ARAZZO_SOURCE__echo"] = sourceBaseUrl,
                },
            });
            ServerlessDeployResult deploy = await deployer.DeployAsync(
                new ServerlessDeployRequest("serverless-check", 1, "isolated", "linux-x64", nativeBinary),
                default);
            deploy.Succeeded.ShouldBeTrue(deploy.Log);

            ListFunctionsResponse functions = await client.ListFunctionsAsync(new ListFunctionsRequest());
            string functionName = functions.Functions.ShouldHaveSingleItem().FunctionName;

            // Dispatch the run with the run-scoped token: the Lambda loads/saves its checkpoint through the public listener
            // (authenticated by the token) and calls the echo source, then returns its outcome document verbatim.
            string invocation = $$"""{"runId":"{{runId.Value}}","environment":"isolated","checkpointUrl":"{{checkpointBaseUrl}}","checkpointToken":"{{checkpointToken}}"}""";
            InvokeResponse invoke = await client.InvokeAsync(new InvokeRequest
            {
                FunctionName = functionName,
                InvocationType = InvocationType.RequestResponse,
                Payload = invocation,
            });

            string payload = invoke.Payload is null ? string.Empty : Encoding.UTF8.GetString(invoke.Payload.ToArray());
            invoke.FunctionError.ShouldBeNullOrEmpty($"the deployed run faulted under LocalStack instead of completing; payload: {payload}");
            payload.ShouldContain("Completed", customMessage: $"the run did not report Completed; payload: {payload}");
        }
        finally
        {
            await localstack.DisposeAsync();
        }

        // The durable proof, read back from the shared store the listener terminated the checkpoint into.
        await ServerlessLiveExecutionSupport.AssertRunCompletedWithEchoAsync(store, runId);
    }
}