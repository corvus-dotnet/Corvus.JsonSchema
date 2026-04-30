// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

// Derived from code:
// Copyright 2010 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

namespace Corvus.Text.Json.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NodaTime;
using NodaTime.Text;
using Xunit;

public class PeriodTest
{
    private static readonly PeriodUnits[] s_allPeriodUnits = (PeriodUnits[])Enum.GetValues(typeof(PeriodUnits));

    public static IEnumerable<object[]> AllPeriodUnitsData => s_allPeriodUnits.Select(unit => new object[] { unit });

    [Theory]
    [InlineData("2016-05-16", "2016-07-13", 58)]
    public void BetweenLocalDates_SingleUnit(string startText, string endText, int expectedValue)
    {
        LocalDate start = LocalDatePattern.Iso.Parse(startText).Value;
        LocalDate end = LocalDatePattern.Iso.Parse(endText).Value;
        int actual = Corvus.Text.Json.Period.DaysBetween(start, end);
        Assert.Equal(expectedValue, actual);
    }

    [Fact]
    public void DaysBetweenLocalDates_DifferentCalendarsThrows()
    {
        var start = new LocalDate(2020, 6, 13, CalendarSystem.Iso);
        var end = new LocalDate(2020, 6, 15, CalendarSystem.Julian);
        Assert.Throws<ArgumentException>(() => Corvus.Text.Json.Period.DaysBetween(start, end));
    }

    [Fact]
    public void DaysBetweenLocalDates_SameDatesReturnsZero()
    {
        var start = new LocalDate(2020, 6, 13, CalendarSystem.Iso);
        LocalDate end = start;
        int expected = 0;
        int actual = Corvus.Text.Json.Period.DaysBetween(start, end);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("2016-05-16", "2016-05-17", 1)]
    [InlineData("2020-06-15", "2020-06-18", 3)]
    [InlineData("2016-05-16", "2016-05-26", 10)]
    [InlineData("2020-06-15", "2021-06-19", 369)]
    [InlineData("2020-03-23", "2020-06-12", 81)]
    public void DaysBetweenLocalDates(string startText, string endText, int expected)
    {
        LocalDate start = LocalDatePattern.Iso.Parse(startText).Value;
        LocalDate end = LocalDatePattern.Iso.Parse(endText).Value;
        int actual = Corvus.Text.Json.Period.DaysBetween(start, end);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("2016-05-16", "2016-05-15", -1)]
    [InlineData("2020-06-15", "2020-06-12", -3)]
    [InlineData("2016-05-16", "2016-05-06", -10)]
    [InlineData("2021-06-19", "2020-06-15", -369)]
    [InlineData("2020-05-16", "2019-05-16", -366)]
    public void DaysBetweenLocalDates_StartDateGreaterThanEndDate(string startText, string endText, int expected)
    {
        LocalDate start = LocalDatePattern.Iso.Parse(startText).Value;
        LocalDate end = LocalDatePattern.Iso.Parse(endText).Value;
        int actual = Corvus.Text.Json.Period.DaysBetween(start, end);
        Assert.Equal(expected, actual);
    }

    public static IEnumerable<object[]> NanosecondsBetweenLocalTimesTestCaseData =>
    [
        [LocalTime.MinValue, LocalTime.MaxValue, LocalTime.MaxValue.NanosecondOfDay],
        [LocalTime.MaxValue, LocalTime.MinValue, -LocalTime.MaxValue.NanosecondOfDay],
        [LocalTime.MinValue, LocalTime.MinValue.PlusNanoseconds(1), 1L],
        [LocalTime.MinValue.PlusNanoseconds(1), LocalTime.MinValue, -1L]
    ];

    [Fact]
    public void Addition_WithDifferentPeriodTypes()
    {
        var p1 = Corvus.Text.Json.Period.FromHours(3);
        var p2 = Corvus.Text.Json.Period.FromMinutes(20);
        Corvus.Text.Json.Period sum = p1 + p2;
        Assert.Equal(3, sum.Hours);
        Assert.Equal(20, sum.Minutes);
    }

    [Fact]
    public void Addition_MaxAndMinValue()
    {
        Corvus.Text.Json.Period p1 = Corvus.Text.Json.Period.MaxValue;
        Corvus.Text.Json.Period p2 = Corvus.Text.Json.Period.MinValue;
        Corvus.Text.Json.Period sum = p1 + p2;
        Assert.Equal(new Corvus.Text.Json.Period(-1, -1, -1, -1, -1, -1, -1, -1, -1, -1), sum);
    }

    [Fact]
    public void Addition_WithIdenticalPeriodTypes()
    {
        var p1 = Corvus.Text.Json.Period.FromHours(3);
        var p2 = Corvus.Text.Json.Period.FromHours(2);
        Corvus.Text.Json.Period sum = p1 + p2;
        Assert.Equal(5, sum.Hours);
        Assert.Equal(sum, Corvus.Text.Json.Period.Add(p1, p2));
    }

    [Fact]
    public void Addition_DayCrossingMonthBoundary()
    {
        var start = new LocalDateTime(2010, 2, 20, 10, 0);
        LocalDateTime result = start + Corvus.Text.Json.Period.FromDays(10);
        Assert.Equal(new LocalDateTime(2010, 3, 2, 10, 0), result);
    }

    [Fact]
    public void Addition_OneYearOnLeapDay()
    {
        var start = new LocalDateTime(2012, 2, 29, 10, 0);
        LocalDateTime result = start + Corvus.Text.Json.Period.FromYears(1);
        // Feb 29th becomes Feb 28th
        Assert.Equal(new LocalDateTime(2013, 2, 28, 10, 0), result);
    }

    [Fact]
    public void Addition_FourYearsOnLeapDay()
    {
        var start = new LocalDateTime(2012, 2, 29, 10, 0);
        LocalDateTime result = start + Corvus.Text.Json.Period.FromYears(4);
        // Feb 29th is still valid in 2016
        Assert.Equal(new LocalDateTime(2016, 2, 29, 10, 0), result);
    }

    [Fact]
    public void Addition_YearMonthDay()
    {
        // One year, one month, two days
        Corvus.Text.Json.Period period = Corvus.Text.Json.Period.FromYears(1) + Corvus.Text.Json.Period.FromMonths(1) + Corvus.Text.Json.Period.FromDays(2);
        var start = new LocalDateTime(2007, 1, 30, 0, 0);
        // Corvus.Text.Json.Periods are added in order, so this becomes...
        // Add one year: Jan 30th 2008
        // Add one month: Feb 29th 2008
        // Add two days: March 2nd 2008
        // If we added the days first, we'd end up with March 1st instead.
        LocalDateTime result = start + period;
        Assert.Equal(new LocalDateTime(2008, 3, 2, 0, 0), result);
    }

    [Fact]
    public void Subtraction_WithDifferentPeriodTypes()
    {
        var p1 = Corvus.Text.Json.Period.FromHours(3);
        var p2 = Corvus.Text.Json.Period.FromMinutes(20);
        Corvus.Text.Json.Period difference = p1 - p2;
        Assert.Equal(3, difference.Hours);
        Assert.Equal(-20, difference.Minutes);
        Assert.Equal(difference, Corvus.Text.Json.Period.Subtract(p1, p2));
    }

    [Fact]
    public void Subtraction_WithIdenticalPeriodTypes()
    {
        var p1 = Corvus.Text.Json.Period.FromHours(3);
        var p2 = Corvus.Text.Json.Period.FromHours(2);
        Corvus.Text.Json.Period difference = p1 - p2;
        Assert.Equal(1, difference.Hours);
        Assert.Equal(difference, Corvus.Text.Json.Period.Subtract(p1, p2));
    }

    [Fact]
    public void Equality_WhenEqual()
    {
        Assert.Equal(Corvus.Text.Json.Period.FromHours(10), Corvus.Text.Json.Period.FromHours(10));
        Assert.Equal(Corvus.Text.Json.Period.FromMinutes(15), Corvus.Text.Json.Period.FromMinutes(15));
        Assert.Equal(Corvus.Text.Json.Period.FromDays(5), Corvus.Text.Json.Period.FromDays(5));
    }

    [Fact]
    public void Equality_WithDifferentPeriodTypes_OnlyConsidersValues()
    {
        Corvus.Text.Json.Period allFields = Corvus.Text.Json.Period.FromMinutes(1) + Corvus.Text.Json.Period.FromHours(1) - Corvus.Text.Json.Period.FromMinutes(1);
        var justHours = Corvus.Text.Json.Period.FromHours(1);
        Assert.Equal(allFields, justHours);
    }

    [Fact]
    public void Equality_WhenUnequal()
    {
        Assert.False(Corvus.Text.Json.Period.FromHours(10).Equals(Corvus.Text.Json.Period.FromHours(20)));
        Assert.False(Corvus.Text.Json.Period.FromMinutes(15).Equals(Corvus.Text.Json.Period.FromSeconds(15)));
        Assert.False(Corvus.Text.Json.Period.FromHours(1).Equals(Corvus.Text.Json.Period.FromMinutes(60)));
        Assert.False(Corvus.Text.Json.Period.FromHours(1).Equals(new object()));
        Assert.False(Corvus.Text.Json.Period.FromHours(1).Equals((object?)null));
    }

    [Fact]
    public void EqualityOperators()
    {
        var val1 = Corvus.Text.Json.Period.FromHours(1);
        var val2 = Corvus.Text.Json.Period.FromHours(1);
        var val3 = Corvus.Text.Json.Period.FromHours(2);
        Corvus.Text.Json.Period? val4 = null;
        Assert.True(val1 == val2);
        Assert.False(val1 == val3);
        Assert.False(val1 == val4);
        Assert.False(val4 == val1);
        Assert.True(val4 == null);
        Assert.True(null == val4);

        Assert.False(val1 != val2);
        Assert.True(val1 != val3);
        Assert.True(val1 != val4);
        Assert.True(val4 != val1);
        Assert.False(val4 != null);
        Assert.False(null != val4);
    }

    [Theory]
    [InlineData(PeriodUnits.Years, false)]
    [InlineData(PeriodUnits.Weeks, false)]
    [InlineData(PeriodUnits.Months, false)]
    [InlineData(PeriodUnits.Days, false)]
    [InlineData(PeriodUnits.Hours, true)]
    [InlineData(PeriodUnits.Minutes, true)]
    [InlineData(PeriodUnits.Seconds, true)]
    [InlineData(PeriodUnits.Milliseconds, true)]
    [InlineData(PeriodUnits.Ticks, true)]
    [InlineData(PeriodUnits.Nanoseconds, true)]
    public void HasTimeComponent_SingleValued(PeriodUnits unit, bool hasTimeComponent)
    {
        Json.Period period = new Corvus.Text.Json.PeriodBuilder { [unit] = 1 }.BuildPeriod();
        Assert.Equal(hasTimeComponent, period.HasTimeComponent);
    }

    [Theory]
    [InlineData(PeriodUnits.Years, true)]
    [InlineData(PeriodUnits.Weeks, true)]
    [InlineData(PeriodUnits.Months, true)]
    [InlineData(PeriodUnits.Days, true)]
    [InlineData(PeriodUnits.Hours, false)]
    [InlineData(PeriodUnits.Minutes, false)]
    [InlineData(PeriodUnits.Seconds, false)]
    [InlineData(PeriodUnits.Milliseconds, false)]
    [InlineData(PeriodUnits.Ticks, false)]
    [InlineData(PeriodUnits.Nanoseconds, false)]
    public void HasDateComponent_SingleValued(PeriodUnits unit, bool hasDateComponent)
    {
        Json.Period period = new Corvus.Text.Json.PeriodBuilder { [unit] = 1 }.BuildPeriod();
        Assert.Equal(hasDateComponent, period.HasDateComponent);
    }

    [Fact]
    public void ToString_Positive()
    {
        Corvus.Text.Json.Period period = Corvus.Text.Json.Period.FromDays(1) + Corvus.Text.Json.Period.FromHours(2);
        Assert.Equal("P1DT2H", period.ToString());
    }

    [Fact]
    public void ToString_Negative()
    {
        Corvus.Text.Json.Period period = Corvus.Text.Json.Period.FromDays(-1) + Corvus.Text.Json.Period.FromHours(-2);
        Assert.Equal("P-1DT-2H", period.ToString());
    }

    [Fact]
    public void ToString_Mixed()
    {
        Corvus.Text.Json.Period period = Corvus.Text.Json.Period.FromDays(-1) + Corvus.Text.Json.Period.FromHours(2);
        Assert.Equal("PT-22H", period.ToString());
    }

    [Fact]
    public void ToString_Zero()
    {
        Assert.Equal("P0D", Corvus.Text.Json.Period.Zero.ToString());
    }

    [Fact]
    public void Normalize_Weeks()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Weeks = 2, Days = 5 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Days = 19 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_Hours()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = 25, Days = 1 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = 1, Days = 2 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_Minutes()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = 1, Minutes = 150 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = 3, Minutes = 30 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }


    [Fact]
    public void Normalize_Seconds()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Minutes = 1, Seconds = 150 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Minutes = 3, Seconds = 30 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_Milliseconds()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Seconds = 1, Milliseconds = 1500 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Seconds = 2, Milliseconds = 500 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_Ticks()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Milliseconds = 1, Ticks = 15000 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Milliseconds = 2, Ticks = 0, Nanoseconds = 500000 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_Nanoseconds()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Ticks = 1, Nanoseconds = 150 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Nanoseconds = 250 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_MultipleFields()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = 1, Minutes = 119, Seconds = 150 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = 3, Minutes = 1, Seconds = 30 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_AllNegative()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = -1, Minutes = -119, Seconds = -150 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = -3, Minutes = -1, Seconds = -30 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_MixedSigns_PositiveResult()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = 3, Minutes = -1 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = 2, Minutes = 59 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_MixedSigns_NegativeResult()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = 1, Minutes = -121 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = -1, Minutes = -1 }.BuildPeriod();
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Normalize_DoesntAffectMonthsAndYears()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Years = 2, Months = 1, Days = 400 }.BuildPeriod();
        Assert.Equal(original, original.Normalize());
    }

    [Fact]
    public void Normalize_ZeroResult()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Years = 0 }.BuildPeriod();
        Assert.Equal(Corvus.Text.Json.Period.Zero, original.Normalize());
    }

    [Fact]
    public void ToString_SingleUnit()
    {
        var period = Corvus.Text.Json.Period.FromHours(5);
        Assert.Equal("PT5H", period.ToString());
    }

    [Fact]
    public void ToString_MultipleUnits()
    {
        Json.Period period = new Corvus.Text.Json.PeriodBuilder { Hours = 5, Minutes = 30 }.BuildPeriod();
        Assert.Equal("PT5H30M", period.ToString());
    }

    [Fact]
    public void ToDuration_InvalidWithYears()
    {
        var period = Corvus.Text.Json.Period.FromYears(1);
        Assert.Throws<InvalidOperationException>(() => period.ToDuration());
    }

    [Fact]
    public void ToDuration_InvalidWithMonths()
    {
        var period = Corvus.Text.Json.Period.FromMonths(1);
        Assert.Throws<InvalidOperationException>(() => period.ToDuration());
    }

    [Fact]
    public void ToDuration_ValidAllAcceptableUnits()
    {
        Corvus.Text.Json.Period period = new Corvus.Text.Json.PeriodBuilder
        {
            Weeks = 1,
            Days = 2,
            Hours = 3,
            Minutes = 4,
            Seconds = 5,
            Milliseconds = 6,
            Ticks = 7
        }.BuildPeriod();
        Assert.Equal(
            1 * NodaConstants.TicksPerWeek +
            2 * NodaConstants.TicksPerDay +
            3 * NodaConstants.TicksPerHour +
            4 * NodaConstants.TicksPerMinute +
            5 * NodaConstants.TicksPerSecond +
            6 * NodaConstants.TicksPerMillisecond + 7,
            period.ToDuration().BclCompatibleTicks);
    }

    [Fact]
    public void ToDuration_ValidWithZeroValuesInMonthYearUnits()
    {
        Corvus.Text.Json.Period period = Corvus.Text.Json.Period.FromMonths(1) + Corvus.Text.Json.Period.FromYears(1);
        period = period - period + Corvus.Text.Json.Period.FromDays(1);
        Assert.False(period.HasTimeComponent);
        Assert.Equal(Duration.FromDays(1), period.ToDuration());
    }

    [Fact]
    public void NormalizingEqualityComparer_PeriodToItself()
    {
        var period = Corvus.Text.Json.Period.FromYears(1);
        Assert.True(Corvus.Text.Json.Period.NormalizingEqualityComparer.Equals(period, period));
    }

    [Fact]
    public void NormalizingEqualityComparer_NonEqualAfterNormalization()
    {
        var period1 = Corvus.Text.Json.Period.FromHours(2);
        var period2 = Corvus.Text.Json.Period.FromMinutes(150);
        Assert.False(Corvus.Text.Json.Period.NormalizingEqualityComparer.Equals(period1, period2));
    }

    [Fact]
    public void NormalizingEqualityComparer_EqualAfterNormalization()
    {
        var period1 = Corvus.Text.Json.Period.FromHours(2);
        var period2 = Corvus.Text.Json.Period.FromMinutes(120);
        Assert.True(Corvus.Text.Json.Period.NormalizingEqualityComparer.Equals(period1, period2));
    }

    [Fact]
    public void NormalizingEqualityComparer_GetHashCodeAfterNormalization()
    {
        var period1 = Corvus.Text.Json.Period.FromHours(2);
        var period2 = Corvus.Text.Json.Period.FromMinutes(120);
        Assert.Equal(Corvus.Text.Json.Period.NormalizingEqualityComparer.GetHashCode(period1),
            Corvus.Text.Json.Period.NormalizingEqualityComparer.GetHashCode(period2));
    }

    [Fact]
    public void Comparer_DurationablePeriods()
    {
        var bigger = Corvus.Text.Json.Period.FromHours(25);
        var smaller = Corvus.Text.Json.Period.FromDays(1);
        IComparer<Json.Period> comparer = Corvus.Text.Json.Period.CreateComparer(new LocalDateTime(2000, 1, 1, 0, 0));
        Assert.True(comparer.Compare(bigger, smaller) > 0);
        Assert.True(comparer.Compare(smaller, bigger) < 0);
        Assert.Equal(0, comparer.Compare(bigger, bigger));
    }

    [Fact]
    public void Comparer_NonDurationablePeriods()
    {
        var month = Corvus.Text.Json.Period.FromMonths(1);
        var days = Corvus.Text.Json.Period.FromDays(30);
        // At the start of January, a month is longer than 30 days
        IComparer<Json.Period> januaryComparer = Corvus.Text.Json.Period.CreateComparer(new LocalDateTime(2000, 1, 1, 0, 0));
        Assert.True(januaryComparer.Compare(month, days) > 0);
        Assert.True(januaryComparer.Compare(days, month) < 0);
        Assert.Equal(0, januaryComparer.Compare(month, month));

        // At the start of February, a month is shorter than 30 days
        IComparer<Json.Period> februaryComparer = Corvus.Text.Json.Period.CreateComparer(new LocalDateTime(2000, 2, 1, 0, 0));
        Assert.True(februaryComparer.Compare(month, days) < 0);
        Assert.True(februaryComparer.Compare(days, month) > 0);
        Assert.Equal(0, februaryComparer.Compare(month, month));
    }

    public static IEnumerable<object[]> PeriodMaxAndMinValues =>
    [
        [Corvus.Text.Json.Period.MaxValue, int.MaxValue, long.MaxValue],
        [Corvus.Text.Json.Period.MinValue, int.MinValue, long.MinValue]
    ];

    /// <summary>
    /// Ensure that Corvus.Text.Json.Period.MaxValue and Corvus.Text.Json.Period.MinValue contain the max/min value assignable to each public property.
    /// </summary>
    [Theory]
    [MemberData(nameof(PeriodMaxAndMinValues))]
    public void Period_MaxAndMinValues_AllMembers(Corvus.Text.Json.Period period, int expectedIntValue, long expectedLongValue)
    {
        foreach (PropertyInfo property in typeof(Corvus.Text.Json.Period).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            object actualValue = property.GetValue(period);

            // HasTimeComponent is a public property that will get included in this loop.
            // This block allows us to ignore it.
            if (actualValue is bool)
            {
                return;
            }

            if (actualValue is int)
            {
                Assert.Equal(expectedIntValue, actualValue);
                return;
            }
            if (actualValue is long)
            {
                Assert.Equal(expectedLongValue, actualValue);
                return;
            }

            throw new InvalidOperationException($"Property {property.Name} has unexpected type {actualValue?.GetType().Name}.");
        }
    }

    [Fact]
    public void FromNanoseconds()
    {
        var period = Corvus.Text.Json.Period.FromNanoseconds(1234567890L);
        Assert.Equal(1234567890L, period.Nanoseconds);
    }
}