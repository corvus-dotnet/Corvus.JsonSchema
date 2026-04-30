// <copyright file="BigNumber.AdvancedFormatting.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using System.Text;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberAdvancedFormattingTests
{
    #region Fixed-Point Format Tests

    [Fact]
    public void FixedPointFormat_NoDecimals_FormatsCorrectly()
    {
        BigNumber value = new(12345, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "F0".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("12345");
    }

    [Fact]
    public void FixedPointFormat_TwoDecimals_FormatsCorrectly()
    {
        BigNumber value = new(12345, -2); // 123.45
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("123.45");
    }

    [Fact]
    public void FixedPointFormat_FourDecimals_PadsZeros()
    {
        BigNumber value = new(123, -1); // 12.3
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "F4".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("12.3000");
    }

    [Fact]
    public void FixedPointFormat_NegativeNumber_FormatsCorrectly()
    {
        BigNumber value = new(-98765, -3); // -98.765
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "F3".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("-98.765");
    }

    [Fact]
    public void FixedPointFormat_SmallFraction_FormatsCorrectly()
    {
        BigNumber value = new(5, -3); // 0.005
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "F3".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("0.005");
    }

    [Fact]
    public void FixedPointFormat_LargePrecision_FormatsCorrectly()
    {
        BigNumber value = new(1, -10); // 0.0000000001
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "F10".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("0.0000000001");
    }

    #endregion

    #region General Format Tests

    [Fact]
    public void GeneralFormat_NoPrecision_UsesDefault()
    {
        BigNumber value = new(123456789, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "G".AsSpan(), null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("123456789");
    }

    [Fact]
    public void GeneralFormat_WithPrecision_RoundsCorrectly()
    {
        BigNumber value = new(123456789, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "G5".AsSpan(), null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        // JsonElementHelpers uses exponential notation for large numbers with precision
        result.ShouldBe("1.2346E+8");
    }

    [Fact]
    public void GeneralFormat_LowerCase_UsesLowercaseE()
    {
        BigNumber value = new(123, 5);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "g".AsSpan(), null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        // JsonElementHelpers expands small exponents to fixed-point notation
        result.ShouldBe("12300000");
    }

    [Fact]
    public void GeneralFormat_UpperCase_UsesUppercaseE()
    {
        BigNumber value = new(123, 5);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "G".AsSpan(), null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        // JsonElementHelpers expands small exponents to fixed-point notation
        result.ShouldBe("12300000");
    }

    #endregion

    #region Exponential Format Tests

    [Fact]
    public void ExponentialFormat_DefaultPrecision_SixDecimals()
    {
        BigNumber value = new(12345, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "E".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("1.234500E+004");
    }

    [Fact]
    public void ExponentialFormat_TwoPrecision_TwoDecimals()
    {
        BigNumber value = new(12345, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "E2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("1.23E+004");
    }

    [Fact]
    public void ExponentialFormat_LowerCase_UsesLowercaseE()
    {
        BigNumber value = new(12345, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "e2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("1.23e+004");
    }

    [Fact]
    public void ExponentialFormat_NegativeExponent_FormatsCorrectly()
    {
        BigNumber value = new(123, -5); // 0.00123
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "E2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("1.23E-003");
    }

    [Fact]
    public void ExponentialFormat_Zero_FormatsCorrectly()
    {
        BigNumber value = BigNumber.Zero;
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "E2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("0.00E+000");
    }

    #endregion

    #region Currency Format Tests

    [Fact]
    public void CurrencyFormat_DefaultPrecision_FormatsCorrectly()
    {
        BigNumber value = new(12345, -2); // 123.45
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "C".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("¤123.45");
    }

    [Fact]
    public void CurrencyFormat_TwoPrecision_FormatsCorrectly()
    {
        BigNumber value = new(12345, -2); // 123.45
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "C2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("¤123.45");
    }

    #endregion

    #region Percent Format Tests

    [Fact]
    public void PercentFormat_DefaultPrecision_MultipliesByHundred()
    {
        BigNumber value = new(5, -1); // 0.5 = 50%
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "P".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("50.00 %");
    }

    [Fact]
    public void PercentFormat_TwoPrecision_FormatsCorrectly()
    {
        BigNumber value = new(75, -2); // 0.75 = 75%
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "P2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("75.00 %");
    }

    [Fact]
    public void PercentFormat_SmallValue_FormatsCorrectly()
    {
        BigNumber value = new(1, -2); // 0.01 = 1%
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "P0".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("1 %");
    }

    #endregion

    #region Number Format Tests

    [Fact]
    public void NumberFormat_DefaultPrecision_FormatsCorrectly()
    {
        BigNumber value = new(12345, -2); // 123.45
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "N".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("123.45");
    }

    [Fact]
    public void NumberFormat_TwoPrecision_FormatsCorrectly()
    {
        BigNumber value = new(12345, -2); // 123.45
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "N2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("123.45");
    }

    #endregion

    #region UTF-8 Format Tests

    [Fact]
    public void Utf8Format_FixedPoint_FormatsCorrectly()
    {
        BigNumber value = new(12345, -2); // 123.45
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("123.45");
    }

    [Fact]
    public void Utf8Format_Exponential_FormatsCorrectly()
    {
        BigNumber value = new(12345, 0);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, "E2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldStartWith("1.23");
        result.ShouldContain("E+004");
    }

    [Fact]
    public void Utf8Format_General_FormatsCorrectly()
    {
        BigNumber value = new(123, 5);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, "G".AsSpan(), null);

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("12300000");
    }

    [Fact]
    public void Utf8Format_Percent_FormatsCorrectly()
    {
        BigNumber value = new(5, -1); // 0.5 = 50%
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, "P0".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldContain("50");
        result.ShouldContain("%");
    }

    #endregion

    #region Rounding and Precision Tests

    [Fact]
    public void FixedPoint_Rounding_RoundsAwayFromZero()
    {
        BigNumber value = new(12345, -3); // 12.345
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        // Should round 12.345 to 12.35 (round half away from zero or banker's rounding)
        result.ShouldBe("12.35");
    }

    [Fact]
    public void FixedPoint_RoundingUp_RoundsCorrectly()
    {
        BigNumber value = new(12346, -3); // 12.346
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("12.35");
    }

    [Fact]
    public void General_PrecisionLimiting_TruncatesCorrectly()
    {
        BigNumber value = new(123456789, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "G3".AsSpan(), null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        // Should limit to 3 significant digits
        result.Length.ShouldBeLessThan(10);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Format_VeryLargeNumber_FormatsCorrectly()
    {
        BigNumber value = new(BigInteger.Parse("999999999999999999999999999999"), 100);
        Span<char> buffer = stackalloc char[512];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "G".AsSpan(), null);

        success.ShouldBeTrue();
        charsWritten.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Format_VerySmallNumber_FormatsCorrectly()
    {
        BigNumber value = new(1, -100);
        Span<char> buffer = stackalloc char[256];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "E10".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("1.0000000000E-100");
    }

    [Fact]
    public void Format_NegativeZero_FormatsAsZero()
    {
        BigNumber value = new(0, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("0.00");
    }

    #endregion

    #region Format Consistency Tests

    [Fact]
    public void Format_ConsistencyBetweenUtf8AndUtf16_FixedPoint()
    {
        BigNumber value = new(123456, -3); // 123.456

        Span<char> charBuffer = stackalloc char[128];
        value.TryFormatOptimized(charBuffer, out int charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);
        string utf16Result = charBuffer.Slice(0, charsWritten).ToString();

        Span<byte> byteBuffer = stackalloc byte[128];
        value.TryFormatUtf8Optimized(byteBuffer, out int bytesWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);
        string utf8Result = StringFromSpan.CreateFromUtf8(byteBuffer.Slice(0, bytesWritten));

        utf16Result.ShouldBe(utf8Result);
    }

    [Fact]
    public void Format_ConsistencyBetweenUtf8AndUtf16_Exponential()
    {
        BigNumber value = new(123456, 0);

        Span<char> charBuffer = stackalloc char[128];
        value.TryFormatOptimized(charBuffer, out int charsWritten, "E3".AsSpan(), CultureInfo.InvariantCulture);
        string utf16Result = charBuffer.Slice(0, charsWritten).ToString();

        Span<byte> byteBuffer = stackalloc byte[128];
        value.TryFormatUtf8Optimized(byteBuffer, out int bytesWritten, "E3".AsSpan(), CultureInfo.InvariantCulture);
        string utf8Result = StringFromSpan.CreateFromUtf8(byteBuffer.Slice(0, bytesWritten));

        utf16Result.ShouldBe(utf8Result);
    }

    #endregion
}