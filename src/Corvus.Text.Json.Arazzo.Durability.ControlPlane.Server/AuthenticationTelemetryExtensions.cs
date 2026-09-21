// <copyright file="AuthenticationTelemetryExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>Registers <see cref="AuthenticationTelemetry"/>.</summary>
public static class AuthenticationTelemetryExtensions
{
    /// <summary>
    /// Adds authentication event telemetry (ADR 0071): the one registration a host makes. Every request's authentication
    /// is counted, and a failure is recorded in the audit chain of the auditor the control plane is mapped with. A
    /// control plane in a secured posture refuses to map without it.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The services, for chaining.</returns>
    public static IServiceCollection AddArazzoAuthenticationTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<AuthenticationTelemetry>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, AuthenticationTelemetry.StartupFilter>());
        return services;
    }
}