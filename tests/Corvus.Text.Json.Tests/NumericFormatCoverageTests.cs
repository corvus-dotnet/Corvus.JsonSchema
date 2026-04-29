// <copyright file="NumericFormatCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for numeric formatting patterns in <see cref="JsonElementHelpers"/>.
/// </summary>
public class NumericFormatCoverageTests
{
    #region Currency Negative Patterns 0-15 (char)

    public static TheoryData<int, string> CurrencyNegativePatternData()
    {
        var data = new TheoryData<int, string>
        {
            { 0, "($1,234.56)" },
            { 1, "-$1,234.56" },
            { 2, "$-1,234.56" },
            { 3, "$1,234.56-" },
            { 4, "(1,234.56$)" },
            { 5, "-1,234.56$" },
            { 6, "1,234.56-$" },
            { 7, "1,234.56$-" },
            { 8, "-1,234.56 $" },
            { 9, "-$ 1,234.56" },
            { 10, "1,234.56 $-" },
            { 11, "$ 1,234.56-" },
            { 12, "$ -1,234.56" },
            { 13, "1,234.56- $" },
            { 14, "($ 1,234.56)" },
            { 15, "(1,234.56 $)" },
        };

        return data;
    }

    [Theory]
    [MemberData(nameof(CurrencyNegativePatternData))]
    public void TryFormatCurrency_NegativePatterns_Char(int pattern, string expected)
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
            CurrencyGroupSizes = [3],
            CurrencyDecimalDigits = 2,
            CurrencyNegativePattern = pattern,
            NegativeSign = "-",
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.True(success);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Currency Positive Patterns 0-3 (char)

    [Theory]
    [InlineData(0, "$1,234.56")]
    [InlineData(1, "1,234.56$")]
    [InlineData(2, "$ 1,234.56")]
    [InlineData(3, "1,234.56 $")]
    public void TryFormatCurrency_PositivePatterns_Char(int pattern, string expected)
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
            CurrencyGroupSizes = [3],
            CurrencyDecimalDigits = 2,
            CurrencyPositivePattern = pattern,
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.True(success);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Percent Negative Patterns 0-11 (char)

    public static TheoryData<int, string> PercentNegativePatternData()
    {
        var data = new TheoryData<int, string>
        {
            { 0, "-50.00 %" },
            { 1, "-50.00%" },
            { 2, "-%50.00" },
            { 3, "%-50.00" },
            { 4, "%50.00-" },
            { 5, "50.00-%" },
            { 6, "50.00%-" },
            { 7, "-% 50.00" },
            { 8, "50.00 %-" },
            { 9, "% 50.00-" },
            { 10, "% -50.00" },
            { 11, "50.00- %" },
        };

        return data;
    }

    [Theory]
    [MemberData(nameof(PercentNegativePatternData))]
    public void TryFormatPercent_NegativePatterns_Char(int pattern, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentGroupSeparator = ",",
            PercentDecimalSeparator = ".",
            PercentGroupSizes = [3],
            PercentDecimalDigits = 2,
            PercentNegativePattern = pattern,
            NegativeSign = "-",
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.True(success);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Percent Positive Patterns 0-3 (char)

    [Theory]
    [InlineData(0, "50.00 %")]
    [InlineData(1, "50.00%")]
    [InlineData(2, "%50.00")]
    [InlineData(3, "% 50.00")]
    public void TryFormatPercent_PositivePatterns_Char(int pattern, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[100];
        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentGroupSeparator = ",",
            PercentDecimalSeparator = ".",
            PercentGroupSizes = [3],
            PercentDecimalDigits = 2,
            PercentPositivePattern = pattern,
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.True(success);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Hex Formatting (X) - char

    [Theory]
    [InlineData("255", "X", "FF")]
    [InlineData("0", "X", "0")]
    [InlineData("16", "X", "10")]
    [InlineData("1", "X", "1")]
    [InlineData("4096", "X", "1000")]
    [InlineData("255", "x", "ff")]
    [InlineData("16", "x", "10")]
    public void TryFormatNumber_Hex_Char(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), null);

        Assert.True(success);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Binary Formatting (B) - char

    [Theory]
    [InlineData("255", "11111111")]
    [InlineData("0", "0")]
    [InlineData("10", "1010")]
    [InlineData("1", "1")]
    [InlineData("8", "1000")]
    [InlineData("128", "10000000")]
    public void TryFormatNumber_Binary_Char(string jsonNumber, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "B".AsSpan(), null);

        Assert.True(success);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Unknown Format Specifier

    [Theory]
    [InlineData("Z")]
    [InlineData("Q")]
    [InlineData("W")]
    public void TryFormatNumber_UnknownFormat_ReturnsFalse_Char(string format)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("Q")]
    public void TryFormatNumber_UnknownFormat_ReturnsFalse_Utf8(string format)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region Zero Formatting (UTF-8 path)

    [Theory]
    [InlineData("C2", "\u00A40.00")]
    [InlineData("P2", "0.00 %")]
    [InlineData("E2", "0.00E+000")]
    [InlineData("X", "0")]
    [InlineData("B", "0")]
    [InlineData("F2", "0.00")]
    [InlineData("G", "0")]
    public void TryFormatZero_Utf8_WithFormatSpecifiers(string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0");
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), NumberFormatInfo.InvariantInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryFormatZero_Utf8_EmptyFormat_ReturnsZero()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0");
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, ReadOnlySpan<char>.Empty, null);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal("0", result);
    }

    [Fact]
    public void TryFormatZero_Char_WithFormatSpecifiers()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0");
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "C2".AsSpan(), NumberFormatInfo.InvariantInfo);

        Assert.True(success);
        Assert.Equal("\u00A40.00", destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region UTF-8 Currency Negative Patterns 0-15

    public static TheoryData<int, string> CurrencyNegativePatternUtf8Data()
    {
        var data = new TheoryData<int, string>
        {
            { 0, "($1,234.56)" },
            { 1, "-$1,234.56" },
            { 2, "$-1,234.56" },
            { 3, "$1,234.56-" },
            { 4, "(1,234.56$)" },
            { 5, "-1,234.56$" },
            { 6, "1,234.56-$" },
            { 7, "1,234.56$-" },
            { 8, "-1,234.56 $" },
            { 9, "-$ 1,234.56" },
            { 10, "1,234.56 $-" },
            { 11, "$ 1,234.56-" },
            { 12, "$ -1,234.56" },
            { 13, "1,234.56- $" },
            { 14, "($ 1,234.56)" },
            { 15, "(1,234.56 $)" },
        };

        return data;
    }

    [Theory]
    [MemberData(nameof(CurrencyNegativePatternUtf8Data))]
    public void TryFormatCurrency_NegativePatterns_Utf8(int pattern, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[200];
        var formatInfo = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSizes = [3],
            CurrencyDecimalDigits = 2,
            CurrencyNegativePattern = pattern,
            NegativeSign = "-",
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, 2, formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion

    #region UTF-8 Percent Negative Patterns 0-11

    public static TheoryData<int, string> PercentNegativePatternUtf8Data()
    {
        var data = new TheoryData<int, string>
        {
            { 0, "-50.00 %" },
            { 1, "-50.00%" },
            { 2, "-%50.00" },
            { 3, "%-50.00" },
            { 4, "%50.00-" },
            { 5, "50.00-%" },
            { 6, "50.00%-" },
            { 7, "-% 50.00" },
            { 8, "50.00 %-" },
            { 9, "% 50.00-" },
            { 10, "% -50.00" },
            { 11, "50.00- %" },
        };

        return data;
    }

    [Theory]
    [MemberData(nameof(PercentNegativePatternUtf8Data))]
    public void TryFormatPercent_NegativePatterns_Utf8(int pattern, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[200];
        var formatInfo = new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentGroupSeparator = ",",
            PercentDecimalSeparator = ".",
            PercentGroupSizes = [3],
            PercentDecimalDigits = 2,
            PercentNegativePattern = pattern,
            NegativeSign = "-",
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, 2, formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion

    #region UTF-8 Hex and Binary Formatting

    [Theory]
    [InlineData("255", "X", "FF")]
    [InlineData("16", "X", "10")]
    [InlineData("1", "X", "1")]
    [InlineData("4096", "X", "1000")]
    [InlineData("255", "x", "ff")]
    public void TryFormatNumber_Hex_Utf8(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), null);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("255", "11111111")]
    [InlineData("10", "1010")]
    [InlineData("1", "1")]
    [InlineData("128", "10000000")]
    public void TryFormatNumber_Binary_Utf8(string jsonNumber, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "B".AsSpan(), null);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion

    #region UTF-8 Percent Positive Patterns 0-3

    [Theory]
    [InlineData(0, "50.00 %")]
    [InlineData(1, "50.00%")]
    [InlineData(2, "%50.00")]
    [InlineData(3, "% 50.00")]
    public void TryFormatPercent_PositivePatterns_Utf8(int pattern, string expected)
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
            PercentSymbol = "%",
            PercentGroupSeparator = ",",
            PercentDecimalSeparator = ".",
            PercentGroupSizes = [3],
            PercentDecimalDigits = 2,
            PercentPositivePattern = pattern,
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, 2, formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion

    #region UTF-8 Currency Positive Patterns 0-3

    [Theory]
    [InlineData(0, "$1,234.56")]
    [InlineData(1, "1,234.56$")]
    [InlineData(2, "$ 1,234.56")]
    [InlineData(3, "1,234.56 $")]
    public void TryFormatCurrency_PositivePatterns_Utf8(int pattern, string expected)
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
            CurrencyGroupSizes = [3],
            CurrencyDecimalDigits = 2,
            CurrencyPositivePattern = pattern,
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, 2, formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion
}
