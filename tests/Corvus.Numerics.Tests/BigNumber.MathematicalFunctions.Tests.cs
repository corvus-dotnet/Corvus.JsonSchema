// <copyright file="BigNumber.MathematicalFunctions.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberMathematicalFunctionsTests
{
    #region Pow Tests

    [Fact]
    public void Pow_ToZero_ReturnsOne()
    {
        BigNumber value = new(123, -2);

        var result = BigNumber.Pow(value, 0);

        result.ShouldBe(BigNumber.One);
    }

    [Fact]
    public void Pow_ToOne_ReturnsSameValue()
    {
        BigNumber value = new(123, -2);

        var result = BigNumber.Pow(value, 1);

        result.ShouldBe(value);
    }

    [Fact]
    public void Pow_SquareSimpleNumber_ReturnsCorrectValue()
    {
        BigNumber value = new(10, 0);

        var result = BigNumber.Pow(value, 2);

        result.ShouldBe(new BigNumber(100, 0));
    }

    [Fact]
    public void Pow_CubeNumber_ReturnsCorrectValue()
    {
        BigNumber value = new(5, 0);

        var result = BigNumber.Pow(value, 3);

        result.ShouldBe(new BigNumber(125, 0));
    }

    [Fact]
    public void Pow_WithExponent_MultipliesExponents()
    {
        BigNumber value = new(10, 2);  // 1000

        var result = BigNumber.Pow(value, 3);

        result.ShouldBe(new BigNumber(1000, 6));
    }

    [Fact]
    public void Pow_ZeroToAnyPower_ReturnsZero()
    {
        var result = BigNumber.Pow(BigNumber.Zero, 5);

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Pow_NegativeExponent_ThrowsArgumentOutOfRangeException()
    {
        BigNumber value = new(10, 0);

        Should.Throw<ArgumentOutOfRangeException>(() => BigNumber.Pow(value, -2));
    }

    #endregion

    #region Sqrt Tests

    [Fact]
    public void Sqrt_PerfectSquare_ReturnsExactValue()
    {
        BigNumber value = new(100, 0);

        var result = BigNumber.Sqrt(value, 10);

        result.ShouldBe(new BigNumber(10, 0));
    }

    [Fact]
    public void Sqrt_Four_ReturnsTwo()
    {
        BigNumber value = new(4, 0);

        var result = BigNumber.Sqrt(value, 10);

        result.ShouldBe(new BigNumber(2, 0));
    }

    [Fact]
    public void Sqrt_WithPrecision_ReturnsApproximation()
    {
        BigNumber value = new(2, 0);

        var result = BigNumber.Sqrt(value, 10);

        // sqrt(2) ≈ 1.4142135623
        double resultDouble = (double)result;
        resultDouble.ShouldBe(Math.Sqrt(2), tolerance: 0.0000000001);
    }

    [Fact]
    public void Sqrt_Zero_ReturnsZero()
    {
        var result = BigNumber.Sqrt(BigNumber.Zero, 10);

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Sqrt_NegativeNumber_ThrowsArgumentException()
    {
        BigNumber negative = new(-100, 0);

        Should.Throw<ArgumentException>(() => BigNumber.Sqrt(negative, 10));
    }

    [Fact]
    public void Sqrt_NegativePrecision_ThrowsArgumentOutOfRangeException()
    {
        BigNumber value = new(100, 0);

        Should.Throw<ArgumentOutOfRangeException>(() => BigNumber.Sqrt(value, -1));
    }

    [Fact]
    public void Sqrt_LargeNumber_ReturnsCorrectValue()
    {
        BigNumber value = new(10000, 0);

        var result = BigNumber.Sqrt(value, 10);

        result.ShouldBe(new BigNumber(100, 0));
    }

    #endregion

    #region Round Tests

    [Fact]
    public void Round_ToZeroDecimals_ReturnsInteger()
    {
        var value = BigNumber.Parse("123.456");

        var result = BigNumber.Round(value, 0);

        result.ShouldBe(new BigNumber(123, 0));
    }

    [Fact]
    public void Round_ToTwoDecimals_RoundsCorrectly()
    {
        var value = BigNumber.Parse("123.456");

        var result = BigNumber.Round(value, 2);

        result.ShouldBe(BigNumber.Parse("123.46"));
    }

    [Fact]
    public void Round_ToEven_MidpointRoundsToEven()
    {
        var value1 = BigNumber.Parse("2.5");
        var value2 = BigNumber.Parse("3.5");

        var result1 = BigNumber.Round(value1, 0, MidpointRounding.ToEven);
        var result2 = BigNumber.Round(value2, 0, MidpointRounding.ToEven);

        result1.ShouldBe(new BigNumber(2, 0));
        result2.ShouldBe(new BigNumber(4, 0));
    }

    [Fact]
    public void Round_AwayFromZero_MidpointRoundsAwayFromZero()
    {
        var value1 = BigNumber.Parse("2.5");
        var value2 = BigNumber.Parse("-2.5");

        var result1 = BigNumber.Round(value1, 0, MidpointRounding.AwayFromZero);
        var result2 = BigNumber.Round(value2, 0, MidpointRounding.AwayFromZero);

        result1.ShouldBe(new BigNumber(3, 0));
        result2.ShouldBe(new BigNumber(-3, 0));
    }

    [Fact]
    public void Round_NegativeDecimals_ThrowsArgumentOutOfRangeException()
    {
        BigNumber value = new(123, 0);

        Should.Throw<ArgumentOutOfRangeException>(() => BigNumber.Round(value, -1));
    }

    #endregion

    #region Floor Tests

    [Fact]
    public void Floor_PositiveDecimal_RoundsDown()
    {
        var value = BigNumber.Parse("123.9");

        var result = BigNumber.Floor(value);

        result.ShouldBe(new BigNumber(123, 0));
    }

    [Fact]
    public void Floor_NegativeDecimal_RoundsDown()
    {
        var value = BigNumber.Parse("-123.1");

        var result = BigNumber.Floor(value);

        result.ShouldBe(new BigNumber(-124, 0));
    }

    [Fact]
    public void Floor_Integer_ReturnsSame()
    {
        BigNumber value = new(100, 0);

        var result = BigNumber.Floor(value);

        result.ShouldBe(value);
    }

    [Fact]
    public void Floor_Zero_ReturnsZero()
    {
        var result = BigNumber.Floor(BigNumber.Zero);

        result.ShouldBe(BigNumber.Zero);
    }

    #endregion

    #region Ceiling Tests

    [Fact]
    public void Ceiling_PositiveDecimal_RoundsUp()
    {
        var value = BigNumber.Parse("123.1");

        var result = BigNumber.Ceiling(value);

        result.ShouldBe(new BigNumber(124, 0));
    }

    [Fact]
    public void Ceiling_NegativeDecimal_RoundsUp()
    {
        var value = BigNumber.Parse("-123.9");

        var result = BigNumber.Ceiling(value);

        result.ShouldBe(new BigNumber(-123, 0));
    }

    [Fact]
    public void Ceiling_Integer_ReturnsSame()
    {
        BigNumber value = new(100, 0);

        var result = BigNumber.Ceiling(value);

        result.ShouldBe(value);
    }

    [Fact]
    public void Ceiling_Zero_ReturnsZero()
    {
        var result = BigNumber.Ceiling(BigNumber.Zero);

        result.ShouldBe(BigNumber.Zero);
    }

    #endregion

    #region Truncate Tests

    [Fact]
    public void Truncate_PositiveDecimal_RemovesFraction()
    {
        var value = BigNumber.Parse("123.9");

        var result = BigNumber.Truncate(value);

        result.ShouldBe(new BigNumber(123, 0));
    }

    [Fact]
    public void Truncate_NegativeDecimal_RemovesFraction()
    {
        var value = BigNumber.Parse("-123.9");

        var result = BigNumber.Truncate(value);

        result.ShouldBe(new BigNumber(-123, 0));
    }

    [Fact]
    public void Truncate_Integer_ReturnsSame()
    {
        BigNumber value = new(100, 0);

        var result = BigNumber.Truncate(value);

        result.ShouldBe(value);
    }

    [Fact]
    public void Truncate_Zero_ReturnsZero()
    {
        var result = BigNumber.Truncate(BigNumber.Zero);

        result.ShouldBe(BigNumber.Zero);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Pow_LargeExponent_ReturnsVeryLargeNumber()
    {
        BigNumber value = new(10, 0);

        var result = BigNumber.Pow(value, 50);

        result.Exponent.ShouldBe(50);
    }

    [Fact]
    public void Sqrt_VerySmallNumber_ReturnsCorrectValue()
    {
        BigNumber value = new(1, -100);

        var result = BigNumber.Sqrt(value, 10);

        // sqrt(10^-100) = 10^-50
        result.Exponent.ShouldBe(-50);
    }

    [Fact]
    public void Floor_VerySmallPositive_ReturnsZero()
    {
        BigNumber value = new(1, -100);

        var result = BigNumber.Floor(value);

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Floor_VerySmallNegative_ReturnsMinusOne()
    {
        BigNumber value = new(-1, -100);

        var result = BigNumber.Floor(value);

        result.ShouldBe(BigNumber.MinusOne);
    }

    [Fact]
    public void Ceiling_VerySmallPositive_ReturnsOne()
    {
        BigNumber value = new(1, -100);

        var result = BigNumber.Ceiling(value);

        result.ShouldBe(BigNumber.One);
    }

    [Fact]
    public void Ceiling_VerySmallNegative_ReturnsZero()
    {
        BigNumber value = new(-1, -100);

        var result = BigNumber.Ceiling(value);

        result.ShouldBe(BigNumber.Zero);
    }

    #endregion
}