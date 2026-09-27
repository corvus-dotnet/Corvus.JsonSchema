// <copyright file="ExecutorAdmission.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Whether a loaded executor may run in an environment under the runner's own executor policy (ADR 0065 phase C),
/// decided after the loader has verified the executor against the platform's signature and before a run is advanced
/// through it.
/// </summary>
public enum ExecutorAdmission
{
    /// <summary>The executor is one the tenant authorized for the environment: its assembly digest is on the runner's allowlist, or the tenant's countersignature over it verifies under a key the runner pins.</summary>
    Admitted,

    /// <summary>The runner's policy for the environment lists digests only, and the executor's is not among them.</summary>
    NotListed,

    /// <summary>The runner pins executor-signing keys for the environment, and the control plane advertises no countersignature for the version there.</summary>
    NotCountersigned,

    /// <summary>A countersignature is advertised, and it does not verify under any pinned key over the executor actually loaded: another environment's, another executor's, or a stranger's.</summary>
    CountersignatureInvalid,

    /// <summary>The countersignature could not be fetched, so the environment is suspended until a later check reaches the control plane.</summary>
    CountersignatureUnavailable,
}