// <copyright file="WorkflowRun.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
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
    private readonly string? correlationId;
    private readonly IReadOnlyList<string>? tags;
    private readonly IReadOnlyList<SecurityTag>? securityTags;
    private readonly WorkflowCheckpointState? resumedState;
    private readonly JsonElement inputs;
    private WorkflowEtag etag;
    private WorkflowWait? wait;
    private WorkflowFault? fault;
    private JsonElement deliveredMessage;
    private bool hasDeliveredMessage;

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
        string? correlationId,
        IReadOnlyList<string>? tags,
        IReadOnlyList<SecurityTag>? securityTags,
        WorkflowWait? wait,
        WorkflowFault? fault,
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
        this.correlationId = correlationId;
        this.tags = tags;
        this.securityTags = securityTags;
        this.wait = wait;
        this.fault = fault;
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

    /// <summary>Gets the wait describing why the run is suspended, if it is (so a worker knows what to wait for).</summary>
    public WorkflowWait? Wait => this.wait;

    /// <summary>Gets the fault record if the run is faulted.</summary>
    public WorkflowFault? Fault => this.fault;

    /// <inheritdoc/>
    public int Cursor { get; private set; }

    /// <inheritdoc/>
    public Dictionary<string, byte[]> CorrelationTokens { get; }

    /// <summary>Gets the run-wide telemetry correlation id (the W3C trace id captured at creation), if any.</summary>
    public string? CorrelationId => this.correlationId;

    /// <summary>Gets the free-form tags applied to the run at creation, if any.</summary>
    public IReadOnlyList<string>? Tags => this.tags;

    /// <summary>Gets the security tags (KVP labels) applied to the run at creation, if any (design §14.2).</summary>
    public IReadOnlyList<SecurityTag>? SecurityTags => this.securityTags;

    /// <summary>Creates a fresh run that starts at cursor <c>0</c> with empty state.</summary>
    /// <param name="store">The state store to persist checkpoints to.</param>
    /// <param name="id">The run id.</param>
    /// <param name="workflowId">The id of the workflow the run executes.</param>
    /// <param name="inputs">The workflow inputs.</param>
    /// <param name="timeProvider">The time source for checkpoint timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="correlationId">The run-wide telemetry correlation id; defaults to the ambient
    /// <see cref="Activity.Current"/> trace id, so the run correlates to the trace that created it.</param>
    /// <param name="tags">Free-form tags to apply to the run (for visibility/filtering); set once at creation.</param>
    /// <param name="securityTags">Security tags (KVP labels) to apply to the run (for row authorization, §14.2); set once at creation, typically inherited from the workflow version.</param>
    /// <returns>The new run.</returns>
    public static WorkflowRun CreateNew(
        IWorkflowStateStore store,
        WorkflowRunId id,
        string workflowId,
        JsonElement inputs,
        TimeProvider? timeProvider = null,
        string? correlationId = null,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<SecurityTag>? securityTags = null)
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
            correlationId: correlationId ?? Activity.Current?.TraceId.ToString(),
            tags: tags is { Count: > 0 } ? tags : null,
            securityTags: securityTags is { Count: > 0 } ? securityTags : null,
            wait: null,
            fault: null,
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
            correlationId: state.CorrelationId,
            tags: state.Tags,
            securityTags: state.SecurityTags,
            wait: state.Wait,
            fault: state.Fault,
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

    /// <summary>
    /// Persists this freshly created run in its <see cref="WorkflowRunStatus.Pending"/> state so a dispatcher
    /// can claim and start it — the store is the queue. Call once, after <see cref="CreateNew"/>, before
    /// handing the run off to a runner; the run does not execute here.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the pending run is durable.</returns>
    public ValueTask EnqueueAsync(CancellationToken cancellationToken) => this.PersistAsync(default, cancellationToken);

    /// <inheritdoc/>
    public ValueTask CheckpointAsync(int cursor, CancellationToken cancellationToken)
    {
        this.Cursor = cursor;
        this.Status = WorkflowRunStatus.Running;
        this.wait = null;
        this.fault = null;
        return this.PersistAsync(default, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CompleteAsync(JsonElement outputs, CancellationToken cancellationToken)
    {
        this.Status = WorkflowRunStatus.Completed;
        this.wait = null;
        this.fault = null;
        return this.PersistAsync(outputs, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowWait> SuspendForTimerAsync(int cursor, TimeSpan delay, CancellationToken cancellationToken)
    {
        this.Cursor = cursor;
        this.Status = WorkflowRunStatus.Suspended;
        this.fault = null;
        var w = WorkflowWait.Timer(this.timeProvider.GetUtcNow() + delay);
        this.wait = w;
        await this.PersistAsync(default, cancellationToken).ConfigureAwait(false);
        ArazzoTelemetry.WorkflowsSuspended.Add(1, new KeyValuePair<string, object?>(ArazzoTelemetry.WorkflowIdTag, this.WorkflowId));
        return w;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowWait> SuspendForMessageAsync(int cursor, string channel, string? correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        this.Cursor = cursor;
        this.Status = WorkflowRunStatus.Suspended;
        this.fault = null;
        var w = WorkflowWait.Message(channel, correlationId);
        this.wait = w;
        await this.PersistAsync(default, cancellationToken).ConfigureAwait(false);
        ArazzoTelemetry.WorkflowsSuspended.Add(1, new KeyValuePair<string, object?>(ArazzoTelemetry.WorkflowIdTag, this.WorkflowId));
        return w;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowFault> FaultAsync(string stepId, int attempt, string error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stepId);
        ArgumentNullException.ThrowIfNull(error);
        this.Status = WorkflowRunStatus.Faulted;
        this.wait = null;
        var f = new WorkflowFault(stepId, attempt, error, this.timeProvider.GetUtcNow());
        this.fault = f;
        await this.PersistAsync(default, cancellationToken).ConfigureAwait(false);
        return f;
    }

    /// <inheritdoc/>
    public bool TryTakeDeliveredMessage(out JsonElement payload)
    {
        if (this.hasDeliveredMessage)
        {
            payload = this.deliveredMessage;
            this.hasDeliveredMessage = false;
            this.deliveredMessage = default;
            return true;
        }

        payload = default;
        return false;
    }

    /// <summary>
    /// Hands a delivered message to a run being resumed from a <see cref="WorkflowWaitKind.Message"/> wait, so
    /// the awaited receive step completes immediately with it instead of blocking. The worker calls this
    /// before re-entering <c>ExecuteAsync</c>; the payload must outlive that call.
    /// </summary>
    /// <param name="payload">The delivered message payload.</param>
    public void DeliverMessage(in JsonElement payload)
    {
        this.deliveredMessage = payload;
        this.hasDeliveredMessage = true;
    }

    /// <inheritdoc/>
    public void Dispose() => this.resumedState?.Dispose();

    private async ValueTask PersistAsync(JsonElement outputs, CancellationToken cancellationToken)
    {
        using Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("workflow.checkpoint");
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(ArazzoTelemetry.RunIdTag, this.Id.Value);
            activity.SetTag(ArazzoTelemetry.WorkflowIdTag, this.WorkflowId);
            activity.SetTag(ArazzoTelemetry.StatusTag, this.Status.ToString());
            activity.SetTag("corvus.arazzo.cursor", this.Cursor);
            if (this.correlationId is { } cid)
            {
                activity.SetTag(ArazzoTelemetry.CorrelationIdTag, cid);
            }
        }

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
            outputs,
            this.wait,
            this.fault,
            this.correlationId,
            this.tags,
            this.securityTags);

        var index = new WorkflowRunIndexEntry(
            this.WorkflowId,
            this.Status,
            this.createdAt,
            this.timeProvider.GetUtcNow(),
            DueAt: this.wait is { Kind: WorkflowWaitKind.Timer } timer ? timer.DueAt : null,
            AwaitingChannel: this.wait is { Kind: WorkflowWaitKind.Message } message ? message.Channel : null,
            AwaitingCorrelationId: this.wait is { Kind: WorkflowWaitKind.Message } messageCorrelation ? messageCorrelation.CorrelationId : null,
            ErrorType: this.fault?.Error,
            CorrelationId: this.correlationId,
            Tags: this.tags,
            SecurityTags: this.securityTags);

        long startedAt = Stopwatch.GetTimestamp();
        this.etag = await this.store.SaveAsync(this.Id, checkpoint, index, this.etag, cancellationToken).ConfigureAwait(false);
        ArazzoTelemetry.CheckpointDuration.Record(
            Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
            new KeyValuePair<string, object?>(ArazzoTelemetry.WorkflowIdTag, this.WorkflowId),
            new KeyValuePair<string, object?>(ArazzoTelemetry.StatusTag, this.Status.ToString()));
    }
}