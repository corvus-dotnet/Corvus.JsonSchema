// <copyright file="RunnerClaim.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// A run this runner has claimed, and what it was told about the lease it now holds.
/// </summary>
/// <remarks>
/// The lease <em>token</em> is deliberately absent. The client holds it and presents it on every operation for the run,
/// so a runner's own code never handles it: there is nothing for it to log, persist, or pass to the wrong run.
/// </remarks>
/// <param name="RunId">The claimed run.</param>
/// <param name="WorkflowId">The versioned workflow id the run executes, from this runner's own hosted versions.</param>
/// <param name="Environment">The environment the run is pinned to, resolved from the run rather than asked for.</param>
/// <param name="LeaseExpiresAt">When the lease lapses and the run becomes claimable again. Renew before this.</param>
/// <param name="LeaseEpoch">The grant's epoch, minted server-side and monotonic.</param>
public readonly record struct RunnerClaim(
    WorkflowRunId RunId,
    string WorkflowId,
    string Environment,
    DateTimeOffset LeaseExpiresAt,
    long LeaseEpoch)
{
    /// <summary>Gets the run's full <c>(environment, runId)</c> address (ADR 0065 decision 9).</summary>
    public WorkflowRunAddress Address => new(this.Environment, this.RunId);
}