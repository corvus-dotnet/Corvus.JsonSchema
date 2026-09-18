// <copyright file="WorkflowExecutorUnresolvableException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Thrown when a resolver refuses a run because of what the run or its version is, and not because of something that
/// may pass: the run's workflow id names no version, the version is not in the catalog, its stored content hash
/// diverges from its documents, it has no executor or no manifest, or a baked host was handed a run for a version it
/// does not serve.
/// </summary>
/// <remarks>
/// A refusal is the same answer on every attempt, so the execution seam ends the run on it
/// (<see cref="WorkflowExecutorFault.Unresolvable"/>). A failure to reach the artifact source is not a refusal and is
/// never reported as one. It propagates, and the run is retried from its last durable checkpoint.
/// </remarks>
public sealed class WorkflowExecutorUnresolvableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowExecutorUnresolvableException"/> class.</summary>
    /// <param name="message">A description of why the executor cannot be resolved.</param>
    public WorkflowExecutorUnresolvableException(string message)
        : base(message)
    {
    }
}