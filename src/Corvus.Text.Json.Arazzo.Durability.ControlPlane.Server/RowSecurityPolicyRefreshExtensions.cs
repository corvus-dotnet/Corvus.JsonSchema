// <copyright file="RowSecurityPolicyRefreshExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>Registers <see cref="RowSecurityPolicyRefreshService"/>.</summary>
public static class RowSecurityPolicyRefreshExtensions
{
    /// <summary>
    /// Refreshes <paramref name="policy"/> from its store every <paramref name="interval"/> (the policy refresh bound,
    /// <see cref="RowSecurityPolicyRefreshService.DefaultInterval"/> when omitted), so a revocation made on another
    /// replica takes effect on this one within the bound. A control plane in a reach-enforcing posture refuses to map
    /// a persistent policy that this was not called for.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="policy">The policy passed to the mapping as its reach policy.</param>
    /// <param name="interval">The refresh bound; must be positive.</param>
    /// <returns>The services, for chaining.</returns>
    public static IServiceCollection AddArazzoRowSecurityPolicyRefresh(this IServiceCollection services, PersistentRowSecurityPolicy policy, TimeSpan? interval = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(policy);
        TimeSpan bound = interval ?? RowSecurityPolicyRefreshService.DefaultInterval;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bound, TimeSpan.Zero);
        services.AddSingleton(new RowSecurityPolicyRefreshRegistration(policy, bound));
        services.AddHostedService(provider => new RowSecurityPolicyRefreshService(
            policy,
            bound,
            provider.GetService<ILoggerFactory>()?.CreateLogger<RowSecurityPolicyRefreshService>(),
            provider.GetService<TimeProvider>()));
        return services;
    }
}