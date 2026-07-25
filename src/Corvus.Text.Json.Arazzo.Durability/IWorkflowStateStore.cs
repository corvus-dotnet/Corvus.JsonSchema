// <copyright file="IWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The pluggable, backend-agnostic run store a <em>dispatching</em> runner sits on: the opaque checkpoint core
/// (<see cref="IWorkflowCheckpointStore"/> — key/value by run id under an etag) plus an advisory single-owner
/// lease and deletion. A dispatching host needs all of it; a checkpoint-only host (the serverless
/// function-side store, which never leases or deletes) needs only the core. Richer backends additionally
/// implement <c>IWorkflowWaitIndex</c> (Tier 2) to answer due-timer and correlation wakeups; capability is
/// negotiated with <c>store is IWorkflowWaitIndex</c>.
/// </summary>
/// <remarks>
/// The store is a <em>host-level</em> concern: it is wired at startup and referenced by nothing the code
/// generator emits. The generated executor only ever touches <see cref="IWorkflowRun"/>, which a host builds
/// over the checkpoint core.
/// </remarks>
public interface IWorkflowStateStore : IWorkflowCheckpointStore
{
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