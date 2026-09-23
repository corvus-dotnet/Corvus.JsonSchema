// <copyright file="RunnerRegistryPruneService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Prunes runners whose heartbeat has gone stale (ADR 0029): every <see cref="Interval"/> it removes each registration
/// last seen longer than <see cref="DeadAfter"/> ago, so a dead runner stops satisfying the hosting gates
/// (<see cref="IRunnerRegistry.IsVersionHostedAsync"/> and its siblings) that would otherwise keep offering work to
/// it. Each pruned runner is recorded as an audit mutation and counted (<c>corvus.arazzo.runners.pruned</c>). A live
/// runner that was pruned during a pause learns of it on its next heartbeat and registers again. A sweep that fails
/// is logged and retried on the next tick; the service never stops on its own.
/// </summary>
public sealed class RunnerRegistryPruneService : BackgroundService
{
    /// <summary>The default sweep interval, one runner heartbeat.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(15);

    /// <summary>The default window after which an unheard runner is dead: three missed heartbeats.</summary>
    public static readonly TimeSpan DefaultDeadAfter = TimeSpan.FromSeconds(45);

    private const string Actor = "system:runner-registry-prune";
    private const string TargetKind = "runner";

    private readonly IRunnerRegistry registry;
    private readonly GovernanceAuditor auditor;
    private readonly TimeSpan interval;
    private readonly TimeSpan deadAfter;
    private readonly ILogger? logger;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="RunnerRegistryPruneService"/> class.</summary>
    /// <param name="registry">The runner registry to prune.</param>
    /// <param name="auditor">The auditor each prune is recorded on.</param>
    /// <param name="interval">The sweep interval; must be positive.</param>
    /// <param name="deadAfter">How long a runner may go unheard before it is pruned; must be positive.</param>
    /// <param name="logger">The logger a failed sweep is recorded on; <see langword="null"/> where the host registers none.</param>
    /// <param name="timeProvider">The clock; defaults to <see cref="TimeProvider.System"/>.</param>
    public RunnerRegistryPruneService(IRunnerRegistry registry, GovernanceAuditor auditor, TimeSpan interval, TimeSpan deadAfter, ILogger? logger, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(auditor);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deadAfter, TimeSpan.Zero);
        this.registry = registry;
        this.auditor = auditor;
        this.interval = interval;
        this.deadAfter = deadAfter;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the sweep interval.</summary>
    public TimeSpan Interval => this.interval;

    /// <summary>Gets the window after which an unheard runner is pruned.</summary>
    public TimeSpan DeadAfter => this.deadAfter;

    /// <summary>Runs one sweep: prunes every runner last seen longer than <see cref="DeadAfter"/> ago, records each
    /// one and counts them.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runners pruned.</returns>
    public async ValueTask<int> SweepAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset deadBefore = this.timeProvider.GetUtcNow() - this.deadAfter;

        // The candidates are named before the prune so each one can be recorded; the prune itself is the store's own
        // bulk operation. A candidate that heartbeats between the two survives, and its point read after the prune says so.
        List<(string RunnerId, string Environment)>? stale = null;
        foreach (RunnerRegistration registration in await this.registry.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (registration.LastSeenAtValue < deadBefore)
            {
                (stale ??= []).Add((registration.RunnerIdValue, registration.EnvironmentValue));
            }
        }

        if (stale is null)
        {
            return 0;
        }

        int pruned = await this.registry.PruneAsync(deadBefore, cancellationToken).ConfigureAwait(false);
        ArazzoTelemetry.RunnersPruned.Add(pruned);
        foreach ((string runnerId, string environment) in stale)
        {
            if (await this.registry.GetAsync(runnerId, cancellationToken).ConfigureAwait(false) is null)
            {
                await this.auditor.MutationAsync("runner.prune", Actor, TargetKind, runnerId, "pruned", environment, cancellationToken).ConfigureAwait(false);
            }
        }

        this.logger?.LogInformation("Pruned {Count} runner(s) whose heartbeat was older than {DeadAfter}.", pruned, this.deadAfter);
        return pruned;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(this.interval, this.timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await this.SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                this.logger?.LogWarning(exception, "The runner registry could not be pruned; the next attempt is in {Interval}.", this.interval);
            }
        }
    }
}