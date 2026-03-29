using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for TryFormatGeneral which outputs the normalized significand with optional exponent.
/// This outputs: [sign]<integral><fractional>[exponentChar][exponentSign]<exponent>
/// For example: "123456e-3" or "-999"
/// </summary>
public class JsonElementHelpersTryFormatGeneralTests
{
    [Theory]
    [InlineData("123", "123")]
    [InlineData("123.456", "123.456")]
    [InlineData("0.001", "1e-3")]
    [InlineData("0.01", "1e-2")]
    [InlineData("0.1", "0.1")]
    [InlineData("1e5", "100000")]
    [InlineData("1e-5", "1e-5")]
    [InlineData("1e6", "1000000")]
    [InlineData("1e15", "1e+15")]
    public void TryFormatGeneral_WithoutPrecisionLimit_OutputsFullSignificandAndExponent(string jsonNumber, string expected)
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

        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1, // no precision limit
            'e',
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123.556", 3, "124")]
    [InlineData("123.456", 3, "123")]
    [InlineData("123.456", 4, "123.5")]
    [InlineData("123.456", 5, "123.46")]
    [InlineData("999.5", 3, "1e+3")]
    [InlineData("9.995", 3, "10")]
    public void TryFormatGeneral_WithPrecision_RoundsSignificand(string jsonNumber, int precision, string expected)
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

        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            precision,
            'e',
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("-123", "-123")]
    [InlineData("-123.456", "-123.456")]
    [InlineData("-0.001", "-1e-3")]
    public void TryFormatGeneral_WithNegativeNumbers_IncludesNegativeSign(string jsonNumber, string expected)
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

        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            'e',
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1e-5", 'e', "1e-5")]
    [InlineData("1e-5", 'E', "1E-5")]
    [InlineData("1e15", 'e', "1e+15")]
    [InlineData("1e15", 'E', "1E+15")]
    public void TryFormatGeneral_UsesSpecifiedExponentChar(string jsonNumber, char exponentChar, string expected)
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

        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            exponentChar,
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatGeneral_UsesCustomNegativeSign()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-123");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo { NegativeSign = "~" };

        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            'e',
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal("~123", result);
    }

    [Fact]
    public void TryFormatGeneral_UsesCustomNegativeSignForExponent()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1.23e-5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo { NegativeSign = "~" };

        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            'e',
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal("1.23e~5", result);
    }

    [Fact]
    public void TryFormatGeneral_ReturnsFalseWhenBufferTooSmall()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[3];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            'e',
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData("123.456789", 6, "123.457")]
    [InlineData("0.9995", 3, "1")]
    public void TryFormatGeneral_RoundingCarriesIntoExponent(string jsonNumber, int precision, string expected)
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

        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            precision,
            'e',
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatGeneral_WithZero_OutputsZero()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int charsWritten,
            -1,
            'e',
            formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal("0", result);
    }
}