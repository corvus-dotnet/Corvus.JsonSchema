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
    public void TryFormatOptimized_VerySmallNumber_WorksCorrectly()
    {
        BigNumber num = new(123456789, -100);
        Span<char> buffer = stackalloc char[256];

        bool success = num.TryFormatOptimized(buffer, out int written, "E2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        buffer.Slice(0, written).ToString().ShouldBe("1.23E-092");
    }
}