// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Corvus.Numerics;
using Xunit;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for BigNumber conversion operators.
/// </summary>
public class BigNumberConversionTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(123L)]
    [InlineData(-456L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void LongToBigNumber_ShouldConvertCorrectly(long value)
    {
        // Act
        Corvus.Numerics.BigNumber bigNumber = value;
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        Assert.Equal(new BigInteger(value), normalized.Significand);
        Assert.Equal(0, normalized.Exponent);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(123UL)]
    [InlineData(ulong.MaxValue)]
    public void ULongToBigNumber_ShouldConvertCorrectly(ulong value)
    {
        // Act
        Corvus.Numerics.BigNumber bigNumber = value;
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        Assert.Equal(new BigInteger(value), normalized.Significand);
        Assert.Equal(0, normalized.Exponent);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    [InlineData(1.2345)]
    [InlineData(-9.8765)]
    [InlineData(1.2345e20)]
    [InlineData(-9.8765e-10)]
#if NET
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
#endif
    [InlineData(double.Epsilon)]
    public void DoubleToBigNumber_ShouldConvertCorrectly(double value)
    {
        // Act
        Corvus.Numerics.BigNumber bigNumber = value;
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        // Convert back to double and check if it's close enough
        string formatted = normalized.ToString();
        double roundTripped = double.Parse(formatted);

        Assert.Equal(value, roundTripped, 15); // Check for approximate equality
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(1.0f)]
    [InlineData(-1.0f)]
    [InlineData(1.2345f)]
    [InlineData(-9.8765f)]
    [InlineData(1.2345e20f)]
    [InlineData(-9.8765e-10f)]
#if NET
    [InlineData(float.MaxValue)]
    [InlineData(float.MinValue)]
#endif
    [InlineData(float.Epsilon)]
    public void FloatToBigNumber_ShouldConvertCorrectly(float value)
    {
        // Act
        Corvus.Numerics.BigNumber bigNumber = value;
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        // Convert back to float and check if it's close enough
        string formatted = normalized.ToString();
        float roundTripped = float.Parse(formatted);

        Assert.Equal(value, roundTripped, 7); // Check for approximate equality
    }

    [Theory]
    [InlineData(123, 0, 123.0)]
    [InlineData(123, -2, 1.23)]
    [InlineData(-456, 3, -456000.0)]
    public void BigNumberToDouble_ShouldConvertCorrectly(long significand, int exponent, double expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(significand), exponent);

        // Act
        double result = (double)bigNumber;

        // Assert
        Assert.Equal(expected, result, 15);
    }

    [Theory]
    [InlineData(123, 0, 123.0f)]
    [InlineData(123, -2, 1.23f)]
    [InlineData(-456, 3, -456000.0f)]
    public void BigNumberToFloat_ShouldConvertCorrectly(long significand, int exponent, float expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(significand), exponent);

        // Act
        float result = (float)bigNumber;

        // Assert
        Assert.Equal(expected, result, 7);
    }

    [Theory]
    [InlineData(123, 0, 123L)]
    [InlineData(123, 2, 12300L)]
    [InlineData(12345, -2, 123L)]
    public void BigNumberToLong_ShouldConvertCorrectly(long significand, int exponent, long expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(significand), exponent);

        // Act
        long result = (long)bigNumber;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(123, 0, 123UL)]
    [InlineData(123, 2, 12300UL)]
    [InlineData(12345, -2, 123UL)]
    public void BigNumberToULong_ShouldConvertCorrectly(ulong significand, int exponent, ulong expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(significand), exponent);

        // Act
        ulong result = (ulong)bigNumber;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(123, 0, "123")]
    [InlineData(123, -2, "1.23")]
    [InlineData(-456, 3, "-456000")]
    public void BigNumberToDecimal_ShouldConvertCorrectly(long significand, int exponent, string expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(significand), exponent);
        decimal expectedDecimal = decimal.Parse(expected);

        // Act
        decimal result = (decimal)bigNumber;

        // Assert
        Assert.Equal(expectedDecimal, result);
    }

    [Fact]
    public void BigNumberToDouble_WithLostPrecision_ShouldApproximate()
    {
        // Arrange
        // This number has more digits than a double can represent
        var significand = BigInteger.Parse("123456789012345678901234567890");
        var bigNumber = new Corvus.Numerics.BigNumber(significand, 0);

        // Act
        double result = (double)bigNumber;
        double expected = double.Parse("1.2345678901234568E+29");

        // Assert
        Assert.Equal(expected, result, 15);
    }

    [Fact]
    public void BigNumberToFloat_WithLostPrecision_ShouldApproximate()
    {
        // Arrange
        // This number has more digits than a float can represent
        var significand = BigInteger.Parse("12345678901234567");
        var bigNumber = new Corvus.Numerics.BigNumber(significand, 0);

        // Act
        float result = (float)bigNumber;
        float expected = float.Parse("1.2345678E+16");

        // Assert
        Assert.Equal(expected, result, 7);
    }

    [Fact]
    public void BigNumberToLong_WithTruncation_ShouldTruncate()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(12345, -2); // Represents 123.45

        // Act
        long result = (long)bigNumber;

        // Assert
        Assert.Equal(123L, result);
    }

    [Fact]
    public void BigNumberToULong_WithTruncation_ShouldTruncate()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(12345, -2); // Represents 123.45

        // Act
        ulong result = (ulong)bigNumber;

        // Assert
        Assert.Equal(123UL, result);
    }

    [Fact]
    public void BigNumberToDecimal_WithLostPrecision_ShouldTruncate()
    {
        // Arrange
        // This number has 30 decimal places, which is more than a decimal can handle.
        var significand = BigInteger.Parse("123456789012345678901234567890");
        var bigNumber = new Corvus.Numerics.BigNumber(significand, -30); // 1.2345...

        // Act
        decimal result = (decimal)bigNumber;
        decimal expected = 0.1234567890123456789012345678M;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BigNumberToLong_Overflow_ShouldThrow()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Parse(long.MaxValue.ToString() + "0"), 0);

        // Act & Assert
        Assert.Throws<OverflowException>(() => (long)bigNumber);
    }

    [Fact]
    public void BigNumberToULong_Overflow_ShouldThrow()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Parse(ulong.MaxValue.ToString() + "0"), 0);

        // Act & Assert
        Assert.Throws<OverflowException>(() => (ulong)bigNumber);
    }

    [Fact]
    public void BigNumberToULong_Negative_ShouldThrow()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(-1, 0);

        // Act & Assert
        Assert.Throws<OverflowException>(() => (ulong)bigNumber);
    }

    [Fact]
    public void BigNumberToDecimal_Overflow_ShouldThrow()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Parse(decimal.MaxValue.ToString() + "0"), 0);

        // Act & Assert
        Assert.Throws<OverflowException>(() => (decimal)bigNumber);
    }
}