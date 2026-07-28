// <copyright file="WorkflowDeployBackgroundService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// The hosted poll/drain loop that runs the runner-side deploy worker (ADR 0059). On each tick it drains the deployment
/// queue, driving <see cref="WorkflowDeployWorker.DriveNextAsync"/> repeatedly until nothing is queued, then waits one poll
/// interval; so a backlog is worked down back-to-back without a poll-interval gap between deployments, and an idle worker
/// polls cheaply. This is the thin host adapter over the host-agnostic <see cref="WorkflowDeployWorker"/>; the worker holds
/// the state-machine logic and is unit-tested without a host, and this service holds only the loop and the logging. Any
/// number of hosts can run one, because the worker's claim is atomic.
/// </summary>
/// <remarks>
/// A transient store or deploy error surfaces out of a cycle and is logged, and the loop retries on the next tick rather
/// than dying. A shutdown cancels the loop; a deploy in flight at shutdown is left <c>Deploying</c> for a later reclaim
/// (a cancellation is not recorded as a failure).
/// </remarks>
public sealed class WorkflowDeployBackgroundService : BackgroundService
{
    private readonly WorkflowDeployWorker worker;
    private readonly WorkflowDeployWorkerOptions options;
    private readonly ILogger<WorkflowDeployBackgroundService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowDeployBackgroundService"/> class.
    /// </summary>
    /// <param name="worker">The deploy worker this service drives.</param>
    /// <param name="options">The worker identity and poll cadence.</param>
    /// <param name="logger">The logger the loop reports each cycle's outcome to.</param>
    public WorkflowDeployBackgroundService(WorkflowDeployWorker worker, WorkflowDeployWorkerOptions options, ILogger<WorkflowDeployBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        this.worker = worker;
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(this.options.PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Drain: keep claiming while the queue has work, so a backlog is built down back-to-back. Each iteration
                // re-checks the stopping token, so a shutdown breaks out promptly even mid-drain.
                while (!stoppingToken.IsCancellationRequested)
                {
                    WorkflowDeployWorkerResult result = await this.worker.DriveNextAsync(this.options.WorkerId, this.options.LeaseTtl, this.options.LeaseRenewalInterval, stoppingToken).ConfigureAwait(false);
                    if (!result.Claimed)
                    {
                        break;
                    }

                    this.LogOutcome(result);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A transient store or deploy error must not kill the loop — log and retry on the next tick.
                this.logger.LogError(ex, "Deploy cycle failed; retrying on the next poll.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void LogOutcome(in WorkflowDeployWorkerResult result)
    {
        if (result.WasSuperseded)
        {
            this.logger.LogInformation(
                "Deployment '{DeploymentId}' for '{RuntimeIdentifier}' was superseded by a re-enqueue; its completion was abandoned.",
                result.DeploymentId,
                result.RuntimeIdentifier);
            return;
        }

        switch (result.Outcome)
        {
            case WorkflowDeploymentStatus.Deployed:
                this.logger.LogInformation(
                    "Deployed the serverless function for '{RuntimeIdentifier}' of deployment '{DeploymentId}' at {FunctionUrl}.",
                    result.RuntimeIdentifier,
                    result.DeploymentId,
                    result.FunctionUrl);
                break;

            case WorkflowDeploymentStatus.Failed:
                this.logger.LogWarning(
                    "Deployment '{DeploymentId}' for '{RuntimeIdentifier}' failed: {FailureReason}",
                    result.DeploymentId,
                    result.RuntimeIdentifier,
                    result.FailureReason);
                break;

            default:
                // A claimed cycle always completes Deployed or Failed (or is superseded, handled above); this is defensive.
                break;
        }
    }
}

/// <summary>
/// The configuration for the <see cref="WorkflowDeployBackgroundService"/>: the worker's identity, stamped on the
/// deployments it claims for audit, and how often it polls when the queue is idle.
/// </summary>
public sealed class WorkflowDeployWorkerOptions
{
    /// <summary>Gets the identity of this worker, stamped as a claimed deployment's <c>claimedBy</c>. Distinct per host so
    /// an audit trail shows which host deployed a version.</summary>
    public required string WorkerId { get; init; }

    /// <summary>Gets the interval the loop waits when the queue is empty before polling again. The default is five seconds;
    /// a deploy takes seconds to minutes, so an idle poll need not be frequent.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets how long the advisory lease stamped on a claimed deployment is held before another worker may reclaim
    /// it as an orphan (ADR 0056). The worker renews it on a heartbeat while it deploys, so this need only exceed the
    /// renewal interval with margin; the default is five minutes, comfortably above a normal deploy time so a brief renewal
    /// hiccup does not trigger a spurious reclaim.</summary>
    public TimeSpan LeaseTtl { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets how often the worker renews its lease while a deploy runs (ADR 0056). It must be comfortably below
    /// <see cref="LeaseTtl"/> so several renewals fall within one lease; the default is thirty seconds, so a five-minute
    /// lease tolerates several missed renewals before another worker reclaims a genuinely dead one.</summary>
    public TimeSpan LeaseRenewalInterval { get; init; } = TimeSpan.FromSeconds(30);
}