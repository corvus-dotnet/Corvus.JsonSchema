// <copyright file="BigNumber.OptimizedFormatting.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using System.Text;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberOptimizedFormattingTests
{
    [Fact]
    public void TryFormatOptimized_SimpleInteger_ZeroAllocation()
    {
        BigNumber value = new(12345, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBe(5);
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("12345");
    }

    [Fact]
    public void TryFormatOptimized_WithPositiveExponent_ZeroAllocation()
    {
        BigNumber value = new(123, 5);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("123E5");
    }

    [Fact]
    public void TryFormatOptimized_WithNegativeExponent_ZeroAllocation()
    {
        BigNumber value = new(456, -10);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("456E-10");
    }

    [Fact]
    public void TryFormatOptimized_NegativeNumber_ZeroAllocation()
    {
        BigNumber value = new(-789, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("-789");
    }

    [Fact]
    public void TryFormatOptimized_Zero_ZeroAllocation()
    {
        BigNumber value = BigNumber.Zero;
        Span<char> buffer = stackalloc char[16];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBe(1);
        buffer[0].ShouldBe('0');
    }

    [Fact]
    public void TryFormatOptimized_LargeNumber_ZeroAllocation()
    {
        BigNumber value = new(BigInteger.Parse("123456789012345678901234567890"), 0);
        Span<char> buffer = stackalloc char[256];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        // Normalized: removes trailing zero
        result.ShouldBe("12345678901234567890123456789E1");
    }

    [Fact]
    public void TryFormatOptimized_InsufficientBuffer_ReturnsFalse()
    {
        BigNumber value = new(BigInteger.Parse("999999999999999999999999999999"), 0);
        Span<char> buffer = stackalloc char[5]; // Too small

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, default, null);

        success.ShouldBeFalse();
        charsWritten.ShouldBe(0);
    }

    [Fact]
    public void TryFormatUtf8Optimized_SimpleInteger_ZeroAllocation()
    {
        BigNumber value = new(98765, 0);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBe(5);
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("98765");
    }

    [Fact]
    public void TryFormatUtf8Optimized_WithPositiveExponent_ZeroAllocation()
    {
        BigNumber value = new(321, 15);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("321E15");
    }

    [Fact]
    public void TryFormatUtf8Optimized_WithNegativeExponent_ZeroAllocation()
    {
        BigNumber value = new(654, -99);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("654E-99");
    }

    [Fact]
    public void TryFormatUtf8Optimized_NegativeNumber_ZeroAllocation()
    {
        BigNumber value = new(-888, 0);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("-888");
    }

    [Fact]
    public void TryFormatUtf8Optimized_Zero_ZeroAllocation()
    {
        BigNumber value = BigNumber.Zero;
        Span<byte> buffer = stackalloc byte[16];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBe(1);
        buffer[0].ShouldBe((byte)'0');
    }

    [Fact]
    public void TryFormatUtf8Optimized_LargeNumber_ZeroAllocation()
    {
        BigNumber value = new(BigInteger.Parse("987654321098765432109876543210"), 0);
        Span<byte> buffer = stackalloc byte[256];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));
        result.ShouldBe("98765432109876543210987654321E1");
    }

    [Fact]
    public void TryFormatUtf8Optimized_InsufficientBuffer_ReturnsFalse()
    {
        BigNumber value = new(BigInteger.Parse("999999999999999999999999999999"), 0);
        Span<byte> buffer = stackalloc byte[5]; // Too small

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, default, null);

        success.ShouldBeFalse();
        bytesWritten.ShouldBe(0);
    }

    [Fact]
    public void OptimizedFormatting_ConsistencyWithStandardFormatting_SimpleInteger()
    {
        BigNumber value = new(123456, 0);

        // Standard formatting
        string standard = value.ToString();

        // Optimized formatting
        Span<char> buffer = stackalloc char[128];
        value.TryFormatOptimized(buffer, out int charsWritten, default, null);
        string optimized = buffer.Slice(0, charsWritten).ToString();

        optimized.ShouldBe(standard);
    }

    [Fact]
    public void OptimizedFormatting_ConsistencyWithStandardFormatting_WithExponent()
    {
        BigNumber value = new(12345, 10);

        string standard = value.ToString();

        Span<char> buffer = stackalloc char[128];
        value.TryFormatOptimized(buffer, out int charsWritten, default, null);
        string optimized = buffer.Slice(0, charsWritten).ToString();

        optimized.ShouldBe(standard);
    }

    [Fact]
    public void OptimizedFormatting_Utf8ConsistencyWithUtf16_SimpleInteger()
    {
        BigNumber value = new(123456, 0);

        // UTF-16
        Span<char> charBuffer = stackalloc char[128];
        value.TryFormatOptimized(charBuffer, out int charsWritten, default, null);
        string utf16 = charBuffer.Slice(0, charsWritten).ToString();

        // UTF-8
        Span<byte> byteBuffer = stackalloc byte[128];
        value.TryFormatUtf8Optimized(byteBuffer, out int bytesWritten, default, null);
        string utf8 = StringFromSpan.CreateFromUtf8(byteBuffer.Slice(0, bytesWritten));

        utf8.ShouldBe(utf16);
        bytesWritten.ShouldBe(charsWritten); // ASCII characters
    }

    [Fact]
    public void OptimizedFormatting_Utf8ConsistencyWithUtf16_NegativeWithExponent()
    {
        BigNumber value = new(-246, 12);

        Span<char> charBuffer = stackalloc char[128];
        value.TryFormatOptimized(charBuffer, out int charsWritten, default, null);
        string utf16 = charBuffer.Slice(0, charsWritten).ToString();

        Span<byte> byteBuffer = stackalloc byte[128];
        value.TryFormatUtf8Optimized(byteBuffer, out int bytesWritten, default, null);
        string utf8 = StringFromSpan.CreateFromUtf8(byteBuffer.Slice(0, bytesWritten));

        utf8.ShouldBe(utf16);
        utf8.ShouldBe("-246E12");
    }

    [Fact]
    public void OptimizedFormatting_GeneralFormat_WithPrecision()
    {
        BigNumber value = new(123456789, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "G5".AsSpan(), null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        // JsonElementHelpers uses exponential notation for large numbers with precision
        result.ShouldBe("1.2346E+8");// Rounded
    }

    [Fact]
    public void OptimizedFormatting_FixedPointFormat_WithPrecision()
    {
        BigNumber value = new(12345, -2); // 123.45
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldBe("123.45");
    }

    [Fact]
    public void OptimizedFormatting_ExponentialFormat_WithPrecision()
    {
        BigNumber value = new(12345, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, "E2".AsSpan(), CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldContain("E+");
        result.ShouldContain("1.");
    }

    [Fact]
    public void OptimizedFormatting_EdgeCases_VeryLargeExponent()
    {
        BigNumber value = new(123, int.MaxValue / 2);
        Span<char> buffer = stackalloc char[256];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldStartWith("123E");
    }

    [Fact]
    public void OptimizedFormatting_EdgeCases_VerySmallExponent()
    {
        BigNumber value = new(456, int.MinValue / 2);
        Span<char> buffer = stackalloc char[256];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        result.ShouldStartWith("456E-");
    }

    [Fact]
    public void OptimizedFormatting_Normalization_TrailingZeros()
    {
        BigNumber value = new(12300, 0);
        Span<char> buffer = stackalloc char[128];

        bool success = value.TryFormatOptimized(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        string result = buffer.Slice(0, charsWritten).ToString();
        // Should normalize: 123E2
        result.ShouldBe("123E2");
    }

    [Fact]
    public void OptimizedFormatting_SequentialCalls_NoInterference()
    {
        BigNumber value1 = new(111, 1);
        BigNumber value2 = new(222, 2);
        Span<char> buffer = stackalloc char[128];

        value1.TryFormatOptimized(buffer, out int chars1, default, null);
        string result1 = buffer.Slice(0, chars1).ToString();

        value2.TryFormatOptimized(buffer, out int chars2, default, null);
        string result2 = buffer.Slice(0, chars2).ToString();

        result1.ShouldBe("111E1");
        result2.ShouldBe("222E2");
    }

    [Fact]
    public void OptimizedFormatting_Utf8_SequentialCalls_NoInterference()
    {
        BigNumber value1 = new(333, 3);
        BigNumber value2 = new(444, 4);
        Span<byte> buffer = stackalloc byte[128];

        value1.TryFormatUtf8Optimized(buffer, out int bytes1, default, null);
        string result1 = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytes1));

        value2.TryFormatUtf8Optimized(buffer, out int bytes2, default, null);
        string result2 = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytes2));

        result1.ShouldBe("333E3");
        result2.ShouldBe("444E4");
    }
}