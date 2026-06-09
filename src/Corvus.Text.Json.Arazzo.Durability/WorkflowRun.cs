// <copyright file="WorkflowRun.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The per-run <see cref="IWorkflowRun"/> a code-generated <em>durable</em> executor touches. It carries the
/// run's resumable state — cursor, each step's <c>outputs</c> product, retry counts, and the correlation
/// register — stages the products as the executor builds them, and on each checkpoint serializes that state
/// and persists it to an <see cref="IWorkflowStateStore"/> under optimistic concurrency.
/// </summary>
/// <remarks>
/// A fresh run (<see cref="CreateNew"/>) starts at cursor <c>0</c> with empty state, so the durable executor
/// behaves exactly like the non-durable form. A resumed run (<see cref="Resume"/> / <see cref="ResumeAsync"/>)
/// starts pre-populated from a checkpoint, so the generated <c>while/switch</c> loop jumps straight to the
/// restored cursor with its step-output locals already loaded. The run owns the resumed checkpoint state and
/// must be disposed.
/// </remarks>
public sealed class WorkflowRun : IWorkflowRun, IDisposable
{
    private readonly IWorkflowStateStore store;
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, int> retryCounts;
    private readonly Dictionary<string, JsonElement> stepOutputs;
    private readonly DateTimeOffset createdAt;
    private readonly WorkflowCheckpointState? resumedState;
    private readonly JsonElement inputs;
    private WorkflowEtag etag;

    private WorkflowRun(
        IWorkflowStateStore store,
        WorkflowRunId id,
        string workflowId,
        TimeProvider timeProvider,
        WorkflowRunStatus status,
        int cursor,
        Dictionary<string, int> retryCounts,
        Dictionary<string, byte[]> correlationTokens,
        JsonElement inputs,
        Dictionary<string, JsonElement> stepOutputs,
        WorkflowEtag etag,
        DateTimeOffset createdAt,
        WorkflowCheckpointState? resumedState)
    {
        this.store = store;
        this.Id = id;
        this.WorkflowId = workflowId;
        this.timeProvider = timeProvider;
        this.Status = status;
        this.Cursor = cursor;
        this.retryCounts = retryCounts;
        this.CorrelationTokens = correlationTokens;
        this.inputs = inputs;
        this.stepOutputs = stepOutputs;
        this.etag = etag;
        this.createdAt = createdAt;
        this.resumedState = resumedState;
    }

    /// <summary>Gets the run id.</summary>
    public WorkflowRunId Id { get; }

    /// <summary>Gets the id of the workflow the run executes.</summary>
    public string WorkflowId { get; }

    /// <summary>Gets the run's current lifecycle status.</summary>
    public WorkflowRunStatus Status { get; private set; }

    /// <summary>Gets the etag of the last persisted checkpoint (<see cref="WorkflowEtag.None"/> before the first save).</summary>
    public WorkflowEtag Etag => this.etag;

    /// <summary>Gets the workflow inputs (so a worker can reconstruct the executor's <c>inputs</c> argument on resume).</summary>
    public JsonElement Inputs => this.inputs;

    /// <inheritdoc/>
    public int Cursor { get; private set; }

    /// <inheritdoc/>
    public Dictionary<string, byte[]> CorrelationTokens { get; }

    /// <summary>Creates a fresh run that starts at cursor <c>0</c> with empty state.</summary>
    /// <param name="store">The state store to persist checkpoints to.</param>
    /// <param name="id">The run id.</param>
    /// <param name="workflowId">The id of the workflow the run executes.</param>
    /// <param name="inputs">The workflow inputs.</param>
    /// <param name="timeProvider">The time source for checkpoint timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The new run.</returns>
    public static WorkflowRun CreateNew(
        IWorkflowStateStore store,
        WorkflowRunId id,
        string workflowId,
        JsonElement inputs,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(workflowId);

        TimeProvider time = timeProvider ?? TimeProvider.System;
        return new WorkflowRun(
            store,
            id,
            workflowId,
            time,
            WorkflowRunStatus.Pending,
            cursor: 0,
            retryCounts: [],
            correlationTokens: [],
            inputs,
            stepOutputs: [],
            etag: WorkflowEtag.None,
            createdAt: time.GetUtcNow(),
            resumedState: null);
    }

    /// <summary>Builds a run from a loaded checkpoint, ready to be re-entered by the executor.</summary>
    /// <param name="store">The state store to persist subsequent checkpoints to.</param>
    /// <param name="state">The deserialized checkpoint state; the run takes ownership and disposes it.</param>
    /// <param name="etag">The etag the checkpoint was read at (passed as <c>expected</c> on the next save).</param>
    /// <param name="timeProvider">The time source for checkpoint timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The resumed run.</returns>
    public static WorkflowRun Resume(
        IWorkflowStateStore store,
        WorkflowCheckpointState state,
        WorkflowEtag etag,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(state);

        return new WorkflowRun(
            store,
            state.RunId,
            state.WorkflowId,
            timeProvider ?? TimeProvider.System,
            state.Status,
            state.Cursor,
            state.RetryCounters,
            state.CorrelationTokens,
            state.Inputs,
            state.StepOutputs,
            etag,
            createdAt: state.CreatedAt,
            resumedState: state);
    }

    /// <summary>Loads a run's checkpoint from the store and builds a resumed run from it.</summary>
    /// <param name="store">The state store.</param>
    /// <param name="id">The run id.</param>
    /// <param name="timeProvider">The time source for checkpoint timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resumed run, or <see langword="null"/> if no run with that id exists.</returns>
    public static async ValueTask<WorkflowRun?> ResumeAsync(
        IWorkflowStateStore store,
        WorkflowRunId id,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        WorkflowCheckpoint? checkpoint = await store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (checkpoint is null)
        {
            return null;
        }

        WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(checkpoint.Value.Utf8);
        return Resume(store, state, checkpoint.Value.Etag, timeProvider);
    }

    /// <inheritdoc/>
    public bool TryGetStepOutputs(string stepId, out JsonElement outputs) => this.stepOutputs.TryGetValue(stepId, out outputs);

    /// <inheritdoc/>
    public int GetRetryCount(string stepId) => this.retryCounts.GetValueOrDefault(stepId);

    /// <inheritdoc/>
    public void SetStepOutputs(string stepId, in JsonElement outputs)
    {
        if (outputs.ValueKind != JsonValueKind.Undefined)
        {
            this.stepOutputs[stepId] = outputs;
        }
    }

    /// <inheritdoc/>
    public void SetRetryCount(string stepId, int count) => this.retryCounts[stepId] = count;

    /// <inheritdoc/>
    public ValueTask CheckpointAsync(int cursor, CancellationToken cancellationToken)
    {
        this.Cursor = cursor;
        this.Status = WorkflowRunStatus.Running;
        return this.PersistAsync(default, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CompleteAsync(JsonElement outputs, CancellationToken cancellationToken)
    {
        this.Status = WorkflowRunStatus.Completed;
        return this.PersistAsync(outputs, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose() => this.resumedState?.Dispose();

    private async ValueTask PersistAsync(JsonElement outputs, CancellationToken cancellationToken)
    {
        byte[] checkpoint = WorkflowCheckpointSerializer.Serialize(
            this.Id,
            this.WorkflowId,
            this.Status,
            this.Cursor,
            this.createdAt,
            this.retryCounts,
            this.CorrelationTokens,
            this.inputs,
            this.stepOutputs,
            outputs);

        var index = new WorkflowRunIndexEntry(
            this.WorkflowId,
            this.Status,
            this.createdAt,
            this.timeProvider.GetUtcNow());

        this.etag = await this.store.SaveAsync(this.Id, checkpoint, index, this.etag, cancellationToken).ConfigureAwait(false);
    }
}