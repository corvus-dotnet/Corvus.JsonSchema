// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Xunit;

namespace Corvus.Text.Json.Tests.BigNumberTests;

/// <summary>
/// Provides test data and helper methods for BigNumber tests.
/// </summary>
public static class BigNumberTestData
{
    /// <summary>
    /// Test data for basic constructor scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int> BasicConstructorData =>
        new()
        {
            { BigInteger.Zero, 0 },
            { BigInteger.One, 0 },
            { new BigInteger(-1), 0 },
            { new BigInteger(123), 0 },
            { new BigInteger(-456), 0 },
            { BigInteger.Zero, 5 },
            { BigInteger.Zero, -5 },
            { new BigInteger(123), 10 },
            { new BigInteger(123), -10 },
            { new BigInteger(-456), 15 },
            { new BigInteger(-456), -15 },
        };

    /// <summary>
    /// Test data for normalization scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int> NormalizeData =>
        new()
        {
            // input significand, input exponent, expected significand, expected exponent
            { BigInteger.Zero, 0, BigInteger.Zero, 0 },
            { BigInteger.Zero, 5, BigInteger.Zero, 0 },
            { new BigInteger(123), 0, new BigInteger(123), 0 }, // No trailing zeros
            { new BigInteger(1200), 5, new BigInteger(12), 7 }, // 1200E5 -> 12E7
            { new BigInteger(34000), -2, new BigInteger(34), 1 }, // 34000E-2 -> 34E1
            { new BigInteger(-5670000), 10, new BigInteger(-567), 14 }, // -5670000E10 -> -567E14
            { new BigInteger(100000), 0, new BigInteger(1), 5 }, // 100000E0 -> 1E5
            { new BigInteger(-90000), -3, new BigInteger(-9), 1 }, // -90000E-3 -> -9E1
        };

    /// <summary>
    /// Test data for TryFormat scenarios with expected results.
    /// </summary>
    public static TheoryData<BigInteger, int, string> FormatData =>
        new()
        {
            // significand, exponent, expected string
            { BigInteger.Zero, 0, "0" },
            { BigInteger.Zero, 5, "0" },
            { new BigInteger(123), 0, "123" },
            { new BigInteger(-456), 0, "-456" },
            { new BigInteger(123), 5, "123E5" },
            { new BigInteger(123), -5, "123E-5" },
            { new BigInteger(-456), 10, "-456E10" },
            { new BigInteger(-456), -10, "-456E-10" },
            { BigInteger.Parse("12345678901234567890"), 0, "1234567890123456789E1" },
            { BigInteger.Parse("12345678901234567890"), 100, "1234567890123456789E101" },
            { BigInteger.Parse("-98765432109876543210"), -100, "-9876543210987654321E-99" },
        };

    /// <summary>
    /// Test data for TryParse scenarios.
    /// </summary>
    public static TheoryData<string, bool, BigInteger, int> ParseData =>
        new()
        {
            // json string, should succeed, expected significand, expected exponent
            { "0", true, BigInteger.Zero, 0 },
            { "123", true, new BigInteger(123), 0 },
            { "-456", true, new BigInteger(-456), 0 },
            { "1.23", true, new BigInteger(123), -2 },
            { "1.23e5", true, new BigInteger(123), 3 },
            { "1.23e+5", true, new BigInteger(123), 3 },
            { "1.23e-5", true, new BigInteger(123), -7 },
            { "1.23E5", true, new BigInteger(123), 3 },
            { "1.23E-5", true, new BigInteger(123), -7 },
            { "0.123", true, new BigInteger(123), -3 },
            { "0.0123", true, new BigInteger(123), -4 },
            { "12300", true, new BigInteger(123), 2 }, // Should normalize
            { "1.2300", true, new BigInteger(123), -2 }, // Should normalize
            { "1.2300e2", true, new BigInteger(123), 0 }, // Should normalize
            { "-0", true, BigInteger.Zero, 0 },
            { "12345678901234567890", true, BigInteger.Parse("1234567890123456789"), 1 },
            { "1.23456789012345678901234567890e50", true, BigInteger.Parse("12345678901234567890123456789"), 22 },
            { ".123", true, new BigInteger(123), -3 },
            { "123.", true, new BigInteger(123), 0 },

            // Invalid cases
            { "", false, BigInteger.Zero, 0 },
            { "abc", false, BigInteger.Zero, 0 },
            { "12.34.56", false, BigInteger.Zero, 0 },
            { "12e", false, BigInteger.Zero, 0 },
            { "12e+", false, BigInteger.Zero, 0 },
            { "12e-", false, BigInteger.Zero, 0 },
            { "e5", false, BigInteger.Zero, 0 },
        };

    /// <summary>
    /// Test data for equality scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int, bool> EqualityData =>
        new()
        {
            // left significand, left exponent, right significand, right exponent, should be equal
            { BigInteger.Zero, 0, BigInteger.Zero, 0, true },
            { BigInteger.Zero, 5, BigInteger.Zero, 5, true },
            { new BigInteger(123), 0, new BigInteger(123), 0, true },
            { new BigInteger(123), 5, new BigInteger(123), 5, true },
            { new BigInteger(-456), -10, new BigInteger(-456), -10, true },

            // Different values
            { new BigInteger(123), 0, new BigInteger(124), 0, false },
            { new BigInteger(123), 0, new BigInteger(123), 1, false },
            { new BigInteger(123), 5, new BigInteger(-123), 5, false },
            { BigInteger.Zero, 0, BigInteger.One, 0, false },
        };

    /// <summary>
    /// Test data for comparison scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int, int> ComparisonData =>
        new()
        {
            // s1, e1, s2, e2, expected sign of CompareTo
            // Equal
            { 123, 5, 123, 5, 0 },
            { -123, -5, -123, -5, 0 },
            { 0, 0, 0, 0, 0 },
            { 0, 5, 0, 10, 0 }, // 0 is 0 regardless of exponent

            // Greater
            { 124, 5, 123, 5, 1 }, // s1 > s2, e1 == e2
            { 123, 6, 123, 5, 1 }, // s1 == s2, e1 > e2 (1230 > 123)
            { 123, 5, -123, 5, 1 }, // positive > negative
            { 1, 0, 9, -1, 1 }, // 1 > 0.9
            { 1234, -2, 123, -1, 1 }, // 12.34 > 12.3
            { 999, -11, 1, -10, 1 }, // 9.99E-10 > 1E-10

            // Less
            { 123, 5, 124, 5, -1 }, // s1 < s2, e1 == e2
            { 123, 5, 123, 6, -1 }, // s1 == s2, e1 < e2 (123 < 1230)
            { -123, 5, 123, 5, -1 }, // negative < positive
            { 9, -1, 1, 0, -1 }, // 0.9 < 1
            { 123, -1, 1234, -2, -1 }, // 12.3 < 12.34
            { 1, -10, 999, -11, -1 }, // 1E-10 < 9.99E-10
        };

    /// <summary>
    /// Test data for addition scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int, BigInteger, int> AdditionData =>
        new()
        {
            // s1, e1, s2, e2, expectedS, expectedE
            { 1, 0, 2, 0, 3, 0 }, // 1 + 2 = 3
            { 123, 2, 45, 1, 1275, 1 }, // 12300 + 450 = 12750 -> 1275 E 1
            { 1, 0, -2, 0, -1, 0 }, // 1 + (-2) = -1
            { 1, 2, 1, -2, 10001, -2 }, // 100 + 0.01 = 100.01 -> 10001 E -2
            { 5, 0, -5, 0, 0, 0 }, // 5 + (-5) = 0
            { 0, 0, 123, 5, 123, 5 }, // 0 + 123E5 = 123E5
        };

    /// <summary>
    /// Test data for subtraction scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int, BigInteger, int> SubtractionData =>
        new()
        {
            // s1, e1, s2, e2, expectedS, expectedE
            { 3, 0, 2, 0, 1, 0 }, // 3 - 2 = 1
            { 123, 2, 45, 1, 1185, 1 }, // 12300 - 450 = 11850 -> 1185 E 1
            { 1, 0, -2, 0, 3, 0 }, // 1 - (-2) = 3
            { 1, 2, 1, -2, 9999, -2 }, // 100 - 0.01 = 99.99 -> 9999 E -2
            { 5, 0, 5, 0, 0, 0 }, // 5 - 5 = 0
            { 123, 5, 0, 0, 123, 5 }, // 123E5 - 0 = 123E5
        };

    /// <summary>
    /// Test data for multiplication scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int, BigInteger, int> MultiplicationData =>
        new()
        {
            // s1, e1, s2, e2, expectedS, expectedE
            { 2, 0, 3, 0, 6, 0 }, // 2 * 3 = 6
            { 12, 1, 3, 1, 36, 2 }, // 120 * 30 = 3600 -> 36 E 2
            { 15, 0, -2, 0, -3, 1 }, // 15 * -2 = -30 -> -3 E 1
            { 1, 2, 1, -2, 1, 0 }, // 100 * 0.01 = 1
            { 123, 5, 0, 0, 0, 0 }, // 123E5 * 0 = 0
        };

    /// <summary>
    /// Test data for division scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int, int, BigInteger, int> DivisionData =>
        new()
        {
            // s1, e1, s2, e2, precision, expectedS, expectedE
            { 6, 0, 2, 0, 10, 3, 0 }, // 6 / 2 = 3
            { 1, 0, 3, 0, 10, 3333333333, -10 }, // 1 / 3 = 0.333...
            { 10, 0, 4, 0, 10, 25, -1 }, // 10 / 4 = 2.5 -> 25 E -1
            { 1, 2, 2, 0, 10, 5, 1 }, // 100 / 2 = 50 -> 5 E 1
            { 0, 0, 123, 5, 10, 0, 0 }, // 0 / 123E5 = 0
        };

    /// <summary>
    /// Test data for modulo scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int, BigInteger, int> ModuloData =>
        new()
        {
            // s1, e1, s2, e2, expectedS, expectedE
            { 7, 0, 3, 0, 1, 0 }, // 7 % 3 = 1
            { 10, 0, 3, 0, 1, 0 }, // 10 % 3 = 1
            { 15, 0, 4, 0, 3, 0 }, // 15 % 4 = 3
            { 100, 0, 30, 0, 1, 1 }, // 100 % 30 = 10 -> 1 E 1 (normalized)
            { 1000, 0, 7, 0, 6, 0 }, // 1000 % 7 = 6
            { 5, 0, 10, 0, 5, 0 }, // 5 % 10 = 5
            { 0, 0, 5, 0, 0, 0 }, // 0 % 5 = 0
            { -7, 0, 3, 0, -1, 0 }, // -7 % 3 = -1 (BigInteger semantics)
            { 7, 0, -3, 0, 1, 0 }, // 7 % -3 = 1 (BigInteger semantics)
            { -7, 0, -3, 0, -1, 0 }, // -7 % -3 = -1 (BigInteger semantics)

            // With positive exponents
            { 123, 2, 45, 1, 15, 1 }, // 12300 % 450 = 150 -> 15 E 1
            { 1, 3, 3, 2, 1, 2 }, // 1000 % 300 = 100 -> 1 E 2
            { 5, 1, 2, 1, 1, 1 }, // 50 % 20 = 10 -> 1 E 1
            { 17, 0, 5, 0, 2, 0 }, // 17 % 5 = 2
            { 25, 1, 6, 1, 1, 1 }, // 250 % 60 = 10 -> 1 E 1

            // With negative exponents
            { 17, -1, 5, -1, 2, -1 }, // 1.7 % 0.5 = 0.2 -> 2 E -1
            { 35, -1, 1, 0, 5, -1 }, // 3.5 % 1 = 0.5 -> 5 E -1
            { 125, -2, 25, -2, 0, 0 }, // 1.25 % 0.25 = 0 (evenly divides)
            { 275, -2, 5, -1, 25, -2 }, // 2.75 % 0.5 = 0.25 -> 25 E -2
            { 1234, -3, 123, -3, 4, -3 }, // 1.234 % 0.123 = 0.004 -> 4 E -3
            { 999, -2, 1, -1, 9, -2 }, // 9.99 % 0.1 = 0.09 -> 9 E -2
            { 5, -1, 2, -1, 1, -1 }, // 0.5 % 0.2 = 0.1 -> 1 E -1
        };

    /// <summary>
    /// Test data for increment scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int> IncrementData =>
        new()
        {
            // input significand, input exponent, expected significand, expected exponent
            { 0, 0, 1, 0 }, // 0++ = 1
            { 1, 0, 2, 0 }, // 1++ = 2
            { -1, 0, 0, 0 }, // -1++ = 0
            { 99, 0, 1, 2 }, // 99++ = 100 -> 1 E 2 (normalized)
            { 999, 0, 1, 3 }, // 999++ = 1000 -> 1 E 3 (normalized)
            { 123, 2, 12301, 0 }, // 12300++ = 12301
            { -2, 0, -1, 0 }, // -2++ = -1

            // With negative exponents
            { 5, -1, 15, -1 }, // 0.5++ = 1.5 -> 15 E -1
            { 99, -2, 199, -2 }, // 0.99++ = 1.99 -> 199 E -2
            { -5, -1, 5, -1 }, // -0.5++ = 0.5 -> 5 E -1
            { 123, -3, 1123, -3 }, // 0.123++ = 1.123 -> 1123 E -3
            { -15, -1, -5, -1 }, // -1.5++ = -0.5 -> -5 E -1
            { 9, -1, 19, -1 }, // 0.9++ = 1.9 -> 19 E -1
            { -1, -2, 99, -2 }, // -0.01++ = 0.99 -> 99 E -2
        };

    /// <summary>
    /// Test data for decrement scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int> DecrementData =>
        new()
        {
            // input significand, input exponent, expected significand, expected exponent
            { 0, 0, -1, 0 }, // 0-- = -1
            { 1, 0, 0, 0 }, // 1-- = 0
            { 2, 0, 1, 0 }, // 2-- = 1
            { -1, 0, -2, 0 }, // -1-- = -2
            { 100, 0, 99, 0 }, // 100-- = 99
            { 1000, 0, 999, 0 }, // 1000-- = 999
            { 123, 2, 12299, 0 }, // 12300-- = 12299
            { 1, 2, 99, 0 }, // 100-- = 99

            // With negative exponents
            { 15, -1, 5, -1 }, // 1.5-- = 0.5 -> 5 E -1
            { 199, -2, 99, -2 }, // 1.99-- = 0.99 -> 99 E -2
            { 5, -1, -5, -1 }, // 0.5-- = -0.5 -> -5 E -1
            { 1123, -3, 123, -3 }, // 1.123-- = 0.123 -> 123 E -3
            { -5, -1, -15, -1 }, // -0.5-- = -1.5 -> -15 E -1
            { 19, -1, 9, -1 }, // 1.9-- = 0.9 -> 9 E -1
            { 1, -2, -99, -2 }, // 0.01-- = -0.99 -> -99 E -2
            { 25, -1, 15, -1 }, // 2.5-- = 1.5 -> 15 E -1
        };

    /// <summary>
    /// Test data for unary plus scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int> UnaryPlusData =>
        new()
        {
            // input significand, input exponent, expected significand, expected exponent
            { 0, 0, 0, 0 }, // +0 = 0
            { 1, 0, 1, 0 }, // +1 = 1
            { -1, 0, -1, 0 }, // +(-1) = -1
            { 123, 5, 123, 5 }, // +(123 E 5) = 123 E 5
            { -456, -10, -456, -10 }, // +(-456 E -10) = -456 E -10

            // Additional cases with negative exponents
            { 5, -1, 5, -1 }, // +(0.5) = 0.5
            { -25, -2, -25, -2 }, // +(-0.25) = -0.25
            { 999, -3, 999, -3 }, // +(0.999) = 0.999
            { 1, -5, 1, -5 }, // +(0.00001) = 0.00001
            { -123456, -4, -123456, -4 }, // +(-12.3456) = -12.3456
        };

    /// <summary>
    /// Test data for unary minus scenarios.
    /// </summary>
    public static TheoryData<BigInteger, int, BigInteger, int> UnaryMinusData =>
 new()
  {
            // input significand, input exponent, expected significand, expected exponent
    { 0, 0, 0, 0 }, // -0 = 0
   { 1, 0, -1, 0 }, // -1 = -1
            { -1, 0, 1, 0 }, // -(-1) = 1
    { 123, 5, -123, 5 }, // -(123 E 5) = -123 E 5
 { -456, -10, 456, -10 }, // -(-456 E -10) = 456 E -10
  { 999, 3, -999, 3 }, // -(999 E 3) = -999 E 3
         
            // Additional cases with negative exponents
            { 5, -1, -5, -1 }, // -(0.5) = -0.5
        { -25, -2, 25, -2 }, // -(-0.25) = 0.25
            { 999, -3, -999, -3 }, // -(0.999) = -0.999
    { 1, -5, -1, -5 }, // -(0.00001) = -0.00001
            { -123456, -4, 123456, -4 }, // -(-12.3456) = 12.3456
            { 12345, -6, -12345, -6 }, // -(0.012345) = -0.012345
 };

    /// <summary>
    /// Test data for extreme values.
    /// </summary>
    public static TheoryData<BigInteger, int> ExtremeValueData =>
        new()
        {
            { BigInteger.Parse("123456789012345678901234567890123456789012345678901234567890"), 0 },
            { BigInteger.Parse("-123456789012345678901234567890123456789012345678901234567890"), 0 },
            { new BigInteger(123), int.MaxValue },
            { new BigInteger(123), int.MinValue },
            { BigInteger.Parse("123456789012345678901234567890"), int.MaxValue },
            { BigInteger.Parse("-123456789012345678901234567890"), int.MinValue },
        };

    /// <summary>
    /// Helper method to convert string to UTF8 bytes.
    /// </summary>
    public static byte[] GetUtf8Bytes(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>
    /// Helper method to create a BigNumber for testing.
    /// </summary>
    public static Corvus.Numerics.BigNumber CreateBigNumber(BigInteger significand, int exponent) =>
        new(significand, exponent);

    /// <summary>
    /// Helper method to assert BigNumber equality with better error messages.
    /// </summary>
    public static void AssertBigNumberEqual(Corvus.Numerics.BigNumber expected, Corvus.Numerics.BigNumber actual, string message = "")
    {
        if (!string.IsNullOrEmpty(message))
        {
            message = $"{message}: ";
        }

        Assert.True(expected.Equals(actual),
            $"{message}Expected BigNumber({expected.Significand}, {expected.Exponent}) but got BigNumber({actual.Significand}, {actual.Exponent})");
    }

    /// <summary>
    /// Helper method to assert BigNumber equality with better error messages.
    /// </summary>
    public static void AssertBigNumbersEqual(Corvus.Numerics.BigNumber expected, Corvus.Numerics.BigNumber actual, string message = "")
    {
        if (!string.IsNullOrEmpty(message))
        {
            message = $"{message}: ";
        }

        Assert.True(expected.Equals(actual),
            $"{message}Expected BigNumber({GetSignificand(expected)}, {GetExponent(expected)}) but got BigNumber({GetSignificand(actual)}, {GetExponent(actual)})");
    }

    /// <summary>
    /// Helper method to assert BigNumber inequality with better error messages.
    /// </summary>
    public static void AssertBigNumbersNotEqual(Corvus.Numerics.BigNumber left, Corvus.Numerics.BigNumber right, string message = "")
    {
        if (!string.IsNullOrEmpty(message))
        {
            message = $"{message}: ";
        }

        Assert.False(left.Equals(right),
            $"{message}BigNumber({GetSignificand(left)}, {GetExponent(left)}) should not equal BigNumber({GetSignificand(right)}, {GetExponent(right)})");
    }

    /// <summary>
    /// Helper method to get the significand from a BigNumber (uses reflection or ToString parsing).
    /// </summary>
    public static BigInteger GetSignificand(Corvus.Numerics.BigNumber bigNumber)
    {
        // Since BigNumber might not expose Significand publicly, we'll use reflection or parse from string
        Type type = typeof(Corvus.Numerics.BigNumber);
        PropertyInfo significandProperty = type.GetProperty("Significand", BindingFlags.Public | BindingFlags.Instance);
        if (significandProperty != null)
        {
            return (BigInteger)significandProperty.GetValue(bigNumber)!;
        }

        // Fallback: try to get it from the string representation and parse it
        string str = bigNumber.ToString();
        if (str.Contains("E"))
        {
            string significandPart = str.Split('E')[0];
            return BigInteger.Parse(significandPart);
        }
        return BigInteger.Parse(str);
    }

    /// <summary>
    /// Helper method to get the exponent from a BigNumber (uses reflection or ToString parsing).
    /// </summary>
    public static int GetExponent(Corvus.Numerics.BigNumber bigNumber)
    {
        // Since BigNumber might not expose Exponent publicly, we'll use reflection or parse from string
        Type type = typeof(Corvus.Numerics.BigNumber);
        PropertyInfo exponentProperty = type.GetProperty("Exponent", BindingFlags.Public | BindingFlags.Instance);
        if (exponentProperty != null)
        {
            return (int)exponentProperty.GetValue(bigNumber)!;
        }

        // Fallback: try to get it from the string representation and parse it
        string str = bigNumber.ToString();
        if (str.Contains("E"))
        {
            string exponentPart = str.Split('E')[1];
            return int.Parse(exponentPart);
        }
        return 0; // No exponent in string means exponent is 0
    }

    /// <summary>
    /// Helper method to validate parsing result.
    /// </summary>
    public static void AssertParseResult(bool success, Corvus.Numerics.BigNumber result, BigInteger expectedSignificand, int expectedExponent, string input)
    {
        Assert.True(success, $"Should successfully parse: '{input}'");

        BigInteger actualSignificand = GetSignificand(result);
        int actualExponent = GetExponent(result);

        Assert.True(expectedSignificand.Equals(actualSignificand),
            $"Significand should match for input: '{input}'. Expected: {expectedSignificand}, Actual: {actualSignificand}");
        Assert.True(expectedExponent == actualExponent,
            $"Exponent should match for input: '{input}'. Expected: {expectedExponent}, Actual: {actualExponent}");
    }

    /// <summary>
    /// Helper method to validate formatting result.
    /// </summary>
    public static void AssertFormatResult(bool success, int charsWritten, string result, string expected, string scenario = "")
    {
        if (!string.IsNullOrEmpty(scenario))
        {
            scenario = $"{scenario}: ";
        }

        Assert.True(success, $"{scenario}TryFormat should have succeeded");
        Assert.True(expected.Length == charsWritten,
            $"{scenario}Characters written should match expected length. Expected: {expected.Length}, Actual: {charsWritten}");
        Assert.True(expected.Equals(result),
            $"{scenario}Formatted result should match expected. Expected: '{expected}', Actual: '{result}'");
    }

    /// <summary>
    /// Test data for invalid parse operations.
    /// </summary>
    public static TheoryData<string> InvalidParseData
    {
        get
        {
            var data = new TheoryData<string>
            {
                "",              // Empty string
                " ",             // Only whitespace
                "   ",           // Multiple whitespace
                "abc",           // Non-numeric
                "123.456",       // Decimal (not supported)
                "123.456E7",     // Decimal with exponent
                "123E",          // Incomplete exponent
                "E123",          // Missing significand
                "123EE5",        // Double exponent marker
                "123E5E",        // Trailing exponent marker
                "++123",         // Multiple signs
                "--123",         // Multiple negative signs
                "123E++5",       // Multiple signs in exponent
                "123E--5",       // Multiple negative signs in exponent
                "1.23.45",       // Multiple decimal points
                "123.E5",        // Decimal point without fractional digits
                ".123",          // Leading decimal point
                "123.",          // Trailing decimal point
                "123eE5",        // Mixed case exponent markers
                "123Ee5",        // Mixed case exponent markers
                "123e5e",        // Lowercase exponent with trailing 'e'
                "12 34",         // Space in number
                "12\t34",        // Tab in number
                "12\n34",        // Newline in number
                "12\r34",        // Carriage return in number
                "1,234",         // Comma separator
                "1_234",         // Underscore separator
                "$123",          // Currency symbol
                "123%",          // Percentage symbol
                "(123)",         // Parentheses
                "123f",          // Float suffix
                "123d",          // Double suffix
                "123m",          // Decimal suffix
                "0x123",         // Hexadecimal
                "0b101",         // Binary
                "∞",             // Infinity symbol
                "NaN",           // Not a Number
                "null",          // JSON null
                "true",          // JSON boolean
                "false",         // JSON boolean
                "\"123\"",       // Quoted number
                "[123]",         // Array notation
                "{\"value\":123}", // Object notation
            };
            return data;
        }
    }

    /// <summary>
    /// Gets test numbers for round-trip testing.
    /// </summary>
    /// <returns>A collection of test BigNumber instances.</returns>
    public static IEnumerable<Corvus.Numerics.BigNumber> GetTestNumbers()
    {
        yield return new Corvus.Numerics.BigNumber(BigInteger.Zero, 0);
        yield return new Corvus.Numerics.BigNumber(BigInteger.One, 0);
        yield return new Corvus.Numerics.BigNumber(BigInteger.MinusOne, 0);
        yield return new Corvus.Numerics.BigNumber(new BigInteger(123), 0);
        yield return new Corvus.Numerics.BigNumber(new BigInteger(123), 5);
        yield return new Corvus.Numerics.BigNumber(new BigInteger(123), -3);
        yield return new Corvus.Numerics.BigNumber(BigInteger.Parse("9999999999999999999999999999"), 0);
        yield return new Corvus.Numerics.BigNumber(BigInteger.Parse("1234567890123456789"), 10);
        yield return new Corvus.Numerics.BigNumber(BigInteger.Parse("1234567890123456789"), -10);
    }
}