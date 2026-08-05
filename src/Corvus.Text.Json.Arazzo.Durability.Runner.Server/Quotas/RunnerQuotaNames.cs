// <copyright file="RunnerQuotaNames.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

/// <summary>
/// The names a quota refusal carries: the stable identifier of the quota that refused, and of the counter it was
/// measured against.
/// </summary>
/// <remarks>
/// Declared as constants rather than composed per refusal. They are part of the API's observable contract, so a client
/// may match on them, and building them by concatenation at each call site would make a rename silent and would
/// allocate on a path a caller can drive.
/// </remarks>
public static class RunnerQuotaNames
{
    /// <summary>The counter reported when the quota is measured against the deployment rather than a named owner group.</summary>
    /// <remarks>
    /// Only a report. The counter a bucket is actually keyed by is the absent owner group itself, so an owner group that
    /// happened to be called <c>deployment</c> shares nothing with the deployment's own counter. Naming it here rather
    /// than leaving the field empty means a runner and an operator reading the refusal see what was charged.
    /// </remarks>
    public const string Deployment = "deployment";

    private const string TenantSuffix = "/tenant";
    private const string RunnerSuffix = "/runner";

    /// <summary>The name of the quota for one dimension at one scope.</summary>
    /// <param name="kind">The dimension.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The quota name.</returns>
    public static string Of(RunnerQuotaKind kind, RunnerQuotaScope scope) => (kind, scope) switch
    {
        (RunnerQuotaKind.Claim, RunnerQuotaScope.Tenant) => "claim-rate" + TenantSuffix,
        (RunnerQuotaKind.Claim, RunnerQuotaScope.Runner) => "claim-rate" + RunnerSuffix,
        (RunnerQuotaKind.Checkpoint, RunnerQuotaScope.Tenant) => "checkpoint-rate" + TenantSuffix,
        (RunnerQuotaKind.Checkpoint, RunnerQuotaScope.Runner) => "checkpoint-rate" + RunnerSuffix,
        (RunnerQuotaKind.CheckpointBytes, RunnerQuotaScope.Tenant) => "checkpoint-bytes" + TenantSuffix,
        (RunnerQuotaKind.CheckpointBytes, RunnerQuotaScope.Runner) => "checkpoint-bytes" + RunnerSuffix,
        (RunnerQuotaKind.LeaseRenewal, RunnerQuotaScope.Tenant) => "lease-renewal-rate" + TenantSuffix,
        (RunnerQuotaKind.LeaseRenewal, RunnerQuotaScope.Runner) => "lease-renewal-rate" + RunnerSuffix,
        (RunnerQuotaKind.Catalog, RunnerQuotaScope.Tenant) => "catalog-rate" + TenantSuffix,
        (RunnerQuotaKind.Catalog, RunnerQuotaScope.Runner) => "catalog-rate" + RunnerSuffix,
        _ => "unknown",
    };

    /// <summary>The name of the counter a scope was measured against.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="counter">The owner group or principal, or <see langword="null"/> for an absent owner group.</param>
    /// <returns>The counter name.</returns>
    public static string CounterOf(RunnerQuotaScope scope, string? counter)
        => counter ?? (scope == RunnerQuotaScope.Tenant ? Deployment : string.Empty);
}