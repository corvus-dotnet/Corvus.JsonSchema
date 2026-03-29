// <copyright file="BigNumber.ArithmeticEdgeCases.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Tier 2 Option 4: Arithmetic edge cases.
/// Target: +0.5% coverage (20 tests).
/// </summary>
public class BigNumberArithmeticEdgeCasesTests
{
    #region Extreme Exponent Differences (+5 tests)

    [Fact]
    public void Add_VeryLargeExponentDifference_HandlesCorrectly()
    {
        BigNumber large = new(1, 100);
        BigNumber small = new(1, 0);

        BigNumber result = large + small;

        // Result should be close to the larger number
        result.Significand.ShouldBeGreaterThan(BigInteger.Zero);
    }

    [Fact]
    public void Add_LargeButSafeExponentDifference_HandlesCorrectly()
    {
        BigNumber large = new(1, 1000000);
        BigNumber small = new(1, 0);

        BigNumber result = large + small;

        // Should complete without overflow
        result.Significand.ShouldBeGreaterThan(BigInteger.Zero);
    }

    [Fact]
    public void Subtract_LargeExponentDifference_HandlesCorrectly()
    {
        BigNumber large = new(1000, 100);
        BigNumber small = new(1, 0);

        BigNumber result = large - small;

        // Result should be close to large number
        result.Significand.ShouldBeGreaterThan(BigInteger.Zero);
    }

    [Fact]
    public void Multiply_ExponentsSumToLarge_HandlesCorrectly()
    {
        BigNumber num1 = new(123, 500);
        BigNumber num2 = new(456, 400);

        BigNumber result = num1 * num2;

        result.Exponent.ShouldBeGreaterThan(899);
    }

    [Fact]
    public void Divide_ExponentDifferenceLarge_HandlesCorrectly()
    {
        BigNumber dividend = new(1000, 500);
        BigNumber divisor = new(10, -200);

        BigNumber result = dividend / divisor;

        result.Exponent.ShouldBeGreaterThan(699);
    }

    #endregion

    #region Very Large Significands (+5 tests)

    [Fact]
    public void Add_VeryLargeSignificands_HandlesCorrectly()
    {
        BigNumber num1 = BigInteger.Parse(new string('9', 100));
        BigNumber num2 = BigInteger.Parse(new string('8', 100));

        BigNumber result = num1 + num2;

        result.Significand.ShouldBeGreaterThan(BigInteger.Zero);
    }

    [Fact]
    public void Multiply_VeryLargeSignificands_HandlesCorrectly()
    {
        BigNumber num1 = BigInteger.Parse(new string('9', 50));
        BigNumber num2 = BigInteger.Parse(new string('9', 50));

        BigNumber result = num1 * num2;

        result.Significand.ToString().Length.ShouldBeGreaterThan(90);
    }

    [Fact]
    public void Divide_VeryLargeByVerySmall_HandlesCorrectly()
    {
        BigNumber large = BigInteger.Parse(new string('9', 100));
        BigNumber small = new(1, -50);

        BigNumber result = large / small;

        result.Exponent.ShouldBeGreaterThan(49);
    }

    [Fact]
    public void Normalize_VeryLargeWithTrailingZeros_HandlesCorrectly()
    {
        string manyNines = new string('9', 100) + new string('0', 50);
        var num = BigNumber.Parse(manyNines);

        BigNumber normalized = num.Normalize();

        normalized.Exponent.ShouldBe(50);
    }

    [Fact]
    public void CompareTo_VeryLargeSignificands_HandlesCorrectly()
    {
        BigNumber num1 = BigInteger.Parse(new string('9', 100));
        BigNumber num2 = BigInteger.Parse("1" + new string('0', 100));

        num1.CompareTo(num2).ShouldBeLessThan(0); // num1 has fewer digits
    }

    #endregion

    #region Mixed Sign Operations (+5 tests)

    [Fact]
    public void Add_PositiveAndNegativeCancel_ResultsInZero()
    {
        BigNumber positive = new(123456, 0);
        BigNumber negative = new(-123456, 0);

        BigNumber result = positive + negative;

        result.Significand.ShouldBe(BigInteger.Zero);
    }

    [Fact]
    public void Add_NegativeLargerMagnitude_ResultNegative()
    {
        BigNumber small = new(100, 0);
        BigNumber largeNeg = new(-500, 0);

        BigNumber result = small + largeNeg;

        result.Significand.ShouldBeLessThan(BigInteger.Zero);
    }

    [Fact]
    public void Multiply_PositiveByNegative_ResultNegative()
    {
        BigNumber positive = new(12345, 5);
        BigNumber negative = new(-67890, 3);

        BigNumber result = positive * negative;

        result.Significand.ShouldBeLessThan(BigInteger.Zero);
    }

    [Fact]
    public void Multiply_NegativeByNegative_ResultPositive()
    {
        BigNumber neg1 = new(-12345, 0);
        BigNumber neg2 = new(-67890, 0);

        BigNumber result = neg1 * neg2;

        result.Significand.ShouldBeGreaterThan(BigInteger.Zero);
    }

    [Fact]
    public void Divide_NegativeByPositive_ResultNegative()
    {
        BigNumber negative = new(-12345, 0);
        BigNumber positive = new(100, 0);

        BigNumber result = negative / positive;

        result.Significand.ShouldBeLessThan(BigInteger.Zero);
    }

    #endregion

    #region Zero and Identity Operations (+5 tests)

    [Fact]
    public void Add_AnyNumberPlusZero_ReturnsNumber()
    {
        BigNumber num = new(12345, 10);
        BigNumber zero = BigNumber.Zero;

        BigNumber result = num + zero;

        result.ShouldBe(num);
    }

    [Fact]
    public void Multiply_AnyNumberTimesZero_ReturnsZero()
    {
        BigNumber num = new(12345, 10);
        BigNumber zero = BigNumber.Zero;

        BigNumber result = num * zero;

        result.Significand.ShouldBe(BigInteger.Zero);
    }

    [Fact]
    public void Multiply_AnyNumberTimesOne_ReturnsNumber()
    {
        BigNumber num = new(12345, -5);
        BigNumber one = BigNumber.One;

        BigNumber result = num * one;

        result.Normalize().ShouldBe(num.Normalize());
    }

    [Fact]
    public void Divide_AnyNumberByOne_ReturnsNumber()
    {
        BigNumber num = new(12345, -5);
        BigNumber one = BigNumber.One;

        BigNumber result = num / one;

        result.ShouldBe(num);
    }

    [Fact]
    public void Subtract_NumberFromItself_ReturnsZero()
    {
        BigNumber num = new(12345, 10);

        BigNumber result = num - num;

        result.Significand.ShouldBe(BigInteger.Zero);
    }

    #endregion
}