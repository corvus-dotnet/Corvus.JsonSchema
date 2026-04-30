// <copyright file="BigNumber.Arithmetic.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberArithmeticTests
{
    [Fact]
    public void Addition_SameExponent_ReturnsCorrectSum()
    {
        BigNumber left = new(123, 0);
        BigNumber right = new(456, 0);

        BigNumber result = left + right;

        result.ShouldBe(new BigNumber(579, 0));
    }

    [Fact]
    public void Addition_DifferentExponent_ReturnsCorrectSum()
    {
        BigNumber left = new(123, -2);  // 1.23
        BigNumber right = new(456, -1);  // 45.6

        BigNumber result = left + right;
        var expected = BigNumber.Parse("46.83");

        result.ShouldBe(expected);
    }

    [Fact]
    public void Addition_WithZero_ReturnsOriginal()
    {
        BigNumber value = new(123, -2);

        BigNumber result = value + BigNumber.Zero;

        result.ShouldBe(value);
    }

    [Fact]
    public void Subtraction_SameExponent_ReturnsCorrectDifference()
    {
        BigNumber left = new(456, 0);
        BigNumber right = new(123, 0);

        BigNumber result = left - right;

        result.ShouldBe(new BigNumber(333, 0));
    }

    [Fact]
    public void Subtraction_DifferentExponent_ReturnsCorrectDifference()
    {
        BigNumber left = new(456, -1);   // 45.6
        BigNumber right = new(123, -2);  // 1.23

        BigNumber result = left - right;
        var expected = BigNumber.Parse("44.37");

        result.ShouldBe(expected);
    }

    [Fact]
    public void Subtraction_ResultingInZero_ReturnsZero()
    {
        BigNumber value = new(123, -2);

        BigNumber result = value - value;

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Multiplication_Simple_ReturnsCorrectProduct()
    {
        BigNumber left = new(123, 0);
        BigNumber right = new(456, 0);

        BigNumber result = left * right;

        result.ShouldBe(new BigNumber(56088, 0));
    }

    [Fact]
    public void Multiplication_WithExponents_ReturnsCorrectProduct()
    {
        BigNumber left = new(12, 1);   // 120
        BigNumber right = new(34, 1);  // 340

        BigNumber result = left * right;

        result.ShouldBe(new BigNumber(408, 2));  // 40800
    }

    [Fact]
    public void Multiplication_ByZero_ReturnsZero()
    {
        BigNumber value = new(123, -2);

        BigNumber result = value * BigNumber.Zero;

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Multiplication_ByOne_ReturnsOriginal()
    {
        BigNumber value = new(123, -2);

        BigNumber result = value * BigNumber.One;

        result.ShouldBe(value);
    }

    [Fact]
    public void Division_Simple_ReturnsCorrectQuotient()
    {
        BigNumber dividend = new(100, 0);
        BigNumber divisor = new(4, 0);

        BigNumber result = dividend / divisor;
        var expected = BigNumber.Parse("25");

        result.ShouldBe(expected);
    }

    [Fact]
    public void Division_WithPrecision_ReturnsCorrectQuotient()
    {
        BigNumber dividend = new(10, 0);
        BigNumber divisor = new(3, 0);

        var result = BigNumber.Divide(dividend, divisor, 10);

        double resultDouble = (double)result;
        resultDouble.ShouldBe(10.0 / 3.0, tolerance: 0.0000000001);
    }

    [Fact]
    public void Division_ByZero_ThrowsDivideByZeroException()
    {
        BigNumber dividend = new(100, 0);

        Should.Throw<DivideByZeroException>(() => dividend / BigNumber.Zero);
    }

    [Fact]
    public void Division_ZeroByNonZero_ReturnsZero()
    {
        BigNumber divisor = new(123, -2);

        BigNumber result = BigNumber.Zero / divisor;

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Modulo_Simple_ReturnsCorrectRemainder()
    {
        BigNumber dividend = new(17, 0);
        BigNumber divisor = new(5, 0);

        BigNumber result = dividend % divisor;
        BigNumber expected = new(2, 0);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Modulo_WithZeroDivisor_ThrowsDivideByZeroException()
    {
        BigNumber dividend = new(100, 0);

        Should.Throw<DivideByZeroException>(() => dividend % BigNumber.Zero);
    }

    [Fact]
    public void Division_WithPositiveExponents_UsesModularArithmetic()
    {
        // 12300 / 100 = 123
        BigNumber dividend = new(123, 2);  // 123 * 10^2 = 12300
        BigNumber divisor = new(1, 2);     // 1 * 10^2 = 100

        BigNumber result = dividend / divisor;
        BigNumber expected = new(123, 0);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Modulo_WithPositiveExponents_UsesModularArithmetic()
    {
        // 12345 % 100 = 45
        BigNumber dividend = new(12345, 0);
        BigNumber divisor = new(1, 2);      // 100

        BigNumber result = dividend % divisor;
        BigNumber expected = new(45, 0);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Division_WithNegativeExponents_UsesModularArithmetic()
    {
        // 0.00123 / 0.01 = 0.123
        BigNumber dividend = new(123, -5);  // 123 * 10^-5 = 0.00123
        BigNumber divisor = new(1, -2);     // 1 * 10^-2 = 0.01

        BigNumber result = dividend / divisor;
        BigNumber expected = new(123, -3);  // 0.123

        result.ShouldBe(expected);
    }

    [Fact]
    public void Modulo_WithNegativeExponents_UsesModularArithmetic()
    {
        // 0.12345 % 0.1 = 0.02345
        BigNumber dividend = new(12345, -5);  // 0.12345
        BigNumber divisor = new(1, -1);       // 0.1

        BigNumber result = dividend % divisor;
        BigNumber expected = new(2345, -5);   // 0.02345

        result.ShouldBe(expected);
    }

    [Fact]
    public void Division_WithMixedExponents_UsesModularArithmetic()
    {
        // 1230 / 0.1 = 12300
        BigNumber dividend = new(123, 1);   // 1230
        BigNumber divisor = new(1, -1);     // 0.1

        BigNumber result = dividend / divisor;
        BigNumber expected = new(123, 2);   // 12300

        result.ShouldBe(expected);
    }

    [Fact]
    public void Modulo_LargeExponentDifference_AvoidsMaterialization()
    {
        // 123 * 10^100 % (1 * 10^50) = 0
        BigNumber dividend = new(123, 100);
        BigNumber divisor = new(1, 50);

        BigNumber result = dividend % divisor;
        BigNumber expected = new(0, 0);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Division_LargeExponentDifference_AvoidsMaterialization()
    {
        // (456 * 10^100) / (1 * 10^50) = 456 * 10^50
        BigNumber dividend = new(456, 100);
        BigNumber divisor = new(1, 50);

        BigNumber result = dividend / divisor;
        BigNumber expected = new(456, 50);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Negation_PositiveNumber_ReturnsNegative()
    {
        BigNumber value = new(123, -2);

        BigNumber result = -value;

        result.ShouldBe(new BigNumber(-123, -2));
    }

    [Fact]
    public void Negation_NegativeNumber_ReturnsPositive()
    {
        BigNumber value = new(-123, -2);

        BigNumber result = -value;

        result.ShouldBe(new BigNumber(123, -2));
    }

    [Fact]
    public void Negation_Zero_ReturnsZero()
    {
        BigNumber result = -BigNumber.Zero;

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void UnaryPlus_ReturnsOriginalValue()
    {
        BigNumber value = new(123, -2);

        BigNumber result = +value;

        result.ShouldBe(value);
    }

    [Fact]
    public void Increment_IncrementsByOne()
    {
        BigNumber value = new(10, 0);

        BigNumber result = ++value;

        result.ShouldBe(new BigNumber(11, 0));
    }

    [Fact]
    public void Decrement_DecrementsByOne()
    {
        BigNumber value = new(10, 0);

        BigNumber result = --value;

        result.ShouldBe(new BigNumber(9, 0));
    }

    [Fact]
    public void Abs_PositiveNumber_ReturnsOriginal()
    {
        BigNumber value = new(123, -2);

        var result = BigNumber.Abs(value);

        result.ShouldBe(value);
    }

    [Fact]
    public void Abs_NegativeNumber_ReturnsPositive()
    {
        BigNumber value = new(-123, -2);

        var result = BigNumber.Abs(value);

        result.ShouldBe(new BigNumber(123, -2));
    }

    [Fact]
    public void Abs_Zero_ReturnsZero()
    {
        var result = BigNumber.Abs(BigNumber.Zero);

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Sign_PositiveNumber_ReturnsOne()
    {
        BigNumber value = new(123, -2);

        int sign = BigNumber.Sign(value);

        sign.ShouldBe(1);
    }

    [Fact]
    public void Sign_NegativeNumber_ReturnsMinusOne()
    {
        BigNumber value = new(-123, -2);

        int sign = BigNumber.Sign(value);

        sign.ShouldBe(-1);
    }

    [Fact]
    public void Sign_Zero_ReturnsZero()
    {
        int sign = BigNumber.Sign(BigNumber.Zero);

        sign.ShouldBe(0);
    }
}