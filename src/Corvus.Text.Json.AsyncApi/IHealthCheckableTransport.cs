// <copyright file="IHealthCheckableTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Provides health status information for a message transport.
/// </summary>
/// <remarks>
/// <para>
/// Transport implementations that support health monitoring should implement this
/// interface in addition to <see cref="IMessageTransport"/>. The
/// <c>MessageTransportHealthCheck</c> in the HealthChecks package uses this to report
/// transport connectivity and operational status.
/// </para>
/// <para>
/// Implementations should return quickly (sub-second). For transports that require
/// a broker round-trip (ping/heartbeat), the health check configures a timeout.
/// </para>
/// </remarks>
public interface IHealthCheckableTransport
{
    /// <summary>
    /// Gets a value indicating whether the transport is currently connected to the broker.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the messaging system name (e.g., <c>"nats"</c>, <c>"kafka"</c>, <c>"amqp"</c>).
    /// </summary>
    string MessagingSystem { get; }

    /// <summary>
    /// Performs a lightweight connectivity check against the broker.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token (typically with a short timeout).</param>
    /// <returns><see langword="true"/> if the broker responded; otherwise <see langword="false"/>.</returns>
    ValueTask<bool> PingAsync(CancellationToken cancellationToken = default);
}