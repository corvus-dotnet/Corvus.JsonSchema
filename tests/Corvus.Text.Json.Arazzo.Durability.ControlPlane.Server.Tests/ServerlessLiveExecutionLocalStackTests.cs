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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
/// handler). Its companion, <see cref="The_deployed_native_bootstrap_runs_a_seeded_run_to_completion_over_the_checkpoint_surface"/>,
/// proves the whole run: a Pending run seeded in a store this test hosts, dispatched to the function with a real
/// <c>{ runId, environment, checkpointUrl }</c>, advances to <c>Completed</c> by loading its checkpoint, calling its
/// source, and saving the advanced checkpoint — all over the runner's real checkpoint surface (ADR 0055).
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

    [TestMethod]
    public async Task The_deployed_native_bootstrap_runs_a_seeded_run_to_completion_over_the_checkpoint_surface()
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

        // 1. Host the runner's real checkpoint surface + the workflow's 'echo' source on Kestrel, bound to 0.0.0.0 on a
        // free port. The deployed function reaches BOTH at host.containers.internal:<port> (the same hop the demo uses):
        // its checkpoint callback loads and saves the run's checkpoint here, and its one source call hits /demo/echo. The
        // store is in-memory and shared between the seed below and MapWorkflowCheckpointEndpoints, so the checkpoint the
        // function writes back lands where the assertions read it.
        int hostPort = FreeTcpPort();
        var store = new InMemoryWorkflowStateStore();
        WebApplication host = WebApplication.CreateSlimBuilder().Build();
        host.Urls.Add($"http://0.0.0.0:{hostPort}");
        host.MapWorkflowCheckpointEndpoints(store, requireAuthorization: false);
        host.MapGet("/demo/echo", () => Results.Json(new { status = "ok" }));
        await host.StartAsync();
        try
        {
            // 2. Seed a Pending run in that store exactly as the control plane's start path does (CreateNew + Enqueue,
            // which persists it Pending at cursor 0). The workflowId must match the baked workflow's — the function's
            // BakedHostedWorkflowResolver rejects a mismatch — so it is the serverless-check workflow this test compiles.
            var runId = new WorkflowRunId(Guid.NewGuid().ToString("n"));
            using (ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray()))
            using (WorkflowRun seed = WorkflowRun.CreateNew(store, runId, "serverless-check", inputs.RootElement, "isolated"))
            {
                await seed.EnqueueAsync(default);
            }

            // 3. Compile the real executor to a native binary (the same build the execute-under-LocalStack proof drives).
            byte[] nativeBinary = await BuildNativeBootstrapAsync(feed, runtimeVersion, image);

            // 4. LocalStack 4.9.2 with prebuilt images (ADR 0060), plus the docker flag that gives each spawned Lambda
            // container a route to host.containers.internal — the host gateway it reaches this test's Kestrel host through.
            LocalStackContainer localstack = new LocalStackBuilder()
                .WithImage("localstack/localstack:4.9.2")
                .WithBindMount(dockerSock, "/var/run/docker.sock")
                .WithEnvironment("DOCKER_HOST", "unix:///var/run/docker.sock")
                .WithEnvironment("LAMBDA_PREBUILD_IMAGES", "1")
                .WithEnvironment("LAMBDA_RUNTIME_ENVIRONMENT_TIMEOUT", "90")
                .WithEnvironment("LAMBDA_DOCKER_FLAGS", "--add-host=host.containers.internal:host-gateway")
                .Build();
            await localstack.StartAsync();
            try
            {
                using var client = new AmazonLambdaClient(
                    new BasicAWSCredentials("test", "test"),
                    new AmazonLambdaConfig { ServiceURL = localstack.GetConnectionString(), AuthenticationRegion = "us-east-1" });

                // 5. Deploy the real binary, baking the 'echo' source's base URL into the function's transport binder
                // (ARAZZO_SOURCE__echo, the same env the production deployer sets from an environment's source registry).
                // The function appends the operation path /demo/echo to it, reaching this host's echo endpoint.
                string sourceBaseUrl = $"http://host.containers.internal:{hostPort}";
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

                // 6. Dispatch the run: the real invocation the runner's ServerlessRunExecutionBackend builds — the run id,
                // its environment, and the checkpoint base URL the function calls back to (trailing slash so the
                // function-side store's relative 'runs/{id}/checkpoint' resolves against it).
                string checkpointBaseUrl = $"http://host.containers.internal:{hostPort}/";
                string invocation = $$"""{"runId":"{{runId.Value}}","environment":"isolated","checkpointUrl":"{{checkpointBaseUrl}}"}""";
                InvokeResponse invoke = await client.InvokeAsync(new InvokeRequest
                {
                    FunctionName = functionName,
                    InvocationType = InvocationType.RequestResponse,
                    Payload = invocation,
                });

                string payload = invoke.Payload is null ? string.Empty : Encoding.UTF8.GetString(invoke.Payload.ToArray());
                invoke.FunctionError.ShouldBeNullOrEmpty($"the deployed run faulted under LocalStack instead of completing; payload: {payload}");

                // The handler returns its outcome document verbatim (ServerlessInvocationHandler): a full advance reports
                // Completed. This is the function's own view; the store assertion below is the durable, independent proof.
                payload.ShouldContain("Completed", customMessage: $"the run did not report Completed; payload: {payload}");
            }
            finally
            {
                await localstack.DisposeAsync();
            }

            // 7. The durable proof: the seeded run, reloaded from the store the function checkpointed back into, is
            // Completed, and its one step read the echo source's body — callEcho's output is { "status": "ok" }.
            using WorkflowRun? finished = await WorkflowRun.ResumeAsync(store, runId);
            finished.ShouldNotBeNull("the run's checkpoint is missing — the function never saved its advance back to this host.");
            finished!.Status.ShouldBe(WorkflowRunStatus.Completed);
            finished.TryGetStepOutputs("callEcho", out JsonElement callEchoOutputs).ShouldBeTrue("the callEcho step recorded no outputs.");
            callEchoOutputs.TryGetProperty("status"u8, out JsonElement status).ShouldBeTrue("callEcho's outputs carry no 'status'.");
            status.GetString().ShouldBe("ok");
        }
        finally
        {
            await host.StopAsync();
            await host.DisposeAsync();
        }
    }

    // A free ephemeral TCP port for the Kestrel checkpoint host. Bind-to-0 then release; the small reuse window is
    // acceptable for an opt-in integration test that immediately binds the port on 0.0.0.0.
    private static int FreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
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