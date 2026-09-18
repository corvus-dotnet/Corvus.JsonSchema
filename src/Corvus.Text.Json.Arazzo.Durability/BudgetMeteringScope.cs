// <copyright file="BudgetMeteringScope.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The scope a budgeted <see cref="WorkflowRun"/> hands a sub-workflow (ADR 0068): it meters the child against the
/// root. Every attempt the child makes is decided by the root's budget before it is made and journaled into the
/// root's journal under the path <c>invokingStep/childStep</c> (nested the same way at every level, which is the
/// tracer's scoped-path shape), so a loop inside a sub-workflow spends the same fuel a loop in the root does and the
/// control plane verifies it from the same journal. Nesting past the budget's depth cap faults the run
/// <see cref="ExecutionBudgetFault.Depth"/> at the invoking step, before the child makes an attempt.
/// </summary>
/// <remarks>
/// <para>
/// On a production run the scope <see cref="MetersOnly"/>: it holds no run state, the durable executor drops its run
/// to <see langword="null"/> and the child executes exactly as it did before it was metered (in-process retry delays,
/// blocking receives, faults that propagate to the root). The durable cursor and the resume verbs stay top-level.
/// </para>
/// <para>
/// On a debug run the recorder already supplies a full recording scope for the child. The metering scope then wraps
/// it: run state and the recording go to the recorder's scope unchanged, and the metering is added around it.
/// </para>
/// </remarks>
internal sealed class BudgetMeteringScope : IWorkflowRun
{
    private readonly WorkflowRun root;
    private readonly string path;
    private readonly int depth;
    private readonly IWorkflowRun? recording;

    // A child step's journal id is built once per scope, so a step a child loops over journals the same string.
    private Dictionary<string, string>? journalIds;

    /// <summary>Initializes a new instance of the <see cref="BudgetMeteringScope"/> class.</summary>
    /// <param name="root">The budgeted root run the scope meters against.</param>
    /// <param name="path">The journal path of the step that invoked the sub-workflow.</param>
    /// <param name="depth">The nesting depth of the sub-workflow (1 for a child of the root).</param>
    /// <param name="recording">The debug recorder's scope for the same invocation, if the run is being recorded.</param>
    public BudgetMeteringScope(WorkflowRun root, string path, int depth, IWorkflowRun? recording)
    {
        this.root = root;
        this.path = path;
        this.depth = depth;
        this.recording = recording;
    }

    /// <inheritdoc/>
    public bool MetersOnly => this.recording is null;

    /// <inheritdoc/>
    public int Cursor => this.recording?.Cursor ?? 0;

    /// <inheritdoc/>
    public string? CorrelationId => this.recording?.CorrelationId;

    /// <inheritdoc/>
    public Dictionary<string, byte[]> CorrelationTokens => this.Recording.CorrelationTokens;

    private IWorkflowRun Recording => this.recording ?? throw ThrowHelper.GetMeteringScopeHoldsNoRunStateException();

    /// <inheritdoc/>
    public ValueTask BeginStepAsync(string stepId, CancellationToken cancellationToken)
    {
        // The depth cap is decided here rather than where the scope is created, because recording the fault is
        // asynchronous and creating a scope is not. The child's first act is to announce its first attempt, so a
        // scope past the cap faults the run before the child reaches a source.
        if (this.root.Budget is { } budget && this.depth > budget.MaxSubWorkflowDepth)
        {
            return this.root.FaultOnBudgetAsync(ExecutionBudgetFault.Depth, this.path, this.JournalId(stepId), cancellationToken);
        }

        return this.root.BeginMeteredStepAsync(this.JournalId(stepId), cancellationToken);
    }

    /// <inheritdoc/>
    public void RecordStep(string stepId, WorkflowStepStatus status, int attempt, DateTimeOffset startedAt, DateTimeOffset endedAt)
        => this.root.RecordStep(this.JournalId(stepId), status, attempt, startedAt, endedAt);

    /// <inheritdoc/>
    public IWorkflowRun? BeginSubWorkflow(string stepId, string subWorkflowId)
        => new BudgetMeteringScope(this.root, this.JournalId(stepId), this.depth + 1, this.recording?.BeginSubWorkflow(stepId, subWorkflowId));

    /// <inheritdoc/>
    public bool TryGetStepOutputs(string stepId, out JsonElement outputs) => this.Recording.TryGetStepOutputs(stepId, out outputs);

    /// <inheritdoc/>
    public int GetRetryCount(string stepId) => this.Recording.GetRetryCount(stepId);

    /// <inheritdoc/>
    public void SetStepOutputs(string stepId, in JsonElement outputs) => this.Recording.SetStepOutputs(stepId, outputs);

    /// <inheritdoc/>
    public void SetRetryCount(string stepId, int count) => this.Recording.SetRetryCount(stepId, count);

    /// <inheritdoc/>
    public ValueTask CheckpointAsync(int cursor, CancellationToken cancellationToken) => this.Recording.CheckpointAsync(cursor, cancellationToken);

    /// <inheritdoc/>
    public ValueTask CompleteAsync(JsonElement outputs, CancellationToken cancellationToken) => this.Recording.CompleteAsync(outputs, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<WorkflowWait> SuspendForTimerAsync(int cursor, TimeSpan delay, CancellationToken cancellationToken)
        => this.Recording.SuspendForTimerAsync(cursor, delay, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<WorkflowWait> SuspendForMessageAsync(int cursor, string channel, string? correlationId, CancellationToken cancellationToken)
        => this.Recording.SuspendForMessageAsync(cursor, channel, correlationId, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<WorkflowFault> FaultAsync(string stepId, int attempt, string error, CancellationToken cancellationToken)
        => this.Recording.FaultAsync(stepId, attempt, error, cancellationToken);

    /// <inheritdoc/>
    public bool TryTakeDeliveredMessage(out JsonElement payload) => this.Recording.TryTakeDeliveredMessage(out payload);

    /// <inheritdoc/>
    public bool TryTakeDeliveredMessage(out JsonElement payload, out JsonElement headers) => this.Recording.TryTakeDeliveredMessage(out payload, out headers);

    private string JournalId(string stepId)
    {
        this.journalIds ??= new Dictionary<string, string>(StringComparer.Ordinal);
        if (!this.journalIds.TryGetValue(stepId, out string? id))
        {
            id = string.Concat(this.path, "/", stepId);
            this.journalIds.Add(stepId, id);
        }

        return id;
    }
}