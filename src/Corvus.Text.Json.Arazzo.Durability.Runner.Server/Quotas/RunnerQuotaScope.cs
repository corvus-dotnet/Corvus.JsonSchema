// <copyright file="RunnerQuotaScope.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

/// <summary>
/// What a quota is measured against (ADR 0065 decision 3). Every dimension is metered at both scopes, and the tighter
/// of the two is what refuses.
/// </summary>
/// <remarks>
/// Both exist because either alone leaves a hole. A per-runner limit alone lets a tenant exceed any aggregate simply by
/// registering more runners, which under an autoscaling deployment it does automatically. A per-tenant limit alone lets
/// one runaway runner consume its tenant's whole allowance and starve that tenant's other runners, which is a
/// self-inflicted outage the platform could have contained.
/// </remarks>
public enum RunnerQuotaScope
{
    /// <summary>The owner group the principal's environments belong to, or the deployment when they name none.</summary>
    Tenant,

    /// <summary>The individual machine principal.</summary>
    Runner,
}