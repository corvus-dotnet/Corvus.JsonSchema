// <copyright file="RunnerQuotaKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

/// <summary>
/// The dimensions the runner API meters (ADR 0065 decision 3). Each is charged separately so a runner told to back off
/// backs off against the dimension it exhausted rather than globally, which is what the <c>quota</c> field of the
/// refusal carries.
/// </summary>
public enum RunnerQuotaKind
{
    /// <summary>Taking work: the run claim and the timer and message sweeps.</summary>
    /// <remarks>
    /// Metered because a claim returns the run's row, so it is a bulk read path in its own right and not merely a
    /// scheduling call. Already batch-capped by <see cref="RunnerApiOptions.ClaimCandidates"/> and
    /// <see cref="RunnerApiOptions.MaximumSweep"/>; the cap bounds one request, and this bounds their rate.
    /// </remarks>
    Claim,

    /// <summary>Reading and writing a run's checkpoint.</summary>
    /// <remarks>The hottest surface in the system, and the one a chatty workflow drives. Charged per request.</remarks>
    Checkpoint,

    /// <summary>The bytes moved by checkpoint reads and writes.</summary>
    /// <remarks>
    /// Separate from <see cref="Checkpoint"/> because request count and volume are different resources: a workflow
    /// saving one large checkpoint per minute and one saving small ones continuously exhaust different things, and a
    /// single counter would refuse whichever it happened to be tuned for.
    /// </remarks>
    CheckpointBytes,

    /// <summary>Extending a held lease.</summary>
    /// <remarks>Metered per runner rather than per tenant by default: a renewal storm from one misbehaving runner must
    /// not be able to stall a tenant's checkpoint saves, which is the thing that actually loses work.</remarks>
    LeaseRenewal,

    /// <summary>Listing and reading executable versions and their artifacts.</summary>
    Catalog,
}