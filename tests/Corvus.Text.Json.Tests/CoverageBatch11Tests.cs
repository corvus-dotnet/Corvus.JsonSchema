// Copyright (c) Endjin Limited. All rights reserved.

using Corvus.Text.Json.Internal;
using NodaTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 11: DateTime.cs Parse* success paths, JsonReaderHelper TryGetValue
/// "too long" guards, and TryEncodePointer/TryUnescapeAndEncodePointer buffer overflow paths.
/// </summary>
[TestClass]
public class CoverageBatch11Tests
{
    #region DateTime.cs Parse* methods — success paths

    /// <summary>
    /// ParsePeriod with valid ISO 8601 period.
    /// Target: DateTime.cs line 101 (return value).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParsePeriod_ValidInput_ReturnsValue()
    {
        Period result = JsonElementHelpers.ParsePeriod("P1Y2M3D"u8);
        Assert.AreEqual(1, result.Years);
        Assert.AreEqual(2, result.Months);
        Assert.AreEqual(3, result.Days);
    }

    /// <summary>
    /// ParseLocalDate with valid ISO 8601 date.
    /// Target: DateTime.cs line 130 (return value).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParseLocalDate_ValidInput_ReturnsValue()
    {
        LocalDate result = JsonElementHelpers.ParseLocalDate("2024-03-15"u8);
        Assert.AreEqual(2024, result.Year);
        Assert.AreEqual(3, result.Month);
        Assert.AreEqual(15, result.Day);
    }

    /// <summary>
    /// ParseOffsetTime with valid ISO 8601 offset time.
    /// Target: DateTime.cs line 172 (return value).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetTime_ValidInput_ReturnsValue()
    {
        OffsetTime result = JsonElementHelpers.ParseOffsetTime("10:30:00Z"u8);
        Assert.AreEqual(10, result.TimeOfDay.Hour);
        Assert.AreEqual(30, result.TimeOfDay.Minute);
    }

    /// <summary>
    /// ParseOffsetDateTime with valid ISO 8601 offset date time.
    /// Target: DateTime.cs line 264 (return value).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetDateTime_ValidInput_ReturnsValue()
    {
        OffsetDateTime result = JsonElementHelpers.ParseOffsetDateTime("2024-03-15T10:30:00Z"u8);
        Assert.AreEqual(2024, result.Year);
        Assert.AreEqual(10, result.Hour);
    }

    /// <summary>
    /// ParseOffsetDate with valid ISO 8601 offset date.
    /// Target: DateTime.cs line 378 (return value).
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void ParseOffsetDate_ValidInput_ReturnsValue()
    {
        OffsetDate result = JsonElementHelpers.ParseOffsetDate("2024-03-15Z"u8);
        Assert.AreEqual(2024, result.Date.Year);
        Assert.AreEqual(3, result.Date.Month);
    }

    #endregion

    #region JsonReaderHelper.TryGetValue — "too long" segment guards

    /// <summary>
    /// TryGetValue(OffsetDate) with segment exceeding max parse length.
    /// Target: JsonReaderHelper.cs lines 190-192.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void TryGetValue_OffsetDate_TooLong_ReturnsFalse()
    {
        // MaximumEscapedDateTimeOffsetParseLength = 6 * (33 + 9) = 252
        // A segment of 260 bytes exceeds the max
        byte[] segment = new byte[260];
        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: false, out OffsetDate _);
        Assert.IsFalse(result);
    }

    /// <summary>
    /// TryGetValue(OffsetTime) with segment exceeding max parse length.
    /// Target: JsonReaderHelper.cs lines 215-217.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void TryGetValue_OffsetTime_TooLong_ReturnsFalse()
    {
        byte[] segment = new byte[260];
        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: false, out OffsetTime _);
        Assert.IsFalse(result);
    }

    /// <summary>
    /// TryGetValue(LocalDate) with segment exceeding max parse length.
    /// Target: JsonReaderHelper.cs lines 240-242.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void TryGetValue_LocalDate_TooLong_ReturnsFalse()
    {
        byte[] segment = new byte[260];
        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: false, out LocalDate _);
        Assert.IsFalse(result);
    }

    /// <summary>
    /// TryGetValue(Period) with segment exceeding max parse length.
    /// Target: JsonReaderHelper.cs lines 265-267.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void TryGetValue_Period_TooLong_ReturnsFalse()
    {
        byte[] segment = new byte[260];
        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: false, out Period _);
        Assert.IsFalse(result);
    }

    #endregion

    #region TryEncodePointer — destination too small (lines 430-432)

    /// <summary>
    /// TryEncodePointer with destination buffer too small.
    /// Target: JsonReaderHelper.cs lines 430-432.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void TryEncodePointer_DestinationTooSmall_ReturnsFalse()
    {
        ReadOnlySpan<byte> input = "hello"u8;
        Span<byte> destination = stackalloc byte[2]; // Too small for 5-byte input
        bool result = JsonReaderHelper.TryEncodePointer(input, destination, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region TryUnescapeAndEncodePointer — first TryEncodePointer fails (lines 376-378)

    /// <summary>
    /// TryUnescapeAndEncodePointer where the first segment (before backslash) is too
    /// long for the destination, causing the first TryEncodePointer to fail.
    /// Target: JsonReaderHelper.cs lines 376-378.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void TryUnescapeAndEncodePointer_FirstEncodeFailsDueToSmallBuffer()
    {
        // Input: "abcde\n" — 5 chars before the backslash + escape sequence
        // Destination too small even for the first segment
        byte[] input = "abcde\\n"u8.ToArray();
        Span<byte> destination = stackalloc byte[2]; // Too small for "abcde"
        bool result = JsonReaderHelper.TryUnescapeAndEncodePointer(input, destination, out int written);
        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion
}
