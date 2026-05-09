// <copyright file="BinaryJsonNumberTypeCheckTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET8_0_OR_GREATER

using System.Numerics;
using Corvus.Json;

namespace CoverageGap;

/// <summary>
/// Tests for BinaryJsonNumber INumberBase/IFloatingPointIeee754 type-check methods.
/// </summary>
[TestClass]
public class BinaryJsonNumberTypeCheckTests
{
    [TestMethod]
    public void IsPositiveInfinity_Double_ReturnsTrue()
    {
        BinaryJsonNumber value = new(double.PositiveInfinity);
        Assert.IsTrue(BinaryJsonNumber.IsPositiveInfinity(value));
    }

    [TestMethod]
    public void IsPositiveInfinity_Double_NegativeInfinity_ReturnsFalse()
    {
        BinaryJsonNumber value = new(double.NegativeInfinity);
        Assert.IsFalse(BinaryJsonNumber.IsPositiveInfinity(value));
    }

    [TestMethod]
    public void IsPositiveInfinity_Half_ReturnsTrue()
    {
        BinaryJsonNumber value = new(Half.PositiveInfinity);
        Assert.IsTrue(BinaryJsonNumber.IsPositiveInfinity(value));
    }

    [TestMethod]
    public void IsPositiveInfinity_Half_NegativeInfinity_ReturnsFalse()
    {
        BinaryJsonNumber value = new(Half.NegativeInfinity);
        Assert.IsFalse(BinaryJsonNumber.IsPositiveInfinity(value));
    }

    [TestMethod]
    public void IsPositiveInfinity_Single_ReturnsTrue()
    {
        BinaryJsonNumber value = new(float.PositiveInfinity);
        Assert.IsTrue(BinaryJsonNumber.IsPositiveInfinity(value));
    }

    [TestMethod]
    public void IsPositiveInfinity_Single_NegativeInfinity_ReturnsFalse()
    {
        BinaryJsonNumber value = new(float.NegativeInfinity);
        Assert.IsFalse(BinaryJsonNumber.IsPositiveInfinity(value));
    }

    [TestMethod]
    public void IsPositiveInfinity_Integer_ReturnsFalse()
    {
        BinaryJsonNumber value = new(42);
        Assert.IsFalse(BinaryJsonNumber.IsPositiveInfinity(value));
    }

    [TestMethod]
    public void IsSubnormal_Double_ReturnsTrue()
    {
        // double.Epsilon is the smallest positive subnormal double
        BinaryJsonNumber value = new(double.Epsilon);
        Assert.IsTrue(BinaryJsonNumber.IsSubnormal(value));
    }

    [TestMethod]
    public void IsSubnormal_Double_Normal_ReturnsFalse()
    {
        BinaryJsonNumber value = new(1.0);
        Assert.IsFalse(BinaryJsonNumber.IsSubnormal(value));
    }

    [TestMethod]
    public void IsSubnormal_Half_ReturnsTrue()
    {
        // Smallest positive subnormal Half
        BinaryJsonNumber value = new(Half.Epsilon);
        Assert.IsTrue(BinaryJsonNumber.IsSubnormal(value));
    }

    [TestMethod]
    public void IsSubnormal_Half_Normal_ReturnsFalse()
    {
        BinaryJsonNumber value = new((Half)1.0);
        Assert.IsFalse(BinaryJsonNumber.IsSubnormal(value));
    }

    [TestMethod]
    public void IsSubnormal_Single_ReturnsTrue()
    {
        BinaryJsonNumber value = new(float.Epsilon);
        Assert.IsTrue(BinaryJsonNumber.IsSubnormal(value));
    }

    [TestMethod]
    public void IsSubnormal_Single_Normal_ReturnsFalse()
    {
        BinaryJsonNumber value = new(1.0f);
        Assert.IsFalse(BinaryJsonNumber.IsSubnormal(value));
    }

    [TestMethod]
    public void IsSubnormal_Integer_ReturnsFalse()
    {
        BinaryJsonNumber value = new(42);
        Assert.IsFalse(BinaryJsonNumber.IsSubnormal(value));
    }

    [TestMethod]
    public void IsNegativeInfinity_Double_ReturnsTrue()
    {
        BinaryJsonNumber value = new(double.NegativeInfinity);
        Assert.IsTrue(BinaryJsonNumber.IsNegativeInfinity(value));
    }

    [TestMethod]
    public void IsNegativeInfinity_Double_PositiveInfinity_ReturnsFalse()
    {
        BinaryJsonNumber value = new(double.PositiveInfinity);
        Assert.IsFalse(BinaryJsonNumber.IsNegativeInfinity(value));
    }

    [TestMethod]
    public void IsNormal_Double_ReturnsTrue()
    {
        BinaryJsonNumber value = new(1.0);
        Assert.IsTrue(BinaryJsonNumber.IsNormal(value));
    }

    [TestMethod]
    public void IsNormal_Double_Subnormal_ReturnsFalse()
    {
        BinaryJsonNumber value = new(double.Epsilon);
        Assert.IsFalse(BinaryJsonNumber.IsNormal(value));
    }

    [TestMethod]
    public void IsNaN_Double_ReturnsTrue()
    {
        BinaryJsonNumber value = new(double.NaN);
        Assert.IsTrue(BinaryJsonNumber.IsNaN(value));
    }

    [TestMethod]
    public void IsNaN_Double_Normal_ReturnsFalse()
    {
        BinaryJsonNumber value = new(1.0);
        Assert.IsFalse(BinaryJsonNumber.IsNaN(value));
    }

    [TestMethod]
    public void IsInfinity_Double_Positive_ReturnsTrue()
    {
        BinaryJsonNumber value = new(double.PositiveInfinity);
        Assert.IsTrue(BinaryJsonNumber.IsInfinity(value));
    }

    [TestMethod]
    public void IsInfinity_Double_Negative_ReturnsTrue()
    {
        BinaryJsonNumber value = new(double.NegativeInfinity);
        Assert.IsTrue(BinaryJsonNumber.IsInfinity(value));
    }

    [TestMethod]
    public void IsInfinity_Double_Finite_ReturnsFalse()
    {
        BinaryJsonNumber value = new(1.0);
        Assert.IsFalse(BinaryJsonNumber.IsInfinity(value));
    }

    [TestMethod]
    public void IsZero_Double_ReturnsTrue()
    {
        BinaryJsonNumber value = new(0.0);
        Assert.IsTrue(BinaryJsonNumber.IsZero(value));
    }

    [TestMethod]
    public void IsZero_Double_NonZero_ReturnsFalse()
    {
        BinaryJsonNumber value = new(1.0);
        Assert.IsFalse(BinaryJsonNumber.IsZero(value));
    }

    [TestMethod]
    public void IsZero_Int32_ReturnsTrue()
    {
        BinaryJsonNumber value = new(0);
        Assert.IsTrue(BinaryJsonNumber.IsZero(value));
    }

    [TestMethod]
    public void IsZero_Decimal_ReturnsTrue()
    {
        BinaryJsonNumber value = new(0m);
        Assert.IsTrue(BinaryJsonNumber.IsZero(value));
    }

    [TestMethod]
    public void IsNegative_Double_ReturnsTrue()
    {
        BinaryJsonNumber value = new(-1.0);
        Assert.IsTrue(BinaryJsonNumber.IsNegative(value));
    }

    [TestMethod]
    public void IsNegative_Double_Positive_ReturnsFalse()
    {
        BinaryJsonNumber value = new(1.0);
        Assert.IsFalse(BinaryJsonNumber.IsNegative(value));
    }

    [TestMethod]
    public void IsFinite_Double_ReturnsTrue()
    {
        BinaryJsonNumber value = new(1.0);
        Assert.IsTrue(BinaryJsonNumber.IsFinite(value));
    }

    [TestMethod]
    public void IsFinite_Double_Infinity_ReturnsFalse()
    {
        BinaryJsonNumber value = new(double.PositiveInfinity);
        Assert.IsFalse(BinaryJsonNumber.IsFinite(value));
    }

    [TestMethod]
    public void IsRealNumber_Always_ReturnsTrue()
    {
        BinaryJsonNumber value = new(42);
        Assert.IsTrue(BinaryJsonNumber.IsRealNumber(value));
    }

    [TestMethod]
    public void IsImaginaryNumber_Always_ReturnsFalse()
    {
        BinaryJsonNumber value = new(42);
        Assert.IsFalse(BinaryJsonNumber.IsImaginaryNumber(value));
    }

    [TestMethod]
    public void IsComplexNumber_Always_ReturnsFalse()
    {
        BinaryJsonNumber value = new(42);
        Assert.IsFalse(BinaryJsonNumber.IsComplexNumber(value));
    }

    [TestMethod]
    public void IsInteger_Int32_ReturnsTrue()
    {
        BinaryJsonNumber value = new(42);
        Assert.IsTrue(BinaryJsonNumber.IsInteger(value));
    }

    [TestMethod]
    public void IsInteger_Double_Fractional_ReturnsFalse()
    {
        BinaryJsonNumber value = new(3.14);
        Assert.IsFalse(BinaryJsonNumber.IsInteger(value));
    }

    [TestMethod]
    public void IsInteger_Double_Whole_ReturnsTrue()
    {
        BinaryJsonNumber value = new(3.0);
        Assert.IsTrue(BinaryJsonNumber.IsInteger(value));
    }

    [TestMethod]
    public void IsEvenInteger_Int32_Even_ReturnsTrue()
    {
        BinaryJsonNumber value = new(42);
        Assert.IsTrue(BinaryJsonNumber.IsEvenInteger(value));
    }

    [TestMethod]
    public void IsEvenInteger_Int32_Odd_ReturnsFalse()
    {
        BinaryJsonNumber value = new(43);
        Assert.IsFalse(BinaryJsonNumber.IsEvenInteger(value));
    }

    [TestMethod]
    public void IsOddInteger_Int32_Odd_ReturnsTrue()
    {
        BinaryJsonNumber value = new(43);
        Assert.IsTrue(BinaryJsonNumber.IsOddInteger(value));
    }

    [TestMethod]
    public void IsOddInteger_Int32_Even_ReturnsFalse()
    {
        BinaryJsonNumber value = new(42);
        Assert.IsFalse(BinaryJsonNumber.IsOddInteger(value));
    }

    [TestMethod]
    public void IsPositive_Double_ReturnsTrue()
    {
        BinaryJsonNumber value = new(1.0);
        Assert.IsTrue(BinaryJsonNumber.IsPositive(value));
    }

    [TestMethod]
    public void IsPositive_Double_Negative_ReturnsFalse()
    {
        BinaryJsonNumber value = new(-1.0);
        Assert.IsFalse(BinaryJsonNumber.IsPositive(value));
    }

    [TestMethod]
    public void IsCanonical_Always_ReturnsTrue()
    {
        BinaryJsonNumber value = new(42);
        Assert.IsTrue(BinaryJsonNumber.IsCanonical(value));
    }

    [TestMethod]
    public void MaxMagnitude_ReturnsLarger()
    {
        BinaryJsonNumber a = new(3.0);
        BinaryJsonNumber b = new(-5.0);
        BinaryJsonNumber result = BinaryJsonNumber.MaxMagnitude(a, b);
        Assert.AreEqual(new BinaryJsonNumber(-5.0), result);
    }

    [TestMethod]
    public void MinMagnitude_ReturnsSmaller()
    {
        BinaryJsonNumber a = new(3.0);
        BinaryJsonNumber b = new(-5.0);
        BinaryJsonNumber result = BinaryJsonNumber.MinMagnitude(a, b);
        Assert.AreEqual(new BinaryJsonNumber(3.0), result);
    }

    [TestMethod]
    public void MaxMagnitudeNumber_ReturnsLarger()
    {
        BinaryJsonNumber a = new(3.0);
        BinaryJsonNumber b = new(-5.0);
        BinaryJsonNumber result = BinaryJsonNumber.MaxMagnitudeNumber(a, b);
        Assert.AreEqual(new BinaryJsonNumber(-5.0), result);
    }

    [TestMethod]
    public void MinMagnitudeNumber_ReturnsSmaller()
    {
        BinaryJsonNumber a = new(3.0);
        BinaryJsonNumber b = new(-5.0);
        BinaryJsonNumber result = BinaryJsonNumber.MinMagnitudeNumber(a, b);
        Assert.AreEqual(new BinaryJsonNumber(3.0), result);
    }

    [TestMethod]
    public void Abs_Negative_ReturnsPositive()
    {
        BinaryJsonNumber value = new(-42.0);
        BinaryJsonNumber result = BinaryJsonNumber.Abs(value);
        Assert.AreEqual(new BinaryJsonNumber(42.0), result);
    }

    [TestMethod]
    public void Abs_Positive_ReturnsSame()
    {
        BinaryJsonNumber value = new(42.0);
        BinaryJsonNumber result = BinaryJsonNumber.Abs(value);
        Assert.AreEqual(new BinaryJsonNumber(42.0), result);
    }

    [TestMethod]
    public void Compare_SameKind_Equal()
    {
        BinaryJsonNumber a = new(42.0);
        BinaryJsonNumber b = new(42.0);
        Assert.AreEqual(0, BinaryJsonNumber.Compare(a, b));
    }

    [TestMethod]
    public void Compare_SameKind_LessThan()
    {
        BinaryJsonNumber a = new(10.0);
        BinaryJsonNumber b = new(20.0);
        Assert.IsTrue(BinaryJsonNumber.Compare(a, b) < 0);
    }

    [TestMethod]
    public void Compare_SameKind_GreaterThan()
    {
        BinaryJsonNumber a = new(20.0);
        BinaryJsonNumber b = new(10.0);
        Assert.IsTrue(BinaryJsonNumber.Compare(a, b) > 0);
    }

    [TestMethod]
    public void Compare_MixedKind_IntDouble()
    {
        BinaryJsonNumber a = new(42);
        BinaryJsonNumber b = new(42.0);
        Assert.AreEqual(0, BinaryJsonNumber.Compare(a, b));
    }

    [TestMethod]
    public void Equals_SameKind_Equal()
    {
        BinaryJsonNumber a = new(42.0);
        BinaryJsonNumber b = new(42.0);
        Assert.IsTrue(BinaryJsonNumber.Equals(a, b));
    }

    [TestMethod]
    public void Equals_MixedKind_Equal()
    {
        BinaryJsonNumber a = new(42);
        BinaryJsonNumber b = new(42.0);
        Assert.IsTrue(BinaryJsonNumber.Equals(a, b));
    }

    [TestMethod]
    public void Equals_MixedKind_NotEqual()
    {
        BinaryJsonNumber a = new(42);
        BinaryJsonNumber b = new(43.0);
        Assert.IsFalse(BinaryJsonNumber.Equals(a, b));
    }

    [TestMethod]
    public void ComparisonOperator_LessThan()
    {
        BinaryJsonNumber a = new(10.0);
        BinaryJsonNumber b = new(20.0);
        Assert.IsTrue(a < b);
    }

    [TestMethod]
    public void ComparisonOperator_GreaterThan()
    {
        BinaryJsonNumber a = new(20.0);
        BinaryJsonNumber b = new(10.0);
        Assert.IsTrue(a > b);
    }

    [TestMethod]
    public void ComparisonOperator_LessThanOrEqual()
    {
        BinaryJsonNumber a = new(10.0);
        BinaryJsonNumber b = new(10.0);
        Assert.IsTrue(a <= b);
    }

    [TestMethod]
    public void ComparisonOperator_GreaterThanOrEqual()
    {
        BinaryJsonNumber a = new(10.0);
        BinaryJsonNumber b = new(10.0);
        Assert.IsTrue(a >= b);
    }

    [TestMethod]
    public void IsMultipleOf_Exact_ReturnsTrue()
    {
        BinaryJsonNumber value = new(12.0);
        BinaryJsonNumber factor = new(3.0);
        Assert.IsTrue(BinaryJsonNumber.IsMultipleOf(value, factor));
    }

    [TestMethod]
    public void IsMultipleOf_NotExact_ReturnsFalse()
    {
        BinaryJsonNumber value = new(13.0);
        BinaryJsonNumber factor = new(3.0);
        Assert.IsFalse(BinaryJsonNumber.IsMultipleOf(value, factor));
    }

    [TestMethod]
    public void IsMultipleOf_MixedKind_ReturnsTrue()
    {
        BinaryJsonNumber value = new(12);
        BinaryJsonNumber factor = new(3.0);
        Assert.IsTrue(BinaryJsonNumber.IsMultipleOf(value, factor));
    }
}

#endif