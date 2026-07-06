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
/// </remarks>
internal sealed class TracingWorkflowRun : IWorkflowRun
{
    private readonly IReadOnlyList<string> stepIds;
    private readonly SimulationStop stop;
    private readonly int maxSteps;
    private readonly ManualTimeProvider clock;
    private readonly MockApiTransport transport;
    private readonly IReadOnlyDictionary<string, StepOutputOverride> overrides;
    private readonly Dictionary<string, JsonElement> outputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> retryCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> arrivals = new(StringComparer.Ordinal);
    private readonly List<SimulatedStepRecord> steps = [];
    private int currentState;
    private int exchangeMark;
    private JsonElement deliveredMessage;
    private bool hasDeliveredMessage;

    public TracingWorkflowRun(
        IReadOnlyList<string> stepIds,
        SimulationStop stop,
        int maxSteps,
        ManualTimeProvider clock,
        MockApiTransport transport,
        IReadOnlyDictionary<string, StepOutputOverride> overrides)
    {
        this.stepIds = stepIds;
        this.stop = stop;
        this.maxSteps = maxSteps;
        this.clock = clock;
        this.transport = transport;
        this.overrides = overrides;

        // The stop condition may name the FIRST step (pause before anything runs).
        this.CheckStop(0, throwBeforeAnything: false);
    }

    /// <summary>Gets the executed-step records, in execution order.</summary>
    public IReadOnlyList<SimulatedStepRecord> Steps => this.steps;

    /// <summary>Gets the total step executions so far (the budget's measure).</summary>
    public int StepsExecuted { get; private set; }

    /// <summary>Gets the workflow outputs recorded by <see cref="CompleteAsync"/>.</summary>
    public JsonElement WorkflowOutputs { get; private set; }

    /// <summary>Gets the fault recorded by <see cref="FaultAsync"/>, if any.</summary>
    public WorkflowFault? RecordedFault { get; private set; }

    /// <summary>Gets the wait recorded by the last suspend, if any.</summary>
    public WorkflowWait? RecordedWait { get; private set; }

    /// <summary>Gets the step whose arrival satisfied the stop condition, when the run paused.</summary>
    public string? PausedBefore { get; private set; }

    /// <summary>Gets the step currently executing, when one is (fault attribution for executor crashes).</summary>
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
        this.RecordedWait = null;
    }

    /// <summary>Hands the next matching trigger's payload to the resumed correlated-receive step.</summary>
    public void DeliverMessage(in JsonElement payload)
    {
        this.deliveredMessage = payload;
        this.hasDeliveredMessage = true;
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
        this.RecordedWait = wait;
        return new(wait);
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowWait> SuspendForMessageAsync(int cursor, string channel, string? correlationId, CancellationToken cancellationToken)
    {
        // A message wait is the step ITSELF waiting (not a completed-step boundary): record no step,
        // just the wait — the resumed invocation re-enters the same state and completes the step.
        this.Cursor = cursor;
        var wait = WorkflowWait.Message(channel, correlationId);
        this.RecordedWait = wait;
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
        if (this.hasDeliveredMessage)
        {
            payload = this.deliveredMessage;
            this.hasDeliveredMessage = false;
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
            if (++this.StepsExecuted > this.maxSteps)
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
                FirstExchange = this.exchangeMark,
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
        if (++this.StepsExecuted > this.maxSteps)
        {
            throw new SimulationBudgetException();
        }

        this.steps.Add(new SimulatedStepRecord
        {
            StepId = stepId,
            Attempt = faulted ? Math.Max(0, faultedAttempt - 1) : this.retryCounts.GetValueOrDefault(stepId),
            Faulted = faulted,
            Outputs = this.outputs.TryGetValue(stepId, out JsonElement produced) ? produced : default,
            FirstExchange = this.exchangeMark,
            ExchangeCount = this.transport.Exchanges.Count - this.exchangeMark,
        });
        this.exchangeMark = this.transport.Exchanges.Count;
        this.currentState = -1; // consumed; the next boundary needs a fresh state (checkpoint/resume sets it)
    }

    /// <summary>Halts the replay when the NEXT step matches the stop condition.</summary>
    private void CheckStop(int nextState, bool throwBeforeAnything)
    {
        if (this.stop.IsEmpty || nextState < 0 || nextState >= this.stepIds.Count)
        {
            return;
        }

        string next = this.stepIds[nextState];
        int arrival = this.arrivals.GetValueOrDefault(next) + 1;
        this.arrivals[next] = arrival;

        bool hit = this.stop.Breakpoints.Contains(next)
            || (this.stop.BeforeStepId == next && arrival == this.stop.Occurrence);
        if (hit)
        {
            this.PausedBefore = next;
            if (throwBeforeAnything)
            {
                throw new SimulationPauseException(next);
            }
        }
    }
}