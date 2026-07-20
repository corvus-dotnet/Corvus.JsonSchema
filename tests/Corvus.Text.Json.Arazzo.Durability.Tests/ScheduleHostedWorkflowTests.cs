// <copyright file="ScheduleHostedWorkflowTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Tests for <see cref="ScheduleHostedWorkflow"/> — the built-in scheduler workflow (#896). A schedule is a durable
/// run of it: on each entry it fires every due occurrence through the host start path, checkpoints its watermark,
/// and suspends until the next occurrence. These tests drive <see cref="ScheduleHostedWorkflow.RunAsync"/> against a
/// real management start path (so fires create real Pending runs) and a fake run that supplies the restored
/// watermark and captures the suspend delay.
/// </summary>
[TestClass]
public sealed class ScheduleHostedWorkflowTests
{
    private static readonly IReadOnlyDictionary<string, IApiTransport> NoTransports =
        ImmutableDictionary<string, IApiTransport>.Empty;

    [TestMethod]
    public async Task A_fresh_schedule_fires_nothing_and_suspends_until_the_next_occurrence()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, owner: "ops");
        var time = new TestTimeProvider(new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero));
        var scheduler = new ScheduleHostedWorkflow(Start(management), time);

        using ParsedJsonDocument<JsonElement> inputs = Inputs("s1", "0 9 * * *", "adopt-v1");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var run = new FakeSchedulerRun(restoredWatermark: null);

        WorkflowRunResultKind kind = await scheduler.RunAsync(NoTransports, null, workspace, inputs.RootElement, run, default);

        // Fresh run: the watermark is seeded to now (08:00), so the 09:00 occurrence is not yet due — nothing fires,
        // and it suspends the one hour until 09:00.
        kind.ShouldBe(WorkflowRunResultKind.Suspended);
        run.SuspendDelay.ShouldBe(TimeSpan.FromHours(1));
        (await CountPending(management)).ShouldBe(0);
    }

    [TestMethod]
    public async Task A_resumed_schedule_catches_up_every_missed_occurrence_then_suspends()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, owner: "ops");
        var time = new TestTimeProvider(new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero));
        var scheduler = new ScheduleHostedWorkflow(Start(management), time);

        using ParsedJsonDocument<JsonElement> inputs = Inputs("s1", "0 9 * * *", "adopt-v1");
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Resumed with a watermark of 2026-06-12 08:00 while now is 2026-06-15 08:00: the 09:00 occurrences on the
        // 12th, 13th and 14th are all due (the 15th's is still in the future), so all three are caught up in one
        // entry — a runner that was down does not miss occurrences — and it suspends the hour until the 15th's 09:00.
        using ParsedJsonDocument<JsonElement> watermark = Instant(new DateTimeOffset(2026, 6, 12, 8, 0, 0, TimeSpan.Zero));
        var run = new FakeSchedulerRun(watermark.RootElement);

        WorkflowRunResultKind kind = await scheduler.RunAsync(NoTransports, null, workspace, inputs.RootElement, run, default);

        kind.ShouldBe(WorkflowRunResultKind.Suspended);
        run.SuspendDelay.ShouldBe(TimeSpan.FromHours(1));
        (await CountPending(management)).ShouldBe(3);
    }

    [TestMethod]
    public async Task Re_running_the_same_due_window_does_not_start_duplicate_target_runs()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, owner: "ops");
        var time = new TestTimeProvider(new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero));
        var scheduler = new ScheduleHostedWorkflow(Start(management), time);

        using ParsedJsonDocument<JsonElement> inputs = Inputs("s1", "0 9 * * *", "adopt-v1");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> watermark = Instant(new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero));

        // Two entries over the same due window (the 14th's 09:00) — as if a lease expired and another runner
        // re-entered — resolve to the same idempotent target run, not two.
        await scheduler.RunAsync(NoTransports, null, workspace, inputs.RootElement, new FakeSchedulerRun(watermark.RootElement), default);
        await scheduler.RunAsync(NoTransports, null, workspace, inputs.RootElement, new FakeSchedulerRun(watermark.RootElement), default);

        (await CountPending(management)).ShouldBe(1);
    }

    [TestMethod]
    public async Task A_schedule_run_resumed_by_the_worker_fires_the_target_and_re_suspends()
    {
        // End to end through the real store, wait index and worker: a schedule is an ordinary durable run, so the
        // same machinery that resumes any due suspended run drives the schedule's occurrences.
        var time = new TestTimeProvider(new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowStateStore(time);
        var management = new SecuredWorkflowManagement(store, owner: "ops");
        var scheduler = new ScheduleHostedWorkflow(Start(management), time);

        WorkflowResumer resume = async (r, ct) =>
        {
            using JsonWorkspace ws = JsonWorkspace.CreateUnrented();
            return await scheduler.RunAsync(NoTransports, null, ws, r.Inputs, r, ct);
        };

        // Register the schedule as a durable "$schedule" run and run it fresh: nothing is due yet, so it suspends
        // until the next 09:00. Its due timer + watermark are now durable in the store.
        using ParsedJsonDocument<JsonElement> inputs = Inputs("s1", "0 9 * * *", "adopt-v1");
        using (JsonWorkspace ws = JsonWorkspace.CreateUnrented())
        using (WorkflowRun run = WorkflowRun.CreateNew(store, "sched-1", ScheduleHostedWorkflow.ScheduleWorkflowId, inputs.RootElement, "development", time))
        {
            (await scheduler.RunAsync(NoTransports, null, ws, run.Inputs, run, default)).ShouldBe(WorkflowRunResultKind.Suspended);
        }

        (await CountPending(management)).ShouldBe(0);

        // The 09:00 occurrence comes due. The worker's wait index surfaces the schedule run, leases it, and resumes
        // it through the resumer, which fires the target run and re-suspends until tomorrow's 09:00.
        time.Advance(TimeSpan.FromHours(1));
        var worker = new WorkflowWorker(store, "runner-1", time);
        (await worker.ResumeDueTimersAsync(resume, default)).ShouldBe(1);

        (await CountPending(management)).ShouldBe(1);

        using WorkflowRun? reloaded = await WorkflowRun.ResumeAsync(store, "sched-1", time, default);
        reloaded.ShouldNotBeNull();
        reloaded!.Status.ShouldBe(WorkflowRunStatus.Suspended);
    }

    private static WorkflowStartHandler Start(SecuredWorkflowManagement management)
        => (request, cancellationToken) => management.StartIdempotentAsync(
            request.WorkflowId, request.Inputs, request.IdempotencyKey, "development", request.CorrelationId, request.Tags, cancellationToken: cancellationToken);

    private static async ValueTask<int> CountPending(SecuredWorkflowManagement management)
    {
        using WorkflowRunPage pending = await management.ListAsync(new WorkflowQuery(WorkflowRunStatus.Pending), AccessContext.System, default);
        return pending.Runs.Count;
    }

    private static ParsedJsonDocument<JsonElement> Inputs(string scheduleId, string cron, string targetWorkflowId)
        => ParsedJsonDocument<JsonElement>.Parse(
            $$"""{"scheduleId":"{{scheduleId}}","cron":"{{cron}}","timeZone":"UTC","targetWorkflowId":"{{targetWorkflowId}}"}""");

    private static ParsedJsonDocument<JsonElement> Instant(DateTimeOffset instant)
        => ParsedJsonDocument<JsonElement>.Parse("\"" + instant.ToString("O", System.Globalization.CultureInfo.InvariantCulture) + "\"");

    /// <summary>A minimal <see cref="IWorkflowRun"/> that supplies a restored watermark and captures the suspend delay.</summary>
    private sealed class FakeSchedulerRun(JsonElement? restoredWatermark) : IWorkflowRun
    {
        public TimeSpan? SuspendDelay { get; private set; }

        public bool Completed { get; private set; }

        public int Cursor => 0;

        public string? CorrelationId => null;

        public Dictionary<string, byte[]> CorrelationTokens { get; } = new(StringComparer.Ordinal);

        public bool TryGetStepOutputs(string stepId, out JsonElement outputs)
        {
            if (restoredWatermark is { } wm)
            {
                outputs = wm;
                return true;
            }

            outputs = default;
            return false;
        }

        public int GetRetryCount(string stepId) => 0;

        public void SetStepOutputs(string stepId, in JsonElement outputs)
        {
        }

        public void SetRetryCount(string stepId, int count)
        {
        }

        public ValueTask CheckpointAsync(int cursor, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask CompleteAsync(JsonElement outputs, CancellationToken cancellationToken)
        {
            this.Completed = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<WorkflowWait> SuspendForTimerAsync(int cursor, TimeSpan delay, CancellationToken cancellationToken)
        {
            this.SuspendDelay = delay;
            return ValueTask.FromResult<WorkflowWait>(default);
        }

        public ValueTask<WorkflowWait> SuspendForMessageAsync(int cursor, string channel, string? correlationId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask<WorkflowFault> FaultAsync(string stepId, int attempt, string error, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public bool TryTakeDeliveredMessage(out JsonElement payload)
        {
            payload = default;
            return false;
        }
    }
}