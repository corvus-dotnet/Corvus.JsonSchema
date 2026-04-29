// <copyright file="DateTimeCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for NodaTime date/time formatting and parsing in <see cref="JsonElementHelpers"/>.
/// Targets specific uncovered code paths identified via Cobertura coverage reports.
/// </summary>
public class DateTimeCoverageTests
{
    #region TryFormat buffer-too-small paths

    [Fact]
    public void TryFormatLocalDate_BufferTooSmall_ReturnsFalse()
    {
        var date = new LocalDate(2024, 6, 7);
        Span<byte> buffer = stackalloc byte[1];
        Assert.False(JsonElementHelpers.TryFormatLocalDate(date, buffer, out int bytesWritten));
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void TryFormatOffsetDate_BufferTooSmall_ReturnsFalse()
    {
        var date = new OffsetDate(new LocalDate(2024, 6, 7), Offset.FromHours(2));
        Span<byte> buffer = stackalloc byte[1];
        Assert.False(JsonElementHelpers.TryFormatOffsetDate(date, buffer, out int bytesWritten));
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void TryFormatOffsetTime_BufferTooSmall_ReturnsFalse()
    {
        var t = new OffsetTime(new LocalTime(12, 34, 56), Offset.FromHours(1));
        Span<byte> buffer = stackalloc byte[1];
        Assert.False(JsonElementHelpers.TryFormatOffsetTime(t, buffer, out int bytesWritten));
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void TryFormatOffsetDateTime_BufferTooSmall_ReturnsFalse()
    {
        var dt = new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 34, 56), Offset.FromHours(1));
        Span<byte> buffer = stackalloc byte[1];
        Assert.False(JsonElementHelpers.TryFormatOffsetDateTime(dt, buffer, out int bytesWritten));
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void TryFormatPeriod_BufferTooSmall_ReturnsFalse()
    {
        Period period = Period.FromDays(5) + Period.FromHours(3);
        Span<byte> buffer = stackalloc byte[1];
        Assert.False(JsonElementHelpers.TryFormatPeriod(period, buffer, out int bytesWritten));
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region OffsetDate/OffsetTime negative offset paths

    [Fact]
    public void TryFormatOffsetDate_NegativeOffset_ContainsMinusSign()
    {
        var date = new OffsetDate(new LocalDate(2024, 6, 7), Offset.FromHours(-5));
        Span<byte> buffer = stackalloc byte[32];
        Assert.True(JsonElementHelpers.TryFormatOffsetDate(date, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        Assert.Contains("-05:00", result);
    }

    [Fact]
    public void TryFormatOffsetDate_NegativeOffset_RoundTrips()
    {
        var date = new OffsetDate(new LocalDate(2024, 1, 15), Offset.FromHoursAndMinutes(-9, -30));
        Span<byte> buffer = stackalloc byte[32];
        Assert.True(JsonElementHelpers.TryFormatOffsetDate(date, buffer, out int bytesWritten));
        Assert.True(JsonElementHelpers.TryParseOffsetDate(buffer.Slice(0, bytesWritten), out OffsetDate parsed));
        Assert.Equal(date, parsed);
    }

    [Fact]
    public void TryFormatOffsetTime_NegativeOffset_ContainsMinusSign()
    {
        var t = new OffsetTime(new LocalTime(8, 30, 0), Offset.FromHours(-3));
        Span<byte> buffer = stackalloc byte[32];
        Assert.True(JsonElementHelpers.TryFormatOffsetTime(t, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        Assert.Contains("-03:00", result);
    }

    [Fact]
    public void TryFormatOffsetTime_NegativeOffset_RoundTrips()
    {
        var t = new OffsetTime(new LocalTime(14, 0, 0, 500), Offset.FromHoursAndMinutes(-5, -30));
        Span<byte> buffer = stackalloc byte[32];
        Assert.True(JsonElementHelpers.TryFormatOffsetTime(t, buffer, out int bytesWritten));
        Assert.True(JsonElementHelpers.TryParseOffsetTime(buffer.Slice(0, bytesWritten), out OffsetTime parsed));
        Assert.Equal(t, parsed);
    }

    #endregion

    #region OffsetDate non-UTC positive offset path

    [Fact]
    public void TryFormatOffsetDate_PositiveNonUtcOffset_FormatsCorrectly()
    {
        var date = new OffsetDate(new LocalDate(2024, 12, 25), Offset.FromHoursAndMinutes(5, 45));
        Span<byte> buffer = stackalloc byte[32];
        Assert.True(JsonElementHelpers.TryFormatOffsetDate(date, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        Assert.Equal("2024-12-25+05:45", result);
    }

    #endregion

    #region Period with time components (seconds and fractional)

    [Fact]
    public void TryFormatPeriod_WithSeconds_RoundTrips()
    {
        // Exercises the seconds/fractional writing path in TryFormat(Period, ...)
        Period period = Period.FromHours(2) + Period.FromMinutes(15) + Period.FromSeconds(45);
        Span<byte> buffer = stackalloc byte[90];
        Assert.True(JsonElementHelpers.TryFormatPeriod(period, buffer, out int bytesWritten));
        Assert.True(JsonElementHelpers.TryParsePeriod(buffer.Slice(0, bytesWritten), out Period parsed));
        Assert.Equal(period.Normalize(), parsed.Normalize());
    }

    [Fact]
    public void TryFormatPeriod_WithNegativeSeconds_FormatsSuccessfully()
    {
        // Exercises the negative sign path for seconds (integral < 0)
        Period period = Period.FromSeconds(-30);
        Span<byte> buffer = stackalloc byte[90];
        Assert.True(JsonElementHelpers.TryFormatPeriod(period, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        Assert.Contains("-", result);
        Assert.Contains("S", result);
    }

    [Fact]
    public void TryFormatPeriod_WithMilliseconds_FormatsSuccessfully()
    {
        // Exercises the fractional != 0 path in TryFormat(Period, ...)
        Period period = Period.FromSeconds(5) + Period.FromMilliseconds(123);
        Span<byte> buffer = stackalloc byte[90];
        Assert.True(JsonElementHelpers.TryFormatPeriod(period, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        Assert.Contains(".", result);
        Assert.Contains("S", result);
    }

    #endregion

    #region Parse failure paths (FormatException)

    [Fact]
    public void ParsePeriod_InvalidInput_ThrowsFormatException()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("not-a-period");
        Assert.Throws<FormatException>(() => JsonElementHelpers.ParsePeriod(invalid));
    }

    [Fact]
    public void ParseLocalDate_InvalidInput_ThrowsFormatException()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("not-a-date");
        Assert.Throws<FormatException>(() => JsonElementHelpers.ParseLocalDate(invalid));
    }

    [Fact]
    public void ParseOffsetTime_InvalidInput_ThrowsFormatException()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("not-a-time+00:00");
        Assert.Throws<FormatException>(() => JsonElementHelpers.ParseOffsetTime(invalid));
    }

    [Fact]
    public void ParseOffsetDateTime_InvalidInput_ThrowsFormatException()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("not-a-datetime-at-all+00:00");
        Assert.Throws<FormatException>(() => JsonElementHelpers.ParseOffsetDateTime(invalid));
    }

    [Fact]
    public void ParseOffsetDate_InvalidInput_ThrowsFormatException()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("not-a-date+00:00");
        Assert.Throws<FormatException>(() => JsonElementHelpers.ParseOffsetDate(invalid));
    }

    #endregion

    #region TryParse failure paths

    [Theory]
    [InlineData("")]
    [InlineData("2024-06")]
    [InlineData("short")]
    public void TryParseLocalDate_WrongLength_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.False(JsonElementHelpers.TryParseLocalDate(data, out _));
    }

    [Theory]
    [InlineData("2024-99-01")]
    [InlineData("2024-02-30")]
    [InlineData("abcd-ef-gh")]
    public void TryParseLocalDate_ParseDateCoreFails_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.False(JsonElementHelpers.TryParseLocalDate(data, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1:2:3")]
    [InlineData("12:34")]
    public void TryParseOffsetTime_TooShort_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.False(JsonElementHelpers.TryParseOffsetTime(data, out _));
    }

    [Theory]
    [InlineData("ab:cd:ef+00:00")]
    [InlineData("99:99:99+00:00")]
    public void TryParseOffsetTime_ParseOffsetTimeCoreFails_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.False(JsonElementHelpers.TryParseOffsetTime(data, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2024-06-07T12:34")]
    [InlineData("short")]
    public void TryParseOffsetDateTime_TooShort_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.False(JsonElementHelpers.TryParseOffsetDateTime(data, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2024-06")]
    [InlineData("short")]
    public void TryParseOffsetDate_TooShort_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.False(JsonElementHelpers.TryParseOffsetDate(data, out _));
    }

    [Theory]
    [InlineData("abcd-ef-gh+00:00")]
    [InlineData("2024-99-01+00:00")]
    public void TryParseOffsetDate_ParseDateCoreFails_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.False(JsonElementHelpers.TryParseOffsetDate(data, out _));
    }

    [Theory]
    [InlineData("2024-06-07XYZABC")]
    [InlineData("2024-06-07+99:99")]
    public void TryParseOffsetDate_ParseOffsetCoreFails_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.False(JsonElementHelpers.TryParseOffsetDate(data, out _));
    }

    #endregion

    #region OffsetDateTime non-UTC negative offset path

    [Fact]
    public void TryFormatOffsetDateTime_NegativeOffset_RoundTrips()
    {
        var dt = new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 0, 0), Offset.FromHoursAndMinutes(-5, -30));
        Span<byte> buffer = stackalloc byte[40];
        Assert.True(JsonElementHelpers.TryFormatOffsetDateTime(dt, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        Assert.Contains("-05:30", result);
        Assert.True(JsonElementHelpers.TryParseOffsetDateTime(buffer.Slice(0, bytesWritten), out OffsetDateTime parsed));
        Assert.Equal(dt, parsed);
    }

    #endregion
}
