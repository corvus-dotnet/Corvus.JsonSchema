// <copyright file="IWorkflowLeaseAdministration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An optional capability of an <see cref="IWorkflowStateStore"/>: administrative invalidation of the advisory leases a
/// given owner holds (design §5.5). It is the control-plane-enforced half of the runner-revocation fence — when an
/// administrator revokes a runner, the store expires every lease that runner owns, so an authorized peer reclaims its
/// in-flight runs within a poll cycle rather than after the lease TTL, and the revoked runner's own further checkpoint
/// writes then conflict under optimistic concurrency (another owner has advanced the etag). Cooperative self-checks are
/// worthless against a compromised, attacker-controlled runner, so the fence is enforced here — in the store — not on the
/// runner.
/// </summary>
/// <remarks>
/// This is an opt-in capability negotiated with <c>store is IWorkflowLeaseAdministration</c>, exactly like
/// <see cref="IWorkflowWaitIndex"/> and <see cref="IWorkflowDispatchIndex"/>. A store that does not implement it cannot
/// fence in-flight work at revoke time (revocation still stops all future dispatch and orphan reclaim through the
/// dispatch gate); the caller records that the deployment's store lacks the capability so the gap is visible.
/// </remarks>
public interface IWorkflowLeaseAdministration
{
    /// <summary>
    /// Expires every advisory lease currently held by <paramref name="owner"/>, so any run they hold becomes immediately
    /// reclaimable by another owner (and the owner's next optimistic-concurrency write for that run conflicts). Idempotent —
    /// an owner holding no live leases expires none.
    /// </summary>
    /// <param name="owner">The lease owner whose leases to expire (a runner id — the same value passed to <see cref="IWorkflowStateStore.AcquireLeaseAsync"/>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of live leases expired.</returns>
    ValueTask<int> ExpireLeasesForOwnerAsync(string owner, CancellationToken cancellationToken);
}