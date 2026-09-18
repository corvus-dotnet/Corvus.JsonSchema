// <copyright file="WorkflowRunBudgetTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The runner's cooperative half of the execution budget (ADR 0068): a budgeted <see cref="WorkflowRun"/> decides
/// before every attempt whether it has fuel and time for it, meters its sub-workflows against the same budget, caps
/// their nesting, and clamps a retry timer to the budget's ceiling.
/// </summary>
[TestClass]
public sealed class WorkflowRunBudgetTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task A_run_spends_exactly_its_fuel_then_faults_itself_before_the_next_attempt()
    {
        var store = new InMemoryWorkflowStateStore();
        var time = new TestTimeProvider(T0);
        using WorkflowRun run = NewRun(store, time, new ExecutionBudget(2, TimeSpan.FromHours(1), 8, TimeSpan.Zero));

        await Attempt(run, "a", time);
        await Attempt(run, "b", time);
        await run.CheckpointAsync(2, default);

        WorkflowBudgetExhaustedException thrown = await Should.ThrowAsync<WorkflowBudgetExhaustedException>(async () => await run.BeginStepAsync("c", default));
        thrown.Error.ShouldBe(ExecutionBudgetFault.Fuel);

        // The fault is durable before the unwind, recorded against the last journaled step (the control plane's own
        // rewrite shape), and the journal holds exactly the fuel: a save the coordinator's predicate does not refuse.
        run.Status.ShouldBe(WorkflowRunStatus.Faulted);
        WorkflowCheckpoint stored = (await store.LoadAsync(run.Address, default))!.Value;
        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex(stored.Utf8);
        index.Status.ShouldBe(WorkflowRunStatus.Faulted);
        index.ErrorType.ShouldBe(ExecutionBudgetFault.Fuel);
        using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(stored.Utf8))
        {
            state.Fault!.Value.StepId.ShouldBe("b");
            state.StepJournal!.Count.ShouldBe(2);
        }

        WorkflowCheckpointSerializer.TryReadBudgetFacts(stored.Utf8, out CheckpointBudgetFacts facts).ShouldBeTrue();
        facts.BudgetFaulted.ShouldBeTrue();
        ExecutionBudgetFault.Find(run.Budget!.Value, facts, T0, time.GetUtcNow()).ShouldBeNull();
    }

    [TestMethod]
    public async Task An_announced_attempt_holds_its_unit_of_fuel_until_it_is_journaled()
    {
        // The step that invokes a sub-workflow is open while the child attempts its steps. With one unit of fuel the
        // parent holds it, so the child's first attempt is refused before it reaches a source: granting both would
        // journal two entries against a fuel of one.
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), new ExecutionBudget(1, TimeSpan.FromHours(1), 8, TimeSpan.Zero));

        await run.BeginStepAsync("callChild", default);
        IWorkflowRun child = run.BeginSubWorkflow("callChild", "child")!;

        WorkflowBudgetExhaustedException thrown = await Should.ThrowAsync<WorkflowBudgetExhaustedException>(async () => await child.BeginStepAsync("getPet", default));
        thrown.Error.ShouldBe(ExecutionBudgetFault.Fuel);
    }

    [TestMethod]
    public async Task A_run_older_than_its_wall_clock_faults_itself_before_the_next_attempt()
    {
        var store = new InMemoryWorkflowStateStore();
        var time = new TestTimeProvider(T0);
        using WorkflowRun run = NewRun(store, time, new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.Zero));

        await Attempt(run, "a", time);
        time.Advance(TimeSpan.FromHours(2));

        WorkflowBudgetExhaustedException thrown = await Should.ThrowAsync<WorkflowBudgetExhaustedException>(async () => await run.BeginStepAsync("b", default));
        thrown.Error.ShouldBe(ExecutionBudgetFault.Deadline);
        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex((await store.LoadAsync(run.Address, default))!.Value.Utf8);
        index.Status.ShouldBe(WorkflowRunStatus.Faulted);
        index.ErrorType.ShouldBe(ExecutionBudgetFault.Deadline);
    }

    [TestMethod]
    public async Task A_sub_workflow_is_metered_into_the_root_journal_under_its_invoking_path()
    {
        var store = new InMemoryWorkflowStateStore();
        var time = new TestTimeProvider(T0);
        using WorkflowRun run = NewRun(store, time, new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.Zero));

        await run.BeginStepAsync("callChild", default);
        IWorkflowRun child = run.BeginSubWorkflow("callChild", "child")!;
        child.MetersOnly.ShouldBeTrue();
        await Attempt(child, "getPet", time);
        await child.BeginStepAsync("callGrandchild", default);
        IWorkflowRun grandchild = child.BeginSubWorkflow("callGrandchild", "grandchild")!;
        await Attempt(grandchild, "getPet", time);
        child.RecordStep("callGrandchild", WorkflowStepStatus.Succeeded, 1, T0, T0);
        run.RecordStep("callChild", WorkflowStepStatus.Succeeded, 1, T0, T0);
        await run.CheckpointAsync(1, default);

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize((await store.LoadAsync(run.Address, default))!.Value.Utf8);
        state.StepJournal!.Select(e => e.StepId).ShouldBe(["callChild/getPet", "callChild/callGrandchild/getPet", "callChild/callGrandchild", "callChild"]);

        // A metering scope holds no run state: the executor drops its run to null and only meters through it.
        Should.Throw<InvalidOperationException>(() => child.GetRetryCount("getPet"));
        await Should.ThrowAsync<InvalidOperationException>(async () => await child.CheckpointAsync(1, default));
    }

    [TestMethod]
    public async Task Nesting_past_the_depth_cap_faults_the_run_at_the_invoking_step_before_the_child_attempts_anything()
    {
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), new ExecutionBudget(10, TimeSpan.FromHours(1), 1, TimeSpan.Zero));

        await run.BeginStepAsync("a", default);
        IWorkflowRun child = run.BeginSubWorkflow("a", "w")!;
        await child.BeginStepAsync("b", default);
        IWorkflowRun grandchild = child.BeginSubWorkflow("b", "w")!;

        WorkflowBudgetExhaustedException thrown = await Should.ThrowAsync<WorkflowBudgetExhaustedException>(async () => await grandchild.BeginStepAsync("c", default));
        thrown.Error.ShouldBe(ExecutionBudgetFault.Depth);
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize((await store.LoadAsync(run.Address, default))!.Value.Utf8);
        state.Status.ShouldBe(WorkflowRunStatus.Faulted);
        state.Fault!.Value.Error.ShouldBe(ExecutionBudgetFault.Depth);
        state.Fault!.Value.StepId.ShouldBe("a/b");
    }

    [TestMethod]
    public async Task A_retry_timer_is_clamped_to_the_budgets_ceiling()
    {
        var store = new InMemoryWorkflowStateStore();
        var time = new TestTimeProvider(T0);
        using WorkflowRun run = NewRun(store, time, new ExecutionBudget(10, TimeSpan.FromDays(7), 8, TimeSpan.FromHours(1)));

        WorkflowWait wait = await run.SuspendForTimerAsync(0, TimeSpan.FromHours(5), default);
        wait.DueAt.ShouldBe(T0 + TimeSpan.FromHours(1));

        WorkflowWait within = await run.SuspendForTimerAsync(0, TimeSpan.FromMinutes(5), default);
        within.DueAt.ShouldBe(T0 + TimeSpan.FromMinutes(5));
    }

    [TestMethod]
    public async Task A_run_without_a_budget_is_unmetered()
    {
        var store = new InMemoryWorkflowStateStore();
        var time = new TestTimeProvider(T0);
        using WorkflowRun run = NewRun(store, time, budget: null);

        for (int i = 0; i < ExecutionBudget.MaxStepsCeiling + 10; i++)
        {
            await Attempt(run, "loop", time);
        }

        time.Advance(TimeSpan.FromDays(400));
        await run.BeginStepAsync("loop", default);
        run.BeginSubWorkflow("loop", "child").ShouldBeNull();
        (await run.SuspendForTimerAsync(0, TimeSpan.FromDays(30), default)).DueAt.ShouldBe(time.GetUtcNow() + TimeSpan.FromDays(30));
    }

    [TestMethod]
    public async Task The_execution_seam_reports_a_budget_exhausted_advance_as_a_clean_fault()
    {
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), new ExecutionBudget(1, TimeSpan.FromHours(1), 8, TimeSpan.Zero));

        WorkflowRunResultKind outcome = await HostedWorkflowExecution.RunAsync(
            new LoopingHostedWorkflow(),
            (_, _) => new WorkflowTransports(new Dictionary<string, IApiTransport>(), new Dictionary<string, IMessageTransport>()),
            run,
            default);

        outcome.ShouldBe(WorkflowRunResultKind.Faulted);
        run.Fault!.Value.Error.ShouldBe(ExecutionBudgetFault.Fuel);
    }

    private static WorkflowRun NewRun(InMemoryWorkflowStateStore store, TimeProvider time, ExecutionBudget? budget)
        => WorkflowRun.CreateNew(store, "0123456789abcdef0123456789abcdef", "wf", default, TestAddresses.Development, time, budget: budget);

    private static async ValueTask Attempt(IWorkflowRun scope, string stepId, TestTimeProvider time)
    {
        await scope.BeginStepAsync(stepId, default);
        scope.RecordStep(stepId, WorkflowStepStatus.Succeeded, 1, time.GetUtcNow(), time.GetUtcNow());
    }

    // An executor that loops forever the way a mutual goto does: announce an attempt, journal it, checkpoint, again.
    private sealed class LoopingHostedWorkflow : IHostedWorkflow
    {
        public WorkflowDescriptor Descriptor { get; } = new("wf", [], []);

        public async ValueTask<WorkflowRunResultKind> RunAsync(
            IReadOnlyDictionary<string, IApiTransport> apiTransports,
            IReadOnlyDictionary<string, IMessageTransport> messageTransports,
            JsonWorkspace workspace,
            JsonElement inputs,
            IWorkflowRun run,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                await run.BeginStepAsync("loop", cancellationToken);
                run.RecordStep("loop", WorkflowStepStatus.Succeeded, 1, T0, T0);
                await run.CheckpointAsync(0, cancellationToken);
            }
        }
    }
}