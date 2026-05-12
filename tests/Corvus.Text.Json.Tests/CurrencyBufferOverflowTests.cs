// <copyright file="CurrencyBufferOverflowTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Systematically tests that TryFormatCurrency (both char and UTF-8 variants) never throws
/// for ANY buffer size smaller than the required output. Every negative currency pattern (0-15)
/// and positive currency pattern (0-3) is tested with every buffer size from 0 to requiredLength-1.
/// This exercises all intermediate buffer guards — the exact paths where we've found bugs.
/// </summary>
[TestClass]
public class CurrencyBufferOverflowTests
{
    /// <summary>
    /// For each negative currency pattern (0-15) and every buffer size from 0 to one-less-than-needed,
    /// TryFormatCurrency must return false without throwing.
    /// </summary>
    [TestMethod]
    [DataRow(0)]   // ($n)
    [DataRow(1)]   // -$n
    [DataRow(2)]   // $-n
    [DataRow(3)]   // $n-
    [DataRow(4)]   // (n$)
    [DataRow(5)]   // -n$
    [DataRow(6)]   // n-$
    [DataRow(7)]   // n$-
    [DataRow(8)]   // -n $
    [DataRow(9)]   // -$ n
    [DataRow(10)]  // n $-
    [DataRow(11)]  // $ n-
    [DataRow(12)]  // $ -n
    [DataRow(13)]  // n- $
    [DataRow(14)]  // ($ n)
    [DataRow(15)]  // (n $)
    public void TryFormatCurrency_Char_NegativePattern_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(pattern, isNegative: true);

        Span<char> largeBuf = stackalloc char[64];
        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatCurrency(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, charsWritten);
        }
    }

    /// <summary>
    /// For each positive currency pattern (0-3) and every buffer size from 0 to one-less-than-needed,
    /// TryFormatCurrency must return false without throwing.
    /// </summary>
    [TestMethod]
    [DataRow(0)]   // $n
    [DataRow(1)]   // n$
    [DataRow(2)]   // $ n
    [DataRow(3)]   // n $
    public void TryFormatCurrency_Char_PositivePattern_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(pattern, isNegative: false);

        Span<char> largeBuf = stackalloc char[64];
        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatCurrency(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, charsWritten);
        }
    }

    /// <summary>
    /// UTF-8 variant: for each negative currency pattern (0-15) and every buffer size from 0
    /// to one-less-than-needed, TryFormatCurrency must return false without throwing.
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
    [DataRow(12)]
    [DataRow(13)]
    [DataRow(14)]
    [DataRow(15)]
    public void TryFormatCurrency_Utf8_NegativePattern_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(pattern, isNegative: true);

        Span<byte> largeBuf = stackalloc byte[64];
        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatCurrency(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, bytesWritten);
        }
    }

    /// <summary>
    /// UTF-8 variant: for each positive currency pattern (0-3) and every buffer size from 0
    /// to one-less-than-needed, TryFormatCurrency must return false without throwing.
    /// </summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void TryFormatCurrency_Utf8_PositivePattern_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(pattern, isNegative: false);

        Span<byte> largeBuf = stackalloc byte[64];
        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatCurrency(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, bytesWritten);
        }
    }

    /// <summary>
    /// Use multi-byte currency symbol and negative sign to stress the UTF-8 guards.
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
    [DataRow(12)]
    [DataRow(13)]
    [DataRow(14)]
    [DataRow(15)]
    public void TryFormatCurrency_Utf8_NegativePattern_MultiByteSymbols_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Use multi-byte symbols: "€" (3 bytes UTF-8) and "−" (3 bytes UTF-8)
        NumberFormatInfo formatInfo = CreateMultiByteCurrencyFormatInfo(pattern, isNegative: true);

        Span<byte> largeBuf = stackalloc byte[128];
        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatCurrency(
                isNegative, integral, fractional, exponent,
                destination, out int bytesWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, bytesWritten);
        }
    }

    /// <summary>
    /// Larger number to exercise group separators within currency formatting.
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
    [DataRow(12)]
    [DataRow(13)]
    [DataRow(14)]
    [DataRow(15)]
    public void TryFormatCurrency_Char_NegativePattern_LargeNumber_AllBufferSizes_NeverThrows(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-12345.678");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(pattern, isNegative: true);

        Span<char> largeBuf = stackalloc char[64];
        bool success = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            largeBuf, out int requiredLength, 2, formatInfo);
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatCurrency(
                isNegative, integral, fractional, exponent,
                destination, out int charsWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, charsWritten);
        }
    }

    private static NumberFormatInfo CreateCurrencyFormatInfo(int pattern, bool isNegative)
    {
        var nfi = new NumberFormatInfo
        {
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyGroupSizes = [3],
            CurrencySymbol = "$",
            NegativeSign = "-",
        };

        if (isNegative)
        {
            nfi.CurrencyNegativePattern = pattern;
        }
        else
        {
            nfi.CurrencyPositivePattern = pattern;
        }

        return nfi;
    }

    private static NumberFormatInfo CreateMultiByteCurrencyFormatInfo(int pattern, bool isNegative)
    {
        var nfi = new NumberFormatInfo
        {
            CurrencyDecimalDigits = 2,
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSeparator = ",",
            CurrencyGroupSizes = [3],
            CurrencySymbol = "\u20AC",  // Euro sign — 3 bytes in UTF-8
            NegativeSign = "\u2212",    // Minus sign — 3 bytes in UTF-8
        };

        if (isNegative)
        {
            nfi.CurrencyNegativePattern = pattern;
        }
        else
        {
            nfi.CurrencyPositivePattern = pattern;
        }

        return nfi;
    }

    /// <summary>
    /// For each positive currency pattern (0-3) formatting zero, every buffer size
    /// from 0 to one-less-than-needed must return false without throwing.
    /// This exercises the TryFormatZeroCurrency overflow guards.
    /// </summary>
    [TestMethod]
    [DataRow(0)]   // $n
    [DataRow(1)]   // n$
    [DataRow(2)]   // $ n
    [DataRow(3)]   // n $
    public void TryFormatZeroCurrency_Char_AllPatterns_AllBufferSizes_NeverThrows(int pattern)
    {
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(pattern, isNegative: false);

        Span<char> largeBuf = stackalloc char[64];
        bool success = JsonElementHelpers.TryFormatZeroCurrency(largeBuf, out int requiredLength, 2, formatInfo);
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatZeroCurrency(destination, out int charsWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, charsWritten);
        }
    }

    /// <summary>
    /// UTF-8 variant: for each positive currency pattern (0-3) formatting zero,
    /// every buffer size from 0 to one-less-than-needed must return false without throwing.
    /// </summary>
    [TestMethod]
    [DataRow(0)]   // $n
    [DataRow(1)]   // n$
    [DataRow(2)]   // $ n
    [DataRow(3)]   // n $
    public void TryFormatZeroCurrency_Utf8_AllPatterns_AllBufferSizes_NeverThrows(int pattern)
    {
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(pattern, isNegative: false);

        Span<byte> largeBuf = stackalloc byte[64];
        bool success = JsonElementHelpers.TryFormatZeroCurrency(largeBuf, out int requiredLength, 2, formatInfo);
        Assert.IsTrue(success, $"Pattern {pattern} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatZeroCurrency(destination, out int bytesWritten, 2, formatInfo);

            Assert.IsFalse(result, $"Pattern {pattern}, bufSize {bufSize}: expected false, got true (requiredLength={requiredLength})");
            Assert.AreEqual(0, bytesWritten);
        }
    }

    /// <summary>
    /// TryFormatExponential with zero input and various buffer sizes.
    /// Exercises the totalLength==0 special-case overflow guard.
    /// </summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(6)]
    public void TryFormatExponential_Char_Zero_AllBufferSizes_NeverThrows(int precision)
    {
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        Span<char> largeBuf = stackalloc char[64];
        bool success = JsonElementHelpers.TryFormatExponential(
            false, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, 0,
            largeBuf, out int requiredLength, precision, 'e', formatInfo);
        Assert.IsTrue(success, $"Precision {precision} failed even with large buffer");

        Span<char> pool = stackalloc char[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<char> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatExponential(
                false, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, 0,
                destination, out int charsWritten, precision, 'e', formatInfo);

            Assert.IsFalse(result, $"Precision {precision}, bufSize {bufSize}: expected false");
            Assert.AreEqual(0, charsWritten);
        }
    }

    /// <summary>
    /// UTF-8 variant: TryFormatExponential with zero input and various buffer sizes.
    /// </summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(6)]
    public void TryFormatExponential_Utf8_Zero_AllBufferSizes_NeverThrows(int precision)
    {
        NumberFormatInfo formatInfo = NumberFormatInfo.InvariantInfo;

        Span<byte> largeBuf = stackalloc byte[64];
        bool success = JsonElementHelpers.TryFormatExponential(
            false, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, 0,
            largeBuf, out int requiredLength, precision, 'e', formatInfo);
        Assert.IsTrue(success, $"Precision {precision} failed even with large buffer");

        Span<byte> pool = stackalloc byte[requiredLength];
        for (int bufSize = 0; bufSize < requiredLength; bufSize++)
        {
            Span<byte> destination = pool.Slice(0, bufSize);

            bool result = JsonElementHelpers.TryFormatExponential(
                false, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, 0,
                destination, out int bytesWritten, precision, 'e', formatInfo);

            Assert.IsFalse(result, $"Precision {precision}, bufSize {bufSize}: expected false");
            Assert.AreEqual(0, bytesWritten);
        }
    }
}
