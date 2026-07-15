// <copyright file="TracingWorkflowRun.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// The simulator's <see cref="IWorkflowRun"/>: an in-memory run that records the trace instead of
/// persisting checkpoints, enforces the step budget, and halts the replay at the stop condition.
/// </summary>
/// <remarks>
/// <para>
/// The durable executor's state indices ARE the workflow's step-array indices (the generated
/// <c>switch</c> emits <c>case i:</c> per step in declaration order), so <see cref="CheckpointAsync"/>'s
/// cursor names the NEXT step directly — "pause before step X" is a control-flow throw from the
/// checkpoint after the previous step completed, with nothing half-executed.
/// </para>
/// <para>
/// Every step completion records a <see cref="SimulatedStepRecord"/> stamped with the exchange range
/// it produced (the mock transport's exchange count at the two boundaries).
/// </para>
/// <para>
/// A workflowId-bound step executes its sub-workflow through a CHILD SCOPE (§15-8a):
/// <see cref="BeginSubWorkflow"/> returns a recorder for the child that SHARES the root's step
/// budget, virtual clock, stop conditions, exchange cursor, and arrival counters (root-owned and
/// cumulative across child invocations — design §10 F5), while keeping its own step records,
/// outputs, retry counts, and an EMPTY step-output-override map (a root override matching a child's
/// bare stepId must never fire inside the child — §10 F6). Child scopes are created fresh per
/// invocation: a suspension that re-enters the root executor replays the child from its start
/// (§8.2's replay model one level down). Message delivery forwards root→child so a trigger injected
/// on the root reaches a receive step inside the child. Cross-source sub-workflows (whose shape
/// lives in another document) run with an empty step-id map: recorded scopes without step
/// attribution — declared out of scope for v1 (§10 F10).
/// </para>
/// </remarks>
internal sealed class TracingWorkflowRun : IWorkflowRun
{
    /// <summary>
    /// The nesting depth cap (decision §8.2): a deliberate constant well past any legitimate
    /// composition depth, so runaway mutual recursion between workflows exhausts predictably
    /// instead of overflowing the stack. The demo mock aligns to this value (slice E).
    /// </summary>
    internal const int MaxSubWorkflowDepth = 8;

    private static readonly IReadOnlyDictionary<string, StepOutputOverride> EmptyOverrides = new Dictionary<string, StepOutputOverride>();
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyStepIdMap = new Dictionary<string, IReadOnlyList<string>>();

    private readonly TracingWorkflowRun root;
    private readonly string pathPrefix;
    private readonly int depth;
    private readonly IReadOnlyList<string> stepIds;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> subWorkflowStepIds;
    private readonly SimulationStop stop;
    private readonly int maxSteps;
    private readonly ManualTimeProvider clock;
    private readonly MockApiTransport transport;
    private readonly IReadOnlyDictionary<string, StepOutputOverride> overrides;
    private readonly Dictionary<string, JsonElement> outputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> retryCounts = new(StringComparer.Ordinal);
    private readonly List<SimulatedStepRecord> steps = [];
    private readonly string? workflowId;
    private int localStepsExecuted;
    private TracingWorkflowRun? activeChild;
    private string? activeChildStepId;

    // Root-owned shared state: the budget, the exchange cursor, the composed-path arrival counters,
    // the recorded wait, the delivered message, and the (scoped) paused-before path. Child scopes
    // read and write these through the root so one budget, one clock policy, and one exchange space
    // hold across nesting.
    private readonly Dictionary<string, int> arrivals = new(StringComparer.Ordinal);
    private int stepsExecuted;
    private int exchangeMark;
    private WorkflowWait? recordedWait;
    private string? pausedBefore;
    private JsonElement deliveredMessage;
    private bool hasDeliveredMessage;
    private int currentState;

    public TracingWorkflowRun(
        IReadOnlyList<string> stepIds,
        SimulationStop stop,
        int maxSteps,
        ManualTimeProvider clock,
        MockApiTransport transport,
        IReadOnlyDictionary<string, StepOutputOverride> overrides,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? subWorkflowStepIds = null)
    {
        this.root = this;
        this.pathPrefix = string.Empty;
        this.depth = 0;
        this.stepIds = stepIds;
        this.subWorkflowStepIds = subWorkflowStepIds ?? EmptyStepIdMap;
        this.stop = stop;
        this.maxSteps = maxSteps;
        this.clock = clock;
        this.transport = transport;
        this.overrides = overrides;

        // The stop condition may name the FIRST step (pause before anything runs).
        this.CheckStop(0, throwBeforeAnything: false);
    }

    private TracingWorkflowRun(TracingWorkflowRun root, string pathPrefix, int depth, IReadOnlyList<string> stepIds, string workflowId)
    {
        this.root = root;
        this.pathPrefix = pathPrefix;
        this.depth = depth;
        this.stepIds = stepIds;
        this.workflowId = workflowId;
        this.subWorkflowStepIds = root.subWorkflowStepIds;
        this.stop = root.stop;
        this.maxSteps = root.maxSteps;
        this.clock = root.clock;
        this.transport = root.transport;
        this.overrides = EmptyOverrides;
    }

    /// <summary>Gets the executed-step records for THIS scope, in execution order (the root's are the trace).</summary>
    public IReadOnlyList<SimulatedStepRecord> Steps => this.steps;

    /// <summary>Gets the total step executions so far across every scope (the one global budget's measure).</summary>
    public int StepsExecuted => this.root.stepsExecuted;

    /// <summary>Gets the workflow outputs recorded by <see cref="CompleteAsync"/> on this scope.</summary>
    public JsonElement WorkflowOutputs { get; private set; }

    /// <summary>Gets the fault recorded by <see cref="FaultAsync"/> on this scope, if any.</summary>
    public WorkflowFault? RecordedFault { get; private set; }

    /// <summary>Gets the wait recorded by the last suspend in ANY scope (a child suspension bubbles), if any.</summary>
    public WorkflowWait? RecordedWait => this.root.recordedWait;

    /// <summary>Gets the scoped step path whose arrival satisfied the stop condition, when the run paused.</summary>
    public string? PausedBefore => this.root.pausedBefore;

    /// <summary>Gets the step currently executing in this scope, when one is (fault attribution for executor crashes).</summary>
    public string? CurrentStepId => this.currentState >= 0 && this.currentState < this.stepIds.Count ? this.stepIds[this.currentState] : null;

    /// <inheritdoc/>
    public int Cursor { get; private set; }

    /// <inheritdoc/>
    public string? CorrelationId => null;

    /// <inheritdoc/>
    public Dictionary<string, byte[]> CorrelationTokens { get; } = new(StringComparer.Ordinal);

    /// <summary>Synchronises the boundary tracker before each executor (re)invocation.</summary>
    public void BeginInvocation()
    {
        this.currentState = this.Cursor;
        this.root.recordedWait = null;
    }

    /// <summary>Hands the next matching trigger's payload to the resumed correlated-receive step.</summary>
    public void DeliverMessage(in JsonElement payload)
    {
        this.root.deliveredMessage = payload;
        this.root.hasDeliveredMessage = true;
    }

    /// <inheritdoc/>
    public bool TryGetStepOutputs(string stepId, out JsonElement outputs) => this.outputs.TryGetValue(stepId, out outputs);

    /// <inheritdoc/>
    public int GetRetryCount(string stepId) => this.retryCounts.GetValueOrDefault(stepId);

    /// <inheritdoc/>
    public void SetStepOutputs(string stepId, in JsonElement outputs) => this.outputs[stepId] = outputs;

    /// <inheritdoc/>
    public void SetRetryCount(string stepId, int count) => this.retryCounts[stepId] = count;

    /// <inheritdoc/>
    public IWorkflowRun? BeginSubWorkflow(string stepId, string subWorkflowId)
    {
        if (this.depth + 1 > MaxSubWorkflowDepth)
        {
            // Depth is a budget-class limit: unwind to BudgetExhausted rather than overflowing.
            throw new SimulationBudgetException();
        }

        // A sub-workflow defined in another source document has no local shape: the child scope
        // still shares the clock/budget surfaces, but records without step attribution (§10 F10).
        IReadOnlyList<string> childStepIds = this.subWorkflowStepIds.TryGetValue(subWorkflowId, out IReadOnlyList<string>? ids) ? ids : [];
        var child = new TracingWorkflowRun(this.root, this.pathPrefix + stepId + "/", this.depth + 1, childStepIds, subWorkflowId);

        // The last invocation's records stand (design §10 F2): a suspension replay re-invokes the
        // step, and the fresh scope replaces the stale one here. Register BEFORE the first-step
        // stop check: a scoped stop naming the child's first step unwinds immediately, and the
        // finalization walk needs the chain to materialize the partial ancestor records (§3.5).
        this.activeChild = child;
        this.activeChildStepId = stepId;
        child.CheckStop(0, throwBeforeAnything: true);
        return child;
    }

    /// <inheritdoc/>
    public ValueTask CheckpointAsync(int cursor, CancellationToken cancellationToken)
    {
        this.RecordStepBoundary(faulted: false);
        this.Cursor = cursor;
        this.currentState = cursor;
        this.CheckStop(cursor, throwBeforeAnything: true);
        if (cursor >= 0 && cursor < this.stepIds.Count && this.overrides.ContainsKey(this.stepIds[cursor]))
        {
            // §15 8b: the next step is overridden — it must not execute. The executor's internal
            // state has already moved on, so unwind the invocation; the driver consumes the skip
            // (SkipOverriddenSteps) and re-enters at the advanced cursor, exactly as a durable
            // Skip resume mutates the checkpoint and re-enters the executor.
            throw new SimulationSkipException();
        }

        return default;
    }

    /// <inheritdoc/>
    public ValueTask CompleteAsync(JsonElement outputs, CancellationToken cancellationToken)
    {
        this.RecordStepBoundary(faulted: false);
        this.WorkflowOutputs = outputs;
        return default;
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowWait> SuspendForTimerAsync(int cursor, TimeSpan delay, CancellationToken cancellationToken)
    {
        this.RecordStepBoundary(faulted: false);
        this.Cursor = cursor;
        var wait = WorkflowWait.Timer(this.clock.GetUtcNow() + delay);
        this.root.recordedWait = wait;
        return new(wait);
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowWait> SuspendForMessageAsync(int cursor, string channel, string? correlationId, CancellationToken cancellationToken)
    {
        // A message wait is the step ITSELF waiting (not a completed-step boundary): record no step,
        // just the wait — the resumed invocation re-enters the same state and completes the step.
        this.Cursor = cursor;
        var wait = WorkflowWait.Message(channel, correlationId);
        this.root.recordedWait = wait;
        return new(wait);
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowFault> FaultAsync(string stepId, int attempt, string error, CancellationToken cancellationToken)
    {
        this.RecordStepBoundary(faulted: true, faultedStepId: stepId, faultedAttempt: attempt);
        var fault = new WorkflowFault(stepId, attempt, error, this.clock.GetUtcNow());
        this.RecordedFault = fault;
        return new(fault);
    }

    /// <inheritdoc/>
    public bool TryTakeDeliveredMessage(out JsonElement payload)
    {
        if (this.root.hasDeliveredMessage)
        {
            payload = this.root.deliveredMessage;
            this.root.hasDeliveredMessage = false;
            return true;
        }

        payload = default;
        return false;
    }

    /// <summary>
    /// Consumes §15 8b step-output overrides at the cursor before an executor (re)invocation: each
    /// overridden step is recorded as skipped — no exchange, attempt 0, its PROVIDED outputs staged
    /// on the run so the resumed executor restores them for downstream references — and the cursor
    /// advances along the override's criteria-less success route. The replay analogue of the durable
    /// engine's Skip resume (outputs recorded, cursor moved past, executor re-entered). A skipped
    /// step counts against the budget and its successor still honours the stop condition, matching
    /// the mock's ordering (breakpoint first, budget second, override third).
    /// </summary>
    public void SkipOverriddenSteps()
    {
        while (this.Cursor >= 0 && this.Cursor < this.stepIds.Count
            && this.overrides.TryGetValue(this.stepIds[this.Cursor], out StepOutputOverride stepOverride))
        {
            string stepId = this.stepIds[this.Cursor];
            this.localStepsExecuted++;
            if (++this.root.stepsExecuted > this.maxSteps)
            {
                throw new SimulationBudgetException();
            }

            if (stepOverride.Outputs.ValueKind is not JsonValueKind.Undefined)
            {
                this.outputs[stepId] = stepOverride.Outputs;
            }

            this.steps.Add(new SimulatedStepRecord
            {
                StepId = stepId,
                Attempt = 0,
                Skipped = true,
                Outputs = stepOverride.Outputs,
                FirstExchange = this.root.exchangeMark,
                ExchangeCount = 0,
                ActionTaken = stepOverride.Action,
            });

            this.Cursor = stepOverride.NextState;
            this.currentState = this.Cursor;
            this.CheckStop(this.Cursor, throwBeforeAnything: true);
        }
    }

    /// <summary>Records the step whose execution just reached a run boundary (checkpoint/complete/fault/timer-suspend).</summary>
    private void RecordStepBoundary(bool faulted, string? faultedStepId = null, int faultedAttempt = 0)
    {
        if (this.currentState < 0 || this.currentState >= this.stepIds.Count)
        {
            return; // the terminal outputs state — CompleteAsync after the last checkpoint
        }

        string stepId = faultedStepId ?? this.stepIds[this.currentState];
        this.localStepsExecuted++;
        if (++this.root.stepsExecuted > this.maxSteps)
        {
            throw new SimulationBudgetException();
        }

        // A workflowId-bound step's boundary attaches its sub-workflow's collected records as the
        // record's nested trace (§15-8a): terminal here, so the child either completed or faulted.
        SimulatedSubTrace? subTrace = null;
        if (this.activeChild is { } child && stepId == this.activeChildStepId)
        {
            subTrace = child.ToSubTrace(child.RecordedFault is not null ? SimulationOutcome.Faulted : SimulationOutcome.Completed);
            this.activeChild = null;
            this.activeChildStepId = null;
        }

        this.steps.Add(new SimulatedStepRecord
        {
            StepId = stepId,
            Attempt = faulted ? Math.Max(0, faultedAttempt - 1) : this.retryCounts.GetValueOrDefault(stepId),
            Faulted = faulted,
            Outputs = this.outputs.TryGetValue(stepId, out JsonElement produced) ? produced : default,
            FirstExchange = this.root.exchangeMark,
            ExchangeCount = this.transport.Exchanges.Count - this.root.exchangeMark,
            SubTrace = subTrace,
        });
        this.root.exchangeMark = this.transport.Exchanges.Count;
        this.currentState = -1; // consumed; the next boundary needs a fresh state (checkpoint/resume sets it)
    }

    /// <summary>
    /// Materializes the in-flight ancestor chain when the run ends with sub-workflow scopes still
    /// open (design §3.5): a stop, suspension, or exhaustion INSIDE a child unwinds without the
    /// invoking steps ever reaching their boundaries, so each ancestor gets a record carrying a
    /// PARTIAL sub-trace (the child's records so far, a non-terminal outcome, and — for a pause —
    /// the scope-local remainder of the scoped path). A goto-workflow transfer that completed also
    /// lands here: its target's records attach to the transferring step on the terminal outcome.
    /// The driver calls this once, on the root, at every terminal return; the appended records are
    /// bookkeeping, not executions — they never count against the budget.
    /// </summary>
    /// <param name="outcome">The run's terminal outcome.</param>
    internal void FinalizeInFlightScopes(SimulationOutcome outcome)
    {
        TracingWorkflowRun scope = this.root;
        while (scope.activeChild is { } child && scope.activeChildStepId is { } stepId)
        {
            SimulationOutcome childOutcome = outcome == SimulationOutcome.Completed
                ? (child.RecordedFault is not null ? SimulationOutcome.Faulted : SimulationOutcome.Completed)
                : outcome;
            scope.steps.Add(new SimulatedStepRecord
            {
                StepId = stepId,
                Attempt = scope.retryCounts.GetValueOrDefault(stepId),
                Faulted = false,
                FirstExchange = this.root.exchangeMark,
                ExchangeCount = 0,
                SubTrace = child.ToSubTrace(childOutcome),
            });
            scope.activeChild = null;
            scope.activeChildStepId = null;
            scope = child;
        }
    }

    /// <summary>Snapshots this scope's collected records as its invoking step's nested trace.</summary>
    private SimulatedSubTrace ToSubTrace(SimulationOutcome outcome)
    {
        // The root's pausedBefore is the FULL scoped path; this scope's trace addresses steps
        // relative to itself, so its pausedBefore is the remainder below its own prefix.
        string? localPausedBefore = null;
        if (outcome == SimulationOutcome.Paused
            && this.root.pausedBefore is { } full
            && full.StartsWith(this.pathPrefix, StringComparison.Ordinal))
        {
            localPausedBefore = full[this.pathPrefix.Length..];
        }

        return new SimulatedSubTrace
        {
            WorkflowId = this.workflowId ?? string.Empty,
            Outcome = outcome,
            PausedBefore = localPausedBefore,
            Fault = this.RecordedFault,
            Wait = outcome == SimulationOutcome.Suspended ? this.root.recordedWait : null,
            Outputs = this.WorkflowOutputs,
            Steps = this.steps,
            StepsExecuted = this.localStepsExecuted,
        };
    }

    /// <summary>
    /// Halts the replay when the NEXT step matches the stop condition. Steps are addressed by their
    /// scoped step path (design §3.5): the scope's path prefix composed with the stepId. A bare id
    /// therefore matches root steps only — a sub-workflow's bare stepId never fires — and arrival
    /// counters live on the root keyed by the composed path, cumulative across child invocations.
    /// </summary>
    private void CheckStop(int nextState, bool throwBeforeAnything)
    {
        if (this.stop.IsEmpty || nextState < 0 || nextState >= this.stepIds.Count)
        {
            return;
        }

        string next = this.pathPrefix.Length == 0 ? this.stepIds[nextState] : this.pathPrefix + this.stepIds[nextState];
        int arrival = this.root.arrivals.GetValueOrDefault(next) + 1;
        this.root.arrivals[next] = arrival;

        bool hit = this.stop.Breakpoints.Contains(next)
            || (this.stop.BeforeStepId == next && arrival == this.stop.Occurrence);
        if (hit)
        {
            this.root.pausedBefore = next;
            if (throwBeforeAnything)
            {
                throw new SimulationPauseException(next);
            }
        }
    }
}