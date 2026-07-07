// <copyright file="InMemoryDraftRunTraceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The in-memory reference implementation of <see cref="IDraftRunTraceStore"/>: the latest trace per run held in a
/// dictionary as an owned copy of its bytes, so a caller reusing or mutating its buffer after the put cannot
/// corrupt the stored trace — exactly how <see cref="InMemoryDraftRunStore"/> owns its package blob. The reference
/// against which the shared trace-store conformance suite runs, and usable for a real single-process host that does
/// not need traces to survive a restart.
/// </summary>
public sealed class InMemoryDraftRunTraceStore : IDraftRunTraceStore
{
    private readonly Dictionary<string, byte[]> traces = [];
    private readonly Lock gate = new();

    /// <inheritdoc/>
    public ValueTask PutAsync(WorkflowRunId id, ReadOnlyMemory<byte> traceUtf8, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            this.traces[id.Value] = traceUtf8.ToArray();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            return this.traces.TryGetValue(id.Value, out byte[]? trace)
                ? ValueTask.FromResult<ReadOnlyMemory<byte>?>(trace)
                : ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            return ValueTask.FromResult(this.traces.Remove(id.Value));
        }
    }
}