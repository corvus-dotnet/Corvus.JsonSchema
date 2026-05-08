// <copyright file="JsonLogicHelpersDirectTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Numerics;
using Corvus.Text.Json.JsonLogic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Direct unit tests for <see cref="JsonLogicHelpers"/> methods, targeting
/// specific uncovered branches identified from Cobertura coverage data.
/// </summary>
[TestClass]
public class JsonLogicHelpersDirectTests
{
    // ─── IsTruthy: uncovered branches at lines 53-54 ─────────────────

    [TestMethod]
    public void IsTruthy_Object_ReturnsTrue()
    {
        // Line 53: JsonValueKind.Object => true
        JsonElement obj = JsonElement.ParseValue("""{"a":1}"""u8);
        Assert.IsTrue(JsonLogicHelpers.IsTruthy(obj));
    }

    [TestMethod]
    public void IsTruthy_Undefined_ReturnsFalse()
    {
        // Line 54: _ => false (undefined/default element)
        Assert.IsFalse(JsonLogicHelpers.IsTruthy(default));
    }

    // ─── TryCoerceToNumber: uncovered branches at lines 140-157 ──────

    [TestMethod]
    public void TryCoerceToNumber_Null_ReturnsZero()
    {
        // Lines 140-142: null → ZeroElement
        JsonElement nullElem = JsonElement.ParseValue("null"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.IsTrue(JsonLogicHelpers.TryCoerceToNumber(nullElem, workspace, out JsonElement result));
        Assert.AreEqual(0, result.GetDouble());
    }

    [TestMethod]
    public void TryCoerceToNumber_True_ReturnsOne()
    {
        // Lines 148-149: True → OneElement
        JsonElement trueElem = JsonElement.ParseValue("true"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.IsTrue(JsonLogicHelpers.TryCoerceToNumber(trueElem, workspace, out JsonElement result));
        Assert.AreEqual(1, result.GetDouble());
    }

    [TestMethod]
    public void TryCoerceToNumber_False_ReturnsZero()
    {
        // Lines 151-152: False → ZeroElement
        JsonElement falseElem = JsonElement.ParseValue("false"u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.IsTrue(JsonLogicHelpers.TryCoerceToNumber(falseElem, workspace, out JsonElement result));
        Assert.AreEqual(0, result.GetDouble());
    }

    [TestMethod]
    public void TryCoerceToNumber_Object_ReturnsFalse()
    {
        // Lines 156-157: default case → false
        JsonElement obj = JsonElement.ParseValue("""{"a":1}"""u8);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.IsFalse(JsonLogicHelpers.TryCoerceToNumber(obj, workspace, out _));
    }

    // ─── CompareNumbers: fully uncovered (lines 165-176) ─────────────

    [TestMethod]
    [DataRow("1", "2", -1)]
    [DataRow("2", "1", 1)]
    [DataRow("42", "42", 0)]
    [DataRow("-5", "5", -1)]
    public void CompareNumbers_ReturnsExpectedSign(string left, string right, int expectedSign)
    {
        JsonElement l = JsonElement.ParseValue(Encoding.UTF8.GetBytes(left));
        JsonElement r = JsonElement.ParseValue(Encoding.UTF8.GetBytes(right));
        int result = JsonLogicHelpers.CompareNumbers(l, r);
        Assert.AreEqual(expectedSign, Math.Sign(result));
    }

    // ─── StringFromQuotedUtf8: fully uncovered (lines 235-238) ───────

    [TestMethod]
    public void StringFromQuotedUtf8_Memory_CreatesElement()
    {
        byte[] quoted = "\"hello\""u8.ToArray();
        JsonElement result = JsonLogicHelpers.StringFromQuotedUtf8(quoted.AsMemory());
        Assert.AreEqual("hello", result.GetString());
    }

    // ─── StringFromQuotedUtf8Span (no workspace): uncovered (247-250) ─

    [TestMethod]
    public void StringFromQuotedUtf8Span_CreatesElement()
    {
        ReadOnlySpan<byte> quoted = "\"world\""u8;
        JsonElement result = JsonLogicHelpers.StringFromQuotedUtf8Span(quoted);
        Assert.AreEqual("world", result.GetString());
    }

    // ─── CoerceToString: fully uncovered (lines 319-335) ─────────────

    [TestMethod]
    [DataRow("\"hello\"", "hello")]
    [DataRow("42", "42")]
    [DataRow("true", "true")]
    [DataRow("false", "false")]
    [DataRow("null", "null")]
    public void CoerceToString_CoercesCorrectly(string json, string expected)
    {
        JsonElement elem = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        Assert.AreEqual(expected, JsonLogicHelpers.CoerceToString(elem));
    }

    [TestMethod]
    public void CoerceToString_Undefined_ReturnsNull()
    {
        // Line 321: IsNullOrUndefined → "null"
        Assert.AreEqual("null", JsonLogicHelpers.CoerceToString(default));
    }

    [TestMethod]
    public void CoerceToString_Array_ReturnsEmpty()
    {
        // Line 333: _ => string.Empty (array/object)
        JsonElement arr = JsonElement.ParseValue("[1,2]"u8);
        Assert.AreEqual(string.Empty, JsonLogicHelpers.CoerceToString(arr));
    }

    // ─── StringToElement: fully uncovered (lines 340-377) ─────────────

    [TestMethod]
    public void StringToElement_CreatesStringElement()
    {
        JsonElement result = JsonLogicHelpers.StringToElement("test");
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
        Assert.AreEqual("test", result.GetString());
    }

    [TestMethod]
    public void StringToElement_EmptyString()
    {
        JsonElement result = JsonLogicHelpers.StringToElement(string.Empty);
        Assert.AreEqual(string.Empty, result.GetString());
    }

    // ─── ConcatToElement: fully uncovered (lines 495-513) ────────────

    [TestMethod]
    public void ConcatToElement_ConcatenatesStrings()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement a = JsonElement.ParseValue("\"hello\""u8);
        JsonElement b = JsonElement.ParseValue("\" \""u8);
        JsonElement c = JsonElement.ParseValue("\"world\""u8);

        JsonElement[] operands = [a, b, c];

        JsonElement result = JsonLogicHelpers.ConcatToElement(operands, workspace);
        Assert.AreEqual("hello world", result.GetString());
    }

    [TestMethod]
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
        Assert.AreEqual("42truefalsenull", result.GetString());
    }

    [TestMethod]
    public void ConcatToElement_UndefinedElement_AppendsNull()
    {
        // Lines 521-525: undefined → "null"
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement str = JsonElement.ParseValue("\"x\""u8);

        JsonElement[] operands = [str, default]; // default = undefined

        JsonElement result = JsonLogicHelpers.ConcatToElement(operands, workspace);
        Assert.AreEqual("xnull", result.GetString());
    }

    // ─── CompareCoerced: fully uncovered (lines 756-782) ─────────────

    [TestMethod]
    [DataRow("3", "2", 1, true)]   // 3 > 2
    [DataRow("2", "3", 1, false)]  // 2 > 3
    [DataRow("3", "3", 2, true)]   // 3 >= 3
    [DataRow("2", "3", 3, true)]   // 2 < 3
    [DataRow("3", "3", 4, true)]   // 3 <= 3
    [DataRow("3", "2", 3, false)]  // 3 < 2
    public void CompareCoerced_NumberComparisons(string left, string right, int op, bool expected)
    {
        JsonElement l = JsonElement.ParseValue(Encoding.UTF8.GetBytes(left));
        JsonElement r = JsonElement.ParseValue(Encoding.UTF8.GetBytes(right));
        Assert.AreEqual(expected, JsonLogicHelpers.CompareCoerced(l, r, op));
    }

    [TestMethod]
    public void CompareCoerced_UndefinedLeft_ReturnsFalse()
    {
        // Lines 758-760: undefined → false
        JsonElement r = JsonElement.ParseValue("1"u8);
        Assert.IsFalse(JsonLogicHelpers.CompareCoerced(default, r, 1));
    }

    // ─── CoercingElementEquals: uncovered branches (lines 718-749) ────

    [TestMethod]
    public void CoercingElementEquals_BothNull_ReturnsTrue()
    {
        // Lines 718-719: both null/undefined → true
        JsonElement a = JsonElement.ParseValue("null"u8);
        JsonElement b = JsonElement.ParseValue("null"u8);
        Assert.IsTrue(JsonLogicHelpers.CoercingElementEquals(a, b));
    }

    [TestMethod]
    public void CoercingElementEquals_OneNull_ReturnsFalse()
    {
        // Lines 723-724: one null/undefined → false
        JsonElement a = JsonElement.ParseValue("null"u8);
        JsonElement b = JsonElement.ParseValue("1"u8);
        Assert.IsFalse(JsonLogicHelpers.CoercingElementEquals(a, b));
    }

    [TestMethod]
    public void CoercingElementEquals_StringAndNumber_Coerces()
    {
        // Lines 732-734: string "2" == number 2
        JsonElement str = JsonElement.ParseValue("\"2\""u8);
        JsonElement num = JsonElement.ParseValue("2"u8);
        Assert.IsTrue(JsonLogicHelpers.CoercingElementEquals(str, num));
    }

    [TestMethod]
    public void CoercingElementEquals_BooleanLeft_Coerces()
    {
        // Lines 737-740: true == 1
        JsonElement t = JsonElement.ParseValue("true"u8);
        JsonElement one = JsonElement.ParseValue("1"u8);
        Assert.IsTrue(JsonLogicHelpers.CoercingElementEquals(t, one));
    }

    [TestMethod]
    public void CoercingElementEquals_BooleanRight_Coerces()
    {
        // Lines 743-746: 0 == false
        JsonElement zero = JsonElement.ParseValue("0"u8);
        JsonElement f = JsonElement.ParseValue("false"u8);
        Assert.IsTrue(JsonLogicHelpers.CoercingElementEquals(zero, f));
    }

    [TestMethod]
    public void CoercingElementEquals_IncompatibleTypes_ReturnsFalse()
    {
        // Line 749: array vs number → false
        JsonElement arr = JsonElement.ParseValue("[1]"u8);
        JsonElement num = JsonElement.ParseValue("1"u8);
        Assert.IsFalse(JsonLogicHelpers.CoercingElementEquals(arr, num));
    }

    // ─── StrictElementEquals: uncovered branches (lines 688, 704) ────

    [TestMethod]
    public void StrictElementEquals_BothUndefined_ReturnsTrue()
    {
        // Lines 688-689: both undefined → true
        Assert.IsTrue(JsonLogicHelpers.StrictElementEquals(default, default));
    }

    [TestMethod]
    public void StrictElementEquals_BoolMatchByKind_ReturnsTrue()
    {
        // Line 704: booleans compared by ValueKind
        JsonElement t1 = JsonElement.ParseValue("true"u8);
        JsonElement t2 = JsonElement.ParseValue("true"u8);
        Assert.IsTrue(JsonLogicHelpers.StrictElementEquals(t1, t2));
    }

    // ─── CoerceToBigNumber: uncovered branches (lines 794-809) ───────

    [TestMethod]
    public void CoerceToBigNumber_True_ReturnsOne()
    {
        // Lines 794-796
        JsonElement t = JsonElement.ParseValue("true"u8);
        Assert.AreEqual(Corvus.Numerics.BigNumber.One, JsonLogicHelpers.CoerceToBigNumber(t));
    }

    [TestMethod]
    public void CoerceToBigNumber_False_ReturnsZero()
    {
        // Lines 799-801
        JsonElement f = JsonElement.ParseValue("false"u8);
        Assert.AreEqual(Corvus.Numerics.BigNumber.Zero, JsonLogicHelpers.CoerceToBigNumber(f));
    }

    [TestMethod]
    public void CoerceToBigNumber_NumericString_Coerces()
    {
        // Lines 804-806
        JsonElement s = JsonElement.ParseValue("\"42\""u8);
        Assert.AreEqual(Corvus.Numerics.BigNumber.Parse("42"u8), JsonLogicHelpers.CoerceToBigNumber(s));
    }

    [TestMethod]
    public void CoerceToBigNumber_NonNumericString_ReturnsZero()
    {
        // Line 809
        JsonElement s = JsonElement.ParseValue("\"abc\""u8);
        Assert.AreEqual(Corvus.Numerics.BigNumber.Zero, JsonLogicHelpers.CoerceToBigNumber(s));
    }

    // ─── TryCoerceToDouble: uncovered branches (lines 839-856) ───────

    [TestMethod]
    public void TryCoerceToDouble_True_ReturnsOne()
    {
        // Lines 839-841
        JsonElement t = JsonElement.ParseValue("true"u8);
        Assert.IsTrue(JsonLogicHelpers.TryCoerceToDouble(t, out double val));
        Assert.AreEqual(1.0, val);
    }

    [TestMethod]
    public void TryCoerceToDouble_False_ReturnsZero()
    {
        // Lines 845-847
        JsonElement f = JsonElement.ParseValue("false"u8);
        Assert.IsTrue(JsonLogicHelpers.TryCoerceToDouble(f, out double val));
        Assert.AreEqual(0.0, val);
    }

    [TestMethod]
    public void TryCoerceToDouble_Object_ReturnsFalse()
    {
        // Lines 855-856
        JsonElement obj = JsonElement.ParseValue("""{"a":1}"""u8);
        Assert.IsFalse(JsonLogicHelpers.TryCoerceToDouble(obj, out _));
    }

    // ─── BigNumberToElement: uncovered fallback (line 823) ───────────

    [TestMethod]
    public void BigNumberToElement_LargeNumber_ReturnsNumber()
    {
        // Line 823: BigNumber that can't be represented as double falls back to Zero
        // Use a very large number that exceeds double range
        using JsonWorkspace workspace = JsonWorkspace.Create();
        byte[] hugeBytes = Encoding.UTF8.GetBytes("1" + new string('0', 310));
        var huge = Corvus.Numerics.BigNumber.Parse(hugeBytes);
        JsonElement result = JsonLogicHelpers.BigNumberToElement(huge, workspace);

        // Should either represent the number or fall back to zero
        Assert.AreEqual(JsonValueKind.Number, result.ValueKind);
    }

    // ─── DoubleToElement: verify non-finite handling ─────────────────

    [TestMethod]
    public void DoubleToElement_Infinity_DoesNotThrow()
    {
        // Exercises DoubleToElement — Utf8Formatter.TryFormat succeeds for Infinity
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonLogicHelpers.DoubleToElement(double.PositiveInfinity, workspace);
        Assert.AreEqual(JsonValueKind.Number, result.ValueKind);
    }

    [TestMethod]
    public void DoubleToElement_Normal_ReturnsCorrectValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonLogicHelpers.DoubleToElement(42.5, workspace);
        Assert.AreEqual(42.5, result.GetDouble());
    }

    // ─── ResolveVar: uncovered branches (lines 880-903) ──────────────

    [TestMethod]
    public void ResolveVar_NullPath_ReturnsData()
    {
        // Lines 880-881: null path → return data as-is
        JsonElement data = JsonElement.ParseValue("""{"x":1}"""u8);
        JsonElement nullPath = JsonElement.ParseValue("null"u8);
        JsonElement result = JsonLogicHelpers.ResolveVar(data, nullPath);
        Assert.AreEqual(data.GetRawText(), result.GetRawText());
    }

    [TestMethod]
    public void ResolveVar_NumericPath_IndexesArray()
    {
        // Lines 885-888: numeric path → array index
        JsonElement data = JsonElement.ParseValue("""[10,20,30]"""u8);
        JsonElement path = JsonElement.ParseValue("1"u8);
        JsonElement result = JsonLogicHelpers.ResolveVar(data, path);
        Assert.AreEqual(20, result.GetDouble());
    }

    [TestMethod]
    public void ResolveVar_EmptyString_ReturnsData()
    {
        // Lines 896-897: empty string path → return data
        JsonElement data = JsonElement.ParseValue("""{"x":1}"""u8);
        JsonElement emptyPath = JsonElement.ParseValue("\"\""u8);
        JsonElement result = JsonLogicHelpers.ResolveVar(data, emptyPath);
        Assert.AreEqual(data.GetRawText(), result.GetRawText());
    }

    [TestMethod]
    public void ResolveVar_NonStringNonNumber_ReturnsNull()
    {
        // Line 903: other types → NullElement
        JsonElement data = JsonElement.ParseValue("""{"x":1}"""u8);
        JsonElement boolPath = JsonElement.ParseValue("true"u8);
        JsonElement result = JsonLogicHelpers.ResolveVar(data, boolPath);
        Assert.AreEqual(JsonValueKind.Null, result.ValueKind);
    }

    // ─── ResolvePathSegment: array index (lines 944-945) ─────────────

    [TestMethod]
    public void ResolvePathSegment_ArrayIndex_Works()
    {
        // Lines 944-945: numeric segment indexes into array
        JsonElement data = JsonElement.ParseValue("""{"items":[10,20,30]}"""u8);
        JsonElement path = JsonElement.ParseValue("\"items.1\""u8);
        JsonElement result = JsonLogicHelpers.ResolveVar(data, path);
        Assert.AreEqual(20, result.GetDouble());
    }

    // ─── TryCoerceStringToNumber: uncovered branches (lines 630-645) ─

    [TestMethod]
    public void TryCoerceToNumber_EmptyString_ReturnsZero()
    {
        // Lines 630-632: empty string → ZeroElement
        JsonElement empty = JsonElement.ParseValue("\"\""u8);
        Assert.IsTrue(JsonLogicHelpers.TryCoerceToNumber(empty, out JsonElement result));
        Assert.AreEqual(0, result.GetDouble());
    }

    [TestMethod]
    public void TryCoerceToNumber_NonNumericString_ReturnsFalse()
    {
        // Lines 644-645: non-numeric string → false
        JsonElement str = JsonElement.ParseValue("\"abc\""u8);
        Assert.IsFalse(JsonLogicHelpers.TryCoerceToNumber(str, out _));
    }

    // ─── NumberFromUtf8: fully uncovered (lines 196-199) ─────────────

    [TestMethod]
    public void NumberFromUtf8_CreatesElement()
    {
        byte[] utf8 = "42"u8.ToArray();
        JsonElement result = JsonLogicHelpers.NumberFromUtf8(utf8.AsMemory());
        Assert.AreEqual(42, result.GetDouble());
    }

    // ─── SubstrFromElement edge cases (lines 404-406, 414-424, 437-438, 447-448) ─

    [TestMethod]
    public void SubstrFromElement_NonAsciiString_UsesSlowPath()
    {
        // Lines 404-406, 414-424: non-ASCII string → SubstrFromManagedString path
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement source = JsonElement.ParseValue("\"café\""u8);
        JsonElement result = JsonLogicHelpers.SubstrFromElement(source, 0, 4, workspace);
        Assert.AreEqual("café", result.GetString());
    }

    [TestMethod]
    public void SubstrFromElement_StartBeyondLength_ReturnsEmpty()
    {
        // Lines 437-438: start >= length → EmptyString
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement source = JsonElement.ParseValue("\"hi\""u8);
        JsonElement result = JsonLogicHelpers.SubstrFromElement(source, 100, int.MaxValue, workspace);
        Assert.AreEqual(string.Empty, result.GetString());
    }

    [TestMethod]
    public void SubstrFromElement_NegativeLength_ClampsToZero()
    {
        // Lines 447-448: negative length that results in 0 actual length
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement source = JsonElement.ParseValue("\"hello\""u8);
        JsonElement result = JsonLogicHelpers.SubstrFromElement(source, 3, -5, workspace);
        Assert.AreEqual(string.Empty, result.GetString());
    }

    // ─── CompareCoerced: BigNumber fallback path (lines 790-803) ─────

    [TestMethod]
    public void CompareCoerced_BigNumberFallback_GreaterThan()
    {
        // Lines 795-798: CompareNumbers with op=1 (>)
        // Use numbers beyond double precision to force BigNumber path
        JsonElement left = JsonElement.ParseValue("99999999999999999999999999999"u8);
        JsonElement right = JsonElement.ParseValue("1"u8);
        Assert.IsTrue(JsonLogicHelpers.CompareCoerced(left, right, 1));
    }

    [TestMethod]
    public void CompareCoerced_BigNumberFallback_GreaterThanOrEqual()
    {
        // Lines 795-799: op=2 (>=)
        JsonElement left = JsonElement.ParseValue("99999999999999999999999999999"u8);
        JsonElement right = JsonElement.ParseValue("99999999999999999999999999999"u8);
        Assert.IsTrue(JsonLogicHelpers.CompareCoerced(left, right, 2));
    }

    [TestMethod]
    public void CompareCoerced_BigNumberFallback_LessThan()
    {
        // Lines 795-800: op=3 (<)
        JsonElement left = JsonElement.ParseValue("1"u8);
        JsonElement right = JsonElement.ParseValue("99999999999999999999999999999"u8);
        Assert.IsTrue(JsonLogicHelpers.CompareCoerced(left, right, 3));
    }

    [TestMethod]
    public void CompareCoerced_BigNumberFallback_LessThanOrEqual()
    {
        // Lines 795-801: op=4 (<=)
        JsonElement left = JsonElement.ParseValue("99999999999999999999999999999"u8);
        JsonElement right = JsonElement.ParseValue("99999999999999999999999999999"u8);
        Assert.IsTrue(JsonLogicHelpers.CompareCoerced(left, right, 4));
    }

    [TestMethod]
    public void CompareCoerced_NonNumeric_ReturnsFalse()
    {
        // Lines 790-792: TryCoerceToNumber fails
        JsonElement left = JsonElement.ParseValue("\"abc\""u8);
        JsonElement right = JsonElement.ParseValue("\"def\""u8);
        Assert.IsFalse(JsonLogicHelpers.CompareCoerced(left, right, 1));
    }

    // ─── CoerceToBigNumber (lines 811-816) ───────────────────────────

    [TestMethod]
    public void CoerceToBigNumber_Number_ReturnsValue()
    {
        // Lines 812-816: number element → BigNumber
        JsonElement elem = JsonElement.ParseValue("42"u8);
        BigNumber result = JsonLogicHelpers.CoerceToBigNumber(elem);
        Assert.AreEqual(BigNumber.Parse("42"u8), result);
    }

    [TestMethod]
    public void CoerceToBigNumber_Null_ReturnsZero()
    {
        JsonElement elem = JsonElement.ParseValue("null"u8);
        BigNumber result = JsonLogicHelpers.CoerceToBigNumber(elem);
        Assert.AreEqual(BigNumber.Zero, result);
    }

    // ─── AppendCoercedUtf8 (lines 274-301): null, string, number ─────

    [TestMethod]
    public void AppendCoercedUtf8_Null_AppendsNullLiteral()
    {
        // Lines 275-278: null/undefined → "null"
        JsonElement elem = JsonElement.ParseValue("null"u8);
        string result = EvalCat("""{"cat":[null]}""");
        Assert.AreEqual("\"null\"", result);
    }

    [TestMethod]
    public void AppendCoercedUtf8_Number_AppendsRawDigits()
    {
        // Lines 298-301: number → raw UTF-8 digits
        string result = EvalCat("""{"cat":[42]}""");
        Assert.AreEqual("\"42\"", result);
    }

    [TestMethod]
    public void AppendCoercedUtf8_String_AppendsContent()
    {
        // Lines 284-292: string → content without quotes
        string result = EvalCat("""{"cat":["hello"]}""");
        Assert.AreEqual("\"hello\"", result);
    }

    // ─── GrowCatBuffer (lines 598-603) ───────────────────────────────

    [TestMethod]
    public void Cat_LongString_GrowsBuffer()
    {
        // Lines 598-603: GrowCatBuffer is called when concat exceeds initial buffer
        string longStr = new('x', 300);
        string rule = $$"""{"cat":["{{longStr}}","{{longStr}}"]}""";
        string result = EvalCat(rule);
        Assert.AreEqual($"\"{longStr}{longStr}\"", result);
    }

    private static string EvalCat(string rule)
    {
        JsonElement ruleElem = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(rule));
        JsonElement dataElem = JsonElement.ParseValue("{}"u8);
        JsonLogicRule logicRule = new(ruleElem);
        JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElem);
        return result.GetRawText();
    }

    // ═══════════════════════════════════════════════════════════════
    // ROUND 3: Additional JsonLogicHelpers coverage
    // ═══════════════════════════════════════════════════════════════

    // ─── AppendCoercedUtf8: null/undefined, true, false, null enum (L273-313) ──

    [TestMethod]
    public void AppendCoercedUtf8_True_AppendsTrue()
    {
        // L304-305: true → "true" bytes
        string result = EvalCat("""{"cat":["x",true,"y"]}""");
        Assert.AreEqual("\"xtruey\"", result);
    }

    [TestMethod]
    public void AppendCoercedUtf8_False_AppendsFalse()
    {
        // L307-308: false → "false" bytes
        string result = EvalCat("""{"cat":["x",false,"y"]}""");
        Assert.AreEqual("\"xfalsey\"", result);
    }

    [TestMethod]
    public void AppendCoercedUtf8_Null_AppendsNull()
    {
        // L310-312: null enum → "null" bytes
        string result = EvalCat("""{"cat":["x",null,"y"]}""");
        Assert.AreEqual("\"xnully\"", result);
    }

    [TestMethod]
    public void AppendCoercedUtf8_UndefinedElement_AppendsNull()
    {
        // L275-278: IsNullOrUndefined → "null" bytes
        // Use var of missing key to get undefined
        string result = EvalCat("""{"cat":["a",{"var":"missing"},"b"]}""");
        Assert.AreEqual("\"anullb\"", result);
    }

    // ─── CoerceToString: various branches (L326-334) ────────────────

    [TestMethod]
    public void CoerceToString_UndefinedVar_ReturnsNull()
    {
        // cat with missing var → undefined coerces to "null"
        string result = EvalCat("""{"cat":[{"var":"missing"}]}""");
        Assert.AreEqual("\"null\"", result);
    }

    [TestMethod]
    public void Cat_WithObject_ReturnsEmptyForObject()
    {
        // Object data via cat: objects are not AppendCoercedUtf8'd (no Object case)
        // so cat of an object via a var returns empty
        string result = EvalCat("""{"cat":[{"var":""}]}""");
        Assert.AreEqual("\"\"", result);
    }

    // ─── Substr slow path (managed string) (L474-497) ───────────────

    [TestMethod]
    public void Substr_NonAsciiString_UsesSlowPath()
    {
        // L418-424, L474-496: non-ASCII forces managed string path
        // The source string contains multi-byte UTF-8
        Assert.AreEqual("\"é\"", Eval("""{"substr":["café",3,1]}"""));
    }

    [TestMethod]
    public void Substr_SlowPath_NegativeStart()
    {
        // L476-478: start < 0 → Math.Max(0, len + start)
        Assert.AreEqual("\"é\"", Eval("""{"substr":["café",-1]}"""));
    }

    [TestMethod]
    public void Substr_SlowPath_StartPastEnd()
    {
        // L481-483: start >= str.Length → EmptyString
        Assert.AreEqual("\"\"", Eval("""{"substr":["café",99]}"""));
    }

    [TestMethod]
    public void Substr_SlowPath_NegativeLength()
    {
        // L488: length < 0 → trim from end
        Assert.AreEqual("\"af\"", Eval("""{"substr":["café",1,-1]}"""));
    }

    [TestMethod]
    public void Substr_SlowPath_ZeroLength()
    {
        // L491-493: actualLength <= 0 → EmptyString
        Assert.AreEqual("\"\"", Eval("""{"substr":["café",2,0]}"""));
    }

    // ─── SubstrFromAsciiUtf8 edge cases (L427-471) ──────────────────

    [TestMethod]
    public void Substr_Ascii_NegativeStart()
    {
        // L431-433: start < 0 with ASCII string
        Assert.AreEqual("\"lo\"", Eval("""{"substr":["hello",-2]}"""));
    }

    [TestMethod]
    public void Substr_Ascii_StartPastEnd()
    {
        // L436-438: start >= len → EmptyString
        Assert.AreEqual("\"\"", Eval("""{"substr":["hello",99]}"""));
    }

    [TestMethod]
    public void Substr_Ascii_NegativeLength()
    {
        // L443: length < 0 → trim from end
        Assert.AreEqual("\"ell\"", Eval("""{"substr":["hello",1,-1]}"""));
    }

    [TestMethod]
    public void Substr_Ascii_ZeroActualLength()
    {
        // L446-448: actualLength <= 0 → EmptyString
        Assert.AreEqual("\"\"", Eval("""{"substr":["hello",2,0]}"""));
    }

    // ─── CoercingElementEquals: bool left/right (L759-771) ──────────

    [TestMethod]
    public void Equals_BoolVsNumber_CoercesBool()
    {
        // L759-762: left is True → coerce to 1, compare
        Assert.AreEqual("true", Eval("""{"==":[true, 1]}"""));
    }

    [TestMethod]
    public void Equals_NumberVsBool_CoercesBool()
    {
        // L765-768: right is True → coerce to 1, compare
        Assert.AreEqual("true", Eval("""{"==":[0, false]}"""));
    }

    // ─── CompareCoerced (public helper L778-804) ────────────────────

    [TestMethod]
    public void CompareCoerced_NonCoercibleStrings_ReturnsFalse()
    {
        // L790-792: TryCoerceToNumber fails → false
        Assert.AreEqual("false", Eval("""{"<":["abc","xyz"]}"""));
    }

    // ─── CoerceToBigNumber (public helper L809-840) ─────────────────

    [TestMethod]
    public void CoerceToBigNumber_True_One()
    {
        // L820-822: True → BigNumber.One (via CG helper path)
        Assert.AreEqual("1", Eval("""{"asBigNumber":true}"""));
    }

    [TestMethod]
    public void CoerceToBigNumber_String_Coerced()
    {
        // L830-837: string → coerce → parse
        Assert.AreEqual("42", Eval("""{"asBigNumber":"42"}"""));
    }

    // ─── DoubleToElement format fail (L895-900) ─────────────────────
    // This covers the unlikely path where Utf8Formatter.TryFormat fails.
    // NaN/Infinity will typically be caught earlier, so this is hard to hit directly.
    // The test below at least exercises the DoubleToElement path via large division:

    [TestMethod]
    public void Division_LargeResult_ProducesElement()
    {
        // Exercises DoubleToElement path
        Assert.AreEqual("1000000000", Eval("""{"/":[10000000000,10]}"""));
    }

    private static string Eval(string rule, string data = "{}")
    {
        JsonElement ruleElem = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(rule));
        JsonElement dataElem = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(data));
        JsonLogicRule logicRule = new(ruleElem);
        JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElem);
        return result.ValueKind == JsonValueKind.Undefined ? "undefined" : result.GetRawText();
    }
}
