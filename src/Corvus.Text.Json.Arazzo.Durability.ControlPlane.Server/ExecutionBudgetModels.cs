// <copyright file="ExecutionBudgetModels.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>Projects an <see cref="ExecutionBudget"/> onto the API's resolved-budget model (ADR 0068).</summary>
internal static class ExecutionBudgetModels
{
    /// <summary>Builds the API model of a budget with every limit decided: a ceiling, an effective budget, or a run's.</summary>
    /// <param name="budget">The budget.</param>
    /// <returns>The model source.</returns>
    /// <remarks>
    /// The API states durations in whole seconds, as an environment's override is authored. Every budget is resolved
    /// from whole-second limits (<see cref="ExecutionBudget.CeilingFrom"/>, <see cref="ExecutionBudgetOverride"/>), so
    /// the conversion loses nothing.
    /// </remarks>
    public static Models.ResolvedExecutionBudget.Source Resolved(ExecutionBudget budget)
        => Models.ResolvedExecutionBudget.Build(
            maxResponseBytes: budget.MaxResponseBytes,
            maxSteps: budget.MaxSteps,
            maxSubWorkflowDepth: budget.MaxSubWorkflowDepth,
            retryAfterCeilingSeconds: (long)budget.RetryAfterCeiling.TotalSeconds,
            stepTimeoutSeconds: (long)budget.StepTimeout.TotalSeconds,
            wallClockSeconds: (long)budget.WallClock.TotalSeconds);
}