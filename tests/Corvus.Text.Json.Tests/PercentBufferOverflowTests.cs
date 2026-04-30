// <copyright file="PercentBufferOverflowTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Systematically tests that TryFormatPercent (both char and UTF-8 variants) never throws
/// for ANY buffer size smaller than the required output. Every negative percent pattern (0-11)
/// and positive percent pattern (0-3) is tested with every buffer size from 0 to requiredLength-1.
/// This exercises all intermediate buffer guards — the exact paths where we've found bugs.
/// </summary>
public class PercentBufferOverflowTests
{
    /// <summary>
    /// For each negative percent pattern (0-11) and every buffer size from 0 to one-less-than-needed,
    /// TryFormatPercent must return false without throwing.
    /// </summary>
    [Theory]
    [InlineData(0)]  // -n %
    [InlineData(1)]  // -n%
    [InlineData(2)]  // -%n
    [InlineData(3)]  // %-n
    [InlineData(4)]  // %n-
    [InlineData(5)]  // n-%
    [InlineData(6)]  // n%-
    [InlineData(7)]  // -% n
    [InlineData(8)]  // n %-
    [InlineData(9)]  // % n-
    [InlineData(10)] // % -n
    [InlineData(11)] // n- %
    public void TryFormatPercent_Char_NegativePattern_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreatePercentFormatInfo(pattern, isNegative: true);

        // First, determine the required output length
        Span<char> largeBuf = stackalloc char[64];
        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.True(success, $"Pattern {pattern} failed even with large buffer");

        // Now test every buffer size from 0 to requiredLength-1
        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.False(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.Equal(0, charsWritten);
        }
    }

    /// <summary>
    /// For each positive percent pattern (0-3) and every buffer size from 0 to one-less-than-needed,
    /// TryFormatPercent must return false without throwing.
    /// </summary>
    [Theory]
    [InlineData(0)]  // n %
    [InlineData(1)]  // n%
    [InlineData(2)]  // %n
    [InlineData(3)]  // % n
    public void TryFormatPercent_Char_PositivePattern_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreatePercentFormatInfo(pattern, isNegative: false);

        Span<char> largeBuf = stackalloc char[64];
        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.True(success, $"Pattern {pattern} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.False(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.Equal(0, charsWritten);
        }
    }

    /// <summary>
    /// UTF-8 variant: for each negative percent pattern (0-11) and every buffer size from 0
    /// to one-less-than-needed, TryFormatPercent must return false without throwing.
    /// </summary>
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
    public void TryFormatPercent_Utf8_NegativePattern_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreatePercentFormatInfo(pattern, isNegative: true);

        // First, determine the required output length
        Span<byte> largeBuf = stackalloc byte[64];
        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.True(success, $"Pattern {pattern} failed even with large buffer");

        // Now test every buffer size from 0 to requiredLength-1
        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.False(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.Equal(0, bytesWritten);
        }
    }

    /// <summary>
    /// UTF-8 variant: for each positive percent pattern (0-3) and every buffer size from 0
    /// to one-less-than-needed, TryFormatPercent must return false without throwing.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TryFormatPercent_Utf8_PositivePattern_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreatePercentFormatInfo(pattern, isNegative: false);

        Span<byte> largeBuf = stackalloc byte[64];
        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.True(success, $"Pattern {pattern} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.False(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.Equal(0, bytesWritten);
        }
    }

    /// <summary>
    /// Use a multi-byte percent symbol and negative sign to stress the UTF-8 guards.
    /// Multi-byte characters expose bugs where byte-count and char-count are conflated.
    /// </summary>
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
    public void TryFormatPercent_Utf8_NegativePattern_MultiByteSymbols_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Use multi-byte symbols: "−" (U+2212, 3 bytes UTF-8) and "٪" (U+066A, 2 bytes UTF-8)
        NumberFormatInfo formatInfo = CreateMultiBytePercentFormatInfo(pattern, isNegative: true);

        Span<byte> largeBuf = stackalloc byte[128];
        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.True(success, $"Pattern {pattern} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.False(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.Equal(0, bytesWritten);
        }
    }

    /// <summary>
    /// Use a multi-byte percent symbol for positive patterns.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TryFormatPercent_Utf8_PositivePattern_MultiByteSymbols_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreateMultiBytePercentFormatInfo(pattern, isNegative: false);

        Span<byte> largeBuf = stackalloc byte[128];
        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.True(success, $"Pattern {pattern} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.False(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.Equal(0, bytesWritten);
        }
    }

    /// <summary>
    /// Also test with a larger number that exercises group separators to hit more guards.
    /// </summary>
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
    public void TryFormatPercent_Char_NegativePattern_LargeNumber_AllBufferSizes_NeverThrows(int pattern)
    {
        // -12345.678 * 100 = -1,234,567.80 with percent formatting
        byte[] utf8 = Encoding.UTF8.GetBytes("-12345.678");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreatePercentFormatInfo(pattern, isNegative: true);

        Span<char> largeBuf = stackalloc char[64];
        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.True(success, $"Pattern {pattern} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.False(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.Equal(0, charsWritten);
        }
    }

    /// <summary>
    /// Large number positive patterns.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TryFormatPercent_Char_PositivePattern_LargeNumber_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("12345.678");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreatePercentFormatInfo(pattern, isNegative: false);

        Span<char> largeBuf = stackalloc char[64];
        bool success = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.True(success, $"Pattern {pattern} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.False(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.Equal(0, charsWritten);
        }
    }

    private static NumberFormatInfo CreatePercentFormatInfo(int pattern, bool isNegative)
    {
        var nfi = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentGroupSizes = [3],
            PercentSymbol = "%",
            NegativeSign = "-",
        };

        if (isNegative)
        {
            nfi.PercentNegativePattern = pattern;
        }
        else
        {
            nfi.PercentPositivePattern = pattern;
        }

        return nfi;
    }

    private static NumberFormatInfo CreateMultiBytePercentFormatInfo(int pattern, bool isNegative)
    {
        var nfi = new NumberFormatInfo
        {
            PercentDecimalDigits = 2,
            PercentDecimalSeparator = ".",
            PercentGroupSeparator = ",",
            PercentGroupSizes = [3],
            PercentSymbol = "\u066A",   // Arabic percent sign — 2 bytes in UTF-8
            NegativeSign = "\u2212",    // Minus sign — 3 bytes in UTF-8
        };

        if (isNegative)
        {
            nfi.PercentNegativePattern = pattern;
        }
        else
        {
            nfi.PercentPositivePattern = pattern;
        }

        return nfi;
    }
}
