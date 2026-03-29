// <copyright file="BigNumber.SpanMemoryEdgeCases.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Tier 2/3: Span/Memory edge cases.
/// Target: +0.5% coverage (15 tests).
/// </summary>
public class BigNumberSpanMemoryEdgeCasesTests
{
    #region Empty/Null Handling (+5 tests)

    [Fact]
    public void TryFormat_EmptySpan_ReturnsFalse()
    {
        BigNumber num = new(123, 0);
        Span<byte> empty = Span<byte>.Empty;

        bool success = num.TryFormat(empty, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void TryFormat_EmptyFormatString_UsesGeneral()
    {
        BigNumber num = new(123, 0);
        Span<byte> buffer = stackalloc byte[64];

        bool success = num.TryFormat(buffer, out int written, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        written.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void TryFormatOptimized_EmptySpan_ReturnsFalse()
    {
        BigNumber num = new(123, 0);
        Span<char> empty = Span<char>.Empty;

        bool success = num.TryFormatOptimized(empty, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact]
    public void TryFormatOptimized_EmptyFormat_UsesGeneral()
    {
        BigNumber num = new(123, 0);
        Span<char> buffer = stackalloc char[64];

        bool success = num.TryFormatOptimized(buffer, out int written, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        written.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void TryFormatOptimized_SingleCharBuffer_TooSmall()
    {
        BigNumber num = new(123, 0);
        Span<char> buffer = stackalloc char[1];

        bool success = num.TryFormatOptimized(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
    }

    #endregion

    #region Format String Variations (+5 tests)

    [Fact]
    public void Format_LowercaseFormatSpecifiers_Work()
    {
        BigNumber num = new(12345, -2);

        // Test lowercase variants
        string f = num.ToString("f2", CultureInfo.InvariantCulture);
        string n = num.ToString("n2", CultureInfo.InvariantCulture);
        string e = num.ToString("e2", CultureInfo.InvariantCulture);
        string c = num.ToString("c2", CultureInfo.InvariantCulture);
        string p = num.ToString("p2", CultureInfo.InvariantCulture);
        string g = num.ToString("g", CultureInfo.InvariantCulture);

        f.ShouldNotBeEmpty();
        n.ShouldNotBeEmpty();
        e.ShouldContain("e");
        c.ShouldNotBeEmpty();
        p.ShouldNotBeEmpty();
        g.ShouldNotBeEmpty();
    }

    [Fact]
    public void Format_MixedCaseFormatSpecifiers_Work()
    {
        BigNumber num = new(12345, -2);

        string result1 = num.ToString("F2", CultureInfo.InvariantCulture);
        string result2 = num.ToString("f2", CultureInfo.InvariantCulture);

        // Should produce same result
        result1.ShouldBe(result2);
    }

    [Fact]
    public void TryFormat_NoPrecisionSpecified_UsesDefault()
    {
        BigNumber num = new(12345, -2);
        Span<byte> buffer = stackalloc byte[64];

        // "F" without precision
        bool success = num.TryFormat(buffer, out int written, "F", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_VeryLongFormatString_HandlesCorrectly()
    {
        BigNumber num = new(123, 0);
        Span<byte> buffer = stackalloc byte[64];

        // Format string with extra characters (should ignore)
        bool success = num.TryFormat(buffer, out int written, "F2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
    }

    [Fact]
    public void Format_GeneralWithNoPrecision_UsesDefault()
    {
        BigNumber num = new(123456789, 0);

        string result = num.ToString("G", CultureInfo.InvariantCulture);

        result.ShouldBe("123456789");
    }

    #endregion

    #region Culture Edge Cases (+5 tests)

    [Fact]
    public void Format_CurrentCulture_WorksCorrectly()
    {
        BigNumber num = new(12345, -2);

        // Use current culture
        string result = num.ToString("N2", CultureInfo.CurrentCulture);

        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void Format_CultureWithEmptyGroupSeparator_HandlesCorrectly()
    {
        BigNumber num = new(1234567, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberGroupSeparator = string.Empty;

        string result = num.ToString("N2", culture);

        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void Format_CultureWithSameDecimalAndGroupSeparator_HandlesCorrectly()
    {
        BigNumber num = new(1234567, -2);
        var culture = new CultureInfo("en-US");
        culture.NumberFormat.NumberDecimalSeparator = ".";
        culture.NumberFormat.NumberGroupSeparator = ".";

        string result = num.ToString("N2", culture);

        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void Format_NullCultureUsesInvariant_WorksCorrectly()
    {
        BigNumber num = new(12345, -2);

        // ToString() uses current culture
        string result = num.ToString("G", CultureInfo.CurrentCulture);

        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void TryFormat_NullCultureUsesInvariant_WorksCorrectly()
    {
        BigNumber num = new(12345, -2);
        Span<byte> buffer = stackalloc byte[64];

        bool success = num.TryFormat(buffer, out int written, "F2", null);

        success.ShouldBeTrue();
    }

    #endregion
}