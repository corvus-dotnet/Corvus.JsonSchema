// <copyright file="ExecutionBudgetResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Resolves the execution budget a run of a workflow started in an environment is frozen with (ADR 0068). A host
/// passes its management seam's <see cref="ISecuredWorkflowManagement.ResolveExecutionBudgetAsync"/>, which is
/// the one resolution.
/// </summary>
/// <param name="workflowId">The workflow the run executes.</param>
/// <param name="environment">The environment the run is pinned to.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <returns>The budget, or <see langword="null"/> where the resolver exempts the run.</returns>
public delegate ValueTask<ExecutionBudget?> ExecutionBudgetResolver(string workflowId, string environment, CancellationToken cancellationToken);