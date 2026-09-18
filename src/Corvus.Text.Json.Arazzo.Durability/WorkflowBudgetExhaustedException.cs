// <copyright file="WorkflowBudgetExhaustedException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The control-flow signal a durable run throws to unwind an advance that ran out of its execution budget
/// (ADR 0068): no fuel or no time for the attempt a step was about to make, or a sub-workflow nested past the depth
/// cap. The run is already persisted <see cref="WorkflowRunStatus.Faulted"/> with the budget fault before the throw,
/// so the execution seam catches this to stop driving the executor and report the fault. It is the same shape as
/// <see cref="WorkflowPauseException"/>, and like it never escapes the seam.
/// </summary>
internal sealed class WorkflowBudgetExhaustedException(string error) : Exception(error)
{
    /// <summary>Gets the budget fault the run was recorded with, one of <see cref="ExecutionBudgetFault"/>.</summary>
    public string Error { get; } = error;
}