// Copyright (c) Endjin Limited. All rights reserved.

using NodaTime.Text;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for <see cref="Utf8ValueCursor"/> targeting ParseInt64 overflow
/// and boundary paths.
/// </summary>
public static class Utf8ValueCursorCoverageTests
{
    [Fact]
    [Trait("category", "coverage")]
    public static void Constructor_SetsPropertiesCorrectly()
    {
        ReadOnlySpan<byte> value = "123"u8;
        Utf8ValueCursor cursor = new(value);
        Assert.Equal(3, cursor.Length);
        Assert.Equal(-1, cursor.Index);
        Assert.Equal(Utf8ValueCursor.Nul, cursor.Current);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void MoveNext_AdvancesThroughValue()
    {
        ReadOnlySpan<byte> value = "AB"u8;
        Utf8ValueCursor cursor = new(value);

        Assert.True(cursor.MoveNext());
        Assert.Equal(0, cursor.Index);
        Assert.Equal((byte)'A', cursor.Current);

        Assert.True(cursor.MoveNext());
        Assert.Equal(1, cursor.Index);
        Assert.Equal((byte)'B', cursor.Current);

        Assert.False(cursor.MoveNext());
        Assert.Equal(2, cursor.Index);
        Assert.Equal(Utf8ValueCursor.Nul, cursor.Current);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void MoveNext_EmptyValue_ReturnsFalse()
    {
        ReadOnlySpan<byte> value = ""u8;
        Utf8ValueCursor cursor = new(value);
        Assert.False(cursor.MoveNext());
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_SimpleNumber()
    {
        ReadOnlySpan<byte> value = "12345"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.True(cursor.ParseInt64(out long result));
        Assert.Equal(12345L, result);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_SingleDigit()
    {
        ReadOnlySpan<byte> value = "7"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.True(cursor.ParseInt64(out long result));
        Assert.Equal(7L, result);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_StopsAtNonDigit()
    {
        ReadOnlySpan<byte> value = "123X"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.True(cursor.ParseInt64(out long result));
        Assert.Equal(123L, result);
        // Cursor should be at 'X'
        Assert.Equal((byte)'X', cursor.Current);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_NoDigits_ReturnsFalse()
    {
        ReadOnlySpan<byte> value = "ABC"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.False(cursor.ParseInt64(out _));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_MaxValue_ExactlyLong()
    {
        // long.MaxValue = 9223372036854775807
        ReadOnlySpan<byte> value = "9223372036854775807"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.True(cursor.ParseInt64(out long result));
        Assert.Equal(long.MaxValue, result);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_OverflowLastDigit_ReturnsFalse()
    {
        // 9223372036854775808 — last digit 8 > 7, so it overflows
        ReadOnlySpan<byte> value = "9223372036854775808"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.False(cursor.ParseInt64(out _));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_OverflowPenultimate_ReturnsFalse()
    {
        // 9223372036854775810 — the prefix > 922337203685477580, overflow detected
        // Actually need a number where result > 922337203685477580 before the last digit
        // 9223372036854775900 — the first 18 digits = 922337203685477590 > threshold
        ReadOnlySpan<byte> value = "9223372036854775900"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.False(cursor.ParseInt64(out _));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_TooManyDigits_ReturnsFalse()
    {
        // 20 digits — even if numeric value is small, there are too many digits
        // The 19th digit causes the "too many digits" branch
        // Use: 92233720368547758070 (max followed by a '0')
        ReadOnlySpan<byte> value = "92233720368547758070"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.False(cursor.ParseInt64(out _));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_ExactThreshold_WithTrailingNonDigit()
    {
        // 922337203685477580 (the threshold value exactly) followed by a non-digit
        // Should succeed with result = 922337203685477580
        ReadOnlySpan<byte> value = "922337203685477580X"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.True(cursor.ParseInt64(out long result));
        Assert.Equal(922337203685477580L, result);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_JustBelowThreshold_Succeeds()
    {
        // 922337203685477579 — just below the threshold, 19 digits, last digit < 8
        ReadOnlySpan<byte> value = "922337203685477579"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.True(cursor.ParseInt64(out long result));
        Assert.Equal(922337203685477579L, result);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_ThresholdPlusLastDigit7_Succeeds()
    {
        // 9223372036854775807 = max long, last digit exactly 7
        ReadOnlySpan<byte> value = "9223372036854775807"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.True(cursor.ParseInt64(out long result));
        Assert.Equal(9223372036854775807L, result);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_ThresholdPlusLastDigit0_Succeeds()
    {
        // 9223372036854775800 — threshold exactly, last digit 0
        ReadOnlySpan<byte> value = "9223372036854775800"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.True(cursor.ParseInt64(out long result));
        Assert.Equal(9223372036854775800L, result);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ParseInt64_ThresholdPlusLastDigitFollowedByStop()
    {
        // 9223372036854775805X — threshold, last digit 5, then non-digit
        ReadOnlySpan<byte> value = "9223372036854775805X"u8;
        Utf8ValueCursor cursor = new(value);
        cursor.MoveNext();

        Assert.True(cursor.ParseInt64(out long result));
        Assert.Equal(9223372036854775805L, result);
        Assert.Equal((byte)'X', cursor.Current);
    }
}
