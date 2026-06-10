// <copyright file="WorkflowManagementClientTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for <see cref="WorkflowManagementClient"/> (the control plane, plan §11).</summary>
[TestClass]
public sealed class WorkflowManagementClientTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Get_returns_the_fault_detail_of_a_faulted_run()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        var client = new WorkflowManagementClient(store, owner: "ops");

        WorkflowRunDetail? detail = await client.GetAsync("r1", default);

        detail.ShouldNotBeNull();
        detail.Value.Status.ShouldBe(WorkflowRunStatus.Faulted);
        detail.Value.Fault!.Value.StepId.ShouldBe("step1");
        detail.Value.Fault!.Value.Error.ShouldBe("boom");
    }

    [TestMethod]
    public async Task Get_returns_null_for_an_unknown_run()
    {
        var client = new WorkflowManagementClient(new InMemoryWorkflowStateStore(), owner: "ops");
        (await client.GetAsync("nope", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task List_filters_by_status()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "faulted-1");
        await CompleteRunAsync(store, "done-1");
        var client = new WorkflowManagementClient(store, owner: "ops");

        WorkflowRunPage page = await client.ListAsync(new WorkflowQuery(WorkflowRunStatus.Faulted), default);

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

        var client = new WorkflowManagementClient(store, owner: "ops", resumer: Resumer);

        bool resumed = await client.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, default);

        resumed.ShouldBeTrue();
        seenStatus.ShouldBe(WorkflowRunStatus.Faulted);
        (await client.GetAsync("r1", default))!.Value.Status.ShouldBe(WorkflowRunStatus.Completed);

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
        var client = new WorkflowManagementClient(store, owner: "ops", resumer: (_, _) =>
        {
            invoked = true;
            return new ValueTask<WorkflowRunResultKind>(WorkflowRunResultKind.Completed);
        });

        (await client.ResumeAsync("done-1", ResumeOptions.RetryFaultedStep, default)).ShouldBeFalse();
        invoked.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Resume_without_a_resumer_throws()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        var client = new WorkflowManagementClient(store, owner: "ops");

        await Should.ThrowAsync<InvalidOperationException>(async () => await client.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, default));
    }

    [TestMethod]
    public async Task Cancel_marks_a_run_cancelled_and_keeps_the_fault_for_audit()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        var client = new WorkflowManagementClient(store, owner: "ops");

        (await client.CancelAsync("r1", "operator abandoned", default)).ShouldBeTrue();

        WorkflowRunDetail detail = (await client.GetAsync("r1", default))!.Value;
        detail.Status.ShouldBe(WorkflowRunStatus.Cancelled);
        detail.Fault!.Value.Error.ShouldBe("boom");
    }

    [TestMethod]
    public async Task Cancel_returns_false_for_a_terminal_run()
    {
        var store = new InMemoryWorkflowStateStore();
        await CompleteRunAsync(store, "done-1");
        var client = new WorkflowManagementClient(store, owner: "ops");

        (await client.CancelAsync("done-1", "too late", default)).ShouldBeFalse();
        (await client.GetAsync("done-1", default))!.Value.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task Cancel_returns_false_when_another_owner_holds_the_lease()
    {
        var store = new InMemoryWorkflowStateStore();
        await FaultRunAsync(store, "r1");
        await store.AcquireLeaseAsync("r1", "worker-7", TimeSpan.FromMinutes(5), default);
        var client = new WorkflowManagementClient(store, owner: "ops");

        (await client.CancelAsync("r1", "race", default)).ShouldBeFalse();
        (await client.GetAsync("r1", default))!.Value.Status.ShouldBe(WorkflowRunStatus.Faulted);
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

        var client = new WorkflowManagementClient(store, owner: "ops", timeProvider: clock);

        int purged = await client.PurgeAsync(new WorkflowPurgeQuery(T0 + TimeSpan.FromMinutes(30)), default);

        purged.ShouldBe(2);
        (await client.GetAsync("old-done", default)).ShouldBeNull();
        (await client.GetAsync("old-cancelled", default)).ShouldBeNull();
        (await client.GetAsync("old-faulted", default)).ShouldNotBeNull();
        (await client.GetAsync("recent-done", default)).ShouldNotBeNull();
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
        var client = new WorkflowManagementClient(store, owner: "ops", timeProvider: clock);
        await client.CancelAsync(id, "test", default);
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}