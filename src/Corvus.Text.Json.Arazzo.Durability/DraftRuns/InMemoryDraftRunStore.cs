// <copyright file="InMemoryDraftRunStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The in-memory reference implementation of <see cref="IDraftRunStore"/>: captures held in a dictionary, the
/// record persisted as its serialized JSON document (so reads parse exactly what a real backend would return)
/// and the package as an owned copy of its bytes. The reference against which the shared draft-run-store
/// conformance suite runs, and usable for a real single-process host that does not need captures to survive a
/// restart.
/// </summary>
public sealed class InMemoryDraftRunStore : IDraftRunStore
{
    private readonly Dictionary<string, Entry> entries = [];
    private readonly Lock gate = new();

    /// <inheritdoc/>
    public ValueTask PutAsync(WorkflowRunId id, DraftRun record, ReadOnlyMemory<byte> package, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] recordUtf8 = PersistedJson.ToArray(record, static (Utf8JsonWriter writer, in DraftRun r) => r.WriteTo(writer));
        lock (this.gate)
        {
            this.entries[id.Value] = new Entry(recordUtf8, package.ToArray());
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<DraftRun>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            return this.entries.TryGetValue(id.Value, out Entry entry)
                ? ValueTask.FromResult<ParsedJsonDocument<DraftRun>?>(PersistedJson.ToPooledDocument<DraftRun>(entry.RecordUtf8))
                : ValueTask.FromResult<ParsedJsonDocument<DraftRun>?>(null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            return this.entries.TryGetValue(id.Value, out Entry entry)
                ? ValueTask.FromResult<ReadOnlyMemory<byte>?>(entry.Package)
                : ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            return ValueTask.FromResult(this.entries.Remove(id.Value));
        }
    }

    private readonly record struct Entry(byte[] RecordUtf8, byte[] Package);
}