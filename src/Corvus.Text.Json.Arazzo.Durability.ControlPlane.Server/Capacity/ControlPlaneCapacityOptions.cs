// <copyright file="ControlPlaneCapacityOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Capacity;

/// <summary>
/// The deployment's standing capacity limits (ADR 0065 decision 3). A limit of zero or less enforces nothing.
/// </summary>
/// <remarks>
/// The figures are starting points sized to sit clear of a working deployment rather than measured against one. They
/// exist so a deployment that enables capacity limits without tuning refuses only plainly abnormal accumulation, and so
/// the refusal path is exercised rather than dormant.
/// </remarks>
public sealed class ControlPlaneCapacityOptions
{
    /// <summary>Gets or sets how many runs one tenant may have in flight (Pending, Running, or Suspended). Defaults to 10,000.</summary>
    /// <remarks>Bounds concurrency, and releases itself: a run that finishes gives its slot back with no operator
    /// action, so this can be enforced from the outset without stranding anyone.</remarks>
    public int ConcurrentRunsPerTenant { get; set; } = 10_000;

    /// <summary>Gets or sets how many runs one tenant may have stored, whatever their status. <strong>Defaults to 0
    /// (disabled).</strong></summary>
    /// <remarks>
    /// <para>
    /// Off by default deliberately, and this is not timidity. Stored runs do not release themselves: there is no
    /// automatic retention, so a completed run keeps its row until it is purged. Enforcing this limit before a
    /// reclamation path exists would refuse new runs to a perfectly well-behaved tenant that had simply been running
    /// for long enough, while it sat completely idle -- a slow outage rather than a limit.
    /// </para>
    /// <para>
    /// The scheduled retention sweep is what makes it safe to switch on, and the default becomes non-zero in the same
    /// change that lands the sweep. Until then a deployment that wants the bound sets it knowingly and takes on the
    /// purging itself.
    /// </para>
    /// </remarks>
    public int StoredRunsPerTenant { get; set; }

    /// <summary>Gets or sets how many runners may be registered for one environment. Defaults to 500.</summary>
    /// <remarks>Releases itself as runners are deregistered or pruned for going stale.</remarks>
    public int RegisteredRunnersPerEnvironment { get; set; } = 500;

    /// <summary>The configured limit for one magnitude.</summary>
    /// <param name="kind">The magnitude.</param>
    /// <returns>The limit, or zero or less when the magnitude is not enforced.</returns>
    public int For(ControlPlaneCapacityKind kind) => kind switch
    {
        ControlPlaneCapacityKind.ConcurrentRuns => this.ConcurrentRunsPerTenant,
        ControlPlaneCapacityKind.StoredRuns => this.StoredRunsPerTenant,
        ControlPlaneCapacityKind.RegisteredRunners => this.RegisteredRunnersPerEnvironment,
        _ => 0,
    };
}