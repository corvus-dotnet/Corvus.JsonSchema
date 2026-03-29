// <copyright file="BigNumber.GroupingAndSeparators.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Phase 3: Grouping and separator logic tests.
/// Target: +1.2% coverage (15 branches).
/// </summary>
public class BigNumberGroupingAndSeparatorsTests
{
    #region Various Group Sizes (+8 branches)

    [Fact]
    public void FormatNumber_StandardGrouping_ThreeDigits()
    {
        // Standard Western grouping [3]
        BigNumber num = new(1234567890, -2); // 12345678.90
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberGroupSizes = new int[] { 3 };

        string result = num.ToString("N2", culture);

        result.ShouldContain("12,345,678.90");
    }

    [Fact]
    public void FormatNumber_IndianGrouping_ThreeTwoPattern()
    {
        // Indian grouping [3, 2]
        BigNumber num = new(123456789, -2); // 1234567.89
        var culture = new CultureInfo("en-IN");
        culture.NumberFormat.NumberGroupSizes = new int[] { 3, 2 };
        culture.NumberFormat.NumberGroupSeparator = ",";

        string result = num.ToString("N2", culture);

        // Should format as 12,34,567.89
        result.ShouldContain("12,34,567");
    }

    [Fact]
    public void FormatNumber_EastAsianGrouping_FourDigits()
    {
        // East Asian grouping [4]
        BigNumber num = new(123456789, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberGroupSizes = new int[] { 4 };

        string result = num.ToString("N2", culture);

        // Should format as 123,4567.89
        result.ShouldContain("123,4567");
    }

    [Fact]
    public void FormatNumber_NoGrouping_NoSeparators()
    {
        // No grouping []
        BigNumber num = new(1234567, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberGroupSizes = new int[] { 0 };

        string result = num.ToString("N2", culture);

        // Should not have group separators
        result.ShouldNotContain(",");
    }

    [Fact]
    public void FormatNumber_ComplexGrouping_MultiplePatterns()
    {
        // Complex grouping [3, 2, 1]
        var num = BigNumber.Parse("1234567890");
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberGroupSizes = new int[] { 3, 2, 1 };

        string result = num.ToString("N0", culture);

        result.ShouldContain(",");
    }

    [Fact]
    public void FormatCurrency_WithCustomGrouping_AppliesGrouping()
    {
        // Currency with custom grouping
        BigNumber num = new(1234567, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyGroupSizes = new int[] { 2, 3 };

        string result = num.ToString("C2", culture);

        result.ShouldContain("$");
    }

    #endregion

    #region Multi-Character Separators (+4 branches)

    [Fact]
    public void FormatNumber_DoubleCharDecimalSeparator_Works()
    {
        BigNumber num = new(12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberDecimalSeparator = "::";

        string result = num.ToString("N2", culture);

        result.ShouldContain("123::45");
    }

    [Fact]
    public void FormatNumber_DoubleCharGroupSeparator_Works()
    {
        BigNumber num = new(1234567, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberGroupSeparator = ",,";

        string result = num.ToString("N2", culture);

        result.ShouldContain(",,");
    }

    [Fact]
    public void FormatCurrency_MultiCharSeparators_FormatsCorrectly()
    {
        BigNumber num = new(1234567, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencyDecimalSeparator = "::";
        culture.NumberFormat.CurrencyGroupSeparator = "--";

        string result = num.ToString("C2", culture);

        result.ShouldContain("::");
        result.ShouldContain("--");
    }

    #endregion

    #region Integer/Fractional Part Splitting (+3 branches)

    [Fact]
    public void FormatNumber_OnlyIntegerPart_NoFractional()
    {
        // Precision 0 - only integer part
        BigNumber num = new(12345, 0);
        string result = num.ToString("N0", CultureInfo.InvariantCulture);

        result.ShouldBe("12,345");
        result.ShouldNotContain(".");
    }

    [Fact]
    public void FormatNumber_OnlyFractionalPart_IntegerIsZero()
    {
        // Value < 1 - integer part is 0
        BigNumber num = new(5, -3); // 0.005
        string result = num.ToString("N3", CultureInfo.InvariantCulture);

        result.ShouldStartWith("0.");
        result.ShouldContain("0.005");
    }

    [Fact]
    public void FormatNumber_BothParts_SplitsCorrectly()
    {
        // Both integer and fractional parts
        BigNumber num = new(123456, -3); // 123.456
        string result = num.ToString("N3", CultureInfo.InvariantCulture);

        result.ShouldBe("123.456");
    }

    [Fact]
    public void FormatNumber_LargeIntegerSmallFraction_SplitsCorrectly()
    {
        // Large integer, small fractional part
        BigNumber num = new(12345678901, -2); // 123456789.01
        string result = num.ToString("N2", CultureInfo.InvariantCulture);

        result.ShouldContain("123,456,789.01");
    }

    [Fact]
    public void FormatNumber_SmallIntegerLargeFraction_SplitsCorrectly()
    {
        // Small integer, large fractional part
        BigNumber num = new(12345678, -6); // 12.345678
        string result = num.ToString("N6", CultureInfo.InvariantCulture);

        result.ShouldBe("12.345678");
    }

    #endregion
}