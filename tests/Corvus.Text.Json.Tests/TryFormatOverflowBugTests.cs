// <copyright file="TryFormatOverflowBugTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that expose bugs in TryFormatPercent and TryFormatCurrency where unguarded
/// CopyTo calls throw instead of returning false when the buffer is too small.
/// A Try* method must NEVER throw — it must return false on insufficient buffer.
/// </summary>
public class TryFormatOverflowBugTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // PERCENT NEGATIVE — PREFIX BUGS
    // Patterns 0, 1, 2, 3, 4, 7, 9, 10 all have unguarded CopyTo at the start.
    // Buffer size 0 triggers the first CopyTo immediately.
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]  // -n %  → negSign.CopyTo first (line 455)
    [InlineData(1)]  // -n%   → negSign.CopyTo first (line 476)
    [InlineData(2)]  // -%n   → negSign.CopyTo first (line 496)
    [InlineData(3)]  // %-n   → percentSym.CopyTo first (line 510)
    [InlineData(4)]  // %n-   → percentSym.CopyTo first (line 524)
    [InlineData(7)]  // -% n  → negSign.CopyTo first (line 584)
    [InlineData(9)]  // % n-  → percentSym.CopyTo first (line 626)
    [InlineData(10)] // % -n  → percentSym.CopyTo first (line 653)
    public void TryFormatPercent_NegativePattern_EmptyBuffer_MustReturnFalse(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = Span<char>.Empty;
        NumberFormatInfo formatInfo = CreatePercentFormatInfo(pattern);

        bool result = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 2 (-%n): negSign fits (buffer=1) but percentSym.CopyTo throws at line 498.
    /// </summary>
    [Fact]
    public void TryFormatPercent_NegativePattern2_SecondPrefixCopyTo_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Buffer of 1: "-" fits, then "%" CopyTo into empty slice should return false, not throw
        Span<char> destination = stackalloc char[1];
        NumberFormatInfo formatInfo = CreatePercentFormatInfo(2);

        bool result = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 3 (%-n): percentSym fits (buffer=1) but negSign.CopyTo throws at line 512.
    /// </summary>
    [Fact]
    public void TryFormatPercent_NegativePattern3_SecondPrefixCopyTo_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Buffer of 1: "%" fits, then "-" CopyTo into empty slice should return false, not throw
        Span<char> destination = stackalloc char[1];
        NumberFormatInfo formatInfo = CreatePercentFormatInfo(3);

        bool result = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 7 (-% n): negSign fits (buffer=1) but percentSym.CopyTo throws at line 586.
    /// </summary>
    [Fact]
    public void TryFormatPercent_NegativePattern7_SecondPrefixCopyTo_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Buffer of 1: "-" fits, then "%" CopyTo should return false, not throw
        Span<char> destination = stackalloc char[1];
        NumberFormatInfo formatInfo = CreatePercentFormatInfo(7);

        bool result = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 10 (% -n): after space guard passes, negSign.CopyTo throws at line 662.
    /// Buffer of 3: "%" (1) + space check passes (pos+1=2, 2≤3) + space (pos=2) + "-" CopyTo into 1 char...
    /// Wait, "%" is 1, space guard checks pos+1>dest.Length → 2>3 → false. Space written at pos 2, pos=3.
    /// Then negSign "-" CopyTo into destination.Slice(3) = empty → throws.
    /// </summary>
    [Fact]
    public void TryFormatPercent_NegativePattern10_NegSignAfterSpace_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Buffer 3: "%" fits, space guard passes (2 ≤ 3), space written, then "-" CopyTo throws
        Span<char> destination = stackalloc char[3];
        NumberFormatInfo formatInfo = CreatePercentFormatInfo(10);

        bool result = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PERCENT POSITIVE — PREFIX BUGS
    // Patterns 2, 3 have unguarded percentSym.CopyTo at the start.
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(2)] // %n  → percentSym.CopyTo first (line 745)
    [InlineData(3)] // % n → percentSym.CopyTo first (line 757)
    public void TryFormatPercent_PositivePattern_EmptyBuffer_MustReturnFalse(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("0.5");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = Span<char>.Empty;
        NumberFormatInfo formatInfo = CreatePercentFormatInfo(positivePattern: pattern);

        bool result = JsonElementHelpers.TryFormatPercent(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CURRENCY NEGATIVE — PREFIX BUGS
    // Patterns with unguarded CopyTo before the number.
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1)]  // -$n   → negSign.CopyTo first (line 1467)
    [InlineData(2)]  // $-n   → currSym.CopyTo first (line 1481)
    [InlineData(3)]  // $n-   → currSym.CopyTo first (line 1495)
    [InlineData(5)]  // -n$   → negSign.CopyTo first (line 1535)
    [InlineData(8)]  // -n $  → negSign.CopyTo first (line 1577)
    [InlineData(9)]  // -$ n  → negSign.CopyTo first (line 1598)
    [InlineData(11)] // $ n-  → currSym.CopyTo first (line 1640)
    [InlineData(12)] // $ -n  → currSym.CopyTo first (line 1661)
    public void TryFormatCurrency_NegativePattern_EmptyBuffer_MustReturnFalse(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = Span<char>.Empty;
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: pattern);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 0 (($n)): has '(' guard, but then currSym.CopyTo at line 1448 is unguarded.
    /// Buffer 2 lets '(' succeed (guard passes: 0+1>2 → false), then "$" CopyTo into slice of 1.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern0_CurrSymAfterParen_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Buffer 2: '(' guard passes (0+1>2 → false), '(' written at pos 0, pos=1.
        // Then "$" CopyTo into destination.Slice(1) = 1 char — this actually fits for "$"!
        // Use a 2-char currency symbol to trigger the bug.
        Span<char> destination = stackalloc char[2];
        var formatInfo = CreateCurrencyFormatInfo(negativePattern: 0);
        formatInfo.CurrencySymbol = "$$"; // 2-char symbol won't fit in remaining 1 char

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 14 (($ n)): has '(' guard, then currSym.CopyTo at line 1710 is unguarded.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern14_CurrSymAfterParen_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Buffer 2: '(' written, then "$$" (2 chars) CopyTo into slice of 1 → throws
        Span<char> destination = stackalloc char[2];
        var formatInfo = CreateCurrencyFormatInfo(negativePattern: 14);
        formatInfo.CurrencySymbol = "$$";

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 1 (-$n): negSign fits (buffer=1) but currSym.CopyTo at line 1469 throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern1_SecondPrefix_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Buffer 1: "-" fits, then "$" CopyTo into empty slice → throws
        Span<char> destination = stackalloc char[1];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 1);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 2 ($-n): currSym fits (buffer=1) but negSign.CopyTo at line 1483 throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern2_SecondPrefix_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Buffer 1: "$" fits, then "-" CopyTo into empty slice → throws
        Span<char> destination = stackalloc char[1];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 2);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 9 (-$ n): negSign fits (buffer=1) but currSym.CopyTo at line 1600 throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern9_SecondPrefix_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[1];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 9);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CURRENCY NEGATIVE — SUFFIX BUGS
    // Patterns where number succeeds but suffix CopyTo throws.
    // Number "1,234.56" = 8 chars. Buffer = 8 makes number fill it exactly,
    // then suffix CopyTo into empty slice throws.
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(3)]  // $n-  → negSign.CopyTo at line 1504 after number
    [InlineData(6)]  // n-$  → negSign.CopyTo at line 1556 after number
    [InlineData(7)]  // n$-  → currSym.CopyTo at line 1570 after number
    public void TryFormatCurrency_NegativePattern_SuffixAfterNumber_MustReturnFalse(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // For patterns that start with currSym or negSign prefix + number:
        // Pattern 3 ($n-): prefix "$" (1) + number "1,234.56" (8) = 9, then "-" CopyTo → buf 9
        // Pattern 6 (n-$): number (8) + "-" CopyTo → buf 8
        // Pattern 7 (n$-): number (8) + "$" CopyTo → buf 8
        int bufferSize = pattern switch
        {
            3 => 9, // "$" prefix + 8 number chars = 9, suffix "-" doesn't fit
            6 => 8, // number only = 8, suffix "-" doesn't fit
            7 => 8, // number only = 8, suffix "$" doesn't fit
            _ => 8
        };

        Span<char> destination = stackalloc char[bufferSize];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: pattern);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 4 ((n$)): '(' + number (8) = 9 chars, then currSym.CopyTo at line 1523 throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern4_CurrSymAfterNumber_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // '(' (1) + number "1,234.56" (8) = 9, then "$" CopyTo into empty → throws
        Span<char> destination = stackalloc char[9];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 4);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 5 (-n$): "-" (1) + number (8) = 9, then "$" CopyTo at line 1544 throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern5_CurrSymSuffix_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // "-" (1) + number (8) = 9, then "$" into empty → throws
        Span<char> destination = stackalloc char[9];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 5);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 6 (n-$): number (8) + "-" (1) = 9, then "$" CopyTo at line 1558 throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern6_SecondSuffix_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // number (8) + "-" (1) = 9, then "$" CopyTo into empty → throws
        Span<char> destination = stackalloc char[9];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 6);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 7 (n$-): number (8) + "$" (1) = 9, then "-" CopyTo at line 1572 throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern7_SecondSuffix_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // number (8) + "$" (1) = 9, then "-" CopyTo into empty → throws
        Span<char> destination = stackalloc char[9];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 7);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 8 (-n $): "-" (1) + number (8) + space guard passes at buf 11,
    /// but at buffer 10: space guard = pos + 1 > 10 → 10 > 10 → false → space written, pos = 10,
    /// then "$" CopyTo at line 1593 into empty → throws.
    /// Actually for buffer 10: prefix "-" (1) + number (8) = 9, pos=9,
    /// space guard: 9+1>10 → 10>10 → false, space at pos 9, pos=10.
    /// "$" CopyTo into Slice(10) of length 10 = empty → throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern8_CurrSymAfterSpace_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // "-" (1) + number (8) = 9, space guard 9+1>10 → false, space written pos=10,
        // then "$" CopyTo into empty → throws
        Span<char> destination = stackalloc char[10];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 8);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 10 (n $-): number (8) + space guard passes → space (pos=9),
    /// then "$" (1) CopyTo + "-" (1) CopyTo at lines 1633-1635.
    /// The space guard only checks pos + 1, not the full suffix.
    /// Buffer 10: number (8), space guard 8+1>10 → false, space at pos 9 → pos=9... 
    /// wait, that's wrong. Let me re-read: guard is pos+1>dest.Length → 8+1>10 → 9>10 → false.
    /// Space written, pos=9. "$" CopyTo into Slice(9) = 1 char → fits! pos=10.
    /// "-" CopyTo into Slice(10) = empty → throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern10_NegSignAfterCurrSym_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // number (8) + space (pos=9) + "$" (pos=10) + "-" into empty → throws
        Span<char> destination = stackalloc char[10];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 10);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 11 ($ n-): currSym (1) + space guard passes, space written, number (8) succeeds,
    /// then negSign.CopyTo at line 1656. Buffer 11: "$" (1) + space guard (1+1>11 → false) +
    /// space (pos=2) + number (8, pos=10) + "-" CopyTo into Slice(10) = 1 char → fits for 1-char sign.
    /// Use buffer 10 instead: pos after number = 10, Slice(10) = empty.
    /// "$" (1) + space (1) + number needs Slice(2) = 8 chars → exactly 10. pos=10. 
    /// Then "-" into Slice(10) of buffer 10 = empty → throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern11_NegSignSuffix_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // "$" (1) + space guard (1+1>10 → false) + space (pos=2) + number into Slice(2)=8 chars 
        // → number "1,234.56" (8 chars) fits exactly, pos=10. Then "-" into Slice(10) = empty.
        Span<char> destination = stackalloc char[10];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 11);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 12 ($ -n): currSym (1) + space guard + space + negSign.CopyTo at line 1670.
    /// Buffer 3: "$" (1) + space guard (1+1>3 → false) + space (pos=2) + "-" CopyTo into Slice(2)=1 → fits.
    /// Buffer 2: "$" (1) + space guard (1+1>2 → 2>2 → false) + space at pos 1, pos=2.
    /// "-" CopyTo into Slice(2) of buffer 2 = empty → throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern12_NegSignAfterSpace_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // Wait: space guard checks pos+1>dest.Length. pos=1 (after "$"), 1+1>2 → 2>2 → false!
        // Space written at pos 1, pos=2. "-" CopyTo into Slice(2) of length-2 buffer = empty. Throws!
        // Hmm actually 1+1>2 → 2>2 is FALSE, so guard does NOT fire. Let me recalculate.
        // pos=1 after "$". Guard: pos+1 > destination.Length → 1+1 > 2 → 2 > 2 → FALSE. 
        // So space IS written at pos 1, pos becomes 2. 
        // Then "-" CopyTo into destination.Slice(2) which for buffer of 2 is Slice(2..2) = empty. Throws!
        Span<char> destination = stackalloc char[2];
        var formatInfo = CreateCurrencyFormatInfo(negativePattern: 12);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 13 (n- $): number (8) + negSign.CopyTo at line 1689 throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern13_NegSignAfterNumber_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // number (8) fills the buffer exactly, then "-" CopyTo into empty → throws
        Span<char> destination = stackalloc char[8];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 13);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 15 ((n $)): '(' + number (8) + space guard passes + space + currSym.CopyTo at line 1757.
    /// Buffer 11: '(' (1) + number (8, pos=9) + space guard (9+1>11 → false) + space (pos=10) +
    /// "$" CopyTo into Slice(10) = 1 char → fits for "$". Use "$$" to trigger.
    /// Actually simpler: buffer 10: '(' (1) + number needs Slice(1)=9 for 8 chars... 
    /// Let me think differently. Use buffer 10:
    /// '(' guard: 0+1>10 → false. '(' written, pos=1.
    /// Number into Slice(1) = 9 chars. "1,234.56" = 8 chars → succeeds. pos=9.
    /// Space guard: 9+1>10 → 10>10 → false. Space written, pos=10.
    /// "$" CopyTo into Slice(10) of buffer 10 = empty → throws!
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern15_CurrSymAfterSpace_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = stackalloc char[10];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 15);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 13 (n- $): number (8) + "-" (1) + space guard passes + space + currSym.CopyTo at line 1698.
    /// Buffer 10: number (8) + "-" CopyTo (line 1689) at pos 8 into Slice(8) = 2 chars → fits for "-" (1).
    /// pos=9. Space guard: 9+1>10 → 10>10 → false. Space at pos 9, pos=10.
    /// "$" CopyTo into Slice(10) = empty → throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_NegativePattern13_CurrSymAfterSpace_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("-1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // number (8) + "-" at pos 8 (fits in buf 10) + space guard passes + space (pos=10)
        // + "$" CopyTo into empty → throws
        Span<char> destination = stackalloc char[10];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(negativePattern: 13);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CURRENCY POSITIVE — BUGS
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)] // $n  → currSym.CopyTo first (line 1779)
    [InlineData(2)] // $ n → currSym.CopyTo first (line 1803)
    public void TryFormatCurrency_PositivePattern_EmptyBuffer_MustReturnFalse(int pattern)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        Span<char> destination = Span<char>.Empty;
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(positivePattern: pattern);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 1 (n$): number (8) + currSym.CopyTo at line 1798.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_PositivePattern1_CurrSymSuffix_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // number "1,234.56" (8) fills buffer exactly, then "$" CopyTo into empty → throws
        Span<char> destination = stackalloc char[8];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(positivePattern: 1);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    /// <summary>
    /// Pattern 3 (n $): number (8) + space guard passes + space + currSym.CopyTo at line 1836.
    /// Buffer 9: number (8, pos=8). Space guard: 8+1>9 → 9>9 → false. Space at pos 8, pos=9.
    /// "$" CopyTo into Slice(9) of buffer 9 = empty → throws.
    /// </summary>
    [Fact]
    public void TryFormatCurrency_PositivePattern3_CurrSymAfterSpace_MustReturnFalse()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("1234.56");
        JsonElementHelpers.ParseNumber(
            utf8,
            out bool isNegative,
            out ReadOnlySpan<byte> integral,
            out ReadOnlySpan<byte> fractional,
            out int exponent);

        // number (8) + space guard passes (8+1>9 → false) + space (pos=9) + "$" into empty
        Span<char> destination = stackalloc char[9];
        NumberFormatInfo formatInfo = CreateCurrencyFormatInfo(positivePattern: 3);

        bool result = JsonElementHelpers.TryFormatCurrency(
            isNegative, integral, fractional, exponent,
            destination, out int charsWritten, 2, formatInfo);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private static NumberFormatInfo CreatePercentFormatInfo(int negativePattern = 0, int positivePattern = 0)
    {
        return new NumberFormatInfo
        {
            PercentSymbol = "%",
            PercentGroupSeparator = ",",
            PercentDecimalSeparator = ".",
            PercentGroupSizes = [3],
            PercentNegativePattern = negativePattern,
            PercentPositivePattern = positivePattern,
            NegativeSign = "-",
            PercentDecimalDigits = 2
        };
    }

    private static NumberFormatInfo CreateCurrencyFormatInfo(int negativePattern = 1, int positivePattern = 0)
    {
        return new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            CurrencyGroupSizes = [3],
            CurrencyNegativePattern = negativePattern,
            CurrencyPositivePattern = positivePattern,
            NegativeSign = "-",
            CurrencyDecimalDigits = 2
        };
    }
}
