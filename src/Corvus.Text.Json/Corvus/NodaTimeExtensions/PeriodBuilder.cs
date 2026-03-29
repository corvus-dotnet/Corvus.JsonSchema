// <copyright file="PeriodBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using NodaTime;

namespace Corvus.Text.Json;

/// <summary>
/// A mutable builder class for <see cref="Period"/> values. Each property can
/// be set independently, and then a Period can be created from the result
/// using the <see cref="BuildPeriod"/> method.
/// </summary>
/// <threadsafety>
/// This type is not thread-safe without extra synchronization, but has no
/// thread affinity.
/// </threadsafety>
public struct PeriodBuilder
{
    /// <summary>
    /// Gets or sets the number of days within the period.
    /// </summary>
    /// <value>The number of days within the period.</value>
    public int Days { get; set; }

    /// <summary>
    /// Gets or sets the number of hours within the period.
    /// </summary>
    /// <value>The number of hours within the period.</value>
    public long Hours { get; set; }

    /// <summary>
    /// Gets or sets the number of milliseconds within the period.
    /// </summary>
    /// <value>The number of milliseconds within the period.</value>
    public long Milliseconds { get; set; }

    /// <summary>
    /// Gets or sets the number of minutes within the period.
    /// </summary>
    /// <value>The number of minutes within the period.</value>
    public long Minutes { get; set; }

    /// <summary>
    /// Gets or sets the number of months within the period.
    /// </summary>
    /// <value>The number of months within the period.</value>
    public int Months { get; set; }

    /// <summary>
    /// Gets or sets the number of nanoseconds within the period.
    /// </summary>
    /// <value>The number of nanoseconds within the period.</value>
    public long Nanoseconds { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds within the period.
    /// </summary>
    /// <value>The number of seconds within the period.</value>
    public long Seconds { get; set; }

    /// <summary>
    /// Gets or sets the number of ticks within the period.
    /// </summary>
    /// <value>The number of ticks within the period.</value>
    public long Ticks { get; set; }

    /// <summary>
    /// Gets or sets the number of weeks within the period.
    /// </summary>
    /// <value>The number of weeks within the period.</value>
    public int Weeks { get; set; }

    /// <summary>
    /// Gets or sets the number of years within the period.
    /// </summary>
    /// <value>The number of years within the period.</value>
    public int Years { get; set; }

    /// <summary>
    /// Gets or sets the value of a single unit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type of this indexer is <see cref="long"/> for uniformity, but any date unit (year, month, week, day) will only ever have a value
    /// in the range of <see cref="int"/>.
    /// </para>
    /// <para>
    /// For the <see cref="PeriodUnits.Nanoseconds"/> unit, the value is converted to <c>Int64</c> when reading from the indexer, causing it to
    /// fail if the value is out of range (around 250 years). To access the values of very large numbers of nanoseconds, use the <see cref="Nanoseconds"/>
    /// property directly.
    /// </para>
    /// </remarks>
    /// <param name="unit">A single value within the <see cref="PeriodUnits"/> enumeration.</param>
    /// <value>The value of the given unit within this period builder, or zero if the unit is unset.</value>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="unit"/> is not a single unit, or a value is provided for a date unit which is outside the range of <see cref="int"/>.</exception>
    public long this[PeriodUnits unit]
    {
        readonly get => unit switch
        {
            PeriodUnits.Years => Years,
            PeriodUnits.Months => Months,
            PeriodUnits.Weeks => Weeks,
            PeriodUnits.Days => Days,
            PeriodUnits.Hours => Hours,
            PeriodUnits.Minutes => Minutes,
            PeriodUnits.Seconds => Seconds,
            PeriodUnits.Milliseconds => Milliseconds,
            PeriodUnits.Ticks => Ticks,
            PeriodUnits.Nanoseconds => Nanoseconds,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), "Indexer for PeriodBuilder only takes a single unit"),
        };

        set
        {
            switch (unit)
            {
                case PeriodUnits.Years: Years = (int)value; return;
                case PeriodUnits.Months: Months = (int)value; return;
                case PeriodUnits.Weeks: Weeks = (int)value; return;
                case PeriodUnits.Days: Days = (int)value; return;
                case PeriodUnits.Hours: Hours = value; return;
                case PeriodUnits.Minutes: Minutes = value; return;
                case PeriodUnits.Seconds: Seconds = value; return;
                case PeriodUnits.Milliseconds: Milliseconds = value; return;
                case PeriodUnits.Ticks: Ticks = value; return;
                case PeriodUnits.Nanoseconds: Nanoseconds = value; return;
                default: throw new ArgumentOutOfRangeException(nameof(unit), "Indexer for PeriodBuilder only takes a single unit");
            }
        }
    }

    /// <summary>
    /// Builds a period from the properties in this builder.
    /// </summary>
    /// <returns>The total number of nanoseconds in the period.</returns>
    public readonly Period BuildPeriod()
    {
        long totalNanoseconds = Nanoseconds +
            (Ticks * NodaConstants.NanosecondsPerTick) +
            (Milliseconds * NodaConstants.NanosecondsPerMillisecond) +
            (Seconds * NodaConstants.NanosecondsPerSecond) +
            (Minutes * NodaConstants.NanosecondsPerMinute) +
            (Hours * NodaConstants.NanosecondsPerHour) +
            (Days * NodaConstants.NanosecondsPerDay) +
            (Weeks * NodaConstants.NanosecondsPerWeek);

        int days = (int)(totalNanoseconds / NodaConstants.NanosecondsPerDay);
        long hours = (totalNanoseconds / NodaConstants.NanosecondsPerHour) % NodaConstants.HoursPerDay;
        long minutes = (totalNanoseconds / NodaConstants.NanosecondsPerMinute) % NodaConstants.MinutesPerHour;
        long seconds = (totalNanoseconds / NodaConstants.NanosecondsPerSecond) % NodaConstants.SecondsPerMinute;
        long milliseconds = (totalNanoseconds / NodaConstants.NanosecondsPerMillisecond) % NodaConstants.MillisecondsPerSecond;
        long nanoseconds = totalNanoseconds % NodaConstants.NanosecondsPerMillisecond;

        return new Period(Years, Months, 0 /* weeks */, days, hours, minutes, seconds, milliseconds, 0 /* ticks */, nanoseconds);
    }
}