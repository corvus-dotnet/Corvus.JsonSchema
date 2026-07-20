// <copyright file="CronSchedule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Cronos;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A cron/calendar <see cref="ISchedule"/>: occurrences are the instants a cron expression matches, evaluated in
/// a given <see cref="TimeZoneInfo"/> so a calendar cadence ("weekdays at 09:00") stays correct across daylight
/// saving transitions rather than drifting with the UTC offset. The expression is parsed once and reused.
/// </summary>
/// <remarks>
/// Backed by Cronos, which is timezone- and DST-aware: an occurrence in a gap left by a spring-forward is moved
/// to the transition instant, and one in a fall-back overlap fires once. Kept behind <see cref="ISchedule"/> so
/// the engine core never sees the parser — a different cron implementation, or a hand-rolled one, slots in here
/// with no ripple. Cron parsing (and hence <see cref="GetNextOccurrence"/>) happens per occurrence, not on any
/// per-message path, so it is deliberately not micro-optimised for allocation.
/// </remarks>
public sealed class CronSchedule : ISchedule
{
    private readonly CronExpression expression;
    private readonly TimeZoneInfo timeZone;

    /// <summary>
    /// Initializes a new instance of the <see cref="CronSchedule"/> class.
    /// </summary>
    /// <param name="expression">The cron expression — a 5-field standard expression, or a 6-field expression with a
    /// leading seconds field when <paramref name="includeSeconds"/> is <see langword="true"/>. Supports ranges,
    /// steps, lists, <c>L</c>/<c>W</c>/<c>#</c>, and <c>@daily</c>-style macros.</param>
    /// <param name="timeZone">The time zone the cadence is expressed in; occurrences are computed in this zone
    /// (DST-correct) and returned as the corresponding instants.</param>
    /// <param name="includeSeconds">Whether <paramref name="expression"/> carries a leading seconds field.</param>
    /// <exception cref="CronFormatException"><paramref name="expression"/> is not a valid cron expression.</exception>
    public CronSchedule(string expression, TimeZoneInfo timeZone, bool includeSeconds = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentNullException.ThrowIfNull(timeZone);
        this.expression = CronExpression.Parse(expression, includeSeconds ? CronFormat.IncludeSeconds : CronFormat.Standard);
        this.timeZone = timeZone;
    }

    /// <inheritdoc/>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset after)
        => this.expression.GetNextOccurrence(after, this.timeZone, inclusive: false);
}