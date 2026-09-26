// <copyright file="RunnerAdmission.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>Whether a runner admits a claim for an environment (ADR 0065 decision 10), and why not.</summary>
public enum RunnerAdmission
{
    /// <summary>The environment is on the allowlist and, for a keyed entry, the advertised seal key is the pinned one.</summary>
    Admitted,

    /// <summary>The environment is not on the runner's allowlist.</summary>
    NotAllowlisted,

    /// <summary>The control plane advertises a seal key for the generation held whose fingerprint is not the pinned one.</summary>
    SealKeyMismatch,

    /// <summary>The generation the runner holds is not one the environment registers as active.</summary>
    GenerationNotActive,

    /// <summary>The advertised seal keys could not be read, so nothing was checked and the environment is not served.</summary>
    SealKeyUnavailable,
}