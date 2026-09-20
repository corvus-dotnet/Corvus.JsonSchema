// <copyright file="AuditSinkHealthCheck.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// A health check over the deployment's audit sink (ADR 0069), which a host registers where it serves health checks:
/// <c>services.AddHealthChecks().AddCheck("audit-sink", new AuditSinkHealthCheck(auditor))</c>. It is unhealthy while the
/// last append to the sink failed, which is while governance mutations are being applied and then refused. It reports
/// what the auditor last saw and makes no call to the sink itself, so polling it costs the sink nothing.
/// </summary>
/// <param name="auditor">The deployment's governance auditor.</param>
public sealed class AuditSinkHealthCheck(GovernanceAuditor auditor) : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        AuditSinkHealth health = auditor.Health;
        if (health.IsHealthy)
        {
            return Task.FromResult(HealthCheckResult.Healthy(auditor.HasSink ? "The last audit append succeeded." : "No audit sink is configured."));
        }

        var data = new Dictionary<string, object>
        {
            ["failuresSinceSuccess"] = health.FailuresSinceSuccess,
            ["lastFailureAt"] = health.LastFailureAt?.ToString("O") ?? string.Empty,
        };
        return Task.FromResult(HealthCheckResult.Unhealthy("The audit sink refused the last record. Governance mutations are being applied and then refused.", data: data));
    }
}