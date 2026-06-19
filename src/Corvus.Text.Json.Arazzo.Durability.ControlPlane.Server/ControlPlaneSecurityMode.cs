// <copyright file="ControlPlaneSecurityMode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The control plane's security posture (design §17.4) — a single <strong>explicit</strong> choice that replaces the two
/// independently-optional flags (<c>requireAuthorization</c> + <c>rowSecurity</c>) whose insecure-by-omission defaults
/// the security review flagged (F2). There is no default: a deployment must name its posture, so it can never get an
/// open control plane, or capability scopes without row reach, by forgetting to configure security.
/// </summary>
/// <remarks>
/// The posture has three orthogonal axes — authentication, capability-scope gating, and row reach. These four modes
/// cover the meaningful combinations; each maps to the existing enforcement mechanics (the declared-scope convention and
/// the row-security policy).
/// </remarks>
public enum ControlPlaneSecurityMode
{
    /// <summary>
    /// Unauthenticated and unrestricted (full <see cref="AccessContext.System"/> reach). For local development and
    /// trusted-network deployments only — it exposes every operation to anonymous callers, so it is logged loudly at
    /// startup. A row-security policy must <strong>not</strong> be supplied (it would be silently ignored).
    /// </summary>
    Open,

    /// <summary>
    /// Authentication + capability-scope gating + per-row reach from a <see cref="ControlPlaneRowSecurityPolicy"/> — the
    /// production posture. A policy is <strong>required</strong> (so a deployment can never get scopes without reach by
    /// omission, the F2 cross-tenant footgun).
    /// </summary>
    Scoped,

    /// <summary>
    /// Authentication + capability-scope gating, but unrestricted (<see cref="AccessContext.System"/>) row reach — for a
    /// genuinely single-tenant deployment that wants capability gating without row security. A deliberate choice, never
    /// reached by omission; a row-security policy must <strong>not</strong> be supplied.
    /// </summary>
    ScopesOnly,

    /// <summary>
    /// Authentication + per-row reach from a <see cref="ControlPlaneRowSecurityPolicy"/>, but <strong>no</strong>
    /// capability-scope gating — every authenticated principal may invoke every operation, restricted only by row reach.
    /// A policy is <strong>required</strong>.
    /// </summary>
    RowSecurityOnly,
}