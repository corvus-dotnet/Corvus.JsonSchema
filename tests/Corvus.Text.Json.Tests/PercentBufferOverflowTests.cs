// <copyright file="PercentBufferOverflowTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Systematically tests that TryFormatPercent (both char and UTF-8 variants) never throws
/// for ANY buffer size smaller than the required output. Every negative percent pattern (0-11)
/// and positive percent pattern (0-3) is tested with every buffer size from 0 to requiredLength-1.
/// This exercises all intermediate buffer guards — the exact paths where we've found bugs.
/// </summary>
[TestClass]
public class PercentBufferOverflowTests
{
    /// <summary>
    /// For each negative percent pattern (0-11) and every buffer size from 0 to one-less-than-needed,
    /// TryFormatPercent must return false without throwing.
    /// </summary>
    [TestMethod]
    [DataRow(0)]  // -n %
    [DataRow(1)]  // -n%
    [DataRow(2)]  // -%n
    [DataRow(3)]  // %-n
    [DataRow(4)]  // %n-
    [DataRow(5)]  // n-%
    [DataRow(6)]  // n%-
    [DataRow(7)]  // -% n
    [DataRow(8)]  // n %-
    [DataRow(9)]  // % n-
    [DataRow(10)] // % -n
    [DataRow(11)] // n- %
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
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        // Now test every buffer size from 0 to requiredLength-1
        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, charsWritten);
        }
    }

    /// <summary>
    /// For each positive percent pattern (0-3) and every buffer size from 0 to one-less-than-needed,
    /// TryFormatPercent must return false without throwing.
    /// </summary>
    [TestMethod]
    [DataRow(0)]  // n %
    [DataRow(1)]  // n%
    [DataRow(2)]  // %n
    [DataRow(3)]  // % n
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
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, charsWritten);
        }
    }

    /// <summary>
    /// UTF-8 variant: for each negative percent pattern (0-11) and every buffer size from 0
    /// to one-less-than-needed, TryFormatPercent must return false without throwing.
    /// </summary>
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
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        // Now test every buffer size from 0 to requiredLength-1
        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, bytesWritten);
        }
    }

    /// <summary>
    /// UTF-8 variant: for each positive percent pattern (0-3) and every buffer size from 0
    /// to one-less-than-needed, TryFormatPercent must return false without throwing.
    /// </summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
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
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, bytesWritten);
        }
    }

    /// <summary>
    /// Use a multi-byte percent symbol and negative sign to stress the UTF-8 guards.
    /// Multi-byte characters expose bugs where byte-count and char-count are conflated.
    /// </summary>
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
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, bytesWritten);
        }
    }

    /// <summary>
    /// Use a multi-byte percent symbol for positive patterns.
    /// </summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
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
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, bytesWritten);
        }
    }

    /// <summary>
    /// Also test with a larger number that exercises group separators to hit more guards.
    /// </summary>
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
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, charsWritten);
        }
    }

    /// <summary>
    /// Large number positive patterns.
    /// </summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
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
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatPercent(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, charsWritten);
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
