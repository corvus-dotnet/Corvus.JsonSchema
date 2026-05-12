using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonElementNumberTest
{
    [TestMethod]
    [DataRow(false, "5", "", 0, true, "5", "", 0, 1)] // Positive vs. Negative
    [DataRow(false, "0", "", 0, false, "0", "", 0, 0)] // Zero Comparisons
    [DataRow(false, "1", "", 3, false, "1000", "", 0, 0)] // Different Exponents
    [DataRow(false, "123", "", 0, false, "124", "", 0, -1)] // Different Integral Parts
    [DataRow(false, "12", "", 0, false, "124", "", 0, -1)] // Integral Parts differ by an order of magnitude
    [DataRow(false, "1", "23", -2, false, "1", "24", -2, -1)] // Different Fractional Parts
    [DataRow(false, "1", "", 100, false, "1", "", 99, 1)] // Very Large Numbers
    [DataRow(false, "1", "", -100, false, "1", "", -99, -1)] // Very Small Numbers
    [DataRow(false, "1", "23", 100, false, "1", "23", 99, 1)] // Very Large Numbers
    [DataRow(false, "1", "23", -100, false, "1", "23", -99, -1)] // Very Small Numbers
    [DataRow(false, "1", "23", 2, false, "123", "", 2, 0)] // Exponent Adjustment
    [DataRow(false, "123", "", -2, false, "1", "23", -2, 0)] // Exponent Adjustment (Negative)
    [DataRow(false, "0", "123", 0, false, "0", "123", 0, 0)] // Zero with Fractional Part
    [DataRow(false, "123", "45", -2, false, "123", "46", -2, -1)] // Mixed Integral and Fractional
    [DataRow(false, "123", "45", -2, false, "124", "00", -2, -1)] // Mixed Integral and Fractional
    [DataRow(false, "123", "", 0, false, "1234", "", 0, -1)] // Different Lengths
    [DataRow(false, "12345", "", -2, false, "123", "45", -2, 0)] // Mixed Integral and Fractional with Exponent
    [DataRow(true, "1", "23", 2, true, "123", "", 2, 0)] // Negative Numbers with Exponent Adjustment
    [DataRow(true, "123", "", -2, true, "1", "23", -2, 0)] // Negative Numbers with Exponent Adjustment (Negative)
    [DataRow(false, "1", "12345678901234567890", 0, false, "1", "12345678901234567891", 0, -1)] // Very Long Fractional Parts
    [DataRow(false, "12345678901234567891", "", 0, false, "12345678901234567892", "", 0, -1)] // Very Long Integral Parts
    public void CompareNormalizedJsonNumbersTests(
        bool leftIsNegative,
        string leftIntegral,
        string leftFractional,
        int leftExponent,
        bool rightIsNegative,
        string rightIntegral,
        string rightFractional,
        int rightExponent,
        int expected)
    {
        // Arrange
        ReadOnlySpan<byte> leftIntegralSpan = GetUtf8Bytes(leftIntegral);
        ReadOnlySpan<byte> leftFractionalSpan = GetUtf8Bytes(leftFractional);
        ReadOnlySpan<byte> rightIntegralSpan = GetUtf8Bytes(rightIntegral);
        ReadOnlySpan<byte> rightFractionalSpan = GetUtf8Bytes(rightFractional);

        // Act
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative,
            leftIntegralSpan,
            leftFractionalSpan,
            leftExponent,
            rightIsNegative,
            rightIntegralSpan,
            rightFractionalSpan,
            rightExponent);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("", "", 0, 1123UL, 0, true)] // Zero is always a multiple of any number, except zero
    [DataRow("", "", 0, 0UL, 0, false)] // A divisor of zero is never a multiple of any number
    [DataRow("123", "", 0, 0UL, 0, false)] // A divisor of zero is never a multiple of any number
    [DataRow("123", "", 0, 1UL, 0, true)] // Any integer is a multiple of 1
    [DataRow("124", "", 0, 2UL, 0, true)] // Even number divisible by 2
    [DataRow("123", "", 0, 2UL, 0, false)] // Odd number not divisible by 2
    [DataRow("126", "", 0, 3UL, 0, true)] // Divisible by 3
    [DataRow("127", "", 0, 3UL, 0, false)] // Not divisible by 3
    [DataRow("692", "", 0, 4UL, 0, true)] // Divisible by 4
    [DataRow("693", "", 0, 4UL, 0, false)] // Not divisible by 4
    [DataRow("125", "", 0, 5UL, 0, true)] // Divisible by 5
    [DataRow("126", "", 0, 5UL, 0, false)] // Not divisible by 5
    [DataRow("582", "", 0, 6UL, 0, true)] // Divisible by 6
    [DataRow("583", "", 0, 6UL, 0, false)] // Not divisible by 6
    [DataRow("576", "", 0, 8UL, 0, true)] // Divisible by 8
    [DataRow("577", "", 0, 8UL, 0, false)] // Not divisible by 8
    [DataRow("56", "", 0, 8UL, 0, true)] // Divisible by 8 (smaller than 100)
    [DataRow("57", "", 0, 8UL, 0, false)] // Not divisible by 8 (smaller than 100)
    [DataRow("100", "", 0, 10UL, 0, true)] // Divisible by 10
    [DataRow("101", "", 0, 10UL, 0, false)] // Not divisible by 10
    [DataRow("123", "45", 0, 1UL, 0, true)] // No fractional exponent.
    [DataRow("123", "45", -2, 1UL, 0, false)] // Fractional part makes it not a multiple
    [DataRow("123", "", -2, 1UL, -2, true)] // Adjusted exponent matches divisor
    [DataRow("1", "23", 0, 1UL, -2, true)] // Adjusted exponent matches divisor
    [DataRow("1", "23", 0, 1UL, 2, false)] // Adjusted exponent matches divisor
    [DataRow("123", "", -2, 1UL, 0, false)] // Exponent leaves a fractional part
    public void IsMultipleOf_FastPathDivisors_ReturnsExpected(
          string integral,
          string fractional,
          int exponent,
          ulong divisor,
          int divisorExponent,
          bool expected)
    {
        // Arrange
        ReadOnlySpan<byte> integralSpan = GetUtf8Bytes(integral);
        ReadOnlySpan<byte> fractionalSpan = GetUtf8Bytes(fractional);

        // Act
        bool result = JsonElementHelpers.IsMultipleOf(
            integralSpan,
            fractionalSpan,
            exponent,
            divisor,
            divisorExponent);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("123456789", "", 0, 3UL, 0, true)] // 123456789 is a multiple of 3
    [DataRow("123456789", "12", 100, 112UL, 0, true)] // 123456789.12E98 is a multiple of 112
    [DataRow("123456789", "121", 100, 112UL, 0, false)] // 123456789.121E97 is not a multiple of 112
    [DataRow("123456789123456789123456789123456789123456789123456789123456789123456789123456789123456789123456789", "123456789123456789123456789123456789123456789123456789", 1200, 112UL, 0, false)] // Very large number is not a multiple of 112
    [DataRow("999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999", "999999999999999999999999999999999999999999999999999999", 54, 9UL, 0, true)] // Very large number is a multiple of 9
    public void IsMultipleOf_GeneralPurposeDivisor_ReturnsExpected(
        string integral,
        string fractional,
        int exponent,
        ulong divisor,
        int divisorExponent,
        bool expected)
    {
        // Arrange
        ReadOnlySpan<byte> integralSpan = GetUtf8Bytes(integral);
        ReadOnlySpan<byte> fractionalSpan = GetUtf8Bytes(fractional);

        // Act
        bool result = JsonElementHelpers.IsMultipleOf(
            integralSpan,
            fractionalSpan,
            exponent,
            divisor,
            divisorExponent);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(false, "123", "45", 2, false, "123", "45", 2, true)] // Identical
    [DataRow(false, "123", "45", 2, true, "123", "45", 2, false)] // Different sign
    [DataRow(false, "123", "45", 2, false, "123", "45", -1, false)] // Different exponent
    [DataRow(false, "123", "45", 2, false, "123", "46", 2, false)] // Different digits
    [DataRow(false, "1", "2345", 2, false, "12", "345", 2, true)] // Same digits, different split
    [DataRow(false, "12345", "", 2, false, "1234", "5", 2, true)] // left is longer than right
    [DataRow(false, "", "", 0, false, "", "", 0, true)] // Both zero
    [DataRow(true, "", "", 0, false, "", "", 0, false)] // Zero, different sign
    [DataRow(false, "12345678901234567890", "", 0, false, "12345678901234567890", "", 0, true)] // Long numbers, equal
    [DataRow(false, "12345678901234567890", "", 0, false, "12345678901234567891", "", 0, false)] // Long numbers, not equal
    public void AreEqualNormalizedJsonNumbersTests(
        bool leftIsNegative,
        string leftIntegral,
        string leftFractional,
        int leftExponent,
        bool rightIsNegative,
        string rightIntegral,
        string rightFractional,
        int rightExponent,
        bool expected)
    {
        // Arrange
        ReadOnlySpan<byte> leftIntegralSpan = GetUtf8Bytes(leftIntegral);
        ReadOnlySpan<byte> leftFractionalSpan = GetUtf8Bytes(leftFractional);
        ReadOnlySpan<byte> rightIntegralSpan = GetUtf8Bytes(rightIntegral);
        ReadOnlySpan<byte> rightFractionalSpan = GetUtf8Bytes(rightFractional);

        // Act
        bool result = JsonElementHelpers.AreEqualNormalizedJsonNumbers(
            leftIsNegative,
            leftIntegralSpan,
            leftFractionalSpan,
            leftExponent,
            rightIsNegative,
            rightIntegralSpan,
            rightFractionalSpan,
            rightExponent);

        // Assert
        Assert.AreEqual(expected, result);
    }

#if NET

    [TestMethod]
    [DataRow("", "", 0, 1123, 0, true)] // Zero is always a multiple of any number, except zero
    [DataRow("", "", 0, 0, 0, false)] // A divisor of zero is never a multiple of any number
    [DataRow("123", "", 0, 0, 0, false)] // A divisor of zero is never a multiple of any number
    [DataRow("123", "", 0, 1, 0, true)] // Any integer is a multiple of 1
    [DataRow("124", "", 0, 2, 0, true)] // Even number divisible by 2
    [DataRow("123", "", 0, 2, 0, false)] // Odd number not divisible by 2
    [DataRow("126", "", 0, 3, 0, true)] // Divisible by 3
    [DataRow("127", "", 0, 3, 0, false)] // Not divisible by 3
    [DataRow("692", "", 0, 4, 0, true)] // Divisible by 4
    [DataRow("693", "", 0, 4, 0, false)] // Not divisible by 4
    [DataRow("125", "", 0, 5, 0, true)] // Divisible by 5
    [DataRow("126", "", 0, 5, 0, false)] // Not divisible by 5
    [DataRow("582", "", 0, 6, 0, true)] // Divisible by 6
    [DataRow("583", "", 0, 6, 0, false)] // Not divisible by 6
    [DataRow("576", "", 0, 8, 0, true)] // Divisible by 8
    [DataRow("577", "", 0, 8, 0, false)] // Not divisible by 8
    [DataRow("56", "", 0, 8, 0, true)] // Divisible by 8 (smaller than 100)
    [DataRow("57", "", 0, 8, 0, false)] // Not divisible by 8 (smaller than 100)    [DataRow("100", "", 0, 10, 0, true)] // Divisible by 10
    [DataRow("101", "", 0, 10, 0, false)] // Not divisible by 10
    [DataRow("123", "45", 0, 1, 0, true)] // No fractional exponent.
    [DataRow("123", "45", -2, 1, 0, false)] // Fractional part makes it not a multiple
    [DataRow("123", "", -2, 1, -2, true)] // Adjusted exponent matches divisor
    [DataRow("1", "23", 0, 1, -2, true)] // Adjusted exponent matches divisor
    [DataRow("1", "23", 0, 1, 2, false)] // Adjusted exponent matches divisor
    [DataRow("123", "", -2, 1, 0, false)] // Exponent leaves a fractional part
    public void IsMultipleOf_FastPathDivisorsBigInt_ReturnsExpected(
      string integral,
      string fractional,
      int exponent,
      int divisorValue,
      int divisorExponent,
      bool expected)
    {
        // Arrange
        System.Numerics.BigInteger divisor = divisorValue;
        ReadOnlySpan<byte> integralSpan = GetUtf8Bytes(integral);
        ReadOnlySpan<byte> fractionalSpan = GetUtf8Bytes(fractional);

        // Act
        bool result = JsonElementHelpers.IsMultipleOf(
            integralSpan,
            fractionalSpan,
            exponent,
            divisor,
            divisorExponent);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("123456789", "", 0, 3, 0, true)] // 123456789 is a multiple of 3
    [DataRow("123456789", "12", 100, 112, 0, true)] // 123456789.12E100 is a multiple of 112
    [DataRow("123456789", "121", 100, 112, 0, false)] // 123456789.121E100 is not a multiple of 112
    [DataRow("123456789123456789123456789123456789123456789123456789123456789123456789123456789123456789123456789", "123456789123456789123456789123456789123456789123456789", 1200, 112, 0, false)] // Very large number is not a multiple of 112
    [DataRow("444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444", "444444444444444444444444444444444444444444444444444444", 1200, 10, 0, true)] // Very large number is a multiple of 4
    public void IsMultipleOf_GeneralPurposeDivisorBigInt_ReturnsExpected(
        string integral,
        string fractional,
        int exponent,
        int divisorValue,
        int divisorExponent,
        bool expected)
    {
        // Arrange
        System.Numerics.BigInteger divisor = divisorValue;
        ReadOnlySpan<byte> integralSpan = GetUtf8Bytes(integral);
        ReadOnlySpan<byte> fractionalSpan = GetUtf8Bytes(fractional);

        // Act
        bool result = JsonElementHelpers.IsMultipleOf(
            integralSpan,
            fractionalSpan,
            exponent,
            divisor,
            divisorExponent);

        // Assert
        Assert.AreEqual(expected, result);
    }

#endif

    private static ReadOnlySpan<byte> GetUtf8Bytes(string value)
    {
        return string.IsNullOrEmpty(value) ? ReadOnlySpan<byte>.Empty : System.Text.Encoding.UTF8.GetBytes(value);
    }
}
