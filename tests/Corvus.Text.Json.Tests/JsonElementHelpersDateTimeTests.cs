// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json.Internal;
using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonElementHelpersDateTimeTests
{
    [Fact]
    public void CreateOffsetDateTimeCore_ProducesExpected()
    {
        OffsetDateTime dt = JsonElementHelpers.CreateOffsetDateTimeCore(2024, 6, 7, 12, 34, 56, 789, 0, 0, 3600);
        Assert.Equal(new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 34, 56, 789), Offset.FromHours(1)), dt);
    }

    [Fact]
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

        Assert.Equal(expected, dt);
    }

    [Fact]
    public void CreateOffsetDateTimeCore_WithoutMicroNano()
    {
        OffsetDateTime dt = JsonElementHelpers.CreateOffsetDateTimeCore(2024, 6, 7, 12, 34, 56, 789, 3600);
        Assert.Equal(new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 34, 56, 789), Offset.FromHours(1)), dt);
    }

    [Fact]
    public void CreateOffsetTimeCore_ProducesExpected()
    {
        OffsetTime t = JsonElementHelpers.CreateOffsetTimeCore(12, 34, 56, 789, 0, 0, 3600);
        Assert.Equal(new OffsetTime(new LocalTime(12, 34, 56, 789), Offset.FromHours(1)), t);
    }

    [Fact]
    public void CreateOffsetTimeCore_WithoutMicroNano()
    {
        OffsetTime t = JsonElementHelpers.CreateOffsetTimeCore(12, 34, 56, 789, 3600);
        Assert.Equal(new OffsetTime(new LocalTime(12, 34, 56, 789), Offset.FromHours(1)), t);
    }

    [Fact]
    public void ParseLocalDate_ThrowsOnInvalid()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("2024-13-40");
        Assert.Throws<FormatException>(() => JsonElementHelpers.ParseLocalDate(invalid));
    }

    [Fact]
    public void ParseOffsetDate_ThrowsOnInvalid()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("2024-13-40+01:00");
        Assert.Throws<FormatException>(() => JsonElementHelpers.ParseOffsetDate(invalid));
    }

    [Fact]
    public void ParseOffsetDateTime_ThrowsOnInvalid()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("2024-06-07T25:00:00+01:00");
        Assert.Throws<FormatException>(() => JsonElementHelpers.ParseOffsetDateTime(invalid));
    }

    [Fact]
    public void ParseOffsetTime_ThrowsOnInvalid()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("25:00:00+01:00");
        Assert.Throws<FormatException>(() => JsonElementHelpers.ParseOffsetTime(invalid));
    }

    [Fact]
    public void ParsePeriod_ThrowsOnInvalid()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("notaperiod");
        Assert.Throws<FormatException>(() => JsonElementHelpers.ParsePeriod(invalid));
    }

    [Fact]
    public void TryFormatLocalDate_RoundTrip()
    {
        var date = new LocalDate(2024, 6, 7);
        Span<byte> buffer = stackalloc byte[16];
        Assert.True(JsonElementHelpers.TryFormatLocalDate(date, buffer, out int written));
        Assert.True(JsonElementHelpers.TryParseLocalDate(buffer[..written], out LocalDate parsed));
        Assert.Equal(date, parsed);
    }

    [Fact]
    public void TryFormatOffsetDate_RoundTrip()
    {
        var date = new OffsetDate(new LocalDate(2024, 6, 7), Offset.FromHours(2));
        Span<byte> buffer = stackalloc byte[32];
        Assert.True(JsonElementHelpers.TryFormatOffsetDate(date, buffer, out int written));
        Assert.True(JsonElementHelpers.TryParseOffsetDate(buffer[..written], out OffsetDate parsed));
        Assert.Equal(date, parsed);
    }

    [Fact]
    public void TryFormatOffsetDateTime_RoundTrip()
    {
        var dt = new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 34, 56, 789), Offset.FromHours(-3));
        Span<byte> buffer = stackalloc byte[40];
        Assert.True(JsonElementHelpers.TryFormatOffsetDateTime(dt, buffer, out int written));
        Assert.True(JsonElementHelpers.TryParseOffsetDateTime(buffer[..written], out OffsetDateTime parsed));
        Assert.Equal(dt, parsed);
    }

    [Fact]
    public void TryFormatOffsetTime_RoundTrip()
    {
        var t = new OffsetTime(new LocalTime(12, 34, 56, 789), Offset.FromHours(1));
        Span<byte> buffer = stackalloc byte[32];
        Assert.True(JsonElementHelpers.TryFormatOffsetTime(t, buffer, out int written));
        Assert.True(JsonElementHelpers.TryParseOffsetTime(buffer[..written], out OffsetTime parsed));
        Assert.Equal(t, parsed);
    }

    [Fact]
    public void TryFormatPeriod_RoundTrip()
    {
        // Simple period: days, hours, minutes
        Period period = Period.FromDays(5) + Period.FromHours(3) + Period.FromMinutes(2);
        Span<byte> buffer = stackalloc byte[90];
        Assert.True(JsonElementHelpers.TryFormatPeriod(period, buffer, out int written));
        Assert.True(JsonElementHelpers.TryParsePeriod(buffer[..written], out Period parsed));
        Assert.Equal(period.Normalize(), parsed.Normalize());

        // Edge case: zero period
        Period zero = Period.Zero;
        buffer = stackalloc byte[90];
        Assert.True(JsonElementHelpers.TryFormatPeriod(zero, buffer, out written));
        Assert.True(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.Equal(zero, parsed);

        // Edge case: only years
        Period years = Period.FromYears(9999).Normalize();
        Assert.True(JsonElementHelpers.TryFormatPeriod(years, buffer, out written));
        Assert.True(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.Equal(years, parsed);

        // Edge case: only weeks
        Period weeks = Period.FromWeeks(52).Normalize();
        Assert.True(JsonElementHelpers.TryFormatPeriod(weeks, buffer, out written));
        Assert.True(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.Equal(weeks, parsed);

        // Edge case: only time (hours, minutes, seconds)
        Period timeOnly = (Period.FromHours(23) + Period.FromMinutes(59) + Period.FromSeconds(59)).Normalize();
        Assert.True(JsonElementHelpers.TryFormatPeriod(timeOnly, buffer, out written));
        Assert.True(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.Equal(timeOnly, parsed);

        // Edge case: fractional seconds (milliseconds)
        Period fractionalMillis = Period.FromMilliseconds(123).Normalize();
        Assert.True(JsonElementHelpers.TryFormatPeriod(fractionalMillis, buffer, out written));
        Assert.True(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.Equal(fractionalMillis, parsed);

        // Edge case: fractional seconds (microseconds)
        Period fractionalMicros = Period.FromTicks(1).Normalize(); // 100ns = 0.0001ms = 0.0000001s
        Assert.True(JsonElementHelpers.TryFormatPeriod(fractionalMicros, buffer, out written));
        Assert.True(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.Equal(fractionalMicros, parsed);

        // Edge case: negative period
        Period negative = (Period.FromDays(-1) + Period.FromHours(-2)).Normalize();
        Assert.True(JsonElementHelpers.TryFormatPeriod(negative, buffer, out written));
        Assert.True(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.Equal(negative.Normalize(), parsed.Normalize());

        // Edge case: maximum supported values
        Period max = (Period.FromYears(9999) + Period.FromMonths(12) + Period.FromDays(31) +
                  Period.FromHours(23) + Period.FromMinutes(59) + Period.FromSeconds(59) +
                  Period.FromMilliseconds(999)).Normalize();
        Assert.True(JsonElementHelpers.TryFormatPeriod(max, buffer, out written));
        Assert.True(JsonElementHelpers.TryParsePeriod(buffer[..written], out parsed));
        Assert.Equal(max.Normalize(), parsed.Normalize());
    }

    [Fact]
    public void TryParseLocalDate_ValidAndInvalid()
    {
        byte[] valid = Encoding.UTF8.GetBytes("2024-06-07");
        Assert.True(JsonElementHelpers.TryParseLocalDate(valid, out LocalDate date));
        Assert.Equal(new LocalDate(2024, 6, 7), date);

        byte[] invalid = Encoding.UTF8.GetBytes("2024-13-40");
        Assert.False(JsonElementHelpers.TryParseLocalDate(invalid, out _));
    }

    [Fact]
    public void TryParseOffsetDate_ValidAndInvalid()
    {
        byte[] valid = Encoding.UTF8.GetBytes("2024-06-07+01:00");
        Assert.True(JsonElementHelpers.TryParseOffsetDate(valid, out OffsetDate date));
        Assert.Equal(new OffsetDate(new LocalDate(2024, 6, 7), Offset.FromHours(1)), date);

        byte[] invalid = Encoding.UTF8.GetBytes("2024-13-40+01:00");
        Assert.False(JsonElementHelpers.TryParseOffsetDate(invalid, out _));
    }

    [Fact]
    public void TryParseOffsetDateTime_ValidAndInvalid()
    {
        byte[] valid = Encoding.UTF8.GetBytes("2024-06-07T12:34:56.7890000+01:00");
        Assert.True(JsonElementHelpers.TryParseOffsetDateTime(valid, out OffsetDateTime dt));
        Assert.Equal(new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 34, 56, 789), Offset.FromHours(1)), dt);

        byte[] invalid = Encoding.UTF8.GetBytes("2024-06-07T25:00:00+01:00");
        Assert.False(JsonElementHelpers.TryParseOffsetDateTime(invalid, out _));
    }

    [Fact]
    public void TryParseOffsetTime_ValidAndInvalid()
    {
        byte[] valid = Encoding.UTF8.GetBytes("12:34:56.7890000+01:00");
        Assert.True(JsonElementHelpers.TryParseOffsetTime(valid, out OffsetTime t));
        Assert.Equal(new OffsetTime(new LocalTime(12, 34, 56, 789), Offset.FromHours(1)), t);

        byte[] invalid = Encoding.UTF8.GetBytes("25:00:00+01:00");
        Assert.False(JsonElementHelpers.TryParseOffsetTime(invalid, out _));
    }

    [Fact]
    public void TryParsePeriod_ValidAndInvalid()
    {
        // Valid examples
        byte[] validDay = Encoding.UTF8.GetBytes("P1D");
        Assert.True(JsonElementHelpers.TryParsePeriod(validDay, out Period periodDay));
        Assert.Equal(Period.FromDays(1).Normalize(), periodDay);

        byte[] validMonth = Encoding.UTF8.GetBytes("P2M");
        Assert.True(JsonElementHelpers.TryParsePeriod(validMonth, out Period periodMonth));
        Assert.Equal(Period.FromMonths(2).Normalize(), periodMonth);

        byte[] validYear = Encoding.UTF8.GetBytes("P3Y");
        Assert.True(JsonElementHelpers.TryParsePeriod(validYear, out Period periodYear));
        Assert.Equal(Period.FromYears(3).Normalize(), periodYear);

        byte[] validWeek = Encoding.UTF8.GetBytes("P4W");
        Assert.True(JsonElementHelpers.TryParsePeriod(validWeek, out Period periodWeek));
        Assert.Equal(Period.FromWeeks(4).Normalize(), periodWeek);

        byte[] validTime = Encoding.UTF8.GetBytes("PT5H6M7S");
        Assert.True(JsonElementHelpers.TryParsePeriod(validTime, out Period periodTime));
        Assert.Equal((Period.FromHours(5) + Period.FromMinutes(6) + Period.FromSeconds(7)).Normalize(), periodTime);

        byte[] validFull = Encoding.UTF8.GetBytes("P1Y2M3DT4H5M6S");
        Assert.True(JsonElementHelpers.TryParsePeriod(validFull, out Period periodFull));
        Assert.Equal(
            (Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(3) +
            Period.FromHours(4) + Period.FromMinutes(5) + Period.FromSeconds(6)).Normalize(),
            periodFull);

        byte[] validFractional = Encoding.UTF8.GetBytes("PT0.5S");
        Assert.True(JsonElementHelpers.TryParsePeriod(validFractional, out Period periodFractional));
        Assert.Equal(Period.FromMilliseconds(500).Normalize(), periodFractional);

        // Invalid examples
        byte[] invalid = Encoding.UTF8.GetBytes("notaperiod");
        Assert.False(JsonElementHelpers.TryParsePeriod(invalid, out _));

        byte[] invalidEmpty = Encoding.UTF8.GetBytes("");
        Assert.False(JsonElementHelpers.TryParsePeriod(invalidEmpty, out _));

        byte[] invalidNoP = Encoding.UTF8.GetBytes("1Y2M");
        Assert.False(JsonElementHelpers.TryParsePeriod(invalidNoP, out _));

        byte[] invalidBadOrder = Encoding.UTF8.GetBytes("P1DT1Y");
        Assert.False(JsonElementHelpers.TryParsePeriod(invalidBadOrder, out _));

        byte[] invalidDoubleT = Encoding.UTF8.GetBytes("P1YT2H3MT4S");
        Assert.False(JsonElementHelpers.TryParsePeriod(invalidDoubleT, out _));
    }
}