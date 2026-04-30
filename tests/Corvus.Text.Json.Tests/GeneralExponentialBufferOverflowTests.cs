// <copyright file="GeneralExponentialBufferOverflowTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Systematically tests that TryFormatGeneral and TryFormatExponential (both char and UTF-8 variants)
/// never throw for ANY buffer size smaller than the required output. Tests with different numeric values
/// to exercise trailing zero removal, rounding, negative sign, and exponent overflow guards.
/// </summary>
public class GeneralExponentialBufferOverflowTests
{
    /// <summary>
    /// General format, char variant — negative number with various buffer sizes.
    /// </summary>
    [Theory]
    [InlineData("-0.5", 'G')]
    [InlineData("-12345.678", 'G')]
    [InlineData("-0.000123", 'G')]
    [InlineData("-99999999", 'G')]
    [InlineData("-1e10", 'g')]
    [InlineData("-1.23456789012345", 'G')]
    public void TryFormatGeneral_Char_AllBufferSizes_NeverThrows(string jsonNumber, char exponentChar)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        Span<char> largeBuf = stackalloc char[128];
        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, -1, exponentChar, formatInfo);
        Assert.True(success, $"Failed with large buffer for {jsonNumber}");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatGeneral(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, -1, exponentChar, formatInfo);

            Assert.False(result, $"Input {jsonNumber}, bufSize {bufSize}: expected false (requiredLength={requiredLength})");
            Assert.Equal(0, charsWritten);
        }
    }

    /// <summary>
    /// General format, char variant — positive number with various buffer sizes.
    /// </summary>
    [Theory]
    [InlineData("0.5")]
    [InlineData("12345.678")]
    [InlineData("0.000123")]
    [InlineData("99999999")]
    public void TryFormatGeneral_Char_Positive_AllBufferSizes_NeverThrows(string jsonNumber)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        Span<char> largeBuf = stackalloc char[128];
        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, -1, 'G', formatInfo);
        Assert.True(success, $"Failed with large buffer for {jsonNumber}");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatGeneral(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, -1, 'G', formatInfo);

            Assert.False(result, $"Input {jsonNumber}, bufSize {bufSize}: expected false (requiredLength={requiredLength})");
            Assert.Equal(0, charsWritten);
        }
    }

    /// <summary>
    /// General format with explicit precision to exercise rounding paths.
    /// </summary>
    [Theory]
    [InlineData("-123.456", 2)]
    [InlineData("-0.99999", 3)]
    [InlineData("-12345.6789", 5)]
    [InlineData("0.99999", 1)]
    public void TryFormatGeneral_Char_WithPrecision_AllBufferSizes_NeverThrows(string jsonNumber, int precision)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        Span<char> largeBuf = stackalloc char[128];
        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, precision, 'G', formatInfo);
        Assert.True(success, $"Failed with large buffer for {jsonNumber}");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatGeneral(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, precision, 'G', formatInfo);

            Assert.False(result, $"Input {jsonNumber} P{precision}, bufSize {bufSize}: expected false (requiredLength={requiredLength})");
            Assert.Equal(0, charsWritten);
        }
    }

    /// <summary>
    /// General format, UTF-8 variant — negative number with various buffer sizes.
    /// </summary>
    [Theory]
    [InlineData("-0.5")]
    [InlineData("-12345.678")]
    [InlineData("-0.000123")]
    [InlineData("-99999999")]
    [InlineData("-1.23456789012345")]
    public void TryFormatGeneral_Utf8_AllBufferSizes_NeverThrows(string jsonNumber)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        Span<byte> largeBuf = stackalloc byte[128];
        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, -1, 'G', formatInfo);
        Assert.True(success, $"Failed with large buffer for {jsonNumber}");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatGeneral(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, -1, 'G', formatInfo);

            Assert.False(result, $"Input {jsonNumber}, bufSize {bufSize}: expected false (requiredLength={requiredLength})");
            Assert.Equal(0, bytesWritten);
        }
    }

    /// <summary>
    /// Exponential format, char variant — various buffer sizes.
    /// </summary>
    [Theory]
    [InlineData("-0.5", 'E')]
    [InlineData("-12345.678", 'E')]
    [InlineData("-0.000123", 'e')]
    [InlineData("-99999999", 'E')]
    [InlineData("0.5", 'E')]
    [InlineData("12345.678", 'e')]
    public void TryFormatExponential_Char_AllBufferSizes_NeverThrows(string jsonNumber, char exponentChar)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;
        int defaultPrecision = 6;

        Span<char> largeBuf = stackalloc char[128];
        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, defaultPrecision, exponentChar, formatInfo);
        Assert.True(success, $"Failed with large buffer for {jsonNumber}");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatExponential(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, defaultPrecision, exponentChar, formatInfo);

            Assert.False(result, $"Input {jsonNumber}, bufSize {bufSize}: expected false (requiredLength={requiredLength})");
            Assert.Equal(0, charsWritten);
        }
    }

    /// <summary>
    /// Exponential format with explicit precision to exercise rounding.
    /// </summary>
    [Theory]
    [InlineData("-123.456", 2)]
    [InlineData("-0.99999", 1)]
    [InlineData("12345.6789", 3)]
    public void TryFormatExponential_Char_WithPrecision_AllBufferSizes_NeverThrows(string jsonNumber, int precision)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        Span<char> largeBuf = stackalloc char[128];
        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, precision, 'E', formatInfo);
        Assert.True(success, $"Failed with large buffer for {jsonNumber}");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatExponential(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, precision, 'E', formatInfo);

            Assert.False(result, $"Input {jsonNumber} P{precision}, bufSize {bufSize}: expected false (requiredLength={requiredLength})");
            Assert.Equal(0, charsWritten);
        }
    }

    /// <summary>
    /// Exponential format, UTF-8 variant — various buffer sizes.
    /// </summary>
    [Theory]
    [InlineData("-0.5")]
    [InlineData("-12345.678")]
    [InlineData("-0.000123")]
    [InlineData("0.5")]
    [InlineData("12345.678")]
    public void TryFormatExponential_Utf8_AllBufferSizes_NeverThrows(string jsonNumber)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;
        int defaultPrecision = 6;

        Span<byte> largeBuf = stackalloc byte[128];
        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, defaultPrecision, 'E', formatInfo);
        Assert.True(success, $"Failed with large buffer for {jsonNumber}");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatExponential(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, defaultPrecision, 'E', formatInfo);

            Assert.False(result, $"Input {jsonNumber}, bufSize {bufSize}: expected false (requiredLength={requiredLength})");
            Assert.Equal(0, bytesWritten);
        }
    }

    /// <summary>
    /// Test with exponent >= 100 to exercise the 3-digit exponent path in UTF-8.
    /// This is a specifically identified uncovered path (lines 6369-6378).
    /// </summary>
    [Theory]
    [InlineData("1e100")]
    [InlineData("-1e200")]
    [InlineData("1.23e150")]
    public void TryFormatExponential_Utf8_LargeExponent_AllBufferSizes_NeverThrows(string jsonNumber)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;
        int defaultPrecision = 6;

        Span<byte> largeBuf = stackalloc byte[128];
        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, defaultPrecision, 'E', formatInfo);
        Assert.True(success, $"Failed with large buffer for {jsonNumber}");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatExponential(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, defaultPrecision, 'E', formatInfo);

            Assert.False(result, $"Input {jsonNumber}, bufSize {bufSize}: expected false (requiredLength={requiredLength})");
            Assert.Equal(0, bytesWritten);
        }
    }

    /// <summary>
    /// Test with exponent >= 100 in char variant.
    /// </summary>
    [Theory]
    [InlineData("1e100")]
    [InlineData("-1e200")]
    [InlineData("1.23e150")]
    public void TryFormatExponential_Char_LargeExponent_AllBufferSizes_NeverThrows(string jsonNumber)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;
        int defaultPrecision = 6;

        Span<char> largeBuf = stackalloc char[128];
        bool success = JsonElementHelpers.TryFormatExponential(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, defaultPrecision, 'E', formatInfo);
        Assert.True(success, $"Failed with large buffer for {jsonNumber}");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatExponential(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, defaultPrecision, 'E', formatInfo);

            Assert.False(result, $"Input {jsonNumber}, bufSize {bufSize}: expected false (requiredLength={requiredLength})");
            Assert.Equal(0, charsWritten);
        }
    }

    /// <summary>
    /// General format with number that exercises the "all digits removed" path (lines 2625-2630).
    /// A very small number with low precision causes all significand digits to be rounded away.
    /// </summary>
    [Theory]
    [InlineData("0.0001", 1)]
    [InlineData("0.00001", 2)]
    public void TryFormatGeneral_Char_AllDigitsRounded_AllBufferSizes_NeverThrows(string jsonNumber, int precision)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonNumber);
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        Span<char> largeBuf = stackalloc char[128];
        bool success = JsonElementHelpers.TryFormatGeneral(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, precision, 'G', formatInfo);
        Assert.True(success, $"Failed with large buffer for {jsonNumber}");

        if (requiredLength > 0)
        {
            Span<char> pool = stackalloc char[requiredLength];
            for (int bufSize = 0; bufSize < requiredLength; bufSize++)
            {
                Span<char> destination = pool.Slice(0, bufSize);

                bool result = JsonElementHelpers.TryFormatGeneral(
                    isNegative, integral, fractional, exponent,
                    destination, out int charsWritten, precision, 'G', formatInfo);

                Assert.False(result, $"Input {jsonNumber} P{precision}, bufSize {bufSize}: expected false (requiredLength={requiredLength})");
                Assert.Equal(0, charsWritten);
            }
        }
    }
}
