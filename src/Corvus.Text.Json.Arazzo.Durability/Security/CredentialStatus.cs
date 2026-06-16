// <copyright file="CredentialStatus.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The derived health of a <see cref="SourceCredentialBinding"/>'s referenced secret (design §13.2). This is
/// <strong>computed</strong> from the binding's <c>expiresAt</c> against the current time, never persisted — so it
/// cannot go stale — and is non-sensitive (no secret material is involved). It drives the operator's rotation
/// worklist (telemetry, the catalog-version rollup, and the new-trigger gate in later slices).
/// </summary>
public enum CredentialStatus
{
    /// <summary>The credential is valid and not within the expiring-soon window (or has no known expiry).</summary>
    Valid,

    /// <summary>The credential is still valid but expires within the configured warning window — rotate soon.</summary>
    ExpiringSoon,

    /// <summary>The credential has expired; a new trigger against it is gated and an in-flight run faults (§13.3).</summary>
    Expired,
}