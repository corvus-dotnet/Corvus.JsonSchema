// <copyright file="RunIsolationModel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The isolation a run executes under: a comparable strength axis where <see cref="Isolated"/> is stronger than
/// <see cref="InProcess"/>. It is distinct from the execution backend (the mechanism). An out-of-process serverless
/// function, container, or micro-guest all provide <see cref="Isolated"/>, differing in weight, not in the
/// guarantee. See <see cref="IRunExecutionBackend"/> and ADR 0028.
/// </summary>
public enum RunIsolationModel
{
    /// <summary>
    /// The run executes in the runner's own process, its version's assembly loaded into a collectible load context
    /// (ADR 0024). Efficient, and enough where the runner trusts the workflow code it hosts.
    /// </summary>
    InProcess,

    /// <summary>
    /// The run executes behind a per-run boundary (an out-of-process serverless function, container, or
    /// micro-guest), so it cannot affect the runner or its neighbours.
    /// </summary>
    Isolated,
}