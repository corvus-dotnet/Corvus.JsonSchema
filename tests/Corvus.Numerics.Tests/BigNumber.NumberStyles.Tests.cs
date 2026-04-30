
using System.Globalization;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;
/// <summary>
/// Tests for BigNumber parsing with different NumberStyles.
/// </summary>
public class BigNumberNumberStylesTests
{
    #region NumberStyles.None

    [Fact]
    public void Parse_NumberStyles_None_IntegerOnly()
    {
        string input = "12345";

        var result = BigNumber.Parse(input, NumberStyles.None, null);

        result.Significand.ShouldBe(12345);
        result.Exponent.ShouldBe(0);
    }

    [Fact]
    public void Parse_NumberStyles_None_RejectsLeadingWhitespace()
    {
        string input = " 12345";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.None, null));
    }

    [Fact]
    public void Parse_NumberStyles_None_RejectsTrailingWhitespace()
    {
        string input = "12345 ";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.None, null));
    }

    [Fact]
    public void Parse_NumberStyles_None_RejectsSign()
    {
        string input = "+12345";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.None, null));
    }

    [Fact]
    public void Parse_NumberStyles_None_RejectsNegativeSign()
    {
        string input = "-12345";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.None, null));
    }

    #endregion

    #region NumberStyles.Integer

    [Fact]
    public void Parse_NumberStyles_Integer_AllowsLeadingWhitespace()
    {
        string input = "  12345";

        var result = BigNumber.Parse(input, NumberStyles.Integer, null);

        result.Significand.ShouldBe(12345);
    }

    [Fact]
    public void Parse_NumberStyles_Integer_AllowsTrailingWhitespace()
    {
        string input = "12345  ";

        var result = BigNumber.Parse(input, NumberStyles.Integer, null);

        result.Significand.ShouldBe(12345);
    }

    [Fact]
    public void Parse_NumberStyles_Integer_AllowsLeadingSign()
    {
        string input = "+12345";

        var result = BigNumber.Parse(input, NumberStyles.Integer, null);

        result.Significand.ShouldBe(12345);
    }

    [Fact]
    public void Parse_NumberStyles_Integer_AllowsNegativeSign()
    {
        string input = "-12345";

        var result = BigNumber.Parse(input, NumberStyles.Integer, null);

        result.Significand.ShouldBe(-12345);
    }

    [Fact]
    public void Parse_NumberStyles_Integer_RejectsDecimalPoint()
    {
        string input = "123.45";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.Integer, null));
    }

    #endregion

    #region NumberStyles.AllowLeadingWhite

    [Fact]
    public void Parse_NumberStyles_AllowLeadingWhite_AcceptsLeadingSpaces()
    {
        string input = "   12345";

        var result = BigNumber.Parse(input, NumberStyles.AllowLeadingWhite, null);

        result.Significand.ShouldBe(12345);
    }

    [Fact]
    public void Parse_NumberStyles_AllowLeadingWhite_AcceptsLeadingTabs()
    {
        string input = "\t\t12345";

        var result = BigNumber.Parse(input, NumberStyles.AllowLeadingWhite, null);

        result.Significand.ShouldBe(12345);
    }

    #endregion

    #region NumberStyles.AllowTrailingWhite

    [Fact]
    public void Parse_NumberStyles_AllowTrailingWhite_AcceptsTrailingSpaces()
    {
        string input = "12345   ";

        var result = BigNumber.Parse(input, NumberStyles.AllowTrailingWhite, null);

        result.Significand.ShouldBe(12345);
    }

    [Fact]
    public void Parse_NumberStyles_AllowTrailingWhite_AcceptsTrailingTabs()
    {
        string input = "12345\t\t";

        var result = BigNumber.Parse(input, NumberStyles.AllowTrailingWhite, null);

        result.Significand.ShouldBe(12345);
    }

    #endregion

    #region NumberStyles.AllowLeadingSign

    [Fact]
    public void Parse_NumberStyles_AllowLeadingSign_AcceptsPlus()
    {
        string input = "+12345";

        var result = BigNumber.Parse(input, NumberStyles.AllowLeadingSign, null);

        result.Significand.ShouldBe(12345);
    }

    [Fact]
    public void Parse_NumberStyles_AllowLeadingSign_AcceptsMinus()
    {
        string input = "-12345";

        var result = BigNumber.Parse(input, NumberStyles.AllowLeadingSign, null);

        result.Significand.ShouldBe(-12345);
    }

    #endregion

    #region NumberStyles.AllowTrailingSign

    [Fact]
    public void Parse_NumberStyles_AllowTrailingSign_AcceptsTrailingPlus()
    {
        string input = "12345+";

        var result = BigNumber.Parse(input, NumberStyles.AllowTrailingSign, null);

        result.Significand.ShouldBe(12345);
    }

    [Fact]
    public void Parse_NumberStyles_AllowTrailingSign_AcceptsTrailingMinus()
    {
        string input = "12345-";

        var result = BigNumber.Parse(input, NumberStyles.AllowTrailingSign, null);

        result.Significand.ShouldBe(-12345);
    }

    #endregion

    #region NumberStyles.AllowParentheses

    [Fact]
    public void Parse_NumberStyles_AllowParentheses_AcceptsParenthesesForNegative()
    {
        string input = "(12345)";

        var result = BigNumber.Parse(input, NumberStyles.AllowParentheses, null);

        result.Significand.ShouldBe(-12345);
    }

    [Fact]
    public void Parse_NumberStyles_AllowParentheses_WithoutParenthesesIsPositive()
    {
        string input = "12345";

        var result = BigNumber.Parse(input, NumberStyles.AllowParentheses, null);

        result.Significand.ShouldBe(12345);
    }

    #endregion

    #region NumberStyles.AllowDecimalPoint

    [Fact]
    public void Parse_NumberStyles_AllowDecimalPoint_AcceptsDecimal()
    {
        string input = "123.45";

        var result = BigNumber.Parse(input, NumberStyles.AllowDecimalPoint, null);

        result.Normalize().Significand.ShouldBe(12345);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    [Fact]
    public void Parse_NumberStyles_AllowDecimalPoint_AcceptsLeadingDecimal()
    {
        string input = ".45";

        var result = BigNumber.Parse(input, NumberStyles.AllowDecimalPoint, null);

        result.Normalize().Significand.ShouldBe(45);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    [Fact]
    public void Parse_NumberStyles_AllowDecimalPoint_AcceptsTrailingDecimal()
    {
        string input = "123.";

        var result = BigNumber.Parse(input, NumberStyles.AllowDecimalPoint, null);

        result.Significand.ShouldBe(123);
        result.Exponent.ShouldBe(0);
    }

    #endregion

    #region NumberStyles.AllowThousands

    [Fact]
    public void Parse_NumberStyles_AllowThousands_AcceptsThousandsSeparator()
    {
        string input = "1,234,567";

        var result = BigNumber.Parse(input, NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

        result.Significand.ShouldBe(1234567);
    }

    [Fact]
    public void Parse_NumberStyles_AllowThousands_AcceptsMultipleSeparators()
    {
        string input = "1,234,567,890";

        var result = BigNumber.Parse(input, NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

        result.Significand.ShouldBe(123456789);
        result.Exponent.ShouldBe(1);
    }

    #endregion

    #region NumberStyles.AllowExponent

    [Fact]
    public void Parse_NumberStyles_AllowExponent_AcceptsExponent()
    {
        string input = "123E5";

        var result = BigNumber.Parse(input, NumberStyles.AllowExponent, null);

        result.Significand.ShouldBe(123);
        result.Exponent.ShouldBe(5);
    }

    [Fact]
    public void Parse_NumberStyles_AllowExponent_AcceptsNegativeExponent()
    {
        string input = "123E-5";

        var result = BigNumber.Parse(input, NumberStyles.AllowExponent, null);

        result.Significand.ShouldBe(123);
        result.Exponent.ShouldBe(-5);
    }

    [Fact]
    public void Parse_NumberStyles_AllowExponent_AcceptsLowercaseE()
    {
        string input = "123e5";

        var result = BigNumber.Parse(input, NumberStyles.AllowExponent, null);

        result.Significand.ShouldBe(123);
        result.Exponent.ShouldBe(5);
    }

    #endregion

    #region NumberStyles.AllowCurrencySymbol

    [Fact]
    public void Parse_NumberStyles_AllowCurrencySymbol_AcceptsCurrencySymbol()
    {
        string input = "$123.45";

        var result = BigNumber.Parse(input, NumberStyles.AllowCurrencySymbol, CultureInfo.GetCultureInfo("en-US"));

        result.Normalize().Significand.ShouldBe(12345);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    [Fact]
    public void Parse_NumberStyles_AllowCurrencySymbol_AcceptsTrailingCurrencySymbol()
    {
        string input = "123,45€"; // fr-FR uses comma as decimal separator

        var result = BigNumber.Parse(input, NumberStyles.AllowCurrencySymbol, CultureInfo.GetCultureInfo("fr-FR"));

        result.Normalize().Significand.ShouldBe(12345);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    #endregion

    #region NumberStyles.Number

    [Fact]
    public void Parse_NumberStyles_Number_AcceptsCompleteNumber()
    {
        string input = "  -1,234.56  ";

        var result = BigNumber.Parse(input, NumberStyles.Number, CultureInfo.InvariantCulture);

        result.Normalize().Significand.ShouldBe(-123456);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    #endregion

    #region NumberStyles.Float

    [Fact]
    public void Parse_NumberStyles_Float_AcceptsScientificNotation()
    {
        string input = "  -1.23E10  ";

        var result = BigNumber.Parse(input, NumberStyles.Float, null);

        result.Normalize().Significand.ShouldBe(-123);
        result.Normalize().Exponent.ShouldBe(8);
    }

    [Fact]
    public void Parse_NumberStyles_Float_AcceptsInfinity()
    {
        string input = "Infinity";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.Float, null));
    }

    [Fact]
    public void Parse_NumberStyles_Float_AcceptsNaN()
    {
        string input = "NaN";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.Float, null));
    }

    #endregion

    #region NumberStyles.Currency

    [Fact]
    public void Parse_NumberStyles_Currency_AcceptsCurrencyFormat()
    {
        string input = "($1,234.56)";

        var result = BigNumber.Parse(input, NumberStyles.Currency, CultureInfo.GetCultureInfo("en-US"));

        result.Normalize().Significand.ShouldBe(-123456);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    #endregion

    #region NumberStyles.Any

    [Fact]
    public void Parse_NumberStyles_Any_AcceptsAnyValidFormat()
    {
        (string, int, int)[] testCases = new[]
        {
            ("123", 123, 0),
            ("  123  ", 123, 0),
            ("+123", 123, 0),
            ("-123", 123, 0), // Note: will be negative
            ("123.45", 12345, -2),
            ("123E5", 123, 5),
            ("1,234,567", 1234567, 0),
        };

        foreach ((string? input, int expectedSig, int expectedExp) in testCases)
        {
            var result = BigNumber.Parse(input, NumberStyles.Any, CultureInfo.InvariantCulture);
            BigNumber normalized = result.Normalize();

            if (input.StartsWith("-"))
            {
                ((int)Math.Abs((double)normalized.Significand)).ShouldBe(expectedSig);
            }
            else
            {
                ((int)normalized.Significand).ShouldBe(expectedSig);
            }

            normalized.Exponent.ShouldBe(expectedExp);
        }
    }

    #endregion

    #region NumberStyles.HexNumber

    [Fact]
    public void Parse_NumberStyles_HexNumber_NotSupported()
    {
        string input = "FF";

        Should.Throw<ArgumentException>(() => BigNumber.Parse(input, NumberStyles.HexNumber, null));
    }

    #endregion

    #region Culture-Specific Decimal Separators

    [Fact]
    public void Parse_DifferentCultures_FrenchDecimalSeparator()
    {
        string input = "123,45"; // French uses comma as decimal separator

        var result = BigNumber.Parse(input, NumberStyles.Number, CultureInfo.GetCultureInfo("fr-FR"));

        result.Normalize().Significand.ShouldBe(12345);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    [Fact]
    public void Parse_DifferentCultures_GermanThousandsSeparator()
    {
        string input = "1.234.567,89"; // German uses dot for thousands, comma for decimal

        var result = BigNumber.Parse(input, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"));

        result.Normalize().Significand.ShouldBe(123456789);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    #endregion

    #region TryParse with NumberStyles

    [Fact]
    public void TryParse_NumberStyles_ValidInput_ReturnsTrue()
    {
        string input = "  -123.45  ";

        bool success = BigNumber.TryParse(input, NumberStyles.Float, null, out BigNumber result);

        success.ShouldBeTrue();
        result.Normalize().Significand.ShouldBe(-12345);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    [Fact]
    public void TryParse_NumberStyles_InvalidInput_ReturnsFalse()
    {
        string input = "  -123.45  ";

        bool success = BigNumber.TryParse(input, NumberStyles.None, null, out BigNumber result);

        success.ShouldBeFalse();
        result.ShouldBe(default);
    }

    [Fact]
    public void TryParse_NumberStyles_Currency_ReturnsTrue()
    {
        string input = "$1,234.56";

        bool success = BigNumber.TryParse(input, NumberStyles.Currency, CultureInfo.GetCultureInfo("en-US"), out BigNumber result);

        success.ShouldBeTrue();
        result.Normalize().Significand.ShouldBe(123456);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    #endregion

    #region UTF-8 with NumberStyles

    [Fact]
    public void Parse_UTF8_NumberStyles_Integer()
    {
        ReadOnlySpan<byte> input = "  -12345  "u8;

        var result = BigNumber.Parse(input, NumberStyles.Integer, null);

        result.Significand.ShouldBe(-12345);
    }

    [Fact]
    public void Parse_UTF8_NumberStyles_Float()
    {
        ReadOnlySpan<byte> input = "123.45E10"u8;

        var result = BigNumber.Parse(input, NumberStyles.Float, null);

        result.Normalize().Significand.ShouldBe(12345);
        result.Normalize().Exponent.ShouldBe(8);
    }

    [Fact]
    public void TryParse_UTF8_NumberStyles_ValidInput()
    {
        ReadOnlySpan<byte> input = "  123.45  "u8;

        bool success = BigNumber.TryParse(input, NumberStyles.Float, null, out BigNumber result);

        success.ShouldBeTrue();
        result.Normalize().Significand.ShouldBe(12345);
        result.Normalize().Exponent.ShouldBe(-2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_NumberStyles_WithWhitespaceAndSign()
    {
        string input = "  +  123  ";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.Integer, null));
    }

    [Fact]
    public void Parse_NumberStyles_MultipleDecimalPoints()
    {
        string input = "123.45.67";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.Float, null));
    }

    [Fact]
    public void Parse_NumberStyles_OnlyDecimalPoint()
    {
        string input = ".";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.Float, null));
    }

    [Fact]
    public void Parse_NumberStyles_EmptyExponent()
    {
        string input = "123E";

        Should.Throw<FormatException>(() => BigNumber.Parse(input, NumberStyles.Float, null));
    }

    #endregion
}