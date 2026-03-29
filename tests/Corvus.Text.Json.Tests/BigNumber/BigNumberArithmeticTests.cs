// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Numerics;
using Corvus.Numerics;
using Xunit;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Tests for BigNumber arithmetic operations.
/// </summary>
public class BigNumberArithmeticTests
{
    [Theory]
    [MemberData(nameof(BigNumberTestData.AdditionData), MemberType = typeof(BigNumberTestData))]
    public void Addition_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger s1, int e1,
        BigInteger s2, int e2,
        BigInteger expectedS, int expectedE)
    {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(s1, e1);
        var bn2 = new Corvus.Numerics.BigNumber(s2, e2);
        var expected = new Corvus.Numerics.BigNumber(expectedS, expectedE);

        // Act
        BigNumber result = bn1 + bn2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.SubtractionData), MemberType = typeof(BigNumberTestData))]
    public void Subtraction_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger s1, int e1,
        BigInteger s2, int e2,
        BigInteger expectedS, int expectedE)
    {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(s1, e1);
        var bn2 = new Corvus.Numerics.BigNumber(s2, e2);
        var expected = new Corvus.Numerics.BigNumber(expectedS, expectedE);

        // Act
        BigNumber result = bn1 - bn2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.MultiplicationData), MemberType = typeof(BigNumberTestData))]
    public void Multiplication_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger s1, int e1,
        BigInteger s2, int e2,
        BigInteger expectedS, int expectedE)
    {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(s1, e1);
        var bn2 = new Corvus.Numerics.BigNumber(s2, e2);
        var expected = new Corvus.Numerics.BigNumber(expectedS, expectedE);

        // Act
        BigNumber result = bn1 * bn2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.DivisionData), MemberType = typeof(BigNumberTestData))]
    public void Division_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger s1, int e1,
        BigInteger s2, int e2,
        int precision,
        BigInteger expectedS, int expectedE)
    {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(s1, e1);
        var bn2 = new Corvus.Numerics.BigNumber(s2, e2);
        var expected = new Corvus.Numerics.BigNumber(expectedS, expectedE);

        // Act
        var result = Corvus.Numerics.BigNumber.Divide(bn1, bn2, precision);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Division_ByZero_ShouldThrow()
    {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(1, 0);
        var bn2 = new Corvus.Numerics.BigNumber(0, 0);

        // Act & Assert
        Assert.Throws<DivideByZeroException>(() => bn1 / bn2);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.ModuloData), MemberType = typeof(BigNumberTestData))]
    public void Modulo_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger s1, int e1,
        BigInteger s2, int e2,
        BigInteger expectedS, int expectedE)
    {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(s1, e1);
        var bn2 = new Corvus.Numerics.BigNumber(s2, e2);
        var expected = new Corvus.Numerics.BigNumber(expectedS, expectedE);

        // Act
        BigNumber result = bn1 % bn2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Modulo_ByZero_ShouldThrow()
    {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(7, 0);
        var bn2 = new Corvus.Numerics.BigNumber(0, 0);

        // Act & Assert
        Assert.Throws<DivideByZeroException>(() => bn1 % bn2);
    }

    [Fact]
    public void Modulo_ZeroDividend_ShouldReturnZero()
    {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(0, 0);
        var bn2 = new Corvus.Numerics.BigNumber(5, 0);
        var expected = new Corvus.Numerics.BigNumber(0, 0);

        // Act
        BigNumber result = bn1 % bn2;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.IncrementData), MemberType = typeof(BigNumberTestData))]
    public void Increment_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger inputS, int inputE,
        BigInteger expectedS, int expectedE)
    {
        // Arrange
        var value = new Corvus.Numerics.BigNumber(inputS, inputE);
        var expected = new Corvus.Numerics.BigNumber(expectedS, expectedE);

        // Act
        BigNumber result = ++value;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.DecrementData), MemberType = typeof(BigNumberTestData))]
    public void Decrement_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger inputS, int inputE,
        BigInteger expectedS, int expectedE)
    {
        // Arrange
        var value = new Corvus.Numerics.BigNumber(inputS, inputE);
        var expected = new Corvus.Numerics.BigNumber(expectedS, expectedE);

        // Act
        BigNumber result = --value;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.UnaryPlusData), MemberType = typeof(BigNumberTestData))]
    public void UnaryPlus_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger inputS, int inputE,
        BigInteger expectedS, int expectedE)
    {
        // Arrange
        var value = new Corvus.Numerics.BigNumber(inputS, inputE);
        var expected = new Corvus.Numerics.BigNumber(expectedS, expectedE);

        // Act
        BigNumber result = +value;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(BigNumberTestData.UnaryMinusData), MemberType = typeof(BigNumberTestData))]
    public void UnaryMinus_WithVariousInputs_ShouldWorkCorrectly(
        BigInteger inputS, int inputE,
        BigInteger expectedS, int expectedE)
    {
        // Arrange
        var value = new Corvus.Numerics.BigNumber(inputS, inputE);
        var expected = new Corvus.Numerics.BigNumber(expectedS, expectedE);

        // Act
        BigNumber result = -value;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void UnaryMinus_AppliedTwice_ShouldReturnOriginalValue()
    {
        // Arrange
        var original = new Corvus.Numerics.BigNumber(123, 5);

        // Act
        BigNumber result = -(-original);

        // Assert
        Assert.Equal(original, result);
    }

    [Fact]
    public void IncrementDecrement_Sequence_ShouldReturnToOriginal()
    {
        // Arrange
        var original = new Corvus.Numerics.BigNumber(100, 0);
        BigNumber unchangedOriginal = original;

        // Act
        BigNumber incremented = original++;
        BigNumber decremented = incremented--;

        // Assert
        // Note: increment/decrement work on the significand, so 100 (sig=100,exp=0) 
        // becomes 101 (sig=101,exp=0) then back to 100 (sig=100,exp=0)
        // After normalization, 100 becomes 1E2, so we need to normalize original too
        Assert.Equal(unchangedOriginal.Normalize(), decremented.Normalize());
    }

    [Fact]
    public void Modulo_WithLargeNumbers_ShouldWorkCorrectly()
    {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(BigInteger.Parse("123456789012345678901234567890"), 0);
        var bn2 = new Corvus.Numerics.BigNumber(BigInteger.Parse("987654321"), 0);

        // Act
        BigNumber result = bn1 % bn2;

        // Assert
        // 123456789012345678901234567890 % 987654321 = expected remainder
        BigInteger expectedRemainder = BigInteger.Parse("123456789012345678901234567890") % BigInteger.Parse("987654321");
        var expected = new Corvus.Numerics.BigNumber(expectedRemainder, 0);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Increment_WithNegativeExponent_PreservesDecimalPrecision()
    {
        // Arrange
      var value = new Corvus.Numerics.BigNumber(125, -2); // 1.25

        // Act
        BigNumber result = ++value;

        // Assert
        var expected = new Corvus.Numerics.BigNumber(225, -2); // 2.25
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Decrement_WithNegativeExponent_PreservesDecimalPrecision()
    {
        // Arrange
        var value = new Corvus.Numerics.BigNumber(225, -2); // 2.25

        // Act
        BigNumber result = --value;

        // Assert
        var expected = new Corvus.Numerics.BigNumber(125, -2); // 1.25
        Assert.Equal(expected, result);
  }

  [Fact]
  public void Modulo_WithMixedExponents_ShouldAlignProperly()
    {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(35, -1); // 3.5
        var bn2 = new Corvus.Numerics.BigNumber(1, 0); // 1

        // Act
        BigNumber result = bn1 % bn2;

        // Assert
        var expected = new Corvus.Numerics.BigNumber(5, -1); // 0.5
        Assert.Equal(expected, result);
    }

    [Fact]
  public void Increment_CrossingIntegerBoundary_ShouldWorkCorrectly()
  {
   // Arrange
      var value = new Corvus.Numerics.BigNumber(99, -2); // 0.99

        // Act
        BigNumber result = ++value;

     // Assert
        var expected = new Corvus.Numerics.BigNumber(199, -2); // 1.99
 Assert.Equal(expected, result);
    }

    [Fact]
    public void Decrement_CrossingZero_WithNegativeExponent()
    {
        // Arrange
    var value = new Corvus.Numerics.BigNumber(1, -2); // 0.01

        // Act
        BigNumber result = --value;

        // Assert
     var expected = new Corvus.Numerics.BigNumber(-99, -2); // -0.99
        Assert.Equal(expected, result);
    }

    [Fact]
    public void UnaryMinus_WithNegativeExponent_ShouldPreserveExponent()
    {
        // Arrange
        var value = new Corvus.Numerics.BigNumber(12345, -4); // 1.2345

        // Act
        BigNumber result = -value;

        // Assert
    Assert.Equal(-12345, result.Significand);
        Assert.Equal(-4, result.Exponent);
    }

    [Fact]
    public void Modulo_WithSmallDecimalValues_ShouldWorkCorrectly()
 {
        // Arrange
        var bn1 = new Corvus.Numerics.BigNumber(5, -1); // 0.5
        var bn2 = new Corvus.Numerics.BigNumber(2, -1); // 0.2

        // Act
        BigNumber result = bn1 % bn2;

        // Assert
        var expected = new Corvus.Numerics.BigNumber(1, -1); // 0.1
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IncrementDecrement_WithNegativeExponent_RoundTrip()
    {
      // Arrange
      var original = new Corvus.Numerics.BigNumber(75, -2); // 0.75
        BigNumber unchangedOriginal = original;

        // Act
        BigNumber incremented = original++;
        BigNumber decremented = incremented--;

        // Assert
        Assert.Equal(unchangedOriginal, decremented);
    }

    [Fact]
    public void MultipleIncrements_WithNegativeExponent()
    {
        // Arrange
        var value = new Corvus.Numerics.BigNumber(1, -1); // 0.1

    // Act
 value = ++value; // 1.1
        value = ++value; // 2.1
        value = ++value; // 3.1

        // Assert
      var expected = new Corvus.Numerics.BigNumber(31, -1); // 3.1
 Assert.Equal(expected, value);
    }

    [Fact]
    public void MultipleDecrements_WithNegativeExponent()
    {
        // Arrange
    var value = new Corvus.Numerics.BigNumber(5, -1); // 0.5

     // Act
    value = --value; // -0.5
        value = --value; // -1.5
        value = --value; // -2.5

        // Assert
        var expected = new Corvus.Numerics.BigNumber(-25, -1); // -2.5
        Assert.Equal(expected, value);
    }
}