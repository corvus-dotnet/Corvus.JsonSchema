// <copyright file="NumericFormatCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for numeric formatting patterns in <see cref="JsonElementHelpers"/>.
/// </summary>
[TestClass]
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

    [TestMethod]
    [DynamicData(nameof(CurrencyNegativePatternData))]
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

        Assert.IsTrue(success);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Currency Positive Patterns 0-3 (char)

    [TestMethod]
    [DataRow(0, "$1,234.56")]
    [DataRow(1, "1,234.56$")]
    [DataRow(2, "$ 1,234.56")]
    [DataRow(3, "1,234.56 $")]
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

        Assert.IsTrue(success);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
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

    [TestMethod]
    [DynamicData(nameof(PercentNegativePatternData))]
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

        Assert.IsTrue(success);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Percent Positive Patterns 0-3 (char)

    [TestMethod]
    [DataRow(0, "50.00 %")]
    [DataRow(1, "50.00%")]
    [DataRow(2, "%50.00")]
    [DataRow(3, "% 50.00")]
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

        Assert.IsTrue(success);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Hex Formatting (X) - char

    [TestMethod]
    [DataRow("255", "X", "FF")]
    [DataRow("0", "X", "0")]
    [DataRow("0", "X3", "000")]
    [DataRow("0", "X5", "00000")]
    [DataRow("16", "X", "10")]
    [DataRow("1", "X", "1")]
    [DataRow("4096", "X", "1000")]
    [DataRow("255", "x", "ff")]
    [DataRow("16", "x", "10")]
    public void TryFormatNumber_Hex_Char(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), null);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Binary Formatting (B) - char

    [TestMethod]
    [DataRow("255", "11111111")]
    [DataRow("0", "0")]
    [DataRow("10", "1010")]
    [DataRow("1", "1")]
    [DataRow("8", "1000")]
    [DataRow("128", "10000000")]
    public void TryFormatNumber_Binary_Char(string jsonNumber, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "B".AsSpan(), null);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    #endregion

    #region Unknown Format Specifier

    [TestMethod]
    [DataRow("Z")]
    [DataRow("Q")]
    [DataRow("W")]
    public void TryFormatNumber_UnknownFormat_ReturnsFalse_Char(string format)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [DataRow("Z")]
    [DataRow("Q")]
    public void TryFormatNumber_UnknownFormat_ReturnsFalse_Utf8(string format)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region Zero Formatting (UTF-8 path)

    [TestMethod]
    [DataRow("C2", "\u00A40.00")]
    [DataRow("P2", "0.00 %")]
    [DataRow("E2", "0.00E+000")]
    [DataRow("X", "0")]
    [DataRow("B", "0")]
    [DataRow("F2", "0.00")]
    [DataRow("G", "0")]
    public void TryFormatZero_Utf8_WithFormatSpecifiers(string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0");
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), NumberFormatInfo.InvariantInfo);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void TryFormatZero_Utf8_EmptyFormat_ReturnsZero()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0");
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, ReadOnlySpan<char>.Empty, null);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual("0", result);
    }

    [TestMethod]
    public void TryFormatZero_Char_WithFormatSpecifiers()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0");
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "C2".AsSpan(), NumberFormatInfo.InvariantInfo);

        Assert.IsTrue(success);
        Assert.AreEqual("\u00A40.00", destination.Slice(0, charsWritten).ToString());
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

    [TestMethod]
    [DynamicData(nameof(CurrencyNegativePatternUtf8Data))]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
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

    [TestMethod]
    [DynamicData(nameof(PercentNegativePatternUtf8Data))]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region UTF-8 Hex and Binary Formatting

    [TestMethod]
    [DataRow("255", "X", "FF")]
    [DataRow("0", "X", "0")]
    [DataRow("0", "X3", "000")]
    [DataRow("0", "X5", "00000")]
    [DataRow("16", "X", "10")]
    [DataRow("1", "X", "1")]
    [DataRow("4096", "X", "1000")]
    [DataRow("255", "x", "ff")]
    public void TryFormatNumber_Hex_Utf8(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), null);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("255", "11111111")]
    [DataRow("10", "1010")]
    [DataRow("1", "1")]
    [DataRow("128", "10000000")]
    public void TryFormatNumber_Binary_Utf8(string jsonNumber, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "B".AsSpan(), null);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region UTF-8 Percent Positive Patterns 0-3

    [TestMethod]
    [DataRow(0, "50.00 %")]
    [DataRow(1, "50.00%")]
    [DataRow(2, "%50.00")]
    [DataRow(3, "% 50.00")]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region UTF-8 Currency Positive Patterns 0-3

    [TestMethod]
    [DataRow(0, "$1,234.56")]
    [DataRow(1, "1,234.56$")]
    [DataRow(2, "$ 1,234.56")]
    [DataRow(3, "1,234.56 $")]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Buffer overflow tests - UTF-8 patterns

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(12)]
    [DataRow(13)]
    [DataRow(14)]
    [DataRow(15)]
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(10)]
    [DataRow(11)]
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [DataRow(0, 10)]   // ($1,234.56) = 11 bytes
    [DataRow(4, 10)]   // (1,234.56$) = 11 bytes
    [DataRow(14, 11)]  // ($ 1,234.56) = 12 bytes
    [DataRow(15, 11)]  // (1,234.56 $) = 12 bytes
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [DataRow(2, 9)]   // $ 1,234.56 = 10 bytes (space guard)
    [DataRow(3, 8)]   // 1,234.56 $ = 10 bytes, buffer 8 hits space guard (8+1>8)
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [DataRow(0, 7)]   // -50.00 % = 8 bytes
    [DataRow(1, 6)]   // -50.00% = 7 bytes
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [DataRow(3, 2)]   // % 50.00 = 7 bytes (guard for space after %)
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region TryFormatNumber UTF-8 edge cases

    [TestMethod]
    public void TryFormatNumber_Utf8_EmptyFormat_BufferTooSmall()
    {
        // Covers lines 3669-3670: empty format, span.TryCopyTo fails
        byte[] utf8 = Encoding.UTF8.GetBytes("12345");
        Span<byte> destination = stackalloc byte[2]; // Too small for "12345"

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, ReadOnlySpan<char>.Empty, null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatNumber_Utf8_InvalidPrecision()
    {
        // Covers lines 3679-3681: invalid precision format (e.g., "Gabc")
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "Gabc".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatNumber_Char_EmptyFormat_BufferTooSmall()
    {
        // Covers empty format path in char overload where TryTranscode fails
        byte[] utf8 = Encoding.UTF8.GetBytes("12345");
        Span<char> destination = stackalloc char[2]; // Too small for "12345"

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, ReadOnlySpan<char>.Empty, null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void TryFormatNumberAsString_InvalidFormat_ReturnsNull()
    {
        // Covers lines 360-362: TryFormatNumberAsString returns false when TryFormatNumber fails
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        bool success = JsonElementHelpers.TryFormatNumberAsString(
            utf8, "Z".AsSpan(), null, out string? value);

        Assert.IsFalse(success);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void TryFormatNumberAsString_InvalidPrecision_ReturnsNull()
    {
        // Covers lines 397-398: invalid precision in format string
        byte[] utf8 = Encoding.UTF8.GetBytes("123");
        bool success = JsonElementHelpers.TryFormatNumberAsString(
            utf8, "Gabc".AsSpan(), null, out string? value);

        Assert.IsFalse(success);
        Assert.IsNull(value);
    }

    #endregion

    #region Hex format buffer overflow

    [TestMethod]
    [DataRow("255", "X", 1)]   // FF needs 2 chars
    [DataRow("65535", "X", 2)] // FFFF needs 4 chars
    [DataRow("0", "X3", 1)]    // 000 needs 3 chars
    public void TryFormatNumber_Hex_BufferTooSmall_Char(string jsonNumber, string format, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[bufferSize];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [DataRow("255", "X", 1)]
    [DataRow("65535", "X", 2)]
    [DataRow("0", "X3", 1)]
    public void TryFormatNumber_Hex_BufferTooSmall_Utf8(string jsonNumber, string format, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[bufferSize];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region Binary format buffer overflow

    [TestMethod]
    [DataRow("255", 4)]   // 11111111 needs 8 chars
    [DataRow("7", 2)]     // 111 needs 3 chars
    public void TryFormatNumber_Binary_BufferTooSmall_Char(string jsonNumber, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[bufferSize];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "B".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [DataRow("255", 4)]
    [DataRow("7", 2)]
    public void TryFormatNumber_Binary_BufferTooSmall_Utf8(string jsonNumber, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[bufferSize];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "B".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region Exponential format buffer overflow

    [TestMethod]
    public void TryFormatNumber_Exponential_BufferTooSmall_Char()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        Span<char> destination = stackalloc char[3]; // Too small for "1.234560E+002"

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "E".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void TryFormatNumber_Exponential_BufferTooSmall_Utf8()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        Span<byte> destination = stackalloc byte[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "E".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region General format buffer overflow

    [TestMethod]
    public void TryFormatNumber_General_BufferTooSmall_Char()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123456789");
        Span<char> destination = stackalloc char[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "G".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void TryFormatNumber_General_BufferTooSmall_Utf8()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123456789");
        Span<byte> destination = stackalloc byte[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "G".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region Fixed-point and N format buffer overflow

    [TestMethod]
    public void TryFormatNumber_FixedPoint_BufferTooSmall_Char()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123456.789");
        Span<char> destination = stackalloc char[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "F2".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void TryFormatNumber_FixedPoint_BufferTooSmall_Utf8()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123456.789");
        Span<byte> destination = stackalloc byte[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "F2".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryFormatNumber_NumberFormat_BufferTooSmall_Char()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234567.89");
        Span<char> destination = stackalloc char[3]; // Too small for "1,234,567.89"

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "N2".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    public void TryFormatNumber_NumberFormat_BufferTooSmall_Utf8()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234567.89");
        Span<byte> destination = stackalloc byte[3]; // Too small

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "N2".AsSpan(), null);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region Edge cases for precision padding and small numbers

    [TestMethod]
    [DataRow("1.5", "F4", "1.5000")]       // fractional digits < precision → pad with zeros (line 2326-2336)
    [DataRow("123.4", "F6", "123.400000")]  // 1 fractional digit → pad 5 zeros
    [DataRow("10", "F3", "10.000")]         // integer only → all zeros in fractional
    public void TryFormatFixedPoint_PrecisionExceedsFractional_Char(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    [DataRow("1.5", "F4", "1.5000")]
    [DataRow("123.4", "F6", "123.400000")]
    [DataRow("10", "F3", "10.000")]
    public void TryFormatFixedPoint_PrecisionExceedsFractional_Utf8(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("0.0000001", "$0.0000")]  // leadingZeros(6) >= precision(4) → all zeros (line 2004-2010)
    [DataRow("0.001", "$0.00")]         // leadingZeros(2) >= precision(2) → all zeros
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("0.0000001", "$0.0000")]
    [DataRow("0.001", "$0.00")]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Exponential edge cases

    [TestMethod]
    [DataRow("0.000001", "E2", "1.00E-006")]     // Very small number
    [DataRow("1e10", "E2", "1.00E+010")]          // Large number via exponent
    [DataRow("-123.456", "E2", "-1.23E+002")]     // Negative with rounding
    public void TryFormatExponential_EdgeCases_Char(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    [DataRow("0.000001", "E2", "1.00E-006")]
    [DataRow("1e10", "E2", "1.00E+010")]
    [DataRow("-123.456", "E2", "-1.23E+002")]
    public void TryFormatExponential_EdgeCases_Utf8(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region N format (number with grouping) edge cases

    [TestMethod]
    [DataRow("1.5", "N4", "1.5000")]          // precision > available fractional
    [DataRow("1234567", "N0", "1,234,567")]    // no decimal, grouping only
    [DataRow("-1.5", "N4", "-1.5000")]         // negative, precision > available
    public void TryFormatNumber_NumberWithGrouping_EdgeCases_Char(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<char> destination = stackalloc char[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    [DataRow("1.5", "N4", "1.5000")]
    [DataRow("1234567", "N0", "1,234,567")]
    [DataRow("-1.5", "N4", "-1.5000")]
    public void TryFormatNumber_NumberWithGrouping_EdgeCases_Utf8(string jsonNumber, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        Span<byte> destination = stackalloc byte[100];

        bool success = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format.AsSpan(), CultureInfo.InvariantCulture);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    #endregion
}
