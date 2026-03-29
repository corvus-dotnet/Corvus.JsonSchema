// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Xunit;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for the BigNumber constructor.
/// </summary>
public class BigNumberConstructorTests
{
    [Fact]
    public void Constructor_WithZeroSignificandZeroExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);

        // Assert
        Assert.Equal(BigInteger.Zero, bigNumber.Significand);
        Assert.Equal(0, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithPositiveSignificandZeroExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 0);

        // Assert
        Assert.Equal(new BigInteger(123), bigNumber.Significand);
        Assert.Equal(0, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithNegativeSignificandZeroExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-456), 0);

        // Assert
        Assert.Equal(new BigInteger(-456), bigNumber.Significand);
        Assert.Equal(0, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithZeroSignificandNonZeroExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 10);

        // Assert
        Assert.Equal(BigInteger.Zero, bigNumber.Significand);
        Assert.Equal(10, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithPositiveSignificandPositiveExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Assert
        Assert.Equal(new BigInteger(123), bigNumber.Significand);
        Assert.Equal(5, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithPositiveSignificandNegativeExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), -5);

        // Assert
        Assert.Equal(new BigInteger(123), bigNumber.Significand);
        Assert.Equal(-5, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithNegativeSignificandPositiveExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-456), 10);

        // Assert
        Assert.Equal(new BigInteger(-456), bigNumber.Significand);
        Assert.Equal(10, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithNegativeSignificandNegativeExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-456), -10);

        // Assert
        Assert.Equal(new BigInteger(-456), bigNumber.Significand);
        Assert.Equal(-10, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithMaxValueSignificand_ShouldCreateValidInstance()
    {
        // Arrange
        var maxSignificand = BigInteger.Parse("12345678901234567890123456789012345678901234567890");

        // Act
        var bigNumber = new Corvus.Numerics.BigNumber(maxSignificand, 0);

        // Assert
        Assert.Equal(maxSignificand, bigNumber.Significand);
        Assert.Equal(0, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithMinValueSignificand_ShouldCreateValidInstance()
    {
        // Arrange
        var minSignificand = BigInteger.Parse("-12345678901234567890123456789012345678901234567890");

        // Act
        var bigNumber = new Corvus.Numerics.BigNumber(minSignificand, 0);

        // Assert
        Assert.Equal(minSignificand, bigNumber.Significand);
        Assert.Equal(0, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithMaxExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), int.MaxValue);

        // Assert
        Assert.Equal(new BigInteger(123), bigNumber.Significand);
        Assert.Equal(int.MaxValue, bigNumber.Exponent);
    }

    [Fact]
    public void Constructor_WithMinExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), int.MinValue);

        // Assert
        Assert.Equal(new BigInteger(123), bigNumber.Significand);
        Assert.Equal(int.MinValue, bigNumber.Exponent);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.BasicConstructorData), MemberType = typeof(BigNumberTestData))]
    public void Constructor_WithVariousInputs_ShouldCreateValidInstance(BigInteger significand, int exponent)
    {
        // Act
        var bigNumber = new Corvus.Numerics.BigNumber(significand, exponent);

        // Assert
        Assert.Equal(significand, bigNumber.Significand);
        Assert.Equal(exponent, bigNumber.Exponent);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.ExtremeValueData), MemberType = typeof(BigNumberTestData))]
    public void Constructor_WithExtremeValues_ShouldNotThrow(BigInteger significand, int exponent)
    {
        // Act & Assert (should not throw)
        var bigNumber = new Corvus.Numerics.BigNumber(significand, exponent);

        Assert.Equal(significand, bigNumber.Significand);
        Assert.Equal(exponent, bigNumber.Exponent);
    }
}