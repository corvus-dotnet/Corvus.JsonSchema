// <copyright file="ControlPlaneSessionTicketsExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>Registers <see cref="ControlPlaneSessionTickets"/>.</summary>
public static class ControlPlaneSessionTicketsExtensions
{
    /// <summary>
    /// Keeps the session tickets of a cookie scheme on the server (ADR 0075), so that signing out revokes the session and
    /// <see cref="IControlPlaneSessionRevocation"/> can end every session of a subject. The host registers the
    /// <see cref="IDistributedCache"/> the tickets are kept in.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="cookieScheme">The cookie authentication scheme whose tickets are kept.</param>
    /// <param name="configure">Sets the maximum session lifetime and the cache key prefix, if the defaults do not suit.</param>
    /// <returns>The services, for chaining.</returns>
    public static IServiceCollection AddArazzoControlPlaneSessionTickets(this IServiceCollection services, string cookieScheme, Action<ControlPlaneSessionTicketOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(cookieScheme);

        ControlPlaneSessionTicketOptions options = new();
        configure?.Invoke(options);
        services.Replace(ServiceDescriptor.Singleton(sp => new ControlPlaneSessionTickets(
            sp.GetRequiredService<IDistributedCache>(),
            sp.GetRequiredService<IDataProtectionProvider>(),
            options,
            sp.GetService<TimeProvider>())));
        services.Replace(ServiceDescriptor.Singleton<IControlPlaneSessionRevocation>(sp => sp.GetRequiredService<ControlPlaneSessionTickets>()));
        services.AddOptions<CookieAuthenticationOptions>(cookieScheme)
            .Configure<ControlPlaneSessionTickets>(static (cookie, tickets) => cookie.SessionStore = tickets);
        return services;
    }
}