// <copyright file="RunnerQuotaLimit.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

/// <summary>
/// One quota's setting: a sustained rate and the burst allowed above it.
/// </summary>
/// <param name="PerSecond">The sustained rate, in units per second. Zero or less disables this quota.</param>
/// <param name="Burst">The most that may be spent at once, in units. Zero or less means one second's worth of
/// <paramref name="PerSecond"/>.</param>
/// <remarks>
/// <para>
/// A rate alone is the wrong shape for this workload. A runner claiming a batch of due timers, or a workflow advancing
/// several steps as one unit of work, produces a legitimate cluster of requests that a smooth rate limiter refuses; the
/// burst is what lets the sustained rate be set to what the deployment can actually carry rather than to the peak it
/// must never reject.
/// </para>
/// <para>
/// A burst below the sustained rate is meaningful and is honoured. It does not cap throughput: tokens refill
/// continuously, so traffic spread evenly at the sustained rate finds a token waiting however small the burst is. What
/// it caps is how much may arrive at once. Only an unset burst is defaulted, because silently raising a configured one
/// would leave a deployment with a setting that reads as effective and is not.
/// </para>
/// </remarks>
public readonly record struct RunnerQuotaLimit(double PerSecond, double Burst)
{
    /// <summary>Gets a limit that enforces nothing.</summary>
    public static RunnerQuotaLimit None => new(0, 0);

    /// <summary>Gets a value indicating whether this limit enforces anything.</summary>
    public bool IsEnabled => this.PerSecond > 0;

    /// <summary>Gets the burst to apply, defaulting an unset one to a second's worth of the sustained rate.</summary>
    public double EffectiveBurst => this.Burst > 0 ? this.Burst : this.PerSecond;
}