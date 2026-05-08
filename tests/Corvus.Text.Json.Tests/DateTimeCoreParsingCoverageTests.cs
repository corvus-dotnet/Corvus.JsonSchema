// Copyright (c) Endjin Limited. All rights reserved.

using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for JsonElementHelpers.DateTime.Core.cs targeting ParseDateCore,
/// ParseTimeCore, ParseOffsetCore, and ParseOffsetTimeCore.
/// </summary>
[TestClass]
public class DateTimeCoreParsingCoverageTests
{
    #region ParseDateCore - success paths (lines 49-52)

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseDateCore_ValidDate_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "2024-03-15"u8;
        bool result = JsonElementHelpers.ParseDateCore(text, out int year, out int month, out int day);

        Assert.IsTrue(result);
        Assert.AreEqual(2024, year);
        Assert.AreEqual(3, month);
        Assert.AreEqual(15, day);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseDateCore_LeapYearDate_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "2000-02-29"u8;
        bool result = JsonElementHelpers.ParseDateCore(text, out int year, out int month, out int day);

        Assert.IsTrue(result);
        Assert.AreEqual(2000, year);
        Assert.AreEqual(2, month);
        Assert.AreEqual(29, day);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseDateCore_MinDate_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "0001-01-01"u8;
        bool result = JsonElementHelpers.ParseDateCore(text, out int year, out int month, out int day);

        Assert.IsTrue(result);
        Assert.AreEqual(1, year);
        Assert.AreEqual(1, month);
        Assert.AreEqual(1, day);
    }

    #endregion

    #region ParseDateCore - failure paths (lines 31-46)

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseDateCore_MissingFirstDash_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "2024X03-15"u8;
        bool result = JsonElementHelpers.ParseDateCore(text, out int year, out int month, out int day);

        Assert.IsFalse(result);
        Assert.AreEqual(0, year);
        Assert.AreEqual(0, month);
        Assert.AreEqual(0, day);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseDateCore_MissingSecondDash_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "2024-03X15"u8;
        bool result = JsonElementHelpers.ParseDateCore(text, out int year, out int month, out int day);

        Assert.IsFalse(result);
        Assert.AreEqual(0, year);
        Assert.AreEqual(0, month);
        Assert.AreEqual(0, day);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseDateCore_NonNumericYear_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "20A4-03-15"u8;
        bool result = JsonElementHelpers.ParseDateCore(text, out int year, out int month, out int day);

        Assert.IsFalse(result);
        Assert.AreEqual(0, year);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseDateCore_NonNumericMonth_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "2024-0A-15"u8;
        bool result = JsonElementHelpers.ParseDateCore(text, out int year, out int month, out int day);

        Assert.IsFalse(result);
        Assert.AreEqual(0, year);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseDateCore_NonNumericDay_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "2024-03-A5"u8;
        bool result = JsonElementHelpers.ParseDateCore(text, out int year, out int month, out int day);

        Assert.IsFalse(result);
        Assert.AreEqual(0, year);
    }

    #endregion

    #region ParseTimeCore - success paths (lines 192-275)

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_WholeSeconds_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "14:30:59"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsTrue(result);
        Assert.AreEqual(14, hours);
        Assert.AreEqual(30, minutes);
        Assert.AreEqual(59, seconds);
        Assert.AreEqual(0, ms);
        Assert.AreEqual(0, us);
        Assert.AreEqual(0, ns);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_WithMilliseconds_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "14:30:59.123"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsTrue(result);
        Assert.AreEqual(14, hours);
        Assert.AreEqual(30, minutes);
        Assert.AreEqual(59, seconds);
        Assert.AreEqual(123, ms);
        Assert.AreEqual(0, us);
        Assert.AreEqual(0, ns);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_WithMicroseconds_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "14:30:59.123456"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsTrue(result);
        Assert.AreEqual(14, hours);
        Assert.AreEqual(30, minutes);
        Assert.AreEqual(59, seconds);
        Assert.AreEqual(123, ms);
        Assert.AreEqual(456, us);
        Assert.AreEqual(0, ns);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_WithNanoseconds_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "14:30:59.123456789"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsTrue(result);
        Assert.AreEqual(14, hours);
        Assert.AreEqual(30, minutes);
        Assert.AreEqual(59, seconds);
        Assert.AreEqual(123, ms);
        Assert.AreEqual(456, us);
        Assert.AreEqual(789, ns);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_Midnight_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "00:00:00"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsTrue(result);
        Assert.AreEqual(0, hours);
        Assert.AreEqual(0, minutes);
        Assert.AreEqual(0, seconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_PartialMilliseconds_ReturnsTrue()
    {
        // Only 1 digit of milliseconds
        ReadOnlySpan<byte> text = "14:30:59.1"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsTrue(result);
        Assert.AreEqual(100, ms); // 1 * 100
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_TwoDigitMilliseconds_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "14:30:59.12"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsTrue(result);
        Assert.AreEqual(120, ms); // 1*100 + 2*10
    }

    #endregion

    #region ParseTimeCore - failure paths (lines 172-189, 200-211, 219-227, 238-246, 257-265, 278-284)

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_TooLong_ReturnsFalse()
    {
        // text.Length > 18 triggers the first validation check
        ReadOnlySpan<byte> text = "14:30:59.1234567890"u8; // 19 chars
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_MissingFirstColon_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "14X30:59"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_MissingSecondColon_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "14:30X59"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_NonNumericHour_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "1A:30:59"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_NonNumericMinute_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "14:3A:59"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_MissingDotBeforeFractional_ReturnsFalse()
    {
        // 9 chars but no dot at position 8
        ReadOnlySpan<byte> text = "14:30:591"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_DotButNoDigits_ReturnsFalse()
    {
        // Dot at position 8 but nothing after (text.Length <= 9)
        ReadOnlySpan<byte> text = "14:30:59."u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_NonNumericMillisecond_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "14:30:59.1A3"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_NonNumericMicrosecond_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "14:30:59.123A56"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_NonNumericNanosecond_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "14:30:59.123456A89"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_HoursOutOfRange_ReturnsFalse()
    {
        // hours >= 24
        ReadOnlySpan<byte> text = "24:30:59"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_MinutesOutOfRange_ReturnsFalse()
    {
        // minutes >= 60
        ReadOnlySpan<byte> text = "14:60:59"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseTimeCore_SecondsOutOfRange_ReturnsFalse()
    {
        // seconds >= 60
        ReadOnlySpan<byte> text = "14:30:60"u8;
        bool result = JsonElementHelpers.ParseTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    #endregion

    #region ParseOffsetCore - success paths (lines 116-157)

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_Zulu_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "Z"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_LowerZ_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "z"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_PositiveHoursMinutes_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "+05:30"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual((5 * 3600) + (30 * 60), offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_NegativeHoursMinutes_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "-08:00"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual(-8 * 3600, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_HoursOnly_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "+05"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual(5 * 3600, offsetSeconds);
    }

    #endregion

    #region ParseOffsetCore - failure paths (lines 119, 124-131, 137-140, 146-149)

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_ZWithTrailing_ReturnsFalse()
    {
        // Z followed by extra chars: text.Length != 1
        ReadOnlySpan<byte> text = "Z0"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_InvalidLength_ReturnsFalse()
    {
        // After sign, need exactly 2 or 5 chars
        ReadOnlySpan<byte> text = "+053"u8; // 3 chars after sign — invalid
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_NonNumericHour_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "+A5:30"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_MissingColon_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "+05X30"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_HoursOver18_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "+19:00"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_MinutesOver59_ReturnsFalse()
    {
        ReadOnlySpan<byte> text = "+05:60"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetCore_TotalExceeds14Hours_ReturnsFalse()
    {
        // +14:01 = 14*3600+60 = 50460 > 50400
        ReadOnlySpan<byte> text = "+14:01"u8;
        bool result = JsonElementHelpers.ParseOffsetCore(text, out int offsetSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, offsetSeconds);
    }

    #endregion

    #region ParseOffsetTimeCore - success paths (lines 71-105)

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetTimeCore_WithZulu_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "14:30:59Z"u8;
        bool result = JsonElementHelpers.ParseOffsetTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns, out int offsetSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual(14, hours);
        Assert.AreEqual(30, minutes);
        Assert.AreEqual(59, seconds);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetTimeCore_WithPositiveOffset_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "14:30:59+05:30"u8;
        bool result = JsonElementHelpers.ParseOffsetTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns, out int offsetSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual(14, hours);
        Assert.AreEqual(30, minutes);
        Assert.AreEqual(59, seconds);
        Assert.AreEqual((5 * 3600) + (30 * 60), offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetTimeCore_WithNegativeOffset_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "14:30:59-08:00"u8;
        bool result = JsonElementHelpers.ParseOffsetTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns, out int offsetSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual(14, hours);
        Assert.AreEqual(-8 * 3600, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetTimeCore_WithFractionalAndOffset_ReturnsTrue()
    {
        ReadOnlySpan<byte> text = "14:30:59.123+05:30"u8;
        bool result = JsonElementHelpers.ParseOffsetTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns, out int offsetSeconds);

        Assert.IsTrue(result);
        Assert.AreEqual(123, ms);
        Assert.AreEqual((5 * 3600) + (30 * 60), offsetSeconds);
    }

    #endregion

    #region ParseOffsetTimeCore - failure paths (lines 73-82, 88-91, 94-102)

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetTimeCore_NoOffset_ReturnsFalse()
    {
        // No offset marker (Z, +, -) found after position 8
        ReadOnlySpan<byte> text = "14:30:59.123"u8;
        bool result = JsonElementHelpers.ParseOffsetTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns, out int offsetSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetTimeCore_InvalidTimePart_ReturnsFalse()
    {
        // Time part is invalid, offset part is valid
        ReadOnlySpan<byte> text = "14:30:5AZ"u8;
        bool result = JsonElementHelpers.ParseOffsetTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns, out int offsetSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, offsetSeconds);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetTimeCore_InvalidOffsetPart_ReturnsFalse()
    {
        // Time part is valid, offset part is invalid
        ReadOnlySpan<byte> text = "14:30:59+19:00"u8;
        bool result = JsonElementHelpers.ParseOffsetTimeCore(
            text, out int hours, out int minutes, out int seconds,
            out int ms, out int us, out int ns, out int offsetSeconds);

        Assert.IsFalse(result);
        Assert.AreEqual(0, hours);
    }

    #endregion
}
