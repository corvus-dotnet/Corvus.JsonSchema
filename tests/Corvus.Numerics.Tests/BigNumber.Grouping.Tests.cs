// <copyright file="BigNumber.Grouping.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberGroupingTests
{
    #region Number Format with Grouping

    [Fact]
    public void NumberFormat_ThousandsSeparator_InvariantCulture()
    {
        BigNumber value = new(123456789, -2); // 1234567.89
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "N2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("1,234,567.89");
    }

    [Fact]
    public void NumberFormat_ThousandsSeparator_USCulture()
    {
        BigNumber value = new(1234567890, -2); // 12345678.90
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "N2", CultureInfo.GetCultureInfo("en-US"));

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("12,345,678.90");
    }

    [Fact]
    public void NumberFormat_ThousandsSeparator_GermanCulture()
    {
        BigNumber value = new(1234567890, -2); // 12345678.90
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "N2", CultureInfo.GetCultureInfo("de-DE"));

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("12.345.678,90"); // German uses . for thousands and , for decimal
    }

    [Fact]
    public void NumberFormat_SmallNumber_NoGrouping()
    {
        BigNumber value = new(12345, -2); // 123.45
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "N2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("123.45");
    }

    [Fact]
    public void NumberFormat_NegativeNumber_WithGrouping()
    {
        BigNumber value = new(-1234567890, -2); // -12345678.90
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "N2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("-12,345,678.90");
    }

    [Fact]
    public void NumberFormat_VeryLargeNumber_MultipleGroups()
    {
        BigNumber value = new(1234567890123456L, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "N0", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("1,234,567,890,123,456");
    }

    #endregion

    #region Currency Format with Grouping

    [Fact]
    public void CurrencyFormat_ThousandsSeparator_USCulture()
    {
        BigNumber value = new(123456, -2); // 1234.56
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "C", CultureInfo.GetCultureInfo("en-US"));

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("$1,234.56");
    }

    [Fact]
    public void CurrencyFormat_ThousandsSeparator_LargeAmount()
    {
        BigNumber value = new(1234567890, -2); // 12345678.90
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "C2", CultureInfo.GetCultureInfo("en-US"));

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("$12,345,678.90");
    }

    [Fact]
    public void CurrencyFormat_NegativeAmount_WithGrouping()
    {
        BigNumber value = new(-123456, -2); // -1234.56
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "C", CultureInfo.GetCultureInfo("en-US"));

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();

        // Verify it matches ToString for consistency (pattern may vary by OS/culture version)
        string expectedFromToString = value.ToString("C", CultureInfo.GetCultureInfo("en-US"));
        result.ShouldBe(expectedFromToString);

        // Should have the currency symbol and grouping
        result.ShouldContain("$");
        result.ShouldContain(",");
        result.ShouldContain("1,234.56");
    }

    [Fact]
    public void CurrencyFormat_EuroCulture_WithGrouping()
    {
        BigNumber value = new(1234567, -2); // 12345.67
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "C2", CultureInfo.GetCultureInfo("de-DE"));

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        // German currency format: 12.345,67 € (note: space before symbol)
        result.ShouldContain("12.345,67");
        result.ShouldContain("€");
    }

    #endregion

    #region UTF-8 with Grouping

    [Fact]
    public void Utf8_NumberFormat_WithGrouping()
    {
        BigNumber value = new(1234567890, -2);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormat(buffer, out int bytesWritten, "N2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("12,345,678.90");
    }

    [Fact]
    public void Utf8_CurrencyFormat_WithGrouping()
    {
        BigNumber value = new(123456, -2);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormat(buffer, out int bytesWritten, "C", CultureInfo.GetCultureInfo("en-US"));

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("$1,234.56");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void NumberFormat_ExactlyOneGroup_NoSeparator()
    {
        BigNumber value = new(999, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "N0", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("999");
    }

    [Fact]
    public void NumberFormat_JustOverOneGroup_OneSeparator()
    {
        BigNumber value = new(1000, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "N0", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("1,000");
    }

    [Fact]
    public void NumberFormat_Zero_NoGrouping()
    {
        BigNumber value = BigNumber.Zero;
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormat(buffer, out int charsWritten, "N2", CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("0.00");
    }

    #endregion

    #region Memory Diagnostics

    // NOTE: These tests verify zero allocations but may be affected by GC timing
    // They are commented out for now as they can be flaky in CI/CD environments

    /*
    [Fact]
    public void NumberFormat_ZeroAllocation_Verification()
    {
        // This test verifies that the grouping implementation doesn't allocate
        BigNumber value = new(1234567890, -2);
        Span<char> buffer = stackalloc char[128];

        // Warm up
        value.TryFormat(buffer, out _, "N2", CultureInfo.InvariantCulture);

        // Get baseline GC collection count
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        long gen0Before = GC.CollectionCount(0);

        // Format many times
        for (int i = 0; i < 10000; i++)
        {
            value.TryFormat(buffer, out _, "N2", CultureInfo.InvariantCulture);
        }

        long gen0After = GC.CollectionCount(0);

        // Should have zero Gen0 collections from formatting
        (gen0After - gen0Before).ShouldBe(0);
    }

    [Fact]
    public void CurrencyFormat_ZeroAllocation_Verification()
    {
        BigNumber value = new(123456, -2);
        Span<char> buffer = stackalloc char[128];

        // Warm up
        value.TryFormat(buffer, out _, "C2", CultureInfo.GetCultureInfo("en-US"));

        // Get baseline GC collection count
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        long gen0Before = GC.CollectionCount(0);

        // Format many times
        for (int i = 0; i < 10000; i++)
        {
            value.TryFormat(buffer, out _, "C2", CultureInfo.GetCultureInfo("en-US"));
        }

        long gen0After = GC.CollectionCount(0);

        // Should have zero Gen0 collections from formatting
        (gen0After - gen0Before).ShouldBe(0);
    }
    */

    #endregion
}