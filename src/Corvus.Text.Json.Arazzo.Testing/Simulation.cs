// <copyright file="Simulation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// The deterministic setup for one simulation command (workflow-designer design §4.3): workflow
/// inputs, scripted mock responses, message triggers, and the virtual-clock policy. The same shape
/// backs run, step, run-to-here, and re-run-after-edit — only the stop condition differs.
/// </summary>
public sealed class SimulationScenario
{
    /// <summary>Gets the workflow inputs value (default: no inputs).</summary>
    public JsonElement Inputs { get; init; }

    /// <summary>Gets the scripted mock routes, in declaration order (repeats on one route form a sequence).</summary>
    public IReadOnlyList<SimulationMockRoute> Mocks { get; init; } = [];

    /// <summary>Gets the message triggers delivered, in order, when the run suspends on a matching channel.</summary>
    public IReadOnlyList<SimulationTrigger> Triggers { get; init; } = [];

    /// <summary>
    /// Gets the step-output overrides (workflow-designer design §15 8b): step id → the PROVIDED
    /// outputs value. An overridden step does not execute — the replay records it as skipped and its
    /// provided outputs stand for downstream reference resolution, exactly as the durable engine's
    /// Skip resume records operator-supplied outputs and moves the cursor past the step.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> StepOutputOverrides { get; init; } =
        System.Collections.ObjectModel.ReadOnlyDictionary<string, JsonElement>.Empty;

    /// <summary>Gets a value indicating whether timer waits auto-advance the virtual clock and resume (default true).</summary>
    public bool AutoAdvanceClock { get; init; } = true;
}

/// <summary>One scripted mock response, keyed by method + OpenAPI path template.</summary>
/// <param name="Method">The HTTP method name (case-insensitive, e.g. <c>get</c>).</param>
/// <param name="PathTemplate">The OpenAPI path template (e.g. <c>/pets/{petId}</c>).</param>
/// <param name="StatusCode">The response status.</param>
/// <param name="Body">The response body value (an undefined element means an empty body).</param>
public readonly record struct SimulationMockRoute(string Method, string PathTemplate, int StatusCode, JsonElement Body);

/// <summary>One message trigger: delivered when the run suspends waiting on <paramref name="Channel"/>.</summary>
/// <param name="Channel">The channel the waiting step listens on.</param>
/// <param name="Payload">The message payload value.</param>
/// <param name="CorrelationId">The correlation id, when the wait is correlated.</param>
public readonly record struct SimulationTrigger(string Channel, JsonElement Payload, string? CorrelationId);

/// <summary>
/// Where the replay stops (§8.2 stateless stepping): pause before a named step's Nth arrival, or
/// before any breakpointed step. No condition = run to completion/fault/suspension/budget.
/// </summary>
public sealed class SimulationStop
{
    /// <summary>Gets the step to pause before, if any.</summary>
    public string? BeforeStepId { get; init; }

    /// <summary>Gets which arrival at <see cref="BeforeStepId"/> pauses (1-based; loops revisit steps).</summary>
    public int Occurrence { get; init; } = 1;

    /// <summary>Gets the breakpointed steps — the replay pauses before any of them executes.</summary>
    public IReadOnlySet<string> Breakpoints { get; init; } = new HashSet<string>();

    /// <summary>Gets a value indicating whether any stop condition is present.</summary>
    public bool IsEmpty => this.BeforeStepId is null && this.Breakpoints.Count == 0;
}

/// <summary>The §8.3 safety limits. The server clamps requests to its own maxima.</summary>
public sealed class SimulationBudget
{
    /// <summary>Gets the step-execution budget (retries and revisits count).</summary>
    public int MaxSteps { get; init; } = 256;

    /// <summary>Gets the real-time execution cap.</summary>
    public TimeSpan WallClock { get; init; } = TimeSpan.FromSeconds(10);
}

/// <summary>How a simulation replay ended.</summary>
public enum SimulationOutcome
{
    /// <summary>The workflow ran to completion; <see cref="SimulationResult.Outputs"/> holds its outputs.</summary>
    Completed,

    /// <summary>The workflow faulted; <see cref="SimulationResult.Fault"/> describes it.</summary>
    Faulted,

    /// <summary>The replay paused at the stop condition; <see cref="SimulationResult.PausedBefore"/> names the step.</summary>
    Paused,

    /// <summary>The run suspended on a wait the scenario did not satisfy; <see cref="SimulationResult.Wait"/> describes it.</summary>
    Suspended,

    /// <summary>The step budget ran out before any other outcome.</summary>
    BudgetExhausted,

    /// <summary>The document does not compile to an executable workflow.</summary>
    NotExecutable,
}

/// <summary>One executed step in the trace (a step reached repeatedly appears repeatedly).</summary>
public sealed class SimulatedStepRecord
{
    /// <summary>Gets the step id.</summary>
    public required string StepId { get; init; }

    /// <summary>Gets the retry attempt this record describes (0 = first try).</summary>
    public required int Attempt { get; init; }

    /// <summary>Gets a value indicating whether the step faulted (rather than completing).</summary>
    public bool Faulted { get; init; }

    /// <summary>Gets a value indicating whether a step-output override skipped the step (§15 8b):
    /// it never executed — <see cref="Outputs"/> holds the PROVIDED value.</summary>
    public bool Skipped { get; init; }

    /// <summary>Gets the step's extracted outputs, when it produced any.</summary>
    public JsonElement Outputs { get; init; }

    /// <summary>Gets the index of this step's first exchange in <see cref="SimulationResult.Exchanges"/>.</summary>
    public int FirstExchange { get; init; }

    /// <summary>Gets the number of exchanges this step performed.</summary>
    public int ExchangeCount { get; init; }

    /// <summary>Gets the success-criterion truth table, re-evaluated post-hoc against the captured context.</summary>
    public IReadOnlyList<SimulatedCriterionVerdict> SuccessCriteria { get; internal set; } = [];

    /// <summary>Gets the inferred routing decision for this step.</summary>
    public SimulatedAction? ActionTaken { get; internal set; }

    /// <summary>Gets the sub-workflow execution's nested trace, when this step is workflowId-bound (§15-8a).</summary>
    public SimulatedSubTrace? SubTrace { get; init; }
}

/// <summary>
/// A sub-workflow execution's complete nested trace, attached to its invoking step's record
/// (§15-8a, design §3.1) — the same trace shape, recursively. Lightweight by design: it owns
/// nothing (child records index the ROOT result's exchange list, and every element lives in the
/// root result's workspace), so it is deliberately NOT a <see cref="SimulationResult"/>. When a
/// suspension replays the child, the LAST invocation's records stand (design §10 F2); a stop or
/// exhaustion inside the child leaves a PARTIAL sub-trace (a non-terminal <see cref="Outcome"/>)
/// on each in-flight ancestor record.
/// </summary>
public sealed class SimulatedSubTrace
{
    /// <summary>Gets the sub-workflow's id (sub-traces always carry one; the root trace never does).</summary>
    public required string WorkflowId { get; init; }

    /// <summary>Gets how this sub-workflow execution ended (or where it stands, for a partial trace).</summary>
    public required SimulationOutcome Outcome { get; init; }

    /// <summary>Gets the scope-local scoped step path the stop landed before, for a partial paused trace.</summary>
    public string? PausedBefore { get; init; }

    /// <summary>Gets the fault, when the sub-workflow faulted.</summary>
    public WorkflowFault? Fault { get; init; }

    /// <summary>Gets the unsatisfied wait, when the sub-workflow suspended.</summary>
    public WorkflowWait? Wait { get; init; }

    /// <summary>Gets the sub-workflow's outputs, when it completed.</summary>
    public JsonElement Outputs { get; init; }

    /// <summary>Gets the sub-workflow's executed steps, in execution order (recursively the same record shape).</summary>
    public required IReadOnlyList<SimulatedStepRecord> Steps { get; init; }

    /// <summary>Gets the steps this sub-workflow execution contributed to the one global budget.</summary>
    public required int StepsExecuted { get; init; }
}

/// <summary>One criterion's re-evaluated verdict.</summary>
/// <param name="Condition">The criterion condition text.</param>
/// <param name="Satisfied">Whether it held against the captured context.</param>
public readonly record struct SimulatedCriterionVerdict(string Condition, bool Satisfied);

/// <summary>The inferred routing decision after a step: the first action whose criteria all passed, or the implicit flow.</summary>
/// <param name="Type">One of <c>end</c>, <c>goto</c>, <c>retry</c>, <c>fallThrough</c>, <c>fault</c>.</param>
/// <param name="Name">The explicit action's name, when one fired.</param>
/// <param name="Target">The goto/retry-from target step, when one applies.</param>
public readonly record struct SimulatedAction(string Type, string? Name, string? Target);

/// <summary>One virtual-clock advance.</summary>
/// <param name="To">The instant advanced to.</param>
/// <param name="Reason">Why (e.g. the awaited timer's description).</param>
public readonly record struct SimulationClockAdvance(DateTimeOffset To, string Reason);

/// <summary>
/// The complete result of one simulation command: the outcome plus the full structured trace up to
/// the stop point. Owns the workspace the trace's <see cref="JsonElement"/>s live in — dispose it
/// after the trace is written out.
/// </summary>
public sealed class SimulationResult : IDisposable
{
    private readonly IDisposable? workspace;

    internal SimulationResult(IDisposable? workspace) => this.workspace = workspace;

    /// <summary>Gets how the replay ended.</summary>
    public required SimulationOutcome Outcome { get; init; }

    /// <summary>Gets the step the replay paused before, when <see cref="Outcome"/> is <see cref="SimulationOutcome.Paused"/>.</summary>
    public string? PausedBefore { get; init; }

    /// <summary>Gets the workflow outputs, when completed.</summary>
    public JsonElement Outputs { get; init; }

    /// <summary>Gets the fault record, when faulted.</summary>
    public WorkflowFault? Fault { get; init; }

    /// <summary>Gets the unsatisfied wait, when suspended.</summary>
    public WorkflowWait? Wait { get; init; }

    /// <summary>Gets the executed steps, in execution order.</summary>
    public required IReadOnlyList<SimulatedStepRecord> Steps { get; init; }

    /// <summary>Gets every request/response exchange, in order (steps reference ranges of this list).</summary>
    public required IReadOnlyList<MockApiExchange> Exchanges { get; init; }

    /// <summary>Gets the virtual-clock advances, in order.</summary>
    public required IReadOnlyList<SimulationClockAdvance> ClockAdvances { get; init; }

    /// <summary>Gets the total step executions (what the budget counted).</summary>
    public required int StepsExecuted { get; init; }

    /// <inheritdoc/>
    public void Dispose() => this.workspace?.Dispose();
}

/// <summary>Thrown by the tracing run to halt the replay at the stop condition; never escapes the simulator.</summary>
internal sealed class SimulationPauseException(string pausedBefore) : Exception
{
    public string PausedBefore { get; } = pausedBefore;
}

/// <summary>Thrown by the tracing run when the step budget runs out; never escapes the simulator.</summary>
internal sealed class SimulationBudgetException : Exception;

/// <summary>
/// One resolved step-output override (§15 8b): the provided outputs, the state the replay resumes at
/// after the skip (the criteria-less success route — end/goto honoured; no <c>$statusCode</c> exists
/// to judge anything else), and the routing decision recorded on the skipped step's record.
/// </summary>
internal readonly record struct StepOutputOverride(JsonElement Outputs, int NextState, SimulatedAction Action);

/// <summary>Thrown by the tracing run when the next step is overridden (§15 8b): the executor
/// invocation unwinds so the driver can consume the skip and re-enter at the advanced cursor — the
/// replay analogue of the durable Skip's mutate-checkpoint-then-re-enter. Never escapes the simulator.</summary>
internal sealed class SimulationSkipException : Exception;