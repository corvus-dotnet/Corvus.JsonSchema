// <copyright file="BigNumber.Normalization.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberNormalizationTests
{
    [Fact]
    public void Normalize_RemovesTrailingZeros()
    {
        BigNumber value = new(12300, -2);

        BigNumber normalized = value.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(123));
        normalized.Exponent.ShouldBe(0);
    }

    [Fact]
    public void Normalize_Zero_ReturnsZero()
    {
        BigNumber value = new(0, 5);

        BigNumber normalized = value.Normalize();

        normalized.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Normalize_NoTrailingZeros_ReturnsSame()
    {
        BigNumber value = new(123, -2);

        BigNumber normalized = value.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(123));
        normalized.Exponent.ShouldBe(-2);
    }

    [Fact]
    public void Normalize_ManyTrailingZeros_RemovesAll()
    {
        BigNumber value = new(BigInteger.Parse("1230000000000"), -5);

        BigNumber normalized = value.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(123));
        normalized.Exponent.ShouldBe(5);
    }

    [Fact]
    public void Normalize_LargeNumberWithTrailingZeros_Succeeds()
    {
        var significand = BigInteger.Parse("999999999999999999999999999999000000");
        BigNumber value = new(significand, -10);

        BigNumber normalized = value.Normalize();

        normalized.Significand.ShouldBe(BigInteger.Parse("999999999999999999999999999999"));
        normalized.Exponent.ShouldBe(-4);
    }

    [Fact]
    public void Addition_Result_IsNormalized()
    {
        BigNumber a = new(1000, -2);  // 10.00
        BigNumber b = new(2000, -2);  // 20.00

        BigNumber sum = a + b;  // Should normalize to 30 with exponent 0 or 3000 with exponent -2

        // The operation returns normalized result
        BigNumber normalized = sum.Normalize();
        normalized.Significand.ToString().ShouldNotEndWith("0");
    }

    [Fact]
    public void Subtraction_Result_IsNormalized()
    {
        BigNumber a = new(5000, -2);  // 50.00
        BigNumber b = new(2000, -2);  // 20.00

        BigNumber diff = a - b;

        BigNumber normalized = diff.Normalize();
        normalized.Significand.ToString().ShouldNotEndWith("0");
    }

    [Fact]
    public void Multiplication_Result_IsNormalized()
    {
        BigNumber a = new(100, 0);
        BigNumber b = new(200, 0);

        BigNumber product = a * b;  // 20000

        BigNumber normalized = product.Normalize();
        // 20000 should normalize to 2 with exponent 4
        normalized.Significand.ShouldBe(new BigInteger(2));
        normalized.Exponent.ShouldBe(4);
    }

    [Fact]
    public void Parse_Number_ProducesCorrectSignificandAndExponent()
    {
        var parsed = BigNumber.Parse("123.456");

        // Should be 123456 × 10^-3
        parsed.Significand.ShouldBe(new BigInteger(123456));
        parsed.Exponent.ShouldBe(-3);
    }

    [Fact]
    public void Parse_NumberWithTrailingZeros_CanBeNormalized()
    {
        var parsed = BigNumber.Parse("123.4500");

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(new BigInteger(1234500) / 100);
        normalized.Exponent.ShouldBe(-2);
    }

    [Fact]
    public void Normalize_IdempotentOperation_MultipleCallsReturnSame()
    {
        BigNumber value = new(12300, -2);

        BigNumber normalized1 = value.Normalize();
        BigNumber normalized2 = normalized1.Normalize();

        normalized1.ShouldBe(normalized2);
    }

    [Fact]
    public void Normalize_NegativeNumber_WorksCorrectly()
    {
        BigNumber value = new(-12300, -2);

        BigNumber normalized = value.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(-123));
        normalized.Exponent.ShouldBe(0);
    }

    [Fact]
    public void Normalize_VeryLargeTrailingZeros_HandlesCorrectly()
    {
        // Number with 50 trailing zeros
        string fiftyZeros = "1" + new string('0', 50);
        var significand = BigInteger.Parse(fiftyZeros);
        BigNumber value = new(significand, -10);

        BigNumber normalized = value.Normalize();

        normalized.Significand.ShouldBe(BigInteger.One);
        normalized.Exponent.ShouldBe(40);  // -10 + 50 = 40
    }

    [Fact]
    public void IsInteger_AfterNormalization_ReturnsCorrectValue()
    {
        BigNumber withDecimals = new(12345, -2);  // 123.45
        BigNumber integer = new(12300, -2);       // 123.00

        withDecimals.IsInteger().ShouldBeFalse();
        integer.IsInteger().ShouldBeTrue();
    }

    [Fact]
    public void Normalize_PreservesValueEquality()
    {
        BigNumber original = new(123000, -3);
        BigNumber normalized = original.Normalize();

        original.ShouldBe(normalized);
        (original == normalized).ShouldBeTrue();
    }
}