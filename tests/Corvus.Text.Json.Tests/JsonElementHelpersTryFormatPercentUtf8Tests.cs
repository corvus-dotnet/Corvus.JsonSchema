using System.Globalization;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonElementHelpersTryFormatPercentUtf8Tests
{
    [Theory]
    [InlineData("0.5", "50.00 %")]
    [InlineData("0.25", "25.00 %")]
    [InlineData("1", "100.00 %")]
    [InlineData("1.5", "150.00 %")]
    [InlineData("0.12345", "12.35 %")]
    public void TryFormatPercent_BasicValues_MultipliesBy100(string jsonNumber, string expected)
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

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            -1, // Use default precision
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.12345", 0, "12 %")]
    [InlineData("0.12345", 1, "12.3 %")]
    [InlineData("0.12345", 2, "12.35 %")]
    [InlineData("0.12345", 3, "12.345 %")]
    [InlineData("0.12345", 4, "12.3450 %")]
    public void TryFormatPercent_WithPrecision_FormatsToSpecifiedDecimalPlaces(string jsonNumber, int precision, string expected)
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

        bool success = JsonElementHelpers.TryFormatPercent(
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
    [InlineData("-0.5", "-50.00 %")]
    [InlineData("-0.25", "-25.00 %")]
    [InlineData("-1.5", "-150.00 %")]
    public void TryFormatPercent_WithNegativeNumbers_IncludesNegativeSign(string jsonNumber, string expected)
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

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            -1,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatPercent_WithGrouping_InsertsGroupSeparators()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("12.345");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentGroupSizes = new[] { 3 },
            PercentPositivePattern = 0 // n %
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            -1,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("1,234.50 %", result);
    }

    [Fact]
    public void TryFormatPercent_UsesCustomPercentSymbol()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "pct",
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentPositivePattern = 1 // n%
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            -1,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("50.00pct", result);
    }

    [Theory]
    [InlineData(0, "50.00 %")]  // n %
    [InlineData(1, "50.00%")]   // n%
    [InlineData(2, "%50.00")]   // %n
    [InlineData(3, "% 50.00")]  // % n
    public void TryFormatPercent_RespectsPositivePattern(int pattern, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentPositivePattern = pattern
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            -1,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "-50.00 %")]   // -n %
    [InlineData(1, "-50.00%")]    // -n%
    [InlineData(2, "-%50.00")]    // -%n
    [InlineData(3, "%-50.00")]    // %-n
    [InlineData(4, "%50.00-")]    // %n-
    [InlineData(5, "50.00-%")]    // n-%
    [InlineData(6, "50.00%-")]    // n%-
    [InlineData(7, "-% 50.00")]   // -% n
    [InlineData(8, "50.00 %-")]   // n %-
    [InlineData(9, "% 50.00-")]   // % n-
    [InlineData(10, "% -50.00")]  // % -n
    [InlineData(11, "50.00- %")]  // n- %
    public void TryFormatPercent_RespectsNegativePattern(int pattern, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentNegativePattern = pattern
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            -1,
            formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatPercent_ReturnsFalseWhenBufferTooSmall()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[3];
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            -1,
            formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [InlineData("0.12995", 2, "13.00 %")]  // Rounds up
    [InlineData("0.12994", 2, "12.99 %")]  // Rounds down
    [InlineData("0.99995", 2, "100.00 %")] // Rounds to 100
    public void TryFormatPercent_RoundsCorrectly(string jsonNumber, int precision, string expected)
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

        bool success = JsonElementHelpers.TryFormatPercent(
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
}