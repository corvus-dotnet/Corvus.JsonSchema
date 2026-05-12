// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

// Derived from code:
// Copyright 2010 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using System.Reflection;

namespace Corvus.Text.Json.Tests;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NodaTime;
using NodaTime.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class PeriodTest
{
    private static readonly PeriodUnits[] s_allPeriodUnits = (PeriodUnits[])Enum.GetValues(typeof(PeriodUnits));

    public static IEnumerable<object[]> AllPeriodUnitsData => s_allPeriodUnits.Select(unit => new object[] { unit });

    [TestMethod]
    [DataRow("2016-05-16", "2016-07-13", 58)]
    public void BetweenLocalDates_SingleUnit(string startText, string endText, int expectedValue)
    {
        LocalDate start = LocalDatePattern.Iso.Parse(startText).Value;
        LocalDate end = LocalDatePattern.Iso.Parse(endText).Value;
        int actual = Corvus.Text.Json.Period.DaysBetween(start, end);
        Assert.AreEqual(expectedValue, actual);
    }

    [TestMethod]
    public void DaysBetweenLocalDates_DifferentCalendarsThrows()
    {
        var start = new LocalDate(2020, 6, 13, CalendarSystem.Iso);
        var end = new LocalDate(2020, 6, 15, CalendarSystem.Julian);
        Assert.ThrowsExactly<ArgumentException>(() => Corvus.Text.Json.Period.DaysBetween(start, end));
    }

    [TestMethod]
    public void DaysBetweenLocalDates_SameDatesReturnsZero()
    {
        var start = new LocalDate(2020, 6, 13, CalendarSystem.Iso);
        LocalDate end = start;
        int expected = 0;
        int actual = Corvus.Text.Json.Period.DaysBetween(start, end);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("2016-05-16", "2016-05-17", 1)]
    [DataRow("2020-06-15", "2020-06-18", 3)]
    [DataRow("2016-05-16", "2016-05-26", 10)]
    [DataRow("2020-06-15", "2021-06-19", 369)]
    [DataRow("2020-03-23", "2020-06-12", 81)]
    public void DaysBetweenLocalDates(string startText, string endText, int expected)
    {
        LocalDate start = LocalDatePattern.Iso.Parse(startText).Value;
        LocalDate end = LocalDatePattern.Iso.Parse(endText).Value;
        int actual = Corvus.Text.Json.Period.DaysBetween(start, end);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("2016-05-16", "2016-05-15", -1)]
    [DataRow("2020-06-15", "2020-06-12", -3)]
    [DataRow("2016-05-16", "2016-05-06", -10)]
    [DataRow("2021-06-19", "2020-06-15", -369)]
    [DataRow("2020-05-16", "2019-05-16", -366)]
    public void DaysBetweenLocalDates_StartDateGreaterThanEndDate(string startText, string endText, int expected)
    {
        LocalDate start = LocalDatePattern.Iso.Parse(startText).Value;
        LocalDate end = LocalDatePattern.Iso.Parse(endText).Value;
        int actual = Corvus.Text.Json.Period.DaysBetween(start, end);
        Assert.AreEqual(expected, actual);
    }

    public static IEnumerable<object[]> NanosecondsBetweenLocalTimesTestCaseData =>
    [
        [LocalTime.MinValue, LocalTime.MaxValue, LocalTime.MaxValue.NanosecondOfDay],
        [LocalTime.MaxValue, LocalTime.MinValue, -LocalTime.MaxValue.NanosecondOfDay],
        [LocalTime.MinValue, LocalTime.MinValue.PlusNanoseconds(1), 1L],
        [LocalTime.MinValue.PlusNanoseconds(1), LocalTime.MinValue, -1L]
    ];

    [TestMethod]
    public void Addition_WithDifferentPeriodTypes()
    {
        var p1 = Corvus.Text.Json.Period.FromHours(3);
        var p2 = Corvus.Text.Json.Period.FromMinutes(20);
        Corvus.Text.Json.Period sum = p1 + p2;
        Assert.AreEqual(3, sum.Hours);
        Assert.AreEqual(20, sum.Minutes);
    }

    [TestMethod]
    public void Addition_MaxAndMinValue()
    {
        Corvus.Text.Json.Period p1 = Corvus.Text.Json.Period.MaxValue;
        Corvus.Text.Json.Period p2 = Corvus.Text.Json.Period.MinValue;
        Corvus.Text.Json.Period sum = p1 + p2;
        Assert.AreEqual(new Corvus.Text.Json.Period(-1, -1, -1, -1, -1, -1, -1, -1, -1, -1), sum);
    }

    [TestMethod]
    public void Addition_WithIdenticalPeriodTypes()
    {
        var p1 = Corvus.Text.Json.Period.FromHours(3);
        var p2 = Corvus.Text.Json.Period.FromHours(2);
        Corvus.Text.Json.Period sum = p1 + p2;
        Assert.AreEqual(5, sum.Hours);
        Assert.AreEqual(sum, Corvus.Text.Json.Period.Add(p1, p2));
    }

    [TestMethod]
    public void Addition_DayCrossingMonthBoundary()
    {
        var start = new LocalDateTime(2010, 2, 20, 10, 0);
        LocalDateTime result = start + Corvus.Text.Json.Period.FromDays(10);
        Assert.AreEqual(new LocalDateTime(2010, 3, 2, 10, 0), result);
    }

    [TestMethod]
    public void Addition_OneYearOnLeapDay()
    {
        var start = new LocalDateTime(2012, 2, 29, 10, 0);
        LocalDateTime result = start + Corvus.Text.Json.Period.FromYears(1);
        // Feb 29th becomes Feb 28th
        Assert.AreEqual(new LocalDateTime(2013, 2, 28, 10, 0), result);
    }

    [TestMethod]
    public void Addition_FourYearsOnLeapDay()
    {
        var start = new LocalDateTime(2012, 2, 29, 10, 0);
        LocalDateTime result = start + Corvus.Text.Json.Period.FromYears(4);
        // Feb 29th is still valid in 2016
        Assert.AreEqual(new LocalDateTime(2016, 2, 29, 10, 0), result);
    }

    [TestMethod]
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
        Assert.AreEqual(new LocalDateTime(2008, 3, 2, 0, 0), result);
    }

    [TestMethod]
    public void Subtraction_WithDifferentPeriodTypes()
    {
        var p1 = Corvus.Text.Json.Period.FromHours(3);
        var p2 = Corvus.Text.Json.Period.FromMinutes(20);
        Corvus.Text.Json.Period difference = p1 - p2;
        Assert.AreEqual(3, difference.Hours);
        Assert.AreEqual(-20, difference.Minutes);
        Assert.AreEqual(difference, Corvus.Text.Json.Period.Subtract(p1, p2));
    }

    [TestMethod]
    public void Subtraction_WithIdenticalPeriodTypes()
    {
        var p1 = Corvus.Text.Json.Period.FromHours(3);
        var p2 = Corvus.Text.Json.Period.FromHours(2);
        Corvus.Text.Json.Period difference = p1 - p2;
        Assert.AreEqual(1, difference.Hours);
        Assert.AreEqual(difference, Corvus.Text.Json.Period.Subtract(p1, p2));
    }

    [TestMethod]
    public void Equality_WhenEqual()
    {
        Assert.AreEqual(Corvus.Text.Json.Period.FromHours(10), Corvus.Text.Json.Period.FromHours(10));
        Assert.AreEqual(Corvus.Text.Json.Period.FromMinutes(15), Corvus.Text.Json.Period.FromMinutes(15));
        Assert.AreEqual(Corvus.Text.Json.Period.FromDays(5), Corvus.Text.Json.Period.FromDays(5));
    }

    [TestMethod]
    public void Equality_WithDifferentPeriodTypes_OnlyConsidersValues()
    {
        Corvus.Text.Json.Period allFields = Corvus.Text.Json.Period.FromMinutes(1) + Corvus.Text.Json.Period.FromHours(1) - Corvus.Text.Json.Period.FromMinutes(1);
        var justHours = Corvus.Text.Json.Period.FromHours(1);
        Assert.AreEqual(allFields, justHours);
    }

    [TestMethod]
    public void Equality_WhenUnequal()
    {
        Assert.IsFalse(Corvus.Text.Json.Period.FromHours(10).Equals(Corvus.Text.Json.Period.FromHours(20)));
        Assert.IsFalse(Corvus.Text.Json.Period.FromMinutes(15).Equals(Corvus.Text.Json.Period.FromSeconds(15)));
        Assert.IsFalse(Corvus.Text.Json.Period.FromHours(1).Equals(Corvus.Text.Json.Period.FromMinutes(60)));
        Assert.IsFalse(Corvus.Text.Json.Period.FromHours(1).Equals(new object()));
        Assert.IsFalse(Corvus.Text.Json.Period.FromHours(1).Equals((object?)null));
    }

    [TestMethod]
    public void EqualityOperators()
    {
        var val1 = Corvus.Text.Json.Period.FromHours(1);
        var val2 = Corvus.Text.Json.Period.FromHours(1);
        var val3 = Corvus.Text.Json.Period.FromHours(2);
        Corvus.Text.Json.Period? val4 = null;
        Assert.IsTrue(val1 == val2);
        Assert.IsFalse(val1 == val3);
        Assert.IsFalse(val1 == val4);
        Assert.IsFalse(val4 == val1);
        Assert.IsTrue(val4 == null);
        Assert.IsTrue(null == val4);

        Assert.IsFalse(val1 != val2);
        Assert.IsTrue(val1 != val3);
        Assert.IsTrue(val1 != val4);
        Assert.IsTrue(val4 != val1);
        Assert.IsFalse(val4 != null);
        Assert.IsFalse(null != val4);
    }

    [TestMethod]
    [DataRow(PeriodUnits.Years, false)]
    [DataRow(PeriodUnits.Weeks, false)]
    [DataRow(PeriodUnits.Months, false)]
    [DataRow(PeriodUnits.Days, false)]
    [DataRow(PeriodUnits.Hours, true)]
    [DataRow(PeriodUnits.Minutes, true)]
    [DataRow(PeriodUnits.Seconds, true)]
    [DataRow(PeriodUnits.Milliseconds, true)]
    [DataRow(PeriodUnits.Ticks, true)]
    [DataRow(PeriodUnits.Nanoseconds, true)]
    public void HasTimeComponent_SingleValued(PeriodUnits unit, bool hasTimeComponent)
    {
        Json.Period period = new Corvus.Text.Json.PeriodBuilder { [unit] = 1 }.BuildPeriod();
        Assert.AreEqual(hasTimeComponent, period.HasTimeComponent);
    }

    [TestMethod]
    [DataRow(PeriodUnits.Years, true)]
    [DataRow(PeriodUnits.Weeks, true)]
    [DataRow(PeriodUnits.Months, true)]
    [DataRow(PeriodUnits.Days, true)]
    [DataRow(PeriodUnits.Hours, false)]
    [DataRow(PeriodUnits.Minutes, false)]
    [DataRow(PeriodUnits.Seconds, false)]
    [DataRow(PeriodUnits.Milliseconds, false)]
    [DataRow(PeriodUnits.Ticks, false)]
    [DataRow(PeriodUnits.Nanoseconds, false)]
    public void HasDateComponent_SingleValued(PeriodUnits unit, bool hasDateComponent)
    {
        Json.Period period = new Corvus.Text.Json.PeriodBuilder { [unit] = 1 }.BuildPeriod();
        Assert.AreEqual(hasDateComponent, period.HasDateComponent);
    }

    [TestMethod]
    public void ToString_Positive()
    {
        Corvus.Text.Json.Period period = Corvus.Text.Json.Period.FromDays(1) + Corvus.Text.Json.Period.FromHours(2);
        Assert.AreEqual("P1DT2H", period.ToString());
    }

    [TestMethod]
    public void ToString_Negative()
    {
        Corvus.Text.Json.Period period = Corvus.Text.Json.Period.FromDays(-1) + Corvus.Text.Json.Period.FromHours(-2);
        Assert.AreEqual("P-1DT-2H", period.ToString());
    }

    [TestMethod]
    public void ToString_Mixed()
    {
        Corvus.Text.Json.Period period = Corvus.Text.Json.Period.FromDays(-1) + Corvus.Text.Json.Period.FromHours(2);
        Assert.AreEqual("PT-22H", period.ToString());
    }

    [TestMethod]
    public void ToString_Zero()
    {
        Assert.AreEqual("P0D", Corvus.Text.Json.Period.Zero.ToString());
    }

    [TestMethod]
    public void Normalize_Weeks()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Weeks = 2, Days = 5 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Days = 19 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_Hours()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = 25, Days = 1 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = 1, Days = 2 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_Minutes()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = 1, Minutes = 150 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = 3, Minutes = 30 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_Seconds()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Minutes = 1, Seconds = 150 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Minutes = 3, Seconds = 30 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_Milliseconds()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Seconds = 1, Milliseconds = 1500 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Seconds = 2, Milliseconds = 500 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_Ticks()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Milliseconds = 1, Ticks = 15000 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Milliseconds = 2, Ticks = 0, Nanoseconds = 500000 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_Nanoseconds()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Ticks = 1, Nanoseconds = 150 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Nanoseconds = 250 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_MultipleFields()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = 1, Minutes = 119, Seconds = 150 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = 3, Minutes = 1, Seconds = 30 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_AllNegative()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = -1, Minutes = -119, Seconds = -150 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = -3, Minutes = -1, Seconds = -30 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_MixedSigns_PositiveResult()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = 3, Minutes = -1 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = 2, Minutes = 59 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_MixedSigns_NegativeResult()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Hours = 1, Minutes = -121 }.BuildPeriod();
        Json.Period normalized = original.Normalize();
        Json.Period expected = new Corvus.Text.Json.PeriodBuilder { Hours = -1, Minutes = -1 }.BuildPeriod();
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalize_DoesntAffectMonthsAndYears()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Years = 2, Months = 1, Days = 400 }.BuildPeriod();
        Assert.AreEqual(original, original.Normalize());
    }

    [TestMethod]
    public void Normalize_ZeroResult()
    {
        Json.Period original = new Corvus.Text.Json.PeriodBuilder { Years = 0 }.BuildPeriod();
        Assert.AreEqual(Corvus.Text.Json.Period.Zero, original.Normalize());
    }

    [TestMethod]
    public void ToString_SingleUnit()
    {
        var period = Corvus.Text.Json.Period.FromHours(5);
        Assert.AreEqual("PT5H", period.ToString());
    }

    [TestMethod]
    public void ToString_MultipleUnits()
    {
        Json.Period period = new Corvus.Text.Json.PeriodBuilder { Hours = 5, Minutes = 30 }.BuildPeriod();
        Assert.AreEqual("PT5H30M", period.ToString());
    }

    [TestMethod]
    public void ToDuration_InvalidWithYears()
    {
        var period = Corvus.Text.Json.Period.FromYears(1);
        Assert.ThrowsExactly<InvalidOperationException>(() => period.ToDuration());
    }

    [TestMethod]
    public void ToDuration_InvalidWithMonths()
    {
        var period = Corvus.Text.Json.Period.FromMonths(1);
        Assert.ThrowsExactly<InvalidOperationException>(() => period.ToDuration());
    }

    [TestMethod]
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
        Assert.AreEqual(
            1 * NodaConstants.TicksPerWeek +
            2 * NodaConstants.TicksPerDay +
            3 * NodaConstants.TicksPerHour +
            4 * NodaConstants.TicksPerMinute +
            5 * NodaConstants.TicksPerSecond +
            6 * NodaConstants.TicksPerMillisecond + 7,
            period.ToDuration().BclCompatibleTicks);
    }

    [TestMethod]
    public void ToDuration_ValidWithZeroValuesInMonthYearUnits()
    {
        Corvus.Text.Json.Period period = Corvus.Text.Json.Period.FromMonths(1) + Corvus.Text.Json.Period.FromYears(1);
        period = period - period + Corvus.Text.Json.Period.FromDays(1);
        Assert.IsFalse(period.HasTimeComponent);
        Assert.AreEqual(Duration.FromDays(1), period.ToDuration());
    }

    [TestMethod]
    public void NormalizingEqualityComparer_PeriodToItself()
    {
        var period = Corvus.Text.Json.Period.FromYears(1);
        Assert.IsTrue(Corvus.Text.Json.Period.NormalizingEqualityComparer.Equals(period, period));
    }

    [TestMethod]
    public void NormalizingEqualityComparer_NonEqualAfterNormalization()
    {
        var period1 = Corvus.Text.Json.Period.FromHours(2);
        var period2 = Corvus.Text.Json.Period.FromMinutes(150);
        Assert.IsFalse(Corvus.Text.Json.Period.NormalizingEqualityComparer.Equals(period1, period2));
    }

    [TestMethod]
    public void NormalizingEqualityComparer_EqualAfterNormalization()
    {
        var period1 = Corvus.Text.Json.Period.FromHours(2);
        var period2 = Corvus.Text.Json.Period.FromMinutes(120);
        Assert.IsTrue(Corvus.Text.Json.Period.NormalizingEqualityComparer.Equals(period1, period2));
    }

    [TestMethod]
    public void NormalizingEqualityComparer_GetHashCodeAfterNormalization()
    {
        var period1 = Corvus.Text.Json.Period.FromHours(2);
        var period2 = Corvus.Text.Json.Period.FromMinutes(120);
        Assert.AreEqual(Corvus.Text.Json.Period.NormalizingEqualityComparer.GetHashCode(period1),
            Corvus.Text.Json.Period.NormalizingEqualityComparer.GetHashCode(period2));
    }

    [TestMethod]
    public void Comparer_DurationablePeriods()
    {
        var bigger = Corvus.Text.Json.Period.FromHours(25);
        var smaller = Corvus.Text.Json.Period.FromDays(1);
        IComparer<Json.Period> comparer = Corvus.Text.Json.Period.CreateComparer(new LocalDateTime(2000, 1, 1, 0, 0));
        Assert.IsTrue(comparer.Compare(bigger, smaller) > 0);
        Assert.IsTrue(comparer.Compare(smaller, bigger) < 0);
        Assert.AreEqual(0, comparer.Compare(bigger, bigger));
    }

    [TestMethod]
    public void Comparer_NonDurationablePeriods()
    {
        var month = Corvus.Text.Json.Period.FromMonths(1);
        var days = Corvus.Text.Json.Period.FromDays(30);
        // At the start of January, a month is longer than 30 days
        IComparer<Json.Period> januaryComparer = Corvus.Text.Json.Period.CreateComparer(new LocalDateTime(2000, 1, 1, 0, 0));
        Assert.IsTrue(januaryComparer.Compare(month, days) > 0);
        Assert.IsTrue(januaryComparer.Compare(days, month) < 0);
        Assert.AreEqual(0, januaryComparer.Compare(month, month));

        // At the start of February, a month is shorter than 30 days
        IComparer<Json.Period> februaryComparer = Corvus.Text.Json.Period.CreateComparer(new LocalDateTime(2000, 2, 1, 0, 0));
        Assert.IsTrue(februaryComparer.Compare(month, days) < 0);
        Assert.IsTrue(februaryComparer.Compare(days, month) > 0);
        Assert.AreEqual(0, februaryComparer.Compare(month, month));
    }

    public static IEnumerable<object[]> PeriodMaxAndMinValues =>
    [
        [Corvus.Text.Json.Period.MaxValue, int.MaxValue, long.MaxValue],
        [Corvus.Text.Json.Period.MinValue, int.MinValue, long.MinValue]
    ];

    public static string PeriodMaxAndMinValuesDisplayName(MethodInfo _, object[] data)
    {
        // Avoid calling Period.ToString() which triggers Normalize() and overflows for max/min values
        int expectedInt = (int)data[1];
        return expectedInt == int.MaxValue ? "MaxValue" : "MinValue";
    }

    /// <summary>
    /// Ensure that Corvus.Text.Json.Period.MaxValue and Corvus.Text.Json.Period.MinValue contain the max/min value assignable to each public property.
    /// </summary>
    [TestMethod]
    [DynamicData(nameof(PeriodMaxAndMinValues), DynamicDataDisplayName = nameof(PeriodMaxAndMinValuesDisplayName))]
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
                Assert.AreEqual(expectedIntValue, actualValue);
                return;
            }
            if (actualValue is long)
            {
                Assert.AreEqual(expectedLongValue, actualValue);
                return;
            }

            throw new InvalidOperationException($"Property {property.Name} has unexpected type {actualValue?.GetType().Name}.");
        }
    }

    [TestMethod]
    public void FromNanoseconds()
    {
        var period = Corvus.Text.Json.Period.FromNanoseconds(1234567890L);
        Assert.AreEqual(1234567890L, period.Nanoseconds);
    }

    [TestMethod]
    [DataRow("P9999999999999999999Y")] // 19 digits, first 18 exceed threshold → overflow
    [DataRow("P9223372036854775808Y")] // Int64.MaxValue + 1 → last digit > 7
    [DataRow("P92233720368547758070Y")] // 20 digits → too many digits after boundary
    [DataRow("P99999999999999999999Y")] // 20 digits all 9s → way over overflow
    public void TryParse_OverflowValues_ReturnsFalse(string input)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(input);
        bool result = Corvus.Text.Json.Period.TryParse(utf8, out _);
        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("P9223372036854775807Y")] // Exactly Int64.MaxValue — parsed but truncated to int
    [DataRow("P922337203685477580Y")] // Just at threshold boundary
    public void TryParse_LargeValidValues_Succeeds(string input)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(input);
        bool result = Corvus.Text.Json.Period.TryParse(utf8, out _);
        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow("P1Y2M3DT4H5M6S")] // Standard valid duration
    [DataRow("PT0S")] // Zero seconds
    [DataRow("P0D")] // Zero days
    public void TryParse_ValidDurations_ReturnsTrue(string input)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(input);
        bool result = Corvus.Text.Json.Period.TryParse(utf8, out _);
        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow("")] // Empty
    [DataRow("P")] // Only prefix
    [DataRow("PT")] // Only prefix with T
    [DataRow("P-1Y")] // Negative (RFC 3339 disallows)
    [DataRow("1Y")] // Missing P prefix
    [DataRow("P1")] // Missing unit designator
    public void TryParse_InvalidDurations_ReturnsFalse(string input)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(input);
        bool result = Corvus.Text.Json.Period.TryParse(utf8, out _);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Equals_SamePeriods_ReturnsTrue()
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("P1Y2M3DT4H5M6S");
        Assert.IsTrue(Corvus.Text.Json.Period.TryParse(utf8, out Corvus.Text.Json.Period p1));
        Assert.IsTrue(Corvus.Text.Json.Period.TryParse(utf8, out Corvus.Text.Json.Period p2));
        Assert.IsTrue(p1.Equals(p2));
    }

    [TestMethod]
    public void Equals_DifferentPeriods_ReturnsFalse()
    {
        Assert.IsTrue(Corvus.Text.Json.Period.TryParse(System.Text.Encoding.UTF8.GetBytes("P1Y"), out Corvus.Text.Json.Period p1));
        Assert.IsTrue(Corvus.Text.Json.Period.TryParse(System.Text.Encoding.UTF8.GetBytes("P2Y"), out Corvus.Text.Json.Period p2));
        Assert.IsFalse(p1.Equals(p2));
    }

    [TestMethod]
    public void ImplicitConversion_ToNodaTimePeriod()
    {
        Assert.IsTrue(Corvus.Text.Json.Period.TryParse(System.Text.Encoding.UTF8.GetBytes("P1Y2M4DT5H6M7S"), out Corvus.Text.Json.Period corvusPeriod));
        NodaTime.Period nodaPeriod = corvusPeriod;
        Assert.AreEqual(1, nodaPeriod.Years);
        Assert.AreEqual(2, nodaPeriod.Months);
        Assert.AreEqual(4, nodaPeriod.Days);
        Assert.AreEqual(5, nodaPeriod.Hours);
        Assert.AreEqual(6, nodaPeriod.Minutes);
        Assert.AreEqual(7, nodaPeriod.Seconds);
    }

    [TestMethod]
    public void ImplicitConversion_FromNodaTimePeriod()
    {
        NodaTime.PeriodBuilder builder = new()
        {
            Years = 3,
            Months = 6,
            Days = 15,
            Hours = 12,
        };

        NodaTime.Period nodaPeriod = builder.Build();
        Corvus.Text.Json.Period corvusPeriod = nodaPeriod;
        Assert.AreEqual(3, corvusPeriod.Years);
        Assert.AreEqual(6, corvusPeriod.Months);
        Assert.AreEqual(15, corvusPeriod.Days);
        Assert.AreEqual(12, corvusPeriod.Hours);
    }
}
