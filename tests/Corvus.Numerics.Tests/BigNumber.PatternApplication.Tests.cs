// <copyright file="BigNumber.PatternApplication.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Phase 4: Pattern application tests.
/// Target: +0.7% coverage (8 branches).
/// </summary>
public class BigNumberPatternApplicationTests
{
    #region Percent Negative Patterns (+4 branches)

    [Fact]
    public void FormatPercent_NegativePattern0_MinusSymbolAfter()
    {
        // Pattern 0: -n %
        BigNumber num = new(-12345, -4); // -1.2345
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 0;

        string result = num.ToString("P2", culture);

        result.ShouldStartWith("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern1_MinusSymbolAfterNumber()
    {
        // Pattern 1: -n%
        BigNumber num = new(-5, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 1;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern2_MinusAfterPercent()
    {
        // Pattern 2: -%n
        BigNumber num = new(-25, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 2;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern3_PercentBefore()
    {
        // Pattern 3: %-n
        BigNumber num = new(-15, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 3;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern4_PercentAfterMinus()
    {
        // Pattern 4: %n-
        BigNumber num = new(-35, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 4;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern5_MinusAfterNumber()
    {
        // Pattern 5: n-%
        BigNumber num = new(-45, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 5;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern6_NumberPercentMinus()
    {
        // Pattern 6: n%-
        BigNumber num = new(-55, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 6;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern7_MinusPercent()
    {
        // Pattern 7: -% n
        BigNumber num = new(-65, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 7;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern8_NumberMinusPercent()
    {
        // Pattern 8: n -%
        BigNumber num = new(-75, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 8;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern9_PercentMinusNumber()
    {
        // Pattern 9: % n-
        BigNumber num = new(-85, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 9;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern10_PercentNumberMinus()
    {
        // Pattern 10: % -n
        BigNumber num = new(-95, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 10;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    [Fact]
    public void FormatPercent_NegativePattern11_NumberPercentSpace()
    {
        // Pattern 11: n- %
        BigNumber num = new(-105, -3);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentNegativePattern = 11;

        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    #endregion

    #region Symbol Variations (+4 branches)

    [Fact]
    public void FormatCurrency_LongSymbol_FormatsCorrectly()
    {
        // Multi-character currency symbol
        BigNumber num = new(12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencySymbol = "USD";

        string result = num.ToString("C2", culture);

        result.ShouldContain("USD");
        result.ShouldContain("123.45");
    }

    [Fact]
    public void FormatPercent_CustomSymbol_FormatsCorrectly()
    {
        // Custom percent symbol
        BigNumber num = new(12345, -4);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentSymbol = "percent";

        string result = num.ToString("P2", culture);

        result.ShouldContain("percent");
    }

    [Fact]
    public void FormatCurrency_EmojiSymbol_FormatsCorrectly()
    {
        // Unicode/emoji symbol
        BigNumber num = new(12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencySymbol = "💵";

        string result = num.ToString("C2", culture);

        result.ShouldContain("💵");
    }

    [Fact]
    public void FormatNumber_CustomNegativeSign_FormatsCorrectly()
    {
        // Custom negative sign
        BigNumber num = new(-12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NegativeSign = "minus";

        string result = num.ToString("N2", culture);

        result.ShouldContain("minus");
        result.ShouldContain("123.45");
    }

    [Fact]
    public void FormatCurrency_PositivePatternAllVariants_Work()
    {
        // Test that positive patterns 0-3 all work
        BigNumber num = new(12345, -2);
        var culture = new CultureInfo("en-US");

        for (int pattern = 0; pattern <= 3; pattern++)
        {
            culture.NumberFormat.CurrencyPositivePattern = pattern;
            string result = num.ToString("C2", culture);
            result.ShouldContain("$");
            result.ShouldContain("123.45");
        }
    }

    [Fact]
    public void FormatPercent_PositivePatternAllVariants_Work()
    {
        // Test that positive patterns 0-3 all work
        BigNumber num = new(12345, -4);
        var culture = new CultureInfo("en-US");

        for (int pattern = 0; pattern <= 3; pattern++)
        {
            culture.NumberFormat.PercentPositivePattern = pattern;
            string result = num.ToString("P2", culture);
            result.ShouldContain("%");
        }
    }

    #endregion
}