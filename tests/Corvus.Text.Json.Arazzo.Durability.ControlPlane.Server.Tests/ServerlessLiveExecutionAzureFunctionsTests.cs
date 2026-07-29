// <copyright file="ServerlessLiveExecutionAzureFunctionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.IO.Compression;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The Azure Functions counterpart to <see cref="ServerlessLiveExecutionLocalStackTests"/>: the automated proof that a
/// version's real ReadyToRun isolated-worker app <b>executes under a live Azure Functions host</b> (ADR 0055/0061). It
/// compiles the shared workflow's executor into a framework-dependent ReadyToRun isolated-worker app in the build
/// container, drops the published app into the real <c>dotnet-isolated</c> Functions runtime image (there is no Azure
/// management-plane emulator, so the gate proves <em>execution</em>, not the deploy, ADR 0061), and dispatches a seeded
/// run to it. The run advances to <c>Completed</c> by loading its checkpoint over this test's checkpoint surface, calling
/// its <c>echo</c> source, and saving the advanced checkpoint back — the same run-to-completion the Lambda gate proves,
/// on the other vendor's real runtime.
/// </summary>
/// <remarks>
/// This is opt-in (<c>[TestCategory("integration")][TestCategory("docker")]</c>): it needs a container runtime, the
/// <c>arazzo-aot-builder</c> image, the local package feed the runtime graph restores from, and the Azure Functions
/// <c>dotnet-isolated</c> image. It skips unless <c>ARAZZO_AOT_LOCAL_FEED</c> and <c>ARAZZO_AOT_RUNTIME_VERSION</c> are
/// set. The isolated worker reaches this test's Kestrel checkpoint host (and its <c>echo</c> source) through
/// <c>host.containers.internal</c>, routed by the <c>--add-host …:host-gateway</c> the container is given.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class ServerlessLiveExecutionAzureFunctionsTests
{
    // The Azure Functions .NET-10 isolated-worker runtime image (the same one the design's feasibility spike ran the app
    // under). Overridable so CI can pin a digest.
    private const string DefaultFunctionsImage = "mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0";

    [TestMethod]
    public async Task The_published_isolated_worker_runs_a_seeded_run_to_completion_over_the_checkpoint_surface()
    {
        string? feed = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_LOCAL_FEED");
        string? runtimeVersion = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_RUNTIME_VERSION");
        if (string.IsNullOrEmpty(feed) || string.IsNullOrEmpty(runtimeVersion))
        {
            Assert.Inconclusive("Set ARAZZO_AOT_LOCAL_FEED (the local package feed path) and ARAZZO_AOT_RUNTIME_VERSION (its Corvus runtime version), and build the arazzo-aot-builder image, to run this proof.");
            return;
        }

        string builderImage = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_IMAGE") ?? "arazzo-aot-builder:net10";
        string functionsImage = System.Environment.GetEnvironmentVariable("ARAZZO_AZURE_FUNCTIONS_IMAGE") ?? DefaultFunctionsImage;

        // 1. Host the runner's real checkpoint surface + the workflow's 'echo' source on Kestrel, bound to 0.0.0.0 on a
        // free port. The isolated worker reaches BOTH at host.containers.internal:<port>: its checkpoint callback loads
        // and saves the run's checkpoint here, and its one source call hits /demo/echo. The store is in-memory and shared
        // between the seed below and MapWorkflowCheckpointEndpoints, so the checkpoint the worker writes back lands where
        // the assertions read it.
        int hostPort = ServerlessLiveExecutionSupport.FreeTcpPort();
        var store = new InMemoryWorkflowStateStore();
        WebApplication host = WebApplication.CreateSlimBuilder().Build();
        host.Urls.Add($"http://0.0.0.0:{hostPort}");
        host.MapWorkflowCheckpointEndpoints(store, requireAuthorization: false);
        host.MapGet("/demo/echo", () => Results.Json(new { status = "ok" }));
        await host.StartAsync();

        string? appDirectory = null;
        try
        {
            // 2. Seed a Pending run in that store exactly as the control plane's start path does (CreateNew + Enqueue,
            // which persists it Pending at cursor 0). The workflowId must match the baked workflow's — the worker's
            // BakedHostedWorkflowResolver rejects a mismatch — so it is the serverless-check workflow this test compiles.
            var runId = new WorkflowRunId(Guid.NewGuid().ToString("n"));
            using (ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray()))
            using (WorkflowRun seed = WorkflowRun.CreateNew(store, runId, "serverless-check", inputs.RootElement, "isolated"))
            {
                await seed.EnqueueAsync(default);
            }

            // 3. Compile the shared workflow's real executor to a framework-dependent ReadyToRun isolated-worker app in the
            // build container (the same build catalog-add + the build worker drive), and unpack the published app so it can
            // be dropped into the Functions runtime image's wwwroot.
            byte[] appZip = await ServerlessLiveExecutionSupport.BuildDeployArtifactAsync(ServerlessTarget.AzureFunctions, feed, runtimeVersion, builderImage);
            appDirectory = Directory.CreateTempSubdirectory("arazzo-azure-app-").FullName;
            using (var archive = new ZipArchive(new MemoryStream(appZip), ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(appDirectory);
            }

            // 4. Run the real dotnet-isolated Functions host over the published app mounted at wwwroot. HTTP-only needs no
            // Azure Storage (empty AzureWebJobsStorage + file secret storage). The 'echo' source's base URL is baked into
            // the worker's transport binder exactly as the production deployer sets it (ARAZZO_SOURCE__echo), and the
            // container is given a route to host.containers.internal so the worker reaches this test's Kestrel host.
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
                // 5. Dispatch the run: the real invocation the runner's ServerlessRunExecutionBackend builds — the run id,
                // its environment, and the checkpoint base URL the worker calls back to (trailing slash so the
                // function-side store's relative 'runs/{id}/checkpoint' resolves against it). The [Function("invoke")]
                // returns the outcome document verbatim, so a full advance responds 200 with a Completed outcome.
                string checkpointBaseUrl = $"http://host.containers.internal:{hostPort}/";
                string invocation = $$"""{"runId":"{{runId.Value}}","environment":"isolated","checkpointUrl":"{{checkpointBaseUrl}}"}""";
                var invokeUri = new UriBuilder(Uri.UriSchemeHttp, functions.Hostname, functions.GetMappedPublicPort(80), "/api/invoke").Uri;

                // The isolated worker warms after the container is up; dispatch is at-least-once, and re-invoking resumes
                // from the last saved checkpoint (a completed run replies Completed), so retrying the real invocation until
                // it returns 200 is both a robust readiness gate and faithful to how the runner dispatches.
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

                // The handler returns its outcome document verbatim (ServerlessInvocationHandler): a full advance reports
                // Completed. This is the worker's own view; the store assertion below is the durable, independent proof.
                payload.ShouldContain("Completed", customMessage: $"the run did not report Completed; payload: {payload}");
            }
            finally
            {
                await functions.DisposeAsync();
            }

            // 6. The durable proof: the seeded run, reloaded from the store the worker checkpointed back into, is Completed,
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
            if (appDirectory is not null)
            {
                try
                {
                    Directory.Delete(appDirectory, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A best-effort cleanup of the unpacked app; a leftover temp directory is not worth failing the test.
                }
            }
        }
    }
}