using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;
public class JsonElementHelpersTryFormatNumberTests
{
    [Theory]
    [InlineData("1234.56", 2, "1,234.56")]
    [InlineData("1234.56", 1, "1,234.6")]
    [InlineData("1234567.89", 2, "1,234,567.89")]
    [InlineData("123.456", 3, "123.456")]
    [InlineData("0.123", 3, "0.123")]
    public void TryFormatNumber_WithGroupSeparators_FormatsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatNumber(
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
    [InlineData("-1234.56", 2, "-1,234.56")]
    [InlineData("-123456.78", 2, "-123,456.78")]
    [InlineData("-0.123", 3, "-0.123")]
    public void TryFormatNumber_WithNegativeNumbers_FormatsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatNumber(
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
    [InlineData("123.456", 2, "123.46")]
    [InlineData("123.454", 2, "123.45")]
    [InlineData("123.455", 2, "123.46")]
    [InlineData("0.999", 2, "1.00")]
    [InlineData("999.999", 2, "1,000.00")]
    public void TryFormatNumber_RoundsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatNumber(
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
    [InlineData("1234567", 2, "1,234,567.00")]
    [InlineData("1234567", 0, "1,234,567")]
    [InlineData("1e6", 2, "1,000,000.00")]
    public void TryFormatNumber_WithLargeIntegers_FormatsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatNumber(
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
    [InlineData("0.000123", 6, "0.000123")]
    [InlineData("0.000123", 5, "0.00012")]
    [InlineData("0.000123", 3, "0.000")]
    public void TryFormatNumber_WithSmallNumbers_FormatsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatNumber(
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
    [InlineData("0", 0, "0")]
    [InlineData("0", 2, "0.00")]
    public void TryFormatNumber_WithZero_FormatsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatNumber(
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
    public void TryFormatNumber_UsesCustomGroupSeparator()
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
            NumberGroupSeparator = " ",
            NumberDecimalSeparator = ","
        };

        bool success = JsonElementHelpers.TryFormatNumber(
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
        Assert.Equal("1 234,56", result);
    }

    [Fact]
    public void TryFormatNumber_UsesCustomNegativeSign()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo { NegativeSign = "~" };

        bool success = JsonElementHelpers.TryFormatNumber(
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
        Assert.Equal("~1,234.56", result);
    }

    [Fact]
    public void TryFormatNumber_ReturnsFalseWhenBufferTooSmall()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[5];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatNumber(
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
    [InlineData("999999.99", 2, "999,999.99")]
    [InlineData("9999.995", 2, "10,000.00")]
    public void TryFormatNumber_RoundingWithCarryIntoGroupSeparator(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatNumber(
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
    [InlineData("9999.995", 2, "10 000.00")]
    [InlineData("999.995", 2, "1 000.00")]
    public void TryFormatNumber_RoundingWithCarryIntoMultiCharGroupSeparator(string jsonNumber, int precision, string expected)
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
            NumberGroupSeparator = " ",
            NumberDecimalSeparator = ".",
            NegativeSign = "-"
        };

        bool success = JsonElementHelpers.TryFormatNumber(
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
}
