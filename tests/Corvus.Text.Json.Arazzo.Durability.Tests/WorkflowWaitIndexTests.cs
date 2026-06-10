// <copyright file="WorkflowWaitIndexTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the in-memory store's <see cref="IWorkflowWaitIndex"/>: due-timer and awaiting-message
/// wakeup queries (what the worker polls) and the operator visibility query.
/// </summary>
[TestClass]
public sealed class WorkflowWaitIndexTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task QueryDue_returns_only_suspended_runs_whose_timer_is_due()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);

        await SuspendTimer(store, time, "soon", TimeSpan.FromMinutes(1));
        await SuspendTimer(store, time, "later", TimeSpan.FromHours(1));
        await Running(store, time, "running");

        List<WorkflowRunId> due = await Collect(store.QueryDueAsync(Start + TimeSpan.FromMinutes(5), default));

        due.ShouldHaveSingleItem();
        due[0].ShouldBe(new WorkflowRunId("soon"));
    }

    [TestMethod]
    public async Task QueryAwaiting_matches_channel_and_correlation()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);

        await SuspendMessage(store, time, "orderA", "responses", "order-A");
        await SuspendMessage(store, time, "orderB", "responses", "order-B");
        await SuspendMessage(store, time, "other", "notifications", "order-A");

        List<WorkflowRunId> matchA = await Collect(store.QueryAwaitingAsync("responses", "order-A", default));
        matchA.ShouldHaveSingleItem();
        matchA[0].ShouldBe(new WorkflowRunId("orderA"));

        List<WorkflowRunId> matchChannel = await Collect(store.QueryAwaitingAsync("responses", null, default));
        matchChannel.Count.ShouldBe(2);

        List<WorkflowRunId> noMatch = await Collect(store.QueryAwaitingAsync("responses", "order-Z", default));
        noMatch.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task QueryAwaiting_matches_a_run_awaiting_a_channel_with_no_correlation()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);

        // A run awaiting the channel with no specific correlation accepts any message on that channel.
        await SuspendMessage(store, time, "anyOnEvents", "events", correlationId: null);

        List<WorkflowRunId> matched = await Collect(store.QueryAwaitingAsync("events", "incoming-99", default));
        matched.ShouldHaveSingleItem();
        matched[0].ShouldBe(new WorkflowRunId("anyOnEvents"));
    }

    [TestMethod]
    public async Task Query_filters_by_status_and_workflow()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);

        await SuspendTimer(store, time, "s1", TimeSpan.FromMinutes(1));
        await Running(store, time, "r1");

        WorkflowRunPage suspended = await store.QueryAsync(new WorkflowQuery(Status: WorkflowRunStatus.Suspended), default);
        suspended.Runs.ShouldHaveSingleItem();
        suspended.Runs[0].Id.ShouldBe(new WorkflowRunId("s1"));
        suspended.Runs[0].Index.Status.ShouldBe(WorkflowRunStatus.Suspended);

        WorkflowRunPage byWorkflow = await store.QueryAsync(new WorkflowQuery(WorkflowId: "wf"), default);
        byWorkflow.Runs.Count.ShouldBe(2);
    }

    private static async ValueTask SuspendTimer(InMemoryWorkflowStateStore store, TimeProvider time, string id, TimeSpan delay)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{ "v": 1 }"""u8.ToArray());
        using var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, time);
        await run.SuspendForTimerAsync(1, delay, default);
    }

    private static async ValueTask SuspendMessage(InMemoryWorkflowStateStore store, TimeProvider time, string id, string channel, string? correlationId)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{ "v": 1 }"""u8.ToArray());
        using var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, time);
        await run.SuspendForMessageAsync(1, channel, correlationId, default);
    }

    private static async ValueTask Running(InMemoryWorkflowStateStore store, TimeProvider time, string id)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{ "v": 1 }"""u8.ToArray());
        using var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, time);
        await run.CheckpointAsync(1, default);
    }

    private static async ValueTask<List<WorkflowRunId>> Collect(IAsyncEnumerable<WorkflowRunId> source)
    {
        var list = new List<WorkflowRunId>();
        await foreach (WorkflowRunId id in source)
        {
            list.Add(id);
        }

        return list;
    }
}