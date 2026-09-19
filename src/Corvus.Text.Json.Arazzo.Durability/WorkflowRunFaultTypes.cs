// <copyright file="WorkflowRunFaultTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>What one of the platform's fixed fault error types means, and what its reader can do about it.</summary>
/// <param name="Meaning">What happened, in a sentence fragment that reads after the error type.</param>
/// <param name="Remedy">What an operator does next.</param>
public readonly record struct WorkflowRunFaultDescription(string Meaning, string Remedy);

/// <summary>
/// The fixed error types the platform itself records on a faulted run (<see cref="ExecutionBudgetFault"/> and
/// <see cref="WorkflowExecutorFault"/>), described for the people who read a run.
/// </summary>
/// <remarks>
/// A fault's error is an open field: a step that fails with no matching failure action records its own failure there.
/// Only these fixed values have a meaning the platform can state, and everything else is shown as it was recorded. The
/// control plane's REST reference and its console carry the same table.
/// </remarks>
public static class WorkflowRunFaultTypes
{
    /// <summary>Describes a fault error, when it is one of the platform's fixed types.</summary>
    /// <param name="error">The fault's error, as recorded on the run.</param>
    /// <param name="description">The description, when the error is a fixed type.</param>
    /// <returns><see langword="true"/> if <paramref name="error"/> is one of the fixed types.</returns>
    public static bool TryDescribe(string? error, out WorkflowRunFaultDescription description)
    {
        description = error switch
        {
            ExecutionBudgetFault.Fuel => new("the run made as many step attempts as its budget allows (max steps)", "Raise the environment's max steps and resume the run, or re-run it. A run that made as many attempts as the journal holds (500) cannot be resumed."),
            ExecutionBudgetFault.Deadline => new("the run outlived its budget's wall clock", "Raise the environment's wall clock and resume the run, or re-run it. A run older than the deployment's ceiling allows cannot be resumed."),
            ExecutionBudgetFault.Depth => new("the run nested sub-workflows past its budget's depth limit", "Raise the environment's sub-workflow depth and resume the run, or flatten the workflow and re-run it."),
            WorkflowExecutorFault.Unhandled => new("the workflow's executor failed in a way nothing in the workflow handled", "The failure itself is in the executor's trace, not on the run. Fix the cause, then resume the run."),
            WorkflowExecutorFault.Unresolvable => new("the workflow version's executor was refused: it is missing, not runnable, or failed verification", "Republish or repair the version in the catalog, then resume the run."),
            WorkflowExecutorFault.TransportUnbound => new("a source the workflow calls has no usable binding in the run's environment", "Add the source's credential binding for the environment, then resume the run."),
            _ => default,
        };

        return description.Meaning is not null;
    }
}