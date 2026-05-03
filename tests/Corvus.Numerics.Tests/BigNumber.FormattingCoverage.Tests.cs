// <copyright file="BigNumber.FormattingCoverage.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Targeted coverage tests for BigNumber formatting paths:
/// currency (C), percent (P), number (N), and rounding modes.
/// </summary>
public class BigNumberFormattingCoverageTests
{
    #region Currency Formatting (FormatCurrency)

    [Fact]
    public void ToString_C_PositiveValue_DefaultCulture()
    {
        BigNumber value = new(12345, -2); // 123.45
        string result = value.ToString("C2", CultureInfo.InvariantCulture);
        // InvariantCulture uses CurrencyPositivePattern=0: $n
        result.ShouldBe("¤123.45");
    }

    [Fact]
    public void ToString_C_NegativeValue_DefaultCulture()
    {
        BigNumber value = new(-12345, -2); // -123.45
        string result = value.ToString("C2", CultureInfo.InvariantCulture);
        // InvariantCulture uses CurrencyNegativePattern=0: ($n)
        result.ShouldBe("(¤123.45)");
    }

    [Theory]
    [InlineData(0, "($123.45)")]
    [InlineData(1, "-$123.45")]
    [InlineData(2, "$-123.45")]
    [InlineData(3, "$123.45-")]
    [InlineData(4, "(123.45$)")]
    [InlineData(5, "-123.45$")]
    [InlineData(6, "123.45-$")]
    [InlineData(7, "123.45$-")]
    [InlineData(8, "-123.45 $")]
    [InlineData(9, "-$ 123.45")]
    [InlineData(10, "123.45 $-")]
    [InlineData(11, "$ 123.45-")]
    [InlineData(12, "$ -123.45")]
    [InlineData(13, "123.45- $")]
    [InlineData(14, "($ 123.45)")]
    [InlineData(15, "(123.45 $)")]
    public void ToString_C_NegativeValue_AllPatterns(int pattern, string expected)
    {
        BigNumber value = new(-12345, -2); // -123.45
        var nfi = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyGroupSizes = [3],
            CurrencyNegativePattern = pattern,
            NegativeSign = "-",
        };
        string result = value.ToString("C2", nfi);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, "$123.45")]
    [InlineData(1, "123.45$")]
    [InlineData(2, "$ 123.45")]
    [InlineData(3, "123.45 $")]
    public void ToString_C_PositiveValue_AllPatterns(int pattern, string expected)
    {
        BigNumber value = new(12345, -2); // 123.45
        var nfi = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyGroupSizes = [3],
            CurrencyPositivePattern = pattern,
        };
        string result = value.ToString("C2", nfi);
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToString_C_Zero()
    {
        BigNumber value = BigNumber.Zero;
        var nfi = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyGroupSizes = [3],
            CurrencyPositivePattern = 0,
            CurrencyDecimalDigits = 2,
        };
        string result = value.ToString("C", nfi);
        result.ShouldBe("$0.00");
    }

    #endregion

    #region Percent Formatting (FormatPercent)

    [Theory]
    [InlineData(0, "-50.00 %")]
    [InlineData(1, "-50.00%")]
    [InlineData(2, "-%50.00")]
    [InlineData(3, "%50.00-")]
    [InlineData(4, "%-50.00")]
    [InlineData(5, "50.00-%")]
    [InlineData(6, "50.00%-")]
    [InlineData(7, "-% 50.00")]
    [InlineData(8, "50.00 %-")]
    [InlineData(9, "% 50.00-")]
    [InlineData(10, "% -50.00")]
    [InlineData(11, "50.00- %")]
    public void ToString_P_NegativeValue_AllPatterns(int pattern, string expected)
    {
        BigNumber value = new(-5, -1); // -0.5 => -50%
        var nfi = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentGroupSizes = [3],
            PercentNegativePattern = pattern,
            PercentDecimalDigits = 2,
            NegativeSign = "-",
        };
        string result = value.ToString("P2", nfi);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, "50.00 %")]
    [InlineData(1, "50.00%")]
    [InlineData(2, "%50.00")]
    [InlineData(3, "% 50.00")]
    public void ToString_P_PositiveValue_AllPatterns(int pattern, string expected)
    {
        BigNumber value = new(5, -1); // 0.5 => 50%
        var nfi = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentGroupSizes = [3],
            PercentPositivePattern = pattern,
            PercentDecimalDigits = 2,
        };
        string result = value.ToString("P2", nfi);
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToString_P_Zero()
    {
        BigNumber value = BigNumber.Zero;
        var nfi = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentGroupSizes = [3],
            PercentPositivePattern = 1,
            PercentDecimalDigits = 2,
        };
        string result = value.ToString("P", nfi);
        result.ShouldBe("0.00%");
    }

    [Fact]
    public void ToString_P_SmallFraction()
    {
        // 0.005 => 0.50% (exercises sigStr.Length <= precision path in FormatPercent)
        BigNumber value = new(5, -3);
        var nfi = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentGroupSizes = [3],
            PercentPositivePattern = 1,
            PercentDecimalDigits = 2,
        };
        string result = value.ToString("P2", nfi);
        result.ShouldBe("0.50%");
    }

    #endregion

    #region Number Formatting (FormatNumber)

    [Fact]
    public void ToString_N_BasicInteger()
    {
        BigNumber value = new(1234567, 0);
        var nfi = new NumberFormatInfo
        {
            NumberDecimalSeparator = ".",
            NumberGroupSeparator = ",",
            NumberGroupSizes = [3],
            NegativeSign = "-",
        };
        string result = value.ToString("N0", nfi);
        result.ShouldBe("1,234,567");
    }

    [Fact]
    public void ToString_N_NegativeValue()
    {
        BigNumber value = new(-1234567, 0);
        var nfi = new NumberFormatInfo
        {
            NumberDecimalSeparator = ".",
            NumberGroupSeparator = ",",
            NumberGroupSizes = [3],
            NegativeSign = "-",
        };
        string result = value.ToString("N0", nfi);
        result.ShouldBe("-1,234,567");
    }

    [Fact]
    public void ToString_N_WithDecimalPlaces()
    {
        BigNumber value = new(123456789, -4); // 12345.6789
        var nfi = new NumberFormatInfo
        {
            NumberDecimalSeparator = ".",
            NumberGroupSeparator = ",",
            NumberGroupSizes = [3],
            NegativeSign = "-",
        };
        string result = value.ToString("N4", nfi);
        result.ShouldBe("12,345.6789");
    }

    [Fact]
    public void ToString_N_SmallFraction()
    {
        // 0.0012 — exercises sigStr.Length <= precision path in FormatNumber
        BigNumber value = new(12, -4);
        var nfi = new NumberFormatInfo
        {
            NumberDecimalSeparator = ".",
            NumberGroupSeparator = ",",
            NumberGroupSizes = [3],
            NegativeSign = "-",
        };
        string result = value.ToString("N4", nfi);
        result.ShouldBe("0.0012");
    }

    #endregion

    #region FormatZero Variants

    [Theory]
    [InlineData("F2", "0.00")]
    [InlineData("N2", "0.00")]
    [InlineData("E2", "0.00E+000")]
    [InlineData("e2", "0.00e+000")]
    [InlineData("G", "0")]
    [InlineData("G5", "0")]
    [InlineData("F0", "0")]
    [InlineData("N0", "0")]
    [InlineData("E0", "0E+000")]
    public void ToString_Zero_AllFormats(string format, string expected)
    {
        BigNumber value = BigNumber.Zero;
        string result = value.ToString(format, CultureInfo.InvariantCulture);
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToString_Zero_InvalidFormatPrecision_ReturnsZero()
    {
        // Invalid precision string like "Fxyz" on zero value returns "0"
        BigNumber value = BigNumber.Zero;
        string result = value.ToString("Fxyz", CultureInfo.InvariantCulture);
        result.ShouldBe("0");
    }

    [Fact]
    public void ToString_Zero_PercentFormat()
    {
        BigNumber value = BigNumber.Zero;
        var nfi = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentPositivePattern = 1,
            PercentDecimalDigits = 2,
        };
        string result = value.ToString("P", nfi);
        result.ShouldBe("0.00%");
    }

    [Fact]
    public void ToString_Zero_CurrencyFormat()
    {
        BigNumber value = BigNumber.Zero;
        var nfi = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0,
            CurrencyDecimalDigits = 2,
        };
        string result = value.ToString("C", nfi);
        result.ShouldBe("$0.00");
    }

    #endregion

    #region Rounding Modes

#if NET
    [Fact]
    public void Round_ToZero_Positive()
    {
        // 1.7 rounded to 0dp ToZero => 1
        BigNumber value = new(17, -1);
        BigNumber result = BigNumber.Round(value, 0, MidpointRounding.ToZero);
        result.ShouldBe(new BigNumber(1, 0));
    }

    [Fact]
    public void Round_ToZero_Negative()
    {
        // -1.7 rounded to 0dp ToZero => -1
        BigNumber value = new(-17, -1);
        BigNumber result = BigNumber.Round(value, 0, MidpointRounding.ToZero);
        result.ShouldBe(new BigNumber(-1, 0));
    }

    [Fact]
    public void Round_ToNegativeInfinity_Positive()
    {
        // 1.7 rounded to 0dp ToNegativeInfinity => 1
        BigNumber value = new(17, -1);
        BigNumber result = BigNumber.Round(value, 0, MidpointRounding.ToNegativeInfinity);
        result.ShouldBe(new BigNumber(1, 0));
    }

    [Fact]
    public void Round_ToNegativeInfinity_Negative()
    {
        // -1.3 rounded to 0dp ToNegativeInfinity => -2
        BigNumber value = new(-13, -1);
        BigNumber result = BigNumber.Round(value, 0, MidpointRounding.ToNegativeInfinity);
        result.ShouldBe(new BigNumber(-2, 0));
    }

    [Fact]
    public void Round_ToPositiveInfinity_Positive()
    {
        // 1.3 rounded to 0dp ToPositiveInfinity => 2
        BigNumber value = new(13, -1);
        BigNumber result = BigNumber.Round(value, 0, MidpointRounding.ToPositiveInfinity);
        result.ShouldBe(new BigNumber(2, 0));
    }

    [Fact]
    public void Round_ToPositiveInfinity_Negative()
    {
        // -1.7 rounded to 0dp ToPositiveInfinity => -1
        BigNumber value = new(-17, -1);
        BigNumber result = BigNumber.Round(value, 0, MidpointRounding.ToPositiveInfinity);
        result.ShouldBe(new BigNumber(-1, 0));
    }
#endif

    #endregion

    #region TryParse with NumberStyles

    [Fact]
    public void TryParse_NoLeadingWhiteAllowed_RejectsLeadingWhite()
    {
        // NumberStyles.None doesn't allow leading whitespace
        bool success = BigNumber.TryParse(" 123", NumberStyles.None, CultureInfo.InvariantCulture, out _);
        success.ShouldBeFalse();
    }

    [Fact]
    public void TryParse_NoTrailingWhiteAllowed_RejectsTrailingWhite()
    {
        // NumberStyles.None doesn't allow trailing whitespace
        bool success = BigNumber.TryParse("123 ", NumberStyles.None, CultureInfo.InvariantCulture, out _);
        success.ShouldBeFalse();
    }

    [Fact]
    public void TryParse_AllowParentheses_ParsesNegative()
    {
        bool success = BigNumber.TryParse(
            "(123)",
            NumberStyles.AllowParentheses | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out BigNumber result);
        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(-123, 0));
    }

    [Fact]
    public void TryParse_AllowCurrencySymbol_LeadingCurrency()
    {
        var nfi = new NumberFormatInfo { CurrencySymbol = "$" };
        bool success = BigNumber.TryParse(
            "$123",
            NumberStyles.AllowCurrencySymbol | NumberStyles.AllowDecimalPoint,
            nfi,
            out BigNumber result);
        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(123, 0));
    }

    [Fact]
    public void TryParse_AllowCurrencySymbol_TrailingCurrency()
    {
        var nfi = new NumberFormatInfo { CurrencySymbol = "€" };
        bool success = BigNumber.TryParse(
            "123€",
            NumberStyles.AllowCurrencySymbol | NumberStyles.AllowDecimalPoint,
            nfi,
            out BigNumber result);
        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(123, 0));
    }

    [Fact]
    public void TryParse_AllowCurrencySymbol_LeadingCurrencyWithSpace()
    {
        var nfi = new NumberFormatInfo { CurrencySymbol = "$" };
        bool success = BigNumber.TryParse(
            "$ 123",
            NumberStyles.AllowCurrencySymbol | NumberStyles.AllowDecimalPoint,
            nfi,
            out BigNumber result);
        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(123, 0));
    }

    [Fact]
    public void TryParse_AllowCurrencySymbol_TrailingCurrencyWithSpace()
    {
        var nfi = new NumberFormatInfo { CurrencySymbol = "€" };
        bool success = BigNumber.TryParse(
            "123 €",
            NumberStyles.AllowCurrencySymbol | NumberStyles.AllowDecimalPoint,
            nfi,
            out BigNumber result);
        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(123, 0));
    }

    [Fact]
    public void TryParse_AllowLeadingSign_RejectsWhiteAfterSign()
    {
        bool success = BigNumber.TryParse(
            "+ 123",
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out _);
        success.ShouldBeFalse();
    }

    [Fact]
    public void TryParse_AllowLeadingSign_RejectsWhiteAfterNegativeSign()
    {
        bool success = BigNumber.TryParse(
            "- 123",
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out _);
        success.ShouldBeFalse();
    }

    [Fact]
    public void TryParse_AllowTrailingSign_ParsesTrailingPlus()
    {
        bool success = BigNumber.TryParse(
            "123+",
            NumberStyles.AllowTrailingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out BigNumber result);
        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(123, 0));
    }

    [Fact]
    public void TryParse_AllowTrailingSign_ParsesTrailingMinus()
    {
        bool success = BigNumber.TryParse(
            "123-",
            NumberStyles.AllowTrailingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out BigNumber result);
        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(-123, 0));
    }

    [Fact]
    public void TryParse_NoSignAllowed_RejectsLeadingSign()
    {
        // NumberStyles.None with decimal but not sign
        bool success = BigNumber.TryParse(
            "+123",
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out _);
        success.ShouldBeFalse();
    }

    [Fact]
    public void TryParse_NoSignAllowed_RejectsTrailingSign()
    {
        bool success = BigNumber.TryParse(
            "123-",
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out _);
        success.ShouldBeFalse();
    }

    #endregion

    #region CompareTo(object)

    [Fact]
    public void CompareTo_Null_ReturnsPositive()
    {
        BigNumber value = new(1, 0);
        int result = value.CompareTo(null);
        result.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_WrongType_ThrowsArgumentException()
    {
        BigNumber value = new(1, 0);
        Should.Throw<ArgumentException>(() => value.CompareTo("not a BigNumber"));
    }

    [Fact]
    public void CompareTo_BoxedBigNumber_Works()
    {
        BigNumber value = new(2, 0);
        object other = new BigNumber(1, 0);
        int result = value.CompareTo(other);
        result.ShouldBeGreaterThan(0);
    }

    #endregion

    #region FormatGeneral (G format)

    [Fact]
    public void ToString_G_WithPrecision_RoundsSignificand()
    {
        // 123456 with G3 => rounds to 3 significant digits => 123000 => "123E3" or "1.23E5"
        BigNumber value = new(123456, 0);
        string result = value.ToString("G3", CultureInfo.InvariantCulture);
        // FormatGeneral with precision=3 rounds to 3 sig digits
        result.ShouldBe("123E3");
    }

    [Fact]
    public void ToString_g_LowercaseExponent()
    {
        BigNumber value = new(123456, 0);
        string result = value.ToString("g3", CultureInfo.InvariantCulture);
        result.ShouldBe("123e3");
    }

    [Fact]
    public void ToString_G_NoPrecision()
    {
        BigNumber value = new(123, 5);
        string result = value.ToString("G", CultureInfo.InvariantCulture);
        result.ShouldBe("123E5");
    }

    [Fact]
    public void ToString_G_ZeroExponent()
    {
        BigNumber value = new(42, 0);
        string result = value.ToString("G", CultureInfo.InvariantCulture);
        result.ShouldBe("42");
    }

    #endregion

    #region FormatFixedPoint (F format)

    [Fact]
    public void ToString_F_BasicDecimal()
    {
        BigNumber value = new(12345, -2); // 123.45
        string result = value.ToString("F2", CultureInfo.InvariantCulture);
        result.ShouldBe("123.45");
    }

    [Fact]
    public void ToString_F_SmallFraction()
    {
        // 0.0012 — exercises sigStr.Length <= precision path
        BigNumber value = new(12, -4);
        string result = value.ToString("F4", CultureInfo.InvariantCulture);
        result.ShouldBe("0.0012");
    }

    [Fact]
    public void ToString_F_NegativeValue()
    {
        BigNumber value = new(-123, -1); // -12.3
        string result = value.ToString("F1", CultureInfo.InvariantCulture);
        result.ShouldBe("-12.3");
    }

    #endregion

    #region FormatExponential (E format)

    [Fact]
    public void ToString_E_BasicValue()
    {
        BigNumber value = new(12345, 0); // 12345
        string result = value.ToString("E2", CultureInfo.InvariantCulture);
        result.ShouldBe("1.23E+004");
    }

    [Fact]
    public void ToString_e_LowercaseExponent()
    {
        BigNumber value = new(12345, 0);
        string result = value.ToString("e2", CultureInfo.InvariantCulture);
        result.ShouldBe("1.23e+004");
    }

    [Fact]
    public void ToString_E_NegativeExponent()
    {
        BigNumber value = new(5, -5); // 0.00005
        string result = value.ToString("E2", CultureInfo.InvariantCulture);
        result.ShouldBe("5.00E-005");
    }

    [Fact]
    public void ToString_E_NegativeValue()
    {
        BigNumber value = new(-12345, 0);
        string result = value.ToString("E2", CultureInfo.InvariantCulture);
        result.ShouldBe("-1.23E+004");
    }

    [Fact]
    public void ToString_E_ZeroPrecision()
    {
        BigNumber value = new(12345, 0);
        string result = value.ToString("E0", CultureInfo.InvariantCulture);
        result.ShouldBe("1E+004");
    }

    [Fact]
    public void ToString_E_RoundingOverflow()
    {
        // 9999 with E0 => rounds 9.999 to 10 => overflow => 1E+004
        BigNumber value = new(9999, 0);
        string result = value.ToString("E0", CultureInfo.InvariantCulture);
        result.ShouldBe("1E+004");
    }

    [Fact]
    public void ToString_E_SingleDigitSignificand()
    {
        // A value with only 1 digit significand padded with zeros
        BigNumber value = new(5, 3); // 5000
        string result = value.ToString("E3", CultureInfo.InvariantCulture);
        result.ShouldBe("5.000E+003");
    }

    #endregion

    #region Invalid Format

    [Fact]
    public void ToString_InvalidFormatString_Throws()
    {
        BigNumber value = new(123, 0);
        Should.Throw<FormatException>(() => value.ToString("Cxyz", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ToString_UnsupportedFormatSpecifier_Throws()
    {
        BigNumber value = new(123, 0);
        Should.Throw<FormatException>(() => value.ToString("X", CultureInfo.InvariantCulture));
    }

    #endregion

    #region Large Exponent Caching

    [Fact]
    public void ToString_LargeExponent_SecondaryCache()
    {
        // Force use of the secondary cache (exponent 256-1023)
        BigNumber value = new(1, 300);
        string result = value.ToString();
        // 1E300 normalized
        result.ShouldBe("1E300");
    }

    [Fact]
    public void ToString_VeryLargeExponent_ComputedOnDemand()
    {
        // Force use of the computed-on-demand path (exponent >= 1024)
        BigNumber value = new(1, 1100);
        string result = value.ToString();
        result.ShouldBe("1E1100");
    }

    [Fact]
    public void FormatFixedPoint_LargeScale_UsesSecondaryCache()
    {
        // F300 format forces GetPowerOf10(300) which uses the secondary cache
        BigNumber value = new(123, -298); // Very small number
        string result = value.ToString("F300", CultureInfo.InvariantCulture);
        // Should have 300 decimal digits
        result.ShouldContain(".");
        result.Split('.')[1].Length.ShouldBe(300);
    }

    #endregion

    #region AddThousandsSeparators

    [Fact]
    public void ToString_N_LargeNumber_MultipleGroups()
    {
        // 1234567890 with custom group sizes [3, 2]
        BigNumber value = new(1234567890, 0);
        var nfi = new NumberFormatInfo
        {
            NumberDecimalSeparator = ".",
            NumberGroupSeparator = ",",
            NumberGroupSizes = [3, 2],
            NegativeSign = "-",
        };
        string result = value.ToString("N0", nfi);
        // 1,23,45,67,890 — first group is 3, then groups of 2
        result.ShouldBe("1,23,45,67,890");
    }

    [Fact]
    public void ToString_N_SmallNumber_NoSeparator()
    {
        // 123 — too small for thousands separator
        BigNumber value = new(123, 0);
        var nfi = new NumberFormatInfo
        {
            NumberDecimalSeparator = ".",
            NumberGroupSeparator = ",",
            NumberGroupSizes = [3],
            NegativeSign = "-",
        };
        string result = value.ToString("N0", nfi);
        result.ShouldBe("123");
    }

    #endregion

    #region Pow

    [Fact]
    public void Pow_NegativeExponent_Throws()
    {
        BigNumber value = new(2, 0);
        Should.Throw<ArgumentOutOfRangeException>(() => BigNumber.Pow(value, -1));
    }

    [Fact]
    public void Pow_ZeroExponent_ReturnsOne()
    {
        BigNumber value = new(5, 0);
        BigNumber result = BigNumber.Pow(value, 0);
        result.ShouldBe(BigNumber.One);
    }

    [Fact]
    public void Pow_OneExponent_ReturnsSameValue()
    {
        BigNumber value = new(5, 2);
        BigNumber result = BigNumber.Pow(value, 1);
        result.ShouldBe(value);
    }

    [Fact]
    public void Pow_ZeroBase_ReturnsZero()
    {
        BigNumber result = BigNumber.Pow(BigNumber.Zero, 5);
        result.ShouldBe(BigNumber.Zero);
    }

    #endregion

    #region Culture-Specific FormatZero Patterns

    [Fact]
    public void ToString_Zero_C_PercentPositivePattern0()
    {
        // PercentPositivePattern 0 => "n %"
        var nfi = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentPositivePattern = 0, // n %
        };
        BigNumber value = BigNumber.Zero;
        string result = value.ToString("P2", nfi);
        result.ShouldBe("0.00 %");
    }

    [Fact]
    public void ToString_Zero_P_PercentPositivePattern2()
    {
        // PercentPositivePattern 2 => "%n"
        var nfi = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentPositivePattern = 2, // %n
        };
        BigNumber value = BigNumber.Zero;
        string result = value.ToString("P2", nfi);
        result.ShouldBe("%0.00");
    }

    [Fact]
    public void ToString_Zero_P_PercentPositivePattern3()
    {
        // PercentPositivePattern 3 => "% n"
        var nfi = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentDecimalSeparator = ".",
            PercentPositivePattern = 3, // % n
        };
        BigNumber value = BigNumber.Zero;
        string result = value.ToString("P2", nfi);
        result.ShouldBe("% 0.00");
    }

    [Fact]
    public void ToString_Zero_C_CurrencyPositivePattern1()
    {
        // CurrencyPositivePattern 1 => "n$"
        var nfi = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 1, // n$
        };
        BigNumber value = BigNumber.Zero;
        string result = value.ToString("C2", nfi);
        result.ShouldBe("0.00$");
    }

    [Fact]
    public void ToString_Zero_C_CurrencyPositivePattern2()
    {
        // CurrencyPositivePattern 2 => "$ n"
        var nfi = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 2, // $ n
        };
        BigNumber value = BigNumber.Zero;
        string result = value.ToString("C2", nfi);
        result.ShouldBe("$ 0.00");
    }

    [Fact]
    public void ToString_Zero_C_CurrencyPositivePattern3()
    {
        // CurrencyPositivePattern 3 => "n $"
        var nfi = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 3, // n $
        };
        BigNumber value = BigNumber.Zero;
        string result = value.ToString("C2", nfi);
        result.ShouldBe("0.00 $");
    }

    #endregion

    #region FormatGeneral Rounding

    [Fact]
    public void ToString_G_RoundsUpAtMidpoint()
    {
        // 125500 with G3 → 125500/1000=125 rem 500, halfDivisor=500
        // rem==half && quotient(125) is odd → round up → 126E3
        BigNumber value = new(125500, 0);
        string result = value.ToString("G3", CultureInfo.InvariantCulture);
        result.ShouldBe("126E3");
    }

    [Fact]
    public void ToString_G_RoundsDownAtMidpointEvenQuotient()
    {
        // 124500 with G3 → 124500/1000=124 rem 500, halfDivisor=500
        // rem==half && quotient(124) is even → don't round → 124E3
        BigNumber value = new(124500, 0);
        string result = value.ToString("G3", CultureInfo.InvariantCulture);
        result.ShouldBe("124E3");
    }

    [Fact]
    public void ToString_G_RoundsUpAboveMidpoint()
    {
        // 123600 with G3 → 123600/1000=123 rem 600, halfDivisor=500
        // rem(600) > half(500) → round up → 124E3
        BigNumber value = new(123600, 0);
        string result = value.ToString("G3", CultureInfo.InvariantCulture);
        result.ShouldBe("124E3");
    }

    #endregion

    #region FormatExponential Rounding Edge Cases

    [Fact]
    public void ToString_E_HighPrecisionPadding()
    {
        // Value with fewer digits than precision — needs zero padding
        BigNumber value = new(5, 0); // Just "5"
        string result = value.ToString("E5", CultureInfo.InvariantCulture);
        result.ShouldBe("5.00000E+000");
    }

    [Fact]
    public void ToString_E_NeedsRounding()
    {
        // 123456 with E2 → mantissa 1.23456 → rounds to 1.23
        BigNumber value = new(123456, 0);
        string result = value.ToString("E2", CultureInfo.InvariantCulture);
        result.ShouldBe("1.23E+005");
    }

    [Fact]
    public void ToString_E_RoundingCausesOverflow()
    {
        // 9995 with E2: mantissa 9.995, rounds to 10.0, overflow → 1.00E+004
        BigNumber value = new(9995, 0);
        string result = value.ToString("E2", CultureInfo.InvariantCulture);
        result.ShouldBe("1.00E+004");
    }

    #endregion

    #region FormatFixedPoint Edge Cases

    [Fact]
    public void ToString_F_ZeroPrecision()
    {
        BigNumber value = new(12345, -2); // 123.45
        string result = value.ToString("F0", CultureInfo.InvariantCulture);
        result.ShouldBe("123");
    }

    [Fact]
    public void ToString_F_VerySmallValue()
    {
        // sigStr.Length < precision path with zeros prepended
        BigNumber value = new(1, -6); // 0.000001
        string result = value.ToString("F6", CultureInfo.InvariantCulture);
        result.ShouldBe("0.000001");
    }

    #endregion

    #region FormatZero Edge Cases

    [Fact]
    public void ToString_Zero_NullFormat()
    {
        BigNumber value = BigNumber.Zero;
        string result = value.ToString(null, CultureInfo.InvariantCulture);
        result.ShouldBe("0");
    }

    [Fact]
    public void ToString_Zero_EmptyFormat()
    {
        BigNumber value = BigNumber.Zero;
        string result = value.ToString("", CultureInfo.InvariantCulture);
        result.ShouldBe("0");
    }

    [Fact]
    public void ToString_Zero_UnsupportedFormat()
    {
        // Unsupported format with zero goes to _ => "0" arm
        BigNumber value = BigNumber.Zero;
        string result = value.ToString("X", CultureInfo.InvariantCulture);
        result.ShouldBe("0");
    }

    #endregion
}
