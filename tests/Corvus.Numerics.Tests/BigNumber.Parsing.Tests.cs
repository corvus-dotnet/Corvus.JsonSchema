// <copyright file="BigNumber.Parsing.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using System.Text;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberParsingTests
{
    [Fact]
    public void Parse_SimpleInteger_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("12345");

        result.Significand.ShouldBe(new BigInteger(12345));
        result.Exponent.ShouldBe(0);
    }

    [Fact]
    public void Parse_NegativeInteger_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("-12345");

        result.Significand.ShouldBe(new BigInteger(-12345));
        result.Exponent.ShouldBe(0);
    }

    [Fact]
    public void Parse_DecimalNumber_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("123.456");

        result.ShouldBe(new BigNumber(123456, -3));
    }

    [Fact]
    public void Parse_ScientificNotation_Positive_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("1.23E+5");

        result.ShouldBe(new BigNumber(123, 3));
    }

    [Fact]
    public void Parse_ScientificNotation_Negative_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("1.23E-5");

        result.ShouldBe(new BigNumber(123, -7));
    }

    [Fact]
    public void Parse_Zero_ReturnsZero()
    {
        var result = BigNumber.Parse("0");

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Parse_ZeroPointZero_ReturnsZero()
    {
        var result = BigNumber.Parse("0.0");

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Parse_VeryLargeNumber_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("123456789012345678901234567890");

        result.Significand.ShouldBe(BigInteger.Parse("12345678901234567890123456789"));
        result.Exponent.ShouldBe(1);
    }

    [Fact]
    public void Parse_VerySmallNumber_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("0.000000000000000000000000000123");

        BigNumber normalized = result.Normalize();
        normalized.Significand.ShouldBe(new BigInteger(123));
        normalized.Exponent.ShouldBe(-30);
    }

    [Fact]
    public void TryParse_ValidNumber_ReturnsTrue()
    {
        bool success = BigNumber.TryParse("123.456", out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Parse("123.456"));
    }

    [Fact]
    public void TryParse_InvalidNumber_ReturnsFalse()
    {
        bool success = BigNumber.TryParse("not a number", out BigNumber result);

        success.ShouldBeFalse();
        result.ShouldBe(default);
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        bool success = BigNumber.TryParse("", out _);

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryParse_NullString_ReturnsFalse()
    {
        bool success = BigNumber.TryParse((string)null!, out _);

        success.ShouldBeFalse();
    }

    [Fact]
    public void Parse_WithCultureInfo_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("123.456", CultureInfo.InvariantCulture);

        result.ShouldBe(BigNumber.Parse("123.456"));
    }

    [Fact]
    public void Parse_UTF8Bytes_ReturnsCorrectValue()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        var result = BigNumber.Parse(utf8.AsSpan());

        result.ShouldBe(BigNumber.Parse("123.456"));
    }

    [Fact]
    public void TryParse_UTF8Bytes_ReturnsTrue()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("123.456");
        bool success = BigNumber.TryParse(utf8.AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out BigNumber result);

        success.ShouldBeTrue();
        result.ShouldBe(BigNumber.Parse("123.456"));
    }

    [Fact]
    public void Parse_Span_ReturnsCorrectValue()
    {
        ReadOnlySpan<char> span = "123.456".AsSpan();
        var result = BigNumber.Parse(span);

        result.ShouldBe(BigNumber.Parse("123.456"));
    }

    [Fact]
    public void Parse_InvalidFormat_ThrowsFormatException()
    {
        Should.Throw<FormatException>(() => BigNumber.Parse("12.34.56"));
    }

    [Fact]
    public void Parse_LeadingWhitespace_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("  123.456  ");

        result.ShouldBe(BigNumber.Parse("123.456"));
    }

    [Fact]
    public void Parse_ExponentWithoutDecimal_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("123E5");

        result.ShouldBe(new BigNumber(123, 5));
    }

    [Fact]
    public void Parse_NegativeExponent_ReturnsCorrectValue()
    {
        var result = BigNumber.Parse("123E-5");

        result.ShouldBe(new BigNumber(123, -5));
    }
}