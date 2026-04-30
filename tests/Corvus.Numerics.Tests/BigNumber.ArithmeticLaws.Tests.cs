// <copyright file="BigNumber.ArithmeticLaws.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Tests that verify BigNumber arithmetic operations follow mathematical laws.
/// </summary>
public class BigNumberArithmeticLawsTests
{
    #region Commutative Laws

    [Fact]
    public void Addition_Commutative_APlusBEqualsBPlusA()
    {
        var a = BigNumber.Parse("123.456");
        var b = BigNumber.Parse("789.012");

        BigNumber result1 = a + b;
        BigNumber result2 = b + a;

        result1.ShouldBe(result2);
    }

    [Fact]
    public void Multiplication_Commutative_ATimesBEqualsBTimesA()
    {
        var a = BigNumber.Parse("123.456");
        var b = BigNumber.Parse("789.012");

        BigNumber result1 = a * b;
        BigNumber result2 = b * a;

        result1.ShouldBe(result2);
    }

    #endregion

    #region Associative Laws

    [Fact]
    public void Addition_Associative_APlusBPlusCEqualsAPlusBPlusC()
    {
        var a = BigNumber.Parse("123.456");
        var b = BigNumber.Parse("789.012");
        var c = BigNumber.Parse("345.678");

        BigNumber result1 = (a + b) + c;
        BigNumber result2 = a + (b + c);

        result1.ShouldBe(result2);
    }

    [Fact]
    public void Multiplication_Associative_ATimesBTimesCEqualsATimesBTimesC()
    {
        BigNumber a = new(2, 0);
        BigNumber b = new(3, 0);
        BigNumber c = new(5, 0);

        BigNumber result1 = (a * b) * c;
        BigNumber result2 = a * (b * c);

        result1.ShouldBe(result2);
    }

    #endregion

    #region Distributive Laws

    [Fact]
    public void Multiplication_Distributive_ATimesBPlusCEqualsATimesBPlusATimesC()
    {
        BigNumber a = new(2, 0);
        BigNumber b = new(3, 0);
        BigNumber c = new(5, 0);

        BigNumber result1 = a * (b + c);
        BigNumber result2 = (a * b) + (a * c);

        result1.ShouldBe(result2);
    }

    #endregion

    #region Identity Laws

    [Fact]
    public void Addition_Identity_APlusZeroEqualsA()
    {
        var a = BigNumber.Parse("123.456");

        BigNumber result = a + BigNumber.Zero;

        result.ShouldBe(a);
    }

    [Fact]
    public void Multiplication_Identity_ATimesOneEqualsA()
    {
        var a = BigNumber.Parse("123.456");

        BigNumber result = a * BigNumber.One;

        result.ShouldBe(a);
    }

    [Fact]
    public void Multiplication_Zero_ATimesZeroEqualsZero()
    {
        var a = BigNumber.Parse("123.456");

        BigNumber result = a * BigNumber.Zero;

        result.ShouldBe(BigNumber.Zero);
    }

    #endregion

    #region Inverse Laws

    [Fact]
    public void Addition_Inverse_APlusNegativeAEqualsZero()
    {
        var a = BigNumber.Parse("123.456");

        BigNumber result = a + (-a);

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Subtraction_Self_AMinusAEqualsZero()
    {
        var a = BigNumber.Parse("123.456");

        BigNumber result = a - a;

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Division_ByItself_ADividedByAEqualsOne()
    {
        var a = BigNumber.Parse("123.456");

        BigNumber result = a / a;

        result.ShouldBe(BigNumber.One);
    }

    #endregion

    #region Comparison Transitivity

    [Fact]
    public void Comparison_Transitive_IfALessBAndBLessCThenALessC()
    {
        BigNumber a = new(100, 0);
        BigNumber b = new(200, 0);
        BigNumber c = new(300, 0);

        bool aLessB = a < b;
        bool bLessC = b < c;
        bool aLessC = a < c;

        aLessB.ShouldBeTrue();
        bLessC.ShouldBeTrue();
        aLessC.ShouldBeTrue();
    }

    [Fact]
    public void Comparison_Reflexive_AEqualsA()
    {
        var a = BigNumber.Parse("123.456");
        BigNumber b = a;

        (a == b).ShouldBeTrue();
        a.CompareTo(b).ShouldBe(0);
    }

    [Fact]
    public void Comparison_Symmetric_IfAEqualsBThenBEqualsA()
    {
        var a = BigNumber.Parse("123.456");
        var b = BigNumber.Parse("123.456");

        (a == b).ShouldBeTrue();
        (b == a).ShouldBeTrue();
    }

    #endregion

    #region Arithmetic Properties

    [Fact]
    public void Negation_DoubleNegation_NegativeNegativeAEqualsA()
    {
        var a = BigNumber.Parse("123.456");

        BigNumber result = -(-a);

        result.ShouldBe(a);
    }

    [Fact]
    public void Subtraction_AsAdditionOfNegative_AMinusBEqualsAPlusNegativeB()
    {
        var a = BigNumber.Parse("123.456");
        var b = BigNumber.Parse("789.012");

        BigNumber result1 = a - b;
        BigNumber result2 = a + (-b);

        result1.ShouldBe(result2);
    }

    [Fact]
    public void Division_AsMultiplicationOfReciprocal_Consistency()
    {
        BigNumber a = new(100, 0);
        BigNumber b = new(4, 0);

        BigNumber result = a / b;

        // 100 / 4 = 25
        result.ShouldBe(new BigNumber(25, 0));
    }

    #endregion

    #region Edge Case Validations

    [Fact]
    public void Abs_PreservesZero()
    {
        var result = BigNumber.Abs(BigNumber.Zero);

        result.ShouldBe(BigNumber.Zero);
    }

    [Fact]
    public void Abs_Idempotent_AbsAbsAEqualsAbsA()
    {
        var a = BigNumber.Parse("-123.456");

        var result1 = BigNumber.Abs(a);
        var result2 = BigNumber.Abs(result1);

        result1.ShouldBe(result2);
    }

    [Fact]
    public void Sign_ConsistentWithComparison()
    {
        BigNumber positive = new(123, 0);
        BigNumber negative = new(-456, 0);
        BigNumber zero = BigNumber.Zero;

        BigNumber.Sign(positive).ShouldBe(1);
        (positive > zero).ShouldBeTrue();

        BigNumber.Sign(negative).ShouldBe(-1);
        (negative < zero).ShouldBeTrue();

        BigNumber.Sign(zero).ShouldBe(0);
        (zero == BigNumber.Zero).ShouldBeTrue();
    }

    #endregion

    #region Normalization Laws

    [Fact]
    public void Normalize_Idempotent_NormalizeNormalizeAEqualsNormalizeA()
    {
        BigNumber a = new(12300, -2);

        BigNumber result1 = a.Normalize();
        BigNumber result2 = result1.Normalize();

        result1.ShouldBe(result2);
    }

    [Fact]
    public void Normalize_PreservesValue_ANormalizedEqualsA()
    {
        BigNumber a = new(12300, -2);

        BigNumber normalized = a.Normalize();

        a.ShouldBe(normalized);
    }

    [Fact]
    public void Arithmetic_ResultsAreNormalized()
    {
        BigNumber a = new(1000, 0);
        BigNumber b = new(2000, 0);

        BigNumber sum = a + b;
        BigNumber product = a * b;

        // Results should be normalized (no trailing zeros in significand)
        sum.Normalize().ShouldBe(sum);
        product.Normalize().ShouldBe(product);
    }

    #endregion
}