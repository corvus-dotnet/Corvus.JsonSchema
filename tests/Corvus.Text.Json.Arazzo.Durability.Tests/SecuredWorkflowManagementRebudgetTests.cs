// <copyright file="SecuredWorkflowManagementRebudgetTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Re-budget on resume (ADR 0068): a run that faulted on its execution budget keeps the work it has done. A resume
/// resolves its budget again, as a start would, from its environment as it is now, and proceeds only if the run is
/// inside the result. The control plane writes the new budget. Nothing else moves a run's frozen budget.
/// </summary>
[TestClass]
public sealed class SecuredWorkflowManagementRebudgetTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task A_run_out_of_fuel_resumes_from_where_it_stopped_once_its_environment_allows_more()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var environments = new InMemoryEnvironmentStore(clock);
        await SetBudgetAsync(environments, "prod", """{"maxSteps":2}""", add: true);
        ExecutionBudget? seen = null;
        int cursorSeen = -1;
        var management = new SecuredWorkflowManagement(store, "ops", Resumer, clock, environments: environments);
        await FuelFaultedRunAsync(store, clock, "r1", new ExecutionBudget(2, TimeSpan.FromHours(24), 8, TimeSpan.FromHours(1), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));

        // Still held to two attempts, and it has made two: not resumable, and said so in advance.
        WorkflowRunDetail before = (await management.GetAsync("r1", AccessContext.System, default))!.Value;
        before.Rebudget!.Value.Resumable.ShouldBeFalse();
        (await management.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeFalse();
        seen.ShouldBeNull();

        // The environment's administrator raises the limit. The run is inside it, so it resumes at the cursor it
        // stopped at, under the new budget, with the two attempts it made still counted.
        await SetBudgetAsync(environments, "prod", """{"maxSteps":10}""", add: false);
        (await management.GetAsync("r1", AccessContext.System, default))!.Value.Rebudget!.Value.Resumable.ShouldBeTrue();
        (await management.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeTrue();

        seen!.Value.MaxSteps.ShouldBe(10);
        cursorSeen.ShouldBe(2);
        using WorkflowCheckpointState? state = await management.LoadStateAsync("r1", AccessContext.System, default);
        state!.Budget!.Value.MaxSteps.ShouldBe(10);
        state.StepJournal.Count.ShouldBe(2);

        async ValueTask<WorkflowRunResultKind> Resumer(WorkflowRun run, CancellationToken ct)
        {
            seen = run.Budget;
            cursorSeen = run.Cursor;
            await run.CompleteAsync(default, ct);
            return WorkflowRunResultKind.Completed;
        }
    }

    [TestMethod]
    public async Task The_hand_off_to_a_runner_re_budgets_by_the_same_rule()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var environments = new InMemoryEnvironmentStore(clock);
        await SetBudgetAsync(environments, "prod", """{"maxSteps":2}""", add: true);
        var management = new SecuredWorkflowManagement(store, "ops", timeProvider: clock, environments: environments);
        await FuelFaultedRunAsync(store, clock, "r1", new ExecutionBudget(2, TimeSpan.FromHours(24), 8, TimeSpan.FromHours(1), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));

        (await management.RequestFaultedResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeFalse();

        await SetBudgetAsync(environments, "prod", """{"maxSteps":10}""", add: false);
        (await management.RequestFaultedResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeTrue();

        // The budget a claiming runner loads, and the coordinator takes as the run's identity, is the new one.
        using WorkflowCheckpointState? state = await management.LoadStateAsync("r1", AccessContext.System, default);
        state!.Budget!.Value.MaxSteps.ShouldBe(10);
    }

    [TestMethod]
    public async Task No_override_can_take_a_run_past_the_deployments_ceiling()
    {
        // The ceiling's wall clock is an hour. A run two hours old is past it whatever its environment says, because
        // an override only tightens: the run can be re-run, never resumed.
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var environments = new InMemoryEnvironmentStore(clock);
        await SetBudgetAsync(environments, "prod", """{"wallClockSeconds":60}""", add: true);
        var management = new SecuredWorkflowManagement(store, "ops", NeverResumer, clock, environments: environments, executionBudget: ExecutionBudget.CeilingFrom(wallClockSeconds: 3600));
        using (WorkflowRun run = WorkflowRun.CreateNew(store, "r1", "wf", default, "prod", clock, budget: new ExecutionBudget(10, TimeSpan.FromSeconds(60), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes)))
        {
            await run.CheckpointAsync(1, default);
            await run.FaultAsync("a", attempt: 1, ExecutionBudgetFault.Deadline, default);
        }

        clock.Advance(TimeSpan.FromHours(2));
        await SetBudgetAsync(environments, "prod", """{"wallClockSeconds":3600}""", add: false);

        (await management.GetAsync("r1", AccessContext.System, default))!.Value.Rebudget!.Value.Resumable.ShouldBeFalse();
        (await management.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_run_inside_a_raised_wall_clock_resumes()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var environments = new InMemoryEnvironmentStore(clock);
        await SetBudgetAsync(environments, "prod", """{"wallClockSeconds":60}""", add: true);
        bool resumed = false;
        var management = new SecuredWorkflowManagement(store, "ops", (run, ct) => { resumed = true; return ValueTask.FromResult(WorkflowRunResultKind.Suspended); }, clock, environments: environments);
        using (WorkflowRun run = WorkflowRun.CreateNew(store, "r1", "wf", default, "prod", clock, budget: new ExecutionBudget(10, TimeSpan.FromSeconds(60), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes)))
        {
            await run.CheckpointAsync(1, default);
            await run.FaultAsync("a", attempt: 1, ExecutionBudgetFault.Deadline, default);
        }

        clock.Advance(TimeSpan.FromMinutes(30));
        await SetBudgetAsync(environments, "prod", """{"wallClockSeconds":7200}""", add: false);

        (await management.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeTrue();
        resumed.ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_depth_fault_needs_a_deeper_cap_than_the_one_it_hit()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var environments = new InMemoryEnvironmentStore(clock);
        await SetBudgetAsync(environments, "prod", """{"maxSubWorkflowDepth":1}""", add: true);
        var management = new SecuredWorkflowManagement(store, "ops", (run, ct) => ValueTask.FromResult(WorkflowRunResultKind.Suspended), clock, environments: environments);
        using (WorkflowRun run = WorkflowRun.CreateNew(store, "r1", "wf", default, "prod", clock, budget: new ExecutionBudget(10, TimeSpan.FromHours(1), 1, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes)))
        {
            await run.CheckpointAsync(1, default);
            await run.FaultAsync("a", attempt: 1, ExecutionBudgetFault.Depth, default);
        }

        // Every other limit has room, and the same depth cap would only fault it again at the same place.
        (await management.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeFalse();

        await SetBudgetAsync(environments, "prod", """{"maxSubWorkflowDepth":3}""", add: false);
        (await management.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_run_that_did_not_fault_on_its_budget_keeps_its_frozen_budget_across_a_resume()
    {
        // The environment's budget changing does not move a run's bound. Only a budget fault is re-budgeted.
        var clock = new MutableClock(T0);
        var store = new InMemoryWorkflowStateStore(clock);
        var environments = new InMemoryEnvironmentStore(clock);
        await SetBudgetAsync(environments, "prod", """{"maxSteps":5}""", add: true);
        ExecutionBudget? seen = null;
        var management = new SecuredWorkflowManagement(store, "ops", (run, ct) => { seen = run.Budget; return ValueTask.FromResult(WorkflowRunResultKind.Suspended); }, clock, environments: environments);
        var frozen = new ExecutionBudget(5, TimeSpan.FromHours(24), 8, TimeSpan.FromHours(1), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes);
        using (WorkflowRun run = WorkflowRun.CreateNew(store, "r1", "wf", default, "prod", clock, budget: frozen))
        {
            await run.CheckpointAsync(1, default);
            await run.FaultAsync("a", attempt: 1, "boom", default);
        }

        await SetBudgetAsync(environments, "prod", """{"maxSteps":400}""", add: false);

        (await management.GetAsync("r1", AccessContext.System, default))!.Value.Rebudget.ShouldBeNull();
        (await management.ResumeAsync("r1", ResumeOptions.RetryFaultedStep, AccessContext.System, default)).ShouldBeTrue();
        seen.ShouldBe(frozen);
    }

    [TestMethod]
    public void A_truncated_journal_is_never_resumable()
    {
        // The journal's cap is the most fuel any budget may carry, and a truncated journal has lost the count.
        ExecutionBudget any = ExecutionBudget.Default;
        var facts = new CheckpointBudgetFacts(any, 500, JournalTruncated: true, BudgetFaulted: true);

        ExecutionBudgetFault.CanResumeUnder(any, any, ExecutionBudgetFault.Fuel, facts, T0, T0).ShouldBeFalse();
    }

    private static ValueTask<WorkflowRunResultKind> NeverResumer(WorkflowRun run, CancellationToken ct)
        => throw new InvalidOperationException("a run outside its budget must never be re-entered");

    private static async Task FuelFaultedRunAsync(InMemoryWorkflowStateStore store, TimeProvider clock, string id, ExecutionBudget budget)
    {
        using WorkflowRun run = WorkflowRun.CreateNew(store, id, "wf", default, "prod", clock, budget: budget);
        await run.BeginStepAsync("a", default);
        run.RecordStep("a", WorkflowStepStatus.Succeeded, 1, T0, T0);
        await run.BeginStepAsync("b", default);
        run.RecordStep("b", WorkflowStepStatus.Succeeded, 1, T0, T0);
        await run.CheckpointAsync(2, default);

        // The third attempt is the one the budget refuses, and the run faults itself before making it.
        await Should.ThrowAsync<Exception>(async () => await run.BeginStepAsync("c", default));
        run.Fault!.Value.Error.ShouldBe(ExecutionBudgetFault.Fuel);
    }

    private static async Task SetBudgetAsync(InMemoryEnvironmentStore environments, string name, string budgetJson, bool add)
    {
        using ParsedJsonDocument<JsonElement> seed = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{\"name\":\"" + name + "\",\"executionBudget\":" + budgetJson + "}"));
        using ParsedJsonDocument<Environment> draft = Environment.Draft(seed.RootElement.GetProperty("name"u8), default, default, default, executionBudget: seed.RootElement.GetProperty("executionBudget"u8));
        if (add)
        {
            (await environments.AddAsync(draft.RootElement, "ops", default)).Dispose();
        }
        else
        {
            (await environments.UpdateAsync(name, draft.RootElement, WorkflowEtag.None, "ops", AccessContext.System, default))!.Dispose();
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}