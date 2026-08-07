// <copyright file="StoreControlPlaneCapacityGuard.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Capacity;

/// <summary>
/// Measures capacity against the store, which is the only place a standing magnitude can be measured (ADR 0065
/// decision 3).
/// </summary>
/// <remarks>
/// <para>
/// Every count is bounded at the limit, so a tenant far above its cap costs the same to refuse as one just over it and
/// the check never becomes a scan of the population it protects. That is what makes it affordable on a start path.
/// </para>
/// <para>
/// Reading the store on each check rather than caching is deliberate. A cached magnitude is wrong in the direction that
/// matters: it admits work a tenant no longer has room for, and it does so for as long as the cache window lasts. A
/// bounded count is cheap enough not to need the risk.
/// </para>
/// </remarks>
public sealed class StoreControlPlaneCapacityGuard : IControlPlaneCapacityGuard
{
    // Everything that is not finished. WorkflowQuery carries one status rather than a set, so concurrency costs one
    // bounded count per status; each stops at the limit, and the loop stops as soon as the total reaches it.
    private static readonly WorkflowRunStatus[] InFlight =
    [
        WorkflowRunStatus.Pending,
        WorkflowRunStatus.Running,
        WorkflowRunStatus.Suspended,
    ];

    private readonly ISecuredWorkflowManagement runs;
    private readonly IEnvironmentRunnerAuthorizationStore runnerAuthorizations;
    private readonly ControlPlaneCapacityOptions options;

    /// <summary>Initializes a new instance of the <see cref="StoreControlPlaneCapacityGuard"/> class.</summary>
    /// <param name="runs">The run management surface, whose counts are already reach-scoped.</param>
    /// <param name="runnerAuthorizations">The runner-authorization records, for the per-environment runner cap.</param>
    /// <param name="options">The deployment's limits; defaults are used when omitted.</param>
    public StoreControlPlaneCapacityGuard(
        ISecuredWorkflowManagement runs,
        IEnvironmentRunnerAuthorizationStore runnerAuthorizations,
        ControlPlaneCapacityOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(runnerAuthorizations);

        this.runs = runs;
        this.runnerAuthorizations = runnerAuthorizations;
        this.options = options ?? new ControlPlaneCapacityOptions();
    }

    /// <inheritdoc/>
    public async ValueTask<ControlPlaneCapacityRejection?> TryAdmitAsync(ControlPlaneCapacityKind kind, string counter, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        int limit = this.options.For(kind);
        if (limit <= 0)
        {
            return null;
        }

        int observed = kind switch
        {
            ControlPlaneCapacityKind.ConcurrentRuns => await this.CountInFlightAsync(limit, context, cancellationToken).ConfigureAwait(false),
            ControlPlaneCapacityKind.StoredRuns => await this.CountAsync(null, limit, context, cancellationToken).ConfigureAwait(false),

            ControlPlaneCapacityKind.RegisteredRunners => await this.CountRunnersAsync(counter, limit, cancellationToken).ConfigureAwait(false),
            _ => 0,
        };

        // The check admits one more, so the limit is reached when the count is already at it.
        return observed >= limit
            ? new ControlPlaneCapacityRejection(ControlPlaneCapacityNames.Of(kind), counter, limit, observed)
            : null;
    }

    private async ValueTask<int> CountAsync(WorkflowRunStatus? status, int limit, AccessContext context, CancellationToken cancellationToken)
    {
        (int count, _) = await this.runs.CountAsync(new WorkflowQuery(Status: status), context, limit, cancellationToken).ConfigureAwait(false);
        return count;
    }

    private async ValueTask<int> CountInFlightAsync(int limit, AccessContext context, CancellationToken cancellationToken)
    {
        int total = 0;
        foreach (WorkflowRunStatus status in InFlight)
        {
            // Each count is capped by what is still unaccounted for, and the walk stops the moment the limit is
            // reached: a tenant well over its concurrency limit is refused after the first status rather than after
            // counting all three.
            total += await this.CountAsync(status, limit - total, context, cancellationToken).ConfigureAwait(false);
            if (total >= limit)
            {
                return total;
            }
        }

        return total;
    }

    private async ValueTask<int> CountRunnersAsync(string environment, int limit, CancellationToken cancellationToken)
    {
        // Every authorization record for the environment, whatever its status: a Quarantined or Revoked runner still
        // holds its registration, and a cap that counted only the dispatchable ones would let a tenant accumulate
        // unbounded rows by registering runners it never gets authorized.
        var query = new RunnerAuthorizationQuery(Environment: environment);
        (int count, _) = await this.runnerAuthorizations.CountAsync(query, limit, cancellationToken).ConfigureAwait(false);
        return count;
    }
}