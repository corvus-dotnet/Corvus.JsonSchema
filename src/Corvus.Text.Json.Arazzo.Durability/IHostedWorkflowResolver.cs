// <copyright file="IHostedWorkflowResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Resolves a run to the <see cref="IHostedWorkflow"/> that executes it, independent of the execution backend. The
/// in-process backend resolves the version's IL executor through the <see cref="Execution.WorkflowExecutorLoader"/>
/// (<see cref="LoaderHostedWorkflowResolver"/>); an AOT serverless backend resolves a baked, compile-time executor.
/// Either way <see cref="HostedWorkflowExecution.RunAsync"/> runs what is resolved (ADR 0028), so the resolution
/// choice is the only thing an isolation model changes about execution.
/// </summary>
public interface IHostedWorkflowResolver
{
    /// <summary>Resolves the run's workflow to the <see cref="IHostedWorkflow"/> that runs it.</summary>
    /// <param name="run">The run to resolve.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resolved workflow.</returns>
    ValueTask<IHostedWorkflow> ResolveAsync(WorkflowRun run, CancellationToken cancellationToken);

    /// <summary>
    /// Pre-warms the resolver for a version so a later <see cref="ResolveAsync"/> of it avoids the cold cost. A
    /// no-op is valid for a resolver with nothing to warm (a baked executor is already in hand).
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the version is warm.</returns>
    ValueTask PrepareAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken);
}