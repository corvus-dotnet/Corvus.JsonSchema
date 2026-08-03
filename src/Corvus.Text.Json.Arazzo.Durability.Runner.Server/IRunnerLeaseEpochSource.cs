// <copyright file="IRunnerLeaseEpochSource.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Mints the epoch a lease grant carries (ADR 0065 decision 6). One epoch is minted per <em>grant</em>, never per
/// extension, so renewing a lease does not invalidate checkpoints already written under it.
/// </summary>
/// <remarks>
/// An implementation must return a value strictly greater than every value it has returned for the same run, because the
/// two rules the epoch exists for are both comparisons: a checkpoint minted above the current grant was written under a
/// grant this holder never held, and one below the run's high-water mark is a rollback. Phase B additionally pairs the
/// epoch with the store's incarnation id, without which a restore silently re-issues spent epochs.
/// </remarks>
public interface IRunnerLeaseEpochSource
{
    /// <summary>Mints the epoch for a fresh grant over a run.</summary>
    /// <param name="runId">The run being granted.</param>
    /// <returns>The grant's epoch, strictly greater than any previously minted for this run.</returns>
    long NextEpoch(WorkflowRunId runId);
}