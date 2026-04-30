// <copyright file="BigNumber.Validation.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberValidationTests
{
    #region Division Validation

    [Fact]
    public void Divide_NegativePrecision_ThrowsArgumentOutOfRangeException()
    {
        BigNumber dividend = new(10, 0);
        BigNumber divisor = new(3, 0);

        Should.Throw<ArgumentOutOfRangeException>(() => BigNumber.Divide(dividend, divisor, -1));
    }

    [Fact]
    public void Divide_ZeroPrecision_Succeeds()
    {
        BigNumber dividend = new(10, 0);
        BigNumber divisor = new(2, 0);

        var result = BigNumber.Divide(dividend, divisor, 0);

        result.ShouldBe(new BigNumber(5, 0));
    }

    [Fact]
    public void Divide_MaxValidPrecision_Succeeds()
    {
        BigNumber dividend = new(10, 0);
        BigNumber divisor = new(3, 0);

        var result = BigNumber.Divide(dividend, divisor, 255);

        // Should complete without error
        result.ShouldNotBe(BigNumber.Zero);
    }

    [Fact]
    public void Divide_PrecisionAbove255_ThrowsArgumentOutOfRangeException()
    {
        BigNumber dividend = new(10, 0);
        BigNumber divisor = new(3, 0);

        Should.Throw<ArgumentOutOfRangeException>(() => BigNumber.Divide(dividend, divisor, 256));
    }

    [Fact]
    public void Divide_PrecisionWellAbove255_ThrowsArgumentOutOfRangeException()
    {
        BigNumber dividend = new(10, 0);
        BigNumber divisor = new(3, 0);

        Should.Throw<ArgumentOutOfRangeException>(() => BigNumber.Divide(dividend, divisor, 1000));
    }

    #endregion

    #region Parsing Validation

    [Fact]
    public void Parse_VeryLongInput_ReturnsFalse()
    {
        // Create a string longer than MaxInputLength (10,000 characters)
        string veryLongInput = new('1', 10_001);
        bool result = BigNumber.TryParse(veryLongInput, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Parse_ExactlyMaxLength_Succeeds()
    {
        // Create a string exactly at MaxInputLength
        string maxInput = new('9', 10_000);

        bool result = BigNumber.TryParse(maxInput, out BigNumber parsed);

        // Should succeed (or at least not fail due to length check)
        // The actual parsing might fail for other reasons, but length check should pass
        result.ShouldBeTrue();
        parsed.Significand.ShouldNotBe(BigInteger.Zero);
    }

    [Fact]
    public void Parse_JustUnderMaxLength_Succeeds()
    {
        string input = new('7', 9_999);
        bool result = BigNumber.TryParse(input, out _);

        result.ShouldBeTrue();
    }

    #endregion

    #region Round Validation

    [Fact]
    public void Round_NegativeDecimals_ThrowsArgumentOutOfRangeException()
    {
        BigNumber value = new(123, 0);

        Should.Throw<ArgumentOutOfRangeException>(() => BigNumber.Round(value, -1));
    }

#if NET
    [Fact]
    public void Round_ToZero_RoundsTowardZero()
    {
        var value1 = BigNumber.Parse("2.7");
        var value2 = BigNumber.Parse("-2.7");

        var result1 = BigNumber.Round(value1, 0, MidpointRounding.ToZero);
        var result2 = BigNumber.Round(value2, 0, MidpointRounding.ToZero);

        // ToZero truncates toward zero (no rounding up)
        result1.ShouldBe(new BigNumber(2, 0));
        result2.ShouldBe(new BigNumber(-2, 0));
    }

    [Fact]
    public void Round_ToPositiveInfinity_RoundsUp()
    {
        var value1 = BigNumber.Parse("2.1");
        var value2 = BigNumber.Parse("-2.1");

        var result1 = BigNumber.Round(value1, 0, MidpointRounding.ToPositiveInfinity);
        var result2 = BigNumber.Round(value2, 0, MidpointRounding.ToPositiveInfinity);

        // ToPositiveInfinity: round toward positive infinity (ceiling)
        // 2.1 -> 3 (positive, round up), -2.1 -> -2 (negative, round toward zero)
        result1.ShouldBe(new BigNumber(3, 0));
        result2.ShouldBe(new BigNumber(-2, 0));
    }

    [Fact]
    public void Round_ToNegativeInfinity_RoundsDown()
    {
        var value1 = BigNumber.Parse("2.1");
        var value2 = BigNumber.Parse("-2.1");

        var result1 = BigNumber.Round(value1, 0, MidpointRounding.ToNegativeInfinity);
        var result2 = BigNumber.Round(value2, 0, MidpointRounding.ToNegativeInfinity);

        // ToNegativeInfinity: round toward negative infinity (floor)
        // 2.1 -> 2 (positive, round toward zero), -2.1 -> -3 (negative, round away)
        result1.ShouldBe(new BigNumber(2, 0));
        result2.ShouldBe(new BigNumber(-3, 0));
    }
#endif

    #endregion
}