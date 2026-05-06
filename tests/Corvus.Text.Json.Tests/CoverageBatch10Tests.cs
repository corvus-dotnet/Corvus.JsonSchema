// Copyright (c) Endjin Limited. All rights reserved.

using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 10: Numeric.Core.cs — TryParseNumber error paths,
/// CompareNormalizedJsonNumbers, and GetDigitAtPosition/GetDecimalLength.
/// </summary>
public static class CoverageBatch10Tests
{
    #region TryParseNumber — error paths (lines 54-55, 96-101, 112-117, 138-143, 167-172, 187-192)

    /// <summary>
    /// ParseNumber wraps TryParseNumber and throws on failure.
    /// Target: lines 54-55.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void ParseNumber_EmptySpan_Throws()
    {
        Assert.Throws<FormatException>(() =>
        {
            JsonElementHelpers.ParseNumber(
                ReadOnlySpan<byte>.Empty,
                out _,
                out _,
                out _,
                out _);
        });
    }

    /// <summary>
    /// TryParseNumber with empty span returns false.
    /// Target: lines 96-101.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryParseNumber_EmptySpan_ReturnsFalse()
    {
        bool result = JsonElementHelpers.TryParseNumber(
            ReadOnlySpan<byte>.Empty,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.False(result);
    }

    /// <summary>
    /// TryParseNumber with non-digit, non-minus first char returns false.
    /// Target: lines 112-117.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryParseNumber_NonDigitFirstChar_ReturnsFalse()
    {
        bool result = JsonElementHelpers.TryParseNumber(
            "abc"u8,
            out _,
            out _,
            out _,
            out _);

        Assert.False(result);
    }

    /// <summary>
    /// TryParseNumber with trailing dot (no fractional digits) returns false.
    /// Target: lines 138-143.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryParseNumber_TrailingDot_ReturnsFalse()
    {
        bool result = JsonElementHelpers.TryParseNumber(
            "1."u8,
            out _,
            out _,
            out _,
            out _);

        Assert.False(result);
    }

    /// <summary>
    /// TryParseNumber with fractional part before exponent (e.g. "1.5e2").
    /// This exercises the frac = span.Slice(0, i) path at line 154.
    /// Target: line 154-155.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryParseNumber_FractionalWithExponent_Parses()
    {
        bool result = JsonElementHelpers.TryParseNumber(
            "1.5e2"u8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.True(result);
        Assert.False(isNegative);

        // Normal form: 1.5e2 = 15 * 10^1
        // integral="1", frac="5" -> significand = "15", exp = 2 - 1(frac.len) = 1
        Assert.Equal(1, exponent);
    }

    /// <summary>
    /// TryParseNumber with invalid exponent part returns false.
    /// Target: lines 167-172.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryParseNumber_InvalidExponent_ReturnsFalse()
    {
        bool result = JsonElementHelpers.TryParseNumber(
            "1eXYZ"u8,
            out _,
            out _,
            out _,
            out _);

        Assert.False(result);
    }

    /// <summary>
    /// TryParseNumber with leading zeros in integral part (e.g. "01") returns false.
    /// Target: lines 187-192.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryParseNumber_LeadingZeros_ReturnsFalse()
    {
        bool result = JsonElementHelpers.TryParseNumber(
            "01"u8,
            out _,
            out _,
            out _,
            out _);

        Assert.False(result);
    }

    #endregion

    #region CompareNormalizedJsonNumbers (lines 302-303, 347, 362, 364-365)

    /// <summary>
    /// Different signs: left negative, right positive → -1.
    /// Target: lines 302-303.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void CompareNormalized_DifferentSigns_NegativeVsPositive()
    {
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: true,
            leftIntegral: "5"u8,
            leftFractional: default,
            leftExponent: 0,
            rightIsNegative: false,
            rightIntegral: "3"u8,
            rightFractional: default,
            rightExponent: 0);

        Assert.Equal(-1, result);
    }

    /// <summary>
    /// Different signs: left positive, right negative → 1.
    /// Target: lines 302-303.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void CompareNormalized_DifferentSigns_PositiveVsNegative()
    {
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: false,
            leftIntegral: "3"u8,
            leftFractional: default,
            leftExponent: 0,
            rightIsNegative: true,
            rightIntegral: "5"u8,
            rightFractional: default,
            rightExponent: 0);

        Assert.Equal(1, result);
    }

    /// <summary>
    /// Equal numbers → 0.
    /// Target: line 347.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void CompareNormalized_EqualNumbers_ReturnsZero()
    {
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: false,
            leftIntegral: "42"u8,
            leftFractional: default,
            leftExponent: 0,
            rightIsNegative: false,
            rightIntegral: "42"u8,
            rightFractional: default,
            rightExponent: 0);

        Assert.Equal(0, result);
    }

    /// <summary>
    /// Numbers with different digit lengths but same effective length,
    /// exercising the digit-by-digit comparison into the fractional part.
    /// Target: lines 362, 364-365 (GetDigitAtPosition fractional branch).
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void CompareNormalized_DifferentDigits_InFractionalPart()
    {
        // Left = 1.23 normalized: integral="1", frac="23", exp=-2
        // Right = 1.24 normalized: integral="1", frac="24", exp=-2
        // They differ in the fractional part → left < right
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: false,
            leftIntegral: "1"u8,
            leftFractional: "23"u8,
            leftExponent: -2,
            rightIsNegative: false,
            rightIntegral: "1"u8,
            rightFractional: "24"u8,
            rightExponent: -2);

        Assert.Equal(-1, result);
    }

    /// <summary>
    /// Numbers where left has fewer fractional digits, exercising the
    /// GetDigitAtPosition path that returns '0' for out-of-range fractional indices.
    /// Target: lines 364-365 (fractionalIndex >= fractional.Length → '0').
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void CompareNormalized_UnequalFractionalLengths()
    {
        // Left = integral="1", frac="2", exp=-1  → 1.2
        // Right = integral="1", frac="20", exp=-2 → 1.20 (same value)
        // GetDigitAtPosition for left at fractional index 1 returns '0'
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: false,
            leftIntegral: "1"u8,
            leftFractional: "2"u8,
            leftExponent: -1,
            rightIsNegative: false,
            rightIntegral: "1"u8,
            rightFractional: "20"u8,
            rightExponent: -2);

        Assert.Equal(0, result);
    }

    #endregion

    #region GetDecimalLength (lines 374-377)

    /// <summary>
    /// CompareNormalizedJsonNumbers with different effective lengths
    /// exercises GetDecimalLength through the effectiveLength comparison.
    /// Target: lines 374-377 (called via lines 312-313).
    /// This also hits the effectiveLength inequality at lines 315-317.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void CompareNormalized_DifferentEffectiveLengths()
    {
        // Left = "1" with exp=2 → 100 (effectiveLength = 1+2 = 3)
        // Right = "1" with exp=0 → 1 (effectiveLength = 1+0 = 1)
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: false,
            leftIntegral: "1"u8,
            leftFractional: default,
            leftExponent: 2,
            rightIsNegative: false,
            rightIntegral: "1"u8,
            rightFractional: default,
            rightExponent: 0);

        Assert.Equal(1, result);
    }

    /// <summary>
    /// Both negative, left has larger magnitude → left is more negative → returns -1.
    /// This exercises the signMultiplier=-1 path at line 306.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void CompareNormalized_BothNegative_DifferentMagnitude()
    {
        // Left = -100 (integral="1", exp=2), Right = -1 (integral="1", exp=0)
        // Both negative. Left has larger magnitude → left is "less than" right.
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: true,
            leftIntegral: "1"u8,
            leftFractional: default,
            leftExponent: 2,
            rightIsNegative: true,
            rightIntegral: "1"u8,
            rightFractional: default,
            rightExponent: 0);

        // signMultiplier=-1 flips the comparison: 3 > 1 → 1 * -1 = -1
        Assert.Equal(-1, result);
    }

    #endregion

    #region GetDecimalLength via TryFormatHexadecimal (lines 374-377)

    /// <summary>
    /// TryFormatHexadecimal calls GetDecimalLength for non-negative integers.
    /// Target: lines 374-377.
    /// </summary>
    [Fact]
    [Trait("category", "coverage")]
    public static void TryFormatHexadecimal_PositiveInteger_CallsGetDecimalLength()
    {
        Span<char> destination = stackalloc char[32];

        // 255 decimal = FF hex. Normalized: integral="255", frac=empty, exp=0.
        bool result = JsonElementHelpers.TryFormatHexadecimal(
            isNegative: false,
            integral: "255"u8,
            fractional: default,
            exponent: 0,
            destination,
            out int charsWritten,
            precision: 0,
            lowercase: true);

        Assert.True(result);
        Assert.Equal("ff", destination.Slice(0, charsWritten).ToString());
    }

    #endregion
}
