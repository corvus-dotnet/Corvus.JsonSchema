// <copyright file="NativeBuildBackgroundService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// The hosted poll/drain loop that runs the control-plane build worker (ADR 0055). On each tick it drains the build queue,
/// driving <see cref="NativeBuildWorker.DriveNextAsync"/> repeatedly until nothing is queued, then waits one poll interval;
/// so a backlog is worked down back-to-back without a poll-interval gap between jobs, and an idle worker polls cheaply.
/// This is the thin host adapter over the host-agnostic <see cref="NativeBuildWorker"/>; the worker holds the state-machine
/// logic and is unit-tested without a host, and this service holds only the loop and the logging. Any number of hosts can
/// run one, because the worker's claim is atomic.
/// </summary>
/// <remarks>
/// A transient store or build error surfaces out of a cycle and is logged, and the loop retries on the next tick rather
/// than dying. A shutdown cancels the loop; a build in flight at shutdown is left <c>Building</c> for a later reclaim
/// (a cancellation is not recorded as a failure).
/// </remarks>
public sealed class NativeBuildBackgroundService : BackgroundService
{
    private readonly NativeBuildWorker worker;
    private readonly NativeBuildWorkerOptions options;
    private readonly ILogger<NativeBuildBackgroundService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeBuildBackgroundService"/> class.
    /// </summary>
    /// <param name="worker">The build worker this service drives.</param>
    /// <param name="options">The worker identity and poll cadence.</param>
    /// <param name="logger">The logger the loop reports each cycle's outcome to.</param>
    public NativeBuildBackgroundService(NativeBuildWorker worker, NativeBuildWorkerOptions options, ILogger<NativeBuildBackgroundService> logger)
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
                    NativeBuildWorkerResult result = await this.worker.DriveNextAsync(this.options.WorkerId, stoppingToken).ConfigureAwait(false);
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
                // A transient store or build error must not kill the loop — log and retry on the next tick.
                this.logger.LogError(ex, "Native build cycle failed; retrying on the next poll.");
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

    private void LogOutcome(in NativeBuildWorkerResult result)
    {
        if (result.WasSuperseded)
        {
            this.logger.LogInformation(
                "Native build job '{JobId}' for '{RuntimeIdentifier}' was superseded by a re-enqueue; its completion was abandoned.",
                result.JobId,
                result.RuntimeIdentifier);
            return;
        }

        switch (result.Outcome)
        {
            case NativeBuildJobStatus.Ready:
                this.logger.LogInformation(
                    "Built the native binary for '{RuntimeIdentifier}' of job '{JobId}'.",
                    result.RuntimeIdentifier,
                    result.JobId);
                break;

            case NativeBuildJobStatus.Failed:
                this.logger.LogWarning(
                    "Native build job '{JobId}' for '{RuntimeIdentifier}' failed: {FailureReason}",
                    result.JobId,
                    result.RuntimeIdentifier,
                    result.FailureReason);
                break;

            default:
                // A claimed cycle always completes Ready or Failed (or is superseded, handled above); this is defensive.
                break;
        }
    }
}

/// <summary>
/// The configuration for the <see cref="NativeBuildBackgroundService"/>: the worker's identity, stamped on the jobs it
/// claims for audit, and how often it polls when the queue is idle.
/// </summary>
public sealed class NativeBuildWorkerOptions
{
    /// <summary>Gets the identity of this worker, stamped as a claimed job's <c>claimedBy</c>. Distinct per host so an
    /// audit trail shows which host built a version.</summary>
    public required string WorkerId { get; init; }

    /// <summary>Gets the interval the loop waits when the queue is empty before polling again. The default is five seconds;
    /// a build takes minutes, so an idle poll need not be frequent.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);
}