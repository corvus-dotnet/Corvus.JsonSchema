// <copyright file="HexBinaryFormatTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for hex (X) and binary (B) formatting of JSON numbers,
/// including edge cases: zero, precision padding, trailing zeros from exponent,
/// buffer overflow, and UTF-8 variants.
/// </summary>
[TestClass]
public class HexBinaryFormatTests
{
    #region Hex format - char

    [TestMethod]
    [DataRow("0", "X", "0")]            // zero → "0"
    [DataRow("0", "X4", "0000")]        // zero with precision → "0000"
    [DataRow("0", "x", "0")]            // lowercase zero
    [DataRow("255", "X", "FF")]         // max single byte
    [DataRow("255", "x", "ff")]         // lowercase
    [DataRow("16", "X", "10")]          // exactly 16
    [DataRow("256", "X", "100")]        // 256 = 0x100
    [DataRow("1000", "X", "3E8")]       // 1000 has trailing zeros
    [DataRow("10000", "X", "2710")]     // larger trailing zeros
    [DataRow("123", "X8", "0000007B")]  // precision 8 → leading zeros
    [DataRow("255", "X4", "00FF")]      // precision with leading zeros
    [DataRow("1", "X1", "1")]           // precision = length (no padding needed)
    [DataRow("15", "X", "F")]           // single hex digit
    [DataRow("10", "X", "A")]           // A digit
    [DataRow("11", "X", "B")]           // B digit
    [DataRow("12", "X", "C")]           // C digit
    [DataRow("13", "X", "D")]           // D digit
    [DataRow("14", "X", "E")]           // E digit
    [DataRow("65535", "X", "FFFF")]     // all Fs
    public void TryFormatHex_Char(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    [DataRow("1e2", "X", "64")]         // 100 (trailing zeros from exponent) = 0x64
    [DataRow("1e3", "X", "3E8")]        // 1000 via exponent
    [DataRow("12e1", "X", "78")]        // 120 via exponent = 0x78
    [DataRow("5e4", "X", "C350")]       // 50000 via exponent = 0xC350
    public void TryFormatHex_WithExponent_Char(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    [DataRow("255", 1)]       // "FF" needs 2 chars, buffer has 1
    [DataRow("256", 2)]       // "100" needs 3 chars, buffer has 2
    [DataRow("123", 1)]       // "7B" needs 2 chars, buffer has 1
    public void TryFormatHex_BufferTooSmall_Char(string number, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "X", CultureInfo.InvariantCulture);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [DataRow("123", "X8", 4)]     // precision 8 but buffer only 4
    [DataRow("0", "X4", 3)]       // zero with precision 4 but buffer only 3
    public void TryFormatHex_PrecisionExceedsBuffer_Char(string number, string format, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    #endregion

    #region Binary format - char

    [TestMethod]
    [DataRow("0", "B", "0")]               // zero
    [DataRow("0", "B8", "00000000")]       // zero with precision
    [DataRow("1", "B", "1")]               // one
    [DataRow("2", "B", "10")]              // two
    [DataRow("3", "B", "11")]              // three
    [DataRow("7", "B", "111")]             // seven
    [DataRow("8", "B", "1000")]            // eight
    [DataRow("255", "B", "11111111")]      // 255
    [DataRow("256", "B", "100000000")]     // 256
    [DataRow("5", "B8", "00000101")]       // precision padding
    [DataRow("1", "B4", "0001")]           // precision padding
    public void TryFormatBinary_Char(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    [DataRow("1e2", "B", "1100100")]       // 100 in binary
    [DataRow("1e3", "B", "1111101000")]    // 1000 in binary
    [DataRow("5e1", "B", "110010")]        // 50 in binary
    public void TryFormatBinary_WithExponent_Char(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.IsTrue(result);
        Assert.AreEqual(expected, destination.Slice(0, charsWritten).ToString());
    }

    [TestMethod]
    [DataRow("255", 4)]       // "11111111" needs 8, buffer has 4
    [DataRow("8", 3)]         // "1000" needs 4, buffer has 3
    public void TryFormatBinary_BufferTooSmall_Char(string number, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "B", CultureInfo.InvariantCulture);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [DataRow("5", "B8", 4)]       // precision 8 but buffer only 4
    [DataRow("0", "B8", 4)]       // zero with precision 8 but buffer only 4
    public void TryFormatBinary_PrecisionExceedsBuffer_Char(string number, string format, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    #endregion

    #region Hex format - UTF-8

    [TestMethod]
    [DataRow("0", "X", "0")]
    [DataRow("0", "X4", "0000")]
    [DataRow("255", "X", "FF")]
    [DataRow("255", "x", "ff")]
    [DataRow("1000", "X", "3E8")]
    [DataRow("123", "X8", "0000007B")]
    [DataRow("1e2", "X", "64")]
    [DataRow("1e3", "X", "3E8")]
    public void TryFormatHex_Utf8(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<byte> destination = stackalloc byte[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format, CultureInfo.InvariantCulture);

        Assert.IsTrue(result);
        string actual = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("255", "X", 1)]
    [DataRow("123", "X8", 4)]
    [DataRow("0", "X4", 3)]
    public void TryFormatHex_BufferTooSmall_Utf8(string number, string format, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<byte> destination = stackalloc byte[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format, CultureInfo.InvariantCulture);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region Binary format - UTF-8

    [TestMethod]
    [DataRow("0", "B", "0")]
    [DataRow("0", "B8", "00000000")]
    [DataRow("255", "B", "11111111")]
    [DataRow("5", "B8", "00000101")]
    [DataRow("1e2", "B", "1100100")]
    public void TryFormatBinary_Utf8(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<byte> destination = stackalloc byte[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format, CultureInfo.InvariantCulture);

        Assert.IsTrue(result);
        string actual = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("255", 4)]    // "11111111" needs 8
    [DataRow("8", 3)]      // "1000" needs 4
    public void TryFormatBinary_BufferTooSmall_Utf8(string number, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<byte> destination = stackalloc byte[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "B", CultureInfo.InvariantCulture);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region Negative numbers - hex and binary (should fail for non-integer)

    [TestMethod]
    [DataRow("0.5", "X")]     // fractional → not integer, hex fails
    [DataRow("-1", "X")]      // negative → hex fails (unsigned format)
    [DataRow("0.5", "B")]     // fractional → binary fails
    [DataRow("-1", "B")]      // negative → binary fails
    public void TryFormatHexBinary_NonPositiveInteger_ReturnsFalse(string number, string format)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    #endregion
}
