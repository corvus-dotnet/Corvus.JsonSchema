using System.Globalization;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonElementHelpersTryFormatExponentialUtf8Tests
{
    [TestMethod]
    [DataRow("123.456", 2, 'e', "1.23e+002")]
    [DataRow("123.456", 3, 'e', "1.235e+002")]
    [DataRow("0.00123", 3, 'e', "1.230e-003")]
    [DataRow("1234567", 2, 'e', "1.23e+006")]
    public void TryFormatExponential_WithPrecision_FormatsCorrectly(string jsonNumber, int precision, char exponentChar, string expected)
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

        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            exponentChar,
            formatInfo);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("123.456", 'e', "1.234560e+002")]
    [DataRow("0.00123", 'e', "1.230000e-003")]
    public void TryFormatExponential_DefaultPrecision_UsesSixDigits(string jsonNumber, char exponentChar, string expected)
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

        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            6,
            exponentChar,
            formatInfo);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("-123.456", 2, "-1.23e+002")]
    [DataRow("-0.00123", 3, "-1.230e-003")]
    public void TryFormatExponential_WithNegativeNumbers_FormatsCorrectly(string jsonNumber, int precision, string expected)
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

        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            'e',
            formatInfo);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("123.456", 'e', "1.23e+002")]
    [DataRow("123.456", 'E', "1.23E+002")]
    public void TryFormatExponential_UsesSpecifiedExponentChar(string jsonNumber, char exponentChar, string expected)
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

        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            exponentChar,
            formatInfo);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("0", 2, "0.00e+000")]
    [DataRow("0.0", 3, "0.000e+000")]
    public void TryFormatExponential_WithZero_FormatsCorrectly(string jsonNumber, int precision, string expected)
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

        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            'e',
            formatInfo);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("999.999", 2, "1.00e+003")]
    [DataRow("9.995", 2, "1.00e+001")]
    public void TryFormatExponential_RoundsCorrectly(string jsonNumber, int precision, string expected)
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

        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            'e',
            formatInfo);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void TryFormatExponential_UsesCustomDecimalSeparator()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<byte> destination = stackalloc byte[100];
        var formatInfo = new NumberFormatInfo { NumberDecimalSeparator = "," };

        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            'e',
            formatInfo);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual("1,23e+002", result);
    }

    [TestMethod]
    public void TryFormatExponential_UsesCustomNegativeSign()
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

        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            'e',
            formatInfo);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual("~1.23e+002", result);
    }

    [TestMethod]
    public void TryFormatExponential_ReturnsFalseWhenBufferTooSmall()
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

        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            2,
            'e',
            formatInfo);

        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [DataRow("1e10", 2, "1.00e+010")]
    [DataRow("1e-10", 2, "1.00e-010")]
    [DataRow("1.23e5", 3, "1.230e+005")]
    public void TryFormatExponential_WithExistingExponent_FormatsCorrectly(string jsonNumber, int precision, string expected)
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

        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative,
            integral,
            fractional,
            exponent,
            destination,
            out int bytesWritten,
            precision,
            'e',
            formatInfo);

        Assert.IsTrue(success);
        string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, result);
    }
}
