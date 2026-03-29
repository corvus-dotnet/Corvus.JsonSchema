// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonSchemaMatchingNumberTests
{
    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid Byte
    [InlineData(false, "255", "", 0, true)] // max Byte
    [InlineData(false, "256", "", 0, false)] // above max
    [InlineData(true, "1", "", 0, false)] // negative not allowed
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid Decimal
    [InlineData(false, "79228162514264337593543950335", "", 0, true)] // max
    [InlineData(true, "79228162514264337593543950335", "", 0, true)] // min
    [InlineData(false, "79228162514264337593543950336", "", 0, false)] // above max
    [InlineData(true, "79228162514264337593543950336", "", 0, false)] // below min
    // Normalized floating point
    [InlineData(false, "123", "", -2, true)]
    [InlineData(false, "1", "", -2, true)]
    [InlineData(false, "123", "45", -4, true)]
    [InlineData(false, "", "123", -2, true)]
    [InlineData(false, "1", "23", -2, true)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid Double
    [InlineData(false, "17976931348623157", "", 291, true)] // max
    [InlineData(true, "17976931348623157", "", 291, true)] // min
    [InlineData(false, "17976931348623158", "", 291, false)] // above max
    [InlineData(true, "17976931348623158", "", 291, false)] // below min
    // Normalized floating point: 123e-2 = 1.23, 1e-2 = 0.01, 1.23e-2 = 0.0123
    [InlineData(false, "123", "", -2, true)]   // 123e-2 = 1.23
    [InlineData(false, "1", "", -2, true)]     // 1e-2 = 0.01
    [InlineData(false, "123", "45", -4, true)] // 123.45e-4 = 0.012345
    [InlineData(false, "", "123", -2, true)]   // 0.123e-2 = 0.00123
    [InlineData(false, "1", "23", -2, true)]   // 1.23e-2 = 0.0123
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid Half
    [InlineData(false, "65504", "", 0, true)] // max
    [InlineData(true, "65504", "", 0, true)] // min
    [InlineData(false, "65505", "", 0, false)] // above max
    [InlineData(true, "65505", "", 0, false)] // below min
    // Normalized floating point
    [InlineData(false, "123", "", -2, true)]
    [InlineData(false, "1", "", -2, true)]
    [InlineData(false, "123", "45", -4, true)]
    [InlineData(false, "", "123", -2, true)]
    [InlineData(false, "1", "23", -2, true)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "123", "", -2, false)]
    [InlineData(false, "1", "", -2, false)]
    [InlineData(false, "123", "45", -4, false)]
    [InlineData(false, "", "123", -2, false)]
    [InlineData(false, "1", "23", -2, false)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid Int128
    [InlineData(true, "170141183460469231731687303715884105728", "", 0, true)] // min
    [InlineData(false, "170141183460469231731687303715884105727", "", 0, true)] // max
    [InlineData(true, "170141183460469231731687303715884105729", "", 0, false)] // below min
    [InlineData(false, "170141183460469231731687303715884105728", "", 0, false)] // above max
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "123", "", -2, false)]
    [InlineData(false, "1", "", -2, false)]
    [InlineData(false, "123", "45", -4, false)]
    [InlineData(false, "", "123", -2, false)]
    [InlineData(false, "1", "23", -2, false)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid Int16
    [InlineData(true, "32768", "", 0, true)] // -32768 is min
    [InlineData(false, "32767", "", 0, true)] // 32767 is max
    [InlineData(true, "32769", "", 0, false)] // below min
    [InlineData(false, "32768", "", 0, false)] // above max
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "123", "", -2, false)]
    [InlineData(false, "1", "", -2, false)]
    [InlineData(false, "123", "45", -4, false)]
    [InlineData(false, "", "123", -2, false)]
    [InlineData(false, "1", "23", -2, false)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid Int32
    [InlineData(true, "2147483648", "", 0, true)] // -2147483648 is valid Int32 (min)
    [InlineData(false, "2147483647", "", 0, true)] // 2147483647 is valid Int32 (max)
    [InlineData(true, "2147483649", "", 0, false)] // -2147483649 is below min
    [InlineData(false, "2147483648", "", 0, false)] // 2147483648 is above max
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "123", "", -2, false)]
    [InlineData(false, "1", "", -2, false)]
    [InlineData(false, "123", "45", -4, false)]
    [InlineData(false, "", "123", -2, false)]
    [InlineData(false, "1", "23", -2, false)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid Int64
    [InlineData(true, "9223372036854775808", "", 0, true)] // -9223372036854775808 is min
    [InlineData(false, "9223372036854775807", "", 0, true)] // 9223372036854775807 is max
    [InlineData(true, "9223372036854775809", "", 0, false)] // below min
    [InlineData(false, "9223372036854775808", "", 0, false)] // above max
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "123", "", -2, false)]
    [InlineData(false, "1", "", -2, false)]
    [InlineData(false, "123", "45", -4, false)]
    [InlineData(false, "", "123", -2, false)]
    [InlineData(false, "1", "23", -2, false)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid SByte
    [InlineData(true, "128", "", 0, true)] // -128 is min
    [InlineData(false, "127", "", 0, true)] // 127 is max
    [InlineData(true, "129", "", 0, false)] // below min
    [InlineData(false, "128", "", 0, false)] // above max
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid Single
    [InlineData(false, "340282346638528859", "", 20, true)] // max
    [InlineData(true, "340282346638528859", "", 20, true)] // min
    [InlineData(false, "340282346638528860", "", 20, false)] // above max
    [InlineData(true, "340282346638528860", "", 20, false)] // below min
    // Normalized floating point
    [InlineData(false, "123", "", -2, true)]
    [InlineData(false, "1", "", -2, true)]
    [InlineData(false, "123", "45", -4, true)]
    [InlineData(false, "", "123", -2, true)]
    [InlineData(false, "1", "23", -2, true)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(JsonTokenType.Number, true)]
    [InlineData(JsonTokenType.String, false)]
    public void MatchTypeNumber_ValidatesTokenType(JsonTokenType tokenType, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);
        bool result = JsonSchemaEvaluation.MatchTypeNumber(tokenType, "dummy"u8, ref context);
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "123", "", -2, false)]
    [InlineData(false, "1", "", -2, false)]
    [InlineData(false, "123", "45", -4, false)]
    [InlineData(false, "", "123", -2, false)]
    [InlineData(false, "1", "23", -2, false)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid UInt128
    [InlineData(false, "340282366920938463463374607431768211455", "", 0, true)] // max
    [InlineData(false, "340282366920938463463374607431768211456", "", 0, false)] // above max
    [InlineData(true, "1", "", 0, false)] // negative not allowed
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid UInt16
    [InlineData(false, "65535", "", 0, true)] // max UInt16
    [InlineData(false, "65536", "", 0, false)] // above max
    [InlineData(true, "1", "", 0, false)] // negative not allowed
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "123", "", -2, false)]
    [InlineData(false, "1", "", -2, false)]
    [InlineData(false, "123", "45", -4, false)]
    [InlineData(false, "", "123", -2, false)]
    [InlineData(false, "1", "23", -2, false)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid UInt32
    [InlineData(false, "4294967295", "", 0, true)] // max UInt32
    [InlineData(false, "4294967296", "", 0, false)] // above max
    [InlineData(true, "1", "", 0, false)] // negative not allowed
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "123", "", -2, false)]
    [InlineData(false, "1", "", -2, false)]
    [InlineData(false, "123", "45", -4, false)]
    [InlineData(false, "", "123", -2, false)]
    [InlineData(false, "1", "23", -2, false)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "0", "", 0, true)] // 0 is valid UInt64
    [InlineData(false, "18446744073709551615", "", 0, true)] // max UInt64
    [InlineData(false, "18446744073709551616", "", 0, false)] // above max
    [InlineData(true, "1", "", 0, false)] // negative not allowed
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [Theory]
    [InlineData(false, "123", "", -2, false)]
    [InlineData(false, "1", "", -2, false)]
    [InlineData(false, "123", "45", -4, false)]
    [InlineData(false, "", "123", -2, false)]
    [InlineData(false, "1", "23", -2, false)]
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
        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    private JsonSchemaContext CreateContext(DummyResultsCollector collector, JsonTokenType tokenType)
    {
        return JsonSchemaContext.BeginContext(new DummyDocument(tokenType), 0, false, false, collector);
    }
}