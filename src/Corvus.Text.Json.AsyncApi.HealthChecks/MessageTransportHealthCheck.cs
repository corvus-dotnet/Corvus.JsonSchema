// <copyright file="MessageTransportHealthCheck.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.AsyncApi;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Corvus.Text.Json.AsyncApi.HealthChecks;

/// <summary>
/// An ASP.NET Core health check that monitors <see cref="IMessageTransport"/> connectivity.
/// </summary>
/// <remarks>
/// <para>
/// Reports <see cref="HealthStatus.Healthy"/> when the transport responds to a ping within
/// the configured timeout, <see cref="HealthStatus.Degraded"/> when the transport reports
/// disconnected but the broker may still be reachable, and <see cref="HealthStatus.Unhealthy"/>
/// when the ping fails or times out.
/// </para>
/// <para>
/// Register via DI:
/// <code>
/// builder.Services.AddHealthChecks()
///     .AddAsyncApiTransport("nats", myTransport);
/// </code>
/// </para>
/// </remarks>
public sealed class MessageTransportHealthCheck : IHealthCheck
{
    private readonly IHealthCheckableTransport transport;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTransportHealthCheck"/> class.
    /// </summary>
    /// <param name="transport">The transport to monitor.</param>
    public MessageTransportHealthCheck(IHealthCheckableTransport transport)
    {
        this.transport = transport;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!this.transport.IsConnected)
            {
                return HealthCheckResult.Degraded(
                    $"Transport '{this.transport.MessagingSystem}' reports disconnected.",
                    data: new Dictionary<string, object>
                    {
                        ["messaging.system"] = this.transport.MessagingSystem,
                        ["connected"] = false,
                    });
            }

            bool pingSuccess = await this.transport.PingAsync(cancellationToken).ConfigureAwait(false);

            if (pingSuccess)
            {
                return HealthCheckResult.Healthy(
                    $"Transport '{this.transport.MessagingSystem}' is responsive.",
                    data: new Dictionary<string, object>
                    {
                        ["messaging.system"] = this.transport.MessagingSystem,
                        ["connected"] = true,
                    });
            }

            return HealthCheckResult.Unhealthy(
                $"Transport '{this.transport.MessagingSystem}' ping failed.",
                data: new Dictionary<string, object>
                {
                    ["messaging.system"] = this.transport.MessagingSystem,
                    ["connected"] = this.transport.IsConnected,
                });
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                $"Transport '{this.transport.MessagingSystem}' ping timed out.",
                data: new Dictionary<string, object>
                {
                    ["messaging.system"] = this.transport.MessagingSystem,
                    ["connected"] = this.transport.IsConnected,
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Transport '{this.transport.MessagingSystem}' health check failed.",
                ex,
                data: new Dictionary<string, object>
                {
                    ["messaging.system"] = this.transport.MessagingSystem,
                    ["error.type"] = ex.GetType().FullName ?? ex.GetType().Name,
                });
        }
    }
}