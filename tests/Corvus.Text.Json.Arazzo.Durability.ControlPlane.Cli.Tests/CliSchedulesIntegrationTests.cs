// <copyright file="CliSchedulesIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// End-to-end tests for the <c>schedules</c> command surface (#896), driven in-process against an Open-mode
/// control-plane server over loopback HTTP. A schedule is a durable run of the reserved $schedule workflow; these seed
/// one directly and exercise the read / delete commands, plus the create/run-now argument binding and error rendering.
/// </summary>
public sealed partial class CliIntegrationTests
{
    [TestMethod]
    public async Task Schedules_are_listed_read_and_deleted_over_http()
    {
        await using Host host = await StartAsync();
        await SeedScheduleAsync(host.Store, host.Clock, "nightly", "0 9 * * *", "flow-v1");

        // list (json) shows the seeded schedule and its versioned target.
        (int listExit, string listOut, _) = await RunAsync(host, "schedules", "list", "--output", "json");
        listExit.ShouldBe(0);
        listOut.ShouldContain("nightly");
        listOut.ShouldContain("flow-v1");

        // get by scheduleId returns it (it is addressable by its id, not the run id).
        (int getExit, string getOut, _) = await RunAsync(host, "schedules", "get", "nightly");
        getExit.ShouldBe(0);
        getOut.ShouldContain("flow-v1");

        // delete cancels it; it then reads back as absent (non-zero exit on the 404).
        (int delExit, string delOut, _) = await RunAsync(host, "schedules", "delete", "nightly");
        delExit.ShouldBe(0);
        delOut.ShouldContain("Deleted schedule 'nightly'.");

        (int goneExit, _, string goneErr) = await RunAsync(host, "schedules", "get", "nightly");
        goneExit.ShouldNotBe(0);
        goneErr.ShouldNotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task Creating_a_schedule_for_an_unknown_target_is_reported()
    {
        await using Host host = await StartAsync();

        // No such catalog version: the create is refused (404). This still drives the whole create path in the CLI —
        // argument binding, the --inputs JSON parse, the ScheduleCreate body build — before the server responds.
        (int createExit, _, string createErr) = await RunAsync(
            host, "schedules", "create", "nightly", "development", "flow", "1", "--cron", "0 9 * * *", "--inputs", """{"petId":5}""");
        createExit.ShouldNotBe(0);
        createErr.ShouldNotBeNullOrEmpty();

        // run-now on a schedule that does not exist is likewise reported.
        (int runExit, _, string runErr) = await RunAsync(host, "schedules", "run-now", "nightly");
        runExit.ShouldNotBe(0);
        runErr.ShouldNotBeNullOrEmpty();
    }

    private static async Task SeedScheduleAsync(InMemoryWorkflowStateStore store, TimeProvider clock, string scheduleId, string cron, string targetWorkflowId)
    {
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(
            $$"""{"scheduleId":"{{scheduleId}}","cron":"{{cron}}","timeZone":"UTC","targetWorkflowId":"{{targetWorkflowId}}"}"""));

        // The run id is derived from the scheduleId exactly as the /schedules API derives it, so the seeded schedule is
        // addressable by get / delete on its scheduleId.
        string runId = SecuredWorkflowManagement.IdempotentRunId(ScheduleHostedWorkflow.ScheduleWorkflowId, scheduleId).Value;
        using WorkflowRun run = WorkflowRun.CreateNew(store, runId, ScheduleHostedWorkflow.ScheduleWorkflowId, inputs.RootElement, "development", clock);
        await run.EnqueueAsync(default);
    }
}