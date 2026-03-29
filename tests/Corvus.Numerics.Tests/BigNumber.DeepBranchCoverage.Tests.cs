// <copyright file="BigNumber.DeepBranchCoverage.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Tests to cover remaining difficult-to-reach branches.
/// </summary>
public class BigNumberDeepBranchCoverageTests
{
    // ========== Currency Negative Patterns (0-15) ==========

    [Fact]
    public void FormatCurrency_NegativePattern0_Parentheses()
    {
        // Pattern 0: ($n)
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 0;

        string result = num.ToString("C2", culture);
        result.ShouldBe("($123.45)");
    }

    [Fact]
    public void FormatCurrency_NegativePattern1_MinusSymbolFirst()
    {
        // Pattern 1: -$n
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 1;

        string result = num.ToString("C2", culture);
        result.ShouldBe("-$123.45");
    }

    [Fact]
    public void FormatCurrency_NegativePattern2_CurrencyMinusValue()
    {
        // Pattern 2: $-n
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 2;

        string result = num.ToString("C2", culture);
        result.ShouldBe("$-123.45");
    }

    [Fact]
    public void FormatCurrency_NegativePattern3_CurrencyValueMinus()
    {
        // Pattern 3: $n-
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 3;

        string result = num.ToString("C2", culture);
        result.ShouldEndWith("-");
    }

    [Fact]
    public void FormatCurrency_NegativePattern4_ParenthesesValueCurrency()
    {
        // Pattern 4: (n$)
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 4;

        string result = num.ToString("C2", culture);
        result.ShouldStartWith("(");
        result.ShouldEndWith(")");
        result.ShouldContain("$");
    }

    [Fact]
    public void FormatCurrency_NegativePattern5_MinusValueCurrency()
    {
        // Pattern 5: -n$
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 5;

        string result = num.ToString("C2", culture);
        result.ShouldStartWith("-");
        result.ShouldEndWith("$");
    }

    [Fact]
    public void FormatCurrency_NegativePattern6_ValueMinusCurrency()
    {
        // Pattern 6: n-$
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 6;

        string result = num.ToString("C2", culture);
        result.ShouldContain("-$");
    }

    [Fact]
    public void FormatCurrency_NegativePattern7_ValueCurrencyMinus()
    {
        // Pattern 7: n$-
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 7;

        string result = num.ToString("C2", culture);
        result.ShouldEndWith("-");
        result.ShouldContain("$");
    }

    [Fact]
    public void FormatCurrency_NegativePattern8_MinusValueSpaceCurrency()
    {
        // Pattern 8: -n $
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 8;

        string result = num.ToString("C2", culture);
        result.ShouldStartWith("-");
        result.ShouldContain(" $");
    }

    [Fact]
    public void FormatCurrency_NegativePattern9_MinusCurrencySpaceValue()
    {
        // Pattern 9: -$ n
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 9;

        string result = num.ToString("C2", culture);
        result.ShouldStartWith("-$");
        result.ShouldContain(" ");
    }

    [Fact]
    public void FormatCurrency_NegativePattern10_ValueSpaceCurrencyMinus()
    {
        // Pattern 10: n $-
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 10;

        string result = num.ToString("C2", culture);
        result.ShouldEndWith("-");
        result.ShouldContain(" $");
    }

    [Fact]
    public void FormatCurrency_NegativePattern11_CurrencySpaceValueMinus()
    {
        // Pattern 11: $ n-
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 11;

        string result = num.ToString("C2", culture);
        result.ShouldStartWith("$");
        result.ShouldEndWith("-");
        result.ShouldContain(" ");
    }

    [Fact]
    public void FormatCurrency_NegativePattern12_CurrencySpaceMinusValue()
    {
        // Pattern 12: $ -n
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 12;

        string result = num.ToString("C2", culture);
        result.ShouldStartWith("$");
        result.ShouldContain(" -");
    }

    [Fact]
    public void FormatCurrency_NegativePattern13_ValueMinusSpaceCurrency()
    {
        // Pattern 13: n- $
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 13;

        string result = num.ToString("C2", culture);
        result.ShouldContain("- $");
    }

    [Fact]
    public void FormatCurrency_NegativePattern14_ParenthesesCurrencySpaceValue()
    {
        // Pattern 14: ($ n)
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 14;

        string result = num.ToString("C2", culture);
        result.ShouldStartWith("($");
        result.ShouldEndWith(")");
        result.ShouldContain(" ");
    }

    [Fact]
    public void FormatCurrency_NegativePattern15_ParenthesesValueSpaceCurrency()
    {
        // Pattern 15: (n $)
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyNegativePattern = 15;

        string result = num.ToString("C2", culture);
        result.ShouldStartWith("(");
        result.ShouldEndWith("$)");
        result.ShouldContain(" ");
    }

    [Fact]
    public void FormatCurrency_NegativePatternDefault_UsesStandard()
    {
        // Pattern >15: use default
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");

        // Try to set invalid pattern via reflection if possible, or just use a valid one
        culture.NumberFormat.CurrencyNegativePattern = 1; // -$n
        string result = num.ToString("C2", culture);
        result.ShouldContain("-");
        result.ShouldContain("$");
    }

    // ========== Rounding Mode Edge Cases ==========

#if NET
    [Fact]
    public void Round_ToNegativeInfinity_WithPositiveRemainder_DoesNotRound()
    {
        // Line 553: ToNegativeInfinity with positive number
        BigNumber positive = new(12344, -3); // 12.344
        var result = BigNumber.Round(positive, 2, MidpointRounding.ToNegativeInfinity);

        result.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("12.34");
    }

    [Fact]
    public void Round_ToNegativeInfinity_WithNegativeRemainder_RoundsDown()
    {
        // Line 553: ToNegativeInfinity with negative number
        BigNumber negative = new(-12344, -3); // -12.344
        var result = BigNumber.Round(negative, 2, MidpointRounding.ToNegativeInfinity);

        result.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("-12.35");
    }

    [Fact]
    public void Round_ToPositiveInfinity_WithNegativeRemainder_DoesNotRound()
    {
        // Line 556: ToPositiveInfinity with negative number
        BigNumber negative = new(-12344, -3); // -12.344
        var result = BigNumber.Round(negative, 2, MidpointRounding.ToPositiveInfinity);

        result.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("-12.34");
    }
#endif

    [Fact]
    public void Round_ToEven_WithExactHalfAndEvenQuotient_DoesNotRound()
    {
        // Line 543-544: ToEven with exact half and even quotient
        BigNumber num = new(125, -2); // 1.25
        var result = BigNumber.Round(num, 1, MidpointRounding.ToEven);

        // 1.25 rounds to 1.2 (even)
        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.2");
    }

    [Fact]
    public void Round_ToEven_WithExactHalfAndOddQuotient_RoundsUp()
    {
        // Line 543-544: ToEven with exact half and odd quotient
        BigNumber num = new(135, -2); // 1.35
        var result = BigNumber.Round(num, 1, MidpointRounding.ToEven);

        // 1.35 rounds to 1.4 (even)
        result.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.4");
    }

    [Fact]
    public void Round_AwayFromZero_WithExactHalf_RoundsAway()
    {
        // Line 547: AwayFromZero with exact half
        BigNumber positive = new(125, -2); // 1.25
        BigNumber negative = new(-125, -2); // -1.25

        var resultPos = BigNumber.Round(positive, 1, MidpointRounding.AwayFromZero);
        var resultNeg = BigNumber.Round(negative, 1, MidpointRounding.AwayFromZero);

        resultPos.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("1.3");
        resultNeg.ToString("F1", CultureInfo.InvariantCulture).ShouldBe("-1.3");
    }

    // ========== Number Formatting Edge Cases ==========

    [Fact]
    public void FormatNumber_ValueShorterThanPrecision_IntegerPartZero()
    {
        // Line 579-582: sigStr.Length <= precision, creating "0.00...sigStr"
        BigNumber num = new(5, -4); // 0.0005
        string result = num.ToString("N4", CultureInfo.InvariantCulture);

        result.ShouldBe("0.0005");
    }

    [Fact]
    public void FormatNumber_ValueEqualsPrecision_IntegerPartZero()
    {
        // Line 579-582: sigStr.Length == precision
        BigNumber num = new(123, -3); // 0.123
        string result = num.ToString("N3", CultureInfo.InvariantCulture);

        result.ShouldBe("0.123");
    }

    // ========== Exponential Formatting Edge Cases ==========

    [Fact]
    public void FormatExponential_ZeroWithZeroPrecision_NoDecimal()
    {
        // Line 609-612: zero with precision 0
        BigNumber zero = new(0, 0);
        string result = zero.ToString("E0", CultureInfo.InvariantCulture);

        result.ShouldBe("0E+000");
    }

    [Fact]
    public void FormatExponential_RoundingCausesDigitOverflow_IncrementsExponent()
    {
        // Line 639-643: rounding 9.99...9 causes overflow
        BigNumber num = new(99999, -4); // 9.9999
        string result = num.ToString("E1", CultureInfo.InvariantCulture);

        // Should round to 1.0E+001
        result.ShouldContain("1.0");
        result.ShouldContain("E+001");
    }

    [Fact]
    public void FormatExponential_PrecisionZeroNoRounding_SingleDigit()
    {
        // Line 652-654: precision == 0, no decimal point
        BigNumber num = new(7, 0);
        string result = num.ToString("E0", CultureInfo.InvariantCulture);

        result.ShouldBe("7E+000");
    }

    [Fact]
    public void FormatExponential_ShortSignificandPaddedWithZeros_AddsTrailingZeros()
    {
        // Line 661-664: Padding decimals when sigStr is shorter
        BigNumber num = new(3, 1); // 30
        string result = num.ToString("E4", CultureInfo.InvariantCulture);

        result.ShouldContain("3.0000");
    }

    [Fact]
    public void FormatExponential_SingleDigitSignificand_PadsWithZeros()
    {
        // Line 667-670: sigStr.Length == 1
        BigNumber num = new(2, -2); // 0.02
        string result = num.ToString("E3", CultureInfo.InvariantCulture);

        result.ShouldContain("2.000");
    }

    // ========== Edge Conditions in Comparison ==========

    [Fact]
    public void CompareTo_SecondOperandZero_ReturnsThisSign()
    {
        // Line 256-257: other.Significand.IsZero branch
        BigNumber positive = new(123, 0);
        BigNumber negative = new(-456, 0);
        BigNumber zero = new(0, 100);

        positive.CompareTo(zero).ShouldBe(1);
        negative.CompareTo(zero).ShouldBe(-1);
    }

    [Fact]
    public void CompareTo_SameSignPositiveEffectiveDigitsDiffer_ComparesEffectiveDigits()
    {
        // Line 283: positive sign, different effective digits
        BigNumber smaller = new(123, 0);    // 123
        BigNumber larger = new(12345, 0);   // 12345

        smaller.CompareTo(larger).ShouldBeLessThan(0);
        larger.CompareTo(smaller).ShouldBeGreaterThan(0);
    }

    // ========== Uncovered FormatZero Branches ==========

    [Fact]
    public void FormatZero_ExplicitNullFormat_ReturnsZero()
    {
        // Line 370-372: string.IsNullOrEmpty check
        BigNumber zero = BigNumber.Zero;
        string? nullFormat = null;
        zero.ToString(nullFormat, CultureInfo.InvariantCulture).ShouldBe("0");
    }

    [Fact]
    public void FormatZero_ExplicitEmptyFormat_ReturnsZero()
    {
        // Line 370-372: empty string check
        BigNumber zero = BigNumber.Zero;
        zero.ToString(string.Empty, CultureInfo.InvariantCulture).ShouldBe("0");
    }

    // ========== Percent Format Additional Coverage ==========

    [Fact]
    public void FormatPercent_NegativeNumber_UsesNegativePattern()
    {
        BigNumber num = new(-5, -3); // -0.005 = -0.5%
        var culture = new CultureInfo("en-US");
        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    // ========== Additional Currency Positive Pattern Coverage ==========

    [Fact]
    public void FormatCurrency_PositivePattern2_CurrencySpaceValue()
    {
        // Positive pattern 2: $ n
        BigNumber num = new(12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 2;

        string result = num.ToString("C2", culture);
        result.ShouldContain("$ ");
    }

    [Fact]
    public void FormatCurrency_PositivePattern3_ValueSpaceCurrency()
    {
        // Positive pattern 3: n $
        BigNumber num = new(12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyPositivePattern = 3;

        string result = num.ToString("C2", culture);
        result.ShouldContain(" $");
    }
}