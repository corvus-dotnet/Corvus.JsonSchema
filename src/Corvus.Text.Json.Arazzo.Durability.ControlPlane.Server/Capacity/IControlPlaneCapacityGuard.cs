// <copyright file="IControlPlaneCapacityGuard.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Capacity;

/// <summary>
/// Bounds what a tenant may accumulate in the store (ADR 0065 decision 3): how many runs it may have outstanding, how
/// many runners it may register in an environment, and how many waits it may leave parked.
/// </summary>
/// <remarks>
/// <para>
/// Separate from the runner API's quota guard, and deliberately not the same instrument. That one bounds a <em>rate</em>
/// with a token bucket, which is the right shape for flow and the wrong shape for this: a magnitude is a standing total
/// that must survive a restart. A bucket-based cap would forget everything the store still holds the moment a process
/// recycled, and report a tenant as being well inside a limit it had exceeded hours earlier.
/// </para>
/// <para>
/// A capacity refusal is therefore not a rate refusal wearing a different name. Waiting does not clear it. The caller
/// has to release capacity — complete or purge runs, deregister a runner, resolve parked waits — before the request
/// will be admitted, which is why the contract documents <c>Retry-After</c> on these operations as advisory rather than
/// a promise.
/// </para>
/// <para>
/// The counter is bounded on the way in: implementations count only as far as the cap, so a tenant far above its limit
/// costs the same to refuse as one just over it, and the check never becomes a scan of the population it is protecting.
/// </para>
/// </remarks>
public interface IControlPlaneCapacityGuard
{
    /// <summary>Whether admitting one more of <paramref name="kind"/> would exceed the tenant's capacity.</summary>
    /// <param name="kind">The magnitude being checked.</param>
    /// <param name="counter">What the magnitude is measured against: the tenant for a run count, the environment for a
    /// registered-runner cap.</param>
    /// <param name="context">The caller's access context, so the count sees exactly the rows the caller's reach does
    /// and a tenant is never measured against another's population.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The refusal, or <see langword="null"/> when there is room.</returns>
    ValueTask<ControlPlaneCapacityRejection?> TryAdmitAsync(ControlPlaneCapacityKind kind, string counter, AccessContext context, CancellationToken cancellationToken);
}