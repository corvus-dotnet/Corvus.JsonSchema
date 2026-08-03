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

    /// <summary>
    /// Extends a lease the caller already holds, conditional on the presented lease still being the current one, and
    /// reports whether it is. This is the primitive behind the runner API's lease renewal and behind the lease check
    /// every operation on a run performs (ADR 0065 decision 2): <see cref="AcquireLeaseAsync"/> cannot serve either,
    /// because it re-acquires for the same owner and would therefore silently mint a fresh lease over one that had
    /// expired, been released, or been administratively fenced — turning a renewal that must fail into one that
    /// succeeds, on a run another runner may already hold.
    /// </summary>
    /// <param name="lease">The lease the caller holds. It is current only when the store's lease for
    /// <see cref="WorkflowLease.RunId"/> carries the same <see cref="WorkflowLease.Token"/> and
    /// <see cref="WorkflowLease.Owner"/> and has not expired. <see cref="WorkflowLease.ExpiresAt"/> is not read: a
    /// caller's view of its own expiry is not evidence, and taking it would let a holder extend itself without bound.</param>
    /// <param name="extension">How far past now to extend the lease. <see cref="TimeSpan.Zero"/> verifies the lease
    /// without writing anything, which is what an operation performed <em>under</em> a lease needs — a load or a save
    /// must not silently keep a lease alive, or the renewal the caller is required to make becomes decorative and a
    /// crashed holder's run is never reclaimed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The lease with its resulting expiry (the stored one when <paramref name="extension"/> is
    /// <see cref="TimeSpan.Zero"/>), or <see langword="null"/> when the presented lease is no longer current.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="extension"/> is negative.</exception>
    ValueTask<WorkflowLease?> TryExtendLeaseAsync(WorkflowLease lease, TimeSpan extension, CancellationToken cancellationToken);

    /// <summary>Deletes a run's checkpoint (e.g. after retention or operator removal).</summary>
    /// <param name="id">The run id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the run is removed. Deleting an unknown run is a no-op.</returns>
    ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken);
}