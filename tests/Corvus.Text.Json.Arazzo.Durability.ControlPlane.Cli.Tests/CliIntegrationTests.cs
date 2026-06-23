// <copyright file="CliIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// End-to-end tests that drive the generated <c>arazzo-runs</c> command surface in-process against a real
/// Kestrel-hosted control-plane server over loopback HTTP — exercising the generated client, the HTTP
/// transport, URI resolution, and the server handler together.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed partial class CliIntegrationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task List_filters_by_status_over_http()
    {
        await using Host host = await StartAsync();
        await CompleteRunAsync(host.Store, "done-1", host.Clock);
        await CompleteRunAsync(host.Store, "done-2", host.Clock);
        await FaultRunAsync(host.Store, "faulted-1", host.Clock);

        (int exit, string stdout, _) = await RunAsync(host, "list", "--status", "Completed");

        exit.ShouldBe(0);
        stdout.ShouldContain("Status");   // table header (table is the default output)
        stdout.ShouldContain("done-1");
        stdout.ShouldContain("done-2");
        stdout.ShouldNotContain("faulted-1");
    }

    [TestMethod]
    public async Task List_emits_json_with_output_json()
    {
        await using Host host = await StartAsync();
        await CompleteRunAsync(host.Store, "done-1", host.Clock);

        (int exit, string stdout, _) = await RunAsync(host, "list", "--status", "Completed", "--output", "json");

        exit.ShouldBe(0);
        stdout.ShouldContain("\"runs\"");
        stdout.ShouldContain("\"id\":\"done-1\"");
    }

    [TestMethod]
    public async Task Get_returns_run_detail_over_http()
    {
        await using Host host = await StartAsync();
        await FaultRunAsync(host.Store, "r1", host.Clock);

        (int exit, string stdout, _) = await RunAsync(host, "get", "r1");

        exit.ShouldBe(0);
        stdout.ShouldContain("\"status\":\"Faulted\"");
        stdout.ShouldContain("step1");
    }

    [TestMethod]
    public async Task Get_unknown_run_exits_non_zero_with_problem()
    {
        await using Host host = await StartAsync();

        (int exit, _, string stderr) = await RunAsync(host, "get", "nope");

        exit.ShouldNotBe(0);
        stderr.ShouldContain("\"status\":404");
    }

    [TestMethod]
    public async Task Resume_rewind_resets_the_cursor_over_http()
    {
        await using Host host = await StartAsync();
        await FaultRunAtAsync(host.Store, "r1", cursor: 3, faultStep: "step3", host.Clock);

        (int exit, string stdout, _) = await RunAsync(host, "resume", "r1", "--mode", "Rewind", "--target-cursor", "1");

        exit.ShouldBe(0);
        stdout.ShouldContain("\"status\":\"Completed\"");
        stdout.ShouldContain("\"cursor\":1");
    }

    [TestMethod]
    public async Task Resume_state_patch_over_http()
    {
        await using Host host = await StartAsync();
        await FaultRunAsync(host.Store, "r1", host.Clock);

        string patchFile = Path.Combine(Path.GetTempPath(), $"arazzo-runs-patch-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(patchFile, """[{"op":"add","path":"/inputs","value":{"x":1}}]""");
        try
        {
            (int exit, string stdout, _) = await RunAsync(host, "resume", "r1", "--mode", "StatePatch", "--patch-file", patchFile);

            exit.ShouldBe(0);
            stdout.ShouldContain("\"status\":\"Completed\"");
        }
        finally
        {
            File.Delete(patchFile);
        }
    }

    [TestMethod]
    public async Task Resume_state_patch_rejects_a_non_conformant_patch()
    {
        await using Host host = await StartAsync();
        await FaultRunAsync(host.Store, "r1", host.Clock);

        // An array of objects (so it passes the loose body schema) but not a valid RFC 6902 op.
        string patchFile = Path.Combine(Path.GetTempPath(), $"arazzo-runs-badpatch-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(patchFile, """[{"foo":"bar"}]""");
        try
        {
            (int exit, _, string stderr) = await RunAsync(host, "resume", "r1", "--mode", "StatePatch", "--patch-file", patchFile);

            exit.ShouldNotBe(0);
            stderr.ShouldContain("not a conformant");
        }
        finally
        {
            File.Delete(patchFile);
        }
    }

    [TestMethod]
    public async Task Cancel_marks_a_run_cancelled_over_http()
    {
        await using Host host = await StartAsync();
        await FaultRunAsync(host.Store, "r1", host.Clock);

        (int exit, string stdout, _) = await RunAsync(host, "cancel", "r1", "--reason", "operator abandoned");

        exit.ShouldBe(0);
        stdout.ShouldContain("\"status\":\"Cancelled\"");
    }

    [TestMethod]
    public async Task Delete_removes_a_run_over_http()
    {
        await using Host host = await StartAsync();
        await CompleteRunAsync(host.Store, "r1", host.Clock);

        (int exit, string stdout, _) = await RunAsync(host, "delete", "r1");

        exit.ShouldBe(0);
        stdout.ShouldContain("Deleted run 'r1'.");

        (int getExit, _, _) = await RunAsync(host, "get", "r1");
        getExit.ShouldNotBe(0);
    }

    [TestMethod]
    public async Task Purge_reaps_old_terminal_runs_over_http()
    {
        await using Host host = await StartAsync();
        await CompleteRunAsync(host.Store, "old-done", host.Clock);

        string olderThan = (T0 + TimeSpan.FromHours(1)).ToString("O");
        (int exit, string stdout, _) = await RunAsync(host, "purge", "--older-than", olderThan);

        exit.ShouldBe(0);
        stdout.ShouldContain("\"purgedCount\":1");
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> RunAsync(Host host, params string[] args)
    {
        string[] fullArgs = [.. args, "--server", host.Url];
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        TextWriter previousOut = Console.Out;
        TextWriter previousError = Console.Error;
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            int exit = await CliApp.Create().RunAsync(fullArgs);
            return (exit, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    private static async Task FaultRunAsync(InMemoryWorkflowStateStore store, string id, TimeProvider clock)
    {
        WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock);
        await run.FaultAsync("step1", attempt: 1, "boom", default);
    }

    private static async Task FaultRunAtAsync(InMemoryWorkflowStateStore store, string id, int cursor, string faultStep, TimeProvider clock)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock);
        await run.CheckpointAsync(cursor, default);
        await run.FaultAsync(faultStep, attempt: 1, "boom", default);
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
        var management = new SecuredWorkflowManagement(store, "ops", CompleteResumer, clock);
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(clock), store, "ops");

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Open);
        await app.StartAsync();

        string url = app.Urls.First();
        return new Host(app, store, clock, url);

        static async ValueTask<WorkflowRunResultKind> CompleteResumer(WorkflowRun run, CancellationToken ct)
        {
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        }
    }

    private sealed record Host(WebApplication App, InMemoryWorkflowStateStore Store, TimeProvider Clock, string Url) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await this.App.DisposeAsync();
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}