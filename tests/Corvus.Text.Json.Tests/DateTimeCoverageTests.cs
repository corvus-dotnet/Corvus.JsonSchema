// <copyright file="DateTimeCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using NodaTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for NodaTime date/time formatting and parsing in <see cref="JsonElementHelpers"/>.
/// Targets specific uncovered code paths identified via Cobertura coverage reports.
/// </summary>
[TestClass]
public class DateTimeCoverageTests
{
    #region TryFormat buffer-too-small paths

    [TestMethod]
    public void TryFormatLocalDate_BufferTooSmall_ReturnsFalse()
    {
        var date = new LocalDate(2024, 6, 7);
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(JsonElementHelpers.TryFormatLocalDate(date, buffer, out int bytesWritten));
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatOffsetDate_BufferTooSmall_ReturnsFalse()
    {
        var date = new OffsetDate(new LocalDate(2024, 6, 7), Offset.FromHours(2));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(JsonElementHelpers.TryFormatOffsetDate(date, buffer, out int bytesWritten));
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatOffsetTime_BufferTooSmall_ReturnsFalse()
    {
        var t = new OffsetTime(new LocalTime(12, 34, 56), Offset.FromHours(1));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(JsonElementHelpers.TryFormatOffsetTime(t, buffer, out int bytesWritten));
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatOffsetDateTime_BufferTooSmall_ReturnsFalse()
    {
        var dt = new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 34, 56), Offset.FromHours(1));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(JsonElementHelpers.TryFormatOffsetDateTime(dt, buffer, out int bytesWritten));
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatPeriod_BufferTooSmall_ReturnsFalse()
    {
        Period period = Period.FromDays(5) + Period.FromHours(3);
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(JsonElementHelpers.TryFormatPeriod(period, buffer, out int bytesWritten));
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region OffsetDate/OffsetTime negative offset paths

    [TestMethod]
    public void TryFormatOffsetDate_NegativeOffset_ContainsMinusSign()
    {
        var date = new OffsetDate(new LocalDate(2024, 6, 7), Offset.FromHours(-5));
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(JsonElementHelpers.TryFormatOffsetDate(date, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        StringAssert.Contains(result, "-05:00");
    }

    [TestMethod]
    public void TryFormatOffsetDate_NegativeOffset_RoundTrips()
    {
        var date = new OffsetDate(new LocalDate(2024, 1, 15), Offset.FromHoursAndMinutes(-9, -30));
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(JsonElementHelpers.TryFormatOffsetDate(date, buffer, out int bytesWritten));
        Assert.IsTrue(JsonElementHelpers.TryParseOffsetDate(buffer.Slice(0, bytesWritten), out OffsetDate parsed));
        Assert.AreEqual(date, parsed);
    }

    [TestMethod]
    public void TryFormatOffsetTime_NegativeOffset_ContainsMinusSign()
    {
        var t = new OffsetTime(new LocalTime(8, 30, 0), Offset.FromHours(-3));
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(JsonElementHelpers.TryFormatOffsetTime(t, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        StringAssert.Contains(result, "-03:00");
    }

    [TestMethod]
    public void TryFormatOffsetTime_NegativeOffset_RoundTrips()
    {
        var t = new OffsetTime(new LocalTime(14, 0, 0, 500), Offset.FromHoursAndMinutes(-5, -30));
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(JsonElementHelpers.TryFormatOffsetTime(t, buffer, out int bytesWritten));
        Assert.IsTrue(JsonElementHelpers.TryParseOffsetTime(buffer.Slice(0, bytesWritten), out OffsetTime parsed));
        Assert.AreEqual(t, parsed);
    }

    #endregion

    #region OffsetDate non-UTC positive offset path

    [TestMethod]
    public void TryFormatOffsetDate_PositiveNonUtcOffset_FormatsCorrectly()
    {
        var date = new OffsetDate(new LocalDate(2024, 12, 25), Offset.FromHoursAndMinutes(5, 45));
        Span<byte> buffer = stackalloc byte[32];
        Assert.IsTrue(JsonElementHelpers.TryFormatOffsetDate(date, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        Assert.AreEqual("2024-12-25+05:45", result);
    }

    #endregion

    #region Period with time components (seconds and fractional)

    [TestMethod]
    public void TryFormatPeriod_WithSeconds_RoundTrips()
    {
        // Exercises the seconds/fractional writing path in TryFormat(Period, ...)
        Period period = Period.FromHours(2) + Period.FromMinutes(15) + Period.FromSeconds(45);
        Span<byte> buffer = stackalloc byte[90];
        Assert.IsTrue(JsonElementHelpers.TryFormatPeriod(period, buffer, out int bytesWritten));
        Assert.IsTrue(JsonElementHelpers.TryParsePeriod(buffer.Slice(0, bytesWritten), out Period parsed));
        Assert.AreEqual(period.Normalize(), parsed.Normalize());
    }

    [TestMethod]
    public void TryFormatPeriod_WithNegativeSeconds_FormatsSuccessfully()
    {
        // Exercises the negative sign path for seconds (integral < 0)
        Period period = Period.FromSeconds(-30);
        Span<byte> buffer = stackalloc byte[90];
        Assert.IsTrue(JsonElementHelpers.TryFormatPeriod(period, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        StringAssert.Contains(result, "-");
        StringAssert.Contains(result, "S");
    }

    [TestMethod]
    public void TryFormatPeriod_WithMilliseconds_FormatsSuccessfully()
    {
        // Exercises the fractional != 0 path in TryFormat(Period, ...)
        Period period = Period.FromSeconds(5) + Period.FromMilliseconds(123);
        Span<byte> buffer = stackalloc byte[90];
        Assert.IsTrue(JsonElementHelpers.TryFormatPeriod(period, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        StringAssert.Contains(result, ".");
        StringAssert.Contains(result, "S");
    }

    #endregion

    #region Parse failure paths (FormatException)

    [TestMethod]
    public void ParsePeriod_InvalidInput_ThrowsFormatException()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("not-a-period");
        Assert.ThrowsExactly<FormatException>(() => JsonElementHelpers.ParsePeriod(invalid));
    }

    [TestMethod]
    public void ParseLocalDate_InvalidInput_ThrowsFormatException()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("not-a-date");
        Assert.ThrowsExactly<FormatException>(() => JsonElementHelpers.ParseLocalDate(invalid));
    }

    [TestMethod]
    public void ParseOffsetTime_InvalidInput_ThrowsFormatException()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("not-a-time+00:00");
        Assert.ThrowsExactly<FormatException>(() => JsonElementHelpers.ParseOffsetTime(invalid));
    }

    [TestMethod]
    public void ParseOffsetDateTime_InvalidInput_ThrowsFormatException()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("not-a-datetime-at-all+00:00");
        Assert.ThrowsExactly<FormatException>(() => JsonElementHelpers.ParseOffsetDateTime(invalid));
    }

    [TestMethod]
    public void ParseOffsetDate_InvalidInput_ThrowsFormatException()
    {
        byte[] invalid = Encoding.UTF8.GetBytes("not-a-date+00:00");
        Assert.ThrowsExactly<FormatException>(() => JsonElementHelpers.ParseOffsetDate(invalid));
    }

    #endregion

    #region TryParse failure paths

    [TestMethod]
    [DataRow("")]
    [DataRow("2024-06")]
    [DataRow("short")]
    public void TryParseLocalDate_WrongLength_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseLocalDate(data, out _));
    }

    [TestMethod]
    [DataRow("2024-99-01")]
    [DataRow("2024-02-30")]
    [DataRow("abcd-ef-gh")]
    public void TryParseLocalDate_ParseDateCoreFails_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseLocalDate(data, out _));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("1:2:3")]
    [DataRow("12:34")]
    public void TryParseOffsetTime_TooShort_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetTime(data, out _));
    }

    [TestMethod]
    [DataRow("ab:cd:ef+00:00")]
    [DataRow("99:99:99+00:00")]
    public void TryParseOffsetTime_ParseOffsetTimeCoreFails_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetTime(data, out _));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("2024-06-07T12:34")]
    [DataRow("short")]
    public void TryParseOffsetDateTime_TooShort_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetDateTime(data, out _));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("2024-06")]
    [DataRow("short")]
    public void TryParseOffsetDate_TooShort_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetDate(data, out _));
    }

    [TestMethod]
    [DataRow("abcd-ef-gh+00:00")]
    [DataRow("2024-99-01+00:00")]
    public void TryParseOffsetDate_ParseDateCoreFails_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetDate(data, out _));
    }

    [TestMethod]
    [DataRow("2024-06-07XYZABC")]
    [DataRow("2024-06-07+99:99")]
    public void TryParseOffsetDate_ParseOffsetCoreFails_ReturnsFalse(string input)
    {
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetDate(data, out _));
    }

    #endregion

    #region OffsetDateTime non-UTC negative offset path

    [TestMethod]
    public void TryFormatOffsetDateTime_NegativeOffset_RoundTrips()
    {
        var dt = new OffsetDateTime(new LocalDateTime(2024, 6, 7, 12, 0, 0), Offset.FromHoursAndMinutes(-5, -30));
        Span<byte> buffer = stackalloc byte[40];
        Assert.IsTrue(JsonElementHelpers.TryFormatOffsetDateTime(dt, buffer, out int bytesWritten));
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten));
        StringAssert.Contains(result, "-05:30");
        Assert.IsTrue(JsonElementHelpers.TryParseOffsetDateTime(buffer.Slice(0, bytesWritten), out OffsetDateTime parsed));
        Assert.AreEqual(dt, parsed);
    }

    #endregion

    #region ParseTimeCore error paths

    [TestMethod]
    [DataRow("12:34:56X+00:00")]  // text[8] != '.' but length > 8
    [DataRow("12:34:56/+00:00")]  // text[8] is '/' not '.'
    public void TryParseOffsetTime_FractionalNotDot_ReturnsFalse(string input)
    {
        // Exercises ParseTimeCore lines 203-211: text[8] != '.' error path
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetTime(data, out _));
    }

    [TestMethod]
    [DataRow("12:34:56.X23+00:00")]  // non-numeric first ms digit
    [DataRow("12:34:56.1X3+00:00")]  // non-numeric second ms digit
    [DataRow("12:34:56.12X+00:00")]  // non-numeric third ms digit
    public void TryParseOffsetTime_NonNumericMillisecond_ReturnsFalse(string input)
    {
        // Exercises ParseTimeCore lines 219-227: non-numeric ms digit error path
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetTime(data, out _));
    }

    [TestMethod]
    [DataRow("12:34:56.123X56+00:00")]  // non-numeric first μs digit
    [DataRow("12:34:56.1231X6+00:00")]  // non-numeric second μs digit (total length offset)
    [DataRow("12:34:56.12312X+00:00")]  // non-numeric third μs digit
    public void TryParseOffsetTime_NonNumericMicrosecond_ReturnsFalse(string input)
    {
        // Exercises ParseTimeCore lines 238-246: non-numeric μs digit error path
        byte[] data = Encoding.UTF8.GetBytes(input);
        Assert.IsFalse(JsonElementHelpers.TryParseOffsetTime(data, out _));
    }

    #endregion
}
