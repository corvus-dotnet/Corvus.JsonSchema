using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonElementNumberTest
{
    [Theory]
    [InlineData(false, "5", "", 0, true, "5", "", 0, 1)] // Positive vs. Negative
    [InlineData(false, "0", "", 0, false, "0", "", 0, 0)] // Zero Comparisons
    [InlineData(false, "1", "", 3, false, "1000", "", 0, 0)] // Different Exponents
    [InlineData(false, "123", "", 0, false, "124", "", 0, -1)] // Different Integral Parts
    [InlineData(false, "12", "", 0, false, "124", "", 0, -1)] // Integral Parts differ by an order of magnitude
    [InlineData(false, "1", "23", -2, false, "1", "24", -2, -1)] // Different Fractional Parts
    [InlineData(false, "1", "", 100, false, "1", "", 99, 1)] // Very Large Numbers
    [InlineData(false, "1", "", -100, false, "1", "", -99, -1)] // Very Small Numbers
    [InlineData(false, "1", "23", 100, false, "1", "23", 99, 1)] // Very Large Numbers
    [InlineData(false, "1", "23", -100, false, "1", "23", -99, -1)] // Very Small Numbers
    [InlineData(false, "1", "23", 2, false, "123", "", 2, 0)] // Exponent Adjustment
    [InlineData(false, "123", "", -2, false, "1", "23", -2, 0)] // Exponent Adjustment (Negative)
    [InlineData(false, "0", "123", 0, false, "0", "123", 0, 0)] // Zero with Fractional Part
    [InlineData(false, "123", "45", -2, false, "123", "46", -2, -1)] // Mixed Integral and Fractional
    [InlineData(false, "123", "45", -2, false, "124", "00", -2, -1)] // Mixed Integral and Fractional
    [InlineData(false, "123", "", 0, false, "1234", "", 0, -1)] // Different Lengths
    [InlineData(false, "12345", "", -2, false, "123", "45", -2, 0)] // Mixed Integral and Fractional with Exponent
    [InlineData(true, "1", "23", 2, true, "123", "", 2, 0)] // Negative Numbers with Exponent Adjustment
    [InlineData(true, "123", "", -2, true, "1", "23", -2, 0)] // Negative Numbers with Exponent Adjustment (Negative)
    [InlineData(false, "1", "12345678901234567890", 0, false, "1", "12345678901234567891", 0, -1)] // Very Long Fractional Parts
    [InlineData(false, "12345678901234567891", "", 0, false, "12345678901234567892", "", 0, -1)] // Very Long Integral Parts
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
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "", 0, 1123, 0, true)] // Zero is always a multiple of any number, except zero
    [InlineData("", "", 0, 0, 0, false)] // A divisor of zero is never a multiple of any number
    [InlineData("123", "", 0, 0, 0, false)] // A divisor of zero is never a multiple of any number
    [InlineData("123", "", 0, 1, 0, true)] // Any integer is a multiple of 1
    [InlineData("124", "", 0, 2, 0, true)] // Even number divisible by 2
    [InlineData("123", "", 0, 2, 0, false)] // Odd number not divisible by 2
    [InlineData("126", "", 0, 3, 0, true)] // Divisible by 3
    [InlineData("127", "", 0, 3, 0, false)] // Not divisible by 3
    [InlineData("692", "", 0, 4, 0, true)] // Divisible by 4
    [InlineData("693", "", 0, 4, 0, false)] // Not divisible by 4
    [InlineData("125", "", 0, 5, 0, true)] // Divisible by 5
    [InlineData("126", "", 0, 5, 0, false)] // Not divisible by 5
    [InlineData("582", "", 0, 6, 0, true)] // Divisible by 6
    [InlineData("583", "", 0, 6, 0, false)] // Not divisible by 6
    [InlineData("576", "", 0, 8, 0, true)] // Divisible by 8
    [InlineData("577", "", 0, 8, 0, false)] // Not divisible by 8
    [InlineData("56", "", 0, 8, 0, true)] // Divisible by 8 (smaller than 100)
    [InlineData("57", "", 0, 8, 0, false)] // Not divisible by 8 (smaller than 100)
    [InlineData("100", "", 0, 10, 0, true)] // Divisible by 10
    [InlineData("101", "", 0, 10, 0, false)] // Not divisible by 10
    [InlineData("123", "45", 0, 1, 0, true)] // No fractional exponent.
    [InlineData("123", "45", -2, 1, 0, false)] // Fractional part makes it not a multiple
    [InlineData("123", "", -2, 1, -2, true)] // Adjusted exponent matches divisor
    [InlineData("1", "23", 0, 1, -2, true)] // Adjusted exponent matches divisor
    [InlineData("1", "23", 0, 1, 2, false)] // Adjusted exponent matches divisor
    [InlineData("123", "", -2, 1, 0, false)] // Exponent leaves a fractional part
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
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123456789", "", 0, 3, 0, true)] // 123456789 is a multiple of 3
    [InlineData("123456789", "12", 100, 112, 0, true)] // 123456789.12E98 is a multiple of 112
    [InlineData("123456789", "121", 100, 112, 0, false)] // 123456789.121E97 is not a multiple of 112
    [InlineData("123456789123456789123456789123456789123456789123456789123456789123456789123456789123456789123456789", "123456789123456789123456789123456789123456789123456789", 1200, 112, 0, false)] // Very large number is not a multiple of 112
    [InlineData("999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999", "999999999999999999999999999999999999999999999999999999", 54, 9, 0, true)] // Very large number is a multiple of 9
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
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(false, "123", "45", 2, false, "123", "45", 2, true)] // Identical
    [InlineData(false, "123", "45", 2, true, "123", "45", 2, false)] // Different sign
    [InlineData(false, "123", "45", 2, false, "123", "45", -1, false)] // Different exponent
    [InlineData(false, "123", "45", 2, false, "123", "46", 2, false)] // Different digits
    [InlineData(false, "1", "2345", 2, false, "12", "345", 2, true)] // Same digits, different split
    [InlineData(false, "12345", "", 2, false, "1234", "5", 2, true)] // left is longer than right
    [InlineData(false, "", "", 0, false, "", "", 0, true)] // Both zero
    [InlineData(true, "", "", 0, false, "", "", 0, false)] // Zero, different sign
    [InlineData(false, "12345678901234567890", "", 0, false, "12345678901234567890", "", 0, true)] // Long numbers, equal
    [InlineData(false, "12345678901234567890", "", 0, false, "12345678901234567891", "", 0, false)] // Long numbers, not equal
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
        Assert.Equal(expected, result);
    }

#if NET

    [Theory]
    [InlineData("", "", 0, 1123, 0, true)] // Zero is always a multiple of any number, except zero
    [InlineData("", "", 0, 0, 0, false)] // A divisor of zero is never a multiple of any number
    [InlineData("123", "", 0, 0, 0, false)] // A divisor of zero is never a multiple of any number
    [InlineData("123", "", 0, 1, 0, true)] // Any integer is a multiple of 1
    [InlineData("124", "", 0, 2, 0, true)] // Even number divisible by 2
    [InlineData("123", "", 0, 2, 0, false)] // Odd number not divisible by 2
    [InlineData("126", "", 0, 3, 0, true)] // Divisible by 3
    [InlineData("127", "", 0, 3, 0, false)] // Not divisible by 3
    [InlineData("692", "", 0, 4, 0, true)] // Divisible by 4
    [InlineData("693", "", 0, 4, 0, false)] // Not divisible by 4
    [InlineData("125", "", 0, 5, 0, true)] // Divisible by 5
    [InlineData("126", "", 0, 5, 0, false)] // Not divisible by 5
    [InlineData("582", "", 0, 6, 0, true)] // Divisible by 6
    [InlineData("583", "", 0, 6, 0, false)] // Not divisible by 6
    [InlineData("576", "", 0, 8, 0, true)] // Divisible by 8
    [InlineData("577", "", 0, 8, 0, false)] // Not divisible by 8
    [InlineData("56", "", 0, 8, 0, true)] // Divisible by 8 (smaller than 100)
    [InlineData("57", "", 0, 8, 0, false)] // Not divisible by 8 (smaller than 100)    [InlineData("100", "", 0, 10, 0, true)] // Divisible by 10
    [InlineData("101", "", 0, 10, 0, false)] // Not divisible by 10
    [InlineData("123", "45", 0, 1, 0, true)] // No fractional exponent.
    [InlineData("123", "45", -2, 1, 0, false)] // Fractional part makes it not a multiple
    [InlineData("123", "", -2, 1, -2, true)] // Adjusted exponent matches divisor
    [InlineData("1", "23", 0, 1, -2, true)] // Adjusted exponent matches divisor
    [InlineData("1", "23", 0, 1, 2, false)] // Adjusted exponent matches divisor
    [InlineData("123", "", -2, 1, 0, false)] // Exponent leaves a fractional part
    public void IsMultipleOf_FastPathDivisorsBigInt_ReturnsExpected(
      string integral,
      string fractional,
      int exponent,
      System.Numerics.BigInteger divisor,
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
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123456789", "", 0, 3, 0, true)] // 123456789 is a multiple of 3
    [InlineData("123456789", "12", 100, 112, 0, true)] // 123456789.12E100 is a multiple of 112
    [InlineData("123456789", "121", 100, 112, 0, false)] // 123456789.121E100 is not a multiple of 112
    [InlineData("123456789123456789123456789123456789123456789123456789123456789123456789123456789123456789123456789", "123456789123456789123456789123456789123456789123456789", 1200, 112, 0, false)] // Very large number is not a multiple of 112
    [InlineData("444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444", "444444444444444444444444444444444444444444444444444444", 1200, 10, 0, true)] // Very large number is a multiple of 4
    public void IsMultipleOf_GeneralPurposeDivisorBigInt_ReturnsExpected(
        string integral,
        string fractional,
        int exponent,
        System.Numerics.BigInteger divisor,
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
        Assert.Equal(expected, result);
    }

#endif

    private static ReadOnlySpan<byte> GetUtf8Bytes(string value)
    {
        return string.IsNullOrEmpty(value) ? ReadOnlySpan<byte>.Empty : System.Text.Encoding.UTF8.GetBytes(value);
    }
}