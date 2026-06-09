// <copyright file="WorkflowCheckpointState.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The deserialized contents of a checkpoint document — the run's complete resumable state. It owns the
/// parsed JSON document the <see cref="Inputs"/> and <see cref="StepOutputs"/> elements point into, so it
/// must be disposed once the run that resumed from it has finished reading those products.
/// </summary>
public sealed class WorkflowCheckpointState : IDisposable
{
    private readonly ParsedJsonDocument<JsonElement> document;

    internal WorkflowCheckpointState(
        ParsedJsonDocument<JsonElement> document,
        WorkflowRunId runId,
        string workflowId,
        WorkflowRunStatus status,
        int cursor,
        DateTimeOffset createdAt,
        Dictionary<string, int> retryCounters,
        Dictionary<string, byte[]> correlationTokens,
        JsonElement inputs,
        Dictionary<string, JsonElement> stepOutputs,
        JsonElement outputs,
        WorkflowWait? wait,
        WorkflowFault? fault)
    {
        this.document = document;
        this.RunId = runId;
        this.WorkflowId = workflowId;
        this.Status = status;
        this.Cursor = cursor;
        this.CreatedAt = createdAt;
        this.RetryCounters = retryCounters;
        this.CorrelationTokens = correlationTokens;
        this.Inputs = inputs;
        this.StepOutputs = stepOutputs;
        this.Outputs = outputs;
        this.Wait = wait;
        this.Fault = fault;
    }

    /// <summary>Gets the run id.</summary>
    public WorkflowRunId RunId { get; }

    /// <summary>Gets the id of the workflow the run executes.</summary>
    public string WorkflowId { get; }

    /// <summary>Gets the run's lifecycle status at the checkpoint.</summary>
    public WorkflowRunStatus Status { get; }

    /// <summary>Gets the cursor (state-machine index of the next step to run).</summary>
    public int Cursor { get; }

    /// <summary>Gets the instant the run was first created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets the restored per-step retry attempt counts.</summary>
    public Dictionary<string, int> RetryCounters { get; }

    /// <summary>Gets the restored correlation register (correlation-id name → token bytes).</summary>
    public Dictionary<string, byte[]> CorrelationTokens { get; }

    /// <summary>Gets the workflow inputs (an <see cref="JsonValueKind.Undefined"/> element if none were stored).</summary>
    public JsonElement Inputs { get; }

    /// <summary>Gets the restored per-step <c>outputs</c> products.</summary>
    public Dictionary<string, JsonElement> StepOutputs { get; }

    /// <summary>Gets the final workflow <c>outputs</c> if the run had completed (an <see cref="JsonValueKind.Undefined"/> element otherwise).</summary>
    public JsonElement Outputs { get; }

    /// <summary>Gets the wait describing why the run is suspended, if it is (Tier 2).</summary>
    public WorkflowWait? Wait { get; }

    /// <summary>Gets the fault record if the run is faulted (Tier 2).</summary>
    public WorkflowFault? Fault { get; }

    /// <inheritdoc/>
    public void Dispose() => this.document.Dispose();
}