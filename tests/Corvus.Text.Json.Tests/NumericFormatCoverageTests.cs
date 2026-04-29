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
    [InlineData("0", "X3", "000")]
    [InlineData("0", "X5", "00000")]
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
    [InlineData("0", "X", "0")]
    [InlineData("0", "X3", "000")]
    [InlineData("0", "X5", "00000")]
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

    #region Buffer overflow tests - UTF-8 patterns

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void TryFormatCurrency_NegativePattern_BufferTooSmall_Utf8(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
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
            CurrencyDecimalDigits = 2,
            CurrencyNegativePattern = pattern,
            CurrencyGroupSizes = [3]
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, 2, formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TryFormatCurrency_PositivePattern_BufferTooSmall_Utf8(int pattern)
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
            CurrencyDecimalDigits = 2,
            CurrencyPositivePattern = pattern,
            CurrencyGroupSizes = [3]
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, 2, formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void TryFormatPercent_NegativePattern_BufferTooSmall_Utf8(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[3];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentSymbol = "%",
            PercentNegativePattern = pattern,
            NegativeSign = "-"
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, -1, formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TryFormatPercent_PositivePattern_BufferTooSmall_Utf8(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[3];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentSymbol = "%",
            PercentPositivePattern = pattern,
            NegativeSign = "-"
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, -1, formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [InlineData(0, 10)]   // ($1,234.56) = 11 bytes
    [InlineData(4, 10)]   // (1,234.56$) = 11 bytes
    [InlineData(14, 11)]  // ($ 1,234.56) = 12 bytes
    [InlineData(15, 11)]  // (1,234.56 $) = 12 bytes
    public void TryFormatCurrency_NegativePattern_BufferOneLess_Utf8(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[bufferSize];
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
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, 2, formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [InlineData(2, 9)]   // $ 1,234.56 = 10 bytes (space guard)
    [InlineData(3, 8)]   // 1,234.56 $ = 10 bytes, buffer 8 hits space guard (8+1>8)
    public void TryFormatCurrency_PositivePattern_BufferOneLess_Utf8(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[bufferSize];
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
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, 2, formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [InlineData(0, 7)]   // -50.00 % = 8 bytes
    [InlineData(1, 6)]   // -50.00% = 7 bytes
    public void TryFormatPercent_NegativePattern_BufferOneLess_Utf8(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[bufferSize];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentSymbol = "%",
            PercentNegativePattern = pattern,
            NegativeSign = "-"
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, -1, formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [InlineData(3, 2)]   // % 50.00 = 7 bytes (guard for space after %)
    public void TryFormatPercent_PositivePattern_BufferOneLess_Utf8(int pattern, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[bufferSize];
        var formatInfo = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentSymbol = "%",
            PercentPositivePattern = pattern,
            NegativeSign = "-"
        };

        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten, -1, formatInfo);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region TryFormatNumber UTF-8 edge cases

    [Fact]
    public void TryFormatNumber_Utf8_EmptyFormat_BufferTooSmall()
    {
        // Covers lines 3669-3670: empty format, span.TryCopyTo fails
        byte[] utf8 = Encoding.UTF8.GetBytes("12345");
        Span<byte> destination = stackalloc byte[2]; // Too small for "12345"

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, ReadOnlySpan<char>.Empty, null);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void TryFormatNumber_Utf8_InvalidPrecision()
    {
        // Covers lines 3679-3681: invalid precision format (e.g., "Gabc")
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "Gabc".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void TryFormatNumber_Char_EmptyFormat_BufferTooSmall()
    {
        // Covers empty format path in char overload where TryTranscode fails
        byte[] utf8 = Encoding.UTF8.GetBytes("12345");
        Span<char> destination = stackalloc char[2]; // Too small for "12345"

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TryFormatNumberAsString_InvalidFormat_ReturnsNull()
    {
        // Covers lines 360-362: TryFormatNumberAsString returns false when TryFormatNumber fails
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        bool success = JsonElementHelpers.TryFormatNumberAsString(
            utf8, "Z".AsSpan(), null, out string? value);

        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public void TryFormatNumberAsString_InvalidPrecision_ReturnsNull()
    {
        // Covers lines 397-398: invalid precision in format string
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        bool success = JsonElementHelpers.TryFormatNumberAsString(
            utf8, "Gabc".AsSpan(), null, out string? value);

        Assert.False(success);
        Assert.Null(value);
    }

    #endregion

    #region Hex format buffer overflow

    [Theory]
    [InlineData("255", "X", 1)]   // FF needs 2 chars
    [InlineData("65535", "X", 2)] // FFFF needs 4 chars
    [InlineData("0", "X3", 1)]    // 000 needs 3 chars
    public void TryFormatNumber_Hex_BufferTooSmall_Char(string jsonNumber, string format, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[bufferSize];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData("255", "X", 1)]
    [InlineData("65535", "X", 2)]
    [InlineData("0", "X3", 1)]
    public void TryFormatNumber_Hex_BufferTooSmall_Utf8(string jsonNumber, string format, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[bufferSize];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region Binary format buffer overflow

    [Theory]
    [InlineData("255", 4)]   // 11111111 needs 8 chars
    [InlineData("7", 2)]     // 111 needs 3 chars
    public void TryFormatNumber_Binary_BufferTooSmall_Char(string jsonNumber, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[bufferSize];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "B".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData("255", 4)]
    [InlineData("7", 2)]
    public void TryFormatNumber_Binary_BufferTooSmall_Utf8(string jsonNumber, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[bufferSize];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "B".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region Exponential format buffer overflow

    [Fact]
    public void TryFormatNumber_Exponential_BufferTooSmall_Char()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        Span<char> destination = stackalloc char[3]; // Too small for "1.234560E+002"

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "E".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TryFormatNumber_Exponential_BufferTooSmall_Utf8()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        Span<byte> destination = stackalloc byte[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "E".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region General format buffer overflow

    [Fact]
    public void TryFormatNumber_General_BufferTooSmall_Char()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123456789");
        Span<char> destination = stackalloc char[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "G".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TryFormatNumber_General_BufferTooSmall_Utf8()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123456789");
        Span<byte> destination = stackalloc byte[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "G".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region Fixed-point and N format buffer overflow

    [Fact]
    public void TryFormatNumber_FixedPoint_BufferTooSmall_Char()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123456.789");
        Span<char> destination = stackalloc char[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "F2".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TryFormatNumber_FixedPoint_BufferTooSmall_Utf8()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123456.789");
        Span<byte> destination = stackalloc byte[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "F2".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void TryFormatNumber_NumberFormat_BufferTooSmall_Char()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234567.89");
        Span<char> destination = stackalloc char[3]; // Too small for "1,234,567.89"

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "N2".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void TryFormatNumber_NumberFormat_BufferTooSmall_Utf8()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234567.89");
        Span<byte> destination = stackalloc byte[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "N2".AsSpan(), null);

        Assert.False(success);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region Edge cases for precision padding and small numbers

    [Theory]
    [InlineData("1.5", "F4", "1.5000")]       // fractional digits < precision → pad with zeros (line 2326-2336)
    [InlineData("123.4", "F6", "123.400000")]  // 1 fractional digit → pad 5 zeros
    [InlineData("10", "F3", "10.000")]         // integer only → all zeros in fractional
    public void TryFormatFixedPoint_PrecisionExceedsFractional_Char(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.True(success);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    [Theory]
    [InlineData("1.5", "F4", "1.5000")]
    [InlineData("123.4", "F6", "123.400000")]
    [InlineData("10", "F3", "10.000")]
    public void TryFormatFixedPoint_PrecisionExceedsFractional_Utf8(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.0000001", "$0.0000")]  // leadingZeros(6) >= precision(4) → all zeros (line 2004-2010)
    [InlineData("0.001", "$0.00")]         // leadingZeros(2) >= precision(2) → all zeros
    public void TryFormatCurrency_SmallNumber_LeadingZerosExceedPrecision_Char(string jsonNumber, string expected)
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
            CurrencyDecimalDigits = 4,
            CurrencyPositivePattern = 0, // $n
            CurrencyGroupSizes = [3]
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten,
            expected.Contains("0000") ? 4 : 2, formatInfo);

        Assert.True(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.0000001", "$0.0000")]
    [InlineData("0.001", "$0.00")]
    public void TryFormatCurrency_SmallNumber_LeadingZerosExceedPrecision_Utf8(string jsonNumber, string expected)
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
            CurrencyDecimalDigits = 4,
            CurrencyPositivePattern = 0, // $n
            CurrencyGroupSizes = [3]
        };

        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int bytesWritten,
            expected.Contains("0000") ? 4 : 2, formatInfo);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion

    #region Exponential edge cases

    [Theory]
    [InlineData("0.000001", "E2", "1.00E-006")]     // Very small number
    [InlineData("1e10", "E2", "1.00E+010")]          // Large number via exponent
    [InlineData("-123.456", "E2", "-1.23E+002")]     // Negative with rounding
    public void TryFormatExponential_EdgeCases_Char(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.True(success);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    [Theory]
    [InlineData("0.000001", "E2", "1.00E-006")]
    [InlineData("1e10", "E2", "1.00E+010")]
    [InlineData("-123.456", "E2", "-1.23E+002")]
    public void TryFormatExponential_EdgeCases_Utf8(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion

    #region N format (number with grouping) edge cases

    [Theory]
    [InlineData("1.5", "N4", "1.5000")]          // precision > available fractional
    [InlineData("1234567", "N0", "1,234,567")]    // no decimal, grouping only
    [InlineData("-1.5", "N4", "-1.5000")]         // negative, precision > available
    public void TryFormatNumber_NumberWithGrouping_EdgeCases_Char(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.True(success);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    [Theory]
    [InlineData("1.5", "N4", "1.5000")]
    [InlineData("1234567", "N0", "1,234,567")]
    [InlineData("-1.5", "N4", "-1.5000")]
    public void TryFormatNumber_NumberWithGrouping_EdgeCases_Utf8(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.True(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, result);
    }

    #endregion
}
