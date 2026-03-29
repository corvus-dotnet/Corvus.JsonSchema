using System.Globalization;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonElementHelpersTryFormatCurrencyUtf8Tests
{
    [Theory]
    [InlineData("1234.56", 0, "$1,235")]
    [InlineData("1234.56", 2, "$1,234.56")]
    [InlineData("1234567.89", 2, "$1,234,567.89")]
    [InlineData("123.456", 2, "$123.46")]
    public void TryFormatCurrency_WithPositiveNumbers_FormatsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0, // $n
            CurrencyNegativePattern = 1  // -$n
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("-1234.56", 0, "-$1,235")]
    [InlineData("-1234.56", 2, "-$1,234.56")]
    [InlineData("-123.456", 2, "-$123.46")]
    public void TryFormatCurrency_WithNegativeNumbers_FormatsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0, // $n
            CurrencyNegativePattern = 1  // -$n
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern0_FormatsAsParentheses()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 0  // ($n)
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("($1,234.56)", result);
    }

    [Fact]
    public void TryFormatCurrency_PositivePattern1_FormatsAsNumberCurrency()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 1  // n$
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("1,234.56$", result);
    }

    [Fact]
    public void TryFormatCurrency_UsesCustomCurrencySymbol()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "€",
            CurrencyGroupSeparator = ".",
            CurrencyDecimalSeparator = ",",
            CurrencyPositivePattern = 0
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("€1.234,56", result);
    }

    [Theory]
    [InlineData("0.00123", 5, "$0.00123")]
    [InlineData("0.999", 2, "$1.00")]
    public void TryFormatCurrency_WithSmallNumbers_FormatsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", 2, "$0.00")]
    [InlineData("0.0", 2, "$0.00")]
    public void TryFormatCurrency_WithZero_FormatsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123.456", 2, "$123.46")]
    [InlineData("123.454", 2, "$123.45")]
    [InlineData("123.455", 2, "$123.46")]
    public void TryFormatCurrency_RoundsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatCurrency_ReturnsFalseWhenBufferTooSmall()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[5];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void TryFormatCurrency_UsesDefaultPrecision()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.567");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo; // Default CurrencyDecimalDigits is 2

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            formatInfo.CurrencyDecimalDigits,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("¤1,234.57", result); // ¤ is the InvariantInfo currency symbol
    }

    [Theory]
    [InlineData("999.99", 0, "$1,000")]
    [InlineData("999.99", 1, "$1,000.0")]
    [InlineData("9999.99", 0, "$10,000")]
    [InlineData("99999.99", 0, "$100,000")]
    [InlineData("999999.99", 0, "$1,000,000")]
    [InlineData("9999999.99", 0, "$10,000,000")]
    public void TryFormatCurrency_RoundingCarriesMultiplePowersOfTen(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("-999.99", 0, "-$1,000")]
    [InlineData("-9999.99", 0, "-$10,000")]
    [InlineData("-99999.99", 0, "-$100,000")]
    public void TryFormatCurrency_NegativeRoundingCarriesMultiplePowersOfTen(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0,
            CurrencyNegativePattern = 1  // -$n
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatCurrency_WithMultiCharacterCurrencySymbol()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "USD",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0  // $n
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            0,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("USD1,235", result);
    }

    [Fact]
    public void TryFormatCurrency_WithMultiCharacterGroupSeparator()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234567.89");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = " ",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("$1 234 567.89", result);
    }

    [Fact]
    public void TryFormatCurrency_WithMultiCharacterDecimalSeparator()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ":::",
            CurrencyPositivePattern = 0
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("$1,234:::56", result);
    }

    [Fact]
    public void TryFormatCurrency_RoundingWithMultiCharacterSeparators()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("999.99");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "EUR",
            CurrencyGroupSeparator = " ",
            CurrencyDecimalSeparator = ",",
            CurrencyPositivePattern = 0
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            0,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("EUR1 000", result);
    }

    [Theory]
    [InlineData("999.995", 2, "$1,000.00")]
    [InlineData("999.994", 2, "$999.99")]
    [InlineData("999.9999", 3, "$1,000.000")]
    public void TryFormatCurrency_RoundingWithFractionalPrecision(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyPositivePattern = 0
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern2_FormatsAsSymbolMinusNumber()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 2  // $-n
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("$-1,234.56", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern3_FormatsAsSymbolNumberMinus()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 3  // $n-
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("$1,234.56-", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern4_FormatsAsParenthesesNumberSymbol()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 4  // (n$)
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("(1,234.56$)", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern5_FormatsAsMinusNumberSymbol()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 5  // -n$
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("-1,234.56$", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern6_FormatsAsNumberMinusSymbol()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 6  // n-$
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("1,234.56-$", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern7_FormatsAsNumberSymbolMinus()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 7  // n$-
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("1,234.56$-", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern8_FormatsAsMinusNumberSpaceSymbol()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 8  // -n $
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("-1,234.56 $", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern9_FormatsAsMinusSymbolSpaceNumber()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 9  // -$ n
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("-$ 1,234.56", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern10_FormatsAsNumberSpaceSymbolMinus()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 10  // n $-
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("1,234.56 $-", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern11_FormatsAsSymbolSpaceNumberMinus()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 11  // $ n-
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("$ 1,234.56-", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern12_FormatsAsSymbolSpaceMinusNumber()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 12  // $ -n
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("$ -1,234.56", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern13_FormatsAsNumberMinusSpaceSymbol()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 13  // n- $
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("1,234.56- $", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern14_FormatsAsParenthesesSymbolSpaceNumber()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 14  // ($ n)
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("($ 1,234.56)", result);
    }

    [Fact]
    public void TryFormatCurrency_NegativePattern15_FormatsAsParenthesesNumberSpaceSymbol()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyNegativePattern = 15  // (n $)
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("(1,234.56 $)", result);
    }
}