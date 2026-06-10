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
    public async Task Resume_retries_a_faulted_run()
    {
        Host host = await StartAsync();
        await using (host.App)
        {
            await FaultRunAsync(host.Store, "r1", host.Clock);

            HttpResponseMessage response = await host.Client.PostAsync("/runs/r1/resume", Json("{}"));

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

            HttpResponseMessage response = await host.Client.PostAsync("/runs/done-1/resume", Json("{}"));

            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            using Stj.JsonDocument doc = await ReadJsonAsync(response);
            doc.RootElement.GetProperty("status").GetInt32().ShouldBe(409);
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

    private static async Task<Host> StartAsync()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);

        // The resumer stands in for re-entering a generated executor: it drives a resumed faulted run to completion.
        var management = new WorkflowManagementClient(store, "ops", CompleteResumer, clock);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        app.MapArazzoControlPlane(management);
        await app.StartAsync();

        return new Host(app, app.GetTestClient(), store, clock);

        static async ValueTask<WorkflowRunResultKind> CompleteResumer(WorkflowRun run, CancellationToken ct)
        {
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        }
    }

    private sealed record Host(WebApplication App, HttpClient Client, InMemoryWorkflowStateStore Store, TimeProvider Clock);

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}