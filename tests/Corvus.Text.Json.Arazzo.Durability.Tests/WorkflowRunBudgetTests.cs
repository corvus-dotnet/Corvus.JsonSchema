// <copyright file="WorkflowRunBudgetTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Internal;
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
        using WorkflowRun run = NewRun(store, time, new ExecutionBudget(2, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));

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
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), new ExecutionBudget(1, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));

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
        using WorkflowRun run = NewRun(store, time, new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));

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
        using WorkflowRun run = NewRun(store, time, new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));

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
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), new ExecutionBudget(10, TimeSpan.FromHours(1), 1, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));

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
        using WorkflowRun run = NewRun(store, time, new ExecutionBudget(10, TimeSpan.FromDays(7), 8, TimeSpan.FromHours(1), ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));

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
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), new ExecutionBudget(1, TimeSpan.FromHours(1), 8, TimeSpan.Zero, ExecutionBudget.DefaultStepTimeout, ExecutionBudget.DefaultMaxResponseBytes));

        WorkflowRunResultKind outcome = await HostedWorkflowExecution.RunAsync(
            new LoopingHostedWorkflow(),
            (_, _) => new WorkflowTransports(new Dictionary<string, IApiTransport>(), new Dictionary<string, IMessageTransport>()),
            run,
            default);

        outcome.ShouldBe(WorkflowRunResultKind.Faulted);
        run.Fault!.Value.Error.ShouldBe(ExecutionBudgetFault.Fuel);
    }

    [TestMethod]
    public async Task The_execution_seam_faults_a_run_whose_executor_throws_and_keeps_the_message_off_the_record()
    {
        // Left to propagate, the exception reaches the host with the run still live: the lease is released, the run is
        // claimed again and the same step throws again. The attempt was announced and never journaled, so that loop is
        // outside the run's fuel and nothing ends it.
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), ExecutionBudget.Default);

        WorkflowRunResultKind outcome = await HostedWorkflowExecution.RunAsync(
            new ThrowingHostedWorkflow(new InvalidOperationException("db-host-17.internal refused the connection")),
            NoTransports,
            run,
            default);

        outcome.ShouldBe(WorkflowRunResultKind.Faulted);
        run.Status.ShouldBe(WorkflowRunStatus.Faulted);
        run.Fault!.Value.Error.ShouldBe(WorkflowExecutorFault.Unhandled);

        // Durable, not just in memory: the host that claims the run next sees it finished.
        using WorkflowRun? reloaded = await WorkflowRun.ResumeAsync(store, run.Address, default);
        reloaded!.Status.ShouldBe(WorkflowRunStatus.Faulted);
        reloaded.Fault!.Value.Error.ShouldBe("executor-unhandled");
    }

    [TestMethod]
    public async Task The_execution_seam_discloses_the_message_only_when_asked_as_a_draft_run_does()
    {
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), ExecutionBudget.Default);

        await HostedWorkflowExecution.RunAsync(new ThrowingHostedWorkflow(new InvalidOperationException("the reason")), NoTransports, run, discloseUnhandledError: true, default);

        run.Fault!.Value.Error.ShouldBe("the reason");
    }

    [TestMethod]
    public async Task The_execution_seam_lets_the_callers_cancellation_through_and_leaves_the_run_live()
    {
        // Cancellation is how a host shuts down. The run is not at fault and resumes elsewhere.
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), ExecutionBudget.Default);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () => await HostedWorkflowExecution.RunAsync(
            new ThrowingHostedWorkflow(new OperationCanceledException(cancelled.Token)), NoTransports, run, cancelled.Token));

        run.Fault.ShouldBeNull();
    }

    [TestMethod]
    public async Task The_execution_seam_lets_a_persistence_failure_through_and_does_not_try_to_persist_a_fault()
    {
        // A lost lease, a refused checkpoint or a store outage cannot be answered by saving a fault. It is the host's to
        // retry from the last durable checkpoint.
        var store = new FailingSaveStore(new InMemoryWorkflowStateStore());
        using WorkflowRun run = WorkflowRun.CreateNew(store, "0123456789abcdef0123456789abcdef", "wf", default, TestAddresses.Development, new TestTimeProvider(T0), budget: ExecutionBudget.Default);

        await Should.ThrowAsync<IOException>(async () => await HostedWorkflowExecution.RunAsync(new CheckpointingHostedWorkflow(), NoTransports, run, default));

        store.Saves.ShouldBe(1);
    }

    [TestMethod]
    public async Task The_execution_seam_faults_a_run_whose_executor_is_refused_and_keeps_the_refusal_off_the_record()
    {
        // Resolution is before the executor, so before anything the executor's own net catches. A refusal is the same
        // answer on every attempt: left to propagate, the run stays live and is claimed and refused again every poll.
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), ExecutionBudget.Default);
        var resolver = new ThrowingResolver(new WorkflowExecutorUnresolvableException("version 7 of 'wf' has stored hash abc and recomputed hash def"));

        WorkflowRunResultKind outcome = await HostedWorkflowExecution.ResolveAndRunAsync(resolver, NoTransports, run, default);

        outcome.ShouldBe(WorkflowRunResultKind.Faulted);
        run.Fault!.Value.Error.ShouldBe(WorkflowExecutorFault.Unresolvable);

        // Durable, so the host that would have claimed it next finds it finished.
        using WorkflowRun? reloaded = await WorkflowRun.ResumeAsync(store, run.Address, default);
        reloaded!.Status.ShouldBe(WorkflowRunStatus.Faulted);
        reloaded.Fault!.Value.Error.ShouldBe("executor-unresolvable");
    }

    [TestMethod]
    public async Task The_execution_seam_treats_the_loaders_verification_failure_as_a_refusal()
    {
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), ExecutionBudget.Default);

        await HostedWorkflowExecution.ResolveAndRunAsync(new ThrowingResolver(new WorkflowExecutorLoadException("assembly digest mismatch")), NoTransports, run, default);

        run.Fault!.Value.Error.ShouldBe(WorkflowExecutorFault.Unresolvable);
    }

    [TestMethod]
    public async Task The_execution_seam_lets_an_artifact_source_failure_through_and_leaves_the_run_live()
    {
        // A catalog that cannot be reached is not a refusal. It may pass, so the run is not ended over it.
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), ExecutionBudget.Default);

        await Should.ThrowAsync<HttpRequestException>(async () => await HostedWorkflowExecution.ResolveAndRunAsync(
            new ThrowingResolver(new HttpRequestException("the catalog is unreachable")), NoTransports, run, default));

        run.Fault.ShouldBeNull();
        (await store.LoadAsync(run.Address, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_execution_seam_lets_a_refusal_through_when_the_caller_is_cancelling()
    {
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), ExecutionBudget.Default);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Should.ThrowAsync<WorkflowExecutorUnresolvableException>(async () => await HostedWorkflowExecution.ResolveAndRunAsync(
            new ThrowingResolver(new WorkflowExecutorUnresolvableException("refused")), NoTransports, run, cancelled.Token));

        run.Fault.ShouldBeNull();
    }

    [TestMethod]
    public async Task The_execution_seam_does_not_overwrite_a_finished_run_with_a_refusal()
    {
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), ExecutionBudget.Default);
        await run.FaultAsync("step", attempt: 1, "the first fault", default);

        await Should.ThrowAsync<WorkflowExecutorUnresolvableException>(async () => await HostedWorkflowExecution.ResolveAndRunAsync(
            new ThrowingResolver(new WorkflowExecutorUnresolvableException("refused")), NoTransports, run, default));

        run.Fault!.Value.Error.ShouldBe("the first fault");
    }

    [TestMethod]
    public async Task The_execution_seam_faults_a_run_whose_transports_cannot_be_bound()
    {
        // Binding is before the executor too. A source with no binding is refused the same way on every attempt.
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), ExecutionBudget.Default);
        var hosted = new SeenTransports();

        WorkflowRunResultKind outcome = await HostedWorkflowExecution.RunAsync(
            hosted,
            (_, _) => throw new WorkflowTransportBindingException("workflow 'wf' calls source 'pets', which has no binding in 'production'"),
            run,
            default);

        outcome.ShouldBe(WorkflowRunResultKind.Faulted);
        hosted.Transports.ShouldBeNull();
        using WorkflowRun? reloaded = await WorkflowRun.ResumeAsync(store, run.Address, default);
        reloaded!.Status.ShouldBe(WorkflowRunStatus.Faulted);
        reloaded.Fault!.Value.Error.ShouldBe("transport-unbound");
    }

    [TestMethod]
    public async Task The_execution_seam_bounds_every_transport_a_binder_returns_with_the_runs_budget()
    {
        // ADR 0068 piece 4: applied here and not by the binder, because there are many binders and every run comes
        // through this seam. The bounded transport replaces the binder's, so it is the one run through and disposed.
        var store = new InMemoryWorkflowStateStore();
        var budget = new ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.Zero, TimeSpan.FromSeconds(7), 2048);
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), budget);
        var original = new BoundableTransport();
        var seen = new SeenTransports();

        await HostedWorkflowExecution.RunAsync(
            seen,
            (_, _) => new WorkflowTransports(new Dictionary<string, IApiTransport> { ["pets"] = original }, new Dictionary<string, IMessageTransport>()),
            run,
            default);

        original.RequestedTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        original.RequestedMaxResponseLength.ShouldBe(2048);
        seen.Transports!["pets"].ShouldBeSameAs(original.Bounded);
        original.Bounded!.Disposed.ShouldBeTrue();
    }

    [TestMethod]
    public async Task The_execution_seam_bounds_an_unbudgeted_run_with_the_default_budget()
    {
        // The scheduler's run carries no budget. Its requests are still never unbounded.
        var store = new InMemoryWorkflowStateStore();
        using WorkflowRun run = NewRun(store, new TestTimeProvider(T0), budget: null);
        var original = new BoundableTransport();

        await HostedWorkflowExecution.RunAsync(
            new SeenTransports(),
            (_, _) => new WorkflowTransports(new Dictionary<string, IApiTransport> { ["pets"] = original }, new Dictionary<string, IMessageTransport>()),
            run,
            default);

        original.RequestedTimeout.ShouldBe(ExecutionBudget.DefaultStepTimeout);
        original.RequestedMaxResponseLength.ShouldBe(ExecutionBudget.DefaultMaxResponseBytes);
    }

    private static WorkflowTransports NoTransports(WorkflowDescriptor descriptor, SecurityTagSet runTags)
        => new(new Dictionary<string, IApiTransport>(), new Dictionary<string, IMessageTransport>());

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

    // An executor that announces an attempt and then fails in a way nothing in the workflow handles.
    private sealed class ThrowingHostedWorkflow(Exception failure) : IHostedWorkflow
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
            await run.BeginStepAsync("call", cancellationToken);
            throw failure;
        }
    }

    // A resolver that fails the way the named cause does, and would on every attempt.
    private sealed class ThrowingResolver(Exception failure) : IHostedWorkflowResolver
    {
        public ValueTask<IHostedWorkflow> ResolveAsync(WorkflowRun run, CancellationToken cancellationToken) => throw failure;

        public ValueTask PrepareAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    // An executor that does nothing but checkpoint, so the only thing that can fail is the save.
    private sealed class CheckpointingHostedWorkflow : IHostedWorkflow
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
            await run.CheckpointAsync(0, cancellationToken);
            return WorkflowRunResultKind.Completed;
        }
    }

    // A store whose every save fails the way an outage does, counting the attempts made on it.
    private sealed class FailingSaveStore(IWorkflowCheckpointStore inner) : IWorkflowCheckpointStore
    {
        public int Saves { get; private set; }

        public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunAddress address, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
        {
            this.Saves++;
            throw new IOException("the store is unreachable");
        }

        public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunAddress address, CancellationToken cancellationToken)
            => inner.LoadAsync(address, cancellationToken);
    }

    // An executor that completes at once, keeping the transports it was handed.
    private sealed class SeenTransports : IHostedWorkflow
    {
        public WorkflowDescriptor Descriptor { get; } = new("wf", ["pets"], []);

        public IReadOnlyDictionary<string, IApiTransport>? Transports { get; private set; }

        public ValueTask<WorkflowRunResultKind> RunAsync(
            IReadOnlyDictionary<string, IApiTransport> apiTransports,
            IReadOnlyDictionary<string, IMessageTransport> messageTransports,
            JsonWorkspace workspace,
            JsonElement inputs,
            IWorkflowRun run,
            CancellationToken cancellationToken)
        {
            this.Transports = apiTransports;
            return new(WorkflowRunResultKind.Completed);
        }
    }

    // A transport that records the bounds it was asked for and hands back a distinct bounded instance.
    private sealed class BoundableTransport : IBoundableApiTransport
    {
        public TimeSpan? RequestedTimeout { get; private set; }

        public long? RequestedMaxResponseLength { get; private set; }

        public BoundableTransport? Bounded { get; private set; }

        public bool Disposed { get; private set; }

        public IApiTransport WithBounds(TimeSpan? requestTimeout, long? maxResponseLength)
        {
            this.RequestedTimeout = requestTimeout;
            this.RequestedMaxResponseLength = maxResponseLength;
            return this.Bounded = new BoundableTransport();
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => throw new NotSupportedException();

        public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TBody : struct, IJsonElement<TBody>
            where TResponse : struct, IApiResponse<TResponse>
            => throw new NotSupportedException();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => throw new NotSupportedException();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            this.Disposed = true;
            return default;
        }
    }
}