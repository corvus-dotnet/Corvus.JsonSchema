// <copyright file="WorkflowDispatchService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Runner.Demo;

/// <summary>
/// The runner's dispatch + resume loop (design §7). It polls the store-as-queue for the versions it hosts:
/// <see cref="WorkflowDispatcher"/> claims <c>Pending</c> runs and lease-expired <c>Running</c> orphans (a
/// crashed runner's in-flight work), while <see cref="WorkflowWorker"/> resumes suspended runs whose durable
/// timer is now due. Both take a per-run lease (CAS) so exactly one runner advances a run.
/// </summary>
/// <remarks>
/// Execution itself is the explicitly-paused phase: loading the version's compiled <c>executor.dll</c> into a
/// collectible ALC and re-entering it (the <see cref="HostedWorkflowResumer"/> path) is not wired yet. Until it
/// is, <see cref="ResumeAsync"/> is a stub that drives each claimed run to completion — so the dispatch, lease,
/// orphan-reclaim and resume plumbing is exercised end-to-end against the shared store. Note this means the
/// seeded orphaned <c>Running</c> run is reclaimed and completed on start-up (orphan reclaim in action).
/// </remarks>
public sealed class WorkflowDispatchService(
    IWorkflowStateStore store,
    SecuredWorkflowCatalog catalog,
    RunnerOptions options,
    ILogger<WorkflowDispatchService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dispatcher = new WorkflowDispatcher(store, options.RunnerId);
        var worker = new WorkflowWorker(store, options.RunnerId);

        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                string[] hostedIds = await this.HostedWorkflowIdsAsync(stoppingToken).ConfigureAwait(false);
                if (hostedIds.Length > 0)
                {
                    int dispatched = await dispatcher.DispatchClaimableAsync(hostedIds, ResumeAsync, stoppingToken).ConfigureAwait(false);
                    int resumed = await worker.ResumeDueTimersAsync(ResumeAsync, stoppingToken).ConfigureAwait(false);
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
                // A transient store error must not kill the loop — log and retry on the next tick.
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

    // The stub resumer (see remarks): live execution re-enters the compiled executor; until then, complete the run.
    private static async ValueTask<WorkflowRunResultKind> ResumeAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        await run.CompleteAsync(default, cancellationToken).ConfigureAwait(false);
        return WorkflowRunResultKind.Completed;
    }

    private async Task<string[]> HostedWorkflowIdsAsync(CancellationToken cancellationToken)
    {
        // The versions this runner hosts = every catalogued version (versioned id, e.g. "onboard-customer-v1").
        // Refreshed each cycle so a newly-published version is picked up without a restart.
        CatalogPage page = await catalog.SearchAsync(new CatalogQuery(Limit: 1000), AccessContext.System, cancellationToken).ConfigureAwait(false);
        return [.. page.Versions.Select(static v => (string)v.WorkflowId)];
    }
}
