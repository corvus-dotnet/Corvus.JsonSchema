// <copyright file="RunnerQuotaRejection.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

/// <summary>
/// A quota's refusal of one request: which quota refused, what it was measured against, and how long the caller should
/// wait (ADR 0065 decision 3).
/// </summary>
/// <param name="Quota">The quota that refused, as the contract's <c>quota</c> field carries it.</param>
/// <param name="Counter">The counter it was measured against, as the contract's <c>counter</c> field carries it.</param>
/// <param name="RetryAfter">How long until the request would be admitted, as the <c>Retry-After</c> header carries it.</param>
/// <remarks>
/// <para>
/// Naming both is what makes the refusal actionable rather than merely retryable. A runner told only "too many
/// requests" can do nothing but back off everything it does; one told which dimension and which counter can keep
/// saving checkpoints while it slows its claims, and an operator reading the refusal knows whether to raise a limit or
/// to find the runner that is consuming its tenant's allowance.
/// </para>
/// <para>
/// This is deliberately not a disclosure: the counter is the caller's own tenant or its own principal, both of which it
/// already knows. It never names another tenant, and it never reveals whether a limit was reached because of this
/// caller's traffic or a peer's.
/// </para>
/// </remarks>
public readonly record struct RunnerQuotaRejection(string Quota, string Counter, TimeSpan RetryAfter);