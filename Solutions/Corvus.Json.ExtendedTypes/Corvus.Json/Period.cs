// <copyright file="Period.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Copyright 2010 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.
// </licensing>

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using NodaTime;
using NodaTime.Text;
using static NodaTime.NodaConstants;

namespace Corvus.Json;

/// <summary>
/// Represents a period of time expressed in human chronological terms: hours, days,
/// weeks, months and so on.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Period"/> contains a set of properties such as <see cref="Years"/>, <see cref="Months"/>, and so on
/// that return the number of each unit contained within this period. Note that these properties are not normalized in
/// any way by default, and so a <see cref="Period"/> may contain values such as "2 hours and 90 minutes". The
/// <see cref="Normalize"/> method will convert equivalent periods into a standard representation.
/// </para>
/// <para>
/// Periods can contain negative units as well as positive units ("+2 hours, -43 minutes, +10 seconds"), but do not
/// differentiate between properties that are zero and those that are absent (i.e. a period created as "10 years"
/// and one created as "10 years, zero months" are equal periods; the <see cref="Months"/> property returns zero in
/// both cases).
/// </para>
/// <para>
/// <see cref="Period"/> equality is implemented by comparing each property's values individually, without any normalization.
/// (For example, a period of "24 hours" is not considered equal to a period of "1 day".) The static
/// <see cref="NormalizingEqualityComparer"/> comparer provides an equality comparer which performs normalization before comparisons.
/// </para>
/// <para>
/// There is no natural ordering for periods, but <see cref="CreateComparer(LocalDateTime)"/> can be used to create a
/// comparer which orders periods according to a reference date, by adding each period to that date and comparing the results.
/// </para>
/// <para>
/// Periods operate on calendar-related types such as
/// <see cref="LocalDateTime" /> whereas <see cref="Duration"/> operates on instants
/// on the time line. (Note that although <see cref="ZonedDateTime" /> includes both concepts, it only supports
/// duration-based arithmetic.)
/// </para>
/// <para>
/// The complexity of each method in this type is hard to document precisely, and often depends on the calendar system
/// involved in performing the actual calculations. Operations do not depend on the magnitude of the units in the period,
/// other than for optimizations for values of zero or occasionally for particularly small values. For example,
/// adding 10,000 days to a date does not require greater algorithmic complexity than adding 1,000 days to the same date.
/// </para>
/// </remarks>
/// <threadsafety>This type is immutable reference type. See the thread safety section of the user guide for more information.</threadsafety>
public readonly struct Period : IEquatable<Period>
{
    /// <summary>
    /// Creates a new period from the given values.
    /// </summary>
#pragma warning disable SA1611
    internal Period(int years, int months, int weeks, int days, long hours, long minutes, long seconds, long milliseconds, long ticks, long nanoseconds)
#pragma warning restore SA1611
    {
        this.Years = years;
        this.Months = months;
        this.Weeks = weeks;
        this.Days = days;
        this.Hours = hours;
        this.Minutes = minutes;
        this.Seconds = seconds;
        this.Milliseconds = milliseconds;
        this.Ticks = ticks;
        this.Nanoseconds = nanoseconds;
    }

    /// <summary>
    /// Creates a period with the given date values.
    /// </summary>
    private Period(int years, int months, int weeks, int days)
    {
        this.Years = years;
        this.Months = months;
        this.Weeks = weeks;
        this.Days = days;
    }

    /// <summary>
    /// Creates a period with the given time values.
    /// </summary>
    private Period(long hours, long minutes, long seconds, long milliseconds, long ticks, long nanoseconds)
    {
        this.Hours = hours;
        this.Minutes = minutes;
        this.Seconds = seconds;
        this.Milliseconds = milliseconds;
        this.Ticks = ticks;
        this.Nanoseconds = nanoseconds;
    }

    // General implementation note: operations such as normalization work out the total number of nanoseconds as an Int64
    // value. This can handle +/- 106,751 days, or 292 years. We could move to using BigInteger if we feel that's required,
    // but it's unlikely to be an issue. Ideally, we'd switch to use BigInteger after detecting that it could be a problem,
    // but without the hit of having to catch the exception...

    /// <summary>
    /// Gets a period containing only zero-valued properties.
    /// </summary>
    public static Period Zero { get; } = new Period(0, 0, 0, 0);

    /// <summary>
    /// Gets an equality comparer which compares periods by first normalizing them - so 24 hours is deemed equal to 1 day, and so on.
    /// Note that as per the <see cref="Normalize"/> method, years and months are unchanged by normalization - so 12 months does not
    /// equal 1 year.
    /// </summary>
    /// <value>An equality comparer which compares periods by first normalizing them.</value>
    public static IEqualityComparer<Period> NormalizingEqualityComparer => NormalizingPeriodEqualityComparer.Instance;

    // The fields that make up this period.

    /// <summary>
    /// Gets the number of nanoseconds within this period.
    /// </summary>
    /// <remarks>
    /// This property returns zero both when the property has been explicitly set to zero and when the period does not
    /// contain this property.
    /// </remarks>
    /// <value>The number of nanoseconds within this period.</value>
    public long Nanoseconds { get; }

    /// <summary>
    /// Gets the number of ticks within this period.
    /// </summary>
    /// <remarks>
    /// This property returns zero both when the property has been explicitly set to zero and when the period does not
    /// contain this property.
    /// </remarks>
    /// <value>The number of ticks within this period.</value>
    public long Ticks { get; }

    /// <summary>
    /// Gets the number of milliseconds within this period.
    /// </summary>
    /// <remarks>
    /// This property returns zero both when the property has been explicitly set to zero and when the period does not
    /// contain this property.
    /// </remarks>
    /// <value>The number of milliseconds within this period.</value>
    public long Milliseconds { get; }

    /// <summary>
    /// Gets the number of seconds within this period.
    /// </summary>
    /// <remarks>
    /// This property returns zero both when the property has been explicitly set to zero and when the period does not
    /// contain this property.
    /// </remarks>
    /// <value>The number of seconds within this period.</value>
    public long Seconds { get; }

    /// <summary>
    /// Gets the number of minutes within this period.
    /// </summary>
    /// <remarks>
    /// This property returns zero both when the property has been explicitly set to zero and when the period does not
    /// contain this property.
    /// </remarks>
    /// <value>The number of minutes within this period.</value>
    public long Minutes { get; }

    /// <summary>
    /// Gets the number of hours within this period.
    /// </summary>
    /// <remarks>
    /// This property returns zero both when the property has been explicitly set to zero and when the period does not
    /// contain this property.
    /// </remarks>
    /// <value>The number of hours within this period.</value>
    public long Hours { get; }

    /// <summary>
    /// Gets the number of days within this period.
    /// </summary>
    /// <remarks>
    /// This property returns zero both when the property has been explicitly set to zero and when the period does not
    /// contain this property.
    /// </remarks>
    /// <value>The number of days within this period.</value>
    public int Days { get; }

    /// <summary>
    /// Gets the number of weeks within this period.
    /// </summary>
    /// <remarks>
    /// This property returns zero both when the property has been explicitly set to zero and when the period does not
    /// contain this property.
    /// </remarks>
    /// <value>The number of weeks within this period.</value>
    public int Weeks { get; }

    /// <summary>
    /// Gets the number of months within this period.
    /// </summary>
    /// <remarks>
    /// This property returns zero both when the property has been explicitly set to zero and when the period does not
    /// contain this property.
    /// </remarks>
    /// <value>The number of months within this period.</value>
    public int Months { get; }

    /// <summary>
    /// Gets the number of years within this period.
    /// </summary>
    /// <remarks>
    /// This property returns zero both when the property has been explicitly set to zero and when the period does not
    /// contain this property.
    /// </remarks>
    /// <value>The number of years within this period.</value>
    public int Years { get; }

    /// <summary>
    /// Gets a value indicating whether or not this period contains any non-zero-valued time-based properties (hours or lower).
    /// </summary>
    /// <value>true if the period contains any non-zero-valued time-based properties (hours or lower); false otherwise.</value>
    public bool HasTimeComponent => this.Hours != 0 || this.Minutes != 0 || this.Seconds != 0 || this.Milliseconds != 0 || this.Ticks != 0 || this.Nanoseconds != 0;

    /// <summary>
    /// Gets a value indicating whether or not this period contains any non-zero date-based properties (days or higher).
    /// </summary>
    /// <value>true if this period contains any non-zero date-based properties (days or higher); false otherwise.</value>
    public bool HasDateComponent => this.Years != 0 || this.Months != 0 || this.Weeks != 0 || this.Days != 0;

    /// <summary>
    /// Gets the total number of nanoseconds duration for the 'standard' properties (all bar years and months).
    /// </summary>
    /// <value>The total number of nanoseconds duration for the 'standard' properties (all bar years and months).</value>
    private long TotalNanoseconds =>
        this.Nanoseconds +
            (this.Ticks * NanosecondsPerTick) +
            (this.Milliseconds * NanosecondsPerMillisecond) +
            (this.Seconds * NanosecondsPerSecond) +
            (this.Minutes * NanosecondsPerMinute) +
            (this.Hours * NanosecondsPerHour) +
            (this.Days * NanosecondsPerDay) +
            (this.Weeks * NanosecondsPerWeek);

    /// <summary>
    /// Convert to a NodaTime.Period.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator NodaTime.Period(in Period value)
    {
        NodaTime.PeriodBuilder builder = new();
        builder[PeriodUnits.Years] = value.Years;
        builder[PeriodUnits.Months] = value.Months;
        builder[PeriodUnits.Weeks] = value.Weeks;
        builder[PeriodUnits.Days] = value.Days;
        builder[PeriodUnits.Hours] = value.Hours;
        builder[PeriodUnits.Minutes] = value.Minutes;
        builder[PeriodUnits.Seconds] = value.Seconds;
        builder[PeriodUnits.Milliseconds] = value.Milliseconds;
        builder[PeriodUnits.Ticks] = value.Ticks;
        builder[PeriodUnits.Nanoseconds] = value.Nanoseconds;
        return builder.Build();
    }

    /// <summary>
    /// Convert to a NodaTime.Period.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator Period(NodaTime.Period value)
    {
        return new Period(
            value.Years,
            value.Months,
            value.Weeks,
            value.Days,
            value.Hours,
            value.Minutes,
            value.Seconds,
            value.Milliseconds,
            value.Ticks,
            value.Nanoseconds);
    }

    /// <summary>
    /// Adds two periods together, by simply adding the values for each property.
    /// </summary>
    /// <param name="left">The first period to add.</param>
    /// <param name="right">The second period to add.</param>
    /// <returns>The sum of the two periods. The units of the result will be the union of those in both
    /// periods.</returns>
    public static Period operator +(Period left, Period right)
    {
        return new Period(
            left.Years + right.Years,
            left.Months + right.Months,
            left.Weeks + right.Weeks,
            left.Days + right.Days,
            left.Hours + right.Hours,
            left.Minutes + right.Minutes,
            left.Seconds + right.Seconds,
            left.Milliseconds + right.Milliseconds,
            left.Ticks + right.Ticks,
            left.Nanoseconds + right.Nanoseconds);
    }

    /// <summary>
    /// Subtracts one period from another, by simply subtracting each property value.
    /// </summary>
    /// <param name="minuend">The period to subtract the second operand from.</param>
    /// <param name="subtrahend">The period to subtract the first operand from.</param>
    /// <returns>The result of subtracting all the values in the second operand from the values in the first. The
    /// units of the result will be the union of both periods, even if the subtraction caused some properties to
    /// become zero (so "2 weeks, 1 days" minus "2 weeks" is "zero weeks, 1 days", not "1 days").</returns>
    public static Period operator -(Period minuend, Period subtrahend)
    {
        return new Period(
            minuend.Years - subtrahend.Years,
            minuend.Months - subtrahend.Months,
            minuend.Weeks - subtrahend.Weeks,
            minuend.Days - subtrahend.Days,
            minuend.Hours - subtrahend.Hours,
            minuend.Minutes - subtrahend.Minutes,
            minuend.Seconds - subtrahend.Seconds,
            minuend.Milliseconds - subtrahend.Milliseconds,
            minuend.Ticks - subtrahend.Ticks,
            minuend.Nanoseconds - subtrahend.Nanoseconds);
    }

    /// <summary>
    /// Implements the operator == (equality).
    /// See the type documentation for a description of equality semantics.
    /// </summary>
    /// <param name="left">The left hand side of the operator.</param>
    /// <param name="right">The right hand side of the operator.</param>
    /// <returns><c>true</c> if values are equal to each other, otherwise <c>false</c>.</returns>
    public static bool operator ==(Period left, Period right) => left.Equals(right);

    /// <summary>
    /// Implements the operator != (inequality).
    /// See the type documentation for a description of equality semantics.
    /// </summary>
    /// <param name="left">The left hand side of the operator.</param>
    /// <param name="right">The right hand side of the operator.</param>
    /// <returns><c>true</c> if values are not equal to each other, otherwise <c>false</c>.</returns>
    public static bool operator !=(Period left, Period right) => !(left == right);

    /// <summary>
    /// Parses a string into a Period.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The resulting period.</param>
    /// <returns><see langword="true"/> if the period could be parsed from the string.</returns>
    public static bool TryParse(ReadOnlySpan<char> value, [NotNullWhen(true)] out Period result)
    {
        if (PeriodParser(value, out PeriodBuilder builder))
        {
            result = builder.BuildPeriod();
            return true;
        }

        result = Zero;
        return false;
    }

    /// <summary>
    /// Parses a string into a Period.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The resulting period.</returns>
    public static Period Parse(ReadOnlySpan<char> value)
    {
        if (TryParse(value, out Period result))
        {
            return result;
        }

        throw new InvalidOperationException($"Unable to parse a period from the string '{value}'");
    }

    /// <summary>
    /// Creates a period representing the specified number of years.
    /// </summary>
    /// <param name="years">The number of years in the new period.</param>
    /// <returns>A period consisting of the given number of years.</returns>
    public static Period FromYears(int years) => new(years, 0, 0, 0);

    /// <summary>
    /// Creates a period representing the specified number of months.
    /// </summary>
    /// <param name="months">The number of months in the new period.</param>
    /// <returns>A period consisting of the given number of months.</returns>
    public static Period FromMonths(int months) => new(0, months, 0, 0);

    /// <summary>
    /// Creates a period representing the specified number of weeks.
    /// </summary>
    /// <param name="weeks">The number of weeks in the new period.</param>
    /// <returns>A period consisting of the given number of weeks.</returns>
    public static Period FromWeeks(int weeks) => new(0, 0, weeks, 0);

    /// <summary>
    /// Creates a period representing the specified number of days.
    /// </summary>
    /// <param name="days">The number of days in the new period.</param>
    /// <returns>A period consisting of the given number of days.</returns>
    public static Period FromDays(int days) => new(0, 0, 0, days);

    /// <summary>
    /// Creates a period representing the specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours in the new period.</param>
    /// <returns>A period consisting of the given number of hours.</returns>
    public static Period FromHours(long hours) => new(hours, 0L, 0L, 0L, 0L, 0L);

    /// <summary>
    /// Creates a period representing the specified number of minutes.
    /// </summary>
    /// <param name="minutes">The number of minutes in the new period.</param>
    /// <returns>A period consisting of the given number of minutes.</returns>
    public static Period FromMinutes(long minutes) => new(0L, minutes, 0L, 0L, 0L, 0L);

    /// <summary>
    /// Creates a period representing the specified number of seconds.
    /// </summary>
    /// <param name="seconds">The number of seconds in the new period.</param>
    /// <returns>A period consisting of the given number of seconds.</returns>
    public static Period FromSeconds(long seconds) => new(0L, 0L, seconds, 0L, 0L, 0L);

    /// <summary>
    /// Creates a period representing the specified number of milliseconds.
    /// </summary>
    /// <param name="milliseconds">The number of milliseconds in the new period.</param>
    /// <returns>A period consisting of the given number of milliseconds.</returns>
    public static Period FromMilliseconds(long milliseconds) => new(0L, 0L, 0L, milliseconds, 0L, 0L);

    /// <summary>
    /// Creates a period representing the specified number of ticks.
    /// </summary>
    /// <param name="ticks">The number of ticks in the new period.</param>
    /// <returns>A period consisting of the given number of ticks.</returns>
    public static Period FromTicks(long ticks) => new(0L, 0L, 0L, 0L, ticks, 0L);

    /// <summary>
    /// Creates a period representing the specified number of nanoseconds.
    /// </summary>
    /// <param name="nanoseconds">The number of nanoseconds in the new period.</param>
    /// <returns>A period consisting of the given number of nanoseconds.</returns>
    public static Period FromNanoseconds(long nanoseconds) => new(0L, 0L, 0L, 0L, 0L, nanoseconds);

    /// <summary>
    /// Adds two periods together, by simply adding the values for each property.
    /// </summary>
    /// <param name="left">The first period to add.</param>
    /// <param name="right">The second period to add.</param>
    /// <returns>The sum of the two periods. The units of the result will be the union of those in both
    /// periods.</returns>
    public static Period Add(Period left, Period right) => left + right;

    /// <summary>
    /// Creates an <see cref="IComparer{T}"/> for periods, using the given "base" local date/time.
    /// </summary>
    /// <remarks>
    /// Certain periods can't naturally be compared without more context - how "one month" compares to
    /// "30 days" depends on where you start. In order to compare two periods, the returned comparer
    /// effectively adds both periods to the "base" specified by <paramref name="baseDateTime"/> and compares
    /// the results. In some cases this arithmetic isn't actually required - when two periods can be
    /// converted to durations, the comparer uses that conversion for efficiency.
    /// </remarks>
    /// <param name="baseDateTime">The base local date/time to use for comparisons.</param>
    /// <returns>The new comparer.</returns>
    public static IComparer<Period> CreateComparer(LocalDateTime baseDateTime) => new PeriodComparer(baseDateTime);

    /// <summary>
    /// Subtracts one period from another, by simply subtracting each property value.
    /// </summary>
    /// <param name="minuend">The period to subtract the second operand from.</param>
    /// <param name="subtrahend">The period to subtract the first operand from.</param>
    /// <returns>The result of subtracting all the values in the second operand from the values in the first. The
    /// units of the result will be the union of both periods, even if the subtraction caused some properties to
    /// become zero (so "2 weeks, 1 days" minus "2 weeks" is "zero weeks, 1 days", not "1 days").</returns>
    public static Period Subtract(Period minuend, Period subtrahend) => minuend - subtrahend;

    /// <summary>
    /// Returns the number of days between two <see cref="LocalDate"/> objects.
    /// </summary>
    /// <param name="start">Start date/time.</param>
    /// <param name="end">End date/time.</param>
    /// <exception cref="ArgumentException"><paramref name="start"/> and <paramref name="end"/> use different calendars.</exception>
    /// <returns>The number of days between the given dates.</returns>
    public static int DaysBetween(LocalDate start, LocalDate end)
    {
        return NodaTime.Period.DaysBetween(start, end);
    }

    /// <summary>
    /// For periods that do not contain a non-zero number of years or months, returns a duration for this period
    /// assuming a standard 7-day week, 24-hour day, 60-minute hour etc.
    /// </summary>
    /// <exception cref="InvalidOperationException">The month or year property in the period is non-zero.</exception>
    /// <exception cref="OverflowException">The period doesn't have years or months, but the calculation
    /// overflows the bounds of <see cref="Duration"/>. In some cases this may occur even though the theoretical
    /// result would be valid due to balancing positive and negative values, but for simplicity there is
    /// no attempt to work around this - in realistic periods, it shouldn't be a problem.</exception>
    /// <returns>The duration of the period.</returns>
    [Pure]
    public Duration ToDuration()
    {
        if (this.Months != 0 || this.Years != 0)
        {
            throw new InvalidOperationException("Cannot construct duration of period with non-zero months or years.");
        }

        return Duration.FromNanoseconds(this.TotalNanoseconds);
    }

    /// <summary>
    /// Returns a normalized version of this period, such that equivalent (but potentially non-equal) periods are
    /// changed to the same representation.
    /// </summary>
    /// <remarks>
    /// Months and years are unchanged
    /// (as they can vary in length), but weeks are multiplied by 7 and added to the
    /// Days property, and all time properties are normalized to their natural range.
    /// Subsecond values are normalized to millisecond and "nanosecond within millisecond" values.
    /// So for example, a period of 25 hours becomes a period of 1 day
    /// and 1 hour. A period of 1,500,750,000 nanoseconds becomes 1 second, 500 milliseconds and
    /// 750,000 nanoseconds. Aside from months and years, either all the properties
    /// end up positive, or they all end up negative. "Week" and "tick" units in the returned period are always 0.
    /// </remarks>
    /// <exception cref="OverflowException">The period doesn't have years or months, but it contains more than
    /// <see cref="long.MaxValue"/> nanoseconds when the combined weeks/days/time portions are considered. This is
    /// over 292 years, so unlikely to be a problem in normal usage.
    /// In some cases this may occur even though the theoretical result would be valid due to balancing positive and
    /// negative values, but for simplicity there is no attempt to work around this.</exception>
    /// <returns>The normalized period.</returns>
    /// <seealso cref="NormalizingEqualityComparer"/>
    [Pure]
    public Period Normalize()
    {
        // Simplest way to normalize: grab all the fields up to "week" and
        // sum them.
        long totalNanoseconds = this.TotalNanoseconds;
        int days = (int)(totalNanoseconds / NanosecondsPerDay);
        long hours = (totalNanoseconds / NanosecondsPerHour) % HoursPerDay;
        long minutes = (totalNanoseconds / NanosecondsPerMinute) % MinutesPerHour;
        long seconds = (totalNanoseconds / NanosecondsPerSecond) % SecondsPerMinute;
        long milliseconds = (totalNanoseconds / NanosecondsPerMillisecond) % MillisecondsPerSecond;
        long nanoseconds = totalNanoseconds % NanosecondsPerMillisecond;

        return new Period(this.Years, this.Months, 0 /* weeks */, days, hours, minutes, seconds, milliseconds, 0 /* ticks */, nanoseconds);
    }

    /// <summary>
    /// Returns this string formatted according to the ISO8601 duration specification used by JSON schema.
    /// </summary>
    /// <returns>A formatted representation of this period.</returns>
    public override string ToString()
    {
        return NodaTime.Text.PeriodPattern.NormalizingIso.Format(this);
    }

    /// <summary>
    /// Compares the given object for equality with this one, as per <see cref="Equals(Period)"/>.
    /// See the type documentation for a description of equality semantics.
    /// </summary>
    /// <param name="other">The value to compare this one with.</param>
    /// <returns>true if the other object is a period equal to this one, consistent with <see cref="Equals(Period)"/>.</returns>
    public override bool Equals(object? other) => (other is Period p && this.Equals(p)) || (other is NodaTime.Period p1 && this.Equals(p1));

    /// <summary>
    /// Returns the hash code for this period, consistent with <see cref="Equals(Period)"/>.
    /// See the type documentation for a description of equality semantics.
    /// </summary>
    /// <returns>The hash code for this period.</returns>
    public override int GetHashCode()
    {
        HashCode hc = default;
        hc.Add(this.Years);
        hc.Add(this.Months);
        hc.Add(this.Weeks);
        hc.Add(this.Days);
        hc.Add(this.Hours);
        hc.Add(this.Minutes);
        hc.Add(this.Seconds);
        hc.Add(this.Milliseconds);
        hc.Add(this.Ticks);
        hc.Add(this.Nanoseconds);
        return hc.ToHashCode();
    }

    /// <summary>
    /// Compares the given period for equality with this one.
    /// See the type documentation for a description of equality semantics.
    /// </summary>
    /// <param name="other">The period to compare this one with.</param>
    /// <returns>True if this period has the same values for the same properties as the one specified.</returns>
    public bool Equals(Period other) =>
        this.Years == other.Years &&
        this.Months == other.Months &&
        this.Weeks == other.Weeks &&
        this.Days == other.Days &&
        this.Hours == other.Hours &&
        this.Minutes == other.Minutes &&
        this.Seconds == other.Seconds &&
        this.Milliseconds == other.Milliseconds &&
        this.Ticks == other.Ticks &&
        this.Nanoseconds == other.Nanoseconds;

    /// <summary>
    /// Compares the given period for equality with this one.
    /// See the type documentation for a description of equality semantics.
    /// </summary>
    /// <param name="other">The period to compare this one with.</param>
    /// <returns>True if this period has the same values for the same properties as the one specified.</returns>
    public bool Equals(NodaTime.Period other) =>
        this.Years == other.Years &&
        this.Months == other.Months &&
        this.Weeks == other.Weeks &&
        this.Days == other.Days &&
        this.Hours == other.Hours &&
        this.Minutes == other.Minutes &&
        this.Seconds == other.Seconds &&
        this.Milliseconds == other.Milliseconds &&
        this.Ticks == other.Ticks &&
        this.Nanoseconds == other.Nanoseconds;

    /// <summary>
    /// A parser for a json period.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="builder">The resulting period builder.</param>
    /// <returns>A period builder parsed from the read only span.</returns>
    internal static bool PeriodParser(ReadOnlySpan<char> text, out PeriodBuilder builder)
    {
        builder = default;

        if (text.Length == 0)
        {
            return false;
        }

        ValueCursor valueCursor = new(text);
        valueCursor.MoveNext();
        if (valueCursor.Current != 'P')
        {
            return false;
        }

        bool inDate = true;
        PeriodUnits unitsSoFar = 0;
        while (valueCursor.MoveNext())
        {
            if (inDate && (valueCursor.Current == 'T' || valueCursor.Current == 't'))
            {
                inDate = false;
                continue;
            }

            bool negative = valueCursor.Current == '-';
            if (!valueCursor.ParseInt64(out long unitValue))
            {
                return false;
            }

            if (valueCursor.Length == valueCursor.Index)
            {
                return false;
            }

            // Various failure cases:
            // - Repeated unit (e.g. P1M2M)
            // - Time unit is in date part (e.g. P5M)
            // - Date unit is in time part (e.g. PT1D)
            // - Unit is in incorrect order (e.g. P5D1Y)
            // - Unit is invalid (e.g. P5J)
            // - Unit is missing (e.g. P5)
            PeriodUnits unit;
            switch (valueCursor.Current)
            {
                case 'Y': unit = PeriodUnits.Years; break;
                case 'M': unit = inDate ? PeriodUnits.Months : PeriodUnits.Minutes; break;
                case 'W': unit = PeriodUnits.Weeks; break;
                case 'D': unit = PeriodUnits.Days; break;
                case 'H': unit = PeriodUnits.Hours; break;
                case 'S': unit = PeriodUnits.Seconds; break;
                case ',':
                case '.': unit = PeriodUnits.Nanoseconds; break; // Special handling below
                default: return false;
            }

            if ((unit & unitsSoFar) != 0)
            {
                return false;
            }

            // This handles putting months before years, for example. Less significant units
            // have higher integer representations.
            if (unit < unitsSoFar)
            {
                return false;
            }

            // The result of checking "there aren't any time units in this unit" should be
            // equal to "we're still in the date part".
            if ((unit & PeriodUnits.AllTimeUnits) == 0 != inDate)
            {
                return false;
            }

            // Seen a . or , which need special handling.
            if (unit == PeriodUnits.Nanoseconds)
            {
                // Check for already having seen seconds, e.g. PT5S0.5
                if ((unitsSoFar & PeriodUnits.Seconds) != 0)
                {
                    return false;
                }

                builder.Seconds = unitValue;

                if (!valueCursor.MoveNext())
                {
                    return false;
                }

                // Can cope with at most 999999999 nanoseconds
                if (!valueCursor.ParseFraction(9, 9, out int totalNanoseconds, 1))
                {
                    return false;
                }

                // Use whether or not the seconds value was negative (even if 0)
                // as the indication of whether this value is negative.
                if (negative)
                {
                    totalNanoseconds = -totalNanoseconds;
                }

                builder.Milliseconds = (totalNanoseconds / NodaConstants.NanosecondsPerMillisecond) % NodaConstants.MillisecondsPerSecond;
                builder.Ticks = (totalNanoseconds / NodaConstants.NanosecondsPerTick) % NodaConstants.TicksPerMillisecond;
                builder.Nanoseconds = totalNanoseconds % NodaConstants.NanosecondsPerTick;

                if (valueCursor.Current != 'S')
                {
                    return false;
                }

                if (valueCursor.MoveNext())
                {
                    return false;
                }

                return false;
            }

            builder[unit] = unitValue;
            unitsSoFar |= unit;
        }

        return unitsSoFar != 0;
    }

    /// <summary>
    /// Equality comparer which simply normalizes periods before comparing them.
    /// </summary>
    private sealed class NormalizingPeriodEqualityComparer : EqualityComparer<Period>
    {
        internal static readonly NormalizingPeriodEqualityComparer Instance = new();

        private NormalizingPeriodEqualityComparer()
        {
        }

        public override bool Equals(Period x, Period y)
        {
            return x.Normalize().Equals(y.Normalize());
        }

        public override int GetHashCode(Period period) =>
            period.Normalize().GetHashCode();
    }

    private sealed class PeriodComparer : Comparer<Period>
    {
        private readonly LocalDateTime baseDateTime;

        internal PeriodComparer(LocalDateTime baseDateTime)
        {
            this.baseDateTime = baseDateTime;
        }

        public override int Compare(Period x, Period y)
        {
            if (x.Months == 0 && y.Months == 0 &&
                x.Years == 0 && y.Years == 0)
            {
                // Note: this *could* throw an OverflowException when the normal approach
                // wouldn't, but it's highly unlikely
                return x.ToDuration().CompareTo(y.ToDuration());
            }

            // We have no access to the internals of period to let
            // us do this without a bunch of interim steps.
            return this.baseDateTime
                    .PlusYears(x.Years)
                    .PlusMonths(x.Months)
                    .PlusWeeks(x.Weeks)
                    .PlusDays(x.Days)
                    .PlusHours(x.Hours)
                    .PlusMinutes(x.Minutes)
                    .PlusSeconds(x.Seconds)
                    .PlusMilliseconds(x.Milliseconds)
                    .PlusTicks(x.Ticks)
                    .PlusNanoseconds(x.Nanoseconds)
                .CompareTo(
                   this.baseDateTime
                    .PlusYears(y.Years)
                    .PlusMonths(y.Months)
                    .PlusWeeks(y.Weeks)
                    .PlusDays(y.Days)
                    .PlusHours(y.Hours)
                    .PlusMinutes(y.Minutes)
                    .PlusSeconds(y.Seconds)
                    .PlusMilliseconds(y.Milliseconds)
                    .PlusTicks(y.Ticks)
                    .PlusNanoseconds(y.Nanoseconds));
        }
    }
}