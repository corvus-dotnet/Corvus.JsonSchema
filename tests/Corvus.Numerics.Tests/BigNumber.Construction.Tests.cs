// <copyright file="BigNumber.Construction.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberConstructionTests
{
    [Fact]
    public void Constructor_WithSignificandAndExponent_CreatesCorrectValue()
    {
        BigNumber number = new(BigInteger.Parse("12345"), -2);

        number.Significand.ShouldBe(BigInteger.Parse("12345"));
        number.Exponent.ShouldBe(-2);
    }

    [Fact]
    public void Zero_ReturnsZeroValue()
    {
        BigNumber.Zero.Significand.ShouldBe(BigInteger.Zero);
        BigNumber.Zero.Exponent.ShouldBe(0);
    }

    [Fact]
    public void One_ReturnsOneValue()
    {
        BigNumber.One.Significand.ShouldBe(BigInteger.One);
        BigNumber.One.Exponent.ShouldBe(0);
    }

    [Fact]
    public void MinusOne_ReturnsMinusOneValue()
    {
        BigNumber.MinusOne.Significand.ShouldBe(BigInteger.MinusOne);
        BigNumber.MinusOne.Exponent.ShouldBe(0);
    }

    [Fact]
    public void Radix_ReturnsTen()
    {
        BigNumber.Radix.ShouldBe(10);
    }

    [Fact]
    public void ImplicitConversion_FromInt_CreatesCorrectValue()
    {
        BigNumber number = 42;

        number.Significand.ShouldBe(new BigInteger(42));
        number.Exponent.ShouldBe(0);
    }

    [Fact]
    public void ImplicitConversion_FromLong_CreatesCorrectValue()
    {
        BigNumber number = 123456789012345L;

        number.Significand.ShouldBe(new BigInteger(123456789012345L));
        number.Exponent.ShouldBe(0);
    }

    [Fact]
    public void ImplicitConversion_FromBigInteger_CreatesCorrectValue()
    {
        var value = BigInteger.Parse("999999999999999999999999999999");
        BigNumber number = value;

        number.Significand.ShouldBe(value);
        number.Exponent.ShouldBe(0);
    }

    [Fact]
    public void ImplicitConversion_FromDecimal_CreatesCorrectValue()
    {
        BigNumber number = 123.456m;

        var parsed = BigNumber.Parse("123.456");
        number.ShouldBe(parsed);
    }

    [Fact]
    public void ImplicitConversion_FromDouble_CreatesCorrectValue()
    {
        BigNumber number = 123.456;

        number.Significand.ShouldNotBe(BigInteger.Zero);
    }

    [Fact]
    public void ImplicitConversion_FromDouble_WithNaN_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => (BigNumber)double.NaN);
    }

    [Fact]
    public void ImplicitConversion_FromDouble_WithInfinity_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => (BigNumber)double.PositiveInfinity);
        Should.Throw<ArgumentException>(() => (BigNumber)double.NegativeInfinity);
    }

    [Fact]
    public void ExplicitConversion_ToDecimal_WithSimpleValue_ReturnsCorrectValue()
    {
        var number = BigNumber.Parse("123.456");

        decimal result = (decimal)number;

        result.ShouldBe(123.456m);
    }

    [Fact]
    public void ExplicitConversion_ToDecimal_WithZero_ReturnsZero()
    {
        decimal result = (decimal)BigNumber.Zero;

        result.ShouldBe(decimal.Zero);
    }

    [Fact]
    public void ExplicitConversion_ToDouble_WithSimpleValue_ReturnsCorrectValue()
    {
        var number = BigNumber.Parse("123.456");

        double result = (double)number;

        result.ShouldBe(123.456, tolerance: 0.001);
    }

    [Fact]
    public void ExplicitConversion_ToLong_WithIntegerValue_ReturnsCorrectValue()
    {
        BigNumber number = 12345;

        long result = (long)number;

        result.ShouldBe(12345L);
    }

    [Fact]
    public void ExplicitConversion_ToLong_WithDecimalValue_Truncates()
    {
        var number = BigNumber.Parse("123.456");

        long result = (long)number;

        result.ShouldBe(123L);
    }

    [Fact]
    public void IsInteger_WithIntegerValue_ReturnsTrue()
    {
        BigNumber number = 42;

        number.IsInteger().ShouldBeTrue();
    }

    [Fact]
    public void IsInteger_WithDecimalValue_ReturnsFalse()
    {
        var number = BigNumber.Parse("42.5");

        number.IsInteger().ShouldBeFalse();
    }

    [Fact]
    public void IsInteger_WithZero_ReturnsTrue()
    {
        BigNumber.Zero.IsInteger().ShouldBeTrue();
    }
}