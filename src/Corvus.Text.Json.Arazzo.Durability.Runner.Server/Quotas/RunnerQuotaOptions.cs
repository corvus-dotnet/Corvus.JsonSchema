// <copyright file="RunnerQuotaOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

/// <summary>
/// The deployment's quota settings for the runner API (ADR 0065 decision 3): a per-tenant aggregate and a per-runner
/// sub-limit for each metered dimension.
/// </summary>
/// <remarks>
/// <para>
/// The defaults are starting points sized to be well clear of a working deployment rather than measured against one.
/// They exist so that a deployment that enables quotas without tuning them refuses only traffic that is plainly
/// abnormal, and so that the refusal path is exercised rather than dormant. A deployment that cares about the numbers
/// should set them from its own measured load.
/// </para>
/// <para>
/// The per-tenant figures are deliberately far above the per-runner ones rather than a small multiple: the per-runner
/// limit is what contains a single runaway runner, and the per-tenant limit is what contains a tenant whose whole fleet
/// is misbehaving. Setting the aggregate close to one runner's share would make normal fleet growth look like abuse.
/// </para>
/// </remarks>
public sealed class RunnerQuotaOptions
{
    /// <summary>Gets or sets the per-tenant claim rate, in claims per second. Defaults to 200/s, burst 400.</summary>
    public RunnerQuotaLimit TenantClaims { get; set; } = new(200, 400);

    /// <summary>Gets or sets the per-runner claim rate, in claims per second. Defaults to 20/s, burst 40.</summary>
    public RunnerQuotaLimit RunnerClaims { get; set; } = new(20, 40);

    /// <summary>Gets or sets the per-tenant checkpoint rate, in requests per second. Defaults to 500/s, burst 1000.</summary>
    public RunnerQuotaLimit TenantCheckpoints { get; set; } = new(500, 1000);

    /// <summary>Gets or sets the per-runner checkpoint rate, in requests per second. Defaults to 50/s, burst 100.</summary>
    public RunnerQuotaLimit RunnerCheckpoints { get; set; } = new(50, 100);

    /// <summary>Gets or sets the per-tenant checkpoint volume, in bytes per second. Defaults to 100 MiB/s, burst 200 MiB.</summary>
    public RunnerQuotaLimit TenantCheckpointBytes { get; set; } = new(100 * 1024 * 1024, 200L * 1024 * 1024);

    /// <summary>Gets or sets the per-runner checkpoint volume, in bytes per second. Defaults to 20 MiB/s, burst 40 MiB.</summary>
    public RunnerQuotaLimit RunnerCheckpointBytes { get; set; } = new(20 * 1024 * 1024, 40L * 1024 * 1024);

    /// <summary>Gets or sets the per-tenant lease-renewal rate. Defaults to <see cref="RunnerQuotaLimit.None"/>.</summary>
    /// <remarks>Off by default. A renewal is what keeps a claimed run from being taken away mid-advance, so refusing one
    /// across a whole tenant costs that tenant work in progress, and the runner that caused it is already contained by
    /// <see cref="RunnerLeaseRenewals"/>.</remarks>
    public RunnerQuotaLimit TenantLeaseRenewals { get; set; } = RunnerQuotaLimit.None;

    /// <summary>Gets or sets the per-runner lease-renewal rate, in renewals per second. Defaults to 20/s, burst 40.</summary>
    public RunnerQuotaLimit RunnerLeaseRenewals { get; set; } = new(20, 40);

    /// <summary>Gets or sets the per-tenant catalog read rate, in requests per second. Defaults to 200/s, burst 400.</summary>
    public RunnerQuotaLimit TenantCatalog { get; set; } = new(200, 400);

    /// <summary>Gets or sets the per-runner catalog read rate, in requests per second. Defaults to 20/s, burst 40.</summary>
    public RunnerQuotaLimit RunnerCatalog { get; set; } = new(20, 40);

    /// <summary>
    /// Gets or sets the most counters to hold at once. A counter is keyed by an authenticated principal or by an owner
    /// group, so the population is bounded in practice; the cap is there so it is bounded by construction too.
    /// Defaults to 4096.
    /// </summary>
    public int MaximumCounters { get; set; } = 4096;

    /// <summary>Gets the limit for one dimension at one scope.</summary>
    /// <param name="kind">The dimension.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The limit, which may be <see cref="RunnerQuotaLimit.None"/>.</returns>
    public RunnerQuotaLimit For(RunnerQuotaKind kind, RunnerQuotaScope scope) => (kind, scope) switch
    {
        (RunnerQuotaKind.Claim, RunnerQuotaScope.Tenant) => this.TenantClaims,
        (RunnerQuotaKind.Claim, RunnerQuotaScope.Runner) => this.RunnerClaims,
        (RunnerQuotaKind.Checkpoint, RunnerQuotaScope.Tenant) => this.TenantCheckpoints,
        (RunnerQuotaKind.Checkpoint, RunnerQuotaScope.Runner) => this.RunnerCheckpoints,
        (RunnerQuotaKind.CheckpointBytes, RunnerQuotaScope.Tenant) => this.TenantCheckpointBytes,
        (RunnerQuotaKind.CheckpointBytes, RunnerQuotaScope.Runner) => this.RunnerCheckpointBytes,
        (RunnerQuotaKind.LeaseRenewal, RunnerQuotaScope.Tenant) => this.TenantLeaseRenewals,
        (RunnerQuotaKind.LeaseRenewal, RunnerQuotaScope.Runner) => this.RunnerLeaseRenewals,
        (RunnerQuotaKind.Catalog, RunnerQuotaScope.Tenant) => this.TenantCatalog,
        (RunnerQuotaKind.Catalog, RunnerQuotaScope.Runner) => this.RunnerCatalog,
        _ => RunnerQuotaLimit.None,
    };
}