// <copyright file="NumericCoreOperationsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for core numeric operations: TryParseNumber, TryGetNormalizedInt64,
/// CompareNormalizedJsonNumbers, AreEqualJsonNumbers, IsMultipleOf.
/// </summary>
public class NumericCoreOperationsTests
{
    #region TryParseNumber - error paths

    [Fact]
    public void TryParseNumber_EmptySpan_ReturnsFalse()
    {
        bool result = JsonElementHelpers.TryParseNumber(
            ReadOnlySpan<byte>.Empty,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.False(result);
    }

    [Theory]
    [InlineData("abc")]    // starts with non-digit non-minus
    [InlineData("+1")]     // leading plus not valid
    [InlineData(" 1")]     // leading space
    [InlineData("x")]      // single non-digit
    public void TryParseNumber_InvalidFirstChar_ReturnsFalse(string input)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        bool result = JsonElementHelpers.TryParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.False(result);
    }

    [Theory]
    [InlineData("1.")]       // trailing dot, no fractional digits
    [InlineData("-1.")]      // negative trailing dot
    public void TryParseNumber_TrailingDot_ReturnsFalse(string input)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        bool result = JsonElementHelpers.TryParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.False(result);
    }

    [Theory]
    [InlineData("1e")]       // exponent with no value
    [InlineData("1eabc")]    // exponent with non-numeric
    [InlineData("1.5e")]     // fractional with empty exponent
    [InlineData("1.5eX")]    // fractional with invalid exponent
    public void TryParseNumber_InvalidExponent_ReturnsFalse(string input)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        bool result = JsonElementHelpers.TryParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.False(result);
    }

    [Theory]
    [InlineData("00")]       // leading zeros in integral
    [InlineData("007")]      // leading zeros
    public void TryParseNumber_LeadingZeros_ReturnsFalse(string input)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        bool result = JsonElementHelpers.TryParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.False(result);
    }

    #endregion

    #region TryParseNumber - normalization

    [Theory]
    [InlineData("0", false, "", "", 0)]            // zero
    [InlineData("-0", false, "", "", 0)]           // negative zero normalizes to positive zero
    [InlineData("0.0", false, "", "", 0)]          // 0.0 = zero
    [InlineData("0.00", false, "", "", 0)]         // trailing zeros in fractional
    [InlineData("123", false, "123", "", 0)]       // simple integer
    [InlineData("-42", true, "42", "", 0)]         // negative integer
    [InlineData("1.5", false, "1", "5", -1)]       // simple decimal
    [InlineData("3.14", false, "3", "14", -2)]     // two decimal places
    [InlineData("0.001", false, "", "1", -3)]      // leading zeros in frac → exponent adjusted
    [InlineData("0.00123", false, "", "123", -5)]  // multiple leading zeros
    [InlineData("1000", false, "1", "", 3)]        // trailing zeros in integral → exponent
    [InlineData("1200", false, "12", "", 2)]       // partial trailing zeros
    [InlineData("1.200", false, "1", "2", -1)]     // trailing zeros in frac trimmed
    [InlineData("1e5", false, "1", "", 5)]         // scientific notation
    [InlineData("1.5e3", false, "1", "5", 2)]      // scientific with fraction
    [InlineData("-1.5E-3", true, "1", "5", -4)]    // negative scientific with uppercase E
    [InlineData("0.0e10", false, "", "", 0)]       // zero with exponent is still zero
    public void TryParseNumber_Normalization(
        string input, bool expectedNeg, string expectedIntg, string expectedFrac, int expectedExp)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        bool result = JsonElementHelpers.TryParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.True(result);
        Assert.Equal(expectedNeg, isNegative);
        Assert.Equal(expectedIntg, integral.IsEmpty ? string.Empty : JsonReaderHelper.TranscodeHelper(integral));
        Assert.Equal(expectedFrac, fractional.IsEmpty ? string.Empty : JsonReaderHelper.TranscodeHelper(fractional));
        Assert.Equal(expectedExp, exponent);
    }

    #endregion

    #region TryGetNormalizedInt64

    [Theory]
    [InlineData(false, "1", "", 0, true, 1L)]                  // simple 1
    [InlineData(true, "1", "", 0, true, -1L)]                  // -1
    [InlineData(false, "", "", 0, true, 0L)]                   // zero (empty integral+frac)
    [InlineData(false, "123", "456", 0, true, 123456L)]        // integral + fractional concatenated
    [InlineData(false, "5", "", 2, true, 500L)]                // exponent 2: 5 * 10^2 = 500
    [InlineData(true, "92233720368547758", "08", 0, true, long.MinValue)] // -9223372036854775808
    [InlineData(false, "92233720368547758", "07", 0, true, long.MaxValue)] // 9223372036854775807
    [InlineData(false, "1", "", -1, false, 0L)]                // negative exponent → not integer
    [InlineData(false, "12345678901234567890", "", 0, false, 0L)] // >19 digits → overflow
    [InlineData(true, "92233720368547758", "09", 0, false, 0L)]  // -(MaxValue+2) → overflow
    [InlineData(false, "92233720368547758", "08", 0, false, 0L)] // MaxValue+1 unsigned → overflow positive
    [InlineData(false, "1", "", 19, false, 0L)]                // exponent causes overflow in loop
    public void TryGetNormalizedInt64(
        bool isNegative,
        string integralStr,
        string fractionalStr,
        int exponent,
        bool expectedResult,
        long expectedValue)
    {
        byte[] integralBytes = Encoding.UTF8.GetBytes(integralStr);
        byte[] fractionalBytes = Encoding.UTF8.GetBytes(fractionalStr);

        bool result = JsonElementHelpers.TryGetNormalizedInt64(
            isNegative,
            integralBytes,
            fractionalBytes,
            exponent,
            out long value);

        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.Equal(expectedValue, value);
        }
    }

    #endregion

    #region CompareNormalizedJsonNumbers

    [Theory]
    [InlineData(false, "1", "", 0, false, "1", "", 0, 0)]         // 1 == 1
    [InlineData(true, "1", "", 0, false, "1", "", 0, -1)]         // -1 < 1
    [InlineData(false, "1", "", 0, true, "1", "", 0, 1)]          // 1 > -1
    [InlineData(false, "2", "", 0, false, "1", "", 0, 1)]         // 2 > 1
    [InlineData(false, "1", "", 0, false, "2", "", 0, -1)]        // 1 < 2
    [InlineData(true, "2", "", 0, true, "1", "", 0, -1)]          // -2 < -1
    [InlineData(true, "1", "", 0, true, "2", "", 0, 1)]           // -1 > -2
    [InlineData(false, "1", "", 1, false, "1", "", 0, 1)]         // 10 > 1 (different magnitude)
    [InlineData(false, "1", "", 0, false, "1", "", 1, -1)]        // 1 < 10
    [InlineData(false, "15", "", -1, false, "14", "", -1, 1)]     // 1.5 > 1.4
    [InlineData(false, "15", "", -1, false, "15", "", -1, 0)]     // 1.5 == 1.5
    [InlineData(false, "123", "45", -3, false, "123", "44", -3, 1)] // 12.345 > 12.344
    public void CompareNormalizedJsonNumbers(
        bool leftNeg, string leftIntg, string leftFrac, int leftExp,
        bool rightNeg, string rightIntg, string rightFrac, int rightExp,
        int expected)
    {
        byte[] leftIntgBytes = Encoding.UTF8.GetBytes(leftIntg);
        byte[] leftFracBytes = Encoding.UTF8.GetBytes(leftFrac);
        byte[] rightIntgBytes = Encoding.UTF8.GetBytes(rightIntg);
        byte[] rightFracBytes = Encoding.UTF8.GetBytes(rightFrac);

        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftNeg, leftIntgBytes, leftFracBytes, leftExp,
            rightNeg, rightIntgBytes, rightFracBytes, rightExp);

        Assert.Equal(expected, result);
    }

    #endregion

    #region AreEqualJsonNumbers

    [Theory]
    [InlineData("1", "1", true)]
    [InlineData("1.0", "1", true)]           // 1.0 == 1 (trailing zeros trimmed)
    [InlineData("10", "1e1", true)]          // 10 == 1e1
    [InlineData("0.001", "1e-3", true)]      // 0.001 == 1e-3
    [InlineData("100", "1e2", true)]         // 100 == 1e2
    [InlineData("-0", "0", true)]            // -0 == 0
    [InlineData("1", "2", false)]            // 1 != 2
    [InlineData("1", "-1", false)]           // 1 != -1
    [InlineData("1.5", "1.50", true)]        // trailing zero in frac
    [InlineData("1.5", "1.500", true)]       // multiple trailing zeros
    [InlineData("1200", "12e2", true)]       // trailing zeros in integral
    [InlineData("0.00120", "12e-4", true)]   // leading zeros in frac + trailing zeros
    public void AreEqualJsonNumbers(string left, string right, bool expected)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);

        bool result = JsonElementHelpers.AreEqualJsonNumbers(leftBytes, rightBytes);
        Assert.Equal(expected, result);
    }

    #endregion

    #region AreEqualNormalizedJsonNumbers - split significand paths

    [Theory]
    [InlineData(false, "12", "34", 0, false, "1", "234", 0, true)]    // diff < 0 path
    [InlineData(false, "1", "234", 0, false, "12", "34", 0, true)]    // diff > 0 path
    [InlineData(false, "12", "34", 0, false, "12", "34", 0, true)]    // diff == 0 path
    [InlineData(false, "12", "34", 0, false, "1", "235", 0, false)]   // diff < 0, not equal
    [InlineData(false, "12", "34", -2, true, "12", "34", -2, false)]  // different sign
    [InlineData(false, "12", "34", -2, false, "12", "34", -3, false)] // different exponent
    [InlineData(false, "12", "34", -2, false, "12", "345", -3, false)] // different length
    public void AreEqualNormalizedJsonNumbers_SplitPaths(
        bool leftNeg, string leftIntg, string leftFrac, int leftExp,
        bool rightNeg, string rightIntg, string rightFrac, int rightExp,
        bool expected)
    {
        byte[] leftIntgBytes = Encoding.UTF8.GetBytes(leftIntg);
        byte[] leftFracBytes = Encoding.UTF8.GetBytes(leftFrac);
        byte[] rightIntgBytes = Encoding.UTF8.GetBytes(rightIntg);
        byte[] rightFracBytes = Encoding.UTF8.GetBytes(rightFrac);

        bool result = JsonElementHelpers.AreEqualNormalizedJsonNumbers(
            leftNeg, leftIntgBytes, leftFracBytes, leftExp,
            rightNeg, rightIntgBytes, rightFracBytes, rightExp);

        Assert.Equal(expected, result);
    }

    #endregion

    #region IsMultipleOf - ulong divisor

    [Theory]
    [InlineData("0", 7, true)]              // 0 mod anything is 0
    [InlineData("12", 1, true)]             // anything mod 1 is 0
    [InlineData("12", 0, false)]            // divisor 0 always false
    [InlineData("6", 2, true)]              // 6 mod 2 = 0
    [InlineData("7", 2, false)]             // 7 mod 2 != 0
    [InlineData("9", 3, true)]              // 9 mod 3 = 0
    [InlineData("10", 3, false)]            // 10 mod 3 != 0
    [InlineData("12", 4, true)]             // 12 mod 4 = 0
    [InlineData("13", 4, false)]            // 13 mod 4 != 0
    [InlineData("15", 5, true)]             // 15 mod 5 = 0
    [InlineData("16", 5, false)]            // 16 mod 5 != 0
    [InlineData("18", 6, true)]             // 18 mod 6 = 0
    [InlineData("19", 6, false)]            // 19 mod 6 != 0
    [InlineData("24", 8, true)]             // 24 mod 8 = 0
    [InlineData("25", 8, false)]            // 25 mod 8 != 0
    [InlineData("30", 10, true)]            // 30 mod 10 = 0
    [InlineData("31", 10, false)]           // 31 mod 10 != 0
    [InlineData("21", 7, true)]             // 21 mod 7 = 0 (general purpose)
    [InlineData("22", 7, false)]            // 22 mod 7 != 0
    [InlineData("0.5", 2, false)]           // fractional → netExponent < 0
    public void IsMultipleOf_UlongDivisor(string number, ulong divisor, bool expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        bool result = JsonElementHelpers.IsMultipleOf(
            integral, fractional, exponent, divisor, 0);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.5", 5, -1, true)]   // 1.5 is a multiple of 0.5: netExp = -1 - (-1) = 0, significand 15 mod 5 = 0
    [InlineData("1.5", 7, -1, false)]  // 1.5 is not a multiple of 0.7
    public void IsMultipleOf_UlongDivisor_WithDivisorExponent(string number, ulong divisor, int divisorExponent, bool expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        bool result = JsonElementHelpers.IsMultipleOf(
            integral, fractional, exponent, divisor, divisorExponent);

        Assert.Equal(expected, result);
    }

    #endregion

    #region IsMultipleOf - BigInteger divisor

    [Theory]
    [InlineData("0", 7, true)]
    [InlineData("12", 1, true)]
    [InlineData("12", 0, false)]
    [InlineData("6", 2, true)]
    [InlineData("7", 2, false)]
    [InlineData("9", 3, true)]
    [InlineData("10", 3, false)]
    [InlineData("12", 4, true)]
    [InlineData("13", 4, false)]
    [InlineData("15", 5, true)]
    [InlineData("16", 5, false)]
    [InlineData("18", 6, true)]
    [InlineData("19", 6, false)]
    [InlineData("24", 8, true)]
    [InlineData("25", 8, false)]
    [InlineData("30", 10, true)]
    [InlineData("31", 10, false)]
    [InlineData("21", 7, true)]
    [InlineData("22", 7, false)]
    [InlineData("0.5", 2, false)]     // fractional → netExponent < 0
    public void IsMultipleOf_BigIntegerDivisor(string number, int divisorValue, bool expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        var divisor = new System.Numerics.BigInteger(divisorValue);
        bool result = JsonElementHelpers.IsMultipleOf(
            integral, fractional, exponent, divisor, 0);

        Assert.Equal(expected, result);
    }

    #endregion

    #region IsIntegerNormalizedJsonNumber

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(-1, false)]
    [InlineData(-5, false)]
    public void IsIntegerNormalizedJsonNumber(int exponent, bool expected)
    {
        bool result = JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent);
        Assert.Equal(expected, result);
    }

    #endregion

    #region TryFormatNumber - format dispatch paths

    [Fact]
    public void TryFormatNumber_ZeroWithFormat_UsesZeroFastPath()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "N2", CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal("0.00", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TryFormatNumber_EmptyFormat_TranscodesRaw()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "", null);

        Assert.True(result);
        Assert.Equal("123.456", destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TryFormatNumber_InvalidPrecision_ReturnsFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "Nxyz", null);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TryFormatNumber_UnknownFormat_ReturnsFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "Z", null);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData("G", "123.456")]
    [InlineData("F2", "123.46")]
    [InlineData("N2", "123.46")]
    [InlineData("E2", "1.23E+002")]
    [InlineData("e2", "1.23e+002")]
    public void TryFormatNumber_VariousFormats(string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    [Theory]
    [InlineData("X", "7B")]     // 123 in hex
    [InlineData("x", "7b")]     // lowercase hex
    [InlineData("B", "1111011")] // 123 in binary
    public void TryFormatNumber_HexAndBinary(string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    [Fact]
    public void TryFormatNumberAsString_Success()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("42.5");

        bool result = JsonElementHelpers.TryFormatNumberAsString(
            utf8, "F1", CultureInfo.InvariantCulture, out string? value);

        Assert.True(result);
        Assert.Equal("42.5", value);
    }

    [Fact]
    public void TryFormatNumberAsString_InvalidFormat_ReturnsFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("42");

        bool result = JsonElementHelpers.TryFormatNumberAsString(
            utf8, "Zabc", null, out string? value);

        Assert.False(result);
        Assert.Null(value);
    }

    #endregion
}
