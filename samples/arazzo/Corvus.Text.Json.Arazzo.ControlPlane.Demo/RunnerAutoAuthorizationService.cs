// <copyright file="RunnerAutoAuthorizationService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo;

/// <summary>
/// A DEMO convenience that stands in for the administrator of the <c>development</c> and <c>system</c> environments. A
/// runner may not self-assert into an environment (design §5.5): it registers a <c>Pending</c> authorization and an
/// administrator of that environment must authorize it before it becomes dispatchable. The open demo has no interactive
/// administrator, so a registered out-of-process runner would sit dispatch-paused forever and never claim catalogued
/// runs. This service — running as the environments' creator <c>demo</c> (the identity that, per §7.7, holds their
/// administration) — authorizes each such runner: the application runner for <c>development</c>, and the control-plane
/// system runner for <c>system</c> (design §16.5.1), so both genuinely claim and execute their catalogued runs.
/// </summary>
/// <remarks>
/// It preserves the §5.5 semantic exactly: an administrator (never the runner itself) grants the authorization, and
/// the runner still registers <c>Pending</c> and cannot self-assert. The only demo-specific shortcut is that the
/// grant is automatic; production has a human administrator authorize runners deliberately through the UI/API.
/// </remarks>
internal sealed class RunnerAutoAuthorizationService(
    IEnvironmentRunnerAuthorizationStore authorizations,
    ILogger<RunnerAutoAuthorizationService> logger) : BackgroundService
{
    // The environments this demo stands in as administrator for: development (the application runner) and system (the
    // control-plane system runner, design §16.5.1). Production authorizes runners deliberately through the UI/API.
    private static readonly string[] Environments = ["development", "system"];
    private const string AdministratorActor = "demo";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                foreach (string environmentName in Environments)
                {
                    // The environment's inbox of Pending runner authorizations — the runners that registered but no
                    // administrator has yet cleared. Authorize each one (idempotent: an already-Authorized runner is not
                    // returned by the Pending query, and DecideAsync is guarded by the record's etag).
                    var query = new RunnerAuthorizationQuery(RunnerAuthorizationStatus.Pending, Environment: environmentName);
                    using PooledDocumentList<EnvironmentRunnerAuthorization> pending =
                        await authorizations.ListAsync(query, stoppingToken).ConfigureAwait(false);

                    foreach (EnvironmentRunnerAuthorization authorization in pending)
                    {
                        if (!authorization.IsPending)
                        {
                            continue;
                        }

                        string runnerId = authorization.RunnerIdValue;
                        var decision = new RunnerAuthorizationDecision(
                            RunnerAuthorizationStatus.Authorized,
                            "Demo auto-authorization: the demo host stands in for the environment administrator.");
                        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? decided = await authorizations.DecideAsync(
                            environmentName, runnerId, decision, authorization.EtagValue, AdministratorActor, stoppingToken).ConfigureAwait(false);
                        if (decided is not null)
                        {
                            logger.LogInformation(
                                "Demo auto-authorized runner {RunnerId} to serve environment '{Environment}' (standing in for its administrator '{Administrator}'; production authorizes deliberately via the UI/API).",
                                runnerId, environmentName, AdministratorActor);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Demo runner auto-authorization cycle failed; retrying on the next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
