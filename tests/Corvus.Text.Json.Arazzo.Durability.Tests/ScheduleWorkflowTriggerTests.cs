// <copyright file="ScheduleWorkflowTriggerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for <see cref="ScheduleWorkflowTrigger"/> — each occurrence starts a run, idempotently on the slot.</summary>
[TestClass]
public sealed class ScheduleWorkflowTriggerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 12, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task A_due_tick_fires_every_elapsed_slot_and_re_ticking_is_idempotent()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, owner: "ops");
        var time = new TestTimeProvider(Start);

        WorkflowStartHandler start = (request, cancellationToken) =>
            management.StartIdempotentAsync(
                request.WorkflowId, request.Inputs, request.IdempotencyKey, request.CorrelationId, request.Tags, cancellationToken: cancellationToken);

        var binding = new ScheduleTriggerBinding("nightly-reconcile-v1");
        await using var trigger = new ScheduleWorkflowTrigger(start, binding, new IntervalSchedule(TimeSpan.FromHours(1)), time);

        // Nothing is due at the watermark (the construction instant).
        (await trigger.FireDueAsync(default)).ShouldBe(0);

        // Three hours elapse: the 01:00, 02:00 and 03:00 slots are all caught up in one tick.
        time.Advance(TimeSpan.FromHours(3));
        (await trigger.FireDueAsync(default)).ShouldBe(3);

        // Re-ticking with no further time elapsed fires nothing (the watermark advanced).
        (await trigger.FireDueAsync(default)).ShouldBe(0);

        // One more hour: exactly the 04:00 slot fires.
        time.Advance(TimeSpan.FromHours(1));
        (await trigger.FireDueAsync(default)).ShouldBe(1);

        // Four distinct Pending runs exist — one per slot, none duplicated.
        WorkflowRunPage pending = await management.ListAsync(new WorkflowQuery(WorkflowRunStatus.Pending), AccessContext.System, default);
        pending.Runs.Count.ShouldBe(4);
    }

    [TestMethod]
    public async Task A_retried_fire_for_the_same_slot_resolves_to_the_same_run()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new WorkflowManagementClient(store, owner: "ops");

        WorkflowStartHandler start = (request, cancellationToken) =>
            management.StartIdempotentAsync(
                request.WorkflowId, request.Inputs, request.IdempotencyKey, request.CorrelationId, request.Tags, cancellationToken: cancellationToken);

        var binding = new ScheduleTriggerBinding("nightly-reconcile-v1");
        await using var trigger = new ScheduleWorkflowTrigger(start, binding, new IntervalSchedule(TimeSpan.FromHours(1)), new TestTimeProvider(Start));

        WorkflowRunId a1 = await trigger.FireOnceAsync(Start, default);
        WorkflowRunId a2 = await trigger.FireOnceAsync(Start, default);  // a retried fire for the same slot
        WorkflowRunId b1 = await trigger.FireOnceAsync(Start.AddHours(1), default);

        a1.ShouldBe(a2);
        b1.ShouldNotBe(a1);

        WorkflowRunPage pending = await management.ListAsync(new WorkflowQuery(WorkflowRunStatus.Pending), AccessContext.System, default);
        pending.Runs.Count.ShouldBe(2);
    }

    [TestMethod]
    public void IntervalSchedule_advances_by_its_interval_and_a_nonpositive_interval_stops()
    {
        var now = new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero);

        new IntervalSchedule(TimeSpan.FromMinutes(15)).GetNextOccurrence(now)
            .ShouldBe(now.AddMinutes(15));

        new IntervalSchedule(TimeSpan.Zero).GetNextOccurrence(now)
            .ShouldBeNull();
    }
}