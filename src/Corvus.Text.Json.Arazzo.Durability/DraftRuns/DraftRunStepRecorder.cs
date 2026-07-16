// <copyright file="DraftRunStepRecorder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The §18 debug runner's at-source step capture (design §15-8a/§3.4): attached to a
/// <see cref="WorkflowRun"/> for the segments this runner drives, it turns the run's boundary hooks
/// into <see cref="RecordedStepRecord"/>s on the per-run <see cref="DraftRunRecording"/> — stepId
/// (via the captured package's cursor→stepId map), attempt, outcome, the exchange range against the
/// recording's ONE exchange list, and sub-workflow nesting through recording-only child scopes.
/// Metadata only, like everything in the recording.
/// </summary>
internal sealed class DraftRunStepRecorder : IWorkflowRunRecorder
{
    private readonly WorkflowRun run;
    private readonly DraftRunRecording recording;
    private readonly DraftRunWorkflowShape shape;
    private int currentState;
    private int exchangeMark;
    private RecordingChildScope? activeChild;
    private string? activeChildStepId;

    public DraftRunStepRecorder(WorkflowRun run, DraftRunRecording recording, DraftRunWorkflowShape shape)
    {
        this.run = run;
        this.recording = recording;
        this.shape = shape;

        // A resumed segment starts mid-run: the in-flight state is the persisted cursor, and the
        // exchange mark starts at whatever earlier segments already recorded.
        this.currentState = run.Cursor;
        this.exchangeMark = recording.ExchangeCount;
    }

    /// <inheritdoc/>
    public void OnCheckpointed(int nextCursor)
    {
        this.RecordBoundary(faulted: false);
        this.currentState = nextCursor;
    }

    /// <inheritdoc/>
    public void OnFaulted(string stepId, int attempt)
        => this.RecordBoundary(faulted: true, stepId, Math.Max(0, attempt - 1));

    /// <inheritdoc/>
    public void OnTimerSuspended() => this.RecordBoundary(faulted: false);

    /// <inheritdoc/>
    public IWorkflowRun? BeginSubWorkflow(string stepId, string subWorkflowId)
    {
        // Past the cap, recording stops (null = untracked, exactly the pre-capture behaviour) —
        // never change a real run's execution semantics from the recording seam.
        var child = RecordingChildScope.Create(this, this.run, stepId, subWorkflowId, depth: 1);
        if (child is not null)
        {
            // The last invocation's records stand (design §10 F2): a suspension replay re-invokes
            // the step and the fresh scope replaces the stale one.
            this.activeChild = child;
            this.activeChildStepId = stepId;
        }

        return child;
    }

    private void RecordBoundary(bool faulted, string? faultedStepId = null, int faultedAttempt = 0)
    {
        IReadOnlyList<string> stepIds = this.shape.StepIds;
        if (faultedStepId is null && (this.currentState < 0 || this.currentState >= stepIds.Count))
        {
            return; // the terminal outputs state
        }

        string stepId = faultedStepId ?? stepIds[this.currentState];
        RecordedSubTrace? subTrace = null;
        if (this.activeChild is { } child && stepId == this.activeChildStepId)
        {
            subTrace = child.ToSubTrace(child.Faulted ? "faulted" : "completed");
            this.activeChild = null;
            this.activeChildStepId = null;
        }

        int exchangeCount = this.recording.ExchangeCount;
        this.recording.RecordStep(new RecordedStepRecord(
            stepId,
            faulted ? faultedAttempt : this.run.GetRetryCount(stepId),
            faulted,
            this.exchangeMark,
            exchangeCount - this.exchangeMark,
            subTrace));
        this.exchangeMark = exchangeCount;
    }

    /// <summary>Snapshots the still-open child chain as partial sub-traces on synthetic parent records — called
    /// once by the runner when a segment ends suspended (the invoking steps never reached their boundaries).</summary>
    /// <param name="outcome">The wire outcome the open scopes stand at (<c>suspended</c>).</param>
    internal void FinalizeInFlightScopes(string outcome)
    {
        if (this.activeChild is { } child && this.activeChildStepId is { } stepId)
        {
            this.recording.RecordStep(new RecordedStepRecord(
                stepId,
                this.run.GetRetryCount(stepId),
                Faulted: false,
                this.exchangeMark,
                ExchangeCount: 0,
                child.ToSubTrace(outcome, finalizeDescendants: true)));
            this.activeChild = null;
            this.activeChildStepId = null;
        }
    }

    /// <summary>
    /// A recording-only sub-workflow scope (design §3.4): it records the child's step boundaries
    /// in memory and NEVER checkpoints — the durable cursor and the resume verbs stay top-level, a
    /// deliberate invariant. Its waits delegate to the ROOT run at the root's current cursor, so the
    /// wait persists at the parent position and a resume replays the child fresh (design §10 F2).
    /// Message delivery forwards from the root so a delivered trigger reaches a receive step inside
    /// the child.
    /// </summary>
    private sealed class RecordingChildScope : IWorkflowRun
    {
        private readonly DraftRunStepRecorder recorder;
        private readonly WorkflowRun rootRun;
        private readonly string workflowId;
        private readonly IReadOnlyList<string> stepIds;
        private readonly int depth;
        private readonly Dictionary<string, JsonElement> outputs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> retryCounts = new(StringComparer.Ordinal);
        private readonly List<RecordedStepRecord> steps = [];
        private int currentState;
        private RecordingChildScope? activeChild;
        private string? activeChildStepId;

        private RecordingChildScope(DraftRunStepRecorder recorder, WorkflowRun rootRun, string workflowId, IReadOnlyList<string> stepIds, int depth)
        {
            this.recorder = recorder;
            this.rootRun = rootRun;
            this.workflowId = workflowId;
            this.stepIds = stepIds;
            this.depth = depth;
        }

        public bool Faulted { get; private set; }

        /// <inheritdoc/>
        public int Cursor { get; private set; }

        /// <inheritdoc/>
        public string? CorrelationId => null;

        /// <inheritdoc/>
        public Dictionary<string, byte[]> CorrelationTokens { get; } = new(StringComparer.Ordinal);

        public static RecordingChildScope? Create(DraftRunStepRecorder recorder, WorkflowRun rootRun, string stepId, string subWorkflowId, int depth)
        {
            _ = stepId;
            if (depth > IWorkflowRun.MaxSubWorkflowDepth)
            {
                return null;
            }

            // A sub-workflow defined in another source document has no local shape: the scope still
            // records (without step attribution) so nesting stays visible (design §10 F10).
            IReadOnlyList<string> stepIds = recorder.shape.SubWorkflows.TryGetValue(subWorkflowId, out IReadOnlyList<string>? ids) ? ids : [];
            return new RecordingChildScope(recorder, rootRun, subWorkflowId, stepIds, depth);
        }

        /// <inheritdoc/>
        public IWorkflowRun? BeginSubWorkflow(string stepId, string subWorkflowId)
        {
            var child = Create(this.recorder, this.rootRun, stepId, subWorkflowId, this.depth + 1);
            if (child is not null)
            {
                this.activeChild = child;
                this.activeChildStepId = stepId;
            }

            return child;
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
            // Record only: the child never persists a checkpoint (cursor/resume stay top-level).
            this.RecordBoundary(faulted: false);
            this.Cursor = cursor;
            this.currentState = cursor;
            return default;
        }

        /// <inheritdoc/>
        public ValueTask CompleteAsync(JsonElement outputs, CancellationToken cancellationToken)
        {
            this.RecordBoundary(faulted: false);
            return default;
        }

        /// <inheritdoc/>
        public async ValueTask<WorkflowWait> SuspendForTimerAsync(int cursor, TimeSpan delay, CancellationToken cancellationToken)
        {
            // The attempt completed (a retry timer): a boundary for the child, then the wait persists
            // on the ROOT at the root's cursor — the resumed segment re-runs the parent step and
            // replays the child fresh.
            this.RecordBoundary(faulted: false);
            this.Cursor = cursor;
            return await this.rootRun.SuspendForTimerAsync(this.rootRun.Cursor, delay, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask<WorkflowWait> SuspendForMessageAsync(int cursor, string channel, string? correlationId, CancellationToken cancellationToken)
        {
            this.Cursor = cursor;
            return await this.rootRun.SuspendForMessageAsync(this.rootRun.Cursor, channel, correlationId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public ValueTask<WorkflowFault> FaultAsync(string stepId, int attempt, string error, CancellationToken cancellationToken)
        {
            // Record the child fault without persisting anything: the parent's result unwrapping
            // decides whether the run faults (through the ROOT's own FaultAsync) or recovers.
            this.RecordBoundary(faulted: true, stepId, Math.Max(0, attempt - 1));
            this.Faulted = true;
            return new(new WorkflowFault(stepId, attempt, error, DateTimeOffset.MinValue));
        }

        /// <inheritdoc/>
        public bool TryTakeDeliveredMessage(out JsonElement payload) => this.rootRun.TryTakeDeliveredMessage(out payload);

        internal RecordedSubTrace ToSubTrace(string outcome, bool finalizeDescendants = false)
        {
            if (finalizeDescendants && this.activeChild is { } child && this.activeChildStepId is { } stepId)
            {
                this.steps.Add(new RecordedStepRecord(
                    stepId,
                    this.retryCounts.GetValueOrDefault(stepId),
                    Faulted: false,
                    this.recorder.exchangeMark,
                    ExchangeCount: 0,
                    child.ToSubTrace(outcome, finalizeDescendants: true)));
                this.activeChild = null;
                this.activeChildStepId = null;
            }

            return new RecordedSubTrace(this.workflowId, outcome, this.steps.ToArray());
        }

        private void RecordBoundary(bool faulted, string? faultedStepId = null, int faultedAttempt = 0)
        {
            if (faultedStepId is null && (this.currentState < 0 || this.currentState >= this.stepIds.Count))
            {
                return;
            }

            string stepId = faultedStepId ?? this.stepIds[this.currentState];
            RecordedSubTrace? subTrace = null;
            if (this.activeChild is { } child && stepId == this.activeChildStepId)
            {
                subTrace = child.ToSubTrace(child.Faulted ? "faulted" : "completed");
                this.activeChild = null;
                this.activeChildStepId = null;
            }

            int exchangeCount = this.recorder.recording.ExchangeCount;
            this.steps.Add(new RecordedStepRecord(
                stepId,
                faulted ? faultedAttempt : this.retryCounts.GetValueOrDefault(stepId),
                faulted,
                this.recorder.exchangeMark,
                exchangeCount - this.recorder.exchangeMark,
                subTrace));
            this.recorder.exchangeMark = exchangeCount;
        }
    }
}

/// <summary>The captured package's cursor→stepId shape: the chosen (first) workflow's step ids —
/// declaration order IS the durable cursor space — plus every workflow's step ids by workflowId for
/// sub-workflow scopes.</summary>
/// <param name="StepIds">The chosen workflow's step ids, in declaration order.</param>
/// <param name="SubWorkflows">Every workflow's step ids by workflowId (the chosen one included).</param>
public sealed record DraftRunWorkflowShape(
    IReadOnlyList<string> StepIds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SubWorkflows);