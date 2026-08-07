// <copyright file="ControlPlaneCapacityKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Capacity;

/// <summary>
/// The standing magnitudes the control plane bounds (ADR 0065 decision 3). Each is a total the store holds, not a rate
/// of arrival.
/// </summary>
/// <remarks>
/// Decision 3 names two further magnitudes -- a parked-wait cap and a total payload-bytes quota -- which are NOT here.
/// Both are incurred when a checkpoint is written, which happens through the runner API, and that assembly does not
/// reference this one. Enforcing them from here would invert the dependency ADR 0065 exists to establish, so they are
/// bounded on the checkpoint write path instead, against a guard that path can see.
/// </remarks>
public enum ControlPlaneCapacityKind
{
    /// <summary>How many runs a tenant may have IN FLIGHT: Pending, Running, or Suspended.</summary>
    /// <remarks>
    /// A bound on concurrency. It is what stops one tenant occupying the dispatch capacity every tenant shares, and it
    /// releases itself: a run that finishes gives its slot back with no operator action.
    /// </remarks>
    ConcurrentRuns,

    /// <summary>How many runs a tenant may have STORED, whatever their status.</summary>
    /// <remarks>
    /// <para>
    /// A bound on storage, which is a different resource from concurrency and is not bounded by it: a tenant can sit at
    /// zero concurrency and still hold millions of terminal runs. ADR 0065 groups its "run-count cap" with the total
    /// payload-bytes quota, both store magnitudes, which is the reading this takes.
    /// </para>
    /// <para>
    /// It does NOT release itself. There is no automatic retention: a completed run keeps its row until it is purged,
    /// so this cap is cleared by purging and never by finishing work. That is why it is paired with a scheduled
    /// retention sweep -- without one, a tenant that never purges reaches this cap eventually however well behaved it
    /// is, and a storage bound with no reclamation is a slow outage rather than a limit.
    /// </para>
    /// </remarks>
    StoredRuns,

    /// <summary>How many runners may be registered for one environment.</summary>
    /// <remarks>Per environment rather than per tenant: it bounds the fan-out serving one environment's work, which is
    /// what the blast radius is drawn around (ADR 0065's residues).</remarks>
    RegisteredRunners,
}