// <copyright file="IRunnerQuotaGuard.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

/// <summary>
/// Meters the runner API's operations against the deployment's quotas (ADR 0065 decision 3).
/// </summary>
/// <remarks>
/// <para>
/// The tenant is passed in rather than resolved here, because the caller has already resolved it as part of resolving
/// the principal's reach, from one read and under one staleness bound. A guard that resolved it again would be charging
/// against a counter on a different schedule from the reach it is meant to bound.
/// </para>
/// <para>
/// This is an interface because the aggregate the ADR requires is per tenant across the whole deployment, and a runner
/// API is deployed as several instances. An in-process implementation therefore meters per instance, which multiplies
/// the effective allowance by the instance count; a deployment that runs more than one instance and means the aggregate
/// literally supplies an implementation backed by shared state. See
/// <see cref="TokenBucketRunnerQuotaGuard"/>, which says so about itself.
/// </para>
/// </remarks>
public interface IRunnerQuotaGuard
{
    /// <summary>Charges one request against every scope that meters <paramref name="kind"/>.</summary>
    /// <param name="kind">The dimension being charged.</param>
    /// <param name="tenant">The owner group the caller's environments belong to, or <see langword="null"/> when they
    /// name none, in which case the charge falls on the deployment.</param>
    /// <param name="principal">The authenticated machine principal.</param>
    /// <param name="cost">What to charge. One for a request-counting dimension; the byte count for
    /// <see cref="RunnerQuotaKind.CheckpointBytes"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The refusal, or <see langword="null"/> when the request is admitted.</returns>
    /// <remarks>
    /// A refusal charges nothing. Charging a request that was refused would let a caller already at its limit hold
    /// itself there by retrying, turning a momentary overshoot into an indefinite one.
    /// </remarks>
    ValueTask<RunnerQuotaRejection?> TryAcquireAsync(RunnerQuotaKind kind, string? tenant, string principal, long cost, CancellationToken cancellationToken);
}