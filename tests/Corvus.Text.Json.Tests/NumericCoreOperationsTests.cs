// <copyright file="NumericCoreOperationsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for core numeric operations: TryParseNumber, TryGetNormalizedInt64,
/// CompareNormalizedJsonNumbers, AreEqualJsonNumbers, IsMultipleOf.
/// </summary>
[TestClass]
public class NumericCoreOperationsTests
{
    #region TryParseNumber - error paths

    [TestMethod]
    public void TryParseNumber_EmptySpan_ReturnsFalse()
    {
        bool result = JsonElementHelpers.TryParseNumber(
            ReadOnlySpan<byte>.Empty,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("abc")]    // starts with non-digit non-minus
    [DataRow("+1")]     // leading plus not valid
    [DataRow(" 1")]     // leading space
    [DataRow("x")]      // single non-digit
    public void TryParseNumber_InvalidFirstChar_ReturnsFalse(string input)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        bool result = JsonElementHelpers.TryParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("1.")]       // trailing dot, no fractional digits
    [DataRow("-1.")]      // negative trailing dot
    public void TryParseNumber_TrailingDot_ReturnsFalse(string input)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        bool result = JsonElementHelpers.TryParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("1e")]       // exponent with no value
    [DataRow("1eabc")]    // exponent with non-numeric
    [DataRow("1.5e")]     // fractional with empty exponent
    [DataRow("1.5eX")]    // fractional with invalid exponent
    public void TryParseNumber_InvalidExponent_ReturnsFalse(string input)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        bool result = JsonElementHelpers.TryParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("00")]       // leading zeros in integral
    [DataRow("007")]      // leading zeros
    public void TryParseNumber_LeadingZeros_ReturnsFalse(string input)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        bool result = JsonElementHelpers.TryParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("abc")]      // non-numeric
    [DataRow("")]         // empty
    [DataRow("1.")]       // trailing dot
    public void ParseNumber_InvalidInput_ThrowsFormatException(string input)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        Assert.ThrowsExactly<FormatException>(() =>
        {
            JsonElementHelpers.ParseNumber(
                utf8,
                out _,
                out _,
                out _,
                out _);
        });
    }

    #endregion

    #region TryParseNumber - normalization

    [TestMethod]
    [DataRow("0", false, "", "", 0)]            // zero
    [DataRow("-0", false, "", "", 0)]           // negative zero normalizes to positive zero
    [DataRow("0.0", false, "", "", 0)]          // 0.0 = zero
    [DataRow("0.00", false, "", "", 0)]         // trailing zeros in fractional
    [DataRow("123", false, "123", "", 0)]       // simple integer
    [DataRow("-42", true, "42", "", 0)]         // negative integer
    [DataRow("1.5", false, "1", "5", -1)]       // simple decimal
    [DataRow("3.14", false, "3", "14", -2)]     // two decimal places
    [DataRow("0.001", false, "", "1", -3)]      // leading zeros in frac → exponent adjusted
    [DataRow("0.00123", false, "", "123", -5)]  // multiple leading zeros
    [DataRow("1000", false, "1", "", 3)]        // trailing zeros in integral → exponent
    [DataRow("1200", false, "12", "", 2)]       // partial trailing zeros
    [DataRow("1.200", false, "1", "2", -1)]     // trailing zeros in frac trimmed
    [DataRow("1e5", false, "1", "", 5)]         // scientific notation
    [DataRow("1.5e3", false, "1", "5", 2)]      // scientific with fraction
    [DataRow("-1.5E-3", true, "1", "5", -4)]    // negative scientific with uppercase E
    [DataRow("0.0e10", false, "", "", 0)]       // zero with exponent is still zero
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

        Assert.IsTrue(result);
        Assert.AreEqual(expectedNeg, isNegative);
        Assert.AreEqual(expectedIntg, integral.IsEmpty ? string.Empty : JsonReaderHelper.TranscodeHelper(integral));
        Assert.AreEqual(expectedFrac, fractional.IsEmpty ? string.Empty : JsonReaderHelper.TranscodeHelper(fractional));
        Assert.AreEqual(expectedExp, exponent);
    }

    #endregion

    #region TryGetNormalizedInt64

    [TestMethod]
    [DataRow(false, "1", "", 0, true, 1L)]                  // simple 1
    [DataRow(true, "1", "", 0, true, -1L)]                  // -1
    [DataRow(false, "", "", 0, true, 0L)]                   // zero (empty integral+frac)
    [DataRow(false, "123", "456", 0, true, 123456L)]        // integral + fractional concatenated
    [DataRow(false, "5", "", 2, true, 500L)]                // exponent 2: 5 * 10^2 = 500
    [DataRow(true, "92233720368547758", "08", 0, true, long.MinValue)] // -9223372036854775808
    [DataRow(false, "92233720368547758", "07", 0, true, long.MaxValue)] // 9223372036854775807
    [DataRow(false, "1", "", -1, false, 0L)]                // negative exponent → not integer
    [DataRow(false, "12345678901234567890", "", 0, false, 0L)] // >19 digits → overflow
    [DataRow(true, "92233720368547758", "09", 0, false, 0L)]  // -(MaxValue+2) → overflow
    [DataRow(false, "92233720368547758", "08", 0, false, 0L)] // MaxValue+1 unsigned → overflow positive
    [DataRow(false, "1", "", 19, false, 0L)]                // exponent causes overflow in loop
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

        Assert.AreEqual(expectedResult, result);
        if (expectedResult)
        {
            Assert.AreEqual(expectedValue, value);
        }
    }

    #endregion

    #region CompareNormalizedJsonNumbers

    [TestMethod]
    [DataRow(false, "1", "", 0, false, "1", "", 0, 0)]         // 1 == 1
    [DataRow(true, "1", "", 0, false, "1", "", 0, -1)]         // -1 < 1
    [DataRow(false, "1", "", 0, true, "1", "", 0, 1)]          // 1 > -1
    [DataRow(false, "2", "", 0, false, "1", "", 0, 1)]         // 2 > 1
    [DataRow(false, "1", "", 0, false, "2", "", 0, -1)]        // 1 < 2
    [DataRow(true, "2", "", 0, true, "1", "", 0, -1)]          // -2 < -1
    [DataRow(true, "1", "", 0, true, "2", "", 0, 1)]           // -1 > -2
    [DataRow(false, "1", "", 1, false, "1", "", 0, 1)]         // 10 > 1 (different magnitude)
    [DataRow(false, "1", "", 0, false, "1", "", 1, -1)]        // 1 < 10
    [DataRow(false, "15", "", -1, false, "14", "", -1, 1)]     // 1.5 > 1.4
    [DataRow(false, "15", "", -1, false, "15", "", -1, 0)]     // 1.5 == 1.5
    [DataRow(false, "123", "45", -3, false, "123", "44", -3, 1)] // 12.345 > 12.344
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

        Assert.AreEqual(expected, result);
    }

    #endregion

    #region AreEqualJsonNumbers

    [TestMethod]
    [DataRow("1", "1", true)]
    [DataRow("1.0", "1", true)]           // 1.0 == 1 (trailing zeros trimmed)
    [DataRow("10", "1e1", true)]          // 10 == 1e1
    [DataRow("0.001", "1e-3", true)]      // 0.001 == 1e-3
    [DataRow("100", "1e2", true)]         // 100 == 1e2
    [DataRow("-0", "0", true)]            // -0 == 0
    [DataRow("1", "2", false)]            // 1 != 2
    [DataRow("1", "-1", false)]           // 1 != -1
    [DataRow("1.5", "1.50", true)]        // trailing zero in frac
    [DataRow("1.5", "1.500", true)]       // multiple trailing zeros
    [DataRow("1200", "12e2", true)]       // trailing zeros in integral
    [DataRow("0.00120", "12e-4", true)]   // leading zeros in frac + trailing zeros
    public void AreEqualJsonNumbers(string left, string right, bool expected)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);

        bool result = JsonElementHelpers.AreEqualJsonNumbers(leftBytes, rightBytes);
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region AreEqualNormalizedJsonNumbers - split significand paths

    [TestMethod]
    [DataRow(false, "12", "34", 0, false, "1", "234", 0, true)]    // diff < 0 path
    [DataRow(false, "1", "234", 0, false, "12", "34", 0, true)]    // diff > 0 path
    [DataRow(false, "12", "34", 0, false, "12", "34", 0, true)]    // diff == 0 path
    [DataRow(false, "12", "34", 0, false, "1", "235", 0, false)]   // diff < 0, not equal
    [DataRow(false, "12", "34", -2, true, "12", "34", -2, false)]  // different sign
    [DataRow(false, "12", "34", -2, false, "12", "34", -3, false)] // different exponent
    [DataRow(false, "12", "34", -2, false, "12", "345", -3, false)] // different length
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

        Assert.AreEqual(expected, result);
    }

    #endregion

    #region IsMultipleOf - ulong divisor

    [TestMethod]
    [DataRow("0", 7UL, true)]              // 0 mod anything is 0
    [DataRow("12", 1UL, true)]             // anything mod 1 is 0
    [DataRow("12", 0UL, false)]            // divisor 0 always false
    [DataRow("6", 2UL, true)]              // 6 mod 2 = 0
    [DataRow("7", 2UL, false)]             // 7 mod 2 != 0
    [DataRow("9", 3UL, true)]              // 9 mod 3 = 0
    [DataRow("10", 3UL, false)]            // 10 mod 3 != 0
    [DataRow("12", 4UL, true)]             // 12 mod 4 = 0
    [DataRow("13", 4UL, false)]            // 13 mod 4 != 0
    [DataRow("15", 5UL, true)]             // 15 mod 5 = 0
    [DataRow("16", 5UL, false)]            // 16 mod 5 != 0
    [DataRow("18", 6UL, true)]             // 18 mod 6 = 0
    [DataRow("19", 6UL, false)]            // 19 mod 6 != 0
    [DataRow("24", 8UL, true)]             // 24 mod 8 = 0
    [DataRow("25", 8UL, false)]            // 25 mod 8 != 0
    [DataRow("30", 10UL, true)]            // 30 mod 10 = 0
    [DataRow("31", 10UL, false)]           // 31 mod 10 != 0
    [DataRow("21", 7UL, true)]             // 21 mod 7 = 0 (general purpose)
    [DataRow("22", 7UL, false)]            // 22 mod 7 != 0
    [DataRow("0.5", 2UL, false)]           // fractional → netExponent < 0
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

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("1.5", 5UL, -1, true)]   // 1.5 is a multiple of 0.5: netExp = -1 - (-1) = 0, significand 15 mod 5 = 0
    [DataRow("1.5", 7UL, -1, false)]  // 1.5 is not a multiple of 0.7
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

        Assert.AreEqual(expected, result);
    }

    #endregion

    #region IsMultipleOf - BigInteger divisor

    [TestMethod]
    [DataRow("0", 7, true)]
    [DataRow("12", 1, true)]
    [DataRow("12", 0, false)]
    [DataRow("6", 2, true)]
    [DataRow("7", 2, false)]
    [DataRow("9", 3, true)]
    [DataRow("10", 3, false)]
    [DataRow("12", 4, true)]
    [DataRow("13", 4, false)]
    [DataRow("15", 5, true)]
    [DataRow("16", 5, false)]
    [DataRow("18", 6, true)]
    [DataRow("19", 6, false)]
    [DataRow("24", 8, true)]
    [DataRow("25", 8, false)]
    [DataRow("30", 10, true)]
    [DataRow("31", 10, false)]
    [DataRow("21", 7, true)]
    [DataRow("22", 7, false)]
    [DataRow("0.5", 2, false)]     // fractional → netExponent < 0
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

        Assert.AreEqual(expected, result);
    }

    #endregion

    #region IsIntegerNormalizedJsonNumber

    [TestMethod]
    [DataRow(0, true)]
    [DataRow(1, true)]
    [DataRow(5, true)]
    [DataRow(-1, false)]
    [DataRow(-5, false)]
    public void IsIntegerNormalizedJsonNumber(int exponent, bool expected)
    {
        bool result = JsonElementHelpers.IsIntegerNormalizedJsonNumber(exponent);
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region TryFormatNumber - format dispatch paths

    [TestMethod]
    public void TryFormatNumber_ZeroWithFormat_UsesZeroFastPath()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "N2", CultureInfo.InvariantCulture);

        Assert.IsTrue(result);
        Assert.AreEqual("0.00", destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    public void TryFormatNumber_EmptyFormat_TranscodesRaw()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "", null);

        Assert.IsTrue(result);
        Assert.AreEqual("123.456", destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    public void TryFormatNumber_InvalidPrecision_ReturnsFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "Nxyz", null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void TryFormatNumber_UnknownFormat_ReturnsFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "Z", null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [DataRow("G", "123.456")]
    [DataRow("F2", "123.46")]
    [DataRow("N2", "123.46")]
    [DataRow("E2", "1.23E+002")]
    [DataRow("e2", "1.23e+002")]
    public void TryFormatNumber_VariousFormats(string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    [DataRow("X", "7B")]     // 123 in hex
    [DataRow("x", "7b")]     // lowercase hex
    [DataRow("B", "1111011")] // 123 in binary
    public void TryFormatNumber_HexAndBinary(string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    public void TryFormatNumberAsString_Success()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("42.5");

        bool result = JsonElementHelpers.TryFormatNumberAsString(
            utf8, "F1", CultureInfo.InvariantCulture, out string? value);

        Assert.IsTrue(result);
        Assert.AreEqual("42.5", value);
    }

    [TestMethod]
    public void TryFormatNumberAsString_InvalidFormat_ReturnsFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("42");

        bool result = JsonElementHelpers.TryFormatNumberAsString(
            utf8, "Zabc", null, out string? value);

        Assert.IsFalse(result);
        Assert.IsNull(value);
    }

    #endregion
}
