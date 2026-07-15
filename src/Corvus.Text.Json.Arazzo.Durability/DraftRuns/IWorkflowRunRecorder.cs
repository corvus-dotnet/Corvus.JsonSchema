// <copyright file="IWorkflowRunRecorder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The at-source capture sink a §18 debug runner attaches to a <see cref="WorkflowRun"/>
/// (workflow-designer design §15-8a/§3.4): the run invokes these hooks at its step boundaries so the
/// runner records faithful per-step records — stepId, attempt, outcome, exchange range, and
/// sub-workflow nesting — that the checkpoint alone cannot express. <see cref="WorkflowRun.Recorder"/>
/// is <see langword="null"/> for a non-debug run, so production runs pay only a null-check and their
/// checkpoints and stored bytes are untouched by construction.
/// </summary>
/// <remarks>
/// The hooks pass cursor-space state, not step ids: the recorder owns the cursor→stepId map (parsed
/// from the captured package, whose chosen workflow is first so its declaration order IS cursor
/// space) and tracks the in-flight state itself, seeded from the run's cursor when it attaches.
/// </remarks>
public interface IWorkflowRunRecorder
{
    /// <summary>Records the boundary of the step that just completed, then tracks <paramref name="nextCursor"/> as in-flight.</summary>
    /// <param name="nextCursor">The cursor the checkpoint advances to (the NEXT state).</param>
    void OnCheckpointed(int nextCursor);

    /// <summary>Records the in-flight step faulting.</summary>
    /// <param name="stepId">The faulting step.</param>
    /// <param name="attempt">The 1-based attempt that faulted.</param>
    void OnFaulted(string stepId, int attempt);

    /// <summary>Records the in-flight step's boundary at a timer suspension (a retry timer completes the
    /// attempt; the resumed segment re-enters the same state). Message suspensions record nothing — the
    /// step itself is still waiting.</summary>
    void OnTimerSuspended();

    /// <summary>Begins a recording-only child scope for a sub-workflow invocation: the child records
    /// through the same sink but NEVER checkpoints (cursor and resume verbs stay top-level — a
    /// deliberate invariant), and its waits persist on the root run at the parent position so a
    /// resume replays the child fresh. Returns <see langword="null"/> past the
    /// <see cref="IWorkflowRun.MaxSubWorkflowDepth"/> cap (recording stops; execution semantics are
    /// unchanged).</summary>
    /// <param name="stepId">The invoking step.</param>
    /// <param name="subWorkflowId">The invoked workflow's id.</param>
    /// <returns>The child scope, or <see langword="null"/> to run the child untracked.</returns>
    IWorkflowRun? BeginSubWorkflow(string stepId, string subWorkflowId);
}

/// <summary>
/// One at-source captured step record (§15-8a/§3.4): metadata only — no outputs (the checkpoint owns
/// those; the assembler merges them in), no bodies, no criterion verdicts. <c>skipped</c> is never
/// transported: the assembler derives it from the checkpoint delta (design §10 F3).
/// </summary>
/// <param name="StepId">The step id.</param>
/// <param name="Attempt">The retry attempt the record describes (0 = first try).</param>
/// <param name="Faulted">Whether the step faulted rather than completing.</param>
/// <param name="FirstExchange">The index of the step's first exchange in the run's ONE recorded exchange list.</param>
/// <param name="ExchangeCount">How many exchanges the step performed.</param>
/// <param name="SubTrace">The sub-workflow's nested trace, when the step is workflowId-bound.</param>
public sealed record RecordedStepRecord(
    string StepId,
    int Attempt,
    bool Faulted,
    int FirstExchange,
    int ExchangeCount,
    RecordedSubTrace? SubTrace = null);

/// <summary>
/// A sub-workflow execution's captured nested trace (§15-8a): the same record shape, recursively,
/// with the outcome the scope reached. The last invocation's records stand when a suspension replays
/// the child (design §10 F2).
/// </summary>
/// <param name="WorkflowId">The sub-workflow's id.</param>
/// <param name="Outcome">The wire outcome the scope reached: <c>completed</c>, <c>faulted</c>, or <c>suspended</c>.</param>
/// <param name="Steps">The scope's captured records, in execution order.</param>
public sealed record RecordedSubTrace(
    string WorkflowId,
    string Outcome,
    IReadOnlyList<RecordedStepRecord> Steps);