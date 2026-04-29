// <copyright file="HexBinaryFormatTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for hex (X) and binary (B) formatting of JSON numbers,
/// including edge cases: zero, precision padding, trailing zeros from exponent,
/// buffer overflow, and UTF-8 variants.
/// </summary>
public class HexBinaryFormatTests
{
    #region Hex format - char

    [Theory]
    [InlineData("0", "X", "0")]            // zero → "0"
    [InlineData("0", "X4", "0000")]        // zero with precision → "0000"
    [InlineData("0", "x", "0")]            // lowercase zero
    [InlineData("255", "X", "FF")]         // max single byte
    [InlineData("255", "x", "ff")]         // lowercase
    [InlineData("16", "X", "10")]          // exactly 16
    [InlineData("256", "X", "100")]        // 256 = 0x100
    [InlineData("1000", "X", "3E8")]       // 1000 has trailing zeros
    [InlineData("10000", "X", "2710")]     // larger trailing zeros
    [InlineData("123", "X8", "0000007B")]  // precision 8 → leading zeros
    [InlineData("255", "X4", "00FF")]      // precision with leading zeros
    [InlineData("1", "X1", "1")]           // precision = length (no padding needed)
    [InlineData("15", "X", "F")]           // single hex digit
    [InlineData("10", "X", "A")]           // A digit
    [InlineData("11", "X", "B")]           // B digit
    [InlineData("12", "X", "C")]           // C digit
    [InlineData("13", "X", "D")]           // D digit
    [InlineData("14", "X", "E")]           // E digit
    [InlineData("65535", "X", "FFFF")]     // all Fs
    public void TryFormatHex_Char(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    [Theory]
    [InlineData("1e2", "X", "64")]         // 100 (trailing zeros from exponent) = 0x64
    [InlineData("1e3", "X", "3E8")]        // 1000 via exponent
    [InlineData("12e1", "X", "78")]        // 120 via exponent = 0x78
    [InlineData("5e4", "X", "C350")]       // 50000 via exponent = 0xC350
    public void TryFormatHex_WithExponent_Char(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    [Theory]
    [InlineData("255", 1)]       // "FF" needs 2 chars, buffer has 1
    [InlineData("256", 2)]       // "100" needs 3 chars, buffer has 2
    [InlineData("123", 1)]       // "7B" needs 2 chars, buffer has 1
    public void TryFormatHex_BufferTooSmall_Char(string number, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "X", CultureInfo.InvariantCulture);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData("123", "X8", 4)]     // precision 8 but buffer only 4
    [InlineData("0", "X4", 3)]       // zero with precision 4 but buffer only 3
    public void TryFormatHex_PrecisionExceedsBuffer_Char(string number, string format, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    #endregion

    #region Binary format - char

    [Theory]
    [InlineData("0", "B", "0")]               // zero
    [InlineData("0", "B8", "00000000")]       // zero with precision
    [InlineData("1", "B", "1")]               // one
    [InlineData("2", "B", "10")]              // two
    [InlineData("3", "B", "11")]              // three
    [InlineData("7", "B", "111")]             // seven
    [InlineData("8", "B", "1000")]            // eight
    [InlineData("255", "B", "11111111")]      // 255
    [InlineData("256", "B", "100000000")]     // 256
    [InlineData("5", "B8", "00000101")]       // precision padding
    [InlineData("1", "B4", "0001")]           // precision padding
    public void TryFormatBinary_Char(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    [Theory]
    [InlineData("1e2", "B", "1100100")]       // 100 in binary
    [InlineData("1e3", "B", "1111101000")]    // 1000 in binary
    [InlineData("5e1", "B", "110010")]        // 50 in binary
    public void TryFormatBinary_WithExponent_Char(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.True(result);
        Assert.Equal(expected, destination.Slice(0, charsWritten).ToString());
    }

    [Theory]
    [InlineData("255", 4)]       // "11111111" needs 8, buffer has 4
    [InlineData("8", 3)]         // "1000" needs 4, buffer has 3
    public void TryFormatBinary_BufferTooSmall_Char(string number, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, "B", CultureInfo.InvariantCulture);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    [Theory]
    [InlineData("5", "B8", 4)]       // precision 8 but buffer only 4
    [InlineData("0", "B8", 4)]       // zero with precision 8 but buffer only 4
    public void TryFormatBinary_PrecisionExceedsBuffer_Char(string number, string format, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    #endregion

    #region Hex format - UTF-8

    [Theory]
    [InlineData("0", "X", "0")]
    [InlineData("0", "X4", "0000")]
    [InlineData("255", "X", "FF")]
    [InlineData("255", "x", "ff")]
    [InlineData("1000", "X", "3E8")]
    [InlineData("123", "X8", "0000007B")]
    [InlineData("1e2", "X", "64")]
    [InlineData("1e3", "X", "3E8")]
    public void TryFormatHex_Utf8(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<byte> destination = stackalloc byte[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format, CultureInfo.InvariantCulture);

        Assert.True(result);
        string actual = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("255", "X", 1)]
    [InlineData("123", "X8", 4)]
    [InlineData("0", "X4", 3)]
    public void TryFormatHex_BufferTooSmall_Utf8(string number, string format, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<byte> destination = stackalloc byte[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format, CultureInfo.InvariantCulture);

        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region Binary format - UTF-8

    [Theory]
    [InlineData("0", "B", "0")]
    [InlineData("0", "B8", "00000000")]
    [InlineData("255", "B", "11111111")]
    [InlineData("5", "B8", "00000101")]
    [InlineData("1e2", "B", "1100100")]
    public void TryFormatBinary_Utf8(string number, string format, string expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<byte> destination = stackalloc byte[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, format, CultureInfo.InvariantCulture);

        Assert.True(result);
        string actual = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("255", 4)]    // "11111111" needs 8
    [InlineData("8", 3)]      // "1000" needs 4
    public void TryFormatBinary_BufferTooSmall_Utf8(string number, int bufferSize)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<byte> destination = stackalloc byte[bufferSize];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int bytesWritten, "B", CultureInfo.InvariantCulture);

        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region Negative numbers - hex and binary (should fail for non-integer)

    [Theory]
    [InlineData("0.5", "X")]     // fractional → not integer, hex fails
    [InlineData("-1", "X")]      // negative → hex fails (unsigned format)
    [InlineData("0.5", "B")]     // fractional → binary fails
    [InlineData("-1", "B")]      // negative → binary fails
    public void TryFormatHexBinary_NonPositiveInteger_ReturnsFalse(string number, string format)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(number);
        Span<char> destination = stackalloc char[50];

        bool result = JsonElementHelpers.TryFormatNumber(
            utf8, destination, out int charsWritten, format, CultureInfo.InvariantCulture);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    #endregion
}
