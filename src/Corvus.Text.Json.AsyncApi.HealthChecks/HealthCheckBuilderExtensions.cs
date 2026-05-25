// <copyright file="HealthCheckBuilderExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AsyncAPI transport health checks.
/// </summary>
public static class HealthCheckBuilderExtensions
{
    /// <summary>
    /// Adds a health check that monitors the specified <see cref="IHealthCheckableTransport"/>.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check registration name (e.g., <c>"nats-transport"</c>).</param>
    /// <param name="transport">The transport instance to monitor.</param>
    /// <param name="failureStatus">The <see cref="HealthStatus"/> to report on failure.
    /// Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for filtering health checks.</param>
    /// <param name="timeout">Optional timeout for the health check. Defaults to 5 seconds.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddAsyncApiTransport(
        this IHealthChecksBuilder builder,
        string name,
        IHealthCheckableTransport transport,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            _ => new MessageTransportHealthCheck(transport),
            failureStatus,
            tags,
            timeout ?? TimeSpan.FromSeconds(5)));
    }

    /// <summary>
    /// Adds a health check that resolves an <see cref="IHealthCheckableTransport"/>
    /// from the service provider.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check registration name (e.g., <c>"nats-transport"</c>).</param>
    /// <param name="failureStatus">The <see cref="HealthStatus"/> to report on failure.
    /// Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for filtering health checks.</param>
    /// <param name="timeout">Optional timeout for the health check. Defaults to 5 seconds.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddAsyncApiTransport(
        this IHealthChecksBuilder builder,
        string name,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new MessageTransportHealthCheck(
                sp.GetRequiredService<IHealthCheckableTransport>()),
            failureStatus,
            tags,
            timeout ?? TimeSpan.FromSeconds(5)));
    }
}