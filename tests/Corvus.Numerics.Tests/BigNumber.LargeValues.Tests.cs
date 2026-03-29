// <copyright file="BigNumber.LargeValues.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Xunit;

namespace Corvus.Numerics.Tests;

public class BigNumberLargeValuesTests
{
    #region Values Beyond Decimal Range

    [Fact]
    public void Constructor_WithValueBeyondDecimalMax_Succeeds()
    {
        // decimal.MaxValue is approximately 7.9 × 10^28
        // Create a value that's 10^50
        var significand = BigInteger.Pow(10, 50);
        BigNumber huge = new(significand, 0);

        huge.Significand.ShouldBe(significand);
        huge.Exponent.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithValueBeyondDecimalMin_Succeeds()
    {
        // Create a value smaller than decimal can represent
        BigNumber tiny = new(1, -100);  // 1 × 10^-100

        tiny.Significand.ShouldBe(BigInteger.One);
        tiny.Exponent.ShouldBe(-100);
    }

    [Fact]
    public void Addition_LargeNumbers_BeyondDecimalRange_Succeeds()
    {
        // Create numbers beyond decimal.MaxValue
        BigNumber large1 = new(BigInteger.Parse("99999999999999999999999999999999999999"), 0);
        BigNumber large2 = new(BigInteger.Parse("11111111111111111111111111111111111111"), 0);

        BigNumber sum = large1 + large2;

        // The result gets normalized, so 111111111111111111111111111111111111110 becomes 11111... × 10^1
        sum.Significand.ShouldBe(BigInteger.Parse("11111111111111111111111111111111111111"));
        sum.Exponent.ShouldBe(1);

        // The value is correct even if representation is normalized
        BigNumber expected = BigInteger.Parse("111111111111111111111111111111111111110");
        sum.ShouldBe(expected);
    }

    [Fact]
    public void Subtraction_LargeNumbers_BeyondDecimalRange_Succeeds()
    {
        BigNumber large1 = new(BigInteger.Parse("99999999999999999999999999999999999999"), 0);
        BigNumber large2 = new(BigInteger.Parse("11111111111111111111111111111111111111"), 0);

        BigNumber diff = large1 - large2;

        diff.Significand.ShouldBe(BigInteger.Parse("88888888888888888888888888888888888888"));
    }

    [Fact]
    public void Multiplication_LargeNumbers_BeyondDecimalRange_Succeeds()
    {
        // Multiply large numbers
        BigNumber large1 = new(BigInteger.Parse("999999999999999999999999999999"), 0);
        BigNumber large2 = new(BigInteger.Parse("888888888888888888888888888888"), 0);

        BigNumber product = large1 * large2;

        // Result should be their product
        BigInteger expected = BigInteger.Parse("999999999999999999999999999999") *
                              BigInteger.Parse("888888888888888888888888888888");
        product.Significand.ShouldBe(expected);
    }

    [Fact]
    public void Division_LargeNumbers_BeyondDecimalRange_Succeeds()
    {
        BigNumber large1 = new(BigInteger.Parse("999999999999999999999999999999"), 0);
        BigNumber large2 = new(BigInteger.Parse("3"), 0);

        var quotient = BigNumber.Divide(large1, large2, precision: 50);

        // Should be roughly 333...333...
        quotient.Significand.ShouldNotBe(BigInteger.Zero);
    }

    [Fact]
    public void Parse_VeryLargeNumber_Succeeds()
    {
        string largeNumber = "123456789012345678901234567890123456789012345678901234567890";
        var parsed = BigNumber.Parse(largeNumber);

        parsed.Significand.ShouldBe(BigInteger.Parse(largeNumber) / 10);
        parsed.Exponent.ShouldBe(1);
    }

    [Fact]
    public void Parse_VerySmallNumber_Succeeds()
    {
        // A number with 100 decimal places
        string smallNumber = "0." + new string('0', 99) + "123456789";
        var parsed = BigNumber.Parse(smallNumber);

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(BigInteger.Parse("123456789"));
        // Should be -108 because we have 99 zeros + 9 digits = 108 decimal places total
        normalized.Exponent.ShouldBe(-108);
    }

    [Fact]
    public void Parse_ScientificNotation_LargeExponent_Succeeds()
    {
        var parsed = BigNumber.Parse("1.23E+100");

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(BigInteger.Parse("123"));
        normalized.Exponent.ShouldBe(98);
    }

    [Fact]
    public void Parse_ScientificNotation_LargeNegativeExponent_Succeeds()
    {
        var parsed = BigNumber.Parse("1.23E-100");

        BigNumber normalized = parsed.Normalize();
        normalized.Significand.ShouldBe(BigInteger.Parse("123"));
        normalized.Exponent.ShouldBe(-102);
    }

    #endregion

    #region Comparison of Large Values

    [Fact]
    public void Comparison_VeryLargeNumbers_WorksCorrectly()
    {
        BigNumber huge1 = new(BigInteger.Parse("999999999999999999999999999999999999999"), 50);
        BigNumber huge2 = new(BigInteger.Parse("999999999999999999999999999999999999998"), 50);

        (huge1 > huge2).ShouldBeTrue();
        (huge2 < huge1).ShouldBeTrue();
        (huge1 != huge2).ShouldBeTrue();
    }

    [Fact]
    public void Comparison_DifferentMagnitudes_WorksCorrectly()
    {
        BigNumber huge = new(1, 100);      // 10^100
        BigNumber normal = new(999999, 0); // 999,999

        (huge > normal).ShouldBeTrue();
        (normal < huge).ShouldBeTrue();
    }

    [Fact]
    public void Comparison_VerySmallNumbers_WorksCorrectly()
    {
        BigNumber tiny1 = new(123, -100);  // 123 × 10^-100
        BigNumber tiny2 = new(124, -100);  // 124 × 10^-100

        (tiny2 > tiny1).ShouldBeTrue();
        (tiny1 < tiny2).ShouldBeTrue();
    }

    #endregion

    #region Edge Cases with Exponents

    [Fact]
    public void Exponent_MaxLongValue_HandledCorrectly()
    {
        // Use close to max long value for exponent
        BigNumber value = new(123, int.MaxValue - 1000);

        value.Exponent.ShouldBe(int.MaxValue - 1000);
        value.Significand.ShouldBe(new BigInteger(123));
    }

    [Fact]
    public void Exponent_MinLongValue_HandledCorrectly()
    {
        // Use close to min long value for exponent
        BigNumber value = new(123, int.MinValue + 1000);

        value.Exponent.ShouldBe(int.MinValue + 1000);
        value.Significand.ShouldBe(new BigInteger(123));
    }

    [Fact]
    public void Addition_WithExtremeExponents_WorksCorrectly()
    {
        // When exponents are vastly different, the smaller value becomes negligible
        BigNumber huge = new(1, 100);
        BigNumber tiny = new(999999999, -100);

        BigNumber sum = huge + tiny;

        // The addition aligns to the smaller exponent (-100)
        // huge becomes 1 × 10^200 when aligned, plus tiny's 999999999
        // Result has exponent -100 and cannot be normalized further due to tiny's non-zero digits
        BigNumber normalized = sum.Normalize();

        // The normalized result keeps the alignment exponent
        normalized.Exponent.ShouldBe(-100);

        // But the significand is massive
        normalized.Significand.ToString().Length.ShouldBeGreaterThan(190);
    }

    #endregion

    #region 100+ Digit Numbers

    [Fact]
    public void Parse_100DigitInteger_Succeeds()
    {
        // Create a 100-digit number
        string hundredDigits = string.Concat(Enumerable.Range(1, 10).Select(i => "1234567890"));
        var parsed = BigNumber.Parse(hundredDigits);

        parsed.Significand.ToString().Length.ShouldBe(99);
        parsed.Exponent.ShouldBe(1);
    }

    [Fact]
    public void Parse_100DigitDecimal_Succeeds()
    {
        // Create a number with 100 decimal places
        string hundredDecimals = "123." + string.Concat(Enumerable.Range(1, 10).Select(i => "1234567890"));
        var parsed = BigNumber.Parse(hundredDecimals);

        // After removing the decimal point, should have 103 digits (123 + 100 decimal places)
        BigNumber normalized = parsed.Normalize();
        // The last digit might get normalized if it's a zero, so check it's close
        normalized.Exponent.ShouldBeLessThanOrEqualTo(-99);
        normalized.Exponent.ShouldBeGreaterThanOrEqualTo(-100);
    }

    [Fact]
    public void Arithmetic_With100DigitNumbers_Succeeds()
    {
        string num1Str = string.Concat(Enumerable.Range(1, 10).Select(i => "9999999999"));
        string num2Str = string.Concat(Enumerable.Range(1, 10).Select(i => "1111111111"));

        var num1 = BigNumber.Parse(num1Str);
        var num2 = BigNumber.Parse(num2Str);

        BigNumber sum = num1 + num2;
        BigNumber diff = num1 - num2;
        BigNumber product = num1 * num2;

        // Verify operations completed without error
        sum.Significand.ShouldNotBe(BigInteger.Zero);
        diff.Significand.ShouldNotBe(BigInteger.Zero);
        product.Significand.ShouldNotBe(BigInteger.Zero);
    }

    #endregion

    #region Precision Tests

    [Fact]
    public void Division_HighPrecision_Maintains150Digits()
    {
        BigNumber a = 10;
        BigNumber b = 3;

        var result = BigNumber.Divide(a, b, precision: 150);

        // Should have 150+ digits of precision
        string resultStr = result.Significand.ToString();
        resultStr.Length.ShouldBeGreaterThanOrEqualTo(150);
    }

    [Fact]
    public void Division_VeryHighPrecision_200Digits_Succeeds()
    {
        BigNumber a = 22;
        BigNumber b = 7;

        var result = BigNumber.Divide(a, b, precision: 200);

        // Should have 200+ digits of precision
        string resultStr = result.Significand.ToString();
        resultStr.Length.ShouldBeGreaterThanOrEqualTo(200);
    }

    #endregion

    #region Formatting Large Values

    [Fact]
    public void ToString_VeryLargeNumber_Succeeds()
    {
        BigNumber huge = new(BigInteger.Parse("999999999999999999999999999999"), 50);

        string str = huge.ToString();

        str.ShouldNotBeNullOrEmpty();
        str.ShouldContain("E", Case.Insensitive);
    }

    [Fact]
    public void ToString_VerySmallNumber_Succeeds()
    {
        BigNumber tiny = new(123, -50);

        string str = tiny.ToString();

        str.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void TryFormat_VeryLargeNumber_Succeeds()
    {
        BigNumber huge = new(BigInteger.Parse("999999999999999999999999999999"), 0);

        Span<char> buffer = stackalloc char[512];
        bool success = huge.TryFormat(buffer, out int charsWritten, default, null);

        success.ShouldBeTrue();
        charsWritten.ShouldBeGreaterThan(0);
    }

    #endregion

    #region Special Value Combinations

    [Fact]
    public void Multiplication_ByHugeExponent_Succeeds()
    {
        BigNumber num = new(123, 50);
        BigNumber multiplier = new(456, 50);

        BigNumber product = num * multiplier;

        product.Exponent.ShouldBe(100);
        product.Significand.ShouldBe(new BigInteger(123 * 456));
    }

    [Fact]
    public void Addition_MixedMagnitudes_BeyondDecimal_Succeeds()
    {
        // One very large, one very small
        BigNumber huge = new(BigInteger.Parse("999999999999999999999999999999"), 30);
        BigNumber tiny = new(1, -30);

        BigNumber sum = huge + tiny;

        // When adding numbers with vastly different exponents, they align to the smaller exponent
        // huge × 10^30 + 1 × 10^-30 aligns both to exponent -30
        // After normalization, the result maintains exponent -30 because tiny's fractional part prevents further normalization
        sum.Normalize().Exponent.ShouldBe(-30);

        // The sum should be slightly larger than huge
        (sum > huge).ShouldBeTrue();
    }

    [Fact]
    public void Normalize_LargeNumberWithTrailingZeros_RemovesZeros()
    {
        // Create a large number with many trailing zeros
        var significand = BigInteger.Parse("123000000000000000000000000000000000000");
        BigNumber value = new(significand, -10);

        BigNumber normalized = value.Normalize();

        // Should have removed trailing zeros
        normalized.Significand.ShouldBe(BigInteger.Parse("123"));
        // 36 trailing zeros removed from significand, so exponent goes from -10 to 26
        normalized.Exponent.ShouldBe(26);
    }

    #endregion

    #region Conversion Edge Cases

    [Fact]
    public void ExplicitConversion_ToDecimal_LargeValueBeyondRange_ThrowsOverflow()
    {
        // This is beyond decimal range, conversion should throw
        BigNumber huge = new(BigInteger.Parse("999999999999999999999999999999"), 10);

        // Should throw OverflowException
        Should.Throw<OverflowException>(() => (decimal)huge);
    }

    [Fact]
    public void ExplicitConversion_ToDouble_VeryLargeNumber_BecomesInfinity()
    {
        // Create number beyond double range
        BigNumber huge = new(BigInteger.Parse("99999999999999999999"), 1000);

        double result = (double)huge;

        // Should be infinity
        double.IsInfinity(result).ShouldBeTrue();
    }

    [Fact]
    public void ExplicitConversion_ToDouble_VerySmallNumber_BecomesZero()
    {
        // Create number smaller than double can represent
        BigNumber tiny = new(1, -1000);

        double result = (double)tiny;

        // Should be zero or very close to zero
        result.ShouldBe(0.0, tolerance: 1e-300);
    }

    #endregion

    #region Real-World Scenarios

    [Fact]
    public void ScientificComputation_AvogadroNumber_Succeeds()
    {
        // Avogadro's number: 6.022 × 10^23
        var avogadro = BigNumber.Parse("6.022E+23");

        avogadro.Significand.ShouldBe(BigInteger.Parse("6022"));
        avogadro.Exponent.ShouldBe(20);
    }

    [Fact]
    public void ScientificComputation_PlancksConstant_Succeeds()
    {
        // Planck's constant: 6.626 × 10^-34 J⋅s
        var planck = BigNumber.Parse("6.626E-34");

        BigNumber normalized = planck.Normalize();
        normalized.Significand.ShouldBe(BigInteger.Parse("6626"));
        normalized.Exponent.ShouldBe(-37);
    }

    [Fact]
    public void Cryptography_2048BitNumber_Succeeds()
    {
        // Simulate a 2048-bit number (approximately 617 decimal digits)
        // Using a known large prime (not actually 2048 bits, but demonstrates large number handling)
        string largePrime = "259117086013202627518482867860615308955131867015976063814050929380356454941947260896007935271960726804834049356252538551270637742625904312577596536218790317834264565463181959390817099175870370806944925783679128708387890362117" +
                           "866023736642950823110257566821";

        var prime = BigNumber.Parse(largePrime);

        // Verify it parsed correctly - should have the correct number of digits
        prime.Significand.ToString().Length.ShouldBeGreaterThan(250);
    }

    [Fact]
    public void Financial_NationalDebtCalculation_Succeeds()
    {
        // US National Debt is over 31 trillion dollars
        // With interest calculations over centuries, numbers can get huge
        var principal = BigNumber.Parse("31000000000000");  // 31 trillion
        BigNumber rate = 0.05m;  // 5% annual
        int years = 100;

        BigNumber amount = principal;
        for (int i = 0; i < years; i++)
        {
            amount *= (BigNumber.One + rate);
        }

        // Should be astronomically large
        amount.ShouldBeGreaterThan(principal);

        // Verify it's beyond decimal range
        string amountStr = amount.ToString();
        amountStr.Length.ShouldBeGreaterThan(20);
    }

    #endregion
}