// <copyright file="InMemoryWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The in-memory reference implementation of <see cref="IWorkflowStateStore"/>. It keeps checkpoints in a
/// dictionary with a monotonic version as the etag and an in-process advisory lease, so the whole
/// durability mechanism is unit-testable with no external store — exactly as <c>InMemoryMessageTransport</c>
/// does for AsyncAPI. It is the reference against which the shared store-conformance suite runs, and is also
/// usable for a real single-process run that does not need to survive a host restart.
/// </summary>
public sealed class InMemoryWorkflowStateStore : IWorkflowStateStore
{
    private readonly Dictionary<string, Entry> entries = [];
    private readonly Dictionary<string, LeaseRecord> leases = [];
    private readonly TimeProvider timeProvider;
    private readonly Lock gate = new();
    private long version;
    private long leaseToken;

    /// <summary>Initializes a new instance of the <see cref="InMemoryWorkflowStateStore"/> class.</summary>
    /// <param name="timeProvider">The time source used for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryWorkflowStateStore(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            bool exists = this.entries.TryGetValue(id.Value, out Entry current);
            if (exists)
            {
                if (expected.IsNone || current.Etag.Value != expected.Value)
                {
                    throw new WorkflowConflictException(id, expected);
                }
            }
            else if (!expected.IsNone)
            {
                throw new WorkflowConflictException(id, expected);
            }

            var newEtag = new WorkflowEtag((++this.version).ToString(System.Globalization.CultureInfo.InvariantCulture));
            this.entries[id.Value] = new Entry(checkpointUtf8.ToArray(), newEtag, index);
            return ValueTask.FromResult(newEtag);
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            return this.entries.TryGetValue(id.Value, out Entry current)
                ? ValueTask.FromResult<WorkflowCheckpoint?>(new WorkflowCheckpoint(current.Checkpoint, current.Etag))
                : ValueTask.FromResult<WorkflowCheckpoint?>(null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            if (this.leases.TryGetValue(id.Value, out LeaseRecord existing) && existing.ExpiresAt > now && existing.Owner != owner)
            {
                return ValueTask.FromResult<WorkflowLease?>(null);
            }

            string token = (++this.leaseToken).ToString(System.Globalization.CultureInfo.InvariantCulture);
            DateTimeOffset expiresAt = now + ttl;
            this.leases[id.Value] = new LeaseRecord(owner, token, expiresAt);
            return ValueTask.FromResult<WorkflowLease?>(new WorkflowLease(id, owner, token, expiresAt));
        }
    }

    /// <inheritdoc/>
    public ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            if (this.leases.TryGetValue(lease.RunId.Value, out LeaseRecord existing) && existing.Token == lease.Token)
            {
                this.leases.Remove(lease.RunId.Value);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            this.entries.Remove(id.Value);
            this.leases.Remove(id.Value);
        }

        return ValueTask.CompletedTask;
    }

    private readonly record struct Entry(byte[] Checkpoint, WorkflowEtag Etag, WorkflowRunIndexEntry Index);

    private readonly record struct LeaseRecord(string Owner, string Token, DateTimeOffset ExpiresAt);
}