// <copyright file="JsonLogicHelpersDirectTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.JsonLogic;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Direct unit tests for <see cref="JsonLogicHelpers"/> methods, targeting
/// specific uncovered branches identified from Cobertura coverage data.
/// </summary>
public class JsonLogicHelpersDirectTests
{
    // ─── IsTruthy: uncovered branches at lines 53-54 ─────────────────

    [Fact]
    public void IsTruthy_Object_ReturnsTrue()
    {
        // Line 53: JsonValueKind.Object => true
        JsonElement obj = JsonElement.ParseValue("""{"a":1}"""u8);
        Assert.True(JsonLogicHelpers.IsTruthy(obj));
    }

    [Fact]
    public void IsTruthy_Undefined_ReturnsFalse()
    {
        // Line 54: _ => false (undefined/default element)
        Assert.False(JsonLogicHelpers.IsTruthy(default));
    }

    // ─── TryCoerceToNumber: uncovered branches at lines 140-157 ──────

    [Fact]
    public void TryCoerceToNumber_Null_ReturnsZero()
    {
        // Lines 140-142: null → ZeroElement
        JsonElement nullElem = JsonElement.ParseValue("null"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.True(JsonLogicHelpers.TryCoerceToNumber(nullElem, workspace, out JsonElement result));
        Assert.Equal(0, result.GetDouble());
    }

    [Fact]
    public void TryCoerceToNumber_True_ReturnsOne()
    {
        // Lines 148-149: True → OneElement
        JsonElement trueElem = JsonElement.ParseValue("true"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.True(JsonLogicHelpers.TryCoerceToNumber(trueElem, workspace, out JsonElement result));
        Assert.Equal(1, result.GetDouble());
    }

    [Fact]
    public void TryCoerceToNumber_False_ReturnsZero()
    {
        // Lines 151-152: False → ZeroElement
        JsonElement falseElem = JsonElement.ParseValue("false"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.True(JsonLogicHelpers.TryCoerceToNumber(falseElem, workspace, out JsonElement result));
        Assert.Equal(0, result.GetDouble());
    }

    [Fact]
    public void TryCoerceToNumber_Object_ReturnsFalse()
    {
        // Lines 156-157: default case → false
        JsonElement obj = JsonElement.ParseValue("""{"a":1}"""u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.False(JsonLogicHelpers.TryCoerceToNumber(obj, workspace, out _));
    }

    // ─── CompareNumbers: fully uncovered (lines 165-176) ─────────────

    [Theory]
    [InlineData("1", "2", -1)]
    [InlineData("2", "1", 1)]
    [InlineData("42", "42", 0)]
    [InlineData("-5", "5", -1)]
    public void CompareNumbers_ReturnsExpectedSign(string left, string right, int expectedSign)
    {
        JsonElement l = JsonElement.ParseValue(Encoding.UTF8.GetBytes(left));
        JsonElement r = JsonElement.ParseValue(Encoding.UTF8.GetBytes(right));
        int result = JsonLogicHelpers.CompareNumbers(l, r);
        Assert.Equal(expectedSign, Math.Sign(result));
    }

    // ─── StringFromQuotedUtf8: fully uncovered (lines 235-238) ───────

    [Fact]
    public void StringFromQuotedUtf8_Memory_CreatesElement()
    {
        byte[] quoted = "\"hello\""u8.ToArray();
        JsonElement result = JsonLogicHelpers.StringFromQuotedUtf8(quoted.AsMemory());
        Assert.Equal("hello", result.GetString());
    }

    // ─── StringFromQuotedUtf8Span (no workspace): uncovered (247-250) ─

    [Fact]
    public void StringFromQuotedUtf8Span_CreatesElement()
    {
        ReadOnlySpan<byte> quoted = "\"world\""u8;
        JsonElement result = JsonLogicHelpers.StringFromQuotedUtf8Span(quoted);
        Assert.Equal("world", result.GetString());
    }

    // ─── CoerceToString: fully uncovered (lines 319-335) ─────────────

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("42", "42")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("null", "null")]
    public void CoerceToString_CoercesCorrectly(string json, string expected)
    {
        JsonElement elem = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        Assert.Equal(expected, JsonLogicHelpers.CoerceToString(elem));
    }

    [Fact]
    public void CoerceToString_Undefined_ReturnsNull()
    {
        // Line 321: IsNullOrUndefined → "null"
        Assert.Equal("null", JsonLogicHelpers.CoerceToString(default));
    }

    [Fact]
    public void CoerceToString_Array_ReturnsEmpty()
    {
        // Line 333: _ => string.Empty (array/object)
        JsonElement arr = JsonElement.ParseValue("[1,2]"u8);
        Assert.Equal(string.Empty, JsonLogicHelpers.CoerceToString(arr));
    }

    // ─── StringToElement: fully uncovered (lines 340-377) ─────────────

    [Fact]
    public void StringToElement_CreatesStringElement()
    {
        JsonElement result = JsonLogicHelpers.StringToElement("test");
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal("test", result.GetString());
    }

    [Fact]
    public void StringToElement_EmptyString()
    {
        JsonElement result = JsonLogicHelpers.StringToElement(string.Empty);
        Assert.Equal(string.Empty, result.GetString());
    }

    // ─── ConcatToElement: fully uncovered (lines 495-513) ────────────

    [Fact]
    public void ConcatToElement_ConcatenatesStrings()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement a = JsonElement.ParseValue("\"hello\""u8);
        JsonElement b = JsonElement.ParseValue("\" \""u8);
        JsonElement c = JsonElement.ParseValue("\"world\""u8);

        JsonElement[] operands = [a, b, c];

        JsonElement result = JsonLogicHelpers.ConcatToElement(operands, workspace);
        Assert.Equal("hello world", result.GetString());
    }

    [Fact]
    public void ConcatToElement_CoercesNumbersAndBooleans()
    {
        // Exercises AppendCoercedToBuffer lines 556-569 (true/false/null branches)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement num = JsonElement.ParseValue("42"u8);
        JsonElement t = JsonElement.ParseValue("true"u8);
        JsonElement f = JsonElement.ParseValue("false"u8);
        JsonElement n = JsonElement.ParseValue("null"u8);

        JsonElement[] operands = [num, t, f, n];

        JsonElement result = JsonLogicHelpers.ConcatToElement(operands, workspace);
        Assert.Equal("42truefalsenull", result.GetString());
    }

    [Fact]
    public void ConcatToElement_UndefinedElement_AppendsNull()
    {
        // Lines 521-525: undefined → "null"
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement str = JsonElement.ParseValue("\"x\""u8);

        JsonElement[] operands = [str, default]; // default = undefined

        JsonElement result = JsonLogicHelpers.ConcatToElement(operands, workspace);
        Assert.Equal("xnull", result.GetString());
    }

    // ─── CompareCoerced: fully uncovered (lines 756-782) ─────────────

    [Theory]
    [InlineData("3", "2", 1, true)]   // 3 > 2
    [InlineData("2", "3", 1, false)]  // 2 > 3
    [InlineData("3", "3", 2, true)]   // 3 >= 3
    [InlineData("2", "3", 3, true)]   // 2 < 3
    [InlineData("3", "3", 4, true)]   // 3 <= 3
    [InlineData("3", "2", 3, false)]  // 3 < 2
    public void CompareCoerced_NumberComparisons(string left, string right, int op, bool expected)
    {
        JsonElement l = JsonElement.ParseValue(Encoding.UTF8.GetBytes(left));
        JsonElement r = JsonElement.ParseValue(Encoding.UTF8.GetBytes(right));
        Assert.Equal(expected, JsonLogicHelpers.CompareCoerced(l, r, op));
    }

    [Fact]
    public void CompareCoerced_UndefinedLeft_ReturnsFalse()
    {
        // Lines 758-760: undefined → false
        JsonElement r = JsonElement.ParseValue("1"u8);
        Assert.False(JsonLogicHelpers.CompareCoerced(default, r, 1));
    }

    // ─── CoercingElementEquals: uncovered branches (lines 718-749) ────

    [Fact]
    public void CoercingElementEquals_BothNull_ReturnsTrue()
    {
        // Lines 718-719: both null/undefined → true
        JsonElement a = JsonElement.ParseValue("null"u8);
        JsonElement b = JsonElement.ParseValue("null"u8);
        Assert.True(JsonLogicHelpers.CoercingElementEquals(a, b));
    }

    [Fact]
    public void CoercingElementEquals_OneNull_ReturnsFalse()
    {
        // Lines 723-724: one null/undefined → false
        JsonElement a = JsonElement.ParseValue("null"u8);
        JsonElement b = JsonElement.ParseValue("1"u8);
        Assert.False(JsonLogicHelpers.CoercingElementEquals(a, b));
    }

    [Fact]
    public void CoercingElementEquals_StringAndNumber_Coerces()
    {
        // Lines 732-734: string "2" == number 2
        JsonElement str = JsonElement.ParseValue("\"2\""u8);
        JsonElement num = JsonElement.ParseValue("2"u8);
        Assert.True(JsonLogicHelpers.CoercingElementEquals(str, num));
    }

    [Fact]
    public void CoercingElementEquals_BooleanLeft_Coerces()
    {
        // Lines 737-740: true == 1
        JsonElement t = JsonElement.ParseValue("true"u8);
        JsonElement one = JsonElement.ParseValue("1"u8);
        Assert.True(JsonLogicHelpers.CoercingElementEquals(t, one));
    }

    [Fact]
    public void CoercingElementEquals_BooleanRight_Coerces()
    {
        // Lines 743-746: 0 == false
        JsonElement zero = JsonElement.ParseValue("0"u8);
        JsonElement f = JsonElement.ParseValue("false"u8);
        Assert.True(JsonLogicHelpers.CoercingElementEquals(zero, f));
    }

    [Fact]
    public void CoercingElementEquals_IncompatibleTypes_ReturnsFalse()
    {
        // Line 749: array vs number → false
        JsonElement arr = JsonElement.ParseValue("[1]"u8);
        JsonElement num = JsonElement.ParseValue("1"u8);
        Assert.False(JsonLogicHelpers.CoercingElementEquals(arr, num));
    }

    // ─── StrictElementEquals: uncovered branches (lines 688, 704) ────

    [Fact]
    public void StrictElementEquals_BothUndefined_ReturnsTrue()
    {
        // Lines 688-689: both undefined → true
        Assert.True(JsonLogicHelpers.StrictElementEquals(default, default));
    }

    [Fact]
    public void StrictElementEquals_BoolMatchByKind_ReturnsTrue()
    {
        // Line 704: booleans compared by ValueKind
        JsonElement t1 = JsonElement.ParseValue("true"u8);
        JsonElement t2 = JsonElement.ParseValue("true"u8);
        Assert.True(JsonLogicHelpers.StrictElementEquals(t1, t2));
    }

    // ─── CoerceToBigNumber: uncovered branches (lines 794-809) ───────

    [Fact]
    public void CoerceToBigNumber_True_ReturnsOne()
    {
        // Lines 794-796
        JsonElement t = JsonElement.ParseValue("true"u8);
        Assert.Equal(Corvus.Numerics.BigNumber.One, JsonLogicHelpers.CoerceToBigNumber(t));
    }

    [Fact]
    public void CoerceToBigNumber_False_ReturnsZero()
    {
        // Lines 799-801
        JsonElement f = JsonElement.ParseValue("false"u8);
        Assert.Equal(Corvus.Numerics.BigNumber.Zero, JsonLogicHelpers.CoerceToBigNumber(f));
    }

    [Fact]
    public void CoerceToBigNumber_NumericString_Coerces()
    {
        // Lines 804-806
        JsonElement s = JsonElement.ParseValue("\"42\""u8);
        Assert.Equal(Corvus.Numerics.BigNumber.Parse("42"u8), JsonLogicHelpers.CoerceToBigNumber(s));
    }

    [Fact]
    public void CoerceToBigNumber_NonNumericString_ReturnsZero()
    {
        // Line 809
        JsonElement s = JsonElement.ParseValue("\"abc\""u8);
        Assert.Equal(Corvus.Numerics.BigNumber.Zero, JsonLogicHelpers.CoerceToBigNumber(s));
    }

    // ─── TryCoerceToDouble: uncovered branches (lines 839-856) ───────

    [Fact]
    public void TryCoerceToDouble_True_ReturnsOne()
    {
        // Lines 839-841
        JsonElement t = JsonElement.ParseValue("true"u8);
        Assert.True(JsonLogicHelpers.TryCoerceToDouble(t, out double val));
        Assert.Equal(1.0, val);
    }

    [Fact]
    public void TryCoerceToDouble_False_ReturnsZero()
    {
        // Lines 845-847
        JsonElement f = JsonElement.ParseValue("false"u8);
        Assert.True(JsonLogicHelpers.TryCoerceToDouble(f, out double val));
        Assert.Equal(0.0, val);
    }

    [Fact]
    public void TryCoerceToDouble_Object_ReturnsFalse()
    {
        // Lines 855-856
        JsonElement obj = JsonElement.ParseValue("""{"a":1}"""u8);
        Assert.False(JsonLogicHelpers.TryCoerceToDouble(obj, out _));
    }

    // ─── BigNumberToElement: uncovered fallback (line 823) ───────────

    [Fact]
    public void BigNumberToElement_LargeNumber_ReturnsNumber()
    {
        // Line 823: BigNumber that can't be represented as double falls back to Zero
        // Use a very large number that exceeds double range
        using JsonWorkspace workspace = JsonWorkspace.Create();
        byte[] hugeBytes = Encoding.UTF8.GetBytes("1" + new string('0', 310));
        var huge = Corvus.Numerics.BigNumber.Parse(hugeBytes);
        JsonElement result = JsonLogicHelpers.BigNumberToElement(huge, workspace);

        // Should either represent the number or fall back to zero
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
    }

    // ─── DoubleToElement: verify non-finite handling ─────────────────

    [Fact]
    public void DoubleToElement_Infinity_DoesNotThrow()
    {
        // Exercises DoubleToElement — Utf8Formatter.TryFormat succeeds for Infinity
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonLogicHelpers.DoubleToElement(double.PositiveInfinity, workspace);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
    }

    [Fact]
    public void DoubleToElement_Normal_ReturnsCorrectValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonLogicHelpers.DoubleToElement(42.5, workspace);
        Assert.Equal(42.5, result.GetDouble());
    }

    // ─── ResolveVar: uncovered branches (lines 880-903) ──────────────

    [Fact]
    public void ResolveVar_NullPath_ReturnsData()
    {
        // Lines 880-881: null path → return data as-is
        JsonElement data = JsonElement.ParseValue("""{"x":1}"""u8);
        JsonElement nullPath = JsonElement.ParseValue("null"u8);
        JsonElement result = JsonLogicHelpers.ResolveVar(data, nullPath);
        Assert.Equal(data.GetRawText(), result.GetRawText());
    }

    [Fact]
    public void ResolveVar_NumericPath_IndexesArray()
    {
        // Lines 885-888: numeric path → array index
        JsonElement data = JsonElement.ParseValue("""[10,20,30]"""u8);
        JsonElement path = JsonElement.ParseValue("1"u8);
        JsonElement result = JsonLogicHelpers.ResolveVar(data, path);
        Assert.Equal(20, result.GetDouble());
    }

    [Fact]
    public void ResolveVar_EmptyString_ReturnsData()
    {
        // Lines 896-897: empty string path → return data
        JsonElement data = JsonElement.ParseValue("""{"x":1}"""u8);
        JsonElement emptyPath = JsonElement.ParseValue("\"\""u8);
        JsonElement result = JsonLogicHelpers.ResolveVar(data, emptyPath);
        Assert.Equal(data.GetRawText(), result.GetRawText());
    }

    [Fact]
    public void ResolveVar_NonStringNonNumber_ReturnsNull()
    {
        // Line 903: other types → NullElement
        JsonElement data = JsonElement.ParseValue("""{"x":1}"""u8);
        JsonElement boolPath = JsonElement.ParseValue("true"u8);
        JsonElement result = JsonLogicHelpers.ResolveVar(data, boolPath);
        Assert.Equal(JsonValueKind.Null, result.ValueKind);
    }

    // ─── ResolvePathSegment: array index (lines 944-945) ─────────────

    [Fact]
    public void ResolvePathSegment_ArrayIndex_Works()
    {
        // Lines 944-945: numeric segment indexes into array
        JsonElement data = JsonElement.ParseValue("""{"items":[10,20,30]}"""u8);
        JsonElement path = JsonElement.ParseValue("\"items.1\""u8);
        JsonElement result = JsonLogicHelpers.ResolveVar(data, path);
        Assert.Equal(20, result.GetDouble());
    }

    // ─── TryCoerceStringToNumber: uncovered branches (lines 630-645) ─

    [Fact]
    public void TryCoerceToNumber_EmptyString_ReturnsZero()
    {
        // Lines 630-632: empty string → ZeroElement
        JsonElement empty = JsonElement.ParseValue("\"\""u8);
        Assert.True(JsonLogicHelpers.TryCoerceToNumber(empty, out JsonElement result));
        Assert.Equal(0, result.GetDouble());
    }

    [Fact]
    public void TryCoerceToNumber_NonNumericString_ReturnsFalse()
    {
        // Lines 644-645: non-numeric string → false
        JsonElement str = JsonElement.ParseValue("\"abc\""u8);
        Assert.False(JsonLogicHelpers.TryCoerceToNumber(str, out _));
    }

    // ─── NumberFromUtf8: fully uncovered (lines 196-199) ─────────────

    [Fact]
    public void NumberFromUtf8_CreatesElement()
    {
        byte[] utf8 = "42"u8.ToArray();
        JsonElement result = JsonLogicHelpers.NumberFromUtf8(utf8.AsMemory());
        Assert.Equal(42, result.GetDouble());
    }

    // ─── SubstrFromElement edge cases (lines 404-406, 414-424, 437-438, 447-448) ─

    [Fact]
    public void SubstrFromElement_NonAsciiString_UsesSlowPath()
    {
        // Lines 404-406, 414-424: non-ASCII string → SubstrFromManagedString path
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement source = JsonElement.ParseValue("\"café\""u8);
        JsonElement result = JsonLogicHelpers.SubstrFromElement(source, 0, 4, workspace);
        Assert.Equal("café", result.GetString());
    }

    [Fact]
    public void SubstrFromElement_StartBeyondLength_ReturnsEmpty()
    {
        // Lines 437-438: start >= length → EmptyString
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement source = JsonElement.ParseValue("\"hi\""u8);
        JsonElement result = JsonLogicHelpers.SubstrFromElement(source, 100, int.MaxValue, workspace);
        Assert.Equal(string.Empty, result.GetString());
    }

    [Fact]
    public void SubstrFromElement_NegativeLength_ClampsToZero()
    {
        // Lines 447-448: negative length that results in 0 actual length
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement source = JsonElement.ParseValue("\"hello\""u8);
        JsonElement result = JsonLogicHelpers.SubstrFromElement(source, 3, -5, workspace);
        Assert.Equal(string.Empty, result.GetString());
    }
}
