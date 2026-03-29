// <copyright file="JsonUtf8FormattingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Tests for the highly-optimized JSON UTF-8 formatting fast path.
/// </summary>
public class JsonUtf8FormattingTests
{
    [Fact]
    public void TryFormatJsonUtf8_Zero_ReturnsZero()
    {
        BigNumber value = BigNumber.Zero;
        Span<byte> buffer = stackalloc byte[64];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("0");
    }

    [Fact]
    public void TryFormatJsonUtf8_PositiveInteger_NoExponent()
    {
        var value = new BigNumber(1234, 0);
        Span<byte> buffer = stackalloc byte[64];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("1234");
    }

    [Fact]
    public void TryFormatJsonUtf8_NegativeInteger_NoExponent()
    {
        var value = new BigNumber(-1234, 0);
        Span<byte> buffer = stackalloc byte[64];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("-1234");
    }

    [Fact]
    public void TryFormatJsonUtf8_PositiveWithNegativeExponent()
    {
        var value = new BigNumber(1234, -3);
        Span<byte> buffer = stackalloc byte[64];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("1234E-3");
    }

    [Fact]
    public void TryFormatJsonUtf8_PositiveWithPositiveExponent()
    {
        var value = new BigNumber(1234, 2);
        Span<byte> buffer = stackalloc byte[64];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("1234E2");
    }

    [Fact]
    public void TryFormatJsonUtf8_NegativeWithNegativeExponent()
    {
        var value = new BigNumber(-1234, -3);
        Span<byte> buffer = stackalloc byte[64];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("-1234E-3");
    }

    [Fact]
    public void TryFormatJsonUtf8_NegativeWithPositiveExponent()
    {
        var value = new BigNumber(-1234, 2);
        Span<byte> buffer = stackalloc byte[64];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("-1234E2");
    }

    [Fact]
    public void TryFormatJsonUtf8_WithGFormat_DifferentThanEmpty()
    {
        var value = new BigNumber(1234, -3);
        Span<byte> buffer1 = stackalloc byte[64];
        Span<byte> buffer2 = stackalloc byte[64];

        bool success1 = value.TryFormatUtf8Optimized(buffer1, out int bytes1, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        bool success2 = value.TryFormatUtf8Optimized(buffer2, out int bytes2, "G".AsSpan(), CultureInfo.InvariantCulture);

        success1.ShouldBeTrue();
        success2.ShouldBeTrue();

        // Empty format produces raw JSON format: "1234E-3"
        StringFromSpan.CreateFromUtf8(buffer1.Slice(0, bytes1)).ShouldBe("1234E-3");

        // G format produces normalized general format: "1.234"
        StringFromSpan.CreateFromUtf8(buffer2.Slice(0, bytes2)).ShouldBe("1.234");
    }

    [Fact]
    public void TryFormatJsonUtf8_WithLowercaseG_DifferentThanEmpty()
    {
        var value = new BigNumber(1234, -3);
        Span<byte> buffer1 = stackalloc byte[64];
        Span<byte> buffer2 = stackalloc byte[64];

        bool success1 = value.TryFormatUtf8Optimized(buffer1, out int bytes1, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        bool success2 = value.TryFormatUtf8Optimized(buffer2, out int bytes2, "g".AsSpan(), CultureInfo.InvariantCulture);

        success1.ShouldBeTrue();
        success2.ShouldBeTrue();

        // Empty format produces raw JSON format: "1234E-3"
        StringFromSpan.CreateFromUtf8(buffer1.Slice(0, bytes1)).ShouldBe("1234E-3");

        // g format produces normalized general format with lowercase e: "1.234"
        StringFromSpan.CreateFromUtf8(buffer2.Slice(0, bytes2)).ShouldBe("1.234");
    }

    [Fact]
    public void TryFormatJsonUtf8_WithNullProvider_SameAsInvariant()
    {
        var value = new BigNumber(1234, -3);
        Span<byte> buffer1 = stackalloc byte[64];
        Span<byte> buffer2 = stackalloc byte[64];

        bool success1 = value.TryFormatUtf8Optimized(buffer1, out int bytes1, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        bool success2 = value.TryFormatUtf8Optimized(buffer2, out int bytes2, ReadOnlySpan<char>.Empty, null);

        success1.ShouldBeTrue();
        success2.ShouldBeTrue();
        bytes1.ShouldBe(bytes2);
        buffer1.Slice(0, bytes1).SequenceEqual(buffer2.Slice(0, bytes2)).ShouldBeTrue();
    }

    [Fact]
    public void TryFormatJsonUtf8_LargeNumber()
    {
        var value = new BigNumber(123456789012345678, 10);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("123456789012345678E10");
    }

    [Fact]
    public void TryFormatJsonUtf8_VeryLargeExponent()
    {
        var value = new BigNumber(123, 12345);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("123E12345");
    }

    [Fact]
    public void TryFormatJsonUtf8_VeryNegativeExponent()
    {
        var value = new BigNumber(123, -12345);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("123E-12345");
    }

    [Fact]
    public void TryFormatJsonUtf8_Normalization_RemovesTrailingZeros()
    {
        var value = new BigNumber(12340, -2); // Should normalize to 1234E-1
        Span<byte> buffer = stackalloc byte[64];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("1234E-1");
    }

    [Fact]
    public void TryFormatJsonUtf8_BufferTooSmall_ReturnsFalse()
    {
        var value = new BigNumber(1234567890, 100);
        Span<byte> buffer = stackalloc byte[5]; // Too small

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeFalse();
        bytesWritten.ShouldBe(0);
    }

    [Fact]
    public void TryFormatJsonUtf8_ExactBufferSize_Succeeds()
    {
        var value = new BigNumber(123, 0);
        Span<byte> buffer = stackalloc byte[3]; // Exact size

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        bytesWritten.ShouldBe(3);
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe("123");
    }

    [Theory]
    [InlineData(0L, 0, "0")]
    [InlineData(1L, 0, "1")]
    [InlineData(-1L, 0, "-1")]
    [InlineData(12L, 0, "12")]
    [InlineData(123L, 0, "123")]
    [InlineData(1234L, 0, "1234")]
    [InlineData(12345L, 0, "12345")]
    [InlineData(1L, 1, "1E1")]
    [InlineData(1L, -1, "1E-1")]
    [InlineData(12L, 5, "12E5")]
    [InlineData(12L, -5, "12E-5")]
    [InlineData(-12L, 5, "-12E5")]
    [InlineData(-12L, -5, "-12E-5")]
    public void TryFormatJsonUtf8_VariousValues_CorrectOutput(long significand, int exponent, string expected)
    {
        var value = new BigNumber(significand, exponent);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();
        StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten)).ShouldBe(expected);
    }

    [Fact]
    public void TryFormatJsonUtf8_ConsistentWithCharFormatting()
    {
        var value = new BigNumber(1234567, -5);

        Span<byte> utf8Buffer = stackalloc byte[128];
        Span<char> charBuffer = stackalloc char[128];

        bool utf8Success = value.TryFormatUtf8Optimized(utf8Buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        bool charSuccess = value.TryFormatOptimized(charBuffer, out int charsWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        utf8Success.ShouldBeTrue();
        charSuccess.ShouldBeTrue();

        string utf8String = StringFromSpan.CreateFromUtf8(utf8Buffer.Slice(0, bytesWritten));
        string charString = charBuffer.Slice(0, charsWritten).ToString();

        utf8String.ShouldBe(charString);
    }

    [Fact]
    public void TryFormatJsonUtf8_DecimalNumber()
    {
        var value = BigNumber.Parse("123.456", CultureInfo.InvariantCulture);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();

        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));

        // Should be normalized form
        result.ShouldContain("E");
    }

    [Fact]
    public void TryFormatJsonUtf8_VerySmallNumber()
    {
        var value = BigNumber.Parse("0.000001", CultureInfo.InvariantCulture);
        Span<byte> buffer = stackalloc byte[128];

        bool success = value.TryFormatUtf8Optimized(buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);

        success.ShouldBeTrue();

        string result = StringFromSpan.CreateFromUtf8(buffer.Slice(0, bytesWritten));

        // Should be in scientific notation
        result.ShouldContain("E");
    }
}