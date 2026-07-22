// <copyright file="BakedHostedWorkflowResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The baked <see cref="IHostedWorkflowResolver"/>: a serverless AOT host is published per (environment, version)
/// with exactly one workflow executor compiled in, so it resolves every run to that single, already-in-hand
/// <see cref="IHostedWorkflow"/> with no loader, no dynamic IL, and nothing to warm. This is the AOT counterpart
/// to <see cref="LoaderHostedWorkflowResolver"/>, which loads a version's IL executor on demand (ADR 0028).
/// </summary>
public sealed class BakedHostedWorkflowResolver : IHostedWorkflowResolver
{
    private readonly IHostedWorkflow workflow;

    /// <summary>Initializes a new instance of the <see cref="BakedHostedWorkflowResolver"/> class.</summary>
    /// <param name="workflow">The single baked executor this host serves, activated at build time.</param>
    public BakedHostedWorkflowResolver(IHostedWorkflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        this.workflow = workflow;
    }

    /// <inheritdoc/>
    public ValueTask<IHostedWorkflow> ResolveAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        // A baked host serves exactly one version, so a run for any other version reached it by a routing fault.
        // Returning the baked executor anyway would silently run the wrong workflow, so fail fast — matching the
        // loader resolver, which only ever returns the workflow the run's id resolves to.
        if (!string.Equals(run.WorkflowId, this.workflow.Descriptor.WorkflowId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"This host is baked for workflow '{this.workflow.Descriptor.WorkflowId}', but the run targets '{run.WorkflowId}' (a routing fault).");
        }

        return new ValueTask<IHostedWorkflow>(this.workflow);
    }

    /// <inheritdoc/>
    public ValueTask PrepareAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}