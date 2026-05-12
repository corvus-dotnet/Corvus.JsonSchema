// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for the BigNumber constructor.
/// </summary>
[TestClass]
public class BigNumberConstructorTests
{
    [TestMethod]
    public void Constructor_WithZeroSignificandZeroExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);

        // Assert
        Assert.AreEqual(BigInteger.Zero, bigNumber.Significand);
        Assert.AreEqual(0, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithPositiveSignificandZeroExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 0);

        // Assert
        Assert.AreEqual(new BigInteger(123), bigNumber.Significand);
        Assert.AreEqual(0, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithNegativeSignificandZeroExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-456), 0);

        // Assert
        Assert.AreEqual(new BigInteger(-456), bigNumber.Significand);
        Assert.AreEqual(0, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithZeroSignificandNonZeroExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 10);

        // Assert
        Assert.AreEqual(BigInteger.Zero, bigNumber.Significand);
        Assert.AreEqual(10, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithPositiveSignificandPositiveExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Assert
        Assert.AreEqual(new BigInteger(123), bigNumber.Significand);
        Assert.AreEqual(5, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithPositiveSignificandNegativeExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), -5);

        // Assert
        Assert.AreEqual(new BigInteger(123), bigNumber.Significand);
        Assert.AreEqual(-5, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithNegativeSignificandPositiveExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-456), 10);

        // Assert
        Assert.AreEqual(new BigInteger(-456), bigNumber.Significand);
        Assert.AreEqual(10, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithNegativeSignificandNegativeExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-456), -10);

        // Assert
        Assert.AreEqual(new BigInteger(-456), bigNumber.Significand);
        Assert.AreEqual(-10, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithMaxValueSignificand_ShouldCreateValidInstance()
    {
        // Arrange
        var maxSignificand = BigInteger.Parse("12345678901234567890123456789012345678901234567890");

        // Act
        var bigNumber = new Corvus.Numerics.BigNumber(maxSignificand, 0);

        // Assert
        Assert.AreEqual(maxSignificand, bigNumber.Significand);
        Assert.AreEqual(0, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithMinValueSignificand_ShouldCreateValidInstance()
    {
        // Arrange
        var minSignificand = BigInteger.Parse("-12345678901234567890123456789012345678901234567890");

        // Act
        var bigNumber = new Corvus.Numerics.BigNumber(minSignificand, 0);

        // Assert
        Assert.AreEqual(minSignificand, bigNumber.Significand);
        Assert.AreEqual(0, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithMaxExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), int.MaxValue);

        // Assert
        Assert.AreEqual(new BigInteger(123), bigNumber.Significand);
        Assert.AreEqual(int.MaxValue, bigNumber.Exponent);
    }

    [TestMethod]
    public void Constructor_WithMinExponent_ShouldCreateValidInstance()
    {
        // Arrange & Act
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), int.MinValue);

        // Assert
        Assert.AreEqual(new BigInteger(123), bigNumber.Significand);
        Assert.AreEqual(int.MinValue, bigNumber.Exponent);
    }

    [TestMethod]
    [DynamicData(nameof(BigNumberTestData.BasicConstructorData), typeof(BigNumberTestData))]
    public void Constructor_WithVariousInputs_ShouldCreateValidInstance(BigInteger significand, int exponent)
    {
        // Act
        var bigNumber = new Corvus.Numerics.BigNumber(significand, exponent);

        // Assert
        Assert.AreEqual(significand, bigNumber.Significand);
        Assert.AreEqual(exponent, bigNumber.Exponent);
    }

    [TestMethod]
    [DynamicData(nameof(BigNumberTestData.ExtremeValueData), typeof(BigNumberTestData))]
    public void Constructor_WithExtremeValues_ShouldNotThrow(BigInteger significand, int exponent)
    {
        // Act & Assert (should not throw)
        var bigNumber = new Corvus.Numerics.BigNumber(significand, exponent);

        Assert.AreEqual(significand, bigNumber.Significand);
        Assert.AreEqual(exponent, bigNumber.Exponent);
    }
}