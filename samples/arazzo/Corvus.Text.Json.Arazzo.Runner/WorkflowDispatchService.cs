// <copyright file="WorkflowDispatchService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Runner.Client;

namespace Corvus.Text.Json.Arazzo.Runner;

/// <summary>
/// The runner's dispatch + resume loop (design §7), driven entirely through the runner API (ADR 0065). It polls for the
/// versions it hosts: <see cref="RunnerApiDispatcher"/> claims <c>Pending</c> runs and lease-expired <c>Running</c>
/// orphans (a crashed runner's in-flight work), while <see cref="RunnerApiWorker"/> resumes suspended runs whose durable
/// timer is now due. Both take a per-run lease so exactly one runner advances a run.
/// </summary>
/// <remarks>
/// <para>
/// The runner holds no store credential, and this loop names no environment. It presents its machine principal, and the
/// control plane intersects every candidate set with the environments an administrator bound that principal to. What
/// used to be a runner-side environment filter and a runner-side authorization gate are the server's to apply, which is
/// what turns them from cooperation into enforcement: a production run cannot land on a staging runner that asks for
/// one, and a revoked runner is not told to stand down, it is simply offered nothing.
/// </para>
/// <para>
/// Both dispatch and timer-resume drive each claimed run through the injected <see cref="WorkflowResumer"/> — the real
/// <see cref="HostedWorkflowResumer"/> that loads the version's compiled <c>executor.dll</c> into a collectible ALC (on
/// first use, cached thereafter) and re-enters it against the runner's transports, the same live-execution path the
/// control-plane host runs in-process. The executor itself is pulled through the API too, so the runner reaches the
/// catalog with no more credential than it reaches the queue with. So the runner genuinely executes catalogued runs: the
/// seeded orphaned <c>Running</c> run is reclaimed and re-executed on start-up (orphan reclaim in action).
/// </para>
/// </remarks>
public sealed class WorkflowDispatchService(
    ArazzoRunnerClient client,
    WorkflowResumer resumer,
    RunnerHostedVersions hostedVersions,
    ILogger<WorkflowDispatchService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dispatcher = new RunnerApiDispatcher(client);
        var worker = new RunnerApiWorker(client);

        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // The runner's single answer to what it has baked, shared with the message listeners so a delivery and
                // a dispatch never disagree about it.
                IReadOnlyList<string> hostedIds = await hostedVersions.GetAsync(stoppingToken).ConfigureAwait(false);
                if (hostedIds.Count > 0)
                {
                    int dispatched = await dispatcher.DispatchClaimableAsync(hostedIds, resumer, stoppingToken).ConfigureAwait(false);

                    // Timer-resume is scoped by the same hosted set as dispatch, so a due run of a version this runner
                    // has not baked is left for one that has, rather than claimed and faulted for want of an executor.
                    int resumed = await worker.ResumeDueTimersAsync(hostedIds, resumer, stoppingToken).ConfigureAwait(false);
                    if (dispatched + resumed > 0)
                    {
                        logger.LogInformation("Dispatched {Dispatched} new/orphaned run(s); resumed {Resumed} due run(s).", dispatched, resumed);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A transient API error must not kill the loop — log and retry on the next tick.
                logger.LogError(ex, "Dispatch cycle failed; retrying on the next poll.");
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
}