// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Corvus.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for BigNumber conversion operators.
/// </summary>
[TestClass]
public class BigNumberConversionTests
{
    [TestMethod]
    [DataRow(0L)]
    [DataRow(123L)]
    [DataRow(-456L)]
    [DataRow(long.MaxValue)]
    [DataRow(long.MinValue)]
    public void LongToBigNumber_ShouldConvertCorrectly(long value)
    {
        // Act
        Corvus.Numerics.BigNumber bigNumber = value;
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        Assert.AreEqual(new BigInteger(value), normalized.Significand);
        Assert.AreEqual(0, normalized.Exponent);
    }

    [TestMethod]
    [DataRow(0UL)]
    [DataRow(123UL)]
    [DataRow(ulong.MaxValue)]
    public void ULongToBigNumber_ShouldConvertCorrectly(ulong value)
    {
        // Act
        Corvus.Numerics.BigNumber bigNumber = value;
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        Assert.AreEqual(new BigInteger(value), normalized.Significand);
        Assert.AreEqual(0, normalized.Exponent);
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(1.0)]
    [DataRow(-1.0)]
    [DataRow(1.2345)]
    [DataRow(-9.8765)]
    [DataRow(1.2345e20)]
    [DataRow(-9.8765e-10)]
#if NET
    [DataRow(double.MaxValue)]
    [DataRow(double.MinValue)]
#endif
    [DataRow(double.Epsilon)]
    public void DoubleToBigNumber_ShouldConvertCorrectly(double value)
    {
        // Act
        Corvus.Numerics.BigNumber bigNumber = value;
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        // Convert back to double and check if it's close enough
        string formatted = normalized.ToString();
        double roundTripped = double.Parse(formatted);

        Assert.AreEqual(value, roundTripped, 15); // Check for approximate equality
    }

    [TestMethod]
    [DataRow(0.0f)]
    [DataRow(1.0f)]
    [DataRow(-1.0f)]
    [DataRow(1.2345f)]
    [DataRow(-9.8765f)]
    [DataRow(1.2345e20f)]
    [DataRow(-9.8765e-10f)]
#if NET
    [DataRow(float.MaxValue)]
    [DataRow(float.MinValue)]
#endif
    [DataRow(float.Epsilon)]
    public void FloatToBigNumber_ShouldConvertCorrectly(float value)
    {
        // Act
        Corvus.Numerics.BigNumber bigNumber = value;
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        // Convert back to float and check if it's close enough
        string formatted = normalized.ToString();
        float roundTripped = float.Parse(formatted);

        Assert.AreEqual(value, roundTripped, 7); // Check for approximate equality
    }

    [TestMethod]
    [DataRow(123L, 0, 123.0)]
    [DataRow(123L, -2, 1.23)]
    [DataRow(-456L, 3, -456000.0)]
    public void BigNumberToDouble_ShouldConvertCorrectly(long significand, int exponent, double expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(significand), exponent);

        // Act
        double result = (double)bigNumber;

        // Assert
        Assert.AreEqual(expected, result, 15);
    }

    [TestMethod]
    [DataRow(123L, 0, 123.0f)]
    [DataRow(123L, -2, 1.23f)]
    [DataRow(-456L, 3, -456000.0f)]
    public void BigNumberToFloat_ShouldConvertCorrectly(long significand, int exponent, float expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(significand), exponent);

        // Act
        float result = (float)bigNumber;

        // Assert
        Assert.AreEqual(expected, result, 7);
    }

    [TestMethod]
    [DataRow(123L, 0, 123L)]
    [DataRow(123L, 2, 12300L)]
    [DataRow(12345L, -2, 123L)]
    public void BigNumberToLong_ShouldConvertCorrectly(long significand, int exponent, long expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(significand), exponent);

        // Act
        long result = (long)bigNumber;

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(123UL, 0, 123UL)]
    [DataRow(123UL, 2, 12300UL)]
    [DataRow(12345UL, -2, 123UL)]
    public void BigNumberToULong_ShouldConvertCorrectly(ulong significand, int exponent, ulong expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(significand), exponent);

        // Act
        ulong result = (ulong)bigNumber;

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(123L, 0, "123")]
    [DataRow(123L, -2, "1.23")]
    [DataRow(-456L, 3, "-456000")]
    public void BigNumberToDecimal_ShouldConvertCorrectly(long significand, int exponent, string expected)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(significand), exponent);
        decimal expectedDecimal = decimal.Parse(expected);

        // Act
        decimal result = (decimal)bigNumber;

        // Assert
        Assert.AreEqual(expectedDecimal, result);
    }

    [TestMethod]
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
        Assert.AreEqual(expected, result, 15);
    }

    [TestMethod]
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
        Assert.AreEqual(expected, result, 7);
    }

    [TestMethod]
    public void BigNumberToLong_WithTruncation_ShouldTruncate()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(12345, -2); // Represents 123.45

        // Act
        long result = (long)bigNumber;

        // Assert
        Assert.AreEqual(123L, result);
    }

    [TestMethod]
    public void BigNumberToULong_WithTruncation_ShouldTruncate()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(12345, -2); // Represents 123.45

        // Act
        ulong result = (ulong)bigNumber;

        // Assert
        Assert.AreEqual(123UL, result);
    }

    [TestMethod]
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
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void BigNumberToLong_Overflow_ShouldThrow()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Parse(long.MaxValue.ToString() + "0"), 0);

        // Act & Assert
        Assert.ThrowsExactly<OverflowException>(() => (long)bigNumber);
    }

    [TestMethod]
    public void BigNumberToULong_Overflow_ShouldThrow()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Parse(ulong.MaxValue.ToString() + "0"), 0);

        // Act & Assert
        Assert.ThrowsExactly<OverflowException>(() => (ulong)bigNumber);
    }

    [TestMethod]
    public void BigNumberToULong_Negative_ShouldThrow()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(-1, 0);

        // Act & Assert
        Assert.ThrowsExactly<OverflowException>(() => (ulong)bigNumber);
    }

    [TestMethod]
    public void BigNumberToDecimal_Overflow_ShouldThrow()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Parse(decimal.MaxValue.ToString() + "0"), 0);

        // Act & Assert
        Assert.ThrowsExactly<OverflowException>(() => (decimal)bigNumber);
    }
}