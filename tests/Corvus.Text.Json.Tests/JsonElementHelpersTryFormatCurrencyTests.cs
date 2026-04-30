using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonElementHelpersTryFormatCurrencyTests
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[5];
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
            out int charsWritten,
            2,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
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

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo; // Default CurrencyDecimalDigits is 2

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            formatInfo.CurrencyDecimalDigits,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            0,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            0,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
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

        Span<char> destination = stackalloc char[100];
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
            out int charsWritten,
            precision,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "($1,234.56)")]   // ($n)
    [InlineData(1, "-$1,234.56")]    // -$n
    [InlineData(2, "$-1,234.56")]    // $-n
    [InlineData(3, "$1,234.56-")]    // $n-
    [InlineData(4, "(1,234.56$)")]   // (n$)
    [InlineData(5, "-1,234.56$")]    // -n$
    [InlineData(6, "1,234.56-$")]    // n-$
    [InlineData(7, "1,234.56$-")]    // n$-
    [InlineData(8, "-1,234.56 $")]   // -n $
    [InlineData(9, "-$ 1,234.56")]   // -$ n
    [InlineData(10, "1,234.56 $-")]  // n $-
    [InlineData(11, "$ 1,234.56-")]  // $ n-
    [InlineData(12, "$ -1,234.56")]  // $ -n
    [InlineData(13, "1,234.56- $")]  // n- $
    [InlineData(14, "($ 1,234.56)")]  // ($ n)
    [InlineData(15, "(1,234.56 $)")]  // (n $)
    public void TryFormatCurrency_RespectsNegativePattern(int pattern, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyDecimalDigits = 2,
            CurrencyNegativePattern = pattern,
            CurrencyGroupSizes = [3]
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 5)]   // ($n) — buffer too small for parenthesized form
    [InlineData(1, 5)]   // -$n
    [InlineData(2, 5)]   // $-n
    [InlineData(3, 5)]   // $n-
    [InlineData(4, 5)]   // (n$)
    [InlineData(5, 5)]   // -n$
    [InlineData(6, 5)]   // n-$
    [InlineData(7, 5)]   // n$-
    [InlineData(8, 5)]   // -n $
    [InlineData(9, 5)]   // -$ n
    [InlineData(10, 5)]  // n $-
    [InlineData(11, 5)]  // $ n-
    [InlineData(12, 5)]  // $ -n
    [InlineData(13, 5)]  // n- $
    [InlineData(14, 5)]  // ($ n)
    [InlineData(15, 5)]  // (n $)
    public void TryFormatCurrency_NegativePattern_BufferTooSmall(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[bufferSize];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyDecimalDigits = 2,
            CurrencyNegativePattern = pattern,
            CurrencyGroupSizes = [3]
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            2,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData(0, "$1,234.56")]   // $n
    [InlineData(1, "1,234.56$")]   // n$
    [InlineData(2, "$ 1,234.56")]  // $ n
    [InlineData(3, "1,234.56 $")]  // n $
    public void TryFormatCurrency_RespectsPositivePattern(int pattern, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyDecimalDigits = 2,
            CurrencyPositivePattern = pattern,
            CurrencyGroupSizes = [3]
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            2,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 5)]   // $n
    [InlineData(1, 5)]   // n$
    [InlineData(2, 5)]   // $ n
    [InlineData(3, 5)]   // n $
    public void TryFormatCurrency_PositivePattern_BufferTooSmall(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[bufferSize];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyDecimalDigits = 2,
            CurrencyPositivePattern = pattern,
            CurrencyGroupSizes = [3]
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            2,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Tests with buffer just 1 char less than the expected output for patterns that have
    /// late overflow guards (parenthesized patterns with closing bracket check).
    /// Only patterns 0, 4, 14, 15 have the guard — other patterns do CopyTo without checking.
    /// </summary>
    [Theory]
    [InlineData(0, 10)]   // ($1,234.56) = 11, buffer 10 fails at closing )
    [InlineData(4, 10)]   // (1,234.56$) = 11, buffer 10 fails at closing )
    [InlineData(14, 11)]  // ($ 1,234.56) = 12, buffer 11 fails at closing )
    [InlineData(15, 11)]  // (1,234.56 $) = 12, buffer 11 fails at closing )
    public void TryFormatCurrency_NegativePattern_BufferOneLessThanNeeded(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[bufferSize];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyDecimalDigits = 2,
            CurrencyNegativePattern = pattern,
            CurrencyGroupSizes = [3]
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            2,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Positive patterns with buffer just 1 less than needed.
    /// Only patterns 2 and 3 have late overflow guards (space char check).
    /// Patterns 0 and 1 don't have guards after number/symbol writes.
    /// $ 1,234.56=10, 1,234.56 $=10
    /// </summary>
    [Theory]
    [InlineData(2, 9)]   // $ 1,234.56 = 10, buffer 9 fails at space check
    [InlineData(3, 9)]   // 1,234.56 $ = 10, buffer 9 fails at space check
    public void TryFormatCurrency_PositivePattern_BufferOneLessThanNeeded(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[bufferSize];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyDecimalDigits = 2,
            CurrencyPositivePattern = pattern,
            CurrencyGroupSizes = [3]
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            2,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }
}