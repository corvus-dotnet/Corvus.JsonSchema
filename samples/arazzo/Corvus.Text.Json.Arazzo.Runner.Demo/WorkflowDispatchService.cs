// <copyright file="WorkflowDispatchService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

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
    IEnvironmentRunnerAuthorizationStore runnerAuthorizations,
    SecuredWorkflowCatalog catalog,
    RunnerOptions options,
    ILogger<WorkflowDispatchService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    // The last-observed authorization state, so IsAuthorizedAsync logs only on a transition (not every poll cycle).
    private bool? lastAuthorized;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // §5.5 dispatch: the runner claims new + orphaned runs only while an administrator of its environment has authorized
        // it (the gate; revoke takes effect within a poll cycle, in-flight runs drain), AND only runs pinned to the single
        // environment it serves (runnerEnvironment) — a production run never lands on a staging runner.
        var dispatcher = new WorkflowDispatcher(store, options.RunnerId, dispatchGate: this.IsAuthorizedAsync, runnerEnvironment: options.Environment);
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

    // The §5.5 dispatch gate: this runner may take new/orphaned work only while an administrator of its environment has
    // Authorized it (Pending on first registration, revocable). Read its own authorization each cycle; log only on a state
    // change so a paused runner does not spam the log. A revoked/pending runner stays registered + heartbeating but idle.
    private async ValueTask<bool> IsAuthorizedAsync(CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? authorization =
            await runnerAuthorizations.GetAsync(options.Environment, options.RunnerId, cancellationToken).ConfigureAwait(false);
        bool authorized = authorization is { } doc && doc.RootElement.IsAuthorized;

        if (this.lastAuthorized != authorized)
        {
            this.lastAuthorized = authorized;
            if (authorized)
            {
                logger.LogInformation("Runner {RunnerId} is authorized to serve environment '{Environment}'; dispatch is active.", options.RunnerId, options.Environment);
            }
            else
            {
                logger.LogWarning("Runner {RunnerId} is not authorized to serve environment '{Environment}' (pending or revoked); dispatch is paused until an administrator authorizes it.", options.RunnerId, options.Environment);
            }
        }

        return authorized;
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
