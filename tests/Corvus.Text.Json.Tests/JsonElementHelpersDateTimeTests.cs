// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json.Internal;
using NodaTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonElementHelpersDateTimeTests
{
    [TestMethod]
    public void CreateOffsetDateTimeCore_ProducesExpected()
    {
        OffsetDateTime dt = JsonElementHelpers.CreateOffsetDateTimeCore(2024, 6, 7, 12, 34, 56, 789, 0, 0, 3600);
        Assert.AreEqual(new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 34, 56, 789), Offset.FromHours(1)), dt);
    }

    [TestMethod]
    public void CreateOffsetDateTimeCore_WithMicroAndNano_ProducesExpected()
    {
        // 2024-06-07T12:34:56.7891234+01:00
        // 789 ms, 1234 ns = 789 ms, 1 us, 234 ns
        OffsetDateTime dt = JsonElementHelpers.CreateOffsetDateTimeCore(
            2024, 6, 7, 12, 34, 56, 789, 1, 234, 3600);

        var expected = new OffsetDateTime(
            new LocalDateTime(2024, 6, 7, 12, 34, 56)
                .PlusMilliseconds(789)
                .PlusNanoseconds((1 * 1000) + 234),
            Offset.FromHours(1));

        Assert.AreEqual(expected, dt);
    }

    [TestMethod]
    public void CreateOffsetDateTimeCore_WithoutMicroNano()
    {
        OffsetDateTime dt = JsonElementHelpers.CreateOffsetDateTimeCore(2024, 6, 7, 12, 34, 56, 789, 3600);
        Assert.AreEqual(new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 34, 56, 789), Offset.FromHours(1)), dt);
    }

    [TestMethod]
    public void CreateOffsetTimeCore_ProducesExpected()
    {
        OffsetTime t = JsonElementHelpers.CreateOffsetTimeCore(12, 34, 56, 789, 0, 0, 3600);
        Assert.AreEqual(new OffsetTime(new LocalTime(12, 34, 56, 789), Offset.FromHours(1)), t);
    }

    [TestMethod]
    public void CreateOffsetTimeCore_WithoutMicroNano()
    {
        OffsetTime t = JsonElementHelpers.CreateOffsetTimeCore(12, 34, 56, 789, 3600);
        Assert.AreEqual(new OffsetTime(new LocalTime(12, 34, 56, 789), Offset.FromHours(1)), t);
    }

    [TestMethod]
    public void ParseLocalDate_ThrowsOnInvalid()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("2024-13-40");
        Assert.ThrowsExactly<FormatException>(() => JsonElementHelpers.ParseLocalDate(invalid));
    }

    [TestMethod]
    public void ParseOffsetDate_ThrowsOnInvalid()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("2024-13-40+01:00");
        Assert.ThrowsExactly<FormatException>(() => JsonElementHelpers.ParseOffsetDate(invalid));
    }

    [TestMethod]
    public void ParseOffsetDateTime_ThrowsOnInvalid()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("2024-06-07T25:00:00+01:00");
        Assert.ThrowsExactly<FormatException>(() => JsonElementHelpers.ParseOffsetDateTime(invalid));
    }

    [TestMethod]
    public void ParseOffsetTime_ThrowsOnInvalid()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("25:00:00+01:00");
        Assert.ThrowsExactly<FormatException>(() => JsonElementHelpers.ParseOffsetTime(invalid));
    }

    [TestMethod]
    public void ParsePeriod_ThrowsOnInvalid()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("notaperiod");
        Assert.ThrowsExactly<FormatException>(() => JsonElementHelpers.ParsePeriod(invalid));
    }

    [TestMethod]
    public void TryFormatLocalDate_RoundTrip()
    {
        var date = new LocalDate(2024, 6, 7);
        Span<byte> buffer = stackalloc byte[16];
        Assert.IsTrue(JsonElementHelpers.TryFormatLocalDate(date, buffer, out int written));
        Assert.IsTrue(JsonElementHelpers.TryParseLocalDate(buffer[..written], out LocalDate parsed));
        Assert.AreEqual(date, parsed);
    }

    [TestMethod]
    public void TryFormatOffsetDate_RoundTrip()
    {
        var date = new OffsetDate(new LocalDate(2024, 6, 7), Offset.FromHours(2));
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(JsonElementHelpers.TryFormatOffsetDate(date, buffer, out int written));
        Assert.IsTrue(JsonElementHelpers.TryParseOffsetDate(buffer[..written], out OffsetDate parsed));
        Assert.AreEqual(date, parsed);
    }

    [TestMethod]
    public void TryFormatOffsetDateTime_RoundTrip()
    {
        var dt = new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 34, 56, 789), Offset.FromHours(-3));
        Span<byte> buffer = stackalloc byte[40];
        Assert.IsTrue(JsonElementHelpers.TryFormatOffsetDateTime(dt, buffer, out int written));
        Assert.IsTrue(JsonElementHelpers.TryParseOffsetDateTime(buffer[..written], out OffsetDateTime parsed));
        Assert.AreEqual(dt, parsed);
    }

    [TestMethod]
    public void TryFormatOffsetTime_RoundTrip()
    {
        var t = new OffsetTime(new LocalTime(12, 34, 56, 789), Offset.FromHours(1));
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(JsonElementHelpers.TryFormatOffsetTime(t, buffer, out int written));
        Assert.IsTrue(JsonElementHelpers.TryParseOffsetTime(buffer[..written], out OffsetTime parsed));
        Assert.AreEqual(t, parsed);
    }

    [TestMethod]
    public void TryFormatPeriod_RoundTrip()
    {
        // Simple period: days, hours, minutes
        Period period = Period.FromDays(5) + Period.FromHours(3) + Period.FromMinutes(2);
        Span<byte> buffer = stackalloc byte[90];
        Assert.IsTrue(JsonElementHelpers.TryFormatPeriod(period, buffer, out int written));
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(buffer[..written], out Period parsed));
        Assert.AreEqual(period.Normalize(), parsed.Normalize());

        // Edge case: zero period
        Period zero = Period.Zero;
        buffer = stackalloc byte[90];
        Assert.IsTrue(JsonElementHelpers.TryFormatPeriod(zero, buffer, out written));
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.AreEqual(zero, parsed);

        // Edge case: only years
        Period years = Period.FromYears(9999).Normalize();
        Assert.IsTrue(JsonElementHelpers.TryFormatPeriod(years, buffer, out written));
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.AreEqual(years, parsed);

        // Edge case: only weeks
        Period weeks = Period.FromWeeks(52).Normalize();
        Assert.IsTrue(JsonElementHelpers.TryFormatPeriod(weeks, buffer, out written));
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.AreEqual(weeks, parsed);

        // Edge case: only time (hours, minutes, seconds)
        Period timeOnly = (Period.FromHours(23) + Period.FromMinutes(59) + Period.FromSeconds(59)).Normalize();
        Assert.IsTrue(JsonElementHelpers.TryFormatPeriod(timeOnly, buffer, out written));
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.AreEqual(timeOnly, parsed);

        // Edge case: maximum supported values (whole seconds only for RFC 3339)
        Period max = (Period.FromYears(9999) + Period.FromMonths(12) + Period.FromDays(31) +
                  Period.FromHours(23) + Period.FromMinutes(59) + Period.FromSeconds(59)).Normalize();
        Assert.IsTrue(JsonElementHelpers.TryFormatPeriod(max, buffer, out written));
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.AreEqual(max.Normalize(), parsed.Normalize());
    }

    [TestMethod]
    public void TryParseLocalDate_ValidAndInvalid()
    {
        byte[] valid = Encoding.UTF8.GetBytes("2024-06-07");
        Assert.IsTrue(JsonElementHelpers.TryParseLocalDate(valid, out LocalDate date));
        Assert.AreEqual(new LocalDate(2024, 6, 7), date);

        byte[] invalid = Encoding.UTF8.GetBytes("2024-13-40");
        Assert.IsFalse(JsonElementHelpers.TryParseLocalDate(invalid, out _));
    }

    [TestMethod]
    public void TryParseOffsetDate_ValidAndInvalid()
    {
        byte[] valid = Encoding.UTF8.GetBytes("2024-06-07+01:00");
        Assert.IsTrue(JsonElementHelpers.TryParseOffsetDate(valid, out OffsetDate date));
        Assert.AreEqual(new OffsetDate(new LocalDate(2024, 6, 7), Offset.FromHours(1)), date);

        byte[] invalid = Encoding.UTF8.GetBytes("2024-13-40+01:00");
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetDate(invalid, out _));
    }

    [TestMethod]
    public void TryParseOffsetDateTime_ValidAndInvalid()
    {
        byte[] valid = Encoding.UTF8.GetBytes("2024-06-07T12:34:56.7890000+01:00");
        Assert.IsTrue(JsonElementHelpers.TryParseOffsetDateTime(valid, out OffsetDateTime dt));
        Assert.AreEqual(new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 34, 56, 789), Offset.FromHours(1)), dt);

        byte[] invalid = Encoding.UTF8.GetBytes("2024-06-07T25:00:00+01:00");
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetDateTime(invalid, out _));
    }

    [TestMethod]
    public void TryParseOffsetTime_ValidAndInvalid()
    {
        byte[] valid = Encoding.UTF8.GetBytes("12:34:56.7890000+01:00");
        Assert.IsTrue(JsonElementHelpers.TryParseOffsetTime(valid, out OffsetTime t));
        Assert.AreEqual(new OffsetTime(new LocalTime(12, 34, 56, 789), Offset.FromHours(1)), t);

        byte[] invalid = Encoding.UTF8.GetBytes("25:00:00+01:00");
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetTime(invalid, out _));
    }

    [TestMethod]
    public void TryParsePeriod_ValidAndInvalid()
    {
        // Valid examples
        byte[] validDay = Encoding.UTF8.GetBytes("P1D");
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(validDay, out Period periodDay));
        Assert.AreEqual(Period.FromDays(1).Normalize(), periodDay);

        byte[] validMonth = Encoding.UTF8.GetBytes("P2M");
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(validMonth, out Period periodMonth));
        Assert.AreEqual(Period.FromMonths(2).Normalize(), periodMonth);

        byte[] validYear = Encoding.UTF8.GetBytes("P3Y");
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(validYear, out Period periodYear));
        Assert.AreEqual(Period.FromYears(3).Normalize(), periodYear);

        byte[] validWeek = Encoding.UTF8.GetBytes("P4W");
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(validWeek, out Period periodWeek));
        Assert.AreEqual(Period.FromWeeks(4).Normalize(), periodWeek);

        byte[] validTime = Encoding.UTF8.GetBytes("PT5H6M7S");
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(validTime, out Period periodTime));
        Assert.AreEqual((Period.FromHours(5) + Period.FromMinutes(6) + Period.FromSeconds(7)).Normalize(), periodTime);

        byte[] validFull = Encoding.UTF8.GetBytes("P1Y2M3DT4H5M6S");
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(validFull, out Period periodFull));
        Assert.AreEqual(
            (Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(3) +
            Period.FromHours(4) + Period.FromMinutes(5) + Period.FromSeconds(6)).Normalize(),
            periodFull);

        byte[] invalidFractional = Encoding.UTF8.GetBytes("PT0.5S");
        Assert.IsFalse(JsonElementHelpers.TryParsePeriod(invalidFractional, out _));

        // Invalid examples
        byte[] invalid = Encoding.UTF8.GetBytes("notaperiod");
        Assert.IsFalse(JsonElementHelpers.TryParsePeriod(invalid, out _));

        byte[] invalidEmpty = Encoding.UTF8.GetBytes("");
        Assert.IsFalse(JsonElementHelpers.TryParsePeriod(invalidEmpty, out _));

        byte[] invalidNoP = Encoding.UTF8.GetBytes("1Y2M");
        Assert.IsFalse(JsonElementHelpers.TryParsePeriod(invalidNoP, out _));

        byte[] invalidBadOrder = Encoding.UTF8.GetBytes("P1DT1Y");
        Assert.IsFalse(JsonElementHelpers.TryParsePeriod(invalidBadOrder, out _));

        byte[] invalidDoubleT = Encoding.UTF8.GetBytes("P1YT2H3MT4S");
        Assert.IsFalse(JsonElementHelpers.TryParsePeriod(invalidDoubleT, out _));
    }
}
