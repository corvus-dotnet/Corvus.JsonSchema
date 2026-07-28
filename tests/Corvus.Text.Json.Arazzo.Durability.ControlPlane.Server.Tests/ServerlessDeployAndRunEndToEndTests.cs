// <copyright file="ServerlessDeployAndRunEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The end-to-end proof that the serverless deploy-and-run wiring composes (ADR 0059, sub-piece 5): over ONE set of shared
/// stores and a hosted control plane, a published version is built by the hosted build worker, that Ready build triggers a
/// deployment (deploy-on-build, 5b), the hosted deploy worker deploys it via an <see cref="IServerlessDeployer"/> and records
/// the function URL (5c), the catalog dispatch gate then admits an Isolated run (5a), and the production
/// <see cref="DeployedFunctionUrlResolver"/> resolves that run to the deployed function which the
/// <see cref="ServerlessRunExecutionBackend"/> invokes (5d) — advancing the run through a stub function endpoint. A fake
/// deployer and a stub function keep the loop deterministic; the real AWS Lambda deploy path is proven separately by the
/// LocalStack integration test (ADR 0060).
/// </summary>
[TestClass]
public sealed class ServerlessDeployAndRunEndToEndTests
{
    // A fake ELF the fake AOT builder returns; the loop signs, attaches, persists, and (deploy-side) verifies it.
    private static readonly byte[] FakeNativeBinary = [0x7F, (byte)'E', (byte)'L', (byte)'F', 1, 2, 3, 4];

    [TestMethod]
    public async Task The_full_loop_builds_deploys_admits_the_run_and_invokes_the_function()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new EcdsaExecutorPackageSigner(key, "release-2026");
        IExecutorPackageVerifier verifier = TrustStore(("release-2026", key));

        // One set of stores shared by the REST surface, the hosted build worker, the hosted deploy worker, the dispatch
        // gate, and the URL resolver — exactly how a runner host wires them.
        var runStore = new InMemoryWorkflowStateStore();
        var catalogStore = new InMemoryWorkflowCatalogStore(
            timeProvider: null,
            metadataProvider: null,
            executorProvider: new WorkflowExecutorProvider(durable: true),
            signer: signer);
        var management = new SecuredWorkflowManagement(runStore, "ops");
        var catalog = new SecuredWorkflowCatalog(catalogStore, runStore, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());
        var buildStore = new InMemoryNativeBuildJobStore();
        var deploymentStore = new InMemoryWorkflowDeploymentStore();
        var environmentStore = new InMemoryEnvironmentStore();
        var availabilityStore = new InMemoryAvailabilityStore();
        var runnerRegistry = new InMemoryRunnerRegistry();
        var buildService = new WorkflowAotBuildService(verifier, signer, new FakeAotBuilder(), new AotHostAppOptions { RuntimePackageVersion = "5.0.0", FeedSources = [("local", "/tmp/feed")] });

        // The fake function platform: it records nothing but the URL the deploy worker will persist as the deployment's
        // invoke URL — a stub endpoint mapped on this same test server (below), reachable through the test client.
        var deployService = new WorkflowDeployService(verifier, new FakeServerlessDeployer("http://localhost/fake-function"));

        WebApplicationBuilder appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();
        appBuilder.Logging.ClearProviders();

        // Register the shared collaborators so both hosted workers resolve them from DI. Registering the deployment store is
        // what turns on deploy-on-build in the build worker (5b) and feeds the deploy worker (5c).
        appBuilder.Services.AddSingleton<INativeBuildJobStore>(buildStore);
        appBuilder.Services.AddSingleton<IWorkflowCatalogStore>(catalogStore);
        appBuilder.Services.AddSingleton<IWorkflowDeploymentStore>(deploymentStore);
        appBuilder.Services.AddSingleton(buildService);
        appBuilder.Services.AddSingleton(deployService);
        appBuilder.Services.AddNativeAotBuildWorker(new NativeBuildWorkerOptions { WorkerId = "e2e-build", PollInterval = TimeSpan.FromMilliseconds(50) });
        appBuilder.Services.AddWorkflowDeployWorker(new WorkflowDeployWorkerOptions { WorkerId = "e2e-deploy", PollInterval = TimeSpan.FromMilliseconds(50) });

        await using WebApplication app = appBuilder.Build();

        // The stub serverless function the deployed URL points at: it echoes a Completed outcome, standing in for the baked
        // ServerlessWorkflowRunHost the real deploy produces.
        app.MapPost("/fake-function", () => Results.Json(new { outcome = "Completed" }));
        app.MapArazzoControlPlane(management, catalog, runnerRegistry, ControlPlaneSecurityMode.Open, environmentStore: environmentStore, availabilityStore: availabilityStore, workflowDeploymentStore: deploymentStore, nativeBuildJobStore: buildStore);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        // An Isolated environment pinned to linux-x64 (its runtime target), the version, its availability there, and an
        // isolated runner that hosts it — the run-start gate's prerequisites besides the deployment.
        using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.Environments.Environment> env =
            ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.Environments.Environment>.Parse(
                """{"name":"production","requiredIsolation":"Isolated","runtimeIdentifier":"linux-x64"}"""u8.ToArray()))
        {
            (await environmentStore.AddAsync(env.RootElement, "ops", default)).Dispose();
        }

        await catalog.AddAsync(CatalogPackage.Build(Workflow(), []), new CatalogOwner("Team", "team@example.com", null, null), default, default, default);
        (await availabilityStore.MakeAvailableAsync("adopt", 1, "production", "ops", default)).Entry.Dispose();
        await runnerRegistry.RegisterAsync(Runner("adopt", 1, isolationModel: "Isolated"), default);

        // Enqueue the build over REST → 202. The hosted build worker builds+signs+persists, then (5b) enqueues the deployment;
        // the hosted deploy worker deploys it and records the function URL.
        (await client.PostAsync("/catalog/adopt/versions/1/nativeBuilds", Json("""{"environment":"production","runtimeIdentifier":"linux-x64"}""")))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Poll the deployment (via the shared store) until it reaches Deployed — proving build → deploy-on-build → deploy all
        // ran back-to-back over the shared stores.
        await PollUntilDeployedAsync(deploymentStore, "adopt", 1, "production", "linux-x64", TimeSpan.FromSeconds(30));
        using (ParsedJsonDocument<WorkflowDeployment>? deployment = await deploymentStore.GetAsync(WorkflowDeployment.DeriveId("adopt", 1, "production", "linux-x64"), default))
        {
            deployment!.RootElement.StatusValue.ShouldBe("Deployed");
            deployment.RootElement.FunctionUrlOrNull.ShouldBe("http://localhost/fake-function");
        }

        // 5a: the dispatch gate now admits the Isolated run (it reads the shared deployment store and sees it Deployed).
        HttpResponseMessage started = await client.PostAsync("/catalog/adopt/versions/1/runs?environment=production", Json("""{}"""));
        started.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // 5d: the production resolver maps a run of this version-in-environment to the deployed function URL from the shared
        // store, and the serverless backend invokes it — the stub returns Completed, so the advance completes.
        var backend = new ServerlessRunExecutionBackend(client, DeployedFunctionUrlResolver.ForStore(deploymentStore, environmentStore), new Uri("http://localhost/checkpoint/"));
        using WorkflowRun run = WorkflowRun.CreateNew(new InMemoryWorkflowStateStore(), "run-e2e", "adopt-v1", default, "production");
        WorkflowRunResultKind kind = await backend.AdvanceAsync(run, default);
        kind.ShouldBe(WorkflowRunResultKind.Completed);
    }

    private static async Task PollUntilDeployedAsync(IWorkflowDeploymentStore deployments, string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await deployments.IsDeployedAsync(baseWorkflowId, versionNumber, environment, runtimeIdentifier, default))
            {
                return;
            }

            await Task.Delay(50);
        }

        // Surface the deployment's terminal reason on timeout so a failed deploy is diagnosable, not just "timed out".
        using ParsedJsonDocument<WorkflowDeployment>? deployment = await deployments.GetAsync(WorkflowDeployment.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier), default);
        throw new TimeoutException($"The deployment did not reach Deployed within {timeout} (status: {deployment?.RootElement.StatusValue ?? "absent"}, reason: {deployment?.RootElement.FailureReasonOrNull ?? "none"}).");
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static TrustStoreExecutorPackageVerifier TrustStore(params (string KeyId, ECDsa SigningKey)[] keys)
    {
        var trusted = new Dictionary<string, AsymmetricAlgorithm>(StringComparer.Ordinal);
        foreach ((string keyId, ECDsa signingKey) in keys)
        {
            var publicKey = ECDsa.Create();
            publicKey.ImportParameters(signingKey.ExportParameters(includePrivateParameters: false));
            trusted[keyId] = publicKey;
        }

        return new TrustStoreExecutorPackageVerifier(trusted);
    }

    // A trivial source-less durable workflow whose base id is "adopt": enough for the executor provider to compile a real,
    // signable executor that the deploy-side attestation check verifies.
    private static byte[] Workflow()
        => Encoding.UTF8.GetBytes("""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Adopt", "description": "A flow." },
          "sourceDescriptions": [],
          "workflows": [ { "workflowId": "adopt", "steps": [] } ]
        }
        """);

    private static RunnerRegistration Runner(string baseWorkflowId, int versionNumber, string runnerId = "runner-isolated", string environment = "production", string? isolationModel = null)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var w = new Stj.Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("runnerId", runnerId);
            w.WriteString("environment", environment);
            w.WriteString("startedAt", "2026-01-01T00:00:00.0000000+00:00");
            w.WriteString("lastSeenAt", "2026-01-01T00:00:00.0000000+00:00");
            w.WriteNumber("maxConcurrency", 4);
            w.WriteStartArray("transports");
            w.WriteStringValue("http");
            w.WriteEndArray();
            w.WriteStartArray("hostedVersions");
            w.WriteStartObject();
            w.WriteString("baseWorkflowId", baseWorkflowId);
            w.WriteNumber("versionNumber", versionNumber);
            w.WriteString("hash", "h");
            w.WriteBoolean("loaded", true);
            w.WriteEndObject();
            w.WriteEndArray();
            if (isolationModel is not null)
            {
                w.WriteString("isolationModel", isolationModel);
            }

            w.WriteEndObject();
        }

        return RunnerRegistration.FromJson(buffer.WrittenMemory);
    }

    private sealed class FakeAotBuilder : IWorkflowAotBuilder
    {
        public ValueTask<AotBuildResult> BuildAsync(AssembledHostApp hostApp, CancellationToken cancellationToken)
            => new(AotBuildResult.Success(FakeNativeBinary, "fake ilc ok"));
    }

    private sealed class FakeServerlessDeployer(string functionUrl) : IServerlessDeployer
    {
        public ValueTask<ServerlessDeployResult> DeployAsync(ServerlessDeployRequest request, CancellationToken cancellationToken)
            => new(ServerlessDeployResult.Success(functionUrl, "deployed (fake)"));
    }
}