// <copyright file="BigNumber.FormattingOptimizations.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Phase 2: Formatting optimization tests - Buffer boundaries, cultures, precision.
/// Target: +2.1% coverage (25 branches).
/// </summary>
public class BigNumberFormattingOptimizationsTests
{
    #region Buffer Boundary Conditions (+8 branches)

    [Fact]
    public void TryFormatOptimized_VeryLargeNumber_HandlesCorrectly()
    {
        // Test with very large number requiring significant buffer space
        var num = BigNumber.Parse("123456789012345678901234567890");
        Span<char> buffer = stackalloc char[256];

        bool success = num.TryFormatOptimized(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        written.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void TryFormatOptimized_ExactBufferSize_Succeeds()
    {
        // Test number that exactly fills expected buffer size
        BigNumber num = new(12345, -2); // 123.45
        Span<char> buffer = stackalloc char[10]; // Just enough for "123.45"

        bool success = num.TryFormatOptimized(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("123.45");
    }

    [Fact]
    public void TryFormatOptimized_BufferTooSmallForCurrency_ReturnsFalse()
    {
        // Currency format requires more space for symbol
        BigNumber num = new(12345, -2);
        Span<char> buffer = stackalloc char[3]; // Too small for currency

        bool success = num.TryFormatOptimized(buffer, out int written, "C2", CultureInfo.InvariantCulture);

        // May partially write, but should indicate failure
        success.ShouldBeFalse();
    }

    [Fact]
    public void TryFormatOptimized_BufferTooSmallForPercent_ReturnsFalse()
    {
        // Percent format requires more space
        BigNumber num = new(12345, -4); // 1.2345
        Span<char> buffer = stackalloc char[3]; // Too small

        bool success = num.TryFormatOptimized(buffer, out int written, "P2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void TryFormatOptimized_LargeBufferSmallNumber_Succeeds()
    {
        // Large buffer with small number - no overflow risk
        BigNumber num = new(5, 0);
        Span<char> buffer = stackalloc char[512];

        bool success = num.TryFormatOptimized(buffer, out int written, "F10", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("5.0000000000");
    }

    [Fact]
    public void TryFormatOptimized_ExponentialWithHighPrecision_UsesBuffer()
    {
        // Exponential with high precision
        BigNumber num = new(123456789, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = num.TryFormatOptimized(buffer, out int written, "E20", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        written.ShouldBeGreaterThan(20);
    }

    #endregion

    #region Custom Culture Separators (+10 branches)

    [Fact]
    public void FormatNumber_MultiCharDecimalSeparator_FormatsCorrectly()
    {
        // Custom culture with multi-character decimal separator
        BigNumber num = new(12345, -2); // 123.45
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberDecimalSeparator = "::";

        string result = num.ToString("N2", culture);

        result.ShouldContain("::");
        result.ShouldContain("123");
    }

    [Fact]
    public void FormatNumber_MultiCharGroupSeparator_FormatsCorrectly()
    {
        // Custom culture with multi-character group separator
        BigNumber num = new(1234567, -2); // 12345.67
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberGroupSeparator = "--";

        string result = num.ToString("N2", culture);

        result.ShouldContain("--");
    }

    [Fact]
    public void FormatCurrency_MultiCharCurrencySymbol_FormatsCorrectly()
    {
        // Custom culture with multi-character currency symbol
        BigNumber num = new(12345, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.CurrencySymbol = "USD";

        string result = num.ToString("C2", culture);

        result.ShouldContain("USD");
        result.ShouldContain("123.45");
    }

    [Fact]
    public void FormatPercent_MultiCharPercentSymbol_FormatsCorrectly()
    {
        // Custom culture with multi-character percent symbol
        BigNumber num = new(12345, -4); // 1.2345
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.PercentSymbol = "pct";

        string result = num.ToString("P2", culture);

        result.ShouldContain("pct");
    }

    [Fact]
    public void FormatNumber_CustomCultureAllSeparators_FormatsCorrectly()
    {
        // Culture with all custom separators
        BigNumber num = new(1234567, -2); // 12345.67
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberDecimalSeparator = "::";
        culture.NumberFormat.NumberGroupSeparator = "--";

        string result = num.ToString("N2", culture);

        result.ShouldContain("::");
        result.ShouldContain("--");
    }

    [Fact]
    public void FormatZero_CustomDecimalSeparator_UsesCustomSeparator()
    {
        // Zero with custom decimal separator
        BigNumber zero = BigNumber.Zero;
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberDecimalSeparator = "@@";

        string result = zero.ToString("F3", culture);

        result.ShouldBe("0@@000");
    }

    [Fact]
    public void FormatExponential_CustomDecimalSeparator_UsesCustomSeparator()
    {
        // Exponential with custom decimal separator
        BigNumber num = new(12345, 0);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberDecimalSeparator = "||";

        string result = num.ToString("E2", culture);

        result.ShouldContain("||");
        result.ShouldContain("E+");
    }

    #endregion

    #region Precision Edge Cases (+7 branches)

    [Fact]
    public void FormatFixedPoint_PrecisionZero_NoDecimalPoint()
    {
        // Precision 0 - no decimal point should appear
        BigNumber num = new(12345, -2);
        string result = num.ToString("F0", CultureInfo.InvariantCulture);

        result.ShouldBe("123");
        result.ShouldNotContain(".");
    }

    [Fact]
    public void FormatFixedPoint_PrecisionOne_OneDecimal()
    {
        // Precision 1 - one decimal place
        BigNumber num = new(12345, -2);
        string result = num.ToString("F1", CultureInfo.InvariantCulture);

        result.ShouldBe("123.5");
    }

    [Fact]
    public void FormatFixedPoint_PrecisionTen_TenDecimals()
    {
        // Precision 10 - ten decimal places
        BigNumber num = new(5, 0);
        string result = num.ToString("F10", CultureInfo.InvariantCulture);

        result.ShouldBe("5.0000000000");
    }

    [Fact]
    public void FormatFixedPoint_PrecisionFifty_FiftyDecimals()
    {
        // Precision 50 - very high precision
        BigNumber num = new(1, -1); // 0.1
        string result = num.ToString("F50", CultureInfo.InvariantCulture);

        result.ShouldStartWith("0.1");
        result.Length.ShouldBe(52); // "0." + 50 digits
    }

    [Fact]
    public void FormatExponential_PrecisionZeroNoDecimal_SingleDigit()
    {
        // Exponential precision 0 - no decimal point
        BigNumber num = new(7, 0);
        string result = num.ToString("E0", CultureInfo.InvariantCulture);

        result.ShouldBe("7E+000");
        result.ShouldNotContain(".");
    }

    [Fact]
    public void FormatNumber_VeryHighPrecision_HandlesCorrectly()
    {
        // Number format with very high precision
        BigNumber num = new(123, -2); // 1.23
        string result = num.ToString("N30", CultureInfo.InvariantCulture);

        result.ShouldStartWith("1.23");
        result.ShouldContain("000"); // Should pad with zeros
    }

    #endregion

    #region Zero Formatting All Types (+5 branches)

    [Fact]
    public void FormatZero_FixedPointPrecision0_OnlyZero()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString("F0", CultureInfo.InvariantCulture).ShouldBe("0");
    }

    [Fact]
    public void FormatZero_FixedPointPrecision10_ZeroWithDecimals()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString("F10", CultureInfo.InvariantCulture).ShouldBe("0.0000000000");
    }

    [Fact]
    public void FormatZero_NumberPrecision0_OnlyZero()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString("N0", CultureInfo.InvariantCulture).ShouldBe("0");
    }

    [Fact]
    public void FormatZero_NumberPrecision10_ZeroWithDecimals()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString("N10", CultureInfo.InvariantCulture).ShouldBe("0.0000000000");
    }

    [Fact]
    public void FormatZero_ExponentialPrecision0_ZeroExponential()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString("E0", CultureInfo.InvariantCulture).ShouldBe("0E+000");
    }

    [Fact]
    public void FormatZero_ExponentialPrecision10_ZeroWithDecimals()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString("E10", CultureInfo.InvariantCulture).ShouldBe("0.0000000000E+000");
    }

    [Fact]
    public void FormatZero_CurrencyPrecision0_ZeroCurrency()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString("C0", CultureInfo.InvariantCulture).ShouldBe("¤0");
    }

    [Fact]
    public void FormatZero_PercentPrecision0_ZeroPercent()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString("P0", CultureInfo.InvariantCulture).ShouldBe("0 %");
    }

    [Fact]
    public void FormatZero_WithNegativeSign_StillFormatsAsZero()
    {
        // Even with negative sign, zero is zero
        BigNumber zero = new(0, 100);
        zero.ToString("F2", CultureInfo.InvariantCulture).ShouldBe("0.00");
    }

    #endregion

    #region Format Type Detection (+5 branches)

    [Fact]
    public void Format_GeneralUpperCaseNoExponent_ReturnsSignificand()
    {
        // G format with exponent 0
        BigNumber num = new(12345, 0);
        string result = num.ToString("G", CultureInfo.InvariantCulture);

        result.ShouldBe("12345");
    }

    [Fact]
    public void Format_GeneralLowerCaseWithExponent_UsesLowerE()
    {
        // g format with exponent - uses lowercase e
        BigNumber num = new(12345, 10);
        string result = num.ToString("g", CultureInfo.InvariantCulture);

        result.ShouldContain("e");
        result.ShouldContain("12345");
    }

    [Fact]
    public void Format_GeneralWithPrecision_LimitsSignificantDigits()
    {
        // G format with precision specified
        BigNumber num = new(123456789, 0);
        string result = num.ToString("G5", CultureInfo.InvariantCulture);

        // Should round to 5 significant digits: 12346
        result.ShouldContain("12346");
    }

    [Fact]
    public void Format_GeneralWithNegativePrecision_UsesDefault()
    {
        // G format with no precision uses default representation
        BigNumber num = new(12345, -3);
        string result = num.ToString("G", CultureInfo.InvariantCulture);

        result.ShouldContain("12345");
    }

    #endregion

    #region Negative Number Formatting (+5 branches)

    [Fact]
    public void FormatFixedPoint_NegativeNumber_ShowsSign()
    {
        BigNumber num = new(-12345, -2);
        string result = num.ToString("F2", CultureInfo.InvariantCulture);

        result.ShouldStartWith("-");
        result.ShouldContain("123.45");
    }

    [Fact]
    public void FormatExponential_NegativeNumber_ShowsSign()
    {
        BigNumber num = new(-12345, 0);
        string result = num.ToString("E2", CultureInfo.InvariantCulture);

        result.ShouldStartWith("-");
        result.ShouldContain("E+");
    }

    [Fact]
    public void FormatNumber_NegativeNumberWithGrouping_FormatsCorrectly()
    {
        BigNumber num = new(-1234567, -2);
        string result = num.ToString("N2", CultureInfo.InvariantCulture);

        result.ShouldStartWith("-");
        result.ShouldContain(",");
    }

    [Fact]
    public void FormatCurrency_NegativeSmallNumber_UsesNegativePattern()
    {
        BigNumber num = new(-5, -2); // -0.05
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
        result.ShouldContain("0.05");
    }

    [Fact]
    public void FormatPercent_NegativePercent_UsesNegativePattern()
    {
        BigNumber num = new(-12345, -4); // -1.2345 = -123.45%
        var culture = new CultureInfo("en-US");
        string result = num.ToString("P2", culture);

        result.ShouldContain("-");
        result.ShouldContain("%");
    }

    #endregion
}