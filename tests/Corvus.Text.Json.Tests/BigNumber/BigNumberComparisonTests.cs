// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for BigNumber comparison operations.
/// </summary>
[TestClass]
public class BigNumberComparisonTests
{
    [TestMethod]
    [DynamicData(nameof(BigNumberTestData.ComparisonData), typeof(BigNumberTestData))]
    public void CompareTo_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger significand1, int exponent1,
        BigInteger significand2, int exponent2,
        int expected)
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(significand1, exponent1);
        var bigNumber2 = new Corvus.Numerics.BigNumber(significand2, exponent2);

        // Act
        int result1 = bigNumber1.CompareTo(bigNumber2);
        int result2 = bigNumber2.CompareTo(bigNumber1);

        // Assert
        Assert.AreEqual(expected, Math.Sign(result1));
        Assert.AreEqual(-expected, Math.Sign(result2));
    }

    [TestMethod]
    [DynamicData(nameof(BigNumberTestData.ComparisonData), typeof(BigNumberTestData))]
    public void ComparisonOperators_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger significand1, int exponent1,
        BigInteger significand2, int exponent2,
        int expected)
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(significand1, exponent1);
        var bigNumber2 = new Corvus.Numerics.BigNumber(significand2, exponent2);

        // Act & Assert
        Assert.AreEqual(expected > 0, bigNumber1 > bigNumber2);
        Assert.AreEqual(expected < 0, bigNumber1 < bigNumber2);
        Assert.AreEqual(expected >= 0, bigNumber1 >= bigNumber2);
        Assert.AreEqual(expected <= 0, bigNumber1 <= bigNumber2);

        Assert.AreEqual(expected < 0, bigNumber2 > bigNumber1);
        Assert.AreEqual(expected > 0, bigNumber2 < bigNumber1);
        Assert.AreEqual(expected <= 0, bigNumber2 >= bigNumber1);
        Assert.AreEqual(expected >= 0, bigNumber2 <= bigNumber1);
    }
}