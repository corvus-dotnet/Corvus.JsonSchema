// <copyright file="RunnerPreAuthorizationService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo;

/// <summary>
/// A DEMO convenience that stands in for the administrators of the <c>development</c>, <c>system</c>, and
/// <c>isolated</c> environments, pre-authorizing the runners this composition is going to start.
/// </summary>
/// <remarks>
/// <para>
/// Since ADR 0065 decision 2 a runner cannot announce itself: registration requires an authorization an administrator
/// of the environment has already made, naming both the runner id and the machine principal that will present it. That
/// is what makes registration reach-scoped per environment rather than a system-context operation any holder of
/// <c>runners:register</c> could aim anywhere, and it is why a stranger can neither squat an id nor discover which ids
/// and environments exist. The consequence is that the decision has to exist before the runner starts, which in a real
/// deployment an administrator makes through the UI or API.
/// </para>
/// <para>
/// The open demo has no interactive administrator, so this makes those decisions at start-up as the environments'
/// creator <c>demo</c> (the identity that, per §7.7, holds their administration). It preserves the semantic exactly —
/// an administrator, never the runner, decides — and the only demo-specific part is that nobody has to type it. The
/// ids match the <c>Runner__RunnerId</c> the AppHost injects into each runner process, because both sides have to name
/// the same runner for the decision to be about it.
/// </para>
/// <para>
/// Ordering is not load-bearing. A runner that starts before this lands is refused and retries on its next heartbeat,
/// so the composition converges either way rather than depending on who wins.
/// </para>
/// </remarks>
internal sealed class RunnerPreAuthorizationService(
    IEnvironmentRunnerAuthorizationStore authorizations,
    ILogger<RunnerPreAuthorizationService> logger) : BackgroundService
{
    private const string AdministratorActor = "demo";

    // The demo's declared runner fleet: which machine principal serves which environment under which runner id. A
    // principal may serve several tenant environments (the app and serverless runners share one Keycloak client), but
    // never a tenant environment and the platform's own — ADR 0065 decision 2 resolves such a principal to nothing at
    // all, so the system runner has its own.
    private static readonly (string Environment, string RunnerId, string Principal)[] Fleet =
    [
        ("development", "demo-runner-development", "arazzo-runner"),
        ("system", "demo-system-runner", "arazzo-access-approval"),
        ("isolated", "demo-serverless-runner", "arazzo-runner"),
    ];

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach ((string environment, string runnerId, string principal) in Fleet)
        {
            try
            {
                // Idempotent: an existing decision for this id is returned unchanged, so a restart re-asserts nothing
                // and cannot overwrite a decision an operator has since made through the UI.
                using ParsedJsonDocument<EnvironmentRunnerAuthorization> pending =
                    await authorizations.EnsurePendingAsync(environment, runnerId, AdministratorActor, principal, stoppingToken).ConfigureAwait(false);
                if (pending.RootElement.IsAuthorized)
                {
                    continue;
                }

                var decision = new RunnerAuthorizationDecision(
                    RunnerAuthorizationStatus.Authorized,
                    "Demo pre-authorization: the demo host stands in for the environment administrator.");
                using ParsedJsonDocument<EnvironmentRunnerAuthorization>? decided = await authorizations.DecideAsync(
                    environment, runnerId, decision, pending.RootElement.EtagValue, AdministratorActor, stoppingToken).ConfigureAwait(false);
                if (decided is not null)
                {
                    logger.LogInformation(
                        "Demo pre-authorized runner {RunnerId} to serve environment '{Environment}' as machine principal '{Principal}' (standing in for its administrator '{Administrator}'; production decides deliberately via the UI/API).",
                        runnerId, environment, principal, AdministratorActor);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // One environment failing must not deny the others their runners. The runner whose decision is missing
                // is refused registration and retries, so the failure is visible as a runner that never becomes ready
                // rather than as a silently idle one.
                logger.LogError(ex, "Demo pre-authorization of runner {RunnerId} for environment '{Environment}' failed; that runner cannot register until it is authorized.", runnerId, environment);
            }
        }
    }
}