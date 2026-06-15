// <copyright file="ControlPlaneServerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// End-to-end tests for the generated control-plane server over an in-memory durability store, hitting the
/// HTTP endpoints through an ASP.NET Core in-process test host.
/// </summary>
[TestClass]
public sealed class ControlPlaneServerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Get_returns_run_detail()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await FaultRunAsync(host.Store, "r1", host.Clock);

            HttpResponseMessage response = await host.Client.GetAsync("/runs/r1");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            doc.RootElement.GetProperty("status").GetString().ShouldBe("Faulted");
            doc.RootElement.GetProperty("fault").GetProperty("stepId").GetString().ShouldBe("step1");
        }
    }

    [TestMethod]
    public async Task Get_unknown_run_returns_404_problem()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            HttpResponseMessage response = await host.Client.GetAsync("/runs/nope");

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            doc.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
            doc.RootElement.GetProperty("title").GetString().ShouldBe("Run not found");
        }
    }

    [TestMethod]
    public async Task List_filters_by_status()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await FaultRunAsync(host.Store, "faulted-1", host.Clock);
            await CompleteRunAsync(host.Store, "done-1", host.Clock);

            HttpResponseMessage response = await host.Client.GetAsync("/runs?status=Faulted");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            Stj.JsonElement runs = doc.RootElement.GetProperty("runs");
            runs.GetArrayLength().ShouldBe(1);
            runs[0].GetProperty("id").GetString().ShouldBe("faulted-1");
            runs[0].GetProperty("status").GetString().ShouldBe("Faulted");
        }
    }

    [TestMethod]
    public async Task List_filters_by_a_created_time_window()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            var clock = (MutableClock)host.Clock;
            await CompleteRunAsync(host.Store, "early", clock);   // created at T0
            clock.Advance(TimeSpan.FromHours(2));
            await CompleteRunAsync(host.Store, "late", clock);    // created at T0 + 2h

            // createdAfter (inclusive) at T0 + 1h keeps only 'late'.
            string after = Uri.EscapeDataString((T0 + TimeSpan.FromHours(1)).ToString("O"));
            using Stj.JsonDocument afterDoc = await ReadJsonAsync(await host.Client.GetAsync($"/runs?createdAfter={after}"));
            Stj.JsonElement afterRuns = afterDoc.RootElement.GetProperty("runs");
            afterRuns.GetArrayLength().ShouldBe(1);
            afterRuns[0].GetProperty("id").GetString().ShouldBe("late");

            // createdBefore (exclusive) at T0 + 1h keeps only 'early'.
            string before = Uri.EscapeDataString((T0 + TimeSpan.FromHours(1)).ToString("O"));
            using Stj.JsonDocument beforeDoc = await ReadJsonAsync(await host.Client.GetAsync($"/runs?createdBefore={before}"));
            Stj.JsonElement beforeRuns = beforeDoc.RootElement.GetProperty("runs");
            beforeRuns.GetArrayLength().ShouldBe(1);
            beforeRuns[0].GetProperty("id").GetString().ShouldBe("early");
        }
    }

    [TestMethod]
    public async Task List_filters_by_tag_and_correlation_id_and_surfaces_them()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await TaggedRunAsync(host.Store, "r-a", host.Clock, "trace-aaa", ["tenant-1", "priority"]);
            await TaggedRunAsync(host.Store, "r-b", host.Clock, "trace-bbb", ["tenant-1"]);
            await TaggedRunAsync(host.Store, "r-c", host.Clock, "trace-ccc", []);

            // Tags are AND-matched.
            using (Stj.JsonDocument both = await ReadJsonAsync(await host.Client.GetAsync("/runs?tag=tenant-1&tag=priority")))
            {
                Stj.JsonElement runs = both.RootElement.GetProperty("runs");
                runs.GetArrayLength().ShouldBe(1);
                runs[0].GetProperty("id").GetString().ShouldBe("r-a");
                // The summary surfaces tags + correlationId so an operator can pivot to telemetry.
                runs[0].GetProperty("correlationId").GetString().ShouldBe("trace-aaa");
                runs[0].GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ShouldBe(["tenant-1", "priority"], ignoreOrder: true);
            }

            // A single tag matches every run carrying it.
            using (Stj.JsonDocument one = await ReadJsonAsync(await host.Client.GetAsync("/runs?tag=tenant-1")))
            {
                one.RootElement.GetProperty("runs").GetArrayLength().ShouldBe(2);
            }

            // Correlation id is an exact match (the trace->run pivot).
            using (Stj.JsonDocument corr = await ReadJsonAsync(await host.Client.GetAsync("/runs?correlationId=trace-bbb")))
            {
                Stj.JsonElement runs = corr.RootElement.GetProperty("runs");
                runs.GetArrayLength().ShouldBe(1);
                runs[0].GetProperty("id").GetString().ShouldBe("r-b");
            }
        }
    }

    [TestMethod]
    public async Task List_pages_with_a_continuation_token()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            for (int i = 1; i <= 5; i++)
            {
                await CompleteRunAsync(host.Store, $"run-{i:00}", host.Clock);
            }

            var collected = new List<string>();
            string? token = null;
            int pages = 0;
            do
            {
                string url = token is null
                    ? "/runs?status=Completed&limit=2"
                    : $"/runs?status=Completed&limit=2&pageToken={Uri.EscapeDataString(token)}";
                HttpResponseMessage response = await host.Client.GetAsync(url);
                response.StatusCode.ShouldBe(HttpStatusCode.OK);

                using Stj.JsonDocument doc = await ReadJsonAsync(response);
                Stj.JsonElement runs = doc.RootElement.GetProperty("runs");
                runs.GetArrayLength().ShouldBeLessThanOrEqualTo(2);
                foreach (Stj.JsonElement run in runs.EnumerateArray())
                {
                    collected.Add(run.GetProperty("id").GetString()!);
                }

                token = doc.RootElement.TryGetProperty("nextPageToken", out Stj.JsonElement t) && t.ValueKind == Stj.JsonValueKind.String
                    ? t.GetString()
                    : null;
                (++pages).ShouldBeLessThanOrEqualTo(10);
            }
            while (token is not null);

            collected.ShouldBe(["run-01", "run-02", "run-03", "run-04", "run-05"]);
        }
    }

    [TestMethod]
    public async Task Resume_retries_a_faulted_run()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await FaultRunAsync(host.Store, "r1", host.Clock);

            HttpResponseMessage response = await host.Client.PostAsync("/runs/r1/resume", Json("""{"mode":"RetryFaultedStep"}"""));

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            doc.RootElement.GetProperty("status").GetString().ShouldBe("Completed");
        }
    }

    [TestMethod]
    public async Task Resume_of_a_non_faulted_run_returns_409()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await CompleteRunAsync(host.Store, "done-1", host.Clock);

            HttpResponseMessage response = await host.Client.PostAsync("/runs/done-1/resume", Json("""{"mode":"RetryFaultedStep"}"""));

            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            doc.RootElement.GetProperty("status").GetInt32().ShouldBe(409);
        }
    }

    [TestMethod]
    public async Task Resume_rewind_resets_the_cursor_via_rest()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await FaultRunAtAsync(host.Store, "r1", cursor: 3, faultStep: "step3", host.Clock);

            HttpResponseMessage response = await host.Client.PostAsync("/runs/r1/resume", Json("""{"mode":"Rewind","targetCursor":1}"""));

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            doc.RootElement.GetProperty("status").GetString().ShouldBe("Completed");
            doc.RootElement.GetProperty("cursor").GetInt32().ShouldBe(1);
        }
    }

    [TestMethod]
    public async Task Resume_skip_advances_the_cursor_via_rest()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await FaultRunAtAsync(host.Store, "r1", cursor: 2, faultStep: "step2", host.Clock);

            HttpResponseMessage response = await host.Client.PostAsync("/runs/r1/resume", Json("""{"mode":"Skip"}"""));

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            doc.RootElement.GetProperty("cursor").GetInt32().ShouldBe(3);
        }
    }

    [TestMethod]
    public async Task Resume_state_patch_fixes_an_input_via_rest()
    {
        int seenX = -1;
        Host host = await StartAsync(async (run, ct) =>
        {
            seenX = run.Inputs.GetProperty("x"u8).GetInt32();
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        });
        await using (host.App)
        {
            using (ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{ "x": 1 }"""u8.ToArray()))
            using (WorkflowRun run = WorkflowRun.CreateNew(host.Store, "r1", "wf", inputs.RootElement, host.Clock))
            {
                await run.CheckpointAsync(cursor: 1, default);
                await run.FaultAsync("step1", attempt: 1, "bad input", default);
            }

            HttpResponseMessage response = await host.Client.PostAsync(
                "/runs/r1/resume",
                Json("""{"mode":"StatePatch","patch":[{"op":"replace","path":"/inputs/x","value":2}]}"""));

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            seenX.ShouldBe(2);
        }
    }

    [TestMethod]
    public async Task Delete_removes_a_run()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await FaultRunAsync(host.Store, "r1", host.Clock);

            HttpResponseMessage response = await host.Client.DeleteAsync("/runs/r1");

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await host.Client.GetAsync("/runs/r1")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    [TestMethod]
    public async Task Delete_unknown_run_returns_404_problem()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            HttpResponseMessage response = await host.Client.DeleteAsync("/runs/nope");

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            doc.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        }
    }

    [TestMethod]
    public async Task Cancel_marks_a_run_cancelled()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await FaultRunAsync(host.Store, "r1", host.Clock);

            HttpResponseMessage response = await host.Client.PostAsync("/runs/r1/cancel", Json("""{"reason":"operator abandoned"}"""));

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            doc.RootElement.GetProperty("status").GetString().ShouldBe("Cancelled");
        }
    }

    [TestMethod]
    public async Task Cancel_of_a_terminal_run_returns_409()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await CompleteRunAsync(host.Store, "done-1", host.Clock);

            HttpResponseMessage response = await host.Client.PostAsync("/runs/done-1/cancel", Json("""{"reason":"too late"}"""));

            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }

    [TestMethod]
    public async Task Purge_reaps_old_terminal_runs()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await CompleteRunAsync(host.Store, "old-done", host.Clock);

            string olderThan = Uri.EscapeDataString((T0 + TimeSpan.FromHours(1)).ToString("O"));
            var request = new HttpRequestMessage(new HttpMethod("PURGE"), $"/runs?olderThan={olderThan}");
            HttpResponseMessage response = await host.Client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            doc.RootElement.GetProperty("purgedCount").GetInt32().ShouldBe(1);
            (await host.Client.GetAsync("/runs/old-done")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task FaultRunAsync(InMemoryWorkflowStateStore store, string id, TimeProvider clock)
    {
        WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock);
        await run.FaultAsync("step1", attempt: 1, "boom", default);
    }

    private static async Task CompleteRunAsync(InMemoryWorkflowStateStore store, string id, TimeProvider clock)
    {
        WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock);
        await run.CompleteAsync(default, default);
    }

    [TestMethod]
    public async Task GetCatalogWorkflowSchemas_returns_the_baked_metadata_and_404_when_absent()
    {
        var clock = new MutableClock(T0);
        var runStore = new InMemoryWorkflowStateStore(clock);
        var catalogStore = new InMemoryWorkflowCatalogStore(clock, new FakeSchemaProvider());
        var management = new WorkflowManagementClient(runStore, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(catalogStore, runStore, "ops");

        // Add a version (bare workflow id "flow" → flow-v1); the store bakes the metadata via the provider.
        await catalog.AddAsync(SchemaWorkflowPackage("flow"), new CatalogOwner("Team", "team@example.com"), default, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        var runnerRegistry = new InMemoryRunnerRegistry();
        app.MapArazzoControlPlane(management, catalog, runnerRegistry);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        HttpResponseMessage ok = await client.GetAsync("/catalog/flow/versions/1/schemas");
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ok.Content.ReadAsStringAsync()).ShouldContain("\"baked\":true");

        HttpResponseMessage missing = await client.GetAsync("/catalog/flow/versions/99/schemas");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await app.StopAsync();
    }

    [TestMethod]
    public async Task GetCatalogExecutor_streams_the_assembly_and_manifest_and_marks_the_version_runnable()
    {
        var clock = new MutableClock(T0);
        var runStore = new InMemoryWorkflowStateStore(clock);
        var catalogStore = new InMemoryWorkflowCatalogStore(clock, executorProvider: new FakeExecutorProvider());
        var management = new WorkflowManagementClient(runStore, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(catalogStore, runStore, "ops");

        await catalog.AddAsync(SchemaWorkflowPackage("flow"), new CatalogOwner("Team", "team@example.com"), default, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        var runnerRegistry = new InMemoryRunnerRegistry();
        app.MapArazzoControlPlane(management, catalog, runnerRegistry);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        // The compiled assembly streams back as octet-stream, byte-for-byte.
        HttpResponseMessage executor = await client.GetAsync("/catalog/flow/versions/1/executor");
        executor.StatusCode.ShouldBe(HttpStatusCode.OK);
        executor.Content.Headers.ContentType!.MediaType.ShouldBe("application/octet-stream");
        (await executor.Content.ReadAsByteArrayAsync()).ShouldBe(FakeExecutorProvider.AssemblyBytes);

        // The manifest streams back as JSON.
        HttpResponseMessage manifest = await client.GetAsync("/catalog/flow/versions/1/executor-manifest");
        manifest.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await manifest.Content.ReadAsStringAsync()).ShouldContain("\"entryType\"");

        // The version summary advertises that it is runnable.
        using (Stj.JsonDocument version = await ReadJsonAsync(await client.GetAsync("/catalog/flow/versions/1")))
        {
            version.RootElement.GetProperty("runnable").GetBoolean().ShouldBeTrue();
        }

        HttpResponseMessage missing = await client.GetAsync("/catalog/flow/versions/99/executor");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await app.StopAsync();
    }

    [TestMethod]
    public async Task GetCatalogExecutor_is_404_and_the_version_is_not_runnable_without_a_provider()
    {
        var clock = new MutableClock(T0);
        var runStore = new InMemoryWorkflowStateStore(clock);
        var catalogStore = new InMemoryWorkflowCatalogStore(clock);
        var management = new WorkflowManagementClient(runStore, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(catalogStore, runStore, "ops");

        await catalog.AddAsync(SchemaWorkflowPackage("flow"), new CatalogOwner("Team", "team@example.com"), default, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        var runnerRegistry = new InMemoryRunnerRegistry();
        app.MapArazzoControlPlane(management, catalog, runnerRegistry);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        HttpResponseMessage executor = await client.GetAsync("/catalog/flow/versions/1/executor");
        executor.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using (Stj.JsonDocument version = await ReadJsonAsync(await client.GetAsync("/catalog/flow/versions/1")))
        {
            version.RootElement.GetProperty("runnable").GetBoolean().ShouldBeFalse();
        }

        await app.StopAsync();
    }

    [TestMethod]
    public async Task ValidateCatalogValue_validates_inputs_against_the_real_schema()
    {
        var clock = new MutableClock(T0);
        var runStore = new InMemoryWorkflowStateStore(clock);
        var catalogStore = new InMemoryWorkflowCatalogStore(clock);
        var management = new WorkflowManagementClient(runStore, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(catalogStore, runStore, "ops");

        await catalog.AddAsync(InputsWorkflowPackage("flow"), new CatalogOwner("Team", "team@example.com"), default, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        var runnerRegistry = new InMemoryRunnerRegistry();
        app.MapArazzoControlPlane(management, catalog, runnerRegistry);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        // A conforming value validates.
        HttpResponseMessage valid = await client.PostAsync(
            "/catalog/flow/versions/1/validate",
            new StringContent("""{ "target": { "kind": "inputs" }, "value": { "petId": 5 } }""", Encoding.UTF8, "application/json"));
        valid.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument doc = await ReadJsonAsync(valid))
        {
            doc.RootElement.GetProperty("valid").GetBoolean().ShouldBeTrue();
            doc.RootElement.GetProperty("errors").GetArrayLength().ShouldBe(0);
        }

        // A non-conforming value (missing required petId, and below the minimum) fails with errors.
        HttpResponseMessage invalid = await client.PostAsync(
            "/catalog/flow/versions/1/validate",
            new StringContent("""{ "target": { "kind": "inputs" }, "value": { } }""", Encoding.UTF8, "application/json"));
        invalid.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument doc = await ReadJsonAsync(invalid))
        {
            doc.RootElement.GetProperty("valid").GetBoolean().ShouldBeFalse();
            doc.RootElement.GetProperty("errors").GetArrayLength().ShouldBeGreaterThan(0);
        }

        // An unknown version is a 404.
        HttpResponseMessage missing = await client.PostAsync(
            "/catalog/flow/versions/99/validate",
            new StringContent("""{ "target": { "kind": "inputs" }, "value": { "petId": 5 } }""", Encoding.UTF8, "application/json"));
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await app.StopAsync();
    }

    [TestMethod]
    public async Task StartCatalogWorkflowRun_creates_a_pending_run_validating_inputs()
    {
        var clock = new MutableClock(T0);
        var runStore = new InMemoryWorkflowStateStore(clock);
        var catalogStore = new InMemoryWorkflowCatalogStore(clock, executorProvider: new FakeExecutorProvider());
        var management = new WorkflowManagementClient(runStore, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(catalogStore, runStore, "ops");

        await catalog.AddAsync(InputsWorkflowPackage("flow"), new CatalogOwner("Team", "team@example.com"), default, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        var runnerRegistry = new InMemoryRunnerRegistry();
        app.MapArazzoControlPlane(management, catalog, runnerRegistry);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        await runnerRegistry.RegisterAsync(Runner("flow", 1), default);

        // Valid inputs → 202 with the run id; the run is persisted Pending.
        HttpResponseMessage accepted = await client.PostAsync(
            "/catalog/flow/versions/1/runs",
            new StringContent("""{ "petId": 5 }""", Encoding.UTF8, "application/json"));
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        string runId;
        using (Stj.JsonDocument doc = await ReadJsonAsync(accepted))
        {
            doc.RootElement.GetProperty("workflowId").GetString().ShouldBe("flow-v1");
            doc.RootElement.GetProperty("status").GetString().ShouldBe("Pending");
            runId = doc.RootElement.GetProperty("runId").GetString()!;
        }

        using (WorkflowRun? run = await WorkflowRun.ResumeAsync(runStore, runId, clock, default))
        {
            run.ShouldNotBeNull();
            run!.Status.ShouldBe(WorkflowRunStatus.Pending);
            run.WorkflowId.ShouldBe("flow-v1");
        }

        // Inputs that fail the baked schema → 422.
        HttpResponseMessage invalid = await client.PostAsync(
            "/catalog/flow/versions/1/runs",
            new StringContent("""{ }""", Encoding.UTF8, "application/json"));
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // An unknown version → 404.
        HttpResponseMessage missing = await client.PostAsync(
            "/catalog/flow/versions/99/runs",
            new StringContent("""{ "petId": 5 }""", Encoding.UTF8, "application/json"));
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await app.StopAsync();
    }

    [TestMethod]
    public async Task StartCatalogWorkflowRun_inherits_the_version_security_tags()
    {
        var clock = new MutableClock(T0);
        var runStore = new InMemoryWorkflowStateStore(clock);
        var catalogStore = new InMemoryWorkflowCatalogStore(clock, executorProvider: new FakeExecutorProvider());
        var management = new WorkflowManagementClient(runStore, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(catalogStore, runStore, "ops");

        SecurityTag[] security = [new("tenant", "acme"), new("team", "payments")];
        await catalog.AddAsync(InputsWorkflowPackage("flow"), new CatalogOwner("Team", "team@example.com"), default, security, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        var runnerRegistry = new InMemoryRunnerRegistry();
        app.MapArazzoControlPlane(management, catalog, runnerRegistry);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        await runnerRegistry.RegisterAsync(Runner("flow", 1), default);

        HttpResponseMessage accepted = await client.PostAsync(
            "/catalog/flow/versions/1/runs",
            new StringContent("""{ "petId": 5 }""", Encoding.UTF8, "application/json"));
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        string runId;
        using (Stj.JsonDocument doc = await ReadJsonAsync(accepted))
        {
            runId = doc.RootElement.GetProperty("runId").GetString()!;
        }

        // The triggered run carries the version's security tags (KVP labels), so row authorization sees them.
        WorkflowRunDetail? detail = await management.GetAsync(runId, AccessContext.System, default);
        detail.ShouldNotBeNull();
        detail.Value.SecurityTags.ShouldBe(security);

        await app.StopAsync();
    }

    [TestMethod]
    public async Task StartCatalogWorkflowRun_is_409_for_a_non_runnable_version()
    {
        var clock = new MutableClock(T0);
        var runStore = new InMemoryWorkflowStateStore(clock);
        var catalogStore = new InMemoryWorkflowCatalogStore(clock); // no executor provider → not runnable
        var management = new WorkflowManagementClient(runStore, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(catalogStore, runStore, "ops");

        await catalog.AddAsync(SchemaWorkflowPackage("flow"), new CatalogOwner("Team", "team@example.com"), default, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        var runnerRegistry = new InMemoryRunnerRegistry();
        app.MapArazzoControlPlane(management, catalog, runnerRegistry);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        HttpResponseMessage conflict = await client.PostAsync(
            "/catalog/flow/versions/1/runs",
            new StringContent("""{ }""", Encoding.UTF8, "application/json"));
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await app.StopAsync();
    }

    [TestMethod]
    public async Task StartCatalogWorkflowRun_is_409_when_no_runner_hosts_the_version()
    {
        var clock = new MutableClock(T0);
        var runStore = new InMemoryWorkflowStateStore(clock);
        var catalogStore = new InMemoryWorkflowCatalogStore(clock, executorProvider: new FakeExecutorProvider());
        var management = new WorkflowManagementClient(runStore, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(catalogStore, runStore, "ops");

        await catalog.AddAsync(InputsWorkflowPackage("flow"), new CatalogOwner("Team", "team@example.com"), default, default);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        var runnerRegistry = new InMemoryRunnerRegistry(); // runnable version, but no runner registered to host it
        app.MapArazzoControlPlane(management, catalog, runnerRegistry);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        HttpResponseMessage conflict = await client.PostAsync(
            "/catalog/flow/versions/1/runs",
            new StringContent("""{ "petId": 5 }""", Encoding.UTF8, "application/json"));
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await app.StopAsync();
    }

    [TestMethod]
    public async Task ListRunners_returns_the_registered_runners()
    {
        var clock = new MutableClock(T0);
        var runStore = new InMemoryWorkflowStateStore(clock);
        var management = new WorkflowManagementClient(runStore, "ops", CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(new InMemoryWorkflowCatalogStore(clock), runStore, "ops");

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        var runnerRegistry = new InMemoryRunnerRegistry();
        app.MapArazzoControlPlane(management, catalog, runnerRegistry);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        await runnerRegistry.RegisterAsync(Runner("flow", 1, "runner-a"), default);
        await runnerRegistry.RegisterAsync(Runner("billing", 2, "runner-b"), default);

        using Stj.JsonDocument doc = await ReadJsonAsync(await client.GetAsync("/runners"));
        System.Collections.Generic.List<Stj.JsonElement> runners = [.. doc.RootElement.GetProperty("runners").EnumerateArray()];
        runners.Count.ShouldBe(2);
        runners.Select(r => r.GetProperty("runnerId").GetString()).ShouldBe(["runner-a", "runner-b"], ignoreOrder: true);

        await app.StopAsync();
    }

    private static RunnerRegistration Runner(string baseWorkflowId, int versionNumber, string runnerId = "r1")
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var w = new Stj.Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("runnerId", runnerId);
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
            w.WriteEndObject();
        }

        return RunnerRegistration.FromJson(buffer.WrittenMemory);
    }

    private static byte[] SchemaWorkflowPackage(string id)
        => WorkflowPackage.Pack(Encoding.UTF8.GetBytes($$"""{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"{{id}}","steps":[]}]}"""), []);

    private static byte[] InputsWorkflowPackage(string id)
        => WorkflowPackage.Pack(
            Encoding.UTF8.GetBytes($$$"""{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"{{{id}}}","inputs":{"type":"object","properties":{"petId":{"type":"integer","minimum":1}},"required":["petId"]},"steps":[]}]}"""),
            []);

    private static async Task TaggedRunAsync(InMemoryWorkflowStateStore store, string id, TimeProvider clock, string correlationId, string[] tags)
    {
        WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock, correlationId: correlationId, tags: TagSet.FromTags(tags));
        await run.CompleteAsync(default, default);
    }

    private static async Task<Host> StartAsync(WorkflowResumer? resumer = null)
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);

        // The resumer stands in for re-entering a generated executor: it drives a resumed faulted run to completion.
        var management = new WorkflowManagementClient(store, "ops", resumer ?? CompleteResumer, clock);
        var catalog = new WorkflowCatalogClient(new InMemoryWorkflowCatalogStore(clock), store, "ops");

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        var runnerRegistry = new InMemoryRunnerRegistry();
        app.MapArazzoControlPlane(management, catalog, runnerRegistry);
        await app.StartAsync();

        return new Host(app, app.GetTestClient(), store, clock);
    }

    private static async ValueTask<WorkflowRunResultKind> CompleteResumer(WorkflowRun run, CancellationToken ct)
    {
        await run.CompleteAsync(default, ct);
        return WorkflowRunResultKind.Completed;
    }

    private static async Task FaultRunAtAsync(InMemoryWorkflowStateStore store, string id, int cursor, string faultStep, TimeProvider clock)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock);
        if (cursor > 0)
        {
            await run.CheckpointAsync(cursor, default);
        }

        await run.FaultAsync(faultStep, attempt: 1, "boom", default);
    }

    private sealed record Host(WebApplication App, HttpClient Client, InMemoryWorkflowStateStore Store, TimeProvider Clock);

    private sealed class FakeSchemaProvider : IWorkflowMetadataProvider
    {
        public ReadOnlyMemory<byte>? BuildSchemas(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
            => Encoding.UTF8.GetBytes("""{"formatVersion":1,"baked":true}""");
    }

    private sealed class FakeExecutorProvider : IWorkflowExecutorProvider
    {
        public static readonly byte[] AssemblyBytes = [0x4D, 0x5A, 0x90, 0x00];

        public WorkflowExecutorArtifact? BuildExecutor(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources, string packageHash)
            => new(AssemblyBytes, Encoding.UTF8.GetBytes($$"""{"formatVersion":1,"entryType":"X","packageHash":"{{packageHash}}"}"""));
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}