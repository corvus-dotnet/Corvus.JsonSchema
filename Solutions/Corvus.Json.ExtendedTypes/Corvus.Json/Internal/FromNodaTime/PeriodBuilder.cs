// <copyright file="PeriodBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Copyright 2012 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.
// </licensing>

using NodaTime;

namespace Corvus.Json;

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
    /// Gets or sets the number of years within the period.
    /// </summary>
    /// <value>The number of years within the period.</value>
    public int Years { get; set; }

    /// <summary>
    /// Gets or sets the number of months within the period.
    /// </summary>
    /// <value>The number of months within the period.</value>
    public int Months { get; set; }

    /// <summary>
    /// Gets or sets the number of weeks within the period.
    /// </summary>
    /// <value>The number of weeks within the period.</value>
    public int Weeks { get; set; }

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
    /// Gets or sets the number of minutes within the period.
    /// </summary>
    /// <value>The number of minutes within the period.</value>
    public long Minutes { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds within the period.
    /// </summary>
    /// <value>The number of seconds within the period.</value>
    public long Seconds { get; set; }

    /// <summary>
    /// Gets or sets the number of milliseconds within the period.
    /// </summary>
    /// <value>The number of milliseconds within the period.</value>
    public long Milliseconds { get; set; }

    /// <summary>
    /// Gets or sets the number of ticks within the period.
    /// </summary>
    /// <value>The number of ticks within the period.</value>
    public long Ticks { get; set; }

    /// <summary>
    /// Gets or sets the number of nanoseconds within the period.
    /// </summary>
    /// <value>The number of nanoseconds within the period.</value>
    public long Nanoseconds { get; set; }

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
            PeriodUnits.Years => this.Years,
            PeriodUnits.Months => this.Months,
            PeriodUnits.Weeks => this.Weeks,
            PeriodUnits.Days => this.Days,
            PeriodUnits.Hours => this.Hours,
            PeriodUnits.Minutes => this.Minutes,
            PeriodUnits.Seconds => this.Seconds,
            PeriodUnits.Milliseconds => this.Milliseconds,
            PeriodUnits.Ticks => this.Ticks,
            PeriodUnits.Nanoseconds => this.Nanoseconds,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), "Indexer for PeriodBuilder only takes a single unit"),
        };
        set
        {
            switch (unit)
            {
                case PeriodUnits.Years: this.Years = (int)value; return;
                case PeriodUnits.Months: this.Months = (int)value; return;
                case PeriodUnits.Weeks: this.Weeks = (int)value; return;
                case PeriodUnits.Days: this.Days = (int)value; return;
                case PeriodUnits.Hours: this.Hours = value; return;
                case PeriodUnits.Minutes: this.Minutes = value; return;
                case PeriodUnits.Seconds: this.Seconds = value; return;
                case PeriodUnits.Milliseconds: this.Milliseconds = value; return;
                case PeriodUnits.Ticks: this.Ticks = value; return;
                case PeriodUnits.Nanoseconds: this.Nanoseconds = value; return;
                default: throw new ArgumentOutOfRangeException(nameof(unit), "Indexer for PeriodBuilder only takes a single unit");
            }
        }
    }

    /// <summary>
    /// Builds a period from the properties in this builder.
    /// </summary>
    /// <returns>The total number of nanoseconds in the period.</returns>
    public readonly Period BuildPeriod() => new Period(this.Years, this.Months, this.Weeks, this.Days, this.Hours, this.Minutes, this.Seconds, this.Milliseconds, this.Ticks, this.Nanoseconds);
}