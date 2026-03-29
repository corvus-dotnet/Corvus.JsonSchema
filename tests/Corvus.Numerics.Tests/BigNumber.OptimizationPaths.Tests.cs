// <copyright file="BigNumber.OptimizationPaths.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Phase 1: Core optimization path tests - Cache, Normalization, Comparison, Rounding.
/// Target: +0.8% coverage (10 branches).
/// </summary>
public class BigNumberOptimizationPathsTests
{
    #region Power-of-10 Cache Boundaries (+2 branches)

    [Fact]
    public void PowerOf10Cache_Exponent0_UsesFirstCacheEntry()
    {
        // Test first entry in primary cache
        BigNumber num = new(7, 0);
        BigNumber multiplier = new(3, 0);
        BigNumber result = num * multiplier;

        result.Significand.ShouldBe(new BigInteger(21));
        result.Exponent.ShouldBe(0);
    }

    [Fact]
    public void PowerOf10Cache_Exponent255_UsesLastPrimaryCacheEntry()
    {
        // Test last entry in primary cache (0-255)
        BigNumber num = new(5, 255);
        BigNumber result = num + BigNumber.Zero;

        result.Significand.ShouldBe(new BigInteger(5));
        result.Exponent.ShouldBe(255);
    }

    [Fact]
    public void PowerOf10Cache_Exponent256_UsesFirstSecondaryCacheEntry()
    {
        // Test first entry in secondary cache (256-1023)
        BigNumber num = new(7, 256);
        BigNumber result = num * new BigNumber(1, 0);

        result.Significand.ShouldBe(new BigInteger(7));
        result.Exponent.ShouldBe(256);
    }

    [Fact]
    public void PowerOf10Cache_Exponent1023_UsesLastSecondaryCacheEntry()
    {
        // Test last entry in secondary cache
        BigNumber num = new(3, 1023);
        BigNumber result = num + BigNumber.Zero;

        result.Exponent.ShouldBe(1023);
    }

    [Fact]
    public void PowerOf10Cache_Exponent1024_UsesOnDemandComputation()
    {
        // Test first value requiring on-demand computation (>1023)
        BigNumber num = new(9, 1024);
        BigNumber result = num * new BigNumber(2, 0);

        result.Significand.ShouldBe(new BigInteger(18));
        result.Exponent.ShouldBe(1024);
    }

    [Fact]
    public void PowerOf10Cache_VeryLargeExponent_ComputesOnDemand()
    {
        // Test very large exponent requiring on-demand computation
        BigNumber num = new(1, 5000);
        BigNumber result = num * new BigNumber(5, 0);

        result.Significand.ShouldBe(new BigInteger(5));
        result.Exponent.ShouldBe(5000);
    }

    [Fact]
    public void PowerOf10Cache_MultipleOperationsAtBoundary_ReusesCache()
    {
        // Verify cache is reused for multiple operations at same exponent
        BigNumber num1 = new(2, 500);
        BigNumber num2 = new(3, 500);

        BigNumber result1 = num1 * new BigNumber(1, 0);
        BigNumber result2 = num2 * new BigNumber(1, 0);

        result1.Exponent.ShouldBe(500);
        result2.Exponent.ShouldBe(500);
    }

    #endregion

    #region Normalization Iterations (+1 branch)

    [Fact]
    public void Normalize_NoTrailingZeros_NoIteration()
    {
        // Number with no trailing zeros - while loop doesn't iterate
        BigNumber num = new(12347, -2);
        BigNumber normalized = num.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(12347));
        normalized.Exponent.ShouldBe(-2);
    }

    [Fact]
    public void Normalize_OneTrailingZero_OneIteration()
    {
        // Number with 1 trailing zero - while loop iterates once
        BigNumber num = new(12340, -3);
        BigNumber normalized = num.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(1234));
        normalized.Exponent.ShouldBe(-2);
    }

    [Fact]
    public void Normalize_ThreeTrailingZeros_ThreeIterations()
    {
        // Number with 3 trailing zeros - while loop iterates 3 times
        BigNumber num = new(123000, -4);
        BigNumber normalized = num.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(123));
        normalized.Exponent.ShouldBe(-1);
    }

    [Fact]
    public void Normalize_FiveTrailingZeros_FiveIterations()
    {
        // Number with 7 trailing zeros - while loop iterates 7 times
        // 1230000000 has 7 trailing zeros -> removes them -> 123
        BigNumber num = new(1230000000, -7);
        BigNumber normalized = num.Normalize();

        // 123 with exponent -7 + 7 = 0
        normalized.Significand.ShouldBe(new BigInteger(123));
        normalized.Exponent.ShouldBe(0);
    }

    [Fact]
    public void Normalize_AllTrailingZeros_ConvertsToZero()
    {
        // Number that is effectively zero
        BigNumber num = new(10000, -4);
        BigNumber normalized = num.Normalize();

        normalized.Significand.ShouldBe(BigInteger.One);
        normalized.Exponent.ShouldBe(0);
    }

    [Fact]
    public void Normalize_LargeNumberWithManyTrailingZeros_HandlesCorrectly()
    {
        // Very large number with many trailing zeros
        var num = BigNumber.Parse("123000000000000000000");
        BigNumber normalized = num.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(123));
        normalized.Exponent.ShouldBe(18);
    }

    #endregion

    #region Comparison Fast Paths (+4 branches)

    [Fact]
    public void CompareTo_BothZeroDifferentExponents_ReturnsZero()
    {
        // Both significands are zero - fast path
        BigNumber zero1 = new(0, 5);
        BigNumber zero2 = new(0, 100);

        zero1.CompareTo(zero2).ShouldBe(0);
        zero2.CompareTo(zero1).ShouldBe(0);
    }

    [Fact]
    public void CompareTo_FirstZeroSecondPositive_ReturnsNegative()
    {
        // First is zero, second is positive
        BigNumber zero = new(0, 0);
        BigNumber positive = new(123, 0);

        zero.CompareTo(positive).ShouldBe(-1);
    }

    [Fact]
    public void CompareTo_FirstZeroSecondNegative_ReturnsPositive()
    {
        // First is zero, second is negative
        BigNumber zero = new(0, 0);
        BigNumber negative = new(-456, 0);

        zero.CompareTo(negative).ShouldBe(1);
    }

    [Fact]
    public void CompareTo_SecondZeroFirstPositive_ReturnsPositive()
    {
        // Second is zero, first is positive - tests other.Significand.IsZero branch
        BigNumber positive = new(789, 0);
        BigNumber zero = new(0, 50);

        positive.CompareTo(zero).ShouldBe(1);
    }

    [Fact]
    public void CompareTo_SecondZeroFirstNegative_ReturnsNegative()
    {
        // Second is zero, first is negative
        BigNumber negative = new(-321, 0);
        BigNumber zero = new(0, 25);

        negative.CompareTo(zero).ShouldBe(-1);
    }

    [Fact]
    public void CompareTo_BothNegativeDifferentMagnitudes_ComparesCorrectly()
    {
        // Both negative - tests sign comparison path
        BigNumber smaller = new(-12345, 0);  // -12345
        BigNumber larger = new(-123, 0);     // -123

        smaller.CompareTo(larger).ShouldBeLessThan(0);
        larger.CompareTo(smaller).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_BothNegativeSameExponent_UsesEffectiveDigits()
    {
        // Negative numbers with same exponent - tests effective digit comparison
        BigNumber num1 = new(-12345, 5);
        BigNumber num2 = new(-67890, 5);

        num1.CompareTo(num2).ShouldBeGreaterThan(0); // -12345E5 > -67890E5
    }

    [Fact]
    public void CompareTo_SameExponentPositiveNumbers_SkipsAlignment()
    {
        // Same exponent - can skip expensive alignment
        BigNumber num1 = new(12345, 10);
        BigNumber num2 = new(67890, 10);

        num1.CompareTo(num2).ShouldBeLessThan(0);
    }

    #endregion

    #region Rounding Mode Edge Cases (+3 branches)

    [Fact]
    public void Round_ToEven_ExactHalfEvenQuotient_RoundsToEven()
    {
        // At exactly 0.5, quotient is even - should NOT round up
        BigNumber num = new(125, -2); // 1.25
        var result = BigNumber.Round(num, 1, MidpointRounding.ToEven);

        // 1.25 -> quotient is 12 (even), so round down to 1.2
        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.2");
    }

    [Fact]
    public void Round_ToEven_ExactHalfOddQuotient_RoundsToEven()
    {
        // At exactly 0.5, quotient is odd - should round up
        BigNumber num = new(135, -2); // 1.35
        var result = BigNumber.Round(num, 1, MidpointRounding.ToEven);

        // 1.35 -> quotient is 13 (odd), so round up to 1.4
        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.4");
    }

    [Fact]
    public void Round_ToEven_LessThanHalf_NoRounding()
    {
        // Less than 0.5 - never rounds
        BigNumber num = new(124, -2); // 1.24
        var result = BigNumber.Round(num, 1, MidpointRounding.ToEven);

        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.2");
    }

    [Fact]
    public void Round_ToEven_GreaterThanHalf_AlwaysRounds()
    {
        // Greater than 0.5 - always rounds
        BigNumber num = new(126, -2); // 1.26
        var result = BigNumber.Round(num, 1, MidpointRounding.ToEven);

        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.3");
    }

    [Fact]
    public void Round_AwayFromZero_ExactHalfPositive_RoundsAway()
    {
        // AwayFromZero at exact half with positive number
        BigNumber num = new(125, -2); // 1.25
        var result = BigNumber.Round(num, 1, MidpointRounding.AwayFromZero);

        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.3");
    }

    [Fact]
    public void Round_AwayFromZero_ExactHalfNegative_RoundsAway()
    {
        // AwayFromZero at exact half with negative number
        BigNumber num = new(-125, -2); // -1.25
        var result = BigNumber.Round(num, 1, MidpointRounding.AwayFromZero);

        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("-1.3");
    }

#if NET
    [Fact]
    public void Round_ToNegativeInfinity_PositiveNumber_FloorBehavior()
    {
        // ToNegativeInfinity with positive - rounds down
        BigNumber num = new(127, -2); // 1.27
        var result = BigNumber.Round(num, 1, MidpointRounding.ToNegativeInfinity);

        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.2");
    }

    [Fact]
    public void Round_ToNegativeInfinity_NegativeNumber_FloorBehavior()
    {
        // ToNegativeInfinity with negative - rounds away from zero
        BigNumber num = new(-122, -2); // -1.22
        var result = BigNumber.Round(num, 1, MidpointRounding.ToNegativeInfinity);

        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("-1.3");
    }

    [Fact]
    public void Round_ToPositiveInfinity_PositiveNumber_CeilingBehavior()
    {
        // ToPositiveInfinity with positive - rounds away from zero
        BigNumber num = new(121, -2); // 1.21
        var result = BigNumber.Round(num, 1, MidpointRounding.ToPositiveInfinity);

        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.3");
    }

    [Fact]
    public void Round_ToPositiveInfinity_NegativeNumber_CeilingBehavior()
    {
        // ToPositiveInfinity with negative - rounds toward zero
        BigNumber num = new(-129, -2); // -1.29
        var result = BigNumber.Round(num, 1, MidpointRounding.ToPositiveInfinity);

        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("-1.2");
    }

    [Fact]
    public void Round_ToZero_PositiveNumber_TruncatesBehavior()
    {
        // ToZero with positive - always rounds down
        BigNumber num = new(129, -2); // 1.29
        var result = BigNumber.Round(num, 1, MidpointRounding.ToZero);

        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.2");
    }

    [Fact]
    public void Round_ToZero_NegativeNumber_TruncatesBehavior()
    {
        // ToZero with negative - rounds toward zero
        BigNumber num = new(-129, -2); // -1.29
        var result = BigNumber.Round(num, 1, MidpointRounding.ToZero);

        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("-1.2");
    }
#endif

    #endregion
}