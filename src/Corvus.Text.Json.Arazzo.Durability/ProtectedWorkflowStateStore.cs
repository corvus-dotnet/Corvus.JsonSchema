// <copyright file="ProtectedWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Wraps any <see cref="IWorkflowStateStore"/> to encrypt checkpoints with an <see cref="ICheckpointProtector"/>
/// before they reach the backend and decrypt them on read — application-level encryption at rest, independent
/// of (and in addition to) the backend's own at-rest encryption. Because every backend stores the checkpoint as
/// an opaque blob, this decorator works for all of them and the same store-conformance suite passes through it.
/// </summary>
/// <remarks>
/// Only the checkpoint payload is encrypted; the projected <see cref="WorkflowRunIndexEntry"/> fields (status,
/// workflow id, due time, awaiting channel/correlation, error type) pass through in the clear so the backend can
/// still serve the wait/visibility queries. If a correlation id is itself sensitive, store a deterministic hash
/// of it instead of the raw value. The etag is the backend's and passes through unchanged. If the inner store
/// also implements <see cref="IWorkflowWaitIndex"/>, so does this wrapper (delegating); otherwise the wait-index
/// members throw <see cref="NotSupportedException"/>.
/// </remarks>
public sealed class ProtectedWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IWorkflowDispatchIndex, IAsyncDisposable
{
    private readonly IWorkflowStateStore inner;
    private readonly IWorkflowWaitIndex? innerIndex;
    private readonly IWorkflowDispatchIndex? innerDispatch;
    private readonly ICheckpointProtector protector;

    /// <summary>Initializes a new instance of the <see cref="ProtectedWorkflowStateStore"/> class.</summary>
    /// <param name="inner">The backend store to wrap.</param>
    /// <param name="protector">The protector that encrypts/decrypts the checkpoint bytes.</param>
    public ProtectedWorkflowStateStore(IWorkflowStateStore inner, ICheckpointProtector protector)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(protector);
        this.inner = inner;
        this.innerIndex = inner as IWorkflowWaitIndex;
        this.innerDispatch = inner as IWorkflowDispatchIndex;
        this.protector = protector;
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
        => this.SaveCoreAsync(id, checkpointUtf8, index, expected, cancellationToken);

    // The interface passes the index by `in`; an async method cannot take an `in` parameter, so SaveAsync
    // copies it (a small struct) and this private core does the encrypt-then-write.
    private async ValueTask<WorkflowEtag> SaveCoreAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> protectedCheckpoint = await this.protector.ProtectAsync(checkpointUtf8, id, cancellationToken).ConfigureAwait(false);
        return await this.inner.SaveAsync(id, protectedCheckpoint, index, expected, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        WorkflowCheckpoint? loaded = await this.inner.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded is not { } checkpoint)
        {
            return null;
        }

        ReadOnlyMemory<byte> plaintext = await this.protector.UnprotectAsync(checkpoint.Utf8, id, cancellationToken).ConfigureAwait(false);
        return new WorkflowCheckpoint(plaintext, checkpoint.Etag);
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
        => this.inner.AcquireLeaseAsync(id, owner, ttl, cancellationToken);

    /// <inheritdoc/>
    public ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
        => this.inner.ReleaseLeaseAsync(lease, cancellationToken);

    /// <inheritdoc/>
    public ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
        => this.inner.DeleteAsync(id, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, CancellationToken cancellationToken)
        => this.RequireIndex().QueryDueAsync(before, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, CancellationToken cancellationToken)
        => this.RequireIndex().QueryAwaitingAsync(channel, correlationId, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
        => this.RequireIndex().QueryAsync(query, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, CancellationToken cancellationToken)
        => (this.innerDispatch ?? throw new NotSupportedException("The wrapped store does not implement IWorkflowDispatchIndex.")).QueryClaimableAsync(hostedWorkflowIds, now, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, string? runnerEnvironment, DateTimeOffset now, CancellationToken cancellationToken)
        => (this.innerDispatch ?? throw new NotSupportedException("The wrapped store does not implement IWorkflowDispatchIndex.")).QueryClaimableAsync(hostedWorkflowIds, runnerEnvironment, now, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.protector is IDisposable disposableProtector)
        {
            disposableProtector.Dispose();
        }

        switch (this.inner)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IWorkflowWaitIndex RequireIndex()
        => this.innerIndex ?? throw new NotSupportedException("The wrapped store does not implement IWorkflowWaitIndex.");
}