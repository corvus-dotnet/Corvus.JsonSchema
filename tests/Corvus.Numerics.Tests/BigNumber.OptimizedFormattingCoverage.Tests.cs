// <copyright file="BigNumber.OptimizedFormattingCoverage.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Additional tests for TryFormatOptimized to achieve better branch coverage.
/// </summary>
public class BigNumberOptimizedFormattingCoverageTests
{
    [Fact]
    public void TryFormatOptimized_WithNullProvider_UsesInvariant()
    {
        BigNumber num = new(12345, -2);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "F2", null);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("123.45");
    }

    [Fact]
    public void TryFormatOptimized_WithInvariantCulture_UsesFastPath()
    {
        BigNumber num = new(12345, -2);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("123.45");
    }

    [Fact]
    public void TryFormatOptimized_WithCustomCulture_UsesCulturePath()
    {
        BigNumber num = new(12345, -2);
        Span<char> buffer = stackalloc char[64];
        var culture = new CultureInfo("fr-FR");

        bool success = num.TryFormatOptimized(buffer, out int written, "F2", culture);

        success.ShouldBeTrue();
        // French culture uses comma as decimal separator
        buffer.Slice(0, written).ToString().ShouldBe("123,45");
    }

    [Fact]
    public void TryFormatOptimized_ZeroWithEmptyFormat_InvariantCulture()
    {
        BigNumber zero = BigNumber.Zero;
        Span<char> buffer = stackalloc char[64];

        bool success = zero.TryFormatOptimized(buffer, out int written, "", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("0");
    }

    [Fact]
    public void TryFormatOptimized_ZeroWithFormat_InvariantCulture()
    {
        BigNumber zero = BigNumber.Zero;
        Span<char> buffer = stackalloc char[64];

        bool success = zero.TryFormatOptimized(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("0.00");
    }

    [Fact]
    public void TryFormatOptimized_ZeroWithCustomCulture()
    {
        BigNumber zero = BigNumber.Zero;
        Span<char> buffer = stackalloc char[64];
        var culture = new CultureInfo("de-DE");

        bool success = zero.TryFormatOptimized(buffer, out int written, "F2", culture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("0,00");
    }

    [Fact]
    public void TryFormatOptimized_EmptyFormat_InvariantCulture()
    {
        BigNumber num = new(12345, -2);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("12345E-2");
    }

    [Fact]
    public void TryFormatOptimized_EmptyFormat_CustomCulture()
    {
        BigNumber num = new(12345, -2);
        Span<char> buffer = stackalloc char[64];
        var culture = new CultureInfo("en-US");

        bool success = num.TryFormatOptimized(buffer, out int written, "", culture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("12345E-2");
    }

    [Fact]
    public void TryFormatOptimized_SingleDigitPrecision_FixedPoint()
    {
        BigNumber num = new(123456, -3);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "F3", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("123.456");
    }

    [Fact]
    public void TryFormatOptimized_SingleDigitPrecision_Exponential()
    {
        BigNumber num = new(12345, 0);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "E4", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("1.2345E+004");
    }

    [Fact]
    public void TryFormatOptimized_SingleDigitPrecision_ExponentialLowerCase()
    {
        BigNumber num = new(12345, 0);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "e4", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("1.2345e+004");
    }

    [Fact]
    public void TryFormatOptimized_SingleDigitPrecision_Number()
    {
        BigNumber num = new(123456, -2);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "N2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("1,234.56");
    }

    [Fact]
    public void TryFormatOptimized_SingleDigitPrecision_General()
    {
        BigNumber num = new(12345, 10);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "G5", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("1.2345E+14");
    }

    [Fact]
    public void TryFormatOptimized_SingleDigitPrecision_GeneralLowerCase()
    {
        BigNumber num = new(12345, 10);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "g5", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("1.2345e+14");
    }

    [Fact]
    public void TryFormatOptimized_SingleDigitPrecision_Currency()
    {
        BigNumber num = new(12345, -2);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "C2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("¤123.45");
    }

    [Fact]
    public void TryFormatOptimized_SingleDigitPrecision_Percent()
    {
        BigNumber num = new(12345, -4); // 1.2345
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "P2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("123.45 %");
    }

    [Fact]
    public void TryFormatOptimized_SingleDigitPrecision_UnknownFormat_FallsBackToGeneral()
    {
        BigNumber num = new(12345, -2);
        Span<char> buffer = stackalloc char[64];

        // Unknown format 'X' with single digit
        bool success = num.TryFormatOptimized(buffer, out int written, "X5", CultureInfo.InvariantCulture);

        // Should fall back to general path and throw or return false
        // Based on implementation, it should use general path
    }

    [Fact]
    public void TryFormatOptimized_MultiCharFormat_UsesGeneralPath()
    {
        BigNumber num = new(12345, -2);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "F10", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("123.4500000000");
    }

    [Fact]
    public void TryFormatOptimized_NonDigitSecondChar_UsesGeneralPath()
    {
        BigNumber num = new(12345, -2);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, "FA", CultureInfo.InvariantCulture);

        // Should fall back to general path which will throw FormatException or return false
    }

    [Fact]
    public void TryFormatOptimized_BufferTooSmall_ReturnsFalse()
    {
        BigNumber num = new(1234567890, 0);
        Span<char> buffer = stackalloc char[5]; // Too small

        bool success = num.TryFormatOptimized(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void TryFormatOptimized_CustomCulture_AllFormats()
    {
        BigNumber num = new(12345, -2);
        var culture = new CultureInfo("de-DE");
        Span<char> buffer = stackalloc char[128];

        // Test each format type with custom culture
        bool success = num.TryFormatOptimized(buffer, out int written, "F2", culture);
        success.ShouldBeTrue();

        success = num.TryFormatOptimized(buffer, out written, "N2", culture);
        success.ShouldBeTrue();

        success = num.TryFormatOptimized(buffer, out written, "E2", culture);
        success.ShouldBeTrue();

        success = num.TryFormatOptimized(buffer, out written, "C2", culture);
        success.ShouldBeTrue();

        success = num.TryFormatOptimized(buffer, out written, "P2", culture);
        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormatOptimized_NegativeNumber_AllFormats()
    {
        BigNumber num = new(-12345, -2);
        Span<char> buffer = stackalloc char[128];

        // Test each format type with negative number
        bool success = num.TryFormatOptimized(buffer, out int written, "F2", CultureInfo.InvariantCulture);
        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("-123.45");

        success = num.TryFormatOptimized(buffer, out written, "N2", CultureInfo.InvariantCulture);
        success.ShouldBeTrue();

        success = num.TryFormatOptimized(buffer, out written, "E2", CultureInfo.InvariantCulture);
        success.ShouldBeTrue();

        success = num.TryFormatOptimized(buffer, out written, "C2", CultureInfo.InvariantCulture);
        success.ShouldBeTrue();

        success = num.TryFormatOptimized(buffer, out written, "P2", CultureInfo.InvariantCulture);
        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormatOptimized_VeryLargeNumber_WorksCorrectly()
    {
        BigNumber num = new(123456789, 100);
        Span<char> buffer = stackalloc char[256];

        bool success = num.TryFormatOptimized(buffer, out int written, "E2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("1.23E+108");
    }

    [Fact]
    public void TryFormatOptimized_RawFormat_Zero_BufferTooSmall()
    {
        BigNumber zero = BigNumber.Zero;
        Span<char> buffer = Span<char>.Empty;

        bool success = zero.TryFormatOptimized(buffer, out int written, ReadOnlySpan<char>.Empty, null);

        success.ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void TryFormatOptimized_RawFormat_ZeroExponent_BufferTooSmall()
    {
        BigNumber num = new(1234567890, 0);
        Span<char> buffer = stackalloc char[3]; // Too small for "1234567890"

        bool success = num.TryFormatOptimized(buffer, out int written, ReadOnlySpan<char>.Empty, null);

        success.ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void TryFormatOptimized_RawFormat_WithExponent_SigFitsButExponentDoesNot()
    {
        // Significand "1" fits but "E" doesn't
        BigNumber num = new(1, 5);
        Span<char> buffer = stackalloc char[1]; // Only room for "1", not "1E5"

        bool success = num.TryFormatOptimized(buffer, out int written, ReadOnlySpan<char>.Empty, null);

        success.ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void TryFormatOptimized_RawFormat_ExponentTruncated()
    {
        // Significand "1" + "E" fit but exponent "12345" doesn't
        BigNumber num = new(1, 12345);
        Span<char> buffer = stackalloc char[3]; // Room for "1E" but not "1E12345"

        bool success = num.TryFormatOptimized(buffer, out int written, ReadOnlySpan<char>.Empty, null);

        success.ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void TryFormatOptimized_Format_SignificandTooLargeForBuffer()
    {
        // Force the TryFormatBigIntegerUtf8 to fail because significand is larger than buffer
        BigNumber num = new(1234567890, 0);
        Span<char> buffer = stackalloc char[3]; // Too small

        bool success = num.TryFormatOptimized(buffer, out int written, "F0", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void TryFormatOptimized_VerySmallNumber_WorksCorrectly()
    {
        BigNumber num = new(123456789, -100);
        Span<char> buffer = stackalloc char[256];

        bool success = num.TryFormatOptimized(buffer, out int written, "E2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("1.23E-092");
    }

#if NET
    #region TryFormatUtf8Optimized (byte span with format)

    [Fact]
    public void TryFormatUtf8Optimized_WithFormat_Succeeds()
    {
        BigNumber value = new(12345, -2); // 123.45
        Span<byte> dest = stackalloc byte[64];
        bool success = value.TryFormatUtf8Optimized(dest, out int bytesWritten, "F2", CultureInfo.InvariantCulture);
        success.ShouldBeTrue();
        System.Text.Encoding.UTF8.GetString(dest.Slice(0, bytesWritten)).ShouldBe("123.45");
    }

    [Fact]
    public void TryFormatUtf8Optimized_NegativeValue_WithFormat()
    {
        BigNumber value = new(-12345, -2); // -123.45
        Span<byte> dest = stackalloc byte[64];
        bool success = value.TryFormatUtf8Optimized(dest, out int bytesWritten, "F2", CultureInfo.InvariantCulture);
        success.ShouldBeTrue();
        System.Text.Encoding.UTF8.GetString(dest.Slice(0, bytesWritten)).ShouldBe("-123.45");
    }

    [Fact]
    public void TryFormatUtf8Optimized_WithFormat_BufferTooSmall()
    {
        BigNumber value = new(12345, -2); // 123.45
        Span<byte> dest = stackalloc byte[2]; // Too small
        bool success = value.TryFormatUtf8Optimized(dest, out int bytesWritten, "F2", CultureInfo.InvariantCulture);
        success.ShouldBeFalse();
        bytesWritten.ShouldBe(0);
    }

    [Fact]
    public void TryFormatUtf8Optimized_ZeroWithFormat()
    {
        BigNumber value = BigNumber.Zero;
        Span<byte> dest = stackalloc byte[64];
        bool success = value.TryFormatUtf8Optimized(dest, out int bytesWritten, "F2", CultureInfo.InvariantCulture);
        success.ShouldBeTrue();
        System.Text.Encoding.UTF8.GetString(dest.Slice(0, bytesWritten)).ShouldBe("0.00");
    }

    [Fact]
    public void TryFormatUtf8Optimized_EmptyFormat_WithExponent()
    {
        BigNumber value = new(123, 5);
        Span<byte> dest = stackalloc byte[64];
        bool success = value.TryFormatUtf8Optimized(dest, out int bytesWritten, ReadOnlySpan<char>.Empty, null);
        success.ShouldBeTrue();
        System.Text.Encoding.UTF8.GetString(dest.Slice(0, bytesWritten)).ShouldBe("123E5");
    }

    [Fact]
    public void TryFormatUtf8Optimized_EmptyFormat_Zero()
    {
        BigNumber value = BigNumber.Zero;
        Span<byte> dest = stackalloc byte[64];
        bool success = value.TryFormatUtf8Optimized(dest, out int bytesWritten, ReadOnlySpan<char>.Empty, null);
        success.ShouldBeTrue();
        System.Text.Encoding.UTF8.GetString(dest.Slice(0, bytesWritten)).ShouldBe("0");
    }

    [Fact]
    public void TryFormatUtf8Optimized_EmptyFormat_Zero_BufferTooSmall()
    {
        BigNumber value = BigNumber.Zero;
        Span<byte> dest = Span<byte>.Empty;
        bool success = value.TryFormatUtf8Optimized(dest, out int bytesWritten, ReadOnlySpan<char>.Empty, null);
        success.ShouldBeFalse();
        bytesWritten.ShouldBe(0);
    }

    [Fact]
    public void TryFormatUtf8Optimized_EmptyFormat_WithExponent_BufferTooSmall()
    {
        BigNumber value = new(12345, 10); // "12345E10"
        Span<byte> dest = stackalloc byte[5]; // Too small
        bool success = value.TryFormatUtf8Optimized(dest, out int bytesWritten, ReadOnlySpan<char>.Empty, null);
        success.ShouldBeFalse();
        bytesWritten.ShouldBe(0);
    }

    [Fact]
    public void TryFormatUtf8Optimized_EmptyFormat_ZeroExponent()
    {
        BigNumber value = new(42, 0);
        Span<byte> dest = stackalloc byte[64];
        bool success = value.TryFormatUtf8Optimized(dest, out int bytesWritten, ReadOnlySpan<char>.Empty, null);
        success.ShouldBeTrue();
        System.Text.Encoding.UTF8.GetString(dest.Slice(0, bytesWritten)).ShouldBe("42");
    }

    [Fact]
    public void TryFormatUtf8Optimized_AllFormats()
    {
        BigNumber value = new(12345, -2);
        Span<byte> dest = stackalloc byte[128];

        value.TryFormatUtf8Optimized(dest, out _, "N2", CultureInfo.InvariantCulture).ShouldBeTrue();
        value.TryFormatUtf8Optimized(dest, out _, "E2", CultureInfo.InvariantCulture).ShouldBeTrue();
        value.TryFormatUtf8Optimized(dest, out _, "C2", CultureInfo.InvariantCulture).ShouldBeTrue();
        value.TryFormatUtf8Optimized(dest, out _, "P2", CultureInfo.InvariantCulture).ShouldBeTrue();
        value.TryFormatUtf8Optimized(dest, out _, "G5", CultureInfo.InvariantCulture).ShouldBeTrue();
    }

    #endregion

    #region TryParseJsonUtf8

    [Fact]
    public void TryParseJsonUtf8_EmptyInput_ReturnsFalse()
    {
        bool success = BigNumber.TryParseJsonUtf8(ReadOnlySpan<byte>.Empty, out BigNumber result);
        success.ShouldBeFalse();
        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void TryParseJsonUtf8_SingleZero()
    {
        ReadOnlySpan<byte> input = "0"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void TryParseJsonUtf8_SimpleInteger()
    {
        ReadOnlySpan<byte> input = "12345"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        result.ToString().ShouldBe("12345");
    }

    [Fact]
    public void TryParseJsonUtf8_NegativeValue()
    {
        ReadOnlySpan<byte> input = "-456"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        result.ToString().ShouldBe("-456");
    }

    [Fact]
    public void TryParseJsonUtf8_WithExponent()
    {
        ReadOnlySpan<byte> input = "123E5"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        result.ToString().ShouldBe("123E5");
    }

    [Fact]
    public void TryParseJsonUtf8_WithNegativeExponent()
    {
        ReadOnlySpan<byte> input = "123E-3"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        result.ToString().ShouldBe("123E-3");
    }

    [Fact]
    public void TryParseJsonUtf8_WithDecimal()
    {
        ReadOnlySpan<byte> input = "123.45"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        string formatted = result.ToString("F2", CultureInfo.InvariantCulture);
        formatted.ShouldBe("123.45");
    }

    [Fact]
    public void TryParseJsonUtf8_InvalidInput()
    {
        ReadOnlySpan<byte> input = "abc"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber _);
        success.ShouldBeFalse();
    }

    [Fact]
    public void TryParseJsonUtf8_NegativeZero()
    {
        ReadOnlySpan<byte> input = "-0"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void TryParseJsonUtf8_DecimalWithExponent()
    {
        ReadOnlySpan<byte> input = "1.5E2"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        // 1.5E2 = 150
        result.ToString("F0", CultureInfo.InvariantCulture).ShouldBe("150");
    }

    [Fact]
    public void TryParseJsonUtf8_LeadingZeroDecimal()
    {
        ReadOnlySpan<byte> input = "0.001"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        result.ToString("F3", CultureInfo.InvariantCulture).ShouldBe("0.001");
    }

    [Fact]
    public void TryParseJsonUtf8_LeadingWhitespace()
    {
        ReadOnlySpan<byte> input = "  123"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        result.ToString().ShouldBe("123");
    }

    [Fact]
    public void TryParseJsonUtf8_AllWhitespace()
    {
        ReadOnlySpan<byte> input = "   "u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeFalse();
    }

    [Fact]
    public void TryParseJsonUtf8_PlusSign()
    {
        ReadOnlySpan<byte> input = "+42"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeTrue();
        result.ToString().ShouldBe("42");
    }

    [Fact]
    public void TryParseJsonUtf8_SignOnly()
    {
        // Just a minus sign with no digits
        ReadOnlySpan<byte> input = "-"u8;
        bool success = BigNumber.TryParseJsonUtf8(input, out BigNumber result);
        success.ShouldBeFalse();
    }

    #endregion
#endif
}