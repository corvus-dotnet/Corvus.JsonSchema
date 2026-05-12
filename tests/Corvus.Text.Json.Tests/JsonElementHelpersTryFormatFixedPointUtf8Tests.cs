using System.Globalization;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonElementHelpersTryFormatFixedPointUtf8Tests
{
    [TestMethod]
    [DataRow("123.456", 2, "123.46")]
    [DataRow("123.454", 2, "123.45")]
    [DataRow("123.455", 2, "123.46")]
    [DataRow("0.999", 2, "1.00")]
    [DataRow("9.999", 2, "10.00")]
    [DataRow("99.999", 2, "100.00")]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("123456", 2, "123456.00")]
    [DataRow("123456", 0, "123456")]
    [DataRow("1e5", 2, "100000.00")]
    [DataRow("1.23e3", 2, "1230.00")]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("0.000123", 5, "0.00012")]
    [DataRow("0.000123", 6, "0.000123")]
    [DataRow("0.000123", 7, "0.0001230")]
    [DataRow("0.000123", 3, "0.000")]
    [DataRow("0.000128", 4, "0.0001")]
    [DataRow("0.000128", 5, "0.00013")]
    [DataRow("1.23e-5", 6, "0.000012")]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("-123.456", 2, "-123.46")]
    [DataRow("-0.999", 2, "-1.00")]
    [DataRow("-123456", 2, "-123456.00")]
    [DataRow("-0.000123", 5, "-0.00012")]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("0", 0, "0")]
    [DataRow("0", 2, "0.00")]
    [DataRow("0.0", 2, "0.00")]
    [DataRow("0.00", 3, "0.000")]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual("123,46", result);
    }

    [TestMethod]
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

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual("~123.46", result);
    }

    [TestMethod]
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

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }
}
