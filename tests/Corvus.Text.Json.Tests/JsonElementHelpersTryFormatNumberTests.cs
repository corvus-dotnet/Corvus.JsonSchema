using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;
[TestClass]
public class JsonElementHelpersTryFormatNumberTests
{
    [TestMethod]
    [DataRow("1234.56", 2, "1,234.56")]
    [DataRow("1234.56", 1, "1,234.6")]
    [DataRow("1234567.89", 2, "1,234,567.89")]
    [DataRow("123.456", 3, "123.456")]
    [DataRow("0.123", 3, "0.123")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("-1234.56", 2, "-1,234.56")]
    [DataRow("-123456.78", 2, "-123,456.78")]
    [DataRow("-0.123", 3, "-0.123")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("123.456", 2, "123.46")]
    [DataRow("123.454", 2, "123.45")]
    [DataRow("123.455", 2, "123.46")]
    [DataRow("0.999", 2, "1.00")]
    [DataRow("999.999", 2, "1,000.00")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("1234567", 2, "1,234,567.00")]
    [DataRow("1234567", 0, "1,234,567")]
    [DataRow("1e6", 2, "1,000,000.00")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("0.000123", 6, "0.000123")]
    [DataRow("0.000123", 5, "0.00012")]
    [DataRow("0.000123", 3, "0.000")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("0", 0, "0")]
    [DataRow("0", 2, "0.00")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual("1 234,56", result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual("~1,234.56", result);
    }

    [TestMethod]
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [DataRow("999999.99", 2, "999,999.99")]
    [DataRow("9999.995", 2, "10,000.00")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("9999.995", 2, "10 000.00")]
    [DataRow("999.995", 2, "1 000.00")]
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

        Assert.IsTrue(success);
        string result = destination.Slice(0, charsWritten).ToString();
        Assert.AreEqual(expected, result);
    }
}
