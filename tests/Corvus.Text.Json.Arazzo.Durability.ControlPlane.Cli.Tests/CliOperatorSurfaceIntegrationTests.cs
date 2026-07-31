// <copyright file="CliOperatorSurfaceIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// End-to-end tests for the operator-surface branches (#876): <c>runners</c>, <c>builds</c>, and <c>deployments</c>,
/// driven in-process against a real Kestrel host. The surfaces are read-only on the control plane (the deploy runs on
/// the runner, ADR 0059), so the tests seed the registry / build-job / deployment stores directly and assert the CLI
/// reports the state.
/// </summary>
public sealed partial class CliIntegrationTests
{
    [TestMethod]
    public async Task Runners_list_reports_the_roster_with_isolation()
    {
        await using OperatorHost host = await StartOperatorHostAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await host.Registry.RegisterAsync(Registration("runner-a", "production", now, isolationModel: "Isolated"), default);
        await host.Registry.RegisterAsync(Registration("runner-b", "development", now), default);

        (int exit, string stdout, _) = await RunAsync(host, "runners", "list");

        exit.ShouldBe(0);
        stdout.ShouldContain("runner-a");
        stdout.ShouldContain("runner-b");
        stdout.ShouldContain("Isolated");
        stdout.ShouldContain("InProcess"); // the unstated advertisement renders as the ADR 0058 default
    }

    [TestMethod]
    public async Task Builds_list_and_get_report_a_versions_build_jobs()
    {
        await using OperatorHost host = await StartOperatorHostAsync();
        await host.SeedVersionAsync("checkout");
        using (ParsedJsonDocument<NativeBuildJob> draft = NativeBuildJob.Draft("checkout", 1, "production", "linux-x64", "nightly"))
        {
            (await host.Builds.EnqueueAsync(draft.RootElement, "seed", default)).Dispose();
        }

        (int listExit, string listJson, _) = await RunAsync(host, "builds", "list", "checkout", "1", "--output", "json");
        listExit.ShouldBe(0);
        listJson.ShouldContain("\"runtimeIdentifier\":\"linux-x64\"");
        listJson.ShouldContain("\"status\":\"Queued\"");
        listJson.ShouldContain("\"buildLabel\":\"nightly\"");

        (int getExit, string getJson, _) = await RunAsync(host, "builds", "get", "checkout", "1", "production", "linux-x64");
        getExit.ShouldBe(0);
        getJson.ShouldContain("\"status\":\"Queued\"");

        // A target with no build is a problem (404), not a success.
        (int missExit, _, string missErr) = await RunAsync(host, "builds", "get", "checkout", "1", "production", "linux-arm64");
        missExit.ShouldNotBe(0);
        missErr.ShouldContain("native-build-not-found");
    }

    [TestMethod]
    public async Task Deployments_list_and_get_report_deploy_state_and_function_url()
    {
        await using OperatorHost host = await StartOperatorHostAsync();
        await host.SeedVersionAsync("checkout");

        // One target reaches Deployed (carrying its function URL); a second stays Queued. Deploy the first fully before
        // seeding the second: the claim primitive takes the oldest queued deployment across the store.
        await SeedDeployedAsync(host.Deployments, "checkout", 1, "production", "linux-x64", "https://checkout-prod.example/invoke");
        using (ParsedJsonDocument<WorkflowDeployment> draft = WorkflowDeployment.Draft("checkout", 1, "production", "linux-arm64"))
        {
            (await host.Deployments.EnqueueAsync(draft.RootElement, "seed", default)).Dispose();
        }

        (int listExit, string listJson, _) = await RunAsync(host, "deployments", "list", "checkout", "1", "--output", "json");
        listExit.ShouldBe(0);
        listJson.ShouldContain("\"functionUrl\":\"https://checkout-prod.example/invoke\"");
        listJson.ShouldContain("\"status\":\"Deployed\"");
        listJson.ShouldContain("\"runtimeIdentifier\":\"linux-arm64\"");
        listJson.ShouldContain("\"status\":\"Queued\"");

        // The status filter narrows the list.
        (int filteredExit, string filteredJson, _) = await RunAsync(host, "deployments", "list", "checkout", "1", "--status", "Deployed", "--output", "json");
        filteredExit.ShouldBe(0);
        filteredJson.ShouldContain("\"status\":\"Deployed\"");
        filteredJson.ShouldNotContain("\"status\":\"Queued\"");

        (int getExit, string getJson, _) = await RunAsync(host, "deployments", "get", "checkout", "1", "production", "linux-x64");
        getExit.ShouldBe(0);
        getJson.ShouldContain("\"functionUrl\":\"https://checkout-prod.example/invoke\"");

        // The table rendering surfaces the lifecycle state.
        (int tableExit, string tableOut, _) = await RunAsync(host, "deployments", "list", "checkout", "1");
        tableExit.ShouldBe(0);
        tableOut.ShouldContain("Deployed");
        tableOut.ShouldContain("Queued");
    }

    /// <summary>Drives a seeded deployment through its real lifecycle to Deployed (enqueue, claim, complete).</summary>
    private static async Task SeedDeployedAsync(InMemoryWorkflowDeploymentStore deployments, string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, string functionUrl)
    {
        using (ParsedJsonDocument<WorkflowDeployment> draft = WorkflowDeployment.Draft(baseWorkflowId, versionNumber, environment, runtimeIdentifier))
        {
            (await deployments.EnqueueAsync(draft.RootElement, "seed", default)).Dispose();
        }

        using ParsedJsonDocument<WorkflowDeployment>? claimed = await deployments.ClaimNextQueuedAsync("seed-worker", TimeSpan.FromMinutes(5), default);
        claimed.ShouldNotBeNull();
        string id = claimed.RootElement.IdValue;
        WorkflowEtag etag = claimed.RootElement.EtagValue;
        (await deployments.CompleteAsync(id, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, FunctionUrl: functionUrl), etag, default))?.Dispose();
    }

    /// <summary>Builds a minimal runner registration document (the §5 wire shape).</summary>
    private static RunnerRegistration Registration(string runnerId, string environment, DateTimeOffset at, string? isolationModel = null)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("runnerId", runnerId);
            writer.WriteString("environment", environment);
            writer.WriteString("startedAt", at.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("lastSeenAt", at.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteNumber("maxConcurrency", 4);
            writer.WriteStartArray("transports");
            writer.WriteStringValue("http");
            writer.WriteEndArray();
            writer.WriteStartArray("hostedVersions");
            writer.WriteEndArray();
            if (isolationModel is not null)
            {
                writer.WriteString("isolationModel", isolationModel);
            }

            writer.WriteEndObject();
        }

        return RunnerRegistration.FromJson(buffer.WrittenMemory);
    }

    private static async Task<OperatorHost> StartOperatorHostAsync()
    {
        var stateStore = new InMemoryWorkflowStateStore();
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), stateStore, "ops");
        var management = new SecuredWorkflowManagement(stateStore, "ops");
        var registry = new InMemoryRunnerRegistry();
        var builds = new InMemoryNativeBuildJobStore();
        var deployments = new InMemoryWorkflowDeploymentStore();

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapArazzoControlPlane(management, catalog, registry, ControlPlaneSecurityMode.Open, nativeBuildJobStore: builds, workflowDeploymentStore: deployments);
        await app.StartAsync();

        return new OperatorHost(app, catalog, registry, builds, deployments, app.Urls.First());
    }

    private static Task<(int Exit, string Stdout, string Stderr)> RunAsync(OperatorHost host, params string[] args)
        => RunAsync(new Host(host.App, null!, TimeProvider.System, host.Url), args);

    private sealed record OperatorHost(
        WebApplication App,
        SecuredWorkflowCatalog Catalog,
        InMemoryRunnerRegistry Registry,
        InMemoryNativeBuildJobStore Builds,
        InMemoryWorkflowDeploymentStore Deployments,
        string Url) : IAsyncDisposable
    {
        public async Task SeedVersionAsync(string workflowId)
        {
            byte[] workflow = Encoding.UTF8.GetBytes($$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "Flow", "description": "A flow." },
              "sourceDescriptions": [],
              "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
            }
            """);
            await this.Catalog.AddAsync(CatalogPackage.Build(workflow, []), new CatalogOwner("Team", "team@example.com", null, null), default, default, default);
        }

        public async ValueTask DisposeAsync() => await this.App.DisposeAsync();
    }
}