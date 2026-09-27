// <copyright file="ControlPlaneSecurityHeadersExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>Registers <see cref="ControlPlaneSecurityHeaders"/>.</summary>
public static class ControlPlaneSecurityHeadersExtensions
{
    /// <summary>
    /// Adds the browser security headers (ADR 0073) to every response the host serves: the one registration a host
    /// makes. A control plane in a secured posture refuses to map without it. A later call replaces an earlier one's
    /// sources.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configure">Names the other origins the host's deployment needs, if any.</param>
    /// <returns>The services, for chaining.</returns>
    public static IServiceCollection AddArazzoSecurityHeaders(this IServiceCollection services, Action<ControlPlaneSecurityHeadersOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ControlPlaneSecurityHeadersOptions options = new();
        configure?.Invoke(options);
        services.Replace(ServiceDescriptor.Singleton(new ControlPlaneSecurityHeaders(options)));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, ControlPlaneSecurityHeaders.StartupFilter>());
        return services;
    }
}