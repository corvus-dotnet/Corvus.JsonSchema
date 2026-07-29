// <copyright file="ServerlessLiveExecutionSupport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Generation;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The shared fixture for the serverless live-execution gates (ADR 0055/0060/0061): the one source-calling workflow both
/// the AWS Lambda and Azure Functions proofs compile, the deploy-artifact build that assembles and container-compiles it
/// for a target, and a free-port helper for the checkpoint host. Holding the workflow as a single constant is what keeps
/// the two gates proving the <em>identical</em> workflow, so they cannot silently drift apart.
/// </summary>
internal static class ServerlessLiveExecutionSupport
{
    // The Azure Functions .NET-10 isolated-worker runtime image the execution and deploy gates run the published app
    // under (the same one the design's feasibility spike used). Overridable so CI can pin a digest.
    internal const string DefaultFunctionsImage = "mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0";

    // A minimal source-calling workflow: one GET on the "echo" source. The same shape the demo deploys, so the compiled
    // artifact carries genuine workflow, transport-binding, and JSON logic rather than a trivial stub.
    internal const string WorkflowJson = """
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

    internal const string EchoOpenApi = """
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

    // A free ephemeral TCP port for the Kestrel checkpoint host. Bind-to-0 then release; the small reuse window is
    // acceptable for an opt-in integration test that immediately binds the port on 0.0.0.0.
    internal static int FreeTcpPort()
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

    /// <summary>
    /// Compiles the shared workflow's real executor and assembles then container-builds the deploy artifact for a target:
    /// the self-contained native <c>bootstrap</c> for AWS Lambda, or the zipped framework-dependent ReadyToRun
    /// isolated-worker app for Azure Functions. This is the exact build path catalog-add and the build worker drive.
    /// </summary>
    /// <param name="target">The serverless platform to assemble and compile for.</param>
    /// <param name="feed">The host path of the local package feed the runtime graph restores from.</param>
    /// <param name="runtimeVersion">The Corvus runtime package version that matches the executor's engine version.</param>
    /// <param name="image">The build container image tag.</param>
    /// <returns>The deploy artifact bytes: a native ELF for Lambda, or the zipped published app for Azure.</returns>
    internal static async Task<byte[]> BuildDeployArtifactAsync(ServerlessTarget target, string feed, string runtimeVersion, string image)
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
                Target = target,
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
        result.Succeeded.ShouldBeTrue($"the {target} deploy-artifact build failed. Log:\n{result.Log}");
        return result.NativeBinary.ToArray();
    }

    // The Azure Functions runtime image to run the published app under, overridable via ARAZZO_AZURE_FUNCTIONS_IMAGE.
    internal static string FunctionsImage()
        => System.Environment.GetEnvironmentVariable("ARAZZO_AZURE_FUNCTIONS_IMAGE") ?? DefaultFunctionsImage;

    /// <summary>
    /// Runs a published isolated-worker app directory under the real Azure Functions host container and drives a seeded
    /// Pending run of the shared <c>serverless-check</c> workflow to completion over the runner's checkpoint surface.
    /// The execution gate (from a freshly assembled build) and the deploy gate (from a package pulled back out of blob
    /// storage) share this, so both prove the identical run over the same runtime. It asserts the whole advance: the
    /// isolated worker returns a success response with a <c>Completed</c> outcome, and the seeded run — reloaded from the
    /// store the worker checkpointed back into — is <c>Completed</c> with the <c>callEcho</c> output <c>{ "status": "ok" }</c>.
    /// </summary>
    /// <param name="appDirectory">The published isolated-worker app directory to mount at the container's <c>/home/site/wwwroot</c>.</param>
    /// <param name="functionsImage">The Azure Functions runtime image to run it under.</param>
    /// <returns>A task that completes when the run has reached and been verified <c>Completed</c>.</returns>
    internal static async Task RunSeededRunToCompletionUnderFunctionsHostAsync(string appDirectory, string functionsImage)
    {
        // Host the runner's real checkpoint surface + the workflow's 'echo' source on Kestrel, bound to 0.0.0.0 on a free
        // port. The isolated worker reaches BOTH at host.containers.internal:<port>: its checkpoint callback loads and
        // saves the run's checkpoint here, and its one source call hits /demo/echo. The store is in-memory and shared
        // between the seed and MapWorkflowCheckpointEndpoints, so the checkpoint the worker writes back lands where the
        // assertions read it.
        int hostPort = FreeTcpPort();
        var store = new InMemoryWorkflowStateStore();
        WebApplication host = WebApplication.CreateSlimBuilder().Build();
        host.Urls.Add($"http://0.0.0.0:{hostPort}");
        host.MapWorkflowCheckpointEndpoints(store, requireAuthorization: false);
        host.MapGet("/demo/echo", () => Results.Json(new { status = "ok" }));
        await host.StartAsync();
        try
        {
            // Seed a Pending run exactly as the control plane's start path does (CreateNew + Enqueue, which persists it
            // Pending at cursor 0). The workflowId must match the baked workflow's — the worker's BakedHostedWorkflowResolver
            // rejects a mismatch — so it is the serverless-check workflow the app was compiled from.
            var runId = new WorkflowRunId(Guid.NewGuid().ToString("n"));
            using (ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray()))
            using (WorkflowRun seed = WorkflowRun.CreateNew(store, runId, "serverless-check", inputs.RootElement, "isolated"))
            {
                await seed.EnqueueAsync(default);
            }

            // Run the real dotnet-isolated Functions host over the published app mounted at wwwroot. HTTP-only needs no
            // Azure Storage. The 'echo' source's base URL is baked into the worker's transport binder exactly as the
            // production deployer sets it (ARAZZO_SOURCE__echo), and the container is given a route to
            // host.containers.internal so the worker reaches this host.
            string sourceBaseUrl = $"http://host.containers.internal:{hostPort}";
            IContainer functions = new ContainerBuilder()
                .WithImage(functionsImage)
                .WithBindMount(appDirectory, "/home/site/wwwroot")
                .WithEnvironment("AzureWebJobsStorage", string.Empty)
                .WithEnvironment("AzureWebJobsSecretStorageType", "files")
                .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
                .WithEnvironment("ARAZZO_SOURCE__echo", sourceBaseUrl)
                .WithExtraHost("host.containers.internal", "host-gateway")
                .WithPortBinding(80, assignRandomHostPort: true)
                .Build();
            await functions.StartAsync();
            try
            {
                // Dispatch the run: the real invocation the runner's ServerlessRunExecutionBackend builds — the run id, its
                // environment, and the checkpoint base URL the worker calls back to (trailing slash so the function-side
                // store's relative 'runs/{id}/checkpoint' resolves against it). The [Function("invoke")] returns the outcome
                // document verbatim, so a full advance responds with a Completed outcome.
                string checkpointBaseUrl = $"http://host.containers.internal:{hostPort}/";
                string invocation = $$"""{"runId":"{{runId.Value}}","environment":"isolated","checkpointUrl":"{{checkpointBaseUrl}}"}""";
                var invokeUri = new UriBuilder(Uri.UriSchemeHttp, functions.Hostname, functions.GetMappedPublicPort(80), "/api/invoke").Uri;

                // The isolated worker warms after the container is up; dispatch is at-least-once, and re-invoking resumes
                // from the last saved checkpoint (a completed run replies Completed), so retrying the real invocation until
                // it returns a success response is both a robust readiness gate and faithful to how the runner dispatches.
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
                string payload = string.Empty;
                bool responded = false;
                var budget = System.Diagnostics.Stopwatch.StartNew();
                while (budget.Elapsed < TimeSpan.FromMinutes(3))
                {
                    try
                    {
                        using HttpResponseMessage response = await httpClient.PostAsync(
                            invokeUri, new StringContent(invocation, Encoding.UTF8, "application/json"));
                        if (response.IsSuccessStatusCode)
                        {
                            payload = await response.Content.ReadAsStringAsync();
                            responded = true;
                            break;
                        }
                    }
                    catch (HttpRequestException)
                    {
                        // The isolated worker is still warming and not yet accepting invocations; retry.
                    }

                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                responded.ShouldBeTrue("the isolated worker never returned a success response for the invocation within the warm-up budget.");
                payload.ShouldContain("Completed", customMessage: $"the run did not report Completed; payload: {payload}");
            }
            finally
            {
                await functions.DisposeAsync();
            }

            // The durable proof: the seeded run, reloaded from the store the worker checkpointed back into, is Completed,
            // and its one step read the echo source's body — callEcho's output is { "status": "ok" }.
            using WorkflowRun? finished = await WorkflowRun.ResumeAsync(store, runId);
            finished.ShouldNotBeNull("the run's checkpoint is missing — the worker never saved its advance back to this host.");
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

    /// <summary>
    /// Runs a published isolated-worker app under the real Azure Functions host container, pointed at a <b>publicly
    /// reachable, token-authenticated checkpoint listener</b> (ADR 0062) and its <c>echo</c> source, and drives a seeded
    /// Pending run of the shared <c>serverless-check</c> workflow to completion through it. Unlike
    /// <see cref="RunSeededRunToCompletionUnderFunctionsHostAsync"/> — which hosts an in-process Kestrel checkpoint surface
    /// over an in-memory store — the checkpoint surface and store here are the real deployed listener and its backing Azure
    /// Storage account, and the callback is authenticated by a run-scoped bearer token the runner mints. The function
    /// process runs locally (the real Functions runtime image), so this gates the token-authenticated cloud checkpoint
    /// round-trip and the shared-store advance in CI without provisioning a Function App; the deployed-Flex counterpart
    /// proves the same run with the function on real Flex Consumption.
    /// </summary>
    /// <param name="appDirectory">The published isolated-worker app directory to mount at the container's <c>/home/site/wwwroot</c>.</param>
    /// <param name="functionsImage">The Azure Functions runtime image to run it under.</param>
    /// <param name="store">The shared Azure Storage run store the listener terminates into — seeded here and read back here.</param>
    /// <param name="listenerBaseUrl">The deployed listener's public base URL (its checkpoint surface and <c>echo</c> source).</param>
    /// <param name="checkpointSecret">The shared checkpoint secret the runner mints the run-scoped token with and the listener validates with.</param>
    /// <returns>A task that completes when the run has reached and been verified <c>Completed</c> in the shared store.</returns>
    internal static async Task RunSeededRunToCompletionAgainstListenerUnderLocalFunctionsHostAsync(
        string appDirectory, string functionsImage, IWorkflowStateStore store, string listenerBaseUrl, ReadOnlyMemory<byte> checkpointSecret)
    {
        // The source base URL is the listener root (no trailing slash; the echo op's '/demo/echo' path is appended); the
        // checkpoint base URL adds a trailing slash so the function-side store's relative 'runs/{id}/checkpoint' resolves.
        string sourceBaseUrl = listenerBaseUrl.TrimEnd('/');

        WorkflowRunId runId = await SeedPendingRunAsync(store);
        string checkpointToken = IssueCheckpointToken(checkpointSecret.Span, runId);

        IContainer functions = new ContainerBuilder()
            .WithImage(functionsImage)
            .WithBindMount(appDirectory, "/home/site/wwwroot")
            .WithEnvironment("AzureWebJobsStorage", string.Empty)
            .WithEnvironment("AzureWebJobsSecretStorageType", "files")
            .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
            .WithEnvironment("ARAZZO_SOURCE__echo", sourceBaseUrl)
            .WithPortBinding(80, assignRandomHostPort: true)
            .Build();
        await functions.StartAsync();
        try
        {
            var invokeUri = new UriBuilder(Uri.UriSchemeHttp, functions.Hostname, functions.GetMappedPublicPort(80), "/api/invoke").Uri;
            try
            {
                await InvokeUntilCompletedAsync(invokeUri, runId, sourceBaseUrl, checkpointToken, TimeSpan.FromMinutes(3));
            }
            catch
            {
                // On failure, surface the isolated worker's own logs — the checkpoint/echo call error that faulted the
                // advance is only visible inside the function, not in the runner's non-2xx retry loop.
                (string workerStdout, string workerStderr) = await functions.GetLogsAsync();
                Console.WriteLine($"=== Functions worker STDOUT ===\n{workerStdout}\n=== Functions worker STDERR ===\n{workerStderr}");
                throw;
            }
        }
        finally
        {
            await functions.DisposeAsync();
        }

        await AssertRunCompletedWithEchoAsync(store, runId);
    }

    /// <summary>
    /// Seeds a Pending run of the shared <c>serverless-check</c> workflow into <paramref name="store"/> exactly as the
    /// control plane's start path does (<c>CreateNew</c> + <c>Enqueue</c>, which persists it Pending at cursor 0), and
    /// returns its id. The workflowId must match the baked workflow's, so it is <c>serverless-check</c>.
    /// </summary>
    /// <param name="store">The store to seed the run into.</param>
    /// <returns>The seeded run's id.</returns>
    internal static async Task<WorkflowRunId> SeedPendingRunAsync(IWorkflowStateStore store)
    {
        var runId = new WorkflowRunId(Guid.NewGuid().ToString("n"));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        using WorkflowRun seed = WorkflowRun.CreateNew(store, runId, "serverless-check", inputs.RootElement, "isolated");
        await seed.EnqueueAsync(default);
        return runId;
    }

    /// <summary>Mints a run-scoped checkpoint token (ADR 0062) valid for a window that comfortably outlives one invocation.</summary>
    /// <param name="secret">The shared checkpoint secret.</param>
    /// <param name="runId">The run the token authorises checkpoints for.</param>
    /// <returns>The bearer token string.</returns>
    internal static string IssueCheckpointToken(ReadOnlySpan<byte> secret, WorkflowRunId runId)
        => CheckpointToken.Issue(secret, runId.Value, DateTimeOffset.UtcNow.AddMinutes(30));

    /// <summary>
    /// Dispatches the real runner invocation to a function's invoke URL — the run id, its environment, the checkpoint base
    /// URL the function calls back to (trailing slash added), and the run-scoped checkpoint token — retrying until the
    /// function returns a success response carrying a <c>Completed</c> outcome or the budget elapses. Re-invoking is safe:
    /// a completed run replies <c>Completed</c>, so retrying is both a readiness gate and faithful to at-least-once dispatch.
    /// </summary>
    /// <param name="invokeUri">The function's HTTP-trigger invoke URL.</param>
    /// <param name="runId">The seeded run to advance.</param>
    /// <param name="checkpointBaseUrl">The checkpoint surface base URL the function calls back to.</param>
    /// <param name="checkpointToken">The run-scoped bearer token the function presents on each checkpoint call.</param>
    /// <param name="budget">How long to keep retrying before failing.</param>
    /// <returns>A task that completes when a <c>Completed</c> outcome is observed.</returns>
    internal static async Task InvokeUntilCompletedAsync(Uri invokeUri, WorkflowRunId runId, string checkpointBaseUrl, string checkpointToken, TimeSpan budget)
    {
        string baseUrl = checkpointBaseUrl.EndsWith('/') ? checkpointBaseUrl : checkpointBaseUrl + "/";
        string invocation = $$"""{"runId":"{{runId.Value}}","environment":"isolated","checkpointUrl":"{{baseUrl}}","checkpointToken":"{{checkpointToken}}"}""";
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        string payload = string.Empty;
        bool responded = false;
        var budgetTimer = System.Diagnostics.Stopwatch.StartNew();
        while (budgetTimer.Elapsed < budget)
        {
            try
            {
                using HttpResponseMessage response = await httpClient.PostAsync(
                    invokeUri, new StringContent(invocation, Encoding.UTF8, "application/json"));
                if (response.IsSuccessStatusCode)
                {
                    payload = await response.Content.ReadAsStringAsync();
                    responded = true;
                    break;
                }
            }
            catch (HttpRequestException)
            {
                // The function is still warming and not yet accepting invocations; retry.
            }
            catch (TaskCanceledException)
            {
                // A request timed out during a slow cold start; retry.
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        responded.ShouldBeTrue("the function never returned a success response for the invocation within the budget.");
        payload.ShouldContain("Completed", customMessage: $"the run did not report Completed; payload: {payload}");
    }

    /// <summary>
    /// Reloads a run from the store the function checkpointed back into and asserts the durable proof: it is
    /// <c>Completed</c>, and its one step read the echo source's body — <c>callEcho</c>'s output is <c>{ "status": "ok" }</c>.
    /// </summary>
    /// <param name="store">The shared store the checkpoint landed in.</param>
    /// <param name="runId">The run to reload and verify.</param>
    /// <returns>A task that completes when the run has been verified <c>Completed</c>.</returns>
    internal static async Task AssertRunCompletedWithEchoAsync(IWorkflowStateStore store, WorkflowRunId runId)
    {
        using WorkflowRun? finished = await WorkflowRun.ResumeAsync(store, runId);
        finished.ShouldNotBeNull("the run's checkpoint is missing — the function never saved its advance back through the listener.");
        finished!.Status.ShouldBe(WorkflowRunStatus.Completed);
        finished.TryGetStepOutputs("callEcho", out JsonElement callEchoOutputs).ShouldBeTrue("the callEcho step recorded no outputs.");
        callEchoOutputs.TryGetProperty("status"u8, out JsonElement status).ShouldBeTrue("callEcho's outputs carry no 'status'.");
        status.GetString().ShouldBe("ok");
    }
}