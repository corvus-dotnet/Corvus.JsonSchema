// <copyright file="MonotonicRunnerLeaseEpochSource.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// An <see cref="IRunnerLeaseEpochSource"/> that mints epochs from a counter seeded at the current instant, so every
/// value is strictly greater than the last within the process and ordered against another instance's to within the two
/// clocks' skew.
/// </summary>
/// <remarks>
/// <para>
/// A run's grants cannot overlap — the lease is exclusive — so a counter that only ever increases is enough to order the
/// grants of any one run, which is what the epoch is compared over.
/// </para>
/// <para>
/// The seed is the wall clock rather than zero because a restarted instance must not re-issue the epochs it already
/// minted. That leaves the ordering between two instances resting on their clocks, which is why ADR 0065 decision 6
/// pairs the epoch with the store's incarnation id in phase B and persists it with the lease row rather than deriving it
/// per process.
/// </para>
/// </remarks>
public sealed class MonotonicRunnerLeaseEpochSource : IRunnerLeaseEpochSource
{
    private long counter;

    /// <summary>Initializes a new instance of the <see cref="MonotonicRunnerLeaseEpochSource"/> class.</summary>
    /// <param name="timeProvider">The time source the counter is seeded from; defaults to <see cref="TimeProvider.System"/>.</param>
    public MonotonicRunnerLeaseEpochSource(TimeProvider? timeProvider = null)
    {
        this.counter = (timeProvider ?? TimeProvider.System).GetUtcNow().ToUnixTimeMilliseconds();
    }

    /// <inheritdoc/>
    public long NextEpoch(WorkflowRunId runId) => Interlocked.Increment(ref this.counter);
}