// <copyright file="PeriodTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using NodaTime;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class PeriodTests
{
    // =====================
    // Factory methods
    // =====================
    [TestMethod]
    public void FromYears_CreatesCorrectPeriod()
    {
        Period p = Period.FromYears(3);
        Assert.AreEqual(3, p.Years);
        Assert.AreEqual(0, p.Months);
    }

    [TestMethod]
    public void FromMonths_CreatesCorrectPeriod()
    {
        Period p = Period.FromMonths(5);
        Assert.AreEqual(5, p.Months);
    }

    [TestMethod]
    public void FromWeeks_CreatesCorrectPeriod()
    {
        Period p = Period.FromWeeks(2);
        Assert.AreEqual(2, p.Weeks);
    }

    [TestMethod]
    public void FromDays_CreatesCorrectPeriod()
    {
        Period p = Period.FromDays(10);
        Assert.AreEqual(10, p.Days);
    }

    [TestMethod]
    public void FromHours_CreatesCorrectPeriod()
    {
        Period p = Period.FromHours(24);
        Assert.AreEqual(24, p.Hours);
    }

    [TestMethod]
    public void FromMinutes_CreatesCorrectPeriod()
    {
        Period p = Period.FromMinutes(90);
        Assert.AreEqual(90, p.Minutes);
    }

    [TestMethod]
    public void FromSeconds_CreatesCorrectPeriod()
    {
        Period p = Period.FromSeconds(45);
        Assert.AreEqual(45, p.Seconds);
    }

    [TestMethod]
    public void FromMilliseconds_CreatesCorrectPeriod()
    {
        Period p = Period.FromMilliseconds(500);
        Assert.AreEqual(500, p.Milliseconds);
    }

    [TestMethod]
    public void FromTicks_CreatesCorrectPeriod()
    {
        Period p = Period.FromTicks(1000);
        Assert.AreEqual(1000, p.Ticks);
    }

    [TestMethod]
    public void FromNanoseconds_CreatesCorrectPeriod()
    {
        Period p = Period.FromNanoseconds(999);
        Assert.AreEqual(999, p.Nanoseconds);
    }

    // =====================
    // Parse / TryParse
    // =====================
    [TestMethod]
    public void Parse_FullPeriod()
    {
        Period p = Period.Parse("P1Y2M3DT4H5M6S");
        Assert.AreEqual(1, p.Years);
        Assert.AreEqual(2, p.Months);
        Assert.AreEqual(3, p.Days);
        Assert.AreEqual(4, p.Hours);
        Assert.AreEqual(5, p.Minutes);
        Assert.AreEqual(6, p.Seconds);
    }

    [TestMethod]
    public void Parse_DateOnly()
    {
        Period p = Period.Parse("P1Y6M");
        Assert.AreEqual(1, p.Years);
        Assert.AreEqual(6, p.Months);
        Assert.IsFalse(p.HasTimeComponent);
    }

    [TestMethod]
    public void Parse_TimeOnly()
    {
        Period p = Period.Parse("PT30S");
        Assert.AreEqual(30, p.Seconds);
        Assert.IsFalse(p.HasDateComponent);
    }

    [TestMethod]
    public void Parse_WithWeeks()
    {
        Period p = Period.Parse("P2W");
        Assert.AreEqual(2, p.Weeks);
    }

    [TestMethod]
    public void Parse_WithFractionalSeconds()
    {
        Period p = Period.Parse("PT1.5S");
        Assert.AreEqual(1, p.Seconds);
        Assert.AreEqual(500, p.Milliseconds);
    }

    [TestMethod]
    public void TryParse_Valid()
    {
        bool success = Period.TryParse("P1Y", out Period p);
        Assert.IsTrue(success);
        Assert.AreEqual(1, p.Years);
    }

    [TestMethod]
    public void TryParse_Invalid_EmptyString()
    {
        bool success = Period.TryParse("", out Period _);
        Assert.IsFalse(success);
    }

    [TestMethod]
    public void TryParse_Invalid_MissingP()
    {
        bool success = Period.TryParse("1Y", out Period _);
        Assert.IsFalse(success);
    }

    [TestMethod]
    public void TryParse_Invalid_JustP()
    {
        bool success = Period.TryParse("P", out Period _);
        Assert.IsFalse(success);
    }

    [TestMethod]
    public void TryParse_Invalid_DuplicateUnit()
    {
        bool success = Period.TryParse("P1Y2Y", out Period _);
        Assert.IsFalse(success);
    }

    [TestMethod]
    public void TryParse_Invalid_DateUnitAfterTime()
    {
        bool success = Period.TryParse("PT1H1D", out Period _);
        Assert.IsFalse(success);
    }

    [TestMethod]
    public void TryParse_Invalid_NoUnits()
    {
        bool success = Period.TryParse("P123", out Period _);
        Assert.IsFalse(success);
    }

    [TestMethod]
    public void Parse_Invalid_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Period.Parse("invalid"));
    }

    [TestMethod]
    public void Parse_String_Valid()
    {
        Period p = Period.Parse("P3M");
        Assert.AreEqual(3, p.Months);
    }

    // =====================
    // Add / Subtract
    // =====================
    [TestMethod]
    public void Add_CombinesPeriods()
    {
        Period a = Period.FromHours(2);
        Period b = Period.FromMinutes(30);
        Period sum = Period.Add(a, b);
        Assert.AreEqual(2, sum.Hours);
        Assert.AreEqual(30, sum.Minutes);
    }

    [TestMethod]
    public void Subtract_SubtractsPeriods()
    {
        Period a = Period.FromHours(5);
        Period b = Period.FromHours(2);
        Period diff = Period.Subtract(a, b);
        Assert.AreEqual(3, diff.Hours);
    }

    [TestMethod]
    public void OperatorPlus()
    {
        Period sum = Period.FromDays(1) + Period.FromHours(12);
        Assert.AreEqual(1, sum.Days);
        Assert.AreEqual(12, sum.Hours);
    }

    [TestMethod]
    public void OperatorMinus()
    {
        Period diff = Period.FromDays(5) - Period.FromDays(3);
        Assert.AreEqual(2, diff.Days);
    }

    // =====================
    // ToDuration
    // =====================
    [TestMethod]
    public void ToDuration_TimeOnlyPeriod()
    {
        Period p = Period.FromHours(2) + Period.FromMinutes(30);
        Duration d = p.ToDuration();
        Assert.AreEqual(2.5 * 60 * 60 * 1_000_000_000, d.TotalNanoseconds);
    }

    [TestMethod]
    public void ToDuration_WithYears_Throws()
    {
        Period p = Period.FromYears(1);
        Assert.ThrowsExactly<InvalidOperationException>(() => p.ToDuration());
    }

    [TestMethod]
    public void ToDuration_WithMonths_Throws()
    {
        Period p = Period.FromMonths(1);
        Assert.ThrowsExactly<InvalidOperationException>(() => p.ToDuration());
    }

    // =====================
    // Normalize
    // =====================
    [TestMethod]
    public void Normalize_ConvertsMinutesToHours()
    {
        Period p = Period.FromMinutes(90);
        Period normalized = p.Normalize();
        Assert.AreEqual(1, normalized.Hours);
        Assert.AreEqual(30, normalized.Minutes);
    }

    [TestMethod]
    public void Normalize_LeavesYearsAndMonthsUnchanged()
    {
        Period p = Period.FromYears(1) + Period.FromMonths(13);
        Period normalized = p.Normalize();
        Assert.AreEqual(1, normalized.Years);
        Assert.AreEqual(13, normalized.Months);
    }

    // =====================
    // Equality
    // =====================
    [TestMethod]
    public void Equals_SamePeriod_True()
    {
        Period a = Period.FromHours(1);
        Period b = Period.FromHours(1);
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentPeriod_False()
    {
        Period a = Period.FromHours(1);
        Period b = Period.FromHours(2);
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void Equals_NodaTimePeriod()
    {
        Period corvusPeriod = Period.FromHours(1);
        NodaTime.Period nodaPeriod = NodaTime.Period.FromHours(1);
        Assert.IsTrue(corvusPeriod.Equals(nodaPeriod));
    }

    [TestMethod]
    public void Equals_Object_Period()
    {
        Period a = Period.FromHours(1);
        object b = Period.FromHours(1);
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_Object_NodaTimePeriod()
    {
        Period a = Period.FromHours(1);
        object b = NodaTime.Period.FromHours(1);
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_Object_Null_False()
    {
        Period a = Period.FromHours(1);
        Assert.IsFalse(a.Equals((object?)null));
    }

    [TestMethod]
    public void Equals_Object_WrongType_False()
    {
        Period a = Period.FromHours(1);
        Assert.IsFalse(a.Equals("not a period"));
    }

    [TestMethod]
    public void OperatorEquals_True()
    {
        Period a = Period.FromDays(1);
        Period b = Period.FromDays(1);
        Assert.IsTrue(a == b);
    }

    [TestMethod]
    public void OperatorNotEquals_True()
    {
        Period a = Period.FromDays(1);
        Period b = Period.FromDays(2);
        Assert.IsTrue(a != b);
    }

    // =====================
    // GetHashCode
    // =====================
    [TestMethod]
    public void GetHashCode_SamePeriod_SameHash()
    {
        Period a = Period.FromHours(3);
        Period b = Period.FromHours(3);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    // =====================
    // HasTimeComponent / HasDateComponent
    // =====================
    [TestMethod]
    public void HasTimeComponent_TimeOnly_True()
    {
        Assert.IsTrue(Period.FromSeconds(1).HasTimeComponent);
    }

    [TestMethod]
    public void HasTimeComponent_DateOnly_False()
    {
        Assert.IsFalse(Period.FromDays(1).HasTimeComponent);
    }

    [TestMethod]
    public void HasDateComponent_DateOnly_True()
    {
        Assert.IsTrue(Period.FromDays(1).HasDateComponent);
    }

    [TestMethod]
    public void HasDateComponent_TimeOnly_False()
    {
        Assert.IsFalse(Period.FromHours(1).HasDateComponent);
    }

    // =====================
    // ToString
    // =====================
    [TestMethod]
    public void ToString_FullPeriod()
    {
        Period p = Period.Parse("P1Y2M3DT4H5M6S");
        Assert.AreEqual("P1Y2M3DT4H5M6S", p.ToString());
    }

    [TestMethod]
    public void ToString_Zero()
    {
        Assert.AreEqual("P0D", Period.Zero.ToString());
    }

    // =====================
    // NormalizingEqualityComparer
    // =====================
    [TestMethod]
    public void NormalizingEqualityComparer_NormalizedEqual()
    {
        Period a = Period.FromMinutes(60);
        Period b = Period.FromHours(1);
        Assert.IsTrue(Period.NormalizingEqualityComparer.Equals(a, b));
    }

    [TestMethod]
    public void NormalizingEqualityComparer_GetHashCode()
    {
        Period a = Period.FromMinutes(60);
        Period b = Period.FromHours(1);
        Assert.AreEqual(
            Period.NormalizingEqualityComparer.GetHashCode(a),
            Period.NormalizingEqualityComparer.GetHashCode(b));
    }

    // =====================
    // CreateComparer
    // =====================
    [TestMethod]
    public void CreateComparer_ComparesRelativeToBase()
    {
        LocalDateTime baseDate = new(2024, 1, 1, 0, 0);
        IComparer<Period> comparer = Period.CreateComparer(baseDate);

        Period oneDay = Period.FromDays(1);
        Period twoDays = Period.FromDays(2);

        Assert.IsTrue(comparer.Compare(oneDay, twoDays) < 0);
        Assert.IsTrue(comparer.Compare(twoDays, oneDay) > 0);
        Assert.AreEqual(0, comparer.Compare(oneDay, oneDay));
    }

    // =====================
    // DaysBetween
    // =====================
    [TestMethod]
    public void DaysBetween_PositiveDifference()
    {
        LocalDate start = new(2024, 1, 1);
        LocalDate end = new(2024, 1, 10);
        Assert.AreEqual(9, Period.DaysBetween(start, end));
    }

    [TestMethod]
    public void DaysBetween_NegativeDifference()
    {
        LocalDate start = new(2024, 1, 10);
        LocalDate end = new(2024, 1, 1);
        Assert.AreEqual(-9, Period.DaysBetween(start, end));
    }

    // =====================
    // Implicit conversions
    // =====================
    [TestMethod]
    public void ImplicitConversion_ToNodaTimePeriod()
    {
        Period corvusPeriod = Period.FromHours(5);
        NodaTime.Period nodaPeriod = corvusPeriod;
        Assert.AreEqual(5, nodaPeriod.Hours);
    }

    [TestMethod]
    public void ImplicitConversion_FromNodaTimePeriod()
    {
        NodaTime.Period nodaPeriod = NodaTime.Period.FromDays(3);
        Period corvusPeriod = nodaPeriod;
        Assert.AreEqual(3, corvusPeriod.Days);
    }
}