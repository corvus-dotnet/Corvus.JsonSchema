// <copyright file="FakeWorkflowRun.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>A minimal in-memory <see cref="IWorkflowRun"/> for a fresh, straight-through completing run.</summary>
internal sealed class FakeWorkflowRun : IWorkflowRun
{
    public bool Completed { get; private set; }

    public int Cursor => 0;

    public string? CorrelationId => null;

    public Dictionary<string, byte[]> CorrelationTokens { get; } = new(StringComparer.Ordinal);

    public bool TryGetStepOutputs(string stepId, out JsonElement outputs)
    {
        outputs = default;
        return false;
    }

    public int GetRetryCount(string stepId) => 0;

    public void SetStepOutputs(string stepId, in JsonElement outputs)
    {
    }

    public void SetRetryCount(string stepId, int count)
    {
    }

    public ValueTask CheckpointAsync(int cursor, CancellationToken cancellationToken) => default;

    public ValueTask CompleteAsync(JsonElement outputs, CancellationToken cancellationToken)
    {
        this.Completed = true;
        return default;
    }

    public ValueTask<WorkflowWait> SuspendForTimerAsync(int cursor, TimeSpan delay, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public ValueTask<WorkflowWait> SuspendForMessageAsync(int cursor, string channel, string? correlationId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public ValueTask<WorkflowFault> FaultAsync(string stepId, int attempt, string error, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public bool TryTakeDeliveredMessage(out JsonElement payload)
    {
        payload = default;
        return false;
    }
}
