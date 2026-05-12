// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonSchemaMatchingNumberTests
{
    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid Byte
    [DataRow(false, "255", "", 0, true)] // max Byte
    [DataRow(false, "256", "", 0, false)] // above max
    [DataRow(true, "1", "", 0, false)] // negative not allowed
    public void MatchByte_ValidatesByte(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchByte(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid Decimal
    [DataRow(false, "79228162514264337593543950335", "", 0, true)] // max
    [DataRow(true, "79228162514264337593543950335", "", 0, true)] // min
    [DataRow(false, "79228162514264337593543950336", "", 0, false)] // above max
    [DataRow(true, "79228162514264337593543950336", "", 0, false)] // below min
    // Normalized floating point
    [DataRow(false, "123", "", -2, true)]
    [DataRow(false, "1", "", -2, true)]
    [DataRow(false, "123", "45", -4, true)]
    [DataRow(false, "", "123", -2, true)]
    [DataRow(false, "1", "23", -2, true)]
    public void MatchDecimal_ValidatesDecimal(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchDecimal(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid Double
    [DataRow(false, "17976931348623157", "", 291, true)] // max
    [DataRow(true, "17976931348623157", "", 291, true)] // min
    [DataRow(false, "17976931348623158", "", 291, false)] // above max
    [DataRow(true, "17976931348623158", "", 291, false)] // below min
    // Normalized floating point: 123e-2 = 1.23, 1e-2 = 0.01, 1.23e-2 = 0.0123
    [DataRow(false, "123", "", -2, true)]   // 123e-2 = 1.23
    [DataRow(false, "1", "", -2, true)]     // 1e-2 = 0.01
    [DataRow(false, "123", "45", -4, true)] // 123.45e-4 = 0.012345
    [DataRow(false, "", "123", -2, true)]   // 0.123e-2 = 0.00123
    [DataRow(false, "1", "23", -2, true)]   // 1.23e-2 = 0.0123
    public void MatchDouble_ValidatesDouble(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchDouble(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid Half
    [DataRow(false, "65504", "", 0, true)] // max
    [DataRow(true, "65504", "", 0, true)] // min
    [DataRow(false, "65505", "", 0, false)] // above max
    [DataRow(true, "65505", "", 0, false)] // below min
    // Normalized floating point
    [DataRow(false, "123", "", -2, true)]
    [DataRow(false, "1", "", -2, true)]
    [DataRow(false, "123", "45", -4, true)]
    [DataRow(false, "", "123", -2, true)]
    [DataRow(false, "1", "23", -2, true)]
    public void MatchHalf_ValidatesHalf(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchHalf(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "123", "", -2, false)]
    [DataRow(false, "1", "", -2, false)]
    [DataRow(false, "123", "45", -4, false)]
    [DataRow(false, "", "123", -2, false)]
    [DataRow(false, "1", "23", -2, false)]
    public void MatchInt128_DoesNotMatchNormalizedFloatingPoint(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchInt128(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid Int128
    [DataRow(true, "170141183460469231731687303715884105728", "", 0, true)] // min
    [DataRow(false, "170141183460469231731687303715884105727", "", 0, true)] // max
    [DataRow(true, "170141183460469231731687303715884105729", "", 0, false)] // below min
    [DataRow(false, "170141183460469231731687303715884105728", "", 0, false)] // above max
    public void MatchInt128_ValidatesInt128(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchInt128(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "123", "", -2, false)]
    [DataRow(false, "1", "", -2, false)]
    [DataRow(false, "123", "45", -4, false)]
    [DataRow(false, "", "123", -2, false)]
    [DataRow(false, "1", "23", -2, false)]
    public void MatchInt16_DoesNotMatchNormalizedFloatingPoint(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchInt16(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid Int16
    [DataRow(true, "32768", "", 0, true)] // -32768 is min
    [DataRow(false, "32767", "", 0, true)] // 32767 is max
    [DataRow(true, "32769", "", 0, false)] // below min
    [DataRow(false, "32768", "", 0, false)] // above max
    public void MatchInt16_ValidatesInt16(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchInt16(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "123", "", -2, false)]
    [DataRow(false, "1", "", -2, false)]
    [DataRow(false, "123", "45", -4, false)]
    [DataRow(false, "", "123", -2, false)]
    [DataRow(false, "1", "23", -2, false)]
    public void MatchInt32_DoesNotMatchNormalizedFloatingPoint(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchInt32(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid Int32
    [DataRow(true, "2147483648", "", 0, true)] // -2147483648 is valid Int32 (min)
    [DataRow(false, "2147483647", "", 0, true)] // 2147483647 is valid Int32 (max)
    [DataRow(true, "2147483649", "", 0, false)] // -2147483649 is below min
    [DataRow(false, "2147483648", "", 0, false)] // 2147483648 is above max
    public void MatchInt32_ValidatesInt32(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchInt32(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "123", "", -2, false)]
    [DataRow(false, "1", "", -2, false)]
    [DataRow(false, "123", "45", -4, false)]
    [DataRow(false, "", "123", -2, false)]
    [DataRow(false, "1", "23", -2, false)]
    public void MatchInt64_DoesNotMatchNormalizedFloatingPoint(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchInt64(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid Int64
    [DataRow(true, "9223372036854775808", "", 0, true)] // -9223372036854775808 is min
    [DataRow(false, "9223372036854775807", "", 0, true)] // 9223372036854775807 is max
    [DataRow(true, "9223372036854775809", "", 0, false)] // below min
    [DataRow(false, "9223372036854775808", "", 0, false)] // above max
    public void MatchInt64_ValidatesInt64(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchInt64(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "123", "", -2, false)]
    [DataRow(false, "1", "", -2, false)]
    [DataRow(false, "123", "45", -4, false)]
    [DataRow(false, "", "123", -2, false)]
    [DataRow(false, "1", "23", -2, false)]
    public void MatchInt8_DoesNotMatchNormalizedFloatingPoint(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchSByte(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid SByte
    [DataRow(true, "128", "", 0, true)] // -128 is min
    [DataRow(false, "127", "", 0, true)] // 127 is max
    [DataRow(true, "129", "", 0, false)] // below min
    [DataRow(false, "128", "", 0, false)] // above max
    public void MatchSByte_ValidatesSByte(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchSByte(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid Single
    [DataRow(false, "340282346638528859", "", 20, true)] // max
    [DataRow(true, "340282346638528859", "", 20, true)] // min
    [DataRow(false, "340282346638528860", "", 20, false)] // above max
    [DataRow(true, "340282346638528860", "", 20, false)] // below min
    // Normalized floating point
    [DataRow(false, "123", "", -2, true)]
    [DataRow(false, "1", "", -2, true)]
    [DataRow(false, "123", "45", -4, true)]
    [DataRow(false, "", "123", -2, true)]
    [DataRow(false, "1", "23", -2, true)]
    public void MatchSingle_ValidatesSingle(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchSingle(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(JsonTokenType.Number, true)]
    [DataRow(JsonTokenType.String, false)]
    public void MatchTypeNumber_ValidatesTokenType(JsonTokenType tokenType, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchTypeNumber(tokenType, "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "123", "", -2, false)]
    [DataRow(false, "1", "", -2, false)]
    [DataRow(false, "123", "45", -4, false)]
    [DataRow(false, "", "123", -2, false)]
    [DataRow(false, "1", "23", -2, false)]
    public void MatchUInt128_DoesNotMatchNormalizedFloatingPoint(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchUInt128(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid UInt128
    [DataRow(false, "340282366920938463463374607431768211455", "", 0, true)] // max
    [DataRow(false, "340282366920938463463374607431768211456", "", 0, false)] // above max
    [DataRow(true, "1", "", 0, false)] // negative not allowed
    public void MatchUInt128_ValidatesUInt128(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchUInt128(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid UInt16
    [DataRow(false, "65535", "", 0, true)] // max UInt16
    [DataRow(false, "65536", "", 0, false)] // above max
    [DataRow(true, "1", "", 0, false)] // negative not allowed
    public void MatchUInt16_ValidatesUInt16(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchUInt16(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "123", "", -2, false)]
    [DataRow(false, "1", "", -2, false)]
    [DataRow(false, "123", "45", -4, false)]
    [DataRow(false, "", "123", -2, false)]
    [DataRow(false, "1", "23", -2, false)]
    public void MatchUInt32_DoesNotMatchNormalizedFloatingPoint(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchUInt32(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid UInt32
    [DataRow(false, "4294967295", "", 0, true)] // max UInt32
    [DataRow(false, "4294967296", "", 0, false)] // above max
    [DataRow(true, "1", "", 0, false)] // negative not allowed
    public void MatchUInt32_ValidatesUInt32(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchUInt32(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "123", "", -2, false)]
    [DataRow(false, "1", "", -2, false)]
    [DataRow(false, "123", "45", -4, false)]
    [DataRow(false, "", "123", -2, false)]
    [DataRow(false, "1", "23", -2, false)]
    public void MatchUInt64_DoesNotMatchNormalizedFloatingPoint(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchUInt64(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "0", "", 0, true)] // 0 is valid UInt64
    [DataRow(false, "18446744073709551615", "", 0, true)] // max UInt64
    [DataRow(false, "18446744073709551616", "", 0, false)] // above max
    [DataRow(true, "1", "", 0, false)] // negative not allowed
    public void MatchUInt64_ValidatesUInt64(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchUInt64(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(false, "123", "", -2, false)]
    [DataRow(false, "1", "", -2, false)]
    [DataRow(false, "123", "45", -4, false)]
    [DataRow(false, "", "123", -2, false)]
    [DataRow(false, "1", "23", -2, false)]
    public void MatchUInt8_DoesNotMatchNormalizedFloatingPoint(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchByte(
            isNegative,
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #region MatchMultipleOf (ulong overload)

    [TestMethod]
    [DataRow("6", "", 0, 3UL, 0, true)]   // 6 is a multiple of 3
    [DataRow("7", "", 0, 3UL, 0, false)]  // 7 is not a multiple of 3
    [DataRow("0", "", 0, 7UL, 0, true)]   // 0 is a multiple of anything
    [DataRow("12", "", 0, 4UL, 0, true)]  // 12 is a multiple of 4
    [DataRow("13", "", 0, 4UL, 0, false)] // 13 is not a multiple of 4
    [DataRow("15", "", -1, 3UL, -1, true)]  // 1.5 is a multiple of 0.3 (both scaled by 10)
    [DataRow("15", "", -1, 7UL, -1, false)] // 1.5 is not a multiple of 0.7
    public void MatchMultipleOf_ULong_Validates(string integral, string fractional, int exponent, ulong divisor, int divisorExponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchMultipleOf(
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            divisor,
            divisorExponent,
            divisor.ToString(),
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #endregion

    #region MatchMultipleOf (BigInteger overload)

    [TestMethod]
    [DataRow("12", "", 0, "3", 0, true)]   // 12 is a multiple of 3
    [DataRow("13", "", 0, "3", 0, false)]  // 13 is not a multiple of 3
    [DataRow("0", "", 0, "7", 0, true)]    // 0 is a multiple of anything
    [DataRow("99", "", 0, "33", 0, true)]  // 99 is a multiple of 33
    [DataRow("100", "", 0, "33", 0, false)] // 100 is not a multiple of 33
    public void MatchMultipleOf_BigInteger_Validates(string integral, string fractional, int exponent, string divisorStr, int divisorExponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchMultipleOf(
            System.Text.Encoding.UTF8.GetBytes(integral),
            System.Text.Encoding.UTF8.GetBytes(fractional),
            exponent,
            System.Numerics.BigInteger.Parse(divisorStr),
            divisorExponent,
            divisorStr,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #endregion

    #region MatchEquals

    [TestMethod]
    [DataRow(false, "42", "", 0, false, "42", "", 0, true)]   // 42 == 42
    [DataRow(false, "42", "", 0, false, "43", "", 0, false)]  // 42 != 43
    [DataRow(true, "5", "", 0, true, "5", "", 0, true)]       // -5 == -5
    [DataRow(true, "5", "", 0, false, "5", "", 0, false)]     // -5 != 5
    [DataRow(false, "1", "5", -1, false, "15", "", -1, true)] // 1.5 == 1.5 (different representation)
    public void MatchEquals_Validates(
        bool leftNeg, string leftInt, string leftFrac, int leftExp,
        bool rightNeg, string rightInt, string rightFrac, int rightExp,
        bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchEquals(
            leftNeg,
            System.Text.Encoding.UTF8.GetBytes(leftInt),
            System.Text.Encoding.UTF8.GetBytes(leftFrac),
            leftExp,
            rightNeg,
            System.Text.Encoding.UTF8.GetBytes(rightInt),
            System.Text.Encoding.UTF8.GetBytes(rightFrac),
            rightExp,
            "rhs",
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #endregion

    #region MatchNotEquals

    [TestMethod]
    [DataRow(false, "42", "", 0, false, "43", "", 0, true)]   // 42 != 43
    [DataRow(false, "42", "", 0, false, "42", "", 0, false)]  // 42 == 42 → not-equals fails
    [DataRow(true, "5", "", 0, false, "5", "", 0, true)]      // -5 != 5
    public void MatchNotEquals_Validates(
        bool leftNeg, string leftInt, string leftFrac, int leftExp,
        bool rightNeg, string rightInt, string rightFrac, int rightExp,
        bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchNotEquals(
            leftNeg,
            System.Text.Encoding.UTF8.GetBytes(leftInt),
            System.Text.Encoding.UTF8.GetBytes(leftFrac),
            leftExp,
            rightNeg,
            System.Text.Encoding.UTF8.GetBytes(rightInt),
            System.Text.Encoding.UTF8.GetBytes(rightFrac),
            rightExp,
            "rhs",
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #endregion

    #region MatchLessThan

    [TestMethod]
    [DataRow(false, "5", "", 0, false, "10", "", 0, true)]    // 5 < 10
    [DataRow(false, "10", "", 0, false, "5", "", 0, false)]   // 10 < 5 → false
    [DataRow(false, "5", "", 0, false, "5", "", 0, false)]    // 5 < 5 → false (not strictly less)
    [DataRow(true, "5", "", 0, false, "5", "", 0, true)]      // -5 < 5
    public void MatchLessThan_Validates(
        bool leftNeg, string leftInt, string leftFrac, int leftExp,
        bool rightNeg, string rightInt, string rightFrac, int rightExp,
        bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchLessThan(
            leftNeg,
            System.Text.Encoding.UTF8.GetBytes(leftInt),
            System.Text.Encoding.UTF8.GetBytes(leftFrac),
            leftExp,
            rightNeg,
            System.Text.Encoding.UTF8.GetBytes(rightInt),
            System.Text.Encoding.UTF8.GetBytes(rightFrac),
            rightExp,
            "rhs",
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #endregion

    #region MatchLessThanOrEquals

    [TestMethod]
    [DataRow(false, "5", "", 0, false, "10", "", 0, true)]    // 5 <= 10
    [DataRow(false, "5", "", 0, false, "5", "", 0, true)]     // 5 <= 5
    [DataRow(false, "10", "", 0, false, "5", "", 0, false)]   // 10 <= 5 → false
    public void MatchLessThanOrEquals_Validates(
        bool leftNeg, string leftInt, string leftFrac, int leftExp,
        bool rightNeg, string rightInt, string rightFrac, int rightExp,
        bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchLessThanOrEquals(
            leftNeg,
            System.Text.Encoding.UTF8.GetBytes(leftInt),
            System.Text.Encoding.UTF8.GetBytes(leftFrac),
            leftExp,
            rightNeg,
            System.Text.Encoding.UTF8.GetBytes(rightInt),
            System.Text.Encoding.UTF8.GetBytes(rightFrac),
            rightExp,
            "rhs",
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #endregion

    #region MatchGreaterThan

    [TestMethod]
    [DataRow(false, "10", "", 0, false, "5", "", 0, true)]    // 10 > 5
    [DataRow(false, "5", "", 0, false, "10", "", 0, false)]   // 5 > 10 → false
    [DataRow(false, "5", "", 0, false, "5", "", 0, false)]    // 5 > 5 → false
    public void MatchGreaterThan_Validates(
        bool leftNeg, string leftInt, string leftFrac, int leftExp,
        bool rightNeg, string rightInt, string rightFrac, int rightExp,
        bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchGreaterThan(
            leftNeg,
            System.Text.Encoding.UTF8.GetBytes(leftInt),
            System.Text.Encoding.UTF8.GetBytes(leftFrac),
            leftExp,
            rightNeg,
            System.Text.Encoding.UTF8.GetBytes(rightInt),
            System.Text.Encoding.UTF8.GetBytes(rightFrac),
            rightExp,
            "rhs",
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #endregion

    #region MatchGreaterThanOrEquals

    [TestMethod]
    [DataRow(false, "10", "", 0, false, "5", "", 0, true)]    // 10 >= 5
    [DataRow(false, "5", "", 0, false, "5", "", 0, true)]     // 5 >= 5
    [DataRow(false, "5", "", 0, false, "10", "", 0, false)]   // 5 >= 10 → false
    public void MatchGreaterThanOrEquals_Validates(
        bool leftNeg, string leftInt, string leftFrac, int leftExp,
        bool rightNeg, string rightInt, string rightFrac, int rightExp,
        bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchGreaterThanOrEquals(
            leftNeg,
            System.Text.Encoding.UTF8.GetBytes(leftInt),
            System.Text.Encoding.UTF8.GetBytes(leftFrac),
            leftExp,
            rightNeg,
            System.Text.Encoding.UTF8.GetBytes(rightInt),
            System.Text.Encoding.UTF8.GetBytes(rightFrac),
            rightExp,
            "rhs",
            "dummy"u8,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #endregion

    #region MatchTypeInteger

    [TestMethod]
    [DataRow(JsonTokenType.Number, 0, true)]   // integer (exponent >= 0)
    [DataRow(JsonTokenType.Number, -1, true)]   // non-integer: returns true but reports keyword as non-match
    [DataRow(JsonTokenType.String, 0, false)]   // wrong token type
    public void MatchTypeInteger_ValidatesTokenAndExponent(JsonTokenType tokenType, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, tokenType);
        bool result = JsonSchemaEvaluation.MatchTypeInteger(
            tokenType,
            "dummy"u8,
            exponent,
            ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #endregion

    private JsonSchemaContext CreateContext(DummyResultsCollector collector, JsonTokenType tokenType)
    {
        return JsonSchemaContext.BeginContext(new DummyDocument(tokenType), 0, false, false, collector);
    }
}
