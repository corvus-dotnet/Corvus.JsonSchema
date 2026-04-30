// <copyright file="BigNumber.Comparison.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberComparisonTests
{
    [Fact]
    public void Equality_WithNormalizedValues_ReturnsTrue()
    {
        BigNumber a = new(12300, -2);  // 123.00
        BigNumber b = new(123, 0);     // 123

        (a == b).ShouldBeTrue();
        a.Equals(b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_WithDifferentValues_ReturnsFalse()
    {
        BigNumber a = new(123, 0);
        BigNumber b = new(456, 0);

        (a == b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_WithZero_WorksCorrectly()
    {
        BigNumber zero1 = BigNumber.Zero;
        BigNumber zero2 = new(0, 0);
        BigNumber zero3 = new(0, -5);

        zero1.ShouldBe(zero2);
        zero1.ShouldBe(zero3);
    }

    [Fact]
    public void CompareTo_LessThan_ReturnsNegative()
    {
        BigNumber smaller = new(123, 0);
        BigNumber larger = new(456, 0);

        smaller.CompareTo(larger).ShouldBeLessThan(0);
        (smaller < larger).ShouldBeTrue();
        (smaller <= larger).ShouldBeTrue();
    }

    [Fact]
    public void CompareTo_GreaterThan_ReturnsPositive()
    {
        BigNumber larger = new(456, 0);
        BigNumber smaller = new(123, 0);

        larger.CompareTo(smaller).ShouldBeGreaterThan(0);
        (larger > smaller).ShouldBeTrue();
        (larger >= smaller).ShouldBeTrue();
    }

    [Fact]
    public void CompareTo_Equal_ReturnsZero()
    {
        BigNumber a = new(123, -2);
        BigNumber b = new(1230, -3);  // Same value, different representation

        a.CompareTo(b).ShouldBe(0);
        (a >= b).ShouldBeTrue();
        (a <= b).ShouldBeTrue();
    }

    [Fact]
    public void CompareTo_WithDifferentExponents_WorksCorrectly()
    {
        BigNumber a = new(123, 5);   // 12,300,000
        BigNumber b = new(456, 3);   // 456,000

        (a > b).ShouldBeTrue();
        a.CompareTo(b).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_NegativeNumbers_WorksCorrectly()
    {
        BigNumber negative = new(-123, 0);
        BigNumber positive = new(123, 0);

        (negative < positive).ShouldBeTrue();
        (negative < BigNumber.Zero).ShouldBeTrue();
        (positive > BigNumber.Zero).ShouldBeTrue();
    }

    [Fact]
    public void CompareTo_VeryLargeNumbers_WorksCorrectly()
    {
        // Numbers beyond decimal range
        BigNumber huge1 = new(BigInteger.Parse("999999999999999999999999999999"), 100);
        BigNumber huge2 = new(BigInteger.Parse("999999999999999999999999999998"), 100);

        (huge1 > huge2).ShouldBeTrue();
        huge1.CompareTo(huge2).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_VerySmallNumbers_WorksCorrectly()
    {
        // Numbers beyond decimal precision
        BigNumber tiny1 = new(123, -100);  // 123 × 10^-100
        BigNumber tiny2 = new(456, -100);  // 456 × 10^-100

        (tiny2 > tiny1).ShouldBeTrue();
        (tiny1 < tiny2).ShouldBeTrue();
    }

    [Fact]
    public void CompareTo_WithNull_ReturnsPositive()
    {
        BigNumber value = new(123, 0);

        value.CompareTo(null).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_WithNonBigNumber_ThrowsArgumentException()
    {
        BigNumber value = new(123, 0);

        Should.Throw<ArgumentException>(() => value.CompareTo("not a BigNumber"));
    }

    [Fact]
    public void GetHashCode_EqualValues_ReturnsSameHash()
    {
        BigNumber a = new(12300, -2);
        BigNumber b = new(123, 0);

        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_MayReturnDifferentHash()
    {
        BigNumber a = new(123, 0);
        BigNumber b = new(456, 0);

        // Hash codes for different values should typically be different
        // (though collisions are possible)
        int hashA = a.GetHashCode();
        int hashB = b.GetHashCode();

        // Just verify they can be computed without error
        hashA.ShouldNotBe(0);
        hashB.ShouldNotBe(0);
    }
}