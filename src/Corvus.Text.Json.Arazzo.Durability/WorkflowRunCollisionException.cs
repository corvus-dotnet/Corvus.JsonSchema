// <copyright file="WorkflowRunCollisionException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Thrown when an idempotent or named start finds its derived run id occupied by a run that is <em>not</em> the
/// same logical start (a different workflow or environment). Reporting the occupant as the caller's run would be
/// exactly the pre-created-id substitution the keyed derivation exists to prevent (ADR 0065 §9), so the start is
/// refused instead; the message deliberately says nothing about the occupant.
/// </summary>
public sealed class WorkflowRunCollisionException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowRunCollisionException"/> class.</summary>
    /// <param name="message">The refusal message.</param>
    public WorkflowRunCollisionException(string message)
        : base(message)
    {
    }
}