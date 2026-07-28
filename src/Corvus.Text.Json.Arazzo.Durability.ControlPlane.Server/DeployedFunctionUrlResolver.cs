// <copyright file="DeployedFunctionUrlResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Publishing;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Resolves the invoke URL of the serverless function a run advances through, from the <see cref="WorkflowDeployment"/>
/// store (ADR 0059). This is the production resolver <see cref="ServerlessRunExecutionBackend"/> consumes: it maps a run
/// to its deploy target and reads the Deployed deployment's recorded function URL, which the runner's deploy worker (the
/// deploy-on-build path) stamped when it deployed the version's signed native binary to the function platform.
/// </summary>
/// <remarks>
/// A run's <see cref="WorkflowRun.WorkflowId"/> is the versioned id <c>{baseWorkflowId}-v{versionNumber}</c> — the
/// catalogued convention the in-process resolver also parses — and its <see cref="WorkflowRun.Environment"/> pins one
/// runtime target (ADR 0058), the same rid the dispatch gate deployed and gated on. The resolver reads that rid from the
/// environment and looks the deployment up by the <em>full</em> target tuple <c>(base, version, environment, rid)</c>, a
/// unique key, so it resolves the one function the environment currently targets and never a stale deployment for a rid the
/// environment no longer targets (there is no "pick the first of a list" — the full-tuple lookup returns at most one, so no
/// load-spreading choice arises: a single serverless function URL per target already auto-scales behind the platform). When
/// no such deployment exists (the dispatch gate should have held the run until it did, so this is a deploy removed after
/// dispatch or a race), the resolver throws, which leaves the run claimable for the dispatcher's retry — the same recovery a
/// failed invocation gets. The store and environment instances must be the ones the deploy worker completes into and the
/// catalog dispatch gate reads, so the runner host shares them across all three.
/// </remarks>
public static class DeployedFunctionUrlResolver
{
    /// <summary>Creates the asynchronous resolver <see cref="ServerlessRunExecutionBackend"/> consumes, closing over the
    /// deployment store the run's Deployed function URL is read from and the environment registry that pins its runtime
    /// target.</summary>
    /// <param name="deployments">The deployment store; must be the instance the runner's deploy worker completes into.</param>
    /// <param name="environments">The environment registry the run's current runtime target is read from (ADR 0058), so the
    /// lookup selects the environment's current rid and not a stale one.</param>
    /// <returns>The resolver delegate: a run to its deployed function's invoke URL.</returns>
    public static Func<WorkflowRun, CancellationToken, ValueTask<Uri>> ForStore(IWorkflowDeploymentStore deployments, IEnvironmentStore environments)
    {
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(environments);
        return (run, cancellationToken) => ResolveAsync(deployments, environments, run, cancellationToken);
    }

    private static async ValueTask<Uri> ResolveAsync(IWorkflowDeploymentStore deployments, IEnvironmentStore environments, WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        // Map the run to its deploy target. The workflow id is the versioned id the catalog assigns and the in-process
        // resolver parses; a non-versioned id cannot name a deployment (a serverless run always carries one).
        if (!TryParseVersionedId(run.WorkflowId, out string baseWorkflowId, out int versionNumber))
        {
            ServerThrowHelper.ThrowRunWorkflowIdNotVersioned(run.Id.Value, run.WorkflowId);
        }

        // A serverless run is always environment-pinned (a run's environment governs its isolation and its deployed
        // function, ADR 0058); without one there is no target to resolve.
        string environment = run.Environment ?? string.Empty;
        if (environment.Length == 0)
        {
            ServerThrowHelper.ThrowRunNotDeployed(run.Id.Value, run.WorkflowId, "(none)");
        }

        // The environment pins the run's current runtime target (the same rid the dispatch gate deployed and gated on).
        // Read it with full reach: the runner is infrastructure resolving its own routing, not a user request (AccessContext.System).
        using var environmentDoc = await environments.GetAsync(environment, AccessContext.System, cancellationToken).ConfigureAwait(false);
        if (environmentDoc is null)
        {
            ServerThrowHelper.ThrowRunNotDeployed(run.Id.Value, run.WorkflowId, environment);
        }

        // Look the deployment up by the FULL target tuple (base, version, environment, rid) — a unique key derived from the
        // same tuple the deployment's id is — so the result is at most one: the function the environment currently targets,
        // never a stale deployment for a superseded rid. No list-order choice, so no scheduling strategy is required.
        string runtimeIdentifier = environmentDoc.RootElement.RuntimeIdentifierValue;
        var query = new WorkflowDeploymentQuery(WorkflowDeploymentStatus.Deployed, baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        using PooledDocumentList<WorkflowDeployment> deployed = await deployments.ListAsync(query, cancellationToken).ConfigureAwait(false);
        string? functionUrl = deployed.Count > 0 ? deployed[0].FunctionUrlOrNull : null;
        if (string.IsNullOrEmpty(functionUrl))
        {
            ServerThrowHelper.ThrowRunNotDeployed(run.Id.Value, run.WorkflowId, environment);
        }

        return new Uri(functionUrl);
    }

    // Splits the versioned workflow id {baseWorkflowId}-v{versionNumber} into its parts, matching the in-process resolver's
    // convention. Returns false (rather than throwing) so the caller throws through ServerThrowHelper.
    private static bool TryParseVersionedId(string workflowId, out string baseWorkflowId, out int versionNumber)
    {
        int suffix = workflowId.LastIndexOf("-v", StringComparison.Ordinal);
        if (suffix > 0 && int.TryParse(workflowId.AsSpan(suffix + 2), out versionNumber))
        {
            baseWorkflowId = workflowId[..suffix];
            return true;
        }

        baseWorkflowId = string.Empty;
        versionNumber = 0;
        return false;
    }
}