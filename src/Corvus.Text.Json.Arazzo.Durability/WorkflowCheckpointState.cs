// <copyright file="WorkflowCheckpointState.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The deserialized contents of a checkpoint row: the run's complete resumable state, as the join of the runner
/// region, the payload and the control-plane region (ADR 0065 decisions 4 and 7). It owns the parsed payload document
/// the <see cref="Inputs"/> and <see cref="StepOutputs"/> elements point into, so it must be disposed once the run
/// that resumed from it has finished reading those products. For an encrypted row read without the key, the payload
/// is absent and <see cref="PayloadSealed"/> says so.
/// </summary>
/// <remarks>
/// The lifecycle values here are the <em>effective</em> ones: <see cref="Status"/>, <see cref="Wait"/>,
/// <see cref="Fault"/> and <see cref="ResumeRequestedAt"/> already take the control plane's decisions into account
/// (a cancellation, a budget fault, a resume request), so a reader acts on what the run is rather than on what the
/// runner last wrote. What the runner itself wrote is <see cref="RunnerStatus"/> and <see cref="RunnerFault"/>, and the
/// control plane's own record is <see cref="ControlPlane"/>.
/// </remarks>
public sealed class WorkflowCheckpointState : IDisposable
{
    private readonly ParsedJsonDocument<JsonElement>? payload;

    internal WorkflowCheckpointState(
        ParsedJsonDocument<JsonElement>? payload,
        ReadOnlyMemory<byte> row,
        in CheckpointEnvelope envelope,
        PooledUtf8Map<int> retryCounters,
        in ControlPlaneRecord controlPlane,
        ReadOnlyMemory<byte> controlPlaneRegion,
        Dictionary<string, byte[]> correlationTokens,
        JsonElement inputs,
        PooledUtf8Map<JsonElement> stepOutputs,
        JsonElement outputs,
        WorkflowWait? clearWait = null)
    {
        this.ClearWait = clearWait;
        this.payload = payload;
        this.PayloadSealed = payload is null;
        this.Row = row;
        this.ControlPlaneRegion = controlPlaneRegion;
        this.RunId = envelope.RunId;
        this.Environment = envelope.Environment;
        this.WorkflowId = envelope.WorkflowId;
        this.RunnerStatus = envelope.Status;
        this.Cursor = envelope.Cursor;
        this.Sequence = envelope.Sequence;
        this.Epoch = envelope.Epoch;
        this.Incarnation = envelope.Incarnation;
        this.SealedStart = envelope.SealedStart;
        this.CreatedAt = envelope.CreatedAt;
        this.UpdatedAt = envelope.UpdatedAt;
        this.CorrelationId = envelope.CorrelationId;
        this.RerunOf = envelope.RerunOf;
        this.Tags = envelope.Tags;
        this.SecurityTags = envelope.SecurityTags;
        this.StepJournal = envelope.StepJournal;
        this.JournalTruncated = envelope.JournalTruncated;
        this.RunnerFault = envelope.Fault;
        this.RetryCounters = retryCounters;
        this.ControlPlane = controlPlane;
        this.CorrelationTokens = correlationTokens;
        this.Inputs = inputs;
        this.StepOutputs = stepOutputs;
        this.Outputs = outputs;

        // The join (ADR 0065 decision 7): the control plane's decisions override the runner's last word. A
        // cancellation is unconditional; a budget fault holds until the runner advances past the sequence it was
        // recorded against; a resume request is outstanding on the same rule.
        (this.Status, this.Wait, this.Fault) = WorkflowCheckpointSerializer.Join(envelope, controlPlane);
        this.ResumeRequestedAt = controlPlane.ResumeRequestedAt(envelope.Sequence);
        this.Pause = controlPlane.Pause;
        this.Budget = controlPlane.Budget;
    }

    /// <summary>Gets the row these contents were read from, exactly as stored, for a control-plane write that replaces its control-plane region and nothing else.</summary>
    public ReadOnlyMemory<byte> Row { get; }

    /// <summary>
    /// Gets a value indicating whether the row's payload is encrypted and was not opened (ADR 0065 decision 5): the
    /// reader holds no key. <see cref="Inputs"/> and <see cref="Outputs"/> are then undefined and
    /// <see cref="StepOutputs"/> and <see cref="CorrelationTokens"/> empty, and a reader that needs them says the
    /// payload is sealed rather than that there is none.
    /// </summary>
    public bool PayloadSealed { get; }

    /// <summary>Gets the control-plane region's bytes as the row carries them, which a runner save re-emits verbatim.</summary>
    public ReadOnlyMemory<byte> ControlPlaneRegion { get; }

    /// <summary>Gets the run id.</summary>
    public WorkflowRunId RunId { get; }

    /// <summary>Gets the id of the workflow the run executes.</summary>
    public string WorkflowId { get; }

    /// <summary>Gets the run's effective lifecycle status: the runner's, unless the control plane cancelled the run or faulted it on its budget.</summary>
    public WorkflowRunStatus Status { get; }

    /// <summary>Gets the lifecycle status the runner region carries, before the control plane's decisions are applied.</summary>
    public WorkflowRunStatus RunnerStatus { get; }

    /// <summary>Gets the cursor (state-machine index of the next step to run).</summary>
    public int Cursor { get; }

    /// <summary>Gets the per-run write sequence this checkpoint was persisted at (ADR 0065 decision 6). A resumed run
    /// continues from it, so the sequence stays monotonic across the run's whole life rather than restarting whenever
    /// the run is rehydrated.</summary>
    public long Sequence { get; }

    /// <summary>Gets the lease epoch the runner wrote into its region (ADR 0065 decision 6), or <see langword="null"/> for a writer that holds no lease grant (the in-process runner).</summary>
    public long? Epoch { get; }

    /// <summary>Gets the tenant-attested store incarnation the runner wrote into its region (ADR 0065 decision 6), or <see langword="null"/> for a writer in an environment that is not anchored.</summary>
    public ulong? Incarnation { get; }

    /// <summary>
    /// Gets a value indicating whether the run was started sealed by an initiator (ADR 0065 decision 9). At sequence
    /// 0 with <see cref="PayloadSealed"/> the row is the sealed genesis row as the control plane wrote it, and only
    /// the runner's <see cref="SealingCheckpointStore"/> can open the inputs; beyond it the runner has vouched for
    /// the flag in every save.
    /// </summary>
    public bool SealedStart { get; }

    /// <summary>Gets the instant the run was first created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets when the checkpoint was written, if the writer stamped it.</summary>
    public DateTimeOffset? UpdatedAt { get; }

    /// <summary>Gets the run's per-step journal (ADR 0050): one payload-free entry per step execution recorded so far.</summary>
    public IReadOnlyList<WorkflowStepJournalEntry> StepJournal { get; }

    /// <summary>Gets a value indicating whether the journal was capped and its oldest entries dropped.</summary>
    public bool JournalTruncated { get; }

    /// <summary>Gets the id of the run this run re-runs, or <see langword="null"/> for a run that was started in its own right.</summary>
    public string? RerunOf { get; }

    /// <summary>Gets the run's effective execution budget (ADR 0068), from the control-plane region, or <see langword="null"/> when the control plane resolved none.</summary>
    public ExecutionBudget? Budget { get; }

    /// <summary>Gets the control plane's own record for the run: its cancellation, budget fault, resume request, pause and budget.</summary>
    public ControlPlaneRecord ControlPlane { get; }

    /// <summary>Gets the restored per-step retry attempt counts (a pooled UTF-8-keyed map; disposed with this state).</summary>
    public PooledUtf8Map<int> RetryCounters { get; }

    /// <summary>Gets the restored correlation register (correlation-id name → token bytes).</summary>
    public Dictionary<string, byte[]> CorrelationTokens { get; }

    /// <summary>Gets the workflow inputs (an <see cref="JsonValueKind.Undefined"/> element if none were stored).</summary>
    public JsonElement Inputs { get; }

    /// <summary>Gets the restored per-step <c>outputs</c> products (a pooled UTF-8-keyed map of borrowed views into the parsed payload; disposed with this state).</summary>
    public PooledUtf8Map<JsonElement> StepOutputs { get; }

    /// <summary>Gets the final workflow <c>outputs</c> if the run had completed (an <see cref="JsonValueKind.Undefined"/> element otherwise).</summary>
    public JsonElement Outputs { get; }

    /// <summary>Gets the wait describing why the run is suspended, if it is; cleared when the control plane cancelled or faulted the run.</summary>
    public WorkflowWait? Wait { get; }

    /// <summary>Gets the clear channel and correlation id behind a blinded message wait (ADR 0065 decision 12), from the opened payload; <see langword="null"/> when the wait is not blinded or the payload is sealed.</summary>
    public WorkflowWait? ClearWait { get; }

    /// <summary>Gets the effective fault record: the control plane's budget fault while it is in effect, otherwise the runner's own fault.</summary>
    public WorkflowFault? Fault { get; }

    /// <summary>Gets the fault record the runner region carries, before the control plane's decisions are applied.</summary>
    public WorkflowFault? RunnerFault { get; }

    /// <summary>Gets the run-wide telemetry correlation id (the W3C trace id) set at creation, if any.</summary>
    public string? CorrelationId { get; }

    /// <summary>Gets the free-form tags applied to the run at creation, if any.</summary>
    public TagSet Tags { get; }

    /// <summary>Gets the security tags (KVP labels) applied to the run at creation, if any (design §14.2).</summary>
    public SecurityTagSet SecurityTags { get; }

    /// <summary>Gets the deployment environment the run is pinned to (design §5.5): its credential set and the runners it can be dispatched to.</summary>
    public string Environment { get; }

    /// <summary>Gets the §18 debugger pause configuration the control plane set, if any: the stop points a claiming runner applies on the next advance.</summary>
    public WorkflowPauseConfig? Pause { get; }

    /// <summary>Gets the instant the control plane marked this run resume-claimable (§18), while that request is outstanding: the runner's next save consumes it.</summary>
    public DateTimeOffset? ResumeRequestedAt { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The maps hold views into the payload document; return their pooled buffers before the document's.
        this.RetryCounters.Dispose();
        this.StepOutputs.Dispose();
        this.payload?.Dispose();
    }
}