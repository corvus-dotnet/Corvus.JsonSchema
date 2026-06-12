// <copyright file="ISchedule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A cadence: given an instant, returns the next instant at which a schedule fires. Kept as an explicit
/// dependency (injected into <see cref="ScheduleWorkflowTrigger"/>) rather than a delegate carried on the
/// binding, so the schedule binding stays plain, serializable host-config data while richer cadences (cron,
/// calendar) can be supplied as code without this layer needing a parser.
/// </summary>
public interface ISchedule
{
    /// <summary>Returns the first occurrence strictly after <paramref name="after"/>, or <see langword="null"/> to stop.</summary>
    /// <param name="after">The instant to advance from.</param>
    /// <returns>The next occurrence, or <see langword="null"/> if the schedule has no further occurrences.</returns>
    DateTimeOffset? GetNextOccurrence(DateTimeOffset after);
}

/// <summary>A fixed-interval <see cref="ISchedule"/>: fires every <see cref="Interval"/> from the current instant.</summary>
/// <param name="Interval">The gap between occurrences. Must be positive.</param>
public sealed record IntervalSchedule(TimeSpan Interval) : ISchedule
{
    /// <inheritdoc/>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset after)
        => this.Interval > TimeSpan.Zero ? after + this.Interval : null;
}