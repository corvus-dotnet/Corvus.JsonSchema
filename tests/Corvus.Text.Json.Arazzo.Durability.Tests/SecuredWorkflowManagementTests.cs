// <copyright file="SecuredWorkflowManagementTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for <see cref="SecuredWorkflowManagement"/> (the control plane, plan §11).</summary>
[TestClass]
public sealed class SecuredWorkflowManagementTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Get_returns_the_fault_detail_of_a_faulted_run()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        var client = new SecuredWorkflowManagement(store, owner: "ops");

        WorkflowRunDetail? detail = await client.GetAsync("r1", AccessContext.System, default);

        detail.ShouldNotBeNull();
        detail.Value.Status.ShouldBe(WorkflowRunStatus.Faulted);
        detail.Value.Fault!.Value.StepId.ShouldBe("step1");
        detail.Value.Fault!.Value.Error.ShouldBe("boom");
    }

    [TestMethod]
    public async Task Get_returns_null_for_an_unknown_run()
    {
        var client = new SecuredWorkflowManagement(new InMemoryWorkflowStateStore(), owner: "ops");
        (await client.GetAsync("nope", AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task List_filters_by_status()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "faulted-1");
        await CompleteRunAsync(store, "done-1");
        var client = new SecuredWorkflowManagement(store, owner: "ops");

        using WorkflowRunPage page = await client.ListAsync(new WorkflowQuery(WorkflowRunStatus.Faulted), AccessContext.System, default);

        page.Runs.Select(r => r.Id.Value).ShouldBe(["faulted-1"]);
    }

    [TestMethod]
    public async Task Resume_retries_a_faulted_run_through_the_resumer()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");

        WorkflowRunStatus seenStatus = default;

        // The resumer stands in for the generated executor re-entered at the faulted step: it observes the
        // faulted run and drives it to completion.
        ValueTask<WorkflowRunResultKind> Resumer(WorkflowRun run, CancellationToken ct)
        {
            seenStatus = run.Status;
            return CompleteAndReport(run, ct);
        }

        var client = new SecuredWorkflowManagement(store, owner: "ops", resumer: Resumer);

        bool resumed = await client.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default);

        resumed.ShouldBeTrue();
        seenStatus.ShouldBe(WorkflowRunStatus.Faulted);
        (await client.GetAsync("r1", AccessContext.System, default))!.Value.Status.ShouldBe(WorkflowRunStatus.Completed);

        static async ValueTask<WorkflowRunResultKind> CompleteAndReport(WorkflowRun run, CancellationToken ct)
        {
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        }
    }

    [TestMethod]
    public async Task Resume_does_not_invoke_the_resumer_for_a_non_faulted_run()
    {
        var store = new InMemoryWorkflowStateStore();
        await CompleteRunAsync(store, "done-1");

        bool invoked = false;
        var client = new SecuredWorkflowManagement(store, owner: "ops", resumer: (_, _) =>
        {
            invoked = true;
            return new ValueTask<WorkflowRunResultKind>(WorkflowRunResultKind.Completed);
        });

        (await client.ResumeAsync("done-1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeFalse();
        invoked.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Resume_without_a_resumer_throws()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        var client = new SecuredWorkflowManagement(store, owner: "ops");

        await Should.ThrowAsync<InvalidOperationException>(async () => await client.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default));
    }

    [TestMethod]
    public async Task Cancel_marks_a_run_cancelled_and_keeps_the_fault_for_audit()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        var client = new SecuredWorkflowManagement(store, owner: "ops");

        (await client.CancelAsync("r1", "operator abandoned", AccessContext.System, default)).ShouldBeTrue();

        WorkflowRunDetail detail = (await client.GetAsync("r1", AccessContext.System, default))!.Value;
        detail.Status.ShouldBe(WorkflowRunStatus.Cancelled);
        detail.Fault!.Value.Error.ShouldBe("boom");
    }

    [TestMethod]
    public async Task Cancel_returns_false_for_a_terminal_run()
    {
        var store = new InMemoryWorkflowStateStore();
        await CompleteRunAsync(store, "done-1");
        var client = new SecuredWorkflowManagement(store, owner: "ops");

        (await client.CancelAsync("done-1", "too late", AccessContext.System, default)).ShouldBeFalse();
        (await client.GetAsync("done-1", AccessContext.System, default))!.Value.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task Cancel_returns_false_when_another_owner_holds_the_lease()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        await store.AcquireLeaseAsync("r1", "worker-7", TimeSpan.FromMinutes(5), default);
        var client = new SecuredWorkflowManagement(store, owner: "ops");

        (await client.CancelAsync("r1", "race", AccessContext.System, default)).ShouldBeFalse();
        (await client.GetAsync("r1", AccessContext.System, default))!.Value.Status.ShouldBe(WorkflowRunStatus.Faulted);
    }

    [TestMethod]
    public async Task Purge_reaps_old_terminal_runs_only()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);

        await CompleteRunAsync(store, "old-done", clock);
        await CancelRunAsync(store, "old-cancelled", clock);
        await FaultRunAsync(store, "old-faulted", clock);  // terminal-but-recoverable, must NOT be purged

        clock.Advance(TimeSpan.FromHours(1));
        await CompleteRunAsync(store, "recent-done", clock); // updated after the cutoff, must NOT be purged

        var client = new SecuredWorkflowManagement(store, owner: "ops", timeProvider: clock);

        int purged = await client.PurgeAsync(new WorkflowPurgeQuery(T0 + TimeSpan.FromMinutes(30)), AccessContext.System, default);

        purged.ShouldBe(2);
        (await client.GetAsync("old-done", AccessContext.System, default)).ShouldBeNull();
        (await client.GetAsync("old-cancelled", AccessContext.System, default)).ShouldBeNull();
        (await client.GetAsync("old-faulted", AccessContext.System, default)).ShouldNotBeNull();
        (await client.GetAsync("recent-done", AccessContext.System, default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Resume_rewind_resets_the_cursor_and_re_runs()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAtAsync(store, "r1", cursor: 3, faultStep: "step3");

        int seenCursor = -1;
        var client = new SecuredWorkflowManagement(store, owner: "ops", resumer: (run, ct) =>
        {
            seenCursor = run.Cursor;
            return CompleteAndReport(run, ct);
        });

        (await client.ResumeAsync("r1", ResumeOptions.Rewind(targetCursor: 1), AccessContext.System, default)).ShouldBeTrue();

        seenCursor.ShouldBe(1);
        (await client.GetAsync("r1", AccessContext.System, default))!.Value.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task Resume_rewind_without_a_target_cursor_throws()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAtAsync(store, "r1", cursor: 3, faultStep: "step3");
        var client = new SecuredWorkflowManagement(store, owner: "ops", resumer: CompleteAndReport);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await client.ResumeAsync("r1", new ResumeOptions(ResumeMode.Rewind), AccessContext.System, default));
    }

    [TestMethod]
    public async Task Resume_skip_advances_past_the_faulted_step_and_records_outputs()
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{ "skipOutputs": { "ok": true } }"""u8.ToArray());
        await FaultRunAtAsync(store, "r1", cursor: 2, faultStep: "step2");

        int seenCursor = -1;
        bool sawSkippedOutputs = false;
        var client = new SecuredWorkflowManagement(store, owner: "ops", resumer: (run, ct) =>
        {
            seenCursor = run.Cursor;
            sawSkippedOutputs = run.TryGetStepOutputs("step2", out JsonElement o) && o.GetProperty("ok"u8).GetBoolean();
            return CompleteAndReport(run, ct);
        });

        (await client.ResumeAsync("r1", ResumeOptions.Skip(doc.RootElement.GetProperty("skipOutputs"u8)), AccessContext.System, default)).ShouldBeTrue();

        seenCursor.ShouldBe(3);
        sawSkippedOutputs.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Resume_state_patch_fixes_an_input_then_retries_the_faulted_step()
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{ "inputs": { "x": 1 }, "patch": [ { "op": "replace", "path": "/inputs/x", "value": 2 } ] }"""u8.ToArray());
        await FaultRunAtAsync(store, "r1", cursor: 1, faultStep: "step1", inputs: doc.RootElement.GetProperty("inputs"u8));

        int seenCursor = -1;
        int seenX = -1;
        var client = new SecuredWorkflowManagement(store, owner: "ops", resumer: (run, ct) =>
        {
            seenCursor = run.Cursor;
            seenX = run.Inputs.GetProperty("x"u8).GetInt32();
            return CompleteAndReport(run, ct);
        });

        (await client.ResumeAsync("r1", ResumeOptions.StatePatch(doc.RootElement.GetProperty("patch"u8)), AccessContext.System, default)).ShouldBeTrue();

        seenCursor.ShouldBe(1); // retry the faulted step
        seenX.ShouldBe(2);      // with the patched input
    }

    [TestMethod]
    public async Task Resume_state_patch_with_a_failing_patch_returns_false_and_does_not_resume()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        using ParsedJsonDocument<JsonElement> patch = ParsedJsonDocument<JsonElement>.Parse(
            """[ { "op": "test", "path": "/inputs/missing", "value": 1 } ]"""u8.ToArray());

        bool invoked = false;
        var client = new SecuredWorkflowManagement(store, owner: "ops", resumer: (_, _) =>
        {
            invoked = true;
            return new ValueTask<WorkflowRunResultKind>(WorkflowRunResultKind.Completed);
        });

        (await client.ResumeAsync("r1", ResumeOptions.StatePatch(patch.RootElement), AccessContext.System, default)).ShouldBeFalse();
        invoked.ShouldBeFalse();
        (await client.GetAsync("r1", AccessContext.System, default))!.Value.Status.ShouldBe(WorkflowRunStatus.Faulted);
    }

    [TestMethod]
    public async Task Delete_removes_a_single_run_of_any_status()
    {
        var store = new InMemoryWorkflowStateStore();
        await CompleteRunAsync(store, "r1");
        var client = new SecuredWorkflowManagement(store, owner: "ops");

        (await client.DeleteAsync("r1", AccessContext.System, default)).ShouldBeTrue();
        (await client.GetAsync("r1", AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Delete_returns_false_for_an_unknown_run()
    {
        var client = new SecuredWorkflowManagement(new InMemoryWorkflowStateStore(), owner: "ops");

        (await client.DeleteAsync("nope", AccessContext.System, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task Delete_returns_false_when_another_owner_holds_the_lease()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        await store.AcquireLeaseAsync("r1", "worker-7", TimeSpan.FromMinutes(5), default);
        var client = new SecuredWorkflowManagement(store, owner: "ops");

        (await client.DeleteAsync("r1", AccessContext.System, default)).ShouldBeFalse();
        (await client.GetAsync("r1", AccessContext.System, default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Resume_emits_an_audit_span_and_a_counter()
    {
        using var telemetry = new RecordedTelemetry();
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        var client = new SecuredWorkflowManagement(store, owner: "ops", resumer: CompleteAndReport);

        (await client.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeTrue();

        telemetry.Sum("corvus.arazzo.workflows.resumed").ShouldBe(1);
        Activity span = telemetry.ActivitiesNamed("workflow.resume").ShouldHaveSingleItem();
        span.GetTagItem("corvus.arazzo.actor").ShouldBe("ops");
        span.GetTagItem("corvus.arazzo.resume_mode").ShouldBe("RetryFaultedStep");
        span.GetTagItem("corvus.arazzo.outcome").ShouldBe("resumed");
    }

    [TestMethod]
    public async Task Cancel_and_delete_emit_their_counters()
    {
        using var telemetry = new RecordedTelemetry();
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        await CompleteRunAsync(store, "r2");
        var client = new SecuredWorkflowManagement(store, owner: "ops");

        (await client.CancelAsync("r1", "abandoned", AccessContext.System, default)).ShouldBeTrue();
        (await client.DeleteAsync("r2", AccessContext.System, default)).ShouldBeTrue();

        telemetry.Sum("corvus.arazzo.workflows.cancelled").ShouldBe(1);
        telemetry.Sum("corvus.arazzo.workflows.deleted").ShouldBe(1);
        telemetry.ActivitiesNamed("workflow.cancel").ShouldHaveSingleItem();
        telemetry.ActivitiesNamed("workflow.delete").ShouldHaveSingleItem();
    }

    private static async ValueTask<WorkflowRunResultKind> CompleteAndReport(WorkflowRun run, CancellationToken ct)
    {
        await run.CompleteAsync(default, ct);
        return WorkflowRunResultKind.Completed;
    }

    private static async Task FaultRunAtAsync(InMemoryWorkflowStateStore store, string id, int cursor, string faultStep, JsonElement inputs = default, TimeProvider? clock = null)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", inputs, clock);
        if (cursor > 0)
        {
            // Advance the cursor to the faulting step before recording the fault (FaultAsync keeps the cursor).
            await run.CheckpointAsync(cursor, default);
        }

        await run.FaultAsync(faultStep, attempt: 1, "boom", default);
    }

    private static async Task FaultRunAsync(InMemoryWorkflowStateStore store, string id, TimeProvider? clock = null)
    {
        WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock);
        await run.FaultAsync("step1", attempt: 1, "boom", default);
    }

    private static async Task CompleteRunAsync(InMemoryWorkflowStateStore store, string id, TimeProvider? clock = null)
    {
        WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, clock);
        await run.CompleteAsync(default, default);
    }

    private static async Task CancelRunAsync(InMemoryWorkflowStateStore store, string id, TimeProvider? clock = null)
    {
        await FaultRunAsync(store, id, clock);
        var client = new SecuredWorkflowManagement(store, owner: "ops", timeProvider: clock);
        await client.CancelAsync(id, "test", AccessContext.System, default);
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}