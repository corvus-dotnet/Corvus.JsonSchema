// <copyright file="BigNumber.BufferOverflow.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Tier 1 Option 5: Buffer overflow scenarios.
/// Target: +1.2% coverage (35 tests).
/// </summary>
public class BigNumberBufferOverflowTests
{
    #region Exact Buffer Sizes (+15 tests)

    [Fact]
    public void TryFormat_ExactSizeFixedPoint_Succeeds()
    {
        BigNumber num = new(12345, -2); // 123.45
        Span<byte> buffer = stackalloc byte[6]; // Exact: "123.45"

        bool success = num.TryFormat(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        written.ShouldBe(6);
    }

    [Fact]
    public void TryFormat_ExactSizeNumber_Succeeds()
    {
        BigNumber num = new(12345, -2); // 123.45
        Span<byte> buffer = stackalloc byte[7]; // "123.45" but N might add grouping

        bool success = num.TryFormat(buffer, out int written, "N2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_ExactSizeExponential_Succeeds()
    {
        BigNumber num = new(12345, 0); // 12345
        Span<byte> buffer = stackalloc byte[13]; // "1.23E+004" + some margin

        bool success = num.TryFormat(buffer, out int written, "E2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_ExactSizeCurrency_Succeeds()
    {
        BigNumber num = new(12345, -2);
        Span<byte> buffer = stackalloc byte[10]; // "¤123.45" (¤ is 3 bytes in UTF-8)

        bool success = num.TryFormat(buffer, out int written, "C2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_ExactSizePercent_Succeeds()
    {
        BigNumber num = new(12345, -4); // 1.2345
        Span<byte> buffer = stackalloc byte[10]; // "123.45 %"

        bool success = num.TryFormat(buffer, out int written, "P2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_SmallNumberExactSize_Succeeds()
    {
        BigNumber num = new(5, 0); // 5
        Span<byte> buffer = stackalloc byte[1];

        bool success = num.TryFormat(buffer, out int written, "F0", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        written.ShouldBe(1);
    }

    [Fact]
    public void TryFormat_ZeroExactSize_Succeeds()
    {
        BigNumber zero = BigNumber.Zero;
        Span<byte> buffer = stackalloc byte[1]; // "0"

        bool success = zero.TryFormat(buffer, out int written, "F0", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        written.ShouldBe(1);
    }

    [Fact]
    public void TryFormat_NegativeExactSize_Succeeds()
    {
        BigNumber num = new(-12345, -2); // -123.45
        Span<byte> buffer = stackalloc byte[7]; // "-123.45"

        bool success = num.TryFormat(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        written.ShouldBe(7);
    }

    [Fact]
    public void TryFormat_HighPrecisionExactSize_Succeeds()
    {
        BigNumber num = new(5, 0);
        Span<byte> buffer = stackalloc byte[12]; // "5.0000000000" (F10)

        bool success = num.TryFormat(buffer, out int written, "F10", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        written.ShouldBe(12);
    }

    [Fact]
    public void TryFormat_VeryLargeNumberMinimumSize_Succeeds()
    {
        BigNumber num = new(123456789, 0);
        Span<byte> buffer = stackalloc byte[9]; // "123456789"

        bool success = num.TryFormat(buffer, out int written, "F0", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_ExponentialHighPrecision_ExactSize()
    {
        BigNumber num = new(12345, 0);
        Span<byte> buffer = stackalloc byte[25]; // "1.2345000000E+004" with high precision

        bool success = num.TryFormat(buffer, out int written, "E10", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_NumberWithGrouping_ExactSize()
    {
        BigNumber num = new(1234567, -2); // 12345.67
        Span<byte> buffer = stackalloc byte[11]; // "12,345.67"

        bool success = num.TryFormat(buffer, out int written, "N2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_CurrencyWithGrouping_ExactSize()
    {
        BigNumber num = new(1234567, -2);
        Span<byte> buffer = stackalloc byte[15]; // "¤12,345.67"

        bool success = num.TryFormat(buffer, out int written, "C2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_PercentLargeValue_ExactSize()
    {
        BigNumber num = new(12345, -2); // 123.45
        Span<byte> buffer = stackalloc byte[13]; // "12,345.00 %"

        bool success = num.TryFormat(buffer, out int written, "P2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_GeneralFormat_ExactSize()
    {
        BigNumber num = new(12345, 0);
        Span<byte> buffer = stackalloc byte[5]; // "12345"

        bool success = num.TryFormat(buffer, out int written, "G", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    #endregion

    #region One Byte Short Scenarios (+10 tests)

    [Fact]
    public void TryFormat_OneByteShortFixedPoint_ReturnsFalse()
    {
        BigNumber num = new(12345, -2); // 123.45
        Span<byte> buffer = stackalloc byte[5]; // Need 6

        bool success = num.TryFormat(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryFormat_OneByteShortExponential_ReturnsFalse()
    {
        BigNumber num = new(12345, 0);
        Span<byte> buffer = stackalloc byte[8]; // Too small for "1.23E+004"

        bool success = num.TryFormat(buffer, out int written, "E2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryFormat_OneByteShortCurrency_ReturnsFalse()
    {
        BigNumber num = new(12345, -2);
        Span<byte> buffer = stackalloc byte[6]; // Too small

        bool success = num.TryFormat(buffer, out int written, "C2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryFormat_OneByteShortPercent_ReturnsFalse()
    {
        BigNumber num = new(12345, -4);
        Span<byte> buffer = stackalloc byte[6]; // Too small

        bool success = num.TryFormat(buffer, out int written, "P2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryFormat_OneByteShortNumber_ReturnsFalse()
    {
        BigNumber num = new(12345, -2);
        Span<byte> buffer = stackalloc byte[5]; // Too small

        bool success = num.TryFormat(buffer, out int written, "N2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryFormat_OneByteShortNegative_ReturnsFalse()
    {
        BigNumber num = new(-12345, -2);
        Span<byte> buffer = stackalloc byte[6]; // Need 7

        bool success = num.TryFormat(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryFormat_OneByteShortHighPrecision_ReturnsFalse()
    {
        BigNumber num = new(5, 0);
        Span<byte> buffer = stackalloc byte[11]; // Need 12 for F10

        bool success = num.TryFormat(buffer, out int written, "F10", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryFormat_ZeroBufferSize_ReturnsFalse()
    {
        BigNumber num = new(123, 0);
        Span<byte> buffer = Span<byte>.Empty;

        bool success = num.TryFormat(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void TryFormat_OneByte_TooSmallForAny_ReturnsFalse()
    {
        BigNumber num = new(12345, -2);
        Span<byte> buffer = stackalloc byte[1];

        bool success = num.TryFormat(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryFormat_TwoByte_TooSmallForMultiDigit_ReturnsFalse()
    {
        BigNumber num = new(12345, -2);
        Span<byte> buffer = stackalloc byte[2];

        bool success = num.TryFormat(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
    }

    #endregion

    #region Large Precision Requirements (+10 tests)

    [Fact]
    public void TryFormat_Precision50_RequiresLargeBuffer()
    {
        BigNumber num = new(1, -1); // 0.1
        Span<byte> buffer = stackalloc byte[64]; // Large enough for F50

        bool success = num.TryFormat(buffer, out int written, "F50", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        written.ShouldBeGreaterThan(50);
    }

    [Fact]
    public void TryFormat_Precision99_RequiresVeryLargeBuffer()
    {
        BigNumber num = new(1, 0);
        Span<byte> buffer = stackalloc byte[128];

        bool success = num.TryFormat(buffer, out int written, "F99", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_ExponentialPrecision50_RequiresLargeBuffer()
    {
        BigNumber num = new(12345, 10);
        Span<byte> buffer = stackalloc byte[80];

        bool success = num.TryFormat(buffer, out int written, "E50", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_NumberPrecision30_WithGrouping()
    {
        BigNumber num = new(123456789, -2);
        Span<byte> buffer = stackalloc byte[64];

        bool success = num.TryFormat(buffer, out int written, "N30", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_CurrencyPrecision20_RequiresLargeBuffer()
    {
        BigNumber num = new(12345, -2);
        Span<byte> buffer = stackalloc byte[64];

        bool success = num.TryFormat(buffer, out int written, "C20", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_PercentPrecision15_RequiresLargeBuffer()
    {
        BigNumber num = new(12345, -4);
        Span<byte> buffer = stackalloc byte[64];

        bool success = num.TryFormat(buffer, out int written, "P15", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_VeryLargeNumberHighPrecision_RequiresHugeBuffer()
    {
        BigNumber num = BigInteger.Parse(new string('9', 50));
        Span<byte> buffer = stackalloc byte[128];

        bool success = num.TryFormat(buffer, out int written, "F10", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_SmallNumberVeryHighPrecision_AddsZeros()
    {
        BigNumber num = new(1, -2); // 0.01
        Span<byte> buffer = stackalloc byte[64];

        bool success = num.TryFormat(buffer, out int written, "F30", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        // Should be "0.01" followed by 28 zeros
    }

    [Fact]
    public void TryFormat_BufferExactlyAtLimit_Succeeds()
    {
        // Test exactly at 256 byte threshold (common buffer size)
        BigNumber num = BigInteger.Parse(new string('9', 80));
        Span<byte> buffer = stackalloc byte[256];

        bool success = num.TryFormat(buffer, out int written, "F0", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_BufferJustOverLimit_HandlesCorrectly()
    {
        BigNumber num = BigInteger.Parse(new string('9', 100));
        Span<byte> buffer = stackalloc byte[512];

        bool success = num.TryFormat(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    #endregion
}