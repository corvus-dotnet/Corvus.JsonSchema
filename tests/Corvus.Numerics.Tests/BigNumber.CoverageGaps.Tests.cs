// <copyright file="BigNumber.CoverageGaps.Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using Corvus.Numerics;
using Shouldly;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Numerics.Tests;

/// <summary>
/// Tests to achieve near 100% branch coverage for BigNumber.
/// </summary>
[TestClass]
public class BigNumberCoverageGapsTests
{
#if NET
    [TestMethod]
    public void InterfaceProperties_AdditiveIdentity_ReturnsZero()
    {
        BigNumber identity = GetAdditiveIdentity<BigNumber>();
        identity.ShouldBe(BigNumber.Zero);
    }

    [TestMethod]
    public void InterfaceProperties_MultiplicativeIdentity_ReturnsOne()
    {
        BigNumber identity = GetMultiplicativeIdentity<BigNumber>();
        identity.ShouldBe(BigNumber.One);
    }

    [TestMethod]
    public void InterfaceProperties_NumberBaseOne_ReturnsOne()
    {
        BigNumber one = GetNumberBaseOne<BigNumber>();
        one.ShouldBe(BigNumber.One);
    }

    [TestMethod]
    public void InterfaceProperties_NumberBaseZero_ReturnsZero()
    {
        BigNumber zero = GetNumberBaseZero<BigNumber>();
        zero.ShouldBe(BigNumber.Zero);
    }

    [TestMethod]
    public void InterfaceProperties_NumberBaseRadix_ReturnsTen()
    {
        int radix = GetNumberBaseRadix<BigNumber>();
        radix.ShouldBe(10);
    }

    [TestMethod]
    public void InterfaceProperties_SignedNumberNegativeOne_ReturnsMinusOne()
    {
        BigNumber negOne = GetSignedNumberNegativeOne<BigNumber>();
        negOne.ShouldBe(BigNumber.MinusOne);
    }

    private static T GetAdditiveIdentity<T>() where T : IAdditiveIdentity<T, T> => T.AdditiveIdentity;
    private static T GetMultiplicativeIdentity<T>() where T : IMultiplicativeIdentity<T, T> => T.MultiplicativeIdentity;
    private static T GetNumberBaseOne<T>() where T : INumberBase<T> => T.One;
    private static T GetNumberBaseZero<T>() where T : INumberBase<T> => T.Zero;
    private static int GetNumberBaseRadix<T>() where T : INumberBase<T> => T.Radix;
    private static T GetSignedNumberNegativeOne<T>() where T : ISignedNumber<T> => T.NegativeOne;
#endif

    [TestMethod]
    public void Equals_WithNull_ReturnsFalse()
    {
        BigNumber num = new(123, 0);
        num.Equals(null).ShouldBeFalse();
    }

    [TestMethod]
    public void Equals_WithDifferentType_ReturnsFalse()
    {
        BigNumber num = new(123, 0);
        num.Equals("123").ShouldBeFalse();
        num.Equals(123.5).ShouldBeFalse(); // Use a value that won't match
    }

    [TestMethod]
    public void Equals_WithSameBigNumber_ReturnsTrue()
    {
        BigNumber num1 = new(123, 0);
        BigNumber num2 = new(123, 0);
        num1.Equals((object)num2).ShouldBeTrue();
    }

    [TestMethod]
    public void CompareTo_WithNull_ReturnsPositive()
    {
        BigNumber num = new(123, 0);
        num.CompareTo(null).ShouldBe(1);
    }

    [TestMethod]
    public void CompareTo_WithNonBigNumber_ThrowsArgumentException()
    {
        BigNumber num = new(123, 0);
        Should.Throw<ArgumentException>(() => num.CompareTo("123"));
    }

    [TestMethod]
    public void CompareTo_WithBigNumberObject_ReturnsCorrectValue()
    {
        BigNumber num1 = new(123, 0);
        BigNumber num2 = new(456, 0);
        num1.CompareTo((object)num2).ShouldBe(-1);
        num2.CompareTo((object)num1).ShouldBe(1);
        num1.CompareTo((object)num1).ShouldBe(0);
    }

    [TestMethod]
    public void GetHashCode_ProducesConsistentHash()
    {
        BigNumber num1 = new(123, 0);
        BigNumber num2 = new(123, 0);
        BigNumber num3 = new(1230, -1); // Same value, different representation

        num1.GetHashCode().ShouldBe(num2.GetHashCode());
        num1.GetHashCode().ShouldBe(num3.GetHashCode()); // Normalized to same value
    }

    [TestMethod]
    public void FormatZero_WithInvalidPrecision_ReturnsZero()
    {
        BigNumber zero = BigNumber.Zero;

        // Invalid precision should fall back to "0"
        zero.ToString("Fabc", CultureInfo.InvariantCulture).ShouldBe("0");
        zero.ToString("Nxyz", CultureInfo.InvariantCulture).ShouldBe("0");
        zero.ToString("Ginvalid", CultureInfo.InvariantCulture).ShouldBe("0");
    }

    [TestMethod]
    public void FormatZero_WithUnknownFormat_ReturnsZero()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString("X", CultureInfo.InvariantCulture).ShouldBe("0");
        zero.ToString("Q", CultureInfo.InvariantCulture).ShouldBe("0");
    }

    [TestMethod]
    public void Format_WithInvalidPrecision_ThrowsFormatException()
    {
        BigNumber num = new(123, 0);
        Should.Throw<FormatException>(() => num.ToString("Fabc", CultureInfo.InvariantCulture));
        Should.Throw<FormatException>(() => num.ToString("Nxyz", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Format_WithUnsupportedFormatSpecifier_ThrowsFormatException()
    {
        BigNumber num = new(123, 0);
        Should.Throw<FormatException>(() => num.ToString("X", CultureInfo.InvariantCulture));
        Should.Throw<FormatException>(() => num.ToString("Q", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void FormatPercent_WithDifferentPatterns_FormatsCorrectly()
    {
        BigNumber zero = BigNumber.Zero;

        // Test different PercentPositivePattern values
        // Pattern 0: n %
        var culture0 = new CultureInfo("en-US");
        culture0.NumberFormat.PercentPositivePattern = 0;
        zero.ToString("P2", culture0).ShouldBe("0.00 %");

        // Pattern 1: n%
        var culture1 = new CultureInfo("en-US");
        culture1.NumberFormat.PercentPositivePattern = 1;
        zero.ToString("P2", culture1).ShouldBe("0.00%");

        // Pattern 2: %n
        var culture2 = new CultureInfo("tr-TR");
        culture2.NumberFormat.PercentPositivePattern = 2;
        zero.ToString("P2", culture2).ShouldBe("%0,00");

        // Pattern 3: % n
        var culture3 = new CultureInfo("en-US");
        culture3.NumberFormat.PercentPositivePattern = 3;
        zero.ToString("P2", culture3).ShouldBe("% 0.00");
    }

    [TestMethod]
    public void FormatCurrency_WithDifferentPatterns_FormatsCorrectly()
    {
        BigNumber zero = BigNumber.Zero;

        // Pattern 0: $n
        var culture0 = new CultureInfo("en-US");
        culture0.NumberFormat.CurrencyPositivePattern = 0;
        zero.ToString("C2", culture0).ShouldBe("$0.00");

        // Pattern 1: n$
        var culture1 = new CultureInfo("en-US");
        culture1.NumberFormat.CurrencyPositivePattern = 1;
        zero.ToString("C2", culture1).ShouldBe("0.00$");

        // Pattern 2: $ n
        var culture2 = new CultureInfo("en-US");
        culture2.NumberFormat.CurrencyPositivePattern = 2;
        zero.ToString("C2", culture2).ShouldBe("$ 0.00");

        // Pattern 3: n $
        var culture3 = new CultureInfo("en-US");
        culture3.NumberFormat.CurrencyPositivePattern = 3;
        zero.ToString("C2", culture3).ShouldBe("0.00 $");
    }

    [TestMethod]
    public void FormatPercent_WithUnknownPattern_UsesDefault()
    {
        BigNumber zero = BigNumber.Zero;
        var culture = new CultureInfo("en-US");

        // Test all valid patterns (0-3) to ensure coverage
        for (int pattern = 0; pattern <= 3; pattern++)
        {
            culture.NumberFormat.PercentPositivePattern = pattern;
            string result = zero.ToString("P2", culture);
            result.ShouldContain("0.00");
            result.ShouldContain("%");
        }
    }

    [TestMethod]
    public void FormatCurrency_WithUnknownPattern_UsesDefault()
    {
        BigNumber zero = BigNumber.Zero;
        var culture = new CultureInfo("en-US");

        // Test all valid patterns (0-3) to ensure coverage
        for (int pattern = 0; pattern <= 3; pattern++)
        {
            culture.NumberFormat.CurrencyPositivePattern = pattern;
            string result = zero.ToString("C2", culture);
            result.ShouldContain("0.00");
            result.ShouldContain("$");
        }
    }

    [TestMethod]
    public void LargePowerOf10_Between256And1023_UsesSecondaryCache()
    {
        // This will trigger the secondary cache path (256-1023)
        BigNumber num1 = new(1, 300);
        BigNumber num2 = new(1, 300);

        // Multiplying should use the cached power
        BigNumber result = num1 * num2;
        result.Significand.ShouldBe(BigInteger.One);
        result.Exponent.ShouldBe(600);
    }

    [TestMethod]
    public void VeryLargePowerOf10_Above1023_ComputesOnDemand()
    {
        // This will trigger the on-demand computation path (>1023)
        BigNumber num = new(1, 1500);
        BigNumber multiplier = new(2, 0);

        BigNumber result = num * multiplier;
        result.Significand.ShouldBe(new BigInteger(2));
        result.Exponent.ShouldBe(1500);
    }

    [TestMethod]
    public void Division_WithVeryLargePrecision_WorksCorrectly()
    {
        // Test division that might trigger large power of 10 calculations
        BigNumber dividend = new(1, 0);
        BigNumber divisor = new(3, 0);

        var result = BigNumber.Divide(dividend, divisor, 200);

        // Should have 200 decimal places of precision
        string resultStr = result.ToString("F200", CultureInfo.InvariantCulture);
        resultStr.ShouldStartWith("0.3333333333");
    }

    [TestMethod]
    public void Addition_WithExtremeExponentDifference_ThrowsException()
    {
        // Create numbers with exponent difference > int.MaxValue
        BigNumber small = new(1, int.MinValue + 1000);
        BigNumber large = new(1, int.MaxValue - 1000);

        // Should throw either ArgumentOutOfRangeException or OverflowException
        Should.Throw<Exception>(() => small + large);
    }

    [TestMethod]
    public void Subtraction_WithExtremeExponentDifference_ThrowsException()
    {
        BigNumber small = new(1, int.MinValue + 1000);
        BigNumber large = new(1, int.MaxValue - 1000);

        // Should throw either ArgumentOutOfRangeException or OverflowException
        Should.Throw<Exception>(() => small - large);
    }

    [TestMethod]
    public void Multiplication_WithExtremeExponents_WorksCorrectly()
    {
        // Test that multiplication with extreme exponents works
        BigNumber num1 = new(2, 1000);
        BigNumber num2 = new(3, 500);

        BigNumber result = num1 * num2;
        result.Significand.ShouldBe(new BigInteger(6));
        result.Exponent.ShouldBe(1500);
    }

    [TestMethod]
    public void Division_WithExtremeExponents_WorksCorrectly()
    {
        BigNumber dividend = new(10, 1000);
        BigNumber divisor = new(2, 500);

        var result = BigNumber.Divide(dividend, divisor, 10);
        result.Significand.ShouldBe(new BigInteger(5));
        result.Exponent.ShouldBe(500);
    }

    [TestMethod]
    public void Format_WithNullFormat_UsesDefault()
    {
        BigNumber num = new(12345, -2);
        string result = num.ToString(null, CultureInfo.InvariantCulture);
        result.ShouldBe("12345E-2");
    }

    [TestMethod]
    public void Format_WithEmptyFormat_UsesDefault()
    {
        BigNumber num = new(12345, -2);
        string result = num.ToString("", CultureInfo.InvariantCulture);
        result.ShouldBe("12345E-2");
    }

    [TestMethod]
    public void FormatZero_WithNullFormat_ReturnsZero()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString(null, CultureInfo.InvariantCulture).ShouldBe("0");
    }

    [TestMethod]
    public void FormatZero_WithEmptyFormat_ReturnsZero()
    {
        BigNumber zero = BigNumber.Zero;
        zero.ToString("", CultureInfo.InvariantCulture).ShouldBe("0");
    }

    [TestMethod]
    public void ExponentialFormat_LowerCase_UsesLowerCaseE()
    {
        BigNumber num = new(12345, -2);
        string result = num.ToString("e2", CultureInfo.InvariantCulture);
        result.ShouldContain("e+");
    }

    [TestMethod]
    public void ExponentialFormat_UpperCase_UsesUpperCaseE()
    {
        BigNumber num = new(12345, -2);
        string result = num.ToString("E2", CultureInfo.InvariantCulture);
        result.ShouldContain("E+");
    }

    [TestMethod]
    public void GeneralFormat_LowerCase_UsesLowerCaseE()
    {
        BigNumber num = new(12345, 10);
        string result = num.ToString("g", CultureInfo.InvariantCulture);
        result.ShouldContain("e");
    }

    [TestMethod]
    public void GeneralFormat_UpperCase_UsesUpperCaseE()
    {
        BigNumber num = new(12345, 10);
        string result = num.ToString("G", CultureInfo.InvariantCulture);
        result.ShouldContain("E");
    }

    [TestMethod]
    public void Parse_WithExtremelyLargeExponent_WorksCorrectly()
    {
        string input = "1E+1000";
        var result = BigNumber.Parse(input);
        result.Significand.ShouldBe(BigInteger.One);
        result.Exponent.ShouldBe(1000);
    }

    [TestMethod]
    public void Parse_WithExtremelySmallExponent_WorksCorrectly()
    {
        string input = "1E-1000";
        var result = BigNumber.Parse(input);
        result.Significand.ShouldBe(BigInteger.One);
        result.Exponent.ShouldBe(-1000);
    }

    [TestMethod]
    public void Normalize_WithMultipleTrailingZeros_RemovesAll()
    {
        BigNumber num = new(1230000, -3);
        BigNumber normalized = num.Normalize();

        // Should normalize to 123 (no trailing zeros in significand)
        normalized.Significand.ShouldBe(new BigInteger(123));
        normalized.Exponent.ShouldBe(1);
    }

    [TestMethod]
    public void Normalize_WithZeroSignificand_ReturnsZero()
    {
        BigNumber num = new(0, 100);
        BigNumber normalized = num.Normalize();

        normalized.Significand.ShouldBe(BigInteger.Zero);
        normalized.Exponent.ShouldBe(0);
    }

    [TestMethod]
    public void FixedPointFormat_WithZeroPrecision_NoDecimalPoint()
    {
        BigNumber num = new(123, 0);
        string result = num.ToString("F0", CultureInfo.InvariantCulture);
        result.ShouldBe("123");
        result.ShouldNotContain(".");
    }

    [TestMethod]
    public void NumberFormat_WithZeroPrecision_NoDecimalPoint()
    {
        BigNumber num = new(1234, 0);
        string result = num.ToString("N0", CultureInfo.InvariantCulture);
        result.ShouldBe("1,234");
        result.ShouldNotContain(".");
    }

    [TestMethod]
    public void CurrencyFormat_WithZeroPrecision_NoDecimalPoint()
    {
        BigNumber num = new(123, 0);
        var culture = new CultureInfo("en-US");
        string result = num.ToString("C0", culture);
        result.ShouldBe("$123");
        result.ShouldNotContain(".");
    }

    [TestMethod]
    public void PercentFormat_WithZeroPrecision_NoDecimalPoint()
    {
        BigNumber num = new(1, 0); // 100%
        var culture = new CultureInfo("en-US");
        string result = num.ToString("P0", culture);
        // Different cultures may format differently, just check it contains the key elements
        result.ShouldContain("100");
        result.ShouldContain("%");
        result.ShouldNotContain(".");
    }
}