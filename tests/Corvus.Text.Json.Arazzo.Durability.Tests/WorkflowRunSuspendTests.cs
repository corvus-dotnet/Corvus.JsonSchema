// <copyright file="WorkflowRunSuspendTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the Tier-2 suspend/fault/resume surface on <see cref="WorkflowRun"/>: a run can suspend on a
/// timer or a correlated message and that wait round-trips through the store; a run can fault and that record
/// round-trips; and a worker can hand a delivered message back to a resumed run.
/// </summary>
[TestClass]
public sealed class WorkflowRunSuspendTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Suspend_on_a_timer_round_trips_as_a_due_wait()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);
        WorkflowRunId id = "timer-1";

        using (ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "v": 1 }"""u8.ToArray()))
        using (var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, time))
        {
            WorkflowWait wait = await run.SuspendForTimerAsync(cursor: 2, TimeSpan.FromMinutes(5), default);

            wait.Kind.ShouldBe(WorkflowWaitKind.Timer);
            wait.DueAt.ShouldBe(Start + TimeSpan.FromMinutes(5));
            run.Status.ShouldBe(WorkflowRunStatus.Suspended);
        }

        using WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, id, time);
        resumed.ShouldNotBeNull();
        resumed.Status.ShouldBe(WorkflowRunStatus.Suspended);
        resumed.Cursor.ShouldBe(2);
        resumed.Wait.ShouldNotBeNull();
        resumed.Wait!.Value.Kind.ShouldBe(WorkflowWaitKind.Timer);
        resumed.Wait.Value.DueAt.ShouldBe(Start + TimeSpan.FromMinutes(5));
    }

    [TestMethod]
    public async Task Suspend_on_a_message_round_trips_as_an_awaiting_wait()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);
        WorkflowRunId id = "msg-1";

        using (ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "v": 1 }"""u8.ToArray()))
        using (var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, time))
        {
            await run.SuspendForMessageAsync(cursor: 1, "responses", "order-9", default);
        }

        using WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, id, time);
        resumed.ShouldNotBeNull();
        resumed.Status.ShouldBe(WorkflowRunStatus.Suspended);
        resumed.Wait!.Value.Kind.ShouldBe(WorkflowWaitKind.Message);
        resumed.Wait.Value.Channel.ShouldBe("responses");
        resumed.Wait.Value.CorrelationId.ShouldBe("order-9");
    }

    [TestMethod]
    public async Task Fault_round_trips_as_a_fault_record()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);
        WorkflowRunId id = "fault-1";

        using (ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "v": 1 }"""u8.ToArray()))
        using (var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, time))
        {
            WorkflowFault fault = await run.FaultAsync("charge", 3, "gateway timeout", default);
            fault.At.ShouldBe(Start);
        }

        using WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, id, time);
        resumed.ShouldNotBeNull();
        resumed.Status.ShouldBe(WorkflowRunStatus.Faulted);
        resumed.Fault.ShouldNotBeNull();
        resumed.Fault!.Value.StepId.ShouldBe("charge");
        resumed.Fault.Value.Attempt.ShouldBe(3);
        resumed.Fault.Value.Error.ShouldBe("gateway timeout");
        resumed.Fault.Value.At.ShouldBe(Start);
    }

    [TestMethod]
    public async Task Checkpointing_after_a_suspend_clears_the_wait()
    {
        var time = new TestTimeProvider(Start);
        var store = new InMemoryWorkflowStateStore(time);
        WorkflowRunId id = "timer-2";

        using (ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "v": 1 }"""u8.ToArray()))
        using (var run = WorkflowRun.CreateNew(store, id, "wf", doc.RootElement, time))
        {
            await run.SuspendForTimerAsync(cursor: 1, TimeSpan.FromMinutes(5), default);
            await run.CheckpointAsync(cursor: 2, default);
            run.Status.ShouldBe(WorkflowRunStatus.Running);
            run.Wait.ShouldBeNull();
        }

        using WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, id, time);
        resumed.ShouldNotBeNull();
        resumed.Status.ShouldBe(WorkflowRunStatus.Running);
        resumed.Wait.ShouldBeNull();
    }

    [TestMethod]
    public void A_delivered_message_is_taken_once()
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "lumens": 150 }"""u8.ToArray());
        using var run = WorkflowRun.CreateNew(store, "deliver-1", "wf", default);

        run.TryTakeDeliveredMessage(out _).ShouldBeFalse();

        run.DeliverMessage(doc.RootElement);
        run.TryTakeDeliveredMessage(out JsonElement payload).ShouldBeTrue();
        payload.GetProperty("lumens"u8).GetInt32().ShouldBe(150);

        // A second take returns nothing — the message is consumed.
        run.TryTakeDeliveredMessage(out _).ShouldBeFalse();
    }
}