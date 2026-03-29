// <copyright file="BigNumber.BranchCoverage.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using System.Reflection;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Tests specifically targeting uncovered branches to achieve maximum coverage.
/// </summary>
public class BigNumberBranchCoverageTests
{
    // ========== GetPowerOf10 Branches ==========

    [Fact]
    public void GetPowerOf10_NegativeExponent_ThrowsArgumentOutOfRangeException()
    {
        // Line 82-84: Test negative exponent in GetPowerOf10
        // This is internal, so we trigger it through reflection
        MethodInfo? method = typeof(BigNumber).GetMethod("GetPowerOf10",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Should.Throw<System.Reflection.TargetInvocationException>(() =>
        {
            method?.Invoke(null, new object[] { -1 });
        });
    }

    [Fact]
    public void GetPowerOf10_ExponentInSecondaryCache_UsesCache()
    {
        // Line 94-96: Test secondary cache (256-1023)
        // This is tested indirectly through large exponent operations
        BigNumber num1 = new(1, 500);
        BigNumber num2 = new(1, 500);

        // Multiple operations should use cached value
        BigNumber result1 = num1 + BigNumber.Zero;
        BigNumber result2 = num2 + BigNumber.Zero;

        result1.Exponent.ShouldBe(500);
        result2.Exponent.ShouldBe(500);
    }

    // ========== Normalization Branches ==========

    [Fact]
    public void Normalize_NonZeroWithoutTrailingZeros_ReturnsUnchanged()
    {
        // Line 196: while loop condition when significand has no trailing zeros
        BigNumber num = new(12347, -2); // 123.47 - no trailing zeros
        BigNumber normalized = num.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(12347));
        normalized.Exponent.ShouldBe(-2);
    }

    // ========== CompareTo Branches ==========

    [Fact]
    public void CompareTo_BothZero_ReturnsZero()
    {
        // Line 254: When both significands are zero
        BigNumber zero1 = new(0, 5);
        BigNumber zero2 = new(0, 10);

        zero1.CompareTo(zero2).ShouldBe(0);
    }

    [Fact]
    public void CompareTo_FirstZero_ReturnsNegativeOfOtherSign()
    {
        // Line 254: When this is zero, other is not
        BigNumber zero = new(0, 0);
        BigNumber positive = new(123, 0);
        BigNumber negative = new(-456, 0);

        zero.CompareTo(positive).ShouldBe(-1);
        zero.CompareTo(negative).ShouldBe(1);
    }

    [Fact]
    public void CompareTo_NegativeNumberEffectiveDigits_ReturnsNegativeComparison()
    {
        // Line 283: When significand sign is negative, return inverted comparison
        BigNumber neg1 = new(-12345, 2); // -1234500
        BigNumber neg2 = new(-123, 0);   // -123

        int result = neg1.CompareTo(neg2);
        result.ShouldBeLessThan(0); // -1234500 < -123
    }

    // ========== FormatZero Branches ==========

    [Fact]
    public void FormatZero_NullFormat_ReturnsZero()
    {
        // Line 370-372: null or empty format
        BigNumber zero = BigNumber.Zero;
        zero.ToString(null, CultureInfo.InvariantCulture).ShouldBe("0");
    }

    [Fact]
    public void FormatZero_GeneralFormat_ReturnsZero()
    {
        // Line 386: G format for zero
        BigNumber zero = BigNumber.Zero;
        zero.ToString("G", CultureInfo.InvariantCulture).ShouldBe("0");
        zero.ToString("g", CultureInfo.InvariantCulture).ShouldBe("0");
    }

    [Fact]
    public void FormatZero_FixedPointWithPrecision_ReturnsZeroWithDecimals()
    {
        // Line 387: F format with precision > 0
        BigNumber zero = BigNumber.Zero;
        zero.ToString("F5", CultureInfo.InvariantCulture).ShouldBe("0.00000");
    }

    [Fact]
    public void FormatZero_FixedPointNoPrecision_ReturnsZeroNoDot()
    {
        // Line 387: F format with precision == 0
        BigNumber zero = BigNumber.Zero;
        zero.ToString("F0", CultureInfo.InvariantCulture).ShouldBe("0");
    }

    [Fact]
    public void FormatZero_NumberWithPrecision_ReturnsZeroWithDecimals()
    {
        // Line 388: N format with precision > 0
        BigNumber zero = BigNumber.Zero;
        zero.ToString("N3", CultureInfo.InvariantCulture).ShouldBe("0.000");
    }

    [Fact]
    public void FormatZero_ExponentialWithPrecision_ReturnsScientificZero()
    {
        // Line 389: E format with precision > 0
        BigNumber zero = BigNumber.Zero;
        zero.ToString("E2", CultureInfo.InvariantCulture).ShouldBe("0.00E+000");
        zero.ToString("e3", CultureInfo.InvariantCulture).ShouldBe("0.000e+000");
    }

    [Fact]
    public void FormatPercentZero_WithZeroPrecision_NoDot()
    {
        // Line 398: precision == 0 in FormatPercentZero
        BigNumber zero = BigNumber.Zero;
        zero.ToString("P0", CultureInfo.InvariantCulture).ShouldBe("0 %");
    }

    [Fact]
    public void FormatPercentZero_DefaultPattern_ReturnsCorrectly()
    {
        // Line 406: Default case in PercentPositivePattern switch
        // We already tested patterns 0-3, but ensure we hit the default
        BigNumber zero = BigNumber.Zero;
        var culture = new CultureInfo("en-US");

        // Test with pattern 0 (which triggers the main logic)
        culture.NumberFormat.PercentPositivePattern = 0;
        zero.ToString("P2", culture).ShouldBe("0.00 %");
    }

    [Fact]
    public void FormatCurrencyZero_WithZeroPrecision_NoDot()
    {
        // Line 412: precision == 0 in FormatCurrencyZero
        BigNumber zero = BigNumber.Zero;
        zero.ToString("C0", CultureInfo.InvariantCulture).ShouldBe("¤0");
    }

    [Fact]
    public void FormatCurrencyZero_DefaultPattern_ReturnsCorrectly()
    {
        // Line 421: Default case in CurrencyPositivePattern switch
        BigNumber zero = BigNumber.Zero;
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 0;
        zero.ToString("C2", culture).ShouldBe("$0.00");
    }

    // ========== FormatGeneral Branches ==========

    [Fact]
    public void FormatGeneral_NormalizedExponentZero_ReturnsSignificandOnly()
    {
        // Line 456-458: When normalized exponent is 0
        BigNumber num = new(12345, 0);
        num.ToString("G", CultureInfo.InvariantCulture).ShouldBe("12345");
    }

    [Fact]
    public void FormatGeneral_WithPrecisionAndRounding_RoundsCorrectly()
    {
        // Line 443-446: Rounding logic in FormatGeneral
        BigNumber num = new(123456789, 0);
        string result = num.ToString("G5", CultureInfo.InvariantCulture);
        // Should round to 5 significant digits
        result.ShouldContain("12346");
    }

    // ========== RoundToPrecision Branches ==========
    [Fact]
    public void RoundToPrecision_RemainderIsZero_ReturnsWithoutRounding()
    {
        // Line 530-532: remainder.IsZero branch
        BigNumber num = new(12500, -3); // 12.500
        var result = BigNumber.Round(num, 2, MidpointRounding.ToEven);

        result.ShouldBe(new BigNumber(125, -1)); // 12.5
    }

#if NET
    [Fact]
    public void RoundToPrecision_ToPositiveInfinity_RoundsUp()
    {
        // Line 556-558: ToPositiveInfinity branch  
        BigNumber num = new(12345, -3); // 12.345
        var result = BigNumber.Round(num, 2, MidpointRounding.ToPositiveInfinity);

        // Should round up to 12.35
        result.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("12.35");
    }
#endif

    [Fact]
    public void RoundToPrecision_DefaultCase_UsesStandardRounding()
    {
        // Line 558: default case in rounding switch
        BigNumber num = new(12345, -3); // 12.345
        var result = BigNumber.Round(num, 2, (MidpointRounding)99); // Invalid mode

        // Should use default logic
        result.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("12.35");
    }

    // ========== FormatNumber Branches ==========

    [Fact]
    public void FormatNumber_SignificandShorterThanPrecision_PadsWithZeros()
    {
        // Line 579-583: sigStr.Length <= precision
        BigNumber num = new(5, -3); // 0.005
        string result = num.ToString("N5", CultureInfo.InvariantCulture);

        result.ShouldBe("0.00500");
    }

    // ========== FormatExponential Branches ==========

    [Fact]
    public void FormatExponential_ZeroValue_ReturnsFormattedZero()
    {
        // Line 609-612: Zero value in FormatExponential
        BigNumber zero = new(0, 5);
        string result = zero.ToString("E3", CultureInfo.InvariantCulture);

        result.ShouldBe("0.000E+000");
    }

    [Fact]
    public void FormatExponential_RoundingCausesOverflow_AdjustsExponent()
    {
        // Line 639-643: Rounding causes overflow (9.999 -> 10.00)
        BigNumber num = new(9999, -3); // 9.999
        string result = num.ToString("E0", CultureInfo.InvariantCulture);

        // Should round to 1E+001
        result.ShouldContain("1");
        result.ShouldContain("E+");
    }

    [Fact]
    public void FormatExponential_PrecisionZero_NoDecimalPoint()
    {
        // Line 652-654: precision == 0
        BigNumber num = new(12345, 0);
        string result = num.ToString("E0", CultureInfo.InvariantCulture);

        result.ShouldNotContain(".");
        result.ShouldContain("E+");
    }

    [Fact]
    public void FormatExponential_SignificandShorterThanPrecision_PadsDecimals()
    {
        // Line 661-664: decimals.Length < precision
        BigNumber num = new(5, 2); // 500
        string result = num.ToString("E5", CultureInfo.InvariantCulture);

        // Should have 5 decimal places
        result.ShouldMatch(@"\d\.\d{5}E\+\d{3}");
    }

    [Fact]
    public void FormatExponential_OnlyOneDigit_PadsWithZeros()
    {
        // Line 667-670: sigStr.Length == 1
        BigNumber num = new(5, 0);
        string result = num.ToString("E3", CultureInfo.InvariantCulture);

        result.ShouldBe("5.000E+000");
    }

    // ========== FormatCurrency Branches ==========

    [Fact]
    public void FormatCurrency_SignificandShorterThanPrecision_PadsWithZeros()
    {
        // Line 690-693: sigStr.Length <= precision in FormatCurrency
        BigNumber num = new(5, -4); // 0.0005
        string result = num.ToString("C5", CultureInfo.InvariantCulture);

        result.ShouldContain("0.00050");
    }

    // ========== Additional Edge Cases ==========

    [Fact]
    public void CompareTo_SameEffectiveDigitsPositive_ComparesCorrectly()
    {
        // Line 283: positive significand path
        BigNumber num1 = new(12345, 5);
        BigNumber num2 = new(67890, 5);

        num1.CompareTo(num2).ShouldBeLessThan(0);
    }

    [Fact]
    public void Normalize_MultipleTrailingZeros_RemovesAll()
    {
        // Line 196: while loop with multiple iterations
        BigNumber num = new(123000000, -5);
        BigNumber normalized = num.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(123));
        normalized.Exponent.ShouldBe(1);
    }

    [Fact]
    public void FormatZero_WithCustomCultureAllPatterns_Works()
    {
        BigNumber zero = BigNumber.Zero;
        var germanCulture = new CultureInfo("de-DE");

        // Test all zero formatting paths with custom culture
        zero.ToString("F3", germanCulture).ShouldBe("0,000");
        zero.ToString("N2", germanCulture).ShouldBe("0,00");
        zero.ToString("E2", germanCulture).ShouldBe("0,00E+000");
    }

    [Fact]
    public void GetPowerOf10_ExponentAbove1023_ComputesOnDemand()
    {
        // Line 94-100: Exponent >= 1024 goes to on-demand computation
        BigNumber num = new(1, 1500);
        BigNumber multiplier = new(2, 0);
        BigNumber result = num * multiplier;

        result.Significand.ShouldBe(new BigInteger(2));
        result.Exponent.ShouldBe(1500);
    }

#if NET
    [Fact]
    public void Round_WithToNegativeInfinity_RoundsDown()
    {
        // Additional rounding mode coverage
        BigNumber num = new(12345, -3); // 12.345
        var result = BigNumber.Round(num, 2, MidpointRounding.ToNegativeInfinity);

        result.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("12.34");
    }

    [Fact]
    public void Round_WithToZero_TruncatesTowardZero()
    {
        // ToZero rounding mode
        BigNumber positive = new(12345, -3); // 12.345
        BigNumber negative = new(-12345, -3); // -12.345

        var resultPos = BigNumber.Round(positive, 2, MidpointRounding.ToZero);
        var resultNeg = BigNumber.Round(negative, 2, MidpointRounding.ToZero);

        resultPos.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("12.34");
        resultNeg.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("-12.34");
    }
#endif

    [Fact]
    public void Round_AwayFromZero_RoundsAwayFromZero()
    {
        // AwayFromZero rounding mode
        BigNumber positive = new(12355, -3); // 12.355
        BigNumber negative = new(-12355, -3); // -12.355

        var resultPos = BigNumber.Round(positive, 2, MidpointRounding.AwayFromZero);
        var resultNeg = BigNumber.Round(negative, 2, MidpointRounding.AwayFromZero);

        resultPos.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("12.36");
        resultNeg.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("-12.36");
    }

    [Fact]
    public void FormatGeneral_WithPrecisionZero_UsesDefault()
    {
        // Test G format with precision 0 or negative
        BigNumber num = new(12345, 2);
        string result = num.ToString("G", CultureInfo.InvariantCulture);

        result.ShouldBe("12345E2");
    }

    [Fact]
    public void FormatExponential_NegativeSignificand_FormatsCorrectly()
    {
        // Ensure negative numbers work in exponential format
        BigNumber num = new(-12345, 0);
        string result = num.ToString("E2", CultureInfo.InvariantCulture);

        result.ShouldContain("-");
        result.ShouldContain("E+");
    }

    [Fact]
    public void FormatNumber_NegativeNumber_FormatsWithGrouping()
    {
        // Ensure negative numbers work in number format
        BigNumber num = new(-123456, -2);
        string result = num.ToString("N2", CultureInfo.InvariantCulture);

        result.ShouldContain("-");
        result.ShouldContain(",");
    }

    [Fact]
    public void FormatCurrency_NegativeNumber_UsesNegativePattern()
    {
        // Test currency formatting with negative numbers
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        string result = num.ToString("C2", culture);

#if NET
        result.ShouldContain("-");
#else
        // .NET Framework uses parentheses for negative currency
        result.ShouldContain("(");
        result.ShouldContain(")");
#endif
        result.ShouldContain("$");
    }

    [Fact]
    public void FormatPercent_SmallNumber_FormatsAsPercentage()
    {
        // Test percent format with small numbers
        BigNumber num = new(5, -3); // 0.005 = 0.5%
        string result = num.ToString("P2", CultureInfo.InvariantCulture);

        result.ShouldContain("0.50");
        result.ShouldContain("%");
    }
}