using System.Globalization;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonElementHelpersTryFormatFixedPointUtf8Tests
{
    [Theory]
    [InlineData("123.456", 2, "123.46")]
    [InlineData("123.454", 2, "123.45")]
    [InlineData("123.455", 2, "123.46")]
    [InlineData("0.999", 2, "1.00")]
    [InlineData("9.999", 2, "10.00")]
    [InlineData("99.999", 2, "100.00")]
    public void TryFormatFixedPoint_RoundsCorrectly(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatFixedPointWithSeparator(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            ".",
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123456", 2, "123456.00")]
    [InlineData("123456", 0, "123456")]
    [InlineData("1e5", 2, "100000.00")]
    [InlineData("1.23e3", 2, "1230.00")]
    public void TryFormatFixedPoint_HandlesIntegersWithTrailingZeros(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatFixedPointWithSeparator(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            ".",
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.000123", 5, "0.00012")]
    [InlineData("0.000123", 6, "0.000123")]
    [InlineData("0.000123", 7, "0.0001230")]
    [InlineData("0.000123", 3, "0.000")]
    [InlineData("0.000128", 4, "0.0001")]
    [InlineData("0.000128", 5, "0.00013")]
    [InlineData("1.23e-5", 6, "0.000012")]
    public void TryFormatFixedPoint_HandlesSmallNumbers(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatFixedPointWithSeparator(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            ".",
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("-123.456", 2, "-123.46")]
    [InlineData("-0.999", 2, "-1.00")]
    [InlineData("-123456", 2, "-123456.00")]
    [InlineData("-0.000123", 5, "-0.00012")]
    public void TryFormatFixedPoint_HandlesNegativeNumbers(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatFixedPointWithSeparator(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            ".",
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", 0, "0")]
    [InlineData("0", 2, "0.00")]
    [InlineData("0.0", 2, "0.00")]
    [InlineData("0.00", 3, "0.000")]
    public void TryFormatFixedPoint_HandlesZero(string jsonNumber, int precision, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatFixedPointWithSeparator(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            ".",
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatFixedPoint_UsesCustomDecimalSeparator()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatFixedPointWithSeparator(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            ",",
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("123,46", result);
    }

    [Fact]
    public void TryFormatFixedPoint_UsesCustomNegativeSign()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-123.456");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo { NegativeSign = "~" };

        bool success = JsonElementHelpers.TryFormatFixedPointWithSeparator(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            ".",
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("~123.46", result);
    }

    [Fact]
    public void TryFormatFixedPoint_ReturnsFalseWhenBufferTooSmall()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[5];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatFixedPointWithSeparator(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            ".",
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }
}