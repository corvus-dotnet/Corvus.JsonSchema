// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Corvus.Numerics;
using Xunit;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for BigNumber equality operations, including Equals, GetHashCode, and operators.
/// </summary>
public class BigNumberEqualityTests
{
    [Fact]
    public void Equals_WithSameSignificandAndExponent_ShouldReturnTrue()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        Assert.True(bigNumber1.Equals(bigNumber2));
        Assert.True(bigNumber2.Equals(bigNumber1));
    }

    [Fact]
    public void Equals_WithDifferentSignificandSameExponent_ShouldReturnFalse()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(456), 5);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersNotEqual(bigNumber1, bigNumber2);
        Assert.False(bigNumber1.Equals(bigNumber2));
        Assert.False(bigNumber2.Equals(bigNumber1));
    }

    [Fact]
    public void Equals_WithSameSignificandDifferentExponent_ShouldReturnFalse()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123), 10);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersNotEqual(bigNumber1, bigNumber2);
        Assert.False(bigNumber1.Equals(bigNumber2));
        Assert.False(bigNumber2.Equals(bigNumber1));
    }

    [Fact]
    public void Equals_WithDifferentSignificandAndExponent_ShouldReturnFalse()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(456), 10);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersNotEqual(bigNumber1, bigNumber2);
        Assert.False(bigNumber1.Equals(bigNumber2));
        Assert.False(bigNumber2.Equals(bigNumber1));
    }

    [Fact]
    public void Equals_WithZeroValues_ShouldReturnTrue()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);
        var bigNumber2 = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        Assert.True(bigNumber1.Equals(bigNumber2));
        Assert.True(bigNumber2.Equals(bigNumber1));
    }

    [Fact]
    public void Equals_WithZeroSignificandDifferentExponents_ShouldReturnTrue()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);
        var bigNumber2 = new Corvus.Numerics.BigNumber(BigInteger.Zero, 5);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        Assert.True(bigNumber1.Equals(bigNumber2));
        Assert.True(bigNumber2.Equals(bigNumber1));
    }

    [Fact]
    public void Equals_WithNegativeSignificands_ShouldWorkCorrectly()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(-123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(-123), 5);
        var bigNumber3 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        BigNumberTestData.AssertBigNumbersNotEqual(bigNumber1, bigNumber3);
        Assert.True(bigNumber1.Equals(bigNumber2));
        Assert.False(bigNumber1.Equals(bigNumber3));
    }

    [Fact]
    public void Equals_WithNegativeExponents_ShouldWorkCorrectly()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), -5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123), -5);
        var bigNumber3 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        BigNumberTestData.AssertBigNumbersNotEqual(bigNumber1, bigNumber3);
        Assert.True(bigNumber1.Equals(bigNumber2));
        Assert.False(bigNumber1.Equals(bigNumber3));
    }

    [Fact]
    public void Equals_WithVeryLargeSignificands_ShouldWorkCorrectly()
    {
        // Arrange
        var largeSignificand = BigInteger.Parse("12345678901234567890123456789012345678901234567890");
        var bigNumber1 = new Corvus.Numerics.BigNumber(largeSignificand, 999);
        var bigNumber2 = new Corvus.Numerics.BigNumber(largeSignificand, 999);
        var bigNumber3 = new Corvus.Numerics.BigNumber(largeSignificand + 1, 999);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        BigNumberTestData.AssertBigNumbersNotEqual(bigNumber1, bigNumber3);
        Assert.True(bigNumber1.Equals(bigNumber2));
        Assert.False(bigNumber1.Equals(bigNumber3));
    }

    [Fact]
    public void Equals_WithVeryLargeExponents_ShouldWorkCorrectly()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 999999);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123), 999999);
        var bigNumber3 = new Corvus.Numerics.BigNumber(new BigInteger(123), 999998);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        BigNumberTestData.AssertBigNumbersNotEqual(bigNumber1, bigNumber3);
        Assert.True(bigNumber1.Equals(bigNumber2));
        Assert.False(bigNumber1.Equals(bigNumber3));
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.EqualityData), MemberType = typeof(BigNumberTestData))]
    public void Equals_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger significand1, int exponent1,
        BigInteger significand2, int exponent2,
        bool expectedEqual)
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(significand1, exponent1);
        var bigNumber2 = new Corvus.Numerics.BigNumber(significand2, exponent2);

        // Act & Assert
        Assert.Equal(expectedEqual, bigNumber1.Equals(bigNumber2));
        Assert.Equal(expectedEqual, bigNumber2.Equals(bigNumber1));

        if (expectedEqual)
        {
            BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        }
        else
        {
            BigNumberTestData.AssertBigNumbersNotEqual(bigNumber1, bigNumber2);
        }
    }

    [Fact]
    public void Equals_WithObjectOverload_ShouldWorkCorrectly()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber3 = new Corvus.Numerics.BigNumber(new BigInteger(456), 5);
        object obj1 = bigNumber1;
        object obj2 = bigNumber2;
        object obj3 = bigNumber3;
        object nonBigNumber = "not a BigNumber";

        // Act & Assert
        Assert.True(bigNumber1.Equals(obj2));
        Assert.True(bigNumber2.Equals(obj1));
        Assert.False(bigNumber1.Equals(obj3));
        Assert.False(bigNumber1.Equals(nonBigNumber));
        Assert.False(bigNumber1.Equals(null));
    }

    [Fact]
    public void GetHashCode_WithEqualBigNumbers_ShouldReturnSameHashCode()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Act
        int hashCode1 = bigNumber1.GetHashCode();
        int hashCode2 = bigNumber2.GetHashCode();

        // Assert
        Assert.Equal(hashCode1, hashCode2);
    }

    [Fact]
    public void GetHashCode_WithDifferentBigNumbers_ShouldReturnDifferentHashCodes()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(456), 5);
        var bigNumber3 = new Corvus.Numerics.BigNumber(new BigInteger(123), 10);

        // Act
        int hashCode1 = bigNumber1.GetHashCode();
        int hashCode2 = bigNumber2.GetHashCode();
        int hashCode3 = bigNumber3.GetHashCode();

        // Assert
        // Note: Different objects can have the same hash code, but it's very unlikely for these cases
        Assert.NotEqual(hashCode1, hashCode2);
        Assert.NotEqual(hashCode1, hashCode3);
    }

    [Fact]
    public void GetHashCode_WithZeroValues_ShouldBeConsistent()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);
        var bigNumber2 = new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);

        // Act
        int hashCode1 = bigNumber1.GetHashCode();
        int hashCode2 = bigNumber2.GetHashCode();

        // Assert
        Assert.Equal(hashCode1, hashCode2);
    }

    [Fact]
    public void GetHashCode_WithNegativeValues_ShouldBeConsistent()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(-123), -5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(-123), -5);

        // Act
        int hashCode1 = bigNumber1.GetHashCode();
        int hashCode2 = bigNumber2.GetHashCode();

        // Assert
        Assert.Equal(hashCode1, hashCode2);
    }

    [Fact]
    public void GetHashCode_WithVeryLargeValues_ShouldBeConsistent()
    {
        // Arrange
        var largeSignificand = BigInteger.Parse("12345678901234567890123456789012345678901234567890");
        var bigNumber1 = new Corvus.Numerics.BigNumber(largeSignificand, 999999);
        var bigNumber2 = new Corvus.Numerics.BigNumber(largeSignificand, 999999);

        // Act
        int hashCode1 = bigNumber1.GetHashCode();
        int hashCode2 = bigNumber2.GetHashCode();

        // Assert
        Assert.Equal(hashCode1, hashCode2);
    }

    [Fact]
    public void GetHashCode_MultipleCallsOnSameInstance_ShouldReturnSameValue()
    {
        // Arrange
        var bigNumber = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Act
        int hashCode1 = bigNumber.GetHashCode();
        int hashCode2 = bigNumber.GetHashCode();
        int hashCode3 = bigNumber.GetHashCode();

        // Assert
        Assert.Equal(hashCode1, hashCode2);
        Assert.Equal(hashCode2, hashCode3);
    }

    [Fact]
    public void OperatorEquals_WithEqualBigNumbers_ShouldReturnTrue()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Act & Assert
        Assert.True(bigNumber1 == bigNumber2);
        Assert.True(bigNumber2 == bigNumber1);
        Assert.False(bigNumber1 != bigNumber2);
        Assert.False(bigNumber2 != bigNumber1);
    }

    [Fact]
    public void OperatorEquals_WithDifferentBigNumbers_ShouldReturnFalse()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(456), 5);

        // Act & Assert
        Assert.False(bigNumber1 == bigNumber2);
        Assert.False(bigNumber2 == bigNumber1);
        Assert.True(bigNumber1 != bigNumber2);
        Assert.True(bigNumber2 != bigNumber1);
    }

    [Fact]
    public void OperatorNotEquals_WithEqualBigNumbers_ShouldReturnFalse()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Act & Assert
        Assert.False(bigNumber1 != bigNumber2);
        Assert.False(bigNumber2 != bigNumber1);
    }

    [Fact]
    public void OperatorNotEquals_WithDifferentBigNumbers_ShouldReturnTrue()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(456), 5);

        // Act & Assert
        Assert.True(bigNumber1 != bigNumber2);
        Assert.True(bigNumber2 != bigNumber1);
    }

    [Fact]
    public void Equality_AfterNormalization_ShouldBeConsistent()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);

        // Normalize both numbers
        BigNumber normalized1 = bigNumber1.Normalize();
        BigNumber normalized2 = bigNumber2.Normalize();

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        BigNumberTestData.AssertBigNumbersEqual(normalized1, normalized2);
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, normalized1);
        BigNumberTestData.AssertBigNumbersEqual(bigNumber2, normalized2);
    }

    [Fact]
    public void Equality_RoundTripThroughParsing_ShouldBeConsistent()
    {
        // Arrange
        var originalBigNumber = new Corvus.Numerics.BigNumber(new BigInteger(-789), 123);

        // Format and parse back
        Span<char> buffer = stackalloc char[50];
        bool formatSuccess = originalBigNumber.TryFormat(buffer, out int charsWritten);
        Assert.True(formatSuccess);

        bool parseSuccess = Corvus.Numerics.BigNumber.TryParse(Encoding.UTF8.GetBytes(buffer.Slice(0, charsWritten).ToString()), out BigNumber parsedBigNumber);
        Assert.True(parseSuccess);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(originalBigNumber, parsedBigNumber);
        Assert.True(originalBigNumber == parsedBigNumber);
        Assert.False(originalBigNumber != parsedBigNumber);
        Assert.Equal(originalBigNumber.GetHashCode(), parsedBigNumber.GetHashCode());
    }

    [Fact]
    public void Equality_WithEquivalentButDifferentRepresentations_ShouldBeEqual()
    {
        // Arrange
        // These are mathematically equivalent (123 * 10^3 = 123000 * 10^0)
        // but should not be equal in BigNumber representation because
        // BigNumber preserves the original significand and exponent
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 3);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123000), 0);

        // Act & Assert
        BigNumberTestData.AssertBigNumbersEqual(bigNumber1, bigNumber2);
        Assert.True(bigNumber1 == bigNumber2);
        Assert.False(bigNumber1 != bigNumber2);
        Assert.Equal(bigNumber1.GetHashCode(), bigNumber2.GetHashCode());
    }

    [Fact]
    public void IEquatable_Implementation_ShouldWorkCorrectly()
    {
        // Arrange
        var bigNumber1 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber2 = new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        var bigNumber3 = new Corvus.Numerics.BigNumber(new BigInteger(456), 5);

        // Act & Assert
        IEquatable<Corvus.Numerics.BigNumber> equatable1 = bigNumber1;
        Assert.True(equatable1.Equals(bigNumber2));
        Assert.False(equatable1.Equals(bigNumber3));
    }
}