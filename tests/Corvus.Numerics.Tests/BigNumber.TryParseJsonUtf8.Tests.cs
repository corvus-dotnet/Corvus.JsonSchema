// <copyright file="BigNumber.TryParseJsonUtf8.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Tests for <see cref="BigNumber.TryParseJsonUtf8"/>.
/// </summary>
public class BigNumberTryParseJsonUtf8Tests
{
    #region Basic Parsing Tests

    [Fact]
    public void TryParseJsonUtf8_Zero_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "0"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void TryParseJsonUtf8_PositiveInteger_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "12345"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(12345, 0));
    }

    [Fact]
    public void TryParseJsonUtf8_NegativeInteger_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "-12345"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(-12345, 0));
    }

    [Fact]
    public void TryParseJsonUtf8_DecimalNumber_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "123.45"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Parse("123.45"));
    }

    [Fact]
    public void TryParseJsonUtf8_NegativeDecimal_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "-123.45"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Parse("-123.45"));
    }

    #endregion

    #region Scientific Notation Tests

    [Fact]
    public void TryParseJsonUtf8_PositiveExponent_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "123E2"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(123, 2));
    }

    [Fact]
    public void TryParseJsonUtf8_NegativeExponent_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "1234E-3"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(1234, -3));
    }

    [Fact]
    public void TryParseJsonUtf8_LowercaseE_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "123e2"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(123, 2));
    }

    [Fact]
    public void TryParseJsonUtf8_NegativeWithPositiveExponent_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "-123E2"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(-123, 2));
    }

    [Fact]
    public void TryParseJsonUtf8_NegativeWithNegativeExponent_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "-1234E-3"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(-1234, -3));
    }

    [Fact]
    public void TryParseJsonUtf8_DecimalWithExponent_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "1.23E5"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Parse("1.23E5"));
    }

    #endregion

    #region Large Number Tests

    [Fact]
    public void TryParseJsonUtf8_LargeNumber_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "123456789012345678901234567890"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Parse("123456789012345678901234567890"));
    }

    [Fact]
    public void TryParseJsonUtf8_VeryLargeNumber_ReturnsTrue()
    {
        // 300-digit number (triggers ArrayPool path)
        string largeNumber = new('9', 300);
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(largeNumber);

        bool success = BigNumber.TryParseJsonUtf8(utf8Bytes, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Parse(largeNumber));
    }

    [Fact]
    public void TryParseJsonUtf8_LargeNumberWithExponent_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "12345678901234567890123456789E10"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Parse("12345678901234567890123456789E10"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TryParseJsonUtf8_EmptySpan_ReturnsFalse()
    {
        ReadOnlySpan<byte> utf8 = ReadOnlySpan<byte>.Empty;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeFalse();
        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void TryParseJsonUtf8_InvalidFormat_ReturnsFalse()
    {
        ReadOnlySpan<byte> utf8 = "not-a-number"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out _);

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryParseJsonUtf8_LeadingWhitespace_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = " 123"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(123, 0));
    }

    [Fact]
    public void TryParseJsonUtf8_TrailingWhitespace_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "123 "u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(new BigNumber(123, 0));
    }

    [Fact]
    public void TryParseJsonUtf8_VerySmallNumber_ReturnsTrue()
    {
        ReadOnlySpan<byte> utf8 = "0.00000000000000000001"u8;
        bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Parse("0.00000000000000000001"));
    }

    #endregion

    #region Roundtrip Tests

    [Fact]
    public void TryParseJsonUtf8_RoundtripWithFormat_Succeeds()
    {
        BigNumber original = new(12345, -2);

        // Format to UTF-8
        Span<byte> utf8Buffer = stackalloc byte[64];
        bool formatted = original.TryFormat(utf8Buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        formatted.ShouldBeTrue();

        // Parse back
        bool parsed = BigNumber.TryParseJsonUtf8(utf8Buffer.Slice(0, bytesWritten), out BigNumber result);
        parsed.ShouldBeTrue();

        result.ShouldBe(original);
    }

    [Fact]
    public void TryParseJsonUtf8_RoundtripLargeNumber_Succeeds()
    {
        var original = BigNumber.Parse("123456789012345678901234567890.123456789");

        // Format to UTF-8
        Span<byte> utf8Buffer = stackalloc byte[256];
        bool formatted = original.TryFormat(utf8Buffer, out int bytesWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
        formatted.ShouldBeTrue();

        // Parse back
        bool parsed = BigNumber.TryParseJsonUtf8(utf8Buffer.Slice(0, bytesWritten), out BigNumber result);
        parsed.ShouldBeTrue();

        result.ShouldBe(original);
    }

    [Fact]
    public void TryParseJsonUtf8_VariousFormats_AllSucceed()
    {
        (string, BigNumber)[] testCases = new[]
        {
            ("0", BigNumber.Zero),
            ("1", new BigNumber(1, 0)),
            ("-1", new BigNumber(-1, 0)),
            ("123", new BigNumber(123, 0)),
            ("-456", new BigNumber(-456, 0)),
            ("1E1", new BigNumber(1, 1)),
            ("1E-1", new BigNumber(1, -1)),
            ("12E5", new BigNumber(12, 5)),
            ("12E-5", new BigNumber(12, -5)),
            ("-12E5", new BigNumber(-12, 5)),
            ("-12E-5", new BigNumber(-12, -5)),
        };

        foreach ((string? input, BigNumber expected) in testCases)
        {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(input);
            bool success = BigNumber.TryParseJsonUtf8(utf8Bytes, out BigNumber result);

            success.ShouldBeTrue($"Failed to parse: {input}");
            result.ShouldBe(expected, $"Input: {input}");
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void TryParseJsonUtf8_BatchOf1000_CompletesQuickly()
    {
        byte[] utf8 = "123.45"u8.ToArray();

        for (int i = 0; i < 1000; i++)
        {
            bool success = BigNumber.TryParseJsonUtf8(utf8, out _);
            success.ShouldBeTrue();
        }
    }

    [Fact]
    public void TryParseJsonUtf8_DirectUtf8Parsing_NoCharConversion()
    {
        // Verify that parsing works correctly without char conversion
        (string, BigNumber)[] testCases = new[]
        {
            ("0", BigNumber.Zero),
            ("123", new BigNumber(123, 0)),
            ("-456", new BigNumber(-456, 0)),
            ("123.45", BigNumber.Parse("123.45")),
            ("-123.45", BigNumber.Parse("-123.45")),
            ("1E5", new BigNumber(1, 5)),
            ("1E-5", new BigNumber(1, -5)),
            ("123.45E10", BigNumber.Parse("123.45E10")),
            (" 123 ", new BigNumber(123, 0)), // whitespace handling
            ("  -456.78E-5  ", BigNumber.Parse("-456.78E-5")),
        };

        foreach ((string? input, BigNumber expected) in testCases)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(input);
            bool success = BigNumber.TryParseJsonUtf8(utf8, out BigNumber result);

            success.ShouldBeTrue($"Failed to parse: '{input}'");
            result.ShouldBe(expected, $"Input: '{input}'");
        }
    }

    #endregion
}