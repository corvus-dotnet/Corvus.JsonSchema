// <copyright file="IWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The pluggable, backend-agnostic checkpoint store the durability layer sits on. It is the universal core
/// every backend can implement: key/value by run id, optimistic concurrency via an etag, and an advisory
/// single-owner lease. Richer backends additionally implement <c>IWorkflowWaitIndex</c> (Tier 2) to answer
/// due-timer and correlation wakeups; capability is negotiated with <c>store is IWorkflowWaitIndex</c>.
/// </summary>
/// <remarks>
/// The store is a <em>host-level</em> concern: it is wired at startup and referenced by nothing the code
/// generator emits. The generated executor only ever touches <see cref="IWorkflowRun"/>, which a host builds
/// over this store. A backend never parses the checkpoint — it stores the opaque bytes by id at an etag and
/// indexes the few projected <see cref="WorkflowRunIndexEntry"/> fields — so each adapter stays thin.
/// </remarks>
public interface IWorkflowStateStore
{
    /// <summary>
    /// Creates or updates a run's checkpoint under optimistic concurrency.
    /// </summary>
    /// <param name="id">The run id.</param>
    /// <param name="checkpointUtf8">The opaque serialized checkpoint document (UTF-8 JSON).</param>
    /// <param name="index">The projected fields to index alongside the bytes.</param>
    /// <param name="expected">
    /// The etag the caller last read; pass <see cref="WorkflowEtag.None"/> to create a run that must not yet
    /// exist. The save fails with <see cref="WorkflowConflictException"/> if the store's current etag differs.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The new etag to use as <paramref name="expected"/> on the next save.</returns>
    /// <exception cref="WorkflowConflictException">The stored etag did not match <paramref name="expected"/>.</exception>
    ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken);

    /// <summary>Loads a run's checkpoint and the etag it was read at.</summary>
    /// <param name="id">The run id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The checkpoint, or <see langword="null"/> if no run with that id exists.</returns>
    ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken);

    /// <summary>Acquires an advisory single-owner lease on a run.</summary>
    /// <param name="id">The run id.</param>
    /// <param name="owner">The opaque identity of the worker requesting the lease.</param>
    /// <param name="ttl">How long the lease is held before it may be re-acquired by another owner.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The lease, or <see langword="null"/> if another owner currently holds an unexpired lease.</returns>
    ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>Releases a lease previously acquired with <see cref="AcquireLeaseAsync"/>.</summary>
    /// <param name="lease">The lease to release.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the lease is released. Releasing an expired or superseded lease is a no-op.</returns>
    ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken);

    /// <summary>Deletes a run's checkpoint (e.g. after retention or operator removal).</summary>
    /// <param name="id">The run id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the run is removed. Deleting an unknown run is a no-op.</returns>
    ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken);
}