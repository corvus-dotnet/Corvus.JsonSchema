// <copyright file="ArazzoRunnerLeasesHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Implements the runner API's lease operations: extending a held lease, and handing a run back.
/// </summary>
public sealed class ArazzoRunnerLeasesHandler : IApiLeasesHandler
{
    private readonly RunnerRunCoordinator coordinator;
    private readonly RunnerPrincipalAccessor principals;

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerLeasesHandler"/> class.</summary>
    /// <param name="coordinator">The store-facing coordinator.</param>
    /// <param name="principals">Reads the authenticated machine principal from the current request.</param>
    public ArazzoRunnerLeasesHandler(RunnerRunCoordinator coordinator, RunnerPrincipalAccessor principals)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(principals);

        this.coordinator = coordinator;
        this.principals = principals;
    }

    /// <inheritdoc/>
    public async ValueTask<RenewLeaseResult> HandleRenewLeaseAsync(RenewLeaseParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return RenewLeaseResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        TimeSpan? requested = parameters.Body.IsNotUndefined() && parameters.Body.LeaseSeconds.IsNotUndefined()
            ? TimeSpan.FromSeconds((long)parameters.Body.LeaseSeconds)
            : null;

        RunnerLeaseGrant? renewed = await this.coordinator.TryRenewAsync(
            principal,
            new WorkflowRunId((string)parameters.RunId),
            (string)parameters.XArazzoLease,
            requested,
            cancellationToken).ConfigureAwait(false);

        // A renewal that would silently mint a fresh lease is the failure this refusal exists for: the run may already
        // have been claimed by another runner.
        return renewed is { } grant
            ? RenewLeaseResult.Ok(LeaseGrant.Build(epoch: grant.Epoch, expiresAt: grant.ExpiresAt, token: grant.Token), workspace)
            : RenewLeaseResult.Conflict(RunnerProblems.LeaseLost(), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ReleaseLeaseResult> HandleReleaseLeaseAsync(ReleaseLeaseParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return ReleaseLeaseResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        await this.coordinator.ReleaseAsync(
            principal,
            new WorkflowRunId((string)parameters.RunId),
            (string)parameters.XArazzoLease,
            cancellationToken).ConfigureAwait(false);

        // The same answer whether the lease was current, had lapsed, or was never this principal's: the postcondition
        // the operation promises holds in all three, and distinguishing them would report on a lease the caller does
        // not hold.
        return ReleaseLeaseResult.NoContent();
    }
}