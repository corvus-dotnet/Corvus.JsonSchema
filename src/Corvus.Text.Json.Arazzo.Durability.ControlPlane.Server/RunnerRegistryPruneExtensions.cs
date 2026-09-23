// <copyright file="RunnerRegistryPruneExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>Registers <see cref="RunnerRegistryPruneService"/>.</summary>
public static class RunnerRegistryPruneExtensions
{
    /// <summary>
    /// Prunes runners whose heartbeat has gone stale (ADR 0029): every <paramref name="interval"/> the runners last seen
    /// longer than <paramref name="deadAfter"/> ago are removed, each one recorded on <paramref name="auditor"/> and
    /// counted. The defaults are one heartbeat and three missed heartbeats.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="registry">The runner registry the control plane is mapped with.</param>
    /// <param name="auditor">The auditor the control plane is mapped with.</param>
    /// <param name="interval">The sweep interval; must be positive.</param>
    /// <param name="deadAfter">How long a runner may go unheard before it is pruned; must be positive.</param>
    /// <returns>The services, for chaining.</returns>
    public static IServiceCollection AddArazzoRunnerRegistryPrune(this IServiceCollection services, IRunnerRegistry registry, GovernanceAuditor auditor, TimeSpan? interval = null, TimeSpan? deadAfter = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(auditor);
        TimeSpan every = interval ?? RunnerRegistryPruneService.DefaultInterval;
        TimeSpan window = deadAfter ?? RunnerRegistryPruneService.DefaultDeadAfter;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(every, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        services.AddHostedService(provider => new RunnerRegistryPruneService(
            registry,
            auditor,
            every,
            window,
            provider.GetService<ILoggerFactory>()?.CreateLogger<RunnerRegistryPruneService>(),
            provider.GetService<TimeProvider>()));
        return services;
    }
}