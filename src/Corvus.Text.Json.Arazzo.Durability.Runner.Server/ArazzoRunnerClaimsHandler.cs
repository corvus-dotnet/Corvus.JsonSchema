// <copyright file="ArazzoRunnerClaimsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Implements the runner API's claim operation: the only way a runner is given work.
/// </summary>
public sealed class ArazzoRunnerClaimsHandler : IApiClaimsHandler
{
    private readonly RunnerRunCoordinator coordinator;
    private readonly RunnerPrincipalAccessor principals;

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerClaimsHandler"/> class.</summary>
    /// <param name="coordinator">The store-facing coordinator.</param>
    /// <param name="principals">Reads the authenticated machine principal from the current request.</param>
    public ArazzoRunnerClaimsHandler(RunnerRunCoordinator coordinator, RunnerPrincipalAccessor principals)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(principals);

        this.coordinator = coordinator;
        this.principals = principals;
    }

    /// <inheritdoc/>
    public async ValueTask<ClaimRunResult> HandleClaimRunAsync(ClaimRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return ClaimRunResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        // The hosted versions are realised as strings here and nowhere earlier: this is the leaf where they become the
        // dispatch index's query, and every backend's query takes strings.
        ClaimRequest.HostedVersionsEntityArray hostedVersions = parameters.Body.HostedVersions;
        var hosted = new List<string>(hostedVersions.GetArrayLength());
        foreach (ClaimRequest.HostedVersionsEntityArray.HostedVersionsEntity version in hostedVersions.EnumerateArray())
        {
            hosted.Add((string)version);
        }

        TimeSpan? requestedLease = parameters.Body.LeaseSeconds.IsNotUndefined()
            ? TimeSpan.FromSeconds((long)parameters.Body.LeaseSeconds)
            : null;

        ClaimedRunRecord? claimed = await this.coordinator.TryClaimAsync(principal, hosted, requestedLease, cancellationToken).ConfigureAwait(false);
        if (claimed is not { } run)
        {
            // Nothing claimable is the common case for an idle runner, and is not an error.
            return ClaimRunResult.NoContent();
        }

        return ClaimRunResult.Ok(
            ClaimedRun.Build(
                environment: run.Environment,
                lease: LeaseGrant.Build(epoch: run.Lease.Epoch, expiresAt: run.Lease.ExpiresAt, token: run.Lease.Token),
                runId: run.RunId.Value,
                workflowId: run.WorkflowId),
            workspace);
    }
}