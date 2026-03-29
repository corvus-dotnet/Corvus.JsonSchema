// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Corvus.Numerics;
using Xunit;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for the BigNumber.Normalize() method.
/// </summary>
public class BigNumberNormalizeTests
{
    [Fact]
    public void Normalize_WithZeroSignificand_ShouldReturnSameInstance()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(bigNumber, normalized);
    }

    [Fact]
    public void Normalize_WithSignificandWithoutTrailingZeros_ShouldReturnSameInstance()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 0);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(bigNumber, normalized);
    }

    [Fact]
    public void Normalize_WithSignificandHavingOneTrailingZero_ShouldNormalize()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(1230), 5);
        var expected = new Corvus.Numerics.BigNumber(new BigInteger(123), 6);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(expected, normalized);
    }

    [Fact]
    public void Normalize_WithSignificandHavingMultipleTrailingZeros_ShouldNormalize()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123000), 5);
        var expected = new Corvus.Numerics.BigNumber(new BigInteger(123), 8);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(expected, normalized);
    }

    [Fact]
    public void Normalize_WithNegativeSignificandHavingTrailingZeros_ShouldNormalize()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-567000), 10);
        var expected = new Corvus.Numerics.BigNumber(new BigInteger(-567), 13);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(expected, normalized);
    }

    [Fact]
    public void Normalize_WithLargeSignificandHavingManyTrailingZeros_ShouldNormalize()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123456000000), -5);
        var expected = new Corvus.Numerics.BigNumber(new BigInteger(123456), 1);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(expected, normalized);
    }

    [Fact]
    public void Normalize_AlreadyNormalizedNumber_ShouldReturnSameValues()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(12345), 7);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(bigNumber, normalized);
    }

    [Fact]
    public void Normalize_WithAllZerosSignificand_ShouldNormalizeToSingleZero()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(100000), 0);
        var expected = new Corvus.Numerics.BigNumber(new BigInteger(1), 5);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(expected, normalized);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.NormalizeData), MemberType = typeof(BigNumberTestData))]
    public void Normalize_WithVariousInputs_ShouldProduceExpectedResults(
        BigInteger inputSignificand, int inputExponent,
        BigInteger expectedSignificand, int expectedExponent)
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(inputSignificand, inputExponent);
        var expected = new Corvus.Numerics.BigNumber(expectedSignificand, expectedExponent);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(expected, normalized,
            $"Normalizing {inputSignificand}E{inputExponent}");
    }

    [Fact]
    public void Normalize_MultipleCallsOnSameInstance_ShouldBeIdempotent()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(12300), 5);

        // Act
        BigNumber normalized1 = bigNumber.Normalize();
        BigNumber normalized2 = normalized1.Normalize();
        BigNumber normalized3 = normalized2.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(normalized1, normalized2);
        BigNumberTestData.AssertBigNumberEqual(normalized2, normalized3);
    }

    [Fact]
    public void Normalize_WithVeryLargeNumberHavingTrailingZeros_ShouldNormalize()
    {
        // Arrange
        var largeSignificand = BigInteger.Parse("12345678901234567890000000000");
        var bigNumber = new Corvus.Numerics.BigNumber(largeSignificand, -10);
        var expectedSignificand = BigInteger.Parse("1234567890123456789");
        var expected = new Corvus.Numerics.BigNumber(expectedSignificand, 0);

        // Act
        BigNumber normalized = bigNumber.Normalize();

        // Assert
        BigNumberTestData.AssertBigNumberEqual(expected, normalized);
    }
}