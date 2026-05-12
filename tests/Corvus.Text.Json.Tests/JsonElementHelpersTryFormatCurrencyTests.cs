using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonElementHelpersTryFormatCurrencyTests
{
    [TestMethod]
    [DataRow("1234.56", 0, "$1,235")]
    [DataRow("1234.56", 2, "$1,234.56")]
    [DataRow("1234567.89", 2, "$1,234,567.89")]
    [DataRow("123.456", 2, "$123.46")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("-1234.56", 0, "-$1,235")]
    [DataRow("-1234.56", 2, "-$1,234.56")]
    [DataRow("-123.456", 2, "-$123.46")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual("($1,234.56)", result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual("1,234.56$", result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual("€1.234,56", result);
    }

    [TestMethod]
    [DataRow("0.00123", 5, "$0.00123")]
    [DataRow("0.999", 2, "$1.00")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("0", 2, "$0.00")]
    [DataRow("0.0", 2, "$0.00")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("123.456", 2, "$123.46")]
    [DataRow("123.454", 2, "$123.45")]
    [DataRow("123.455", 2, "$123.46")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual("¤1,234.57", result); // ¤ is the InvariantInfo currency symbol
    }

    [TestMethod]
    [DataRow("999.99", 0, "$1,000")]
    [DataRow("999.99", 1, "$1,000.0")]
    [DataRow("9999.99", 0, "$10,000")]
    [DataRow("99999.99", 0, "$100,000")]
    [DataRow("999999.99", 0, "$1,000,000")]
    [DataRow("9999999.99", 0, "$10,000,000")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("-999.99", 0, "-$1,000")]
    [DataRow("-9999.99", 0, "-$10,000")]
    [DataRow("-99999.99", 0, "-$100,000")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual("USD1,235", result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual("$1 234 567.89", result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual("$1,234:::56", result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual("EUR1 000", result);
    }

    [TestMethod]
    [DataRow("999.995", 2, "$1,000.00")]
    [DataRow("999.994", 2, "$999.99")]
    [DataRow("999.9999", 3, "$1,000.000")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(0, "($1,234.56)")]   // ($n)
    [DataRow(1, "-$1,234.56")]    // -$n
    [DataRow(2, "$-1,234.56")]    // $-n
    [DataRow(3, "$1,234.56-")]    // $n-
    [DataRow(4, "(1,234.56$)")]   // (n$)
    [DataRow(5, "-1,234.56$")]    // -n$
    [DataRow(6, "1,234.56-$")]    // n-$
    [DataRow(7, "1,234.56$-")]    // n$-
    [DataRow(8, "-1,234.56 $")]   // -n $
    [DataRow(9, "-$ 1,234.56")]   // -$ n
    [DataRow(10, "1,234.56 $-")]  // n $-
    [DataRow(11, "$ 1,234.56-")]  // $ n-
    [DataRow(12, "$ -1,234.56")]  // $ -n
    [DataRow(13, "1,234.56- $")]  // n- $
    [DataRow(14, "($ 1,234.56)")]  // ($ n)
    [DataRow(15, "(1,234.56 $)")]  // (n $)
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(0, 5)]   // ($n) — buffer too small for parenthesized form
    [DataRow(1, 5)]   // -$n
    [DataRow(2, 5)]   // $-n
    [DataRow(3, 5)]   // $n-
    [DataRow(4, 5)]   // (n$)
    [DataRow(5, 5)]   // -n$
    [DataRow(6, 5)]   // n-$
    [DataRow(7, 5)]   // n$-
    [DataRow(8, 5)]   // -n $
    [DataRow(9, 5)]   // -$ n
    [DataRow(10, 5)]  // n $-
    [DataRow(11, 5)]  // $ n-
    [DataRow(12, 5)]  // $ -n
    [DataRow(13, 5)]  // n- $
    [DataRow(14, 5)]  // ($ n)
    [DataRow(15, 5)]  // (n $)
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [DataRow(0, "$1,234.56")]   // $n
    [DataRow(1, "1,234.56$")]   // n$
    [DataRow(2, "$ 1,234.56")]  // $ n
    [DataRow(3, "1,234.56 $")]  // n $
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(0, 5)]   // $n
    [DataRow(1, 5)]   // n$
    [DataRow(2, 5)]   // $ n
    [DataRow(3, 5)]   // n $
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    /// <summary>
    /// Tests with buffer just 1 char less than the expected output for patterns that have
    /// late overflow guards (parenthesized patterns with closing bracket check).
    /// Only patterns 0, 4, 14, 15 have the guard — other patterns do CopyTo without checking.
    /// </summary>
    [TestMethod]
    [DataRow(0, 10)]   // ($1,234.56) = 11, buffer 10 fails at closing )
    [DataRow(4, 10)]   // (1,234.56$) = 11, buffer 10 fails at closing )
    [DataRow(14, 11)]  // ($ 1,234.56) = 12, buffer 11 fails at closing )
    [DataRow(15, 11)]  // (1,234.56 $) = 12, buffer 11 fails at closing )
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    /// <summary>
    /// Positive patterns with buffer just 1 less than needed.
    /// Only patterns 2 and 3 have late overflow guards (space char check).
    /// Patterns 0 and 1 don't have guards after number/symbol writes.
    /// $ 1,234.56=10, 1,234.56 $=10
    /// </summary>
    [TestMethod]
    [DataRow(2, 9)]   // $ 1,234.56 = 10, buffer 9 fails at space check
    [DataRow(3, 9)]   // 1,234.56 $ = 10, buffer 9 fails at space check
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }
}
