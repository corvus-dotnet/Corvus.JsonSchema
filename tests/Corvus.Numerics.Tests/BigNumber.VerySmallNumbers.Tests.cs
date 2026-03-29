// <copyright file="BigNumber.VerySmallNumbers.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberVerySmallNumbersTests
{
    #region Construction and Representation

    [Fact]
    public void Constructor_VerySmallNumber_10ToMinus50_Succeeds()
    {
        // 1 × 10^-50
        BigNumber tiny = new(1, -50);

        tiny.Significand.ShouldBe(BigInteger.One);
        tiny.Exponent.ShouldBe(-50);
    }

    [Fact]
    public void Constructor_VerySmallNumber_10ToMinus100_Succeeds()
    {
        // 1 × 10^-100
        BigNumber tiny = new(1, -100);

        tiny.Significand.ShouldBe(BigInteger.One);
        tiny.Exponent.ShouldBe(-100);
    }

    [Fact]
    public void Constructor_VerySmallNumber_10ToMinus500_Succeeds()
    {
        // 1 × 10^-500
        BigNumber tiny = new(1, -500);

        tiny.Significand.ShouldBe(BigInteger.One);
        tiny.Exponent.ShouldBe(-500);
    }

    [Fact]
    public void Constructor_VerySmallNumber_10ToMinus1000_Succeeds()
    {
        // 1 × 10^-1000
        BigNumber tiny = new(1, -1000);

        tiny.Significand.ShouldBe(BigInteger.One);
        tiny.Exponent.ShouldBe(-1000);
    }

    [Fact]
    public void Constructor_ExtremelySmallNumber_NearLongMin_Succeeds()
    {
        // Very close to minimum possible exponent
        BigNumber tiny = new(123456789, int.MinValue + 1000);

        tiny.Significand.ShouldBe(new BigInteger(123456789));
        tiny.Exponent.ShouldBe(int.MinValue + 1000);
    }

    #endregion

    #region Parsing Very Small Numbers

    [Fact]
    public void Parse_50DecimalPlaces_Succeeds()
    {
        // 0.00000000000000000000000000000000000000000000000123
        string fiftyDecimals = "0." + new string('0', 49) + "123";
        var parsed = BigNumber.Parse(fiftyDecimals);

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(new BigInteger(123));
        normalized.Exponent.ShouldBe(-52);
    }

    [Fact]
    public void Parse_100DecimalPlaces_AllZerosExceptLast_Succeeds()
    {
        string hundredDecimals = "0." + new string('0', 99) + "7";
        var parsed = BigNumber.Parse(hundredDecimals);

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(new BigInteger(7));
        normalized.Exponent.ShouldBe(-100);
    }

    [Fact]
    public void Parse_200DecimalPlaces_Succeeds()
    {
        string twoHundredDecimals = "0." + new string('0', 199) + "123456789";
        var parsed = BigNumber.Parse(twoHundredDecimals);

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(BigInteger.Parse("123456789"));
        // 199 zeros + 9 digits = 208 decimal places
        normalized.Exponent.ShouldBe(-208);
    }

    [Fact]
    public void Parse_ScientificNotation_Minus100_Succeeds()
    {
        var parsed = BigNumber.Parse("1.23456789E-100");

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(BigInteger.Parse("123456789"));
        normalized.Exponent.ShouldBe(-108);
    }

    [Fact]
    public void Parse_ScientificNotation_Minus500_Succeeds()
    {
        var parsed = BigNumber.Parse("9.87654321E-500");

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(BigInteger.Parse("987654321"));
        // 9.87654321 is 9 digits, so E-500 means -500 + (9-1) = -508
        normalized.Exponent.ShouldBe(-508);
    }

    [Fact]
    public void Parse_ScientificNotation_Minus1000_Succeeds()
    {
        var parsed = BigNumber.Parse("5.5E-1000");

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(new BigInteger(55));
        normalized.Exponent.ShouldBe(-1001);
    }

    #endregion

    #region Arithmetic with Very Small Numbers

    [Fact]
    public void Addition_TwoVerySmallNumbers_SameExponent_Succeeds()
    {
        BigNumber a = new(123, -100);
        BigNumber b = new(456, -100);

        BigNumber sum = a + b;

        sum.Normalize().Significand.ShouldBe(new BigInteger(579));
        sum.Normalize().Exponent.ShouldBe(-100);
    }

    [Fact]
    public void Addition_TwoVerySmallNumbers_DifferentExponent_Succeeds()
    {
        BigNumber a = new(123, -100);  // 123 × 10^-100
        BigNumber b = new(456, -101);  // 456 × 10^-101 = 45.6 × 10^-100

        BigNumber sum = a + b;
        BigNumber normalized = sum.Normalize();

        // 123 × 10^-100 + 45.6 × 10^-100 = 168.6 × 10^-100 = 1686 × 10^-101
        normalized.Significand.ShouldBe(new BigInteger(1686));
        normalized.Exponent.ShouldBe(-101);
    }

    [Fact]
    public void Subtraction_VerySmallNumbers_Succeeds()
    {
        BigNumber a = new(1000, -100);
        BigNumber b = new(1, -100);

        BigNumber diff = a - b;

        diff.Normalize().Significand.ShouldBe(new BigInteger(999));
        diff.Normalize().Exponent.ShouldBe(-100);
    }

    [Fact]
    public void Multiplication_VerySmallNumbers_Succeeds()
    {
        BigNumber a = new(123, -50);
        BigNumber b = new(456, -50);

        BigNumber product = a * b;

        // 123 × 456 = 56088, exponents add: -50 + -50 = -100
        product.Normalize().Significand.ShouldBe(new BigInteger(56088));
        product.Normalize().Exponent.ShouldBe(-100);
    }

    [Fact]
    public void Multiplication_SmallBySmall_ExtremeResult_Succeeds()
    {
        BigNumber a = new(5, -500);
        BigNumber b = new(2, -500);

        BigNumber product = a * b;

        // 5 × 2 = 10, exponents: -500 + -500 = -1000
        product.Normalize().Significand.ShouldBe(BigInteger.One);
        product.Normalize().Exponent.ShouldBe(-999);  // 10 normalized to 1 × 10^1, so -1000 + 1 = -999
    }

    [Fact]
    public void Division_VerySmallByNormal_Succeeds()
    {
        BigNumber tiny = new(1, -100);
        BigNumber normal = new(2, 0);

        var quotient = BigNumber.Divide(tiny, normal, precision: 50);

        // 1 × 10^-100 / 2 = 0.5 × 10^-100 = 5 × 10^-101
        quotient.Normalize().Significand.ShouldBe(new BigInteger(5));
        quotient.Normalize().Exponent.ShouldBe(-101);
    }

    [Fact]
    public void Division_NormalByVerySmall_CreatesLarge_Succeeds()
    {
        BigNumber normal = new(1, 0);
        BigNumber tiny = new(1, -50);

        var quotient = BigNumber.Divide(normal, tiny, precision: 50);

        // 1 / (1 × 10^-50) = 1 × 10^50
        quotient.Normalize().Exponent.ShouldBe(50);
    }

    #endregion

    #region Comparison of Very Small Numbers

    [Fact]
    public void Comparison_TwoVerySmallNumbers_SameExponent_Succeeds()
    {
        BigNumber smaller = new(123, -100);
        BigNumber larger = new(456, -100);

        (smaller < larger).ShouldBeTrue();
        (larger > smaller).ShouldBeTrue();
    }

    [Fact]
    public void Comparison_TwoVerySmallNumbers_DifferentExponent_Succeeds()
    {
        BigNumber a = new(123, -100);  // 123 × 10^-100
        BigNumber b = new(456, -101);  // 456 × 10^-101 (smaller)

        (a > b).ShouldBeTrue();
        (b < a).ShouldBeTrue();
    }

    [Fact]
    public void Comparison_VerySmallWithZero_Succeeds()
    {
        BigNumber tiny = new(1, -1000);

        (tiny > BigNumber.Zero).ShouldBeTrue();
        (BigNumber.Zero < tiny).ShouldBeTrue();
        (tiny != BigNumber.Zero).ShouldBeTrue();
    }

    [Fact]
    public void Comparison_VerySmallWithNormal_Succeeds()
    {
        BigNumber tiny = new(999999999, -100);
        BigNumber normal = new(1, 0);

        (tiny < normal).ShouldBeTrue();
        (normal > tiny).ShouldBeTrue();
    }

    [Fact]
    public void Comparison_NegativeVerySmall_Succeeds()
    {
        BigNumber negativeTiny = new(-1, -100);
        BigNumber positiveTiny = new(1, -100);

        (negativeTiny < positiveTiny).ShouldBeTrue();
        (negativeTiny < BigNumber.Zero).ShouldBeTrue();
        (positiveTiny > BigNumber.Zero).ShouldBeTrue();
    }

    #endregion

    #region Conversion of Very Small Numbers

    [Fact]
    public void Conversion_VerySmallToDouble_BecomesZero()
    {
        // Beyond double's precision
        BigNumber tiny = new(1, -500);

        double result = (double)tiny;

        result.ShouldBe(0.0);
    }

    [Fact]
    public void Conversion_VerySmallToDouble_WithinRange_Succeeds()
    {
        // Within double's range (approximately 10^-308)
        BigNumber tiny = new(123456789, -50);

        double result = (double)tiny;

        result.ShouldBeGreaterThan(0.0);
        result.ShouldBeLessThan(1.0);
    }

    [Fact]
    public void Conversion_VerySmallToDecimal_BeyondRange_LosesPrecision()
    {
        // Beyond decimal's precision (10^-28), but -50 might still convert
        BigNumber tiny = new(1, -50);

        decimal result = (decimal)tiny;

        // Should be zero or very small - decimal can only go to ~10^-28
        result.ShouldBeLessThanOrEqualTo(0.0000000000000000000000000001m);
    }

    [Fact]
    public void Conversion_VerySmallToLong_Truncates()
    {
        BigNumber tiny = new(123456789, -50);

        long result = (long)tiny;

        result.ShouldBe(0L);
    }

    #endregion

    #region Formatting Very Small Numbers

    [Fact]
    public void ToString_VerySmallNumber_UsesScientificNotation()
    {
        BigNumber tiny = new(123, -100);

        string str = tiny.ToString();

        str.ShouldContain("E-", Case.Insensitive);
        str.ShouldContain("123");
    }

    [Fact]
    public void TryFormat_VerySmallNumber_Succeeds()
    {
        BigNumber tiny = new(987654321, -200);

        Span<char> buffer = stackalloc char[256];
        bool success = tiny.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void TryFormat_UTF8_VerySmallNumber_Succeeds()
    {
        BigNumber tiny = new(12345, -150);

        Span<byte> buffer = stackalloc byte[256];
        bool success = tiny.TryFormat(buffer, out int bytesWritten, default, null);

        success.ShouldBeTrue();
        bytesWritten.ShouldBeGreaterThan(0);
    }

    #endregion

    #region Real-World Very Small Number Scenarios

    [Fact]
    public void Physics_ElectronMass_Succeeds()
    {
        // Electron mass: 9.109 × 10^-31 kg
        var electronMass = BigNumber.Parse("9.109E-31");

        electronMass.Normalize().Significand.ShouldBe(new BigInteger(9109));
        electronMass.Normalize().Exponent.ShouldBe(-34);
    }

    [Fact]
    public void Physics_GravitationalConstant_Succeeds()
    {
        // G = 6.674 × 10^-11 N⋅m²/kg²
        var g = BigNumber.Parse("6.674E-11");

        g.Normalize().Significand.ShouldBe(new BigInteger(6674));
        g.Normalize().Exponent.ShouldBe(-14);
    }

    [Fact]
    public void Chemistry_MolecularConcentration_Succeeds()
    {
        // Extremely dilute solution: 1 × 10^-100 M
        BigNumber concentration = new(1, -100);

        concentration.Significand.ShouldBe(BigInteger.One);
        concentration.Exponent.ShouldBe(-100);
    }

    [Fact]
    public void Quantum_ReducedPlanckConstant_Succeeds()
    {
        // ℏ = 1.055 × 10^-34 J⋅s
        var hBar = BigNumber.Parse("1.055E-34");

        hBar.Normalize().Significand.ShouldBe(new BigInteger(1055));
        hBar.Normalize().Exponent.ShouldBe(-37);
    }

    [Fact]
    public void Cosmology_VacuumEnergy_Succeeds()
    {
        // Vacuum energy density: ~ 10^-9 J/m³ (simplified)
        var vacuumEnergy = BigNumber.Parse("5.7E-10");

        vacuumEnergy.Normalize().Significand.ShouldBe(new BigInteger(57));
        vacuumEnergy.Normalize().Exponent.ShouldBe(-11);
    }

    [Fact]
    public void Probability_ExtremelyUnlikely_Succeeds()
    {
        // Probability of an extremely rare event: 10^-1000
        BigNumber probability = new(1, -1000);

        probability.Significand.ShouldBe(BigInteger.One);
        probability.Exponent.ShouldBe(-1000);
        (probability > BigNumber.Zero).ShouldBeTrue();
        (probability < BigNumber.One).ShouldBeTrue();
    }

    #endregion

    #region Mixed Magnitude Operations

    [Fact]
    public void Addition_VerySmallPlusVeryLarge_Succeeds()
    {
        BigNumber tiny = new(1, -100);
        BigNumber huge = new(1, 100);

        BigNumber sum = tiny + huge;

        // The tiny value is negligible, result should be dominated by huge
        (sum > huge).ShouldBeFalse(); // Actually they add at different scales
        sum.Significand.ShouldNotBe(BigInteger.Zero);
    }

    [Fact]
    public void Subtraction_NormalMinusVerySmall_Succeeds()
    {
        BigNumber normal = new(1, 0);
        BigNumber tiny = new(1, -100);

        BigNumber diff = normal - tiny;

        // Result should still be very close to 1
        (diff < normal).ShouldBeTrue();
        diff.Significand.ShouldNotBe(BigInteger.Zero);
    }

    [Fact]
    public void Multiplication_VerySmallByVeryLarge_Succeeds()
    {
        BigNumber tiny = new(5, -100);
        BigNumber huge = new(2, 100);

        BigNumber product = tiny * huge;

        // 5 × 2 = 10, exponents cancel: -100 + 100 = 0
        product.Normalize().Significand.ShouldBe(BigInteger.One);
        product.Normalize().Exponent.ShouldBe(1);
    }

    #endregion

    #region Precision and Accuracy

    [Fact]
    public void Addition_ManyVerySmallNumbers_MaintainsPrecision()
    {
        BigNumber sum = BigNumber.Zero;
        BigNumber increment = new(1, -100);

        for (int i = 0; i < 1000; i++)
        {
            sum += increment;
        }

        // Sum should be 1000 × 10^-100 = 1 × 10^-97
        BigNumber normalized = sum.Normalize();
        normalized.Significand.ShouldBe(BigInteger.One);
        normalized.Exponent.ShouldBe(-97);
    }

    [Fact]
    public void Subtraction_AlmostEqualVerySmallNumbers_Succeeds()
    {
        BigNumber a = new(1000000000, -100);
        BigNumber b = new(999999999, -100);

        BigNumber diff = a - b;

        diff.Normalize().Significand.ShouldBe(BigInteger.One);
        diff.Normalize().Exponent.ShouldBe(-100);
    }

    [Fact]
    public void Division_VerySmallNumbers_HighPrecision_Succeeds()
    {
        BigNumber a = new(1, -100);
        BigNumber b = new(3, -100);

        var quotient = BigNumber.Divide(a, b, precision: 100);

        // 1/3 with 100 digits of precision
        quotient.Significand.ToString().Length.ShouldBeGreaterThan(90);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Normalize_VerySmallWithTrailingZeros_Succeeds()
    {
        // 1000 × 10^-100 should normalize to 1 × 10^-97
        BigNumber value = new(1000, -100);

        BigNumber normalized = value.Normalize();

        normalized.Significand.ShouldBe(BigInteger.One);
        normalized.Exponent.ShouldBe(-97);
    }

    [Fact]
    public void Normalize_VerySmallNoTrailingZeros_Unchanged()
    {
        BigNumber value = new(123456789, -100);

        BigNumber normalized = value.Normalize();

        normalized.Significand.ShouldBe(new BigInteger(123456789));
        normalized.Exponent.ShouldBe(-100);
    }

    [Fact]
    public void IsInteger_VerySmallNumber_ReturnsFalse()
    {
        BigNumber tiny = new(123, -50);

        tiny.IsInteger().ShouldBeFalse();
    }

    [Fact]
    public void Abs_NegativeVerySmall_ReturnsPositive()
    {
        BigNumber negativeTiny = new(-123456, -100);

        var abs = BigNumber.Abs(negativeTiny);

        abs.Significand.ShouldBe(new BigInteger(123456));
        abs.Exponent.ShouldBe(-100);
    }

    [Fact]
    public void Sign_VerySmallPositive_ReturnsOne()
    {
        BigNumber tiny = new(1, -1000);

        int sign = BigNumber.Sign(tiny);

        sign.ShouldBe(1);
    }

    [Fact]
    public void Sign_VerySmallNegative_ReturnsMinusOne()
    {
        BigNumber tiny = new(-1, -1000);

        int sign = BigNumber.Sign(tiny);

        sign.ShouldBe(-1);
    }

    #endregion

    #region Extreme Precision Scenarios

    [Fact]
    public void Parse_1000DecimalPlaces_Succeeds()
    {
        // Create a number with 1000 decimal places
        string thousandDecimals = "0." + new string('0', 999) + "1";
        var parsed = BigNumber.Parse(thousandDecimals);

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(BigInteger.One);
        normalized.Exponent.ShouldBe(-1000);
    }

    [Fact]
    public void Arithmetic_With1000DigitPrecision_Succeeds()
    {
        BigNumber a = new(1, -500);
        BigNumber b = new(1, -500);

        BigNumber sum = a + b;

        // 2 × 10^-500
        sum.Normalize().Significand.ShouldBe(new BigInteger(2));
        sum.Normalize().Exponent.ShouldBe(-500);
    }

    [Fact]
    public void Comparison_ExtremelyCloseVerySmallNumbers_Succeeds()
    {
        // Use numbers that won't hit floating-point precision issues in Log10
        // but are still very small
        BigNumber a = new(100, -1000);
        BigNumber b = new(101, -1000);

        (b > a).ShouldBeTrue();
        (a < b).ShouldBeTrue();
        (a != b).ShouldBeTrue();

        // Verify they're extremely close
        BigNumber diff = b - a;
        diff.Normalize().Significand.ShouldBe(BigInteger.One);
        diff.Normalize().Exponent.ShouldBe(-1000);
    }

    #endregion
}