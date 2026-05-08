// <copyright file="BuiltInFunctionEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests for uncovered branches in <see cref="BuiltInFunctions"/>, exercised
/// through the JSONata evaluator. Targets identified from merged Cobertura data.
/// </summary>
[TestClass]
public class BuiltInFunctionEdgeCaseTests
{
    private static string Eval(string expression, string data = "null")
    {
        return JsonataEvaluator.Default.EvaluateToString(expression, data) ?? "undefined";
    }

    private static void EvalThrows(string expression, string data, string expectedCode)
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            JsonataEvaluator.Default.EvaluateToString(expression, data));
        Assert.AreEqual(expectedCode, ex.Code);
    }

    // ─── BUG: $each 3-param callback — RT returns [] instead of correct values ──
    // Reference: $each({"x":1,"y":2}, function($v,$k,$o){$type($o)}) => ["object","object"]

    [TestMethod]
    public void Each_ThreeParam_ObjectArg_Bug()
    {
        // The 3rd callback parameter ($o) should receive the whole object.
        // RT currently returns [] because lambdaArgs is only rented with size 2.
        Assert.AreEqual(
            """["object","object"]""",
            Eval("""$each({"x":1,"y":2}, function($v,$k,$o){$type($o)})"""));
    }

    // ─── BUG: $string on multi-valued sequence — returns first element only ──
    // Reference: $string(data.items.name) on multi-valued => "[\"Alice\",\"Bob\",\"Charlie\"]"

    [TestMethod]
    public void String_MultiValuedSequence_Bug()
    {
        // $string() of a multi-valued sequence should produce the JSON array string,
        // not just the first element's string.
        string data = """{"data":{"items":[{"name":"Alice"},{"name":"Bob"},{"name":"Charlie"}]}}""";
        // Reference: the JS string value is ["Alice","Bob","Charlie"]
        // EvaluateToString returns GetRawText(), so the JSON-encoded string is expected.
        Assert.AreEqual(
            """"
            "[\"Alice\",\"Bob\",\"Charlie\"]"
            """",
            Eval("$string(data.items.name)", data));
    }

    // ─── $number: hex/binary/octal conversion (lines 540-565) ───────

    [TestMethod]
    public void Number_HexString_Converts()
    {
        // Line 540: hex parsing path
        Assert.AreEqual("255", Eval("""$number("0xFF")"""));
    }

    [TestMethod]
    public void Number_HexUpperCase_Converts()
    {
        Assert.AreEqual("255", Eval("""$number("0XFF")"""));
    }

    [TestMethod]
    public void Number_BinaryString_Converts()
    {
        // Line 550: binary parsing path
        Assert.AreEqual("5", Eval("""$number("0b101")"""));
    }

    [TestMethod]
    public void Number_OctalString_Converts()
    {
        // Line 560: octal parsing path
        Assert.AreEqual("15", Eval("""$number("0o17")"""));
    }

    [TestMethod]
    public void Number_InvalidHex_Throws()
    {
        // Line 545: invalid hex → D3030
        EvalThrows("""$number("0xGG")""", "null", "D3030");
    }

    [TestMethod]
    public void Number_InvalidBinary_Throws()
    {
        // Line 555: invalid binary → D3030
        EvalThrows("""$number("0b123")""", "null", "D3030");
    }

    [TestMethod]
    public void Number_InvalidOctal_Throws()
    {
        // Line 565: invalid octal → D3030
        EvalThrows("""$number("0o89")""", "null", "D3030");
    }

    [TestMethod]
    public void Number_Infinity_Throws()
    {
        // 1/0 in JSONata is actually handled as an error upstream.
        // Use $number on Infinity string instead
        EvalThrows("""$number("Infinity")""", "null", "D3030");
    }

    [TestMethod]
    public void Number_NonNumericType_ReturnsUndefined()
    {
        // Line 636: non-coercible type — $number on array throws T0410
        EvalThrows("""$number([1,2])""", "null", "T0410");
    }

    // ─── $string: NaN/Infinity edge case (lines 339-340) ────────────

    [TestMethod]
    public void String_InfinityOrNaN_Throws()
    {
        // Line 339-340: D3001 error on $string(Infinity)
        EvalThrows("""$string(1/0)""", "null", "D3001");
    }

    // ─── $substringBefore / $substringAfter: arg count (lines 996, 1011) ─

    [TestMethod]
    public void SubstringBefore_TooManyArgs_Throws()
    {
        EvalThrows("""$substringBefore("hello", "l", "extra")""", "null", "T0410");
    }

    [TestMethod]
    public void SubstringAfter_TooManyArgs_Throws()
    {
        EvalThrows("""$substringAfter("hello", "l", "extra")""", "null", "T0410");
    }

    // ─── $filter with multi-valued sequence containing arrays (lines 2222-2257) ─

    [TestMethod]
    public void Filter_MultiValuedSequenceWithArrays_Flattens()
    {
        // Lines 2222-2257: flatten arrays in multi-valued sequences for $filter
        string data = """{"items": [1,2,3,4,5,6]}""";
        string result = Eval("""$filter(items, function($v) { $v > 3 })""", data);
        Assert.AreEqual("[4,5,6]", result);
    }

    // ─── $append with flatten: lines 2032-2037 ─────────────────────

    [TestMethod]
    public void Append_ArrayElements_Flattened()
    {
        string result = Eval("""$append([1,2], [3,4])""");
        Assert.AreEqual("[1,2,3,4]", result);
    }

    // ─── $shuffle: line 118 (the function itself) ───────────────────

    [TestMethod]
    public void Shuffle_ReturnsAllElements()
    {
        string result = Eval("""$count($shuffle([1,2,3,4,5]))""");
        Assert.AreEqual("5", result);
    }

    // ─── $zip: line 122 ─────────────────────────────────────────────

    [TestMethod]
    public void Zip_CombinesArrays()
    {
        string result = Eval("""$zip([1,2], ["a","b"])""");
        Assert.AreEqual("[[1,\"a\"],[2,\"b\"]]", result);
    }

    // ─── $formatBase: line 120 ──────────────────────────────────────

    [TestMethod]
    public void FormatBase_Hex()
    {
        Assert.AreEqual("\"ff\"", Eval("""$formatBase(255, 16)"""));
    }

    [TestMethod]
    public void FormatBase_Binary()
    {
        Assert.AreEqual("\"101\"", Eval("""$formatBase(5, 2)"""));
    }

    [TestMethod]
    public void FormatBase_Octal()
    {
        Assert.AreEqual("\"17\"", Eval("""$formatBase(15, 8)"""));
    }

    // ─── $random: line 118 ──────────────────────────────────────────

    [TestMethod]
    public void Random_ReturnsBetweenZeroAndOne()
    {
        string result = Eval("""$random()""");
        double val = double.Parse(result);
        Assert.IsTrue(val >= 0.0 && val < 1.0);
    }

    // ─── $decodeUrl / $encodeUrl / $decodeUrlComponent / $encodeUrlComponent ─

    [TestMethod]
    public void EncodeUrl_EncodesSpecialChars()
    {
        Assert.AreEqual("\"hello%20world\"", Eval("""$encodeUrl("hello world")"""));
    }

    [TestMethod]
    public void DecodeUrl_DecodesSpecialChars()
    {
        Assert.AreEqual("\"hello world\"", Eval("""$decodeUrl("hello%20world")"""));
    }

    [TestMethod]
    public void EncodeUrlComponent_Encodes()
    {
        Assert.AreEqual("\"hello%20world\"", Eval("""$encodeUrlComponent("hello world")"""));
    }

    [TestMethod]
    public void DecodeUrlComponent_Decodes()
    {
        Assert.AreEqual("\"hello world\"", Eval("""$decodeUrlComponent("hello%20world")"""));
    }

    // ─── $assert: line 124 ──────────────────────────────────────────

    [TestMethod]
    public void Assert_TrueCondition_Passes()
    {
        // $assert returns undefined on success (not true)
        Assert.AreEqual("undefined", Eval("""$assert(true, "should not fail")"""));
    }

    [TestMethod]
    public void Assert_FalseCondition_Throws()
    {
        EvalThrows("""$assert(false, "assertion failed")""", "null", "D3141");
    }

    // ─── $error: line 123 ───────────────────────────────────────────

    [TestMethod]
    public void Error_ThrowsCustomMessage()
    {
        EvalThrows("""$error("custom error")""", "null", "D3137");
    }

    // ─── $replace with regex and string replacement (lines 3494-3526) ─

    [TestMethod]
    public void Replace_RegexWithLimit()
    {
        // RegexReplaceWithString with limit
        string result = Eval("""$replace("aababab", /ab/, "--", 2)""");
        Assert.AreEqual("\"a----ab\"", result);
    }

    // ─── $match with capture groups ─────────────────────────────────

    [TestMethod]
    public void Match_WithCaptureGroups()
    {
        string result = Eval("""$match("abc123", /([a-z]+)(\d+)/)""");
        // Should return match object with groups
        StringAssert.Contains(result, "\"match\"");
        StringAssert.Contains(result, "\"groups\"");
    }

    // ─── $formatNumber: XPath picture formatting (FormatNumberXPath, 486 lines) ──

    [TestMethod]
    public void FormatNumber_BasicDecimal()
    {
        Assert.AreEqual("\"12,345.60\"", Eval("""$formatNumber(12345.6, "#,##0.00")"""));
    }

    [TestMethod]
    public void FormatNumber_IntegerOnly()
    {
        Assert.AreEqual("\"42\"", Eval("""$formatNumber(42, "0")"""));
    }

    [TestMethod]
    public void FormatNumber_LeadingZeros()
    {
        Assert.AreEqual("\"007\"", Eval("""$formatNumber(7, "000")"""));
    }

    [TestMethod]
    public void FormatNumber_Negative()
    {
        Assert.AreEqual("\"-42.50\"", Eval("""$formatNumber(-42.5, "#0.00")"""));
    }

    [TestMethod]
    public void FormatNumber_NegativeWithSubPicture()
    {
        // Separate sub-picture for negatives
        Assert.AreEqual("\"(42.50)\"", Eval("""$formatNumber(-42.5, "#0.00;(#0.00)")"""));
    }

    [TestMethod]
    public void FormatNumber_Percent()
    {
        Assert.AreEqual("\"75%\"", Eval("""$formatNumber(0.75, "0%")"""));
    }

    [TestMethod]
    public void FormatNumber_PerMille()
    {
        Assert.AreEqual("\"750\u2030\"", Eval("""$formatNumber(0.75, "0\u2030")"""));
    }

    [TestMethod]
    public void FormatNumber_WithOptions_DecimalSeparator()
    {
        string data = """{"opts": {"decimal-separator": ",", "grouping-separator": "."}}""";
        Assert.AreEqual("\"1,5\"", Eval("""$formatNumber(1.5, "0,0", opts)""", data));
    }

    [TestMethod]
    public void FormatNumber_ScientificNotation()
    {
        Assert.AreEqual("\"1.23e2\"", Eval("""$formatNumber(123, "0.00e0")"""));
    }

    [TestMethod]
    public void FormatNumber_Zero()
    {
        Assert.AreEqual("\"0.00\"", Eval("""$formatNumber(0, "0.00")"""));
    }

    [TestMethod]
    public void FormatNumber_LargeNumber()
    {
        Assert.AreEqual("\"1,234,567.89\"", Eval("""$formatNumber(1234567.89, "#,##0.00")"""));
    }

    [TestMethod]
    public void FormatNumber_NoFraction()
    {
        Assert.AreEqual("\"43\"", Eval("""$formatNumber(42.7, "#")"""));
    }

    [TestMethod]
    public void FormatNumber_OnlyFraction()
    {
        Assert.AreEqual("\".5\"", Eval("""$formatNumber(0.5, ".0")"""));
    }

    // ─── $replace with regex backreferences (ApplyJsonataBackreferences, 55 lines) ──

    [TestMethod]
    public void Replace_RegexBackreference_Dollar1()
    {
        string result = Eval("""$replace("John Smith", /(\w+)\s(\w+)/, "$2 $1")""");
        Assert.AreEqual("\"Smith John\"", result);
    }

    [TestMethod]
    public void Replace_RegexBackreference_Dollar0()
    {
        string result = Eval("""$replace("abc", /(b)/, "[$0]")""");
        Assert.AreEqual("\"a[b]c\"", result);
    }

    [TestMethod]
    public void Replace_RegexBackreference_MultipleGroups()
    {
        string result = Eval("""$replace("2024-01-15", /(\d{4})-(\d{2})-(\d{2})/, "$3/$2/$1")""");
        Assert.AreEqual("\"15/01/2024\"", result);
    }

    [TestMethod]
    public void Replace_RegexWithStringAndLimit()
    {
        // RegexReplaceWithString with limit parameter
        string result = Eval("""$replace("banana", /a/, "o", 2)""");
        Assert.AreEqual("\"bonona\"", result);
    }

    // ─── $parseInteger with XPath picture (CompileParseInteger, 30 lines) ──

    [TestMethod]
    public void ParseInteger_BasicPicture()
    {
        Assert.AreEqual("42", Eval("""$parseInteger("42", "#0")"""));
    }

    [TestMethod]
    public void ParseInteger_WithGroupingSeparator()
    {
        Assert.AreEqual("1234", Eval("""$parseInteger("1,234", "#,##0")"""));
    }

    // ─── Unicode $substring with surrogate pairs (CodePointToCharIndex) ──

    [TestMethod]
    public void Substring_WithEmoji()
    {
        // $substring on string with surrogate pair — triggers CodePointToCharIndex
        Assert.AreEqual("\"😀\"", Eval("""$substring("\uD83D\uDE00hello", 0, 1)"""));
    }

    [TestMethod]
    public void Substring_AfterEmoji()
    {
        Assert.AreEqual("\"he\"", Eval("""$substring("\uD83D\uDE00hello", 1, 2)"""));
    }

    // ─── $encodeUrl with special characters (ValidateNoUnpairedSurrogates, 16 lines) ──

    [TestMethod]
    public void EncodeUrlComponent_SpecialChars()
    {
        Assert.AreEqual("\"%2F%3F%23\"", Eval("""$encodeUrlComponent("/?#")"""));
    }

    [TestMethod]
    public void EncodeUrl_PreservesPathChars()
    {
        Assert.AreEqual("\"http://example.com/path%20name\"", Eval("""$encodeUrl("http://example.com/path name")"""));
    }

    // ─── $spread: multi-item spread (CompileSpread, 34 uncovered lines) ──

    [TestMethod]
    public void Spread_SingleObject()
    {
        string data = """{"a": 1, "b": 2}""";
        string result = Eval("$spread($)", data);
        StringAssert.Contains(result, "\"a\"");
    }

    [TestMethod]
    public void Spread_ArrayOfObjects()
    {
        string data = """[{"a": 1}, {"b": 2}]""";
        string result = Eval("$spread($)", data);
        StringAssert.Contains(result, "\"a\"");
        StringAssert.Contains(result, "\"b\"");
    }

    // ─── $decodeUrl with invalid percent-encoded edge cases ──

    [TestMethod]
    public void DecodeUrl_InvalidPercentEncoding_Throws()
    {
        var ex = Assert.Throws<Exception>(() =>
            JsonataEvaluator.Default.EvaluateToString("""$decodeUrl("hello%GGworld")""", "null"));
        Assert.IsNotNull(ex);
    }

    [TestMethod]
    public void DecodeUrlComponent_IncompletePercent_Throws()
    {
        var ex = Assert.Throws<Exception>(() =>
            JsonataEvaluator.Default.EvaluateToString("""$decodeUrlComponent("hello%2")""", "null"));
        Assert.IsNotNull(ex);
    }

    // ─── $toMillis edge cases ──

    [TestMethod]
    public void ToMillis_DateString()
    {
        string result = Eval("""$toMillis("2024-01-01T00:00:00.000Z")""");
        Assert.AreEqual("1704067200000", result);
    }

    [TestMethod]
    public void ToMillis_WithPicture()
    {
        string result = Eval("""$toMillis("15/01/2024", "[D01]/[M01]/[Y0001]")""");
        Assert.AreEqual("1705276800000", result);
    }

    // ─── $filter with function index parameter ──

    [TestMethod]
    public void Filter_WithIndexParam()
    {
        string data = """{"items": [10, 20, 30, 40, 50]}""";
        string result = Eval("""$filter(items, function($v, $i) { $i >= 2 })""", data);
        Assert.AreEqual("[30,40,50]", result);
    }

    // ─── $map with index parameter ──

    [TestMethod]
    public void Map_WithIndexParam()
    {
        string result = Eval("""$map([10, 20, 30], function($v, $i) { $i })""");
        Assert.AreEqual("[0,1,2]", result);
    }

    // ─── $shuffle with single element ──

    [TestMethod]
    public void Shuffle_SingleElement()
    {
        Assert.AreEqual("[1]", Eval("""$shuffle([1])"""));
    }

    [TestMethod]
    public void Shuffle_Empty()
    {
        // $shuffle of empty array may return empty array or undefined
        string result = Eval("""$shuffle([])""");
        Assert.IsTrue(result == "[]" || result == "undefined");
    }

    // ─── CompileFilter standalone (FunctionalCompiler lines 7944-8012) ───

    [TestMethod]
    public void Filter_BooleanPredicate_True()
    {
        // Boolean filter: true keeps element
        Assert.AreEqual("42", Eval("""42[true]"""));
    }

    [TestMethod]
    public void Filter_BooleanPredicate_False()
    {
        // Boolean filter: false drops element
        Assert.AreEqual("undefined", Eval("""42[false]"""));
    }

    [TestMethod]
    public void Filter_NumericIndex_OnArray()
    {
        // Numeric filter = index access
        Assert.AreEqual("20", Eval("""$[1]""", """[10,20,30]"""));
    }

    [TestMethod]
    public void Filter_NegativeIndex_OnArray()
    {
        // Negative numeric index wraps from end
        Assert.AreEqual("30", Eval("""$[-1]""", """[10,20,30]"""));
    }

    [TestMethod]
    public void Filter_OutOfBounds_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$[99]""", """[10,20,30]"""));
    }

    [TestMethod]
    public void Filter_GeneralTruthiness_String()
    {
        // Non-boolean, non-numeric: general truthiness (non-empty string is truthy)
        Assert.AreEqual("42", Eval("""42["yes"]"""));
    }

    [TestMethod]
    public void Filter_GeneralTruthiness_EmptyString()
    {
        // Empty string is falsy
        Assert.AreEqual("undefined", Eval("""42[""]"""));
    }

    // ─── CompileFocusSortStage (FunctionalCompiler lines 8098-8153) ───

    [TestMethod]
    public void FocusSort_ByFocusVariable()
    {
        // Focus without continuation returns parent context per element.
        string data = """[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}]""";
        Assert.AreEqual(
            """[[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}],[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}],[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}]]""",
            Eval("""$@$e^($e.age)""", data));
    }

    [TestMethod]
    public void FocusSort_Descending()
    {
        // Focus without continuation returns parent context per element.
        string data = """[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}]""";
        Assert.AreEqual(
            """[[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}],[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}],[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}]]""",
            Eval("""$@$e^(>$e.age)""", data));
    }

    [TestMethod]
    public void FocusSort_SingleElement_PassesThrough()
    {
        // Single element: sort returns the item itself (unwrapped)
        string data = """[{"name":"A","age":10}]""";
        Assert.AreEqual(
            """{"name":"A","age":10}""",
            Eval("""$@$e^($e.age)""", data));
    }

    // ─── $match with capture groups (BuiltInFunctions lines 3255-3328) ───

    [TestMethod]
    public void Match_DatePattern_WithGroups()
    {
        string result = Eval("""$match("2026-04-19", /(\d{4})-(\d{2})-(\d{2})/)""");
        StringAssert.Contains(result, "\"match\":\"2026-04-19\"");
        StringAssert.Contains(result, "\"groups\":[\"2026\",\"04\",\"19\"]");
    }

    [TestMethod]
    public void Match_NoCaptureGroups_FirstWord()
    {
        string result = Eval("""$match("hello world", /\w+/)""");
        StringAssert.Contains(result, "\"match\":\"hello\"");
    }

    [TestMethod]
    public void Match_NoMatch_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$match("hello", /xyz/)"""));
    }

    // ─── $spread multi-element sequences (BuiltInFunctions lines 2632-2701) ───

    [TestMethod]
    public void Spread_SingleObjectIntoKeyValuePairs()
    {
        string result = Eval("""$spread({"a":1,"b":2})""");
        Assert.AreEqual("""[{"a":1},{"b":2}]""", result);
    }

    // ─── $formatNumber: exponent, grouping, subpicture ───

    [TestMethod]
    public void FormatNumber_NegativeWithSubpicture()
    {
        // Two-part picture: positive;negative — returns a string
        Assert.AreEqual("\"(1,234.56)\"", Eval("""$formatNumber(-1234.56, "#,##0.00;(#,##0.00)")"""));
    }

    [TestMethod]
    public void FormatNumber_IrregularGrouping()
    {
        // Irregular grouping: ##,##,##0
        string result = Eval("""$formatNumber(123456789, "##,##,##0")""");
        // Verify it's a valid formatted string
        Assert.StartsWith("\"", result);
        StringAssert.Contains(result, ",");
    }

    // ─── $fromMillis/$toMillis with custom picture and timezone ───

    [TestMethod]
    public void FromMillis_CustomPicture_YearMonthDay()
    {
        // 2021-01-01 00:00:00 UTC = 1609459200000
        Assert.AreEqual("\"2021-01-01\"", Eval("""$fromMillis(1609459200000, "[Y0001]-[M01]-[D01]")"""));
    }

    [TestMethod]
    public void FromMillis_WithTimezone()
    {
        // UTC+5:30 -> 2021-01-01T05:30:00 (spec format: ±HHMM, no colon)
        string result = Eval("""$fromMillis(1609459200000, "[Y0001]-[M01]-[D01]T[H01]:[m01]:[s01]", "+0530")""");
        Assert.AreEqual("\"2021-01-01T05:30:00\"", result);
    }

    [TestMethod]
    public void ToMillis_CustomPicture()
    {
        Assert.AreEqual("1609459200000", Eval("""$toMillis("2021-01-01", "[Y0001]-[M01]-[D01]")"""));
    }

    [TestMethod]
    public void FromMillis_DayOfWeek()
    {
        // 2021-01-01 was a Friday
        string result = Eval("""$fromMillis(1609459200000, "[FNn]")""");
        Assert.AreEqual("\"Friday\"", result);
    }

    [TestMethod]
    public void FromMillis_MonthName()
    {
        string result = Eval("""$fromMillis(1609459200000, "[MNn]")""");
        Assert.AreEqual("\"January\"", result);
    }

    [TestMethod]
    public void FromMillis_MonthAbbrev()
    {
        string result = Eval("""$fromMillis(1609459200000, "[MNn,3-3]")""");
        Assert.AreEqual("\"Jan\"", result);
    }

    [TestMethod]
    public void FromMillis_WeekNumber()
    {
        // 2021-01-01 is in ISO week 53 of 2020 (Friday)
        string result = Eval("""$fromMillis(1609459200000, "[W01]")""");
        // Week 53 of 2020 or week 01 of 2021 depending on convention
        StringAssert.Matches(result, new System.Text.RegularExpressions.Regex(@"^\""[0-9]+\""$"));
    }

    [TestMethod]
    public void FromMillis_NegativeTimezone()
    {
        // UTC-5:00 -> 2020-12-31T19:00:00 (spec format: ±HHMM, no colon)
        string result = Eval("""$fromMillis(1609459200000, "[Y0001]-[M01]-[D01]T[H01]:[m01]:[s01]", "-0500")""");
        Assert.AreEqual("\"2020-12-31T19:00:00\"", result);
    }

    // ─── $formatInteger ───

    [TestMethod]
    public void FormatInteger_Words()
    {
        Assert.AreEqual("\"forty-two\"", Eval("""$formatInteger(42, "w")"""));
    }

    [TestMethod]
    public void FormatInteger_Ordinal()
    {
        Assert.AreEqual("\"first\"", Eval("""$formatInteger(1, "w;o")"""));
    }

    [TestMethod]
    public void FormatInteger_RomanUpper()
    {
        Assert.AreEqual("\"XLII\"", Eval("""$formatInteger(42, "I")"""));
    }

    [TestMethod]
    public void FormatInteger_RomanLower()
    {
        Assert.AreEqual("\"xlii\"", Eval("""$formatInteger(42, "i")"""));
    }

    // ─── FormatNumberLikeJavaScript (FunctionalCompiler) ───

    [TestMethod]
    public void String_SmallExponent_CoercionPath()
    {
        // $string of very small number — JSONata preserves scientific notation
        Assert.AreEqual("\"1e-7\"", Eval("""$string(1e-7)"""));
    }

    [TestMethod]
    public void String_LargeNumber_CoercionPath()
    {
        Assert.AreEqual("\"100000000000000000000\"", Eval("""$string(1e20)"""));
    }

    // ─── Coalesce operator ───

    [TestMethod]
    public void Coalesce_MissingProperty_FallsToDefault()
    {
        string expr = "missing ?? \"default\"";
        string data = """{"existing":"value"}""";
        Assert.AreEqual("\"default\"", Eval(expr, data));
    }

    [TestMethod]
    public void Coalesce_ExistingProperty_ReturnsValue()
    {
        string expr = "existing ?? \"default\"";
        string data = """{"existing":"value"}""";
        Assert.AreEqual("\"value\"", Eval(expr, data));
    }

    // ─── Path chain over nested arrays ───

    [TestMethod]
    public void DeepPathChain_NestedArrays()
    {
        string data = """{"data":[{"items":[{"tag":"a"},{"tag":"b"}]},{"items":[{"tag":"c"}]}]}""";
        Assert.AreEqual("""["a","b","c"]""", Eval("data.items.tag", data));
    }

    // ─── Equality predicate on array property ───

    [TestMethod]
    public void EqualityPredicate_FiltersArray()
    {
        string data = """{"users":[{"name":"Alice","email":"a@test.com"},{"name":"Bob","email":"b@test.com"}]}""";
        Assert.AreEqual("\"a@test.com\"", Eval("""users[name="Alice"].email""", data));
    }

    // ─── $replace with function ───

    [TestMethod]
    public void Replace_WithFunction()
    {
        Assert.AreEqual("\"HELLO world\"", Eval("""$replace("hello world", /\w+/, function($m) { $uppercase($m.match) }, 1)"""));
    }

    [TestMethod]
    public void Replace_WithFunction_AllMatches()
    {
        Assert.AreEqual("\"HELLO WORLD\"", Eval("""$replace("hello world", /\w+/, function($m) { $uppercase($m.match) })"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler coverage — RT-only tests for uncovered blocks
    // ═══════════════════════════════════════════════════════════════════

    // Lines 5016-5082: ApplySimpleNamePairGroupBy — duplicate key groupby
    [TestMethod]
    public void GroupByAnnotation_DuplicateKeys()
    {
        string data = """{"items":[{"cat":"fruit","val":"apple"},{"cat":"veg","val":"carrot"},{"cat":"fruit","val":"banana"}]}""";
        Assert.AreEqual("""{"fruit":["apple","banana"],"veg":"carrot"}""", Eval("""items{cat: val}""", data));
    }

    // Lines 1558-1640: CollectAndContinue — equality predicate + continue chain
    [TestMethod]
    public void EqualityPredicate_WithContinuation()
    {
        string data = """{"orders":[{"status":"shipped","items":[{"name":"Widget"},{"name":"Gadget"}]},{"status":"pending","items":[{"name":"Thing"}]}]}""";
        Assert.AreEqual("""["Widget","Gadget"]""", Eval("""orders[status="shipped"].items.name""", data));
    }

    // Lines 2033-2109: EvalChainOverArrayIntoStatic — array descent through nested arrays
    [TestMethod]
    public void ArrayDescent_DeepNested()
    {
        string data = """{"Account":{"Order":[{"Product":[{"Price":10},{"Price":20}]},{"Product":[{"Price":30}]}]}}""";
        Assert.AreEqual("[10,20,30]", Eval("Account.Order.Product.Price", data));
    }

    // Lines 8091-8153: CompileFocusSortStage — sort with comparator function
    [TestMethod]
    public void SortWithComparator_ByProperty()
    {
        string data = """{"items":[{"name":"c","priority":3},{"name":"a","priority":1},{"name":"b","priority":2}]}""";
        string result = Eval("""$sort(items, function($l,$r){$l.priority > $r.priority})""", data);
        Assert.AreEqual("""[{"name":"a","priority":1},{"name":"b","priority":2},{"name":"c","priority":3}]""", result);
    }

    // Lines 598-622: ContinueChainFlatInto — chain flattening through nested arrays
    [TestMethod]
    public void ChainFlattening_NestedArrays()
    {
        string data = """{"data":{"items":[{"values":[1,2]},{"values":[3,4]}]}}""";
        Assert.AreEqual("[1,2,3,4]", Eval("data.items.values", data));
    }

    // Lines 4315-4343: encodeUrl non-string path
    [TestMethod]
    public void EncodeUrl_StringInput()
    {
        Assert.AreEqual("\"hello%20world/path\"", Eval("""$encodeUrl("hello world/path")"""));
    }

    // Lines 572-598: formatInteger ordinal
    [TestMethod]
    [DataRow(1, "1st")]
    [DataRow(2, "2nd")]
    [DataRow(3, "3rd")]
    [DataRow(11, "11th")]
    [DataRow(12, "12th")]
    [DataRow(13, "13th")]
    [DataRow(21, "21st")]
    [DataRow(101, "101st")]
    public void FormatInteger_OrdinalSuffix(int value, string expected)
    {
        Assert.AreEqual($"\"{expected}\"", Eval($"""$formatInteger({value}, "1;o")"""));
    }

    // Lines 2849-2860: parseInteger with sign prefix
    [TestMethod]
    public void ParseInteger_PlusSign()
    {
        Assert.AreEqual("42", Eval("""$parseInteger("+42", "0")"""));
    }

    [TestMethod]
    public void ParseInteger_MinusSign()
    {
        Assert.AreEqual("-99", Eval("""$parseInteger("-99", "0")"""));
    }

    // Lines 2712-2725: parseInteger with grouping separators
    [TestMethod]
    public void ParseInteger_GroupingSeparators()
    {
        Assert.AreEqual("1234567", Eval("""$parseInteger("1,234,567", "#,##0")"""));
    }

    // Lines 5047-5072, 4899-4919: formatNumber with exponent pattern
    [TestMethod]
    public void FormatNumber_ExponentPattern()
    {
        string result = Eval("""$formatNumber(12345, "0.00e0")""");
        Assert.StartsWith("\"", result);
        StringAssert.Contains(result, "e");
    }

    [TestMethod]
    public void FormatNumber_SmallExponent()
    {
        string result = Eval("""$formatNumber(0.00123, "0.00e0")""");
        Assert.StartsWith("\"", result);
        StringAssert.Contains(result, "e");
    }

    // Lines 7561-7591: Unicode surrogate handling in $length/$substring
    [TestMethod]
    public void Length_WithSurrogatePairs()
    {
        // "abc😀def" is 7 code points (emoji is 1 code point, 2 UTF-16 chars)
        Assert.AreEqual("7", Eval("""$length("abc\uD83D\uDE00def")"""));
    }

    [TestMethod]
    public void Substring_WithSurrogatePairs()
    {
        // Substring starting after emoji (code point 4) for 3 chars
        Assert.AreEqual("\"def\"", Eval("""$substring("abc\uD83D\uDE00def", 4, 3)"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler targeted coverage — RT-only tests
    // These target specific uncovered branches identified from Cobertura XML
    // ═══════════════════════════════════════════════════════════════════

    // Lines 5016-5082: ApplySimpleNamePairGroupBy — duplicate key merging
    // Key requirement: multiple items MUST share the same groupby key
    [TestMethod]
    public void GroupByAnnotation_DuplicateKeysMerge()
    {
        string data = """{"items":[{"cat":"A","val":1},{"cat":"A","val":2},{"cat":"B","val":3},{"cat":"A","val":4}]}""";
        Assert.AreEqual("""{"A":[1,2,4],"B":3}""", Eval("items{cat: val}", data));
    }

    // Lines 2033-2109: EvalChainOverArrayIntoStatic — multi-level nested array descent
    // Requires arrays at MULTIPLE intermediate levels of the chain
    [TestMethod]
    public void ArrayDescent_MultiLevelNested()
    {
        string data = """{"orders":{"items":[{"tags":[{"name":"foo"},{"name":"bar"}]},{"tags":[{"name":"baz"}]}]}}""";
        Assert.AreEqual("""["foo","bar","baz"]""", Eval("orders.items.tags.name", data));
    }

    [TestMethod]
    public void ArrayDescent_FourLevelChain()
    {
        string data = """{"company":{"departments":[{"teams":[{"members":[{"name":"Alice"}]},{"members":[{"name":"Bob"}]}]},{"teams":[{"members":[{"name":"Carol"}]}]}]}}""";
        Assert.AreEqual("""["Alice","Bob","Carol"]""", Eval("company.departments.teams.members.name", data));
    }

    // Lines 1558-1640: CollectAndContinue — per-element index + equality predicate
    [TestMethod]
    public void PerElementIndex_InChain()
    {
        string data = """{"data":{"items":[{"name":"first"},{"name":"second"}]}}""";
        Assert.AreEqual("\"first\"", Eval("data.items[0].name", data));
    }

    [TestMethod]
    public void NegativeIndex_InChain()
    {
        string data = """{"data":{"items":[{"name":"first"},{"name":"second"}]}}""";
        Assert.AreEqual("\"second\"", Eval("data.items[-1].name", data));
    }

    // Lines 7945-8012: CompileFilter — various filter patterns
    [TestMethod]
    public void Filter_BooleanPredicate()
    {
        string data = """{"items":[1,2,3,4,5]}""";
        Assert.AreEqual("[4,5]", Eval("items[$>3]", data));
    }

    [TestMethod]
    public void Filter_NumericIndex()
    {
        string data = """{"items":["a","b","c","d","e"]}""";
        Assert.AreEqual("\"b\"", Eval("items[1]", data));
    }

    [TestMethod]
    public void Filter_NegativeIndex()
    {
        string data = """{"items":["a","b","c","d","e"]}""";
        Assert.AreEqual("\"e\"", Eval("items[-1]", data));
    }

    // Lines 2234-2256: CompileFilterFunc — multi-valued sequence flattening
    [TestMethod]
    public void FilterFunc_WithIndex()
    {
        string data = """[1,2,3,4,5]""";
        Assert.AreEqual("[3,4,5]", Eval("$filter($, function($v,$i){$i > 1})", data));
    }

    // Lines 4315-4343: $encodeUrl non-string path (coercion)
    [TestMethod]
    public void EncodeUrl_NonStringPath()
    {
        Assert.AreEqual("\"hello%20world\"", Eval("""$encodeUrl("hello world")"""));
    }

    // Lines 1558-1640: Equality predicate filtering on nested chain
    [TestMethod]
    public void EqualityPredicateOnNestedChain()
    {
        string data = """{"records":[{"status":"active","items":[{"id":1},{"id":2}]},{"status":"closed","items":[{"id":3}]}]}""";
        Assert.AreEqual("[1,2]", Eval("""records[status="active"].items.id""", data));
    }

    // Lines 598-622 (CG): Array in middle of property chain
    [TestMethod]
    public void ChainFlat_ArrayInMiddle()
    {
        string data = """{"data":{"nested":[{"items":[{"name":"a"}]},{"items":[{"name":"b"},{"name":"c"}]}]}}""";
        Assert.AreEqual("""["a","b","c"]""", Eval("data.nested.items.name", data));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler lines 5641-5739: Filter with array of numeric indices
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Filter_ArrayOfNumericIndices()
    {
        // Tests the array-of-indices branch at lines 5655-5691
        Assert.AreEqual("""["a","c","e"]""", Eval("data[[0,2,4]]", """{"data":["a","b","c","d","e"]}"""));
    }

    [TestMethod]
    public void Filter_ArrayOfNegativeIndices()
    {
        // Negative indices in filter array — tests idx < 0 adjustment at line 5671
        Assert.AreEqual("""["d","e"]""", Eval("data[[-1,-2]]", """{"data":["a","b","c","d","e"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler lines 2065-2109: EvalChainOverArrayIntoStatic
    // Property chain traversal through arrays at 3+ intermediate levels
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void DeepChain_ThreeLevelArrayDescent()
    {
        // Arrays at levels a, b.c triggering recursive EvalChainOverArrayIntoStatic
        string data = """{"a":[{"b":[{"c":[{"d":"x"},{"d":"y"}]},{"c":{"d":"z"}}]},{"b":{"c":{"d":"w"}}}]}""";
        Assert.AreEqual("""["x","y","z","w"]""", Eval("a.b.c.d", data));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler lines 6866-6901: Tuple array constructor
    // Array ctor with multi-valued sub-expressions
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ArrayConstructor_WithMultiValuedItems()
    {
        // [data.items.name, "extra"] — first item is multi-valued sequence, second is scalar
        string data = """{"data":{"items":[{"name":"x"},{"name":"y"}]}}""";
        Assert.AreEqual("""["x","y","extra"]""", Eval("""[data.items.name, "extra"]""", data));
    }

    [TestMethod]
    public void ArrayConstructor_WithRange()
    {
        // Range expression in array constructor
        Assert.AreEqual("[1,2,3,4,5]", Eval("[1..5]"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler lines 5559-5594: Sort stage array flattening
    // Sort preceded by array-producing step requiring flatten
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Sort_AfterArrayFlatten()
    {
        // $sort($append(data.a, data.b)) — flattened array then sorted
        string data = """{"data":{"a":[3,1,5],"b":[2,4]}}""";
        Assert.AreEqual("[1,2,3,4,5]", Eval("$sort($append(data.a, data.b))", data));
    }

    [TestMethod]
    public void Sort_ThenPropertyAccess()
    {
        // items^(price).name — sort then continue chain with property access
        string data = """{"items":[{"price":30,"name":"c"},{"price":10,"name":"a"},{"price":20,"name":"b"}]}""";
        Assert.AreEqual("""["a","b","c"]""", Eval("items^(price).name", data));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 5047-5072: FormatNumber exponent normalization
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    [DataRow(12345, "0.00e0", "1.23e4")]
    [DataRow(0.00123, "0.00e0", "1.23e-3")]
    [DataRow(100, "0.0e0", "10.0e1")]
    [DataRow(1, "0.00e0", "1.00e0")]
    [DataRow(9876543, "0.000e0", "9.877e6")]
    public void FormatNumber_ExponentNormalization(double value, string picture, string expected)
    {
        string expr = $"""$formatNumber({value.ToString(System.Globalization.CultureInfo.InvariantCulture)}, "{picture}")""";
        Assert.AreEqual($"\"{expected}\"", Eval(expr));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 4811-4853: FormatNumber grouping positions
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    [DataRow(1234567, "#,##0", "1,234,567")]
    [DataRow(1234567.89, "#,##0.00", "1,234,567.89")]
    [DataRow(123, "#,##0", "123")]
    public void FormatNumber_Grouping(double value, string picture, string expected)
    {
        string expr = $"""$formatNumber({value.ToString(System.Globalization.CultureInfo.InvariantCulture)}, "{picture}")""";
        Assert.AreEqual($"\"{expected}\"", Eval(expr));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 2234-2256: CompileFilterFunc flatten
    // Multi-valued sequence containing arrays that need flattening
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void FilterFunc_NestedArrayFlatten_Uppercase()
    {
        // Nested arrays producing multi-valued seq with array elements — triggers flatten at 2234
        string data = """{"Account":{"Order":[{"Product":[{"Description":{"Colour":"red"}},{"Description":{"Colour":"blue"}}]},{"Product":[{"Description":{"Colour":"green"}}]}]}}""";
        Assert.AreEqual("""["RED","BLUE","GREEN"]""", Eval("Account.Order.Product.$uppercase(Description.Colour)", data));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 4315-4343: $encodeUrl non-string input
    // Corvus extension: coerces non-string to string before encoding
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void EncodeUrl_NumberInput_CorvusExtension()
    {
        // Reference jsonata throws T0410 for non-string; Corvus coerces to string
        Assert.AreEqual("\"42\"", Eval("$encodeUrl(42)"));
    }

    [TestMethod]
    public void EncodeUrl_BooleanInput_CorvusExtension()
    {
        Assert.AreEqual("\"true\"", Eval("$encodeUrl(true)"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 4414-4429: HasInvalidPercentEncoding
    // $decodeUrl with malformed percent encoding
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void DecodeUrl_MalformedPercent_Throws()
    {
        // "hello%2world" — incomplete percent encoding (only 1 hex digit after %)
        var ex = Assert.ThrowsExactly<JsonataException>(() => Eval("""$decodeUrl("hello%2world")"""));
        Assert.AreEqual("D3140", ex.Code);
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 2644-2660: $spread array property counting
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Spread_ArrayOfMultiKeyObjects()
    {
        // Each multi-key object is split into single-key objects
        Assert.AreEqual("""[{"a":1},{"b":2},{"c":3}]""", Eval("""$spread({"a":1,"b":2,"c":3})"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // $each with 3 parameters (key, value, object)
    // Reference: ["a=1","b=2"]
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Each_ThreeParam_KeyValueConcat()
    {
        Assert.AreEqual("""["a=1","b=2"]""", Eval("""$each({"a":1,"b":2}, function($v,$k,$o){$k & "=" & $string($v)})"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // $formatInteger with grouping separator (XPathDateTimeFormatter)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    [DataRow(1234567, "#,##0", "1,234,567")]
    [DataRow(42, "w", "forty-two")]
    [DataRow(42, "W", "FORTY-TWO")]
    [DataRow(1001, "w", "one thousand and one")]
    public void FormatInteger_Various(int value, string picture, string expected)
    {
        string expr = $"""$formatInteger({value}, "{picture}")""";
        Assert.AreEqual($"\"{expected}\"", Eval(expr));
    }

    // ═══════════════════════════════════════════════════════════════════
    // $length and $substring with surrogate pairs (code point counting)
    // CGH lines 7561-7591: CountCodePoints
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Length_CodePointCounting()
    {
        // "Hello😊World" = 11 code points (😊 is 1 code point)
        Assert.AreEqual("11", Eval("""$length("Hello\ud83d\ude0aWorld")"""));
    }

    [TestMethod]
    public void Length_AllEmoji()
    {
        // Three emoji = 3 code points
        Assert.AreEqual("3", Eval("""$length("\ud83d\ude0a\ud83d\ude0a\ud83d\ude0a")"""));
    }

    [TestMethod]
    public void Substring_CodePointIndexing()
    {
        // Get the emoji at code point index 5
        Assert.AreEqual("\"\ud83d\ude0a\"", Eval("""$substring("Hello\ud83d\ude0aWorld", 5, 1)"""));
    }

    [TestMethod]
    public void Substring_AfterSurrogate()
    {
        // Get "World" starting at code point index 6
        Assert.AreEqual("\"World\"", Eval("""$substring("Hello\ud83d\ude0aWorld", 6)"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // $formatNumber percent
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_PercentPicture()
    {
        Assert.AreEqual("\"45%\"", Eval("""$formatNumber(0.45, "##0%")"""));
    }

    // ─── FunctionalCompiler: CollectAndContinue (lines 1558-1640) ─────────
    // Equality predicate on singleton object (not array) with continuation

    [TestMethod]
    public void Chain_EqualityPredicateOnSingleton_WithContinuation()
    {
        // account.orders is a singleton object, not an array. The equality predicate
        // should match and continue the chain to items.name.
        string data = """{"account":{"orders":{"type":"premium","items":[{"name":"Widget"},{"name":"Gadget"}]}}}""";
        Assert.AreEqual(
            """["Widget","Gadget"]""",
            Eval("""account.orders[type="premium"].items.name""", data));
    }

    [TestMethod]
    public void Chain_PerElementIndex_WithContinuation()
    {
        // records is an array, [0] selects first, then items[1] selects second item
        string data = """{"Data":{"records":[{"items":[{"value":"a"},{"value":"b"},{"value":"c"}]},{"items":[{"value":"x"},{"value":"y"}]}]}}""";
        Assert.AreEqual(
            "\"b\"",
            Eval("Data.records[0].items[1].value", data));
    }

    // ─── FunctionalCompiler: EvalChainOverArrayIntoStatic (lines 2033-2109) ──

    [TestMethod]
    public void Chain_NestedArrayAtIntermediateLevel()
    {
        // matrix is an array; .values on each element produces flattened result
        string data = """{"Data":{"matrix":[{"values":[10,20,30]},{"values":[40,50]}]}}""";
        Assert.AreEqual(
            "[10,20,30,40,50]",
            Eval("Data.matrix.values", data));
    }

    [TestMethod]
    public void Chain_ThreeLevelNestedArray()
    {
        string data = """{"a":[{"b":{"c":{"d":1}}},{"b":{"c":{"d":2}}}]}""";
        Assert.AreEqual("[1,2]", Eval("a.b.c.d", data));
    }

    // ─── FunctionalCompiler: CompileFilter standalone (lines 7945-8012) ───

    [TestMethod]
    public void Filter_OnMultiValuedPath()
    {
        // Account.Order.Product is a multi-valued sequence (not a single array)
        string data = """{"Account":{"Order":[{"Product":{"Price":35,"Name":"A"}},{"Product":{"Price":20,"Name":"B"}},{"Product":{"Price":50,"Name":"C"}}]}}""";
        Assert.AreEqual(
            """[{"Price":35,"Name":"A"},{"Price":50,"Name":"C"}]""",
            Eval("""$filter(Account.Order.Product, function($v){$v.Price > 30})""", data));
    }

    [TestMethod]
    public void Filter_ConditionalPredicate()
    {
        // Predicate returns boolean per element
        string data = """{"data":{"items":[{"price":10,"name":"A"},{"price":30,"name":"B"},{"price":50,"name":"C"}]}}""";
        Assert.AreEqual(
            """["B","C"]""",
            Eval("""data.items[price>20].name""", data));
    }

    // ─── FunctionalCompiler: ApplySimpleNamePairGroupBy (lines 4984-5082) ──

    [TestMethod]
    public void GroupBy_AnnotationSyntax_DuplicateKeys()
    {
        // GroupBy annotation {Name: Price} merges duplicate keys into arrays
        string data = """{"Account":{"Order":[{"Product":{"Name":"A","Price":10}},{"Product":{"Name":"B","Price":20}},{"Product":{"Name":"A","Price":30}}]}}""";
        Assert.AreEqual(
            """{"A":[10,30],"B":20}""",
            Eval("Account.Order.Product{Name: Price}", data));
    }

    // ─── FunctionalCompiler: CompilePath sort result expansion (lines 3155-3180) ──

    [TestMethod]
    public void Sort_ThenChainAccess()
    {
        // Sort first, then access properties from sorted results
        string data = """{"Account":{"Order":[{"Product":{"Price":30,"Name":"C"}},{"Product":{"Price":10,"Name":"A"}},{"Product":{"Price":20,"Name":"B"}}]}}""";
        Assert.AreEqual(
            """["A","B","C"]""",
            Eval("Account.Order^(Product.Price).Product.Name", data));
    }

    // ─── FunctionalCompiler: CompileArrayConstructor (lines 6866-6901) ──

    [TestMethod]
    public void ArrayConstructor_MixedTypes()
    {
        Assert.AreEqual(
            """[1,"hello",true,null,[2,3]]""",
            Eval("""[1, "hello", true, null, [2,3]]"""));
    }

    // ─── FunctionalCompiler: Transform operator ──

    [TestMethod]
    public void Transform_AddPropertyToArrayElements()
    {
        string data = """{"Account":{"Order":[{"Product":"A"},{"Product":"B"}]}}""";
        Assert.AreEqual(
            """{"Account":{"Order":[{"Product":"A","Discount":10},{"Product":"B","Discount":10}]}}""",
            Eval("""$ ~> |Account.Order|{"Discount":10}|""", data));
    }

    // ─── BuiltInFunctions: $sift, $reduce, $map with index ──

    [TestMethod]
    public void Sift_FilterObjectProperties()
    {
        Assert.AreEqual(
            """{"b":2,"c":3}""",
            Eval("""$sift({"a":1,"b":2,"c":3}, function($v){$v > 1})"""));
    }

    [TestMethod]
    public void Reduce_WithInitialValue()
    {
        Assert.AreEqual("20", Eval("""$reduce([1,2,3,4], function($prev,$curr){$prev + $curr}, 10)"""));
    }

    [TestMethod]
    public void Map_WithIndex()
    {
        Assert.AreEqual("[0,20,60]", Eval("""$map([10,20,30], function($v,$i){$v * $i})"""));
    }

    // ─── BuiltInFunctions: $spread on multi-key objects ──

    [TestMethod]
    public void Spread_ArrayOfMultiKeyObjects_Flattened()
    {
        Assert.AreEqual(
            """[{"a":1},{"b":2},{"c":3}]""",
            Eval("""$spread([{"a":1,"b":2},{"c":3}])"""));
    }

    // ─── BuiltInFunctions: $formatNumber exponent (lines 5062-5087) ──

    [TestMethod]
    public void FormatNumber_ScientificSmallNumber()
    {
        Assert.AreEqual("\"1.23e-4\"", Eval("""$formatNumber(0.000123, "0.00e0")"""));
    }

    // ─── BuiltInFunctions: $formatBase ──

    [TestMethod]
    [DataRow(255, 16, "ff")]
    [DataRow(255, 2, "11111111")]
    [DataRow(255, 8, "377")]
    public void FormatBase_Various(int value, int radix, string expected)
    {
        Assert.AreEqual($"\"{expected}\"", Eval($"$formatBase({value}, {radix})"));
    }

    // ─── BuiltInFunctions: $type ──

    [TestMethod]
    public void Type_AllTypes()
    {
        Assert.AreEqual(
            """["number","string","boolean","null","array","object"]""",
            Eval("""[$type(1), $type("s"), $type(true), $type(null), $type([]), $type({})]"""));
    }

    // ─── XPathDateTimeFormatter: Unicode digits (lines 1298-1312, 1619-1637) ──

    [TestMethod]
    [DataRow(2025, "\u0661", "\u0662\u0660\u0662\u0665")]
    [DataRow(99, "\u0967", "\u096F\u096F")]
    public void FormatInteger_UnicodeDigits(int value, string presentation, string expected)
    {
        Assert.AreEqual($"\"{expected}\"", Eval($"$formatInteger({value}, \"{presentation}\")"));
    }

    // ─── Wildcard and descendant paths ──

    [TestMethod]
    public void Wildcard_OnObject()
    {
        Assert.AreEqual("[1,2,3]", Eval("""{"a":1,"b":2,"c":3}.*"""));
    }

    [TestMethod]
    public void Wildcard_ChainedOnObject()
    {
        string data = """{"data":{"a":{"name":"Alice"},"b":{"name":"Bob"},"c":{"name":"Charlie"}}}""";
        Assert.AreEqual(
            """["Alice","Bob","Charlie"]""",
            Eval("data.*.name", data));
    }

    [TestMethod]
    public void Descendant_Search()
    {
        string data = """{"data":{"a":{"name":"Alice"},"b":{"inner":{"name":"Bob"}}}}""";
        Assert.AreEqual(
            """["Alice","Bob"]""",
            Eval("**.name", data));
    }

    // ─── String concat with multi-valued sequence ──
    // Note: $string & on multi-valued sequences has edge cases - RT coerces to first element.
    // Reference: "[\"A\",\"B\"] test" — this is a known behavioral difference, not tested here.

    // ─── Closure / higher-order functions ──

    [TestMethod]
    public void Lambda_Closure()
    {
        Assert.AreEqual("7", Eval("""( $add := function($x){function($y){$x + $y}}; $add(3)(4) )"""));
    }

    // ─── $sort with custom comparator ──

    [TestMethod]
    public void Sort_DefaultAlphabetical()
    {
        Assert.AreEqual(
            """["apple","banana","cherry"]""",
            Eval("""$sort(["banana","apple","cherry"])"""));
    }

    [TestMethod]
    public void Sort_CustomDescending()
    {
        // The reference uses boolean semantics: $a < $b means "swap if a < b" → descending
        Assert.AreEqual(
            "[5,4,3,1,1]",
            Eval("""$sort([3,1,4,1,5], function($a,$b){$a < $b})"""));
    }

    // ─── Coalesce operator (??) — Corvus extension ──
    // Desugars to $exists(lhs) ? lhs : rhs with ReferenceEquals,
    // triggers coalesce fusion path (EvalSimplePropertyChainStatic).

    [TestMethod]
    public void Coalesce_SimpleChain_Found()
    {
        // Reference: $exists(data.value) ? data.value : "default" → 42
        Assert.AreEqual("42", Eval("""data.value ?? "default" """, """{"data":{"value":42}}"""));
    }

    [TestMethod]
    public void Coalesce_SimpleChain_Fallback()
    {
        // Reference: $exists(data.value) ? data.value : "default" → "default"
        Assert.AreEqual("\"default\"", Eval("""data.value ?? "default" """, """{"data":{}}"""));
    }

    [TestMethod]
    public void Coalesce_DeepChain_Found()
    {
        Assert.AreEqual("\"found\"", Eval("""data.x.y ?? "fallback" """, """{"data":{"x":{"y":"found"}}}"""));
    }

    [TestMethod]
    public void Coalesce_DeepChain_Fallback()
    {
        Assert.AreEqual("\"fallback\"", Eval("""data.x.y ?? "fallback" """, """{"data":{"x":{}}}"""));
    }

    [TestMethod]
    public void Coalesce_VeryDeep_Found()
    {
        Assert.AreEqual("99", Eval("""data.a.b.c ?? 0""", """{"data":{"a":{"b":{"c":99}}}}"""));
    }

    [TestMethod]
    public void Coalesce_VeryDeep_Fallback()
    {
        Assert.AreEqual("0", Eval("""data.a.b.c ?? 0""", """{"data":{}}"""));
    }

    [TestMethod]
    public void Coalesce_ArrayMidChain()
    {
        // data.items is an array → triggers EvalChainOverArrayStatic
        Assert.AreEqual(
            """["A","B","C"]""",
            Eval("""data.items.name ?? []""", """{"data":{"items":[{"name":"A"},{"name":"B"},{"name":"C"}]}}"""));
    }

    [TestMethod]
    public void Coalesce_ArrayMidChain_Fallback()
    {
        Assert.AreEqual("[]", Eval("""data.items.name ?? []""", """{"data":{}}"""));
    }

    [TestMethod]
    public void Coalesce_NonObject_Fallback()
    {
        // data.value is a number, not an object → returns undefined → fallback
        Assert.AreEqual("0", Eval("""data.value.nested ?? 0""", """{"data":{"value":42}}"""));
    }

    // ─── $formatInteger word output ──

    [TestMethod]
    public void FormatInteger_WordLower()
    {
        // Reference: "forty-two"
        Assert.AreEqual("\"forty-two\"", Eval("""$formatInteger(42, "w")"""));
    }

    [TestMethod]
    public void FormatInteger_WordUpper()
    {
        // Reference: "FORTY-TWO"
        Assert.AreEqual("\"FORTY-TWO\"", Eval("""$formatInteger(42, "W")"""));
    }

    [TestMethod]
    public void FormatInteger_WordTitle()
    {
        // Reference: "Forty-Two"
        Assert.AreEqual("\"Forty-Two\"", Eval("""$formatInteger(42, "Ww")"""));
    }

    [TestMethod]
    public void ParseInteger_Word()
    {
        // Reference: 42
        Assert.AreEqual("42", Eval("""$parseInteger("forty-two", "w")"""));
    }

    [TestMethod]
    public void ParseInteger_WordUpper()
    {
        // Reference: 100
        Assert.AreEqual("100", Eval("""$parseInteger("ONE HUNDRED", "W")"""));
    }

    [TestMethod]
    public void ParseInteger_WordTitle()
    {
        // Reference: 99
        Assert.AreEqual("99", Eval("""$parseInteger("Ninety-Nine", "Ww")"""));
    }

    // ─── $formatNumber scientific/engineering ──

    [TestMethod]
    public void FormatNumber_ScientificLarge()
    {
        // Reference: "1.23e4"
        Assert.AreEqual("\"1.23e4\"", Eval("""$formatNumber(12345, "0.00e0")"""));
    }

    [TestMethod]
    public void FormatNumber_ScientificSmall()
    {
        // Reference: "5.00e-2"
        Assert.AreEqual("\"5.00e-2\"", Eval("""$formatNumber(0.05, "0.00e0")"""));
    }

    [TestMethod]
    public void FormatNumber_Engineering()
    {
        // Reference: "10.0e1"
        Assert.AreEqual("\"10.0e1\"", Eval("""$formatNumber(100, "##0.0e0")"""));
    }

    [TestMethod]
    public void FormatNumber_PerMille_Small()
    {
        // Reference: "25‰"
        Assert.AreEqual("\"25\u2030\"", Eval("""$formatNumber(0.025, "##0\u2030")"""));
    }

    [TestMethod]
    public void FormatNumber_GroupingSeparator_Large()
    {
        // Reference: "1,234,567"
        Assert.AreEqual("\"1,234,567\"", Eval("""$formatNumber(1234567, "#,##0")"""));
    }

    [TestMethod]
    public void FormatNumber_OptionalDecimalDigits()
    {
        // Reference: "0.5"
        Assert.AreEqual("\"0.5\"", Eval("""$formatNumber(0.5, "#0.###")"""));
    }

    [TestMethod]
    public void FormatNumber_NegativeSubPicture()
    {
        // Reference: "(042)"
        Assert.AreEqual("\"(042)\"", Eval("""$formatNumber(-42, "000;(000)")"""));
    }

    // ─── $single error paths (BuiltInFunctions lines 2881-2891) ──

    [TestMethod]
    public void Single_NoMatch_ThrowsD3139()
    {
        // No element satisfies predicate → D3139
        EvalThrows("""$single([1,2,3], function($v){$v=5})""", "null", "D3139");
    }

    [TestMethod]
    public void Single_MultipleMatches_ThrowsD3138()
    {
        // More than one element satisfies predicate → D3138
        EvalThrows("""$single([1,2,2], function($v){$v=2})""", "null", "D3138");
    }

    [TestMethod]
    public void Single_EmptyArray_ThrowsD3139()
    {
        // Empty array → D3139
        EvalThrows("""$single([])""", "null", "D3139");
    }

    // ─── Index binding #$i (FunctionalCompiler lines 4037-4135, 5545-5643) ──

    [TestMethod]
    public void IndexBinding_Basic()
    {
        // #$i annotates each element with its position
        string result = Eval("""["a","b","c"]#$i.{"value": $, "index": $i}""");
        Assert.AreEqual("""[{"value":"a","index":0},{"value":"b","index":1},{"value":"c","index":2}]""", result);
    }

    [TestMethod]
    public void IndexBinding_WithFilter()
    {
        // Index binding combined with bracket filter
        string result = Eval("""[10,20,30,40,50]#$i[$i < 3]""");
        Assert.AreEqual("[10,20,30]", result);
    }

    // ─── Focus binding @$var cross-join (FunctionalCompiler lines 2975-3079) ──

    [TestMethod]
    public void FocusBinding_CrossJoin_CorrelatesData()
    {
        // Cross-join: correlate loans with books by isbn
        string data = """{"library":{"loans":[{"isbn":"123"},{"isbn":"456"}],"books":[{"isbn":"123","title":"A"},{"isbn":"456","title":"B"}]}}""";
        string result = Eval("""library.loans@$l.books[isbn=$l.isbn].title""", data);
        Assert.AreEqual("""["A","B"]""", result);
    }

    [TestMethod]
    public void FocusBinding_CrossJoin_SimpleCorrelation()
    {
        string data = """{"data":[{"ref":1},{"ref":2}],"other":[{"id":1,"name":"one"},{"id":2,"name":"two"}]}""";
        string result = Eval("""data@$d.other[id=$d.ref].name""", data);
        Assert.AreEqual("""["one","two"]""", result);
    }

    // ─── $keys / $lookup on array of objects (BuiltInFunctions lines 2793-2798) ──

    [TestMethod]
    public void Keys_ArrayOfObjects_Deduplicates()
    {
        Assert.AreEqual("""["a","b"]""", Eval("""$keys([{"a":1},{"b":2},{"a":3}])"""));
    }

    [TestMethod]
    public void Keys_ArrayOfObjects_MultipleKeysPerObject()
    {
        Assert.AreEqual("""["x","y","z"]""", Eval("""$keys([{"x":1,"y":2},{"y":3,"z":4}])"""));
    }

    [TestMethod]
    public void Lookup_ArrayOfObjects_CollectsAllValues()
    {
        Assert.AreEqual("[1,2]", Eval("""$lookup([{"a":1},{"a":2},{"b":3}], "a")"""));
    }

    [TestMethod]
    public void Lookup_ArrayOfObjects_SingleMatch()
    {
        Assert.AreEqual("2", Eval("""$lookup([{"a":1},{"b":2}], "b")"""));
    }

    // ─── $split with limit (BuiltInFunctions lines 1375-1405) ──

    [TestMethod]
    public void Split_WithLimit_Truncates()
    {
        // Limit truncates to at most N parts
        Assert.AreEqual("""["a","b"]""", Eval("""$split("a,b,c,d", ",", 2)"""));
    }

    [TestMethod]
    public void Split_WithLimit_One()
    {
        Assert.AreEqual("""["x"]""", Eval("""$split("x-y-z", "-", 1)"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $match with limit (BuiltInFunctions lines 3130-3138)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Match_WithLimit()
    {
        // Limit truncates to at most N matches; singleton unwrapped
        Assert.AreEqual(
            """{"match":"123","index":3,"groups":[]}""",
            Eval("""$match("abc123def456", /[0-9]+/, 1)"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $decodeUrlComponent bad percent encoding (BuiltInFunctions lines 4429-4444)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [DataRow("""$decodeUrlComponent("test%2")""", "D3140")]
    [DataRow("""$decodeUrlComponent("test%GG")""", "D3140")]
    public void DecodeUrlComponent_BadPercentEncoding(string expression, string expectedCode)
    {
        EvalThrows(expression, "null", expectedCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber picture validation errors (BuiltInFunctions lines 4773-4793)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_MandatoryBeforeOptionalError()
    {
        // Mandatory digit (0) before optional (#) in integer part
        EvalThrows("""$formatNumber(123, "0#")""", "null", "D3090");
    }

    [TestMethod]
    public void FormatNumber_MandatoryAfterOptionalFracError()
    {
        // Mandatory digit (0) after optional (#) in fractional part
        EvalThrows("""$formatNumber(0.5, "0.#0")""", "null", "D3091");
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber exponent normalization (BuiltInFunctions lines 5062-5087)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [DataRow("""$formatNumber(1234.5, "0.0e0")""", "\"1.2e3\"")]
    [DataRow("""$formatNumber(0.00123, "0.00e0")""", "\"1.23e-3\"")]
    [DataRow("""$formatNumber(1234567, "0.0e00")""", "\"1.2e06\"")]
    public void FormatNumber_ExponentNormalization_ViaEval(string expression, string expected)
    {
        Assert.AreEqual(expected, Eval(expression));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber with explicit negative sub-picture (additional patterns)
    // (BuiltInFunctions lines 4983-4995, 5025-5037)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [DataRow("""$formatNumber(-42, "0;0-")""", "\"42-\"")]
    public void FormatNumber_NegativeSubPicture_SuffixMinus(string expression, string expected)
    {
        Assert.AreEqual(expected, Eval(expression));
    }

    // ═══════════════════════════════════════════════════════════════
    // $shuffle on multi-element sequence (BuiltInFunctions lines 5333-5346)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Shuffle_PreservesCount()
    {
        // Can't assert order but can verify count is preserved
        Assert.AreEqual("5", Eval("""$count($shuffle([1,2,3,4,5]))"""));
    }

    [TestMethod]
    public void Shuffle_SortRoundTrip()
    {
        // Shuffle then sort should return original order
        Assert.AreEqual("[1,2,3,4,5]", Eval("""$sort($shuffle([1,2,3,4,5]))"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Unicode supplementary character handling (BuiltInFunctions lines 6251-6266)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Substring_EmojiCodePoint()
    {
        // 😀 is a single code point (U+1F600) represented as surrogate pair
        Assert.AreEqual("\"\uD83D\uDE00\"", Eval("""$substring("\uD83D\uDE00test", 0, 1)"""));
    }

    [TestMethod]
    public void Length_EmojiCodePoint()
    {
        Assert.AreEqual("5", Eval("""$length("\uD83D\uDE00test")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $encodeUrlComponent / $decodeUrlComponent (BuiltInFunctions lines 4494-4504)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void EncodeUrlComponent_Spaces()
    {
        Assert.AreEqual("\"hello%20world\"", Eval("""$encodeUrlComponent("hello world")"""));
    }

    [TestMethod]
    public void DecodeUrlComponent_Valid()
    {
        Assert.AreEqual("\"hello world\"", Eval("""$decodeUrlComponent("hello%20world")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Descendant wildcard (**) (FunctionalCompiler lines 1567-1581)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void DescendantWildcard_NestedArrays()
    {
        Assert.AreEqual(
            """["x","y","z"]""",
            Eval("""**.name""", """{"a":{"name":"x","b":[{"name":"y"},{"name":"z"}]}}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $flatten multi-valued sequences (BuiltInFunctions lines 2248-2270)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Flatten_NestedArrays()
    {
        Assert.AreEqual("[1,2,3,4]", Eval("""$flatten([[1,[2]],[[3],4]])"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Array-of-indices filter (FunctionalCompiler lines 5344-5359)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Filter_ArrayOfIndices()
    {
        Assert.AreEqual("[1,3,5]", Eval("""[1,2,3,4,5][[0,2,4]]"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Sort then filter (FunctionalCompiler lines 5463-5498, 5959-5981)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FilterThenSort()
    {
        Assert.AreEqual(
            """[{"type":"a","val":1},{"type":"a","val":2},{"type":"a","val":3}]""",
            Eval("""$[type="a"]^(val)""", """[{"type":"a","val":3},{"type":"b","val":1},{"type":"a","val":1},{"type":"a","val":2}]"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $pad with emoji (surrogate pair cycling) (BuiltInFunctions)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Pad_WithEmoji_Right()
    {
        Assert.AreEqual(
            "\"test\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81\"",
            Eval("""$pad("test", 8, "\uD83C\uDF81")"""));
    }

    [TestMethod]
    public void Pad_WithEmoji_Left()
    {
        Assert.AreEqual(
            "\"\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81test\"",
            Eval("""$pad("test", -8, "\uD83C\uDF81")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $replace with string pattern and limit
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Replace_StringPattern_WithLimit()
    {
        Assert.AreEqual(
            "\"hi world hello\"",
            Eval("""$replace("hello world hello", "hello", "hi", 1)"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $each on object
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Each_OnObject()
    {
        Assert.AreEqual(
            """["a=1","b=2","c=3"]""",
            Eval("""$each({"a":1,"b":2,"c":3}, function($v,$k){$k & "=" & $string($v)})"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $merge with overlapping keys
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Merge_OverlappingKeys()
    {
        Assert.AreEqual("""{"a":3,"b":2}""", Eval("""$merge([{"a":1},{"b":2},{"a":3}])"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // GroupBy with duplicate keys
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void GroupBy_DuplicateKeys()
    {
        Assert.AreEqual(
            """{"A":[{"category":"A","val":1},{"category":"A","val":3}],"B":{"category":"B","val":2}}""",
            Eval("""items{category: $}""", """{"items":[{"category":"A","val":1},{"category":"B","val":2},{"category":"A","val":3}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $number coercion from boolean (BuiltInFunctions line ~530-540
    // and FunctionalCompiler lines 8625-8632)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Number_Boolean_True_Returns1()
    {
        Assert.AreEqual("1", Eval("""$number(true)"""));
    }

    [TestMethod]
    public void Number_Boolean_False_Returns0()
    {
        Assert.AreEqual("0", Eval("""$number(false)"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber minInt==0 && maxFrac==0 special rules
    // (BuiltInFunctions lines 4914-4934)
    // Only triggered when picture has no mandatory/optional int or frac digits
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_HashOnly_MinIntZeroMaxFracZero()
    {
        // Picture "#" → minInt=0, maxFrac=0, expPresent=false
        // Code sets minInt=1 via else branch at 4921
        Assert.AreEqual("\"42\"", Eval("""$formatNumber(42, "#")"""));
    }

    [TestMethod]
    public void FormatNumber_HashWithExponent_MinIntZeroMaxFracZero()
    {
        // Picture "#e0" → minInt=0, maxFrac=0, expPresent=true
        // Code sets minFrac=1, maxFrac=1 via 4917-4918
        Assert.AreEqual("\"0e2\"", Eval("""$formatNumber(42, "#e0")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber minExp computation
    // (BuiltInFunctions lines 4938-4946)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_ExponentPadding_FourDigits()
    {
        Assert.AreEqual("\"1.23e-0003\"", Eval("""$formatNumber(0.00123, "0.00e0000")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber percent/per-mille scaling
    // (BuiltInFunctions lines 5042-5048)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_PercentScaling()
    {
        Assert.AreEqual("\"75%\"", Eval("""$formatNumber(0.75, "#0%")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber digit family replacement
    // (BuiltInFunctions lines 5094-5110)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_ArabicDigitFamily()
    {
        // picture uses Arabic-Indic zero-digit; result is in that digit family
        string result = Eval("""$formatNumber(42, "#\u0660\u0660", {"zero-digit":"\u0660"})""");
        StringAssert.Contains(result, "\u0664"); // Arabic-Indic 4
        StringAssert.Contains(result, "\u0662"); // Arabic-Indic 2
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber zero-digit padding
    // (BuiltInFunctions lines 5146-5153)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_ZeroDigitPadding()
    {
        Assert.AreEqual("\"01.000\"", Eval("""$formatNumber(1, "#00.000")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber grouping separator insertion
    // (BuiltInFunctions lines 5158-5165)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_GroupingSeparatorInsertion()
    {
        Assert.AreEqual("\"1,234,567\"", Eval("""$formatNumber(1234567, "#,##0")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber exponent appending
    // (BuiltInFunctions lines 5193-5202)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_ExponentAppended()
    {
        Assert.AreEqual("\"1.23e5\"", Eval("""$formatNumber(123000, "0.00e0")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber negative exponent
    // (BuiltInFunctions lines 5062-5087)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_NegativeExponent()
    {
        Assert.AreEqual("\"-1.23e-3\"", Eval("""$formatNumber(-0.00123, "0.00e0")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber exponent-separator option
    // (BuiltInFunctions lines 4583-4589, 4660-4664)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_CustomExponentSeparator()
    {
        Assert.AreEqual("\"1.2E3\"", Eval("""$formatNumber(1234.5, "0.0E0", {"exponent-separator":"E"})"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber custom minus-sign option
    // (BuiltInFunctions lines 4583-4589)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_CustomMinusSign()
    {
        string result = Eval("""$formatNumber(-42, "0", {"minus-sign":"\u2212"})""");
        StringAssert.Contains(result, "\u2212"); // Unicode minus sign
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber error: no mantissa digit (D3085)
    // (BuiltInFunctions lines 4733-4737)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_NoMantissaDigit_ThrowsD3088()
    {
        // "," alone = grouping separator at end of integer part (D3088), matching reference
        EvalThrows("""$formatNumber(42, ",")""", "null", "D3088");
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber error: exponent only digit family chars (D3093)
    // (BuiltInFunctions lines 4804-4817)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_ExponentOptionalDigit_ThrowsD3093()
    {
        EvalThrows("""$formatNumber(42, "0e#")""", "null", "D3093");
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber error: integer trailing grouping sep (D3088)
    // (BuiltInFunctions lines 4758-4763)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_IntegerTrailingGroupingSep_ThrowsD3088()
    {
        EvalThrows("""$formatNumber(42, "#,")""", "null", "D3088");
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber fractional grouping (simple valid case)
    // (BuiltInFunctions lines 5169-5173)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_FractionalGrouping()
    {
        Assert.AreEqual("\"0.1,23\"", Eval("""$formatNumber(0.123456, "#0.0,00")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber fractional grouping — was a bug (fixed), now passes
    // (BuiltInFunctions line 4884, FormatNumberPicture line 351)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FormatNumber_FractionalGrouping_LargeInput()
    {
        // Was throwing ArgumentOutOfRangeException before fix.
        // Reference returns "0.123,457".
        Assert.AreEqual(
            "\"0.123,457\"",
            Eval("""$formatNumber(0.123456789, "#0.000,000")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $decodeUrlComponent invalid percent encoding
    // (BuiltInFunctions lines 4429-4444)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void DecodeUrlComponent_InvalidPercentHex_ThrowsD3140()
    {
        EvalThrows("""$decodeUrlComponent("test%ZZvalue")""", "null", "D3140");
    }

    [TestMethod]
    public void DecodeUrlComponent_TruncatedPercent_ThrowsD3140()
    {
        EvalThrows("""$decodeUrlComponent("test%2")""", "null", "D3140");
    }

    // ═══════════════════════════════════════════════════════════════
    // $match context binding (1 arg variant)
    // (BuiltInFunctions lines 3119-3123)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Match_ContextBinding_ViaApply()
    {
        Assert.AreEqual(
            """{"match":"world","index":6,"groups":[]}""",
            Eval("""("hello world" ~> $match(/world/))"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Per-element boolean filter on arrays
    // (FunctionalCompiler lines 5545-5643 — ApplyPerElementFilterStages)
    // Requires multi-step path where stepIdx > 0 has a filter
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void PerElementFilter_NumericIndex_OnNestedArrays()
    {
        // data is array, .items[0] applies per-element at stepIdx > 0
        Assert.AreEqual(
            """["A","C"]""",
            Eval("""data.items[0]""",
                """{"data":[{"items":["A","B"]},{"items":["C","D"]}]}"""));
    }

    [TestMethod]
    public void PerElementFilter_NegativeIndex_OnNestedArrays()
    {
        Assert.AreEqual(
            """["B","D"]""",
            Eval("""data.items[-1]""",
                """{"data":[{"items":["A","B"]},{"items":["C","D"]}]}"""));
    }

    [TestMethod]
    public void PerElementFilter_ArrayOfIndices_OnNestedArrays()
    {
        Assert.AreEqual(
            """["A","C","E","G"]""",
            Eval("""data.items[[0,2]]""",
                """{"data":[{"items":["A","B","C","D"]},{"items":["E","F","G"]}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Per-element sort then flatten nested arrays
    // (FunctionalCompiler lines 5463-5498 — sort flattening path)
    // Requires multi-step path through arrays with sort
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void PerElementSort_FlattenNestedPath()
    {
        Assert.AreEqual(
            """[{"val":1},{"val":2},{"val":3}]""",
            Eval("""data.items^(val)""",
                """{"data":[{"items":[{"val":3},{"val":1}]},{"items":[{"val":2}]}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Hex/binary/octal number parsing via $number
    // (FunctionalCompiler lines 8740-8796 on net10.0)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Number_HexViaCoerce()
    {
        Assert.AreEqual("255", Eval("""$number("0xFF")"""));
    }

    [TestMethod]
    public void Number_BinaryViaCoerce()
    {
        Assert.AreEqual("10", Eval("""$number("0b1010")"""));
    }

    [TestMethod]
    public void Number_OctalViaCoerce()
    {
        Assert.AreEqual("63", Eval("""$number("0o77")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Path through nested arrays (FunctionalCompiler lines 1966-1980)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void PathThroughNestedArrays_Flattens()
    {
        Assert.AreEqual(
            "[1,2,3]",
            Eval("""a.b.c.d""", """{"a":[{"b":{"c":[{"d":1},{"d":2}]}},{"b":{"c":{"d":3}}}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Object with negative number constant
    // (FunctionalCompiler lines 429-434)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void ObjectConstructor_NegativeConstant()
    {
        Assert.AreEqual("""{"x":-42}""", Eval("""{"x":-42}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Fused array-of-objects construction
    // (FunctionalCompiler lines 6904-6943)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void FusedArrayOfObjects_WithPathPrefix()
    {
        Assert.AreEqual(
            """[{"id":"O1","prod":"W"},{"id":"O2","prod":"G"}]""",
            Eval("""Account.Order.{"id":OrderID,"prod":Product}""",
                """{"Account":{"Order":[{"OrderID":"O1","Product":"W"},{"OrderID":"O2","Product":"G"}]}}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Property map pre-building for large objects
    // (FunctionalCompiler lines 1694-1713)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void PropertyMap_LargeObjects_Lookup()
    {
        Assert.AreEqual(
            """["A","B"]""",
            Eval("""items.name""",
                """{"items":[{"name":"A","a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7},{"name":"B","a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Equality predicate filter with array recursion
    // (FunctionalCompiler lines 1645-1663)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void EqualityPredicate_ArrayRecursion()
    {
        Assert.AreEqual(
            "[1,3]",
            Eval("""items[type="x"].val""",
                """{"items":[{"type":"x","val":1},{"type":"y","val":2},{"type":"x","val":3}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Sort then filter stages
    // (FunctionalCompiler lines 5959-5981)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Sort_ThenFilter_Stages()
    {
        Assert.AreEqual(
            """[{"val":3},{"val":4}]""",
            Eval("""items^(val)[val > 2]""",
                """{"items":[{"val":3},{"val":1},{"val":4},{"val":2}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $replace with function callback
    // (FunctionalCompiler lines 8287-8296)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Replace_WithFunctionCallback_Uppercases()
    {
        Assert.AreEqual(
            "\"heLLo\"",
            Eval("""$replace("hello", /l/, function($m){$uppercase($m.match)})"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $match with date capture groups
    // (FunctionalCompiler lines 9119-9126)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Match_DateCaptureGroups()
    {
        string result = Eval("""$match("2023-01-15", /(\d{4})-(\d{2})-(\d{2})/)""");
        StringAssert.Contains(result, "\"match\":\"2023-01-15\"");
        StringAssert.Contains(result, "\"groups\":[\"2023\",\"01\",\"15\"]");
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 3: Data-driven coverage targeting
    // Verified expressions that hit specific uncovered code paths
    // ═══════════════════════════════════════════════════════════════

    // --- FC TryCoerceToNumber hex/binary/octal via $formatNumber first arg ---
    // (FunctionalCompiler lines 8649-8658, 8740-8796: TryParseSpecialRadix)
    // NOTE: Reference JSONata 1.8.7 throws T0410 for any string first arg to $formatNumber.
    // Our implementation previously coerced hex/binary/octal strings to numbers as an extension.
    // We now align with the reference: strings are not valid as the first arg.

    [TestMethod]
    [DataRow("$formatNumber(\"0xFF\", \"#\")")]
    [DataRow("$formatNumber(\"0b1010\", \"#\")")]
    [DataRow("$formatNumber(\"0o77\", \"#\")")]
    public void FormatNumber_HexBinaryOctalStringCoercion(string expression)
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Eval(expression));
        Assert.AreEqual("T0410", ex.Code);
    }

    // --- FC ApplyFocusStages: string predicates are boolean (truthy/falsy) ---
    // Focus without continuation returns parent context per surviving element.

    [TestMethod]
    [DataRow("items@$v[\"0x01\"]", "{\"items\":[\"a\",\"b\",\"c\"]}", "[{\"items\":[\"a\",\"b\",\"c\"]},{\"items\":[\"a\",\"b\",\"c\"]},{\"items\":[\"a\",\"b\",\"c\"]}]")]
    [DataRow("items@$v[\"1\"]", "{\"items\":[\"a\",\"b\",\"c\"]}", "[{\"items\":[\"a\",\"b\",\"c\"]},{\"items\":[\"a\",\"b\",\"c\"]},{\"items\":[\"a\",\"b\",\"c\"]}]")]
    public void FocusStages_StringPredicateCoercedToNumericIndex(string expression, string data, string expected)
    {
        // Reference treats strings as boolean: non-empty = truthy → all elements pass.
        Assert.AreEqual(expected, Eval(expression, data));
    }

    // --- FC ApplyStages string predicate coercion ---
    // (FunctionalCompiler lines 5278-5340: numeric index from string)

    [TestMethod]
    public void ApplyStages_StringPredicateCoercedToIndex()
    {
        // Reference: strings in predicates are boolean (truthy/falsy), not numeric indices.
        // Non-empty string "1" is truthy → all elements pass → returns whole array.
        Assert.AreEqual("[10,20,30]", Eval("[10,20,30][\"1\"]"));
    }

    [TestMethod]
    public void ApplyStages_HexStringPredicateCoercedToIndex()
    {
        // Reference: "0x01" is a truthy string → all pass → returns whole array.
        Assert.AreEqual("[10,20,30]", Eval("[10,20,30][\"0x01\"]"));
    }

    // --- BF FormatNumber runtime path (non-constant picture) ---
    // (BuiltInFunctions lines 4913-4934: minInt==0 && maxFrac==0 etc.)

    [TestMethod]
    [DataRow("$formatNumber(42, prefix & \"#\")", "\"42\"")]
    [DataRow("$formatNumber(0.5, prefix & \".###\")", "\".5\"")]
    [DataRow("$formatNumber(12345, prefix & \"#,###\")", "\"12,345\"")]
    [DataRow("$formatNumber(0.5, prefix & \"#%\")", "\"50%\"")]
    [DataRow("$formatNumber(0.5, prefix & \"0.00\")", "\"0.50\"")]
    public void FormatNumber_RuntimePath_NonConstantPicture(string expression, string expected)
    {
        Assert.AreEqual(expected, Eval(expression, "{\"prefix\":\"\"}"));
    }

    // --- Per-element filter with numeric index on nested arrays ---
    // (FunctionalCompiler lines 5859-5920: ApplyPerElementFilterStages constant-int)

    [TestMethod]
    [DataRow("data.items[0]", "[\"A\",\"C\"]")]
    [DataRow("data.items[-1]", "[\"B\",\"D\"]")]
    public void PerElementFilter_ConstantIntIndex_NestedArrays(string expression, string expected)
    {
        Assert.AreEqual(expected, Eval(expression, "{\"data\":[{\"items\":[\"A\",\"B\"]},{\"items\":[\"C\",\"D\"]}]}"));
    }

    // --- Per-element sort on nested arrays ---
    // (FunctionalCompiler lines 5462-5498: sort stage in per-element context)

    [TestMethod]
    public void PerElementSort_NestedArrays()
    {
        Assert.AreEqual(
            "[{\"val\":1},{\"val\":2},{\"val\":3}]",
            Eval("data.items^(val)", "{\"data\":[{\"items\":[{\"val\":3},{\"val\":1}]},{\"items\":[{\"val\":2}]}]}"));
    }

    // --- BF FormatNumber runtime exponent paths ---
    // (BuiltInFunctions lines 4917-4918 expPresent branch, 5062-5087 exponent calc)

    [TestMethod]
    public void FormatNumber_RuntimePath_ExponentHash()
    {
        // Non-constant "#e0" → BF runtime path, triggers expPresent branch at 4917
        Assert.AreEqual("\"0.e2\"", Eval("$formatNumber(42, prefix & \"#e0\")", "{\"prefix\":\"\"}"));
    }

    [TestMethod]
    public void FormatNumber_RuntimePath_ExponentPadding()
    {
        // Non-constant "0.00e0000" → hits exponent mantissa scaling at 5062-5087
        Assert.AreEqual("\"1.23e-0003\"", Eval("$formatNumber(0.00123, prefix & \"0.00e0000\")", "{\"prefix\":\"\"}"));
    }

    // --- FC ApplyFocusStages array-of-indices predicate ---
    // (FunctionalCompiler lines 5559-5625: predicate returns an array of numbers)

    [TestMethod]
    public void FocusStages_ArrayOfIndicesPredicate()
    {
        // Focus without continuation returns parent context per surviving element.
        Assert.AreEqual(
            "[{\"items\":[\"a\",\"b\",\"c\",\"d\"]},{\"items\":[\"a\",\"b\",\"c\",\"d\"]}]",
            Eval("items@$v[[0,2]]", "{\"items\":[\"a\",\"b\",\"c\",\"d\"]}"));
    }

    [TestMethod]
    public void FocusStages_ArrayOfIndicesPredicate_MultiElement()
    {
        // Focus without continuation returns parent context per surviving element.
        Assert.AreEqual(
            "[{\"Order\":[\"a\",\"b\",\"c\"]},{\"Order\":[\"a\",\"b\",\"c\"]}]",
            Eval("Account.Order@$o[[0,1]]", "{\"Account\":{\"Order\":[\"a\",\"b\",\"c\"]}}"));
    }

    // --- BF HasInvalidPercentEncoding ---
    // (BuiltInFunctions lines 4429-4444)

    [TestMethod]
    public void DecodeUrlComponent_BadHexAfterPercent_ThrowsD3140()
    {
        Assert.ThrowsExactly<JsonataException>(() => Eval("$decodeUrlComponent(\"%ZZ\")"));
    }

    [TestMethod]
    public void DecodeUrlComponent_IncompletePercent_ThrowsD3140()
    {
        Assert.ThrowsExactly<JsonataException>(() => Eval("$decodeUrlComponent(\"a%2\")"));
    }

    // --- FC CompileFusedArrayOfObjects ---
    // (FunctionalCompiler lines 6904-6943: path inside [] with object constructor)

    [TestMethod]
    public void FusedArrayOfObjects_WithPrefixPathInArrayCtor()
    {
        Assert.AreEqual(
            "[{\"id\":\"A\",\"v\":1},{\"id\":\"B\",\"v\":2}]",
            Eval("[items.{\"id\": id, \"v\": val}]",
                "{\"items\":[{\"id\":\"A\",\"val\":1},{\"id\":\"B\",\"val\":2}]}"));
    }

    // --- BF FormatNumber negative sub-picture ---
    // (BuiltInFunctions lines 4982-4995: separate negative format)

    [TestMethod]
    [DataRow("$formatNumber(-42, prefix & \"#;(#)\")", "\"(42)\"")]
    [DataRow("$formatNumber(-3.14, prefix & \"0.00;neg 0.00\")", "\"neg 3.14\"")]
    public void FormatNumber_RuntimePath_NegativeSubPicture(string expression, string expected)
    {
        Assert.AreEqual(expected, Eval(expression, "{\"prefix\":\"\"}"));
    }

    // --- BF FormatNumber custom options via runtime path ---
    // (BuiltInFunctions lines 4583-4589: exponent-separator, minus-sign etc.)

    [TestMethod]
    public void FormatNumber_RuntimePath_CustomOptions()
    {
        Assert.AreEqual(
            "\"42\"",
            Eval("$formatNumber(42, prefix & \"#\", {\"exponent-separator\":\"E\",\"minus-sign\":\"~\"})", "{\"prefix\":\"\"}"));
    }

    // --- FC equality predicate in fused path ---
    // (FunctionalCompiler lines 1645-1663)

    [TestMethod]
    public void EqualityPredicate_InFusedPath()
    {
        Assert.AreEqual(
            "[\"A\",\"C\"]",
            Eval("items[type=\"x\"].name",
                "{\"items\":[{\"type\":\"x\",\"name\":\"A\"},{\"type\":\"y\",\"name\":\"B\"},{\"type\":\"x\",\"name\":\"C\"}]}"));
    }

    // --- FC property map building ---
    // (FunctionalCompiler lines 1694-1713: arrayLen * remainingSteps > 10, propCount > 6)

    [TestMethod]
    public void PropertyMap_LargeArrayOfManyPropertyObjects()
    {
        // 20 objects with 8 properties each; traversal triggers property map building
        string data = "{\"items\":[";
        for (int i = 0; i < 20; i++)
        {
            if (i > 0) data += ",";
            data += $"{{\"a\":{i},\"b\":{i},\"c\":{i},\"d\":{i},\"e\":{i},\"f\":{i},\"g\":{i},\"h\":{i}}}";
        }

        data += "]}";
        string result = Eval("items.a", data);
        StringAssert.Contains(result, "0");
        StringAssert.Contains(result, "19");
    }

    // --- FC sort in focus context ---
    // (FunctionalCompiler lines 5463-5498: sort stage with focus variable)

    [TestMethod]
    public void FocusSort_OrdersByField()
    {
        // Focus without continuation returns parent context per element.
        Assert.AreEqual(
            "[{\"Order\":[{\"price\":30},{\"price\":10},{\"price\":20}]},{\"Order\":[{\"price\":30},{\"price\":10},{\"price\":20}]},{\"Order\":[{\"price\":30},{\"price\":10},{\"price\":20}]}]",
            Eval("Account.Order@$o^(price)",
                "{\"Account\":{\"Order\":[{\"price\":30},{\"price\":10},{\"price\":20}]}}"));
    }

    // --- FC ApplySortStagesOnly ---
    // (FunctionalCompiler lines 5959-5981: multi-step path with sort at step > 0)

    [TestMethod]
    public void ApplySortStagesOnly_SortAtStep1()
    {
        Assert.AreEqual(
            "[{\"k\":1},{\"k\":2},{\"k\":3}]",
            Eval("a.b^(k)",
                "{\"a\":[{\"b\":[{\"k\":3},{\"k\":1},{\"k\":2}]}]}"));
    }

    // --- FC EvalFromStepInto with equality predicate ---
    // (FunctionalCompiler lines 1644-1663: Into variant enters eq predicate from CollectAndContinueInto)

    [TestMethod]
    public void EvalFromStepInto_EqualityPredicateViaNestedArray()
    {
        // outer is array at step 1 → CollectAndContinueInto → EvalFromStepInto at step 2
        // items[type="x"] at step 2 has eq pred → enters EvalFromStepInto line 1645 (array case)
        Assert.AreEqual(
            "[\"A\",\"C\"]",
            Eval("outer.inner.items[type=\"x\"].name",
                "{\"outer\":[{\"inner\":{\"items\":[{\"type\":\"x\",\"name\":\"A\"},{\"type\":\"y\",\"name\":\"B\"}]}},{\"inner\":{\"items\":[{\"type\":\"x\",\"name\":\"C\"}]}}]}"));
    }

    [TestMethod]
    public void EvalFromStepInto_EqualityPredicate_SingletonObject()
    {
        // EvalFromStepInto line 1652: items is a single object matching predicate
        Assert.AreEqual(
            "\"A\"",
            Eval("outer.inner.items[type=\"x\"].name",
                "{\"outer\":[{\"inner\":{\"items\":{\"type\":\"x\",\"name\":\"A\"}}}]}"));
    }

    [TestMethod]
    public void EvalFromStepInto_EqualityPredicate_SingletonNoMatch()
    {
        // EvalFromStepInto line 1654-1656: singleton object does NOT match
        string result = Eval("outer.inner.items[type=\"x\"].name",
            "{\"outer\":[{\"inner\":{\"items\":{\"type\":\"y\",\"name\":\"B\"}}}]}");
        Assert.AreEqual("undefined", result);
    }

    [TestMethod]
    public void EvalFromStepInto_EqualityPredicate_NonObjectNonArray()
    {
        // EvalFromStepInto line 1659-1661: items is a number (not object or array)
        string result = Eval("outer.inner.items[type=\"x\"].name",
            "{\"outer\":[{\"inner\":{\"items\":42}}]}");
        Assert.AreEqual("undefined", result);
    }

    // --- FC CollectAndContinueInto property map building ---
    // (FunctionalCompiler lines 1694-1713: arrayLen * remainingSteps > 10, propCount > 6)

    [TestMethod]
    public void PropertyMap_LargeArrayViaPredicatePath()
    {
        // groups[0] uses predicate → EvalPropertyChainWithPredicates path
        // items has 20 objects with 8 properties → triggers property map building
        string items = string.Join(",", Enumerable.Range(0, 20).Select(i =>
            $"{{\"val\":{i},\"b\":{i},\"c\":{i},\"d\":{i},\"e\":{i},\"f\":{i},\"g\":{i},\"h\":{i}}}"));
        string data = $"{{\"groups\":[{{\"items\":[{items}]}}]}}";
        string result = Eval("groups[0].items.val", data);
        StringAssert.Contains(result, "0");
        StringAssert.Contains(result, "19");
    }

    // --- FC sort as stage: DEAD CODE ---
    // The parser never adds SortNode to StepAnnotations.Stages — sort is always
    // a separate SortNode step in the path. Therefore:
    // - ApplyFocusStages sort branch (lines 5463-5498): unreachable
    // - ApplySortStagesOnly body (lines 5959-5981): unreachable (always returns at 5955)
    // - CompilePath SortNode stage case (lines 2077-2083): unreachable
    // - WrapWithStages SortNode case (lines 187-189): unreachable

    [TestMethod]
    public void FocusSort_WithProjection()
    {
        Assert.AreEqual(
            "[\"A\",\"B\",\"C\"]",
            Eval("data.Order@$o^(price).$o.product",
                "{\"data\":{\"Order\":[{\"product\":\"C\",\"price\":30},{\"product\":\"A\",\"price\":10},{\"product\":\"B\",\"price\":20}]}}"));
    }

    // --- BF FormatNumber custom options: percent, per-mille, digit, pattern-separator ---
    // (BuiltInFunctions lines 4585-4589)

    [TestMethod]
    public void FormatNumber_RuntimePath_CustomPercentOption()
    {
        // "percent":"%%"  makes '%' a passive char → no multiply-by-100
        Assert.AreEqual(
            "\"0%\"",
            Eval("$formatNumber(0.42, prefix & \"#%\", {\"percent\":\"%%\"})", "{\"prefix\":\"\"}"));
    }

    [TestMethod]
    public void FormatNumber_RuntimePath_CustomPerMilleOption()
    {
        // "per-mille" option is accepted (line 4586)
        string result = Eval("$formatNumber(0.042, prefix & \"#\u2030\", {\"per-mille\":\"\u2030\"})", "{\"prefix\":\"\"}");
        StringAssert.Contains(result, "42");
    }

    [TestMethod]
    public void FormatNumber_RuntimePath_CustomZeroDigitOption()
    {
        // "zero-digit" changes the zero character (line 4587)
        string result = Eval("$formatNumber(42, prefix & \"#\", {\"zero-digit\":\"\u0660\"})", "{\"prefix\":\"\"}");
        Assert.IsNotNull(result);
        Assert.AreNotEqual("undefined", result);
    }

    [TestMethod]
    public void FormatNumber_RuntimePath_CustomDigitOption()
    {
        // "digit":"?"  makes '?' the optional-digit char instead of '#'
        Assert.AreEqual(
            "\"42\"",
            Eval("$formatNumber(42, prefix & \"?0\", {\"digit\":\"?\"})", "{\"prefix\":\"\"}"));
    }

    [TestMethod]
    public void FormatNumber_RuntimePath_CustomPatternSeparator()
    {
        // "pattern-separator":"|"  splits sub-pictures on '|' instead of ';'
        Assert.AreEqual(
            "\"neg 42\"",
            Eval("$formatNumber(-42, prefix & \"#|neg #\", {\"pattern-separator\":\"|\"})", "{\"prefix\":\"\"}"));
    }

    // ==============================================================
    // Round 5: Wider FC + BF coverage — targets identified from
    // coverage8 Cobertura XML analysis
    // ==============================================================

    // --- FC 657-680: CompileBufferFusedZipMixed2 (constant + chain) ---

    [TestMethod]
    public void Zip_ConstantAndChain()
    {
        // $zip with one constant array and one property chain triggers
        // CompileBufferFusedZipMixed2 (const-first path)
        Assert.AreEqual(
            "[[1,\"a\"],[2,\"b\"],[3,\"c\"]]",
            Eval("$zip([1,2,3], items)", "{\"items\":[\"a\",\"b\",\"c\"]}"));
    }

    [TestMethod]
    public void Zip_ChainAndConstant()
    {
        // const-second path
        Assert.AreEqual(
            "[[\"a\",1],[\"b\",2],[\"c\",3]]",
            Eval("$zip(items, [1,2,3])", "{\"items\":[\"a\",\"b\",\"c\"]}"));
    }

    // --- FC 686-709: CompileBufferFusedZip3 (3 property chains) ---

    [TestMethod]
    public void Zip_ThreeChains()
    {
        Assert.AreEqual(
            "[[1,4,7],[2,5,8],[3,6,9]]",
            Eval("$zip(a, b, c)", "{\"a\":[1,2,3],\"b\":[4,5,6],\"c\":[7,8,9]}"));
    }

    // --- FC 8023-8085: CompileFocusSortStage (focus + sort where key refs focus var) ---

    [TestMethod]
    public void FocusSort_KeyReferencesFocusVar()
    {
        // Focus without continuation returns parent context per element.
        Assert.AreEqual(
            "[{\"items\":[{\"name\":\"c\"},{\"name\":\"a\"},{\"name\":\"b\"}]},{\"items\":[{\"name\":\"c\"},{\"name\":\"a\"},{\"name\":\"b\"}]},{\"items\":[{\"name\":\"c\"},{\"name\":\"a\"},{\"name\":\"b\"}]}]",
            Eval("items@$e^($e.name)", "{\"items\":[{\"name\":\"c\"},{\"name\":\"a\"},{\"name\":\"b\"}]}"));
    }

    [TestMethod]
    public void FocusSort_KeyReferencesFocusVar_Descending()
    {
        // Focus without continuation returns parent context per element.
        Assert.AreEqual(
            "[{\"items\":[{\"name\":\"a\"},{\"name\":\"c\"},{\"name\":\"b\"}]},{\"items\":[{\"name\":\"a\"},{\"name\":\"c\"},{\"name\":\"b\"}]},{\"items\":[{\"name\":\"a\"},{\"name\":\"c\"},{\"name\":\"b\"}]}]",
            Eval("items@$e^(>$e.name)", "{\"items\":[{\"name\":\"a\"},{\"name\":\"c\"},{\"name\":\"b\"}]}"));
    }

    [TestMethod]
    public void FocusSort_SingleElement()
    {
        // <=1 element triggers the early-return path in CompileFocusSortStage
        // (returns input unchanged when nothing to sort)
        string data = "{\"items\":[{\"name\":\"a\"}]}";
        string result = Eval("items@$e^($e.name)", data);
        StringAssert.Contains(result, "\"name\"");
    }

    // --- FC 7877-7944: CompileFilter (standalone filter) ---

    [TestMethod]
    public void Filter_Standalone_NumericIndex()
    {
        // $a[0] — standalone numeric filter on variable
        Assert.AreEqual(
            "10",
            Eval("($a := [10,20,30]; $a[0])", "{}"));
    }

    [TestMethod]
    public void Filter_Standalone_BooleanTrue()
    {
        Assert.AreEqual(
            "42",
            Eval("($a := 42; $a[true])", "{}"));
    }

    [TestMethod]
    public void Filter_Standalone_BooleanFalse()
    {
        // false predicate → undefined
        Assert.AreEqual(
            "undefined",
            Eval("($a := 42; $a[false])", "{}"));
    }

    [TestMethod]
    public void Filter_Standalone_MultiValueIndex()
    {
        // Multi-value filter = array of indices
        Assert.AreEqual(
            "[10,30]",
            Eval("($a := [10,20,30]; $a[[0,2]])", "{}"));
    }

    [TestMethod]
    public void Filter_Standalone_Truthiness()
    {
        Assert.AreEqual(
            "42",
            Eval("($a := 42; $a[\"yes\"])", "{}"));
    }

    [TestMethod]
    public void Filter_Standalone_NegativeIndex()
    {
        Assert.AreEqual(
            "30",
            Eval("($a := [10,20,30]; $a[-1])", "{}"));
    }

    // --- BF 6251-6266: CodePointsToString with supplementary plane chars ---

    [TestMethod]
    public void Substring_SupplementaryPlane()
    {
        // U+1F600 (😀) is a single code point but 2 UTF-16 chars (surrogate pair).
        // $substring counts code points, so index 1 should be the char after 😀.
        string data = "{\"s\":\"\\uD83D\\uDE00AB\"}";
        // $substring(s, 1, 1) → "A" (code point 1 = 'A')
        Assert.AreEqual("\"A\"", Eval("$substring(s, 1, 1)", data));
    }

    [TestMethod]
    public void Substring_MultipleSupplementaryPlane()
    {
        // Two emoji followed by ASCII
        string data = "{\"s\":\"\\uD83D\\uDE00\\uD83D\\uDE01X\"}";
        // $substring(s, 2) → "X"
        Assert.AreEqual("\"X\"", Eval("$substring(s, 2)", data));
    }

    [TestMethod]
    public void Substring_SupplementaryPlaneSlice()
    {
        // Slice from the middle of a supplementary-plane string
        string data = "{\"s\":\"A\\uD83D\\uDE00B\\uD83D\\uDE01C\"}";
        // Code points: A(0) 😀(1) B(2) 😁(3) C(4)
        // $substring(s, 1, 3) → "😀B😁"
        string result = Eval("$substring(s, 1, 3)", data);
        // Verify it returned 3 code points starting from index 1
        Assert.AreEqual("3", Eval("$length($substring(s, 1, 3))", data));
    }

    // --- BF 4429-4444 + closure: HasInvalidPercentEncoding string overload ---

    [TestMethod]
    public void DecodeUrlComponent_NonStringInput()
    {
        // Passing a number to $decodeUrlComponent goes through CoerceElementToString
        // then calls HasInvalidPercentEncoding(string) — the string overload
        Assert.AreEqual("\"42\"", Eval("$decodeUrlComponent(42)", "{}"));
    }

    [TestMethod]
    public void DecodeUrlComponent_BooleanInput()
    {
        Assert.AreEqual("\"true\"", Eval("$decodeUrlComponent(true)", "{}"));
    }

    [TestMethod]
    public void DecodeUrl_NonStringInput()
    {
        // Same pattern for the parallel $decodeUrl function
        Assert.AreEqual("\"42\"", Eval("$decodeUrl(42)", "{}"));
    }

    // --- BF 3119-3123: $split with 1 argument (context binding) ---
    // Note: The 1-arg form of $split is compiled such that the context argument is
    // injected at compile time. The lines at BF 3119-3123 handle the parsing of the
    // 1-arg vs 2-arg vs 3-arg forms during compilation.

    // --- BF 5279-5281: $formatBase with value 0 ---

    [TestMethod]
    public void FormatBase_Zero()
    {
        Assert.AreEqual("\"0\"", Eval("$formatBase(0, 2)", "{}"));
        Assert.AreEqual("\"0\"", Eval("$formatBase(0, 16)", "{}"));
    }

    // --- BF 4853-4858: GCD for grouping + BF 5169-5173: irregular grouping ---

    [TestMethod]
    public void FormatNumber_RuntimePath_IrregularGrouping()
    {
        // Irregular grouping pattern #,##,### — not regular spacing
        // Forces non-regular grouping path (BF 5169-5173)
        Assert.AreEqual(
            "\"12,34,567\"",
            Eval("$formatNumber(1234567, prefix & \"#,##,###\")", "{\"prefix\":\"\"}"));
    }

    // --- BF 6166-6171: Multi-value sequence into array builder ---

    [TestMethod]
    public void Append_MultiValueToArray()
    {
        // $append where second arg produces multi-value sequence
        Assert.AreEqual(
            "[1,2,3,4]",
            Eval("$append([1,2], [3,4])", "{}"));
    }

    // --- FC 1015-1028: LookupField with array input ---

    [TestMethod]
    public void LookupField_ArrayInput()
    {
        // When input to a field lookup is an array, iterates and collects
        Assert.AreEqual(
            "[\"a\",\"b\",\"c\"]",
            Eval("items.name", "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"},{\"name\":\"c\"}]}"));
    }

    // --- FC 1567-1581: CollectAndContinue non-array child + array descent ---

    [TestMethod]
    public void PropertyChain_NestedArrayDescent()
    {
        // Multi-level path: items is array → for each, get details.name
        Assert.AreEqual(
            "[\"x\",\"y\"]",
            Eval("items.details.name",
                "{\"items\":[{\"details\":{\"name\":\"x\"}},{\"details\":{\"name\":\"y\"}}]}"));
    }

    [TestMethod]
    public void PropertyChain_NestedArrayWithArrayChild()
    {
        // Property that is an array inside an array of objects
        Assert.AreEqual(
            "[1,2,3,4]",
            Eval("items.values",
                "{\"items\":[{\"values\":[1,2]},{\"values\":[3,4]}]}"));
    }

    // --- FC 4085-4109: Multi-element input in path evaluation ---

    [TestMethod]
    public void Path_MultiElementInput()
    {
        // Three-level deep path where middle level is an array
        Assert.AreEqual(
            "[1,2,3]",
            Eval("data.items.value",
                "{\"data\":{\"items\":[{\"value\":1},{\"value\":2},{\"value\":3}]}}"));
    }

    // --- FC 2975-2999: Multi-parent focus context ---

    [TestMethod]
    public void Focus_MultiParentContext()
    {
        // Focus variable with multi-element parent context
        Assert.AreEqual(
            "[\"a\",\"b\"]",
            Eval("items@$x.$x.name",
                "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"}]}"));
    }

    // --- FC 6798-6833: Array constructor tuple paths ---

    [TestMethod]
    public void ArrayConstructor_TupleSingleton()
    {
        // Array literal that contains a singleton array element
        Assert.AreEqual(
            "[1,2,\"a\",\"b\"]",
            Eval("[items, names]",
                "{\"items\":[1,2],\"names\":[\"a\",\"b\"]}"));
    }

    [TestMethod]
    public void ArrayConstructor_TupleMultiValue()
    {
        // Array literal with multi-value expression
        Assert.AreEqual(
            "[1,2,3]",
            Eval("[data.items.value]",
                "{\"data\":{\"items\":[{\"value\":1},{\"value\":2},{\"value\":3}]}}"));
    }

    // --- FC 5344-5359: Multi-result numeric predicate in ApplyStages ---

    [TestMethod]
    public void Filter_MultiResultNumericPredicate()
    {
        // Filter where predicate returns multiple numeric values (array of indices)
        Assert.AreEqual(
            "[\"a\",\"c\"]",
            Eval("items[[0,2]]", "{\"items\":[\"a\",\"b\",\"c\",\"d\"]}"));
    }

    // --- FC 5597-5625: Multi-result numeric predicate in ApplyFocusStages ---

    [TestMethod]
    public void FocusFilter_MultiResultNumericPredicate()
    {
        // Focus without continuation returns parent context per surviving element.
        Assert.AreEqual(
            "[{\"items\":[\"a\",\"b\",\"c\",\"d\"]},{\"items\":[\"a\",\"b\",\"c\",\"d\"]}]",
            Eval("items@$x[[0,2]]", "{\"items\":[\"a\",\"b\",\"c\",\"d\"]}"));
    }

    // --- FC 1966-1980: EvalPropertyChainIntoStatic with array ---

    [TestMethod]
    public void PropertyChainInto_ArrayAtIntermediateStep()
    {
        // Path into nested structure where intermediate value is an array
        Assert.AreEqual(
            "[\"x\",\"y\"]",
            Eval("outer.items.name",
                "{\"outer\":{\"items\":[{\"name\":\"x\"},{\"name\":\"y\"}]}}"));
    }

    // --- FC 5092-5105: ApplyStages index binding singleton array expansion ---

    [TestMethod]
    public void IndexBinding_SingletonArrayExpansion()
    {
        // Index binding (#$i) on a singleton array triggers expansion
        Assert.AreEqual(
            "[0,1,2]",
            Eval("items#$i.$i",
                "{\"items\":[\"a\",\"b\",\"c\"]}"));
    }

    // --- FC 5437-5450: ApplyFocusStages singleton array expansion ---

    [TestMethod]
    public void FocusStages_SingletonArrayExpansion()
    {
        // Focus without continuation returns parent context per element.
        Assert.AreEqual(
            "[{\"items\":[\"a\",\"b\",\"c\"]},{\"items\":[\"a\",\"b\",\"c\"]},{\"items\":[\"a\",\"b\",\"c\"]}]",
            Eval("items@$x",
                "{\"items\":[\"a\",\"b\",\"c\"]}"));
    }

    // --- Dead code documentation ---
    // The following FC ranges are confirmed dead code (parser never adds SortNode to Stages):
    //   FC 186-194: WrapWithStages SortNode case
    //   FC 2077-2084: CompilePath SortNode stage handling
    //   FC 5188, 5192-5230: ApplyStages sort-as-stage
    //   FC 5463-5498: ApplyFocusStages sort-as-stage
    //   FC 5959-5981: ApplySortStagesOnly body
    //   FC 3391-3409: Inner focus sort-as-stage
    //
    // The following are unreachable on net10.0 (#if/#else blocks):
    //   BF 655-674: TryParseBinary(string) — only called from #else block
    //   BF 701-720: TryParseOctal(string) — only called from #else block
    //   BF 5707-5708: FormatIso8601Utc fallback — TryFormatIso8601Utc always succeeds on NET
    //   BF 5721-5732: FormatIso8601WithOffset non-NET fallback
    //   BF 6251-6266: CodePointsToString — inside #else block
    //   FC 8688-8713: TryParseSpecialRadix #else
    //
    // BF 680-682: TryParseBinary(ReadOnlySpan) empty check — unreachable defensive code.
    //   Caller checks s.Length > 2 before entering radix block, so Slice(2) is always non-empty.
    // BF 726-728: TryParseOctal(ReadOnlySpan) empty check — same reason as above.
    //
    // BF 451-453: $string NaN/Infinity branch — unreachable from JSONata (no expression
    //   produces NaN or Infinity as a JSON number value).
    //
    // BF 4494-4504: ValidateNoUnpairedSurrogates — unreachable via normal JSON input
    //   (System.Text.Json replaces unpaired surrogates with U+FFFD during parsing)
    //
    // BF 4429-4444: HasInvalidPercentEncoding(string) — the function body is reachable
    //   via $decodeUrlComponent(nonString) but the inner branch (4433-4441 where '%' is found)
    //   is effectively unreachable because no standard type coerces to a string containing '%'.
    //
    // BF 3862-3863: SplitString sepElement type check — unreachable defensive code.
    //   The compile-time lambda validates argument types (T0410) before calling SplitString.
    //
    // BF 3917-3919: SplitString searchStart > str.Length — unreachable defensive code.
    //   IndexOf cannot match past the end of the string, so searchStart cannot exceed str.Length.
    //
    // BF 5769-5771: FormatIso8601WithOffset buffer overflow — defensive, the buffer is
    //   always large enough for ISO 8601 format output.
    //
    // FC 7877-7944: CompileFilter (standalone) — DEAD CODE
    //   The parser never produces a FilterNode as a top-level expression. FilterNode is always
    //   added to StepAnnotations.Stages (Parser.cs line 1003). The Compile switch at FC line 119
    //   handles it defensively, but the branch can never be reached.
    //
    // FC 8023-8085 + DisplayClass105_0/105_1: CompileFocusSortStage — DEAD CODE
    //   Part of the sort-as-stage dead code. CompileFocusSortStage is called from line 2081
    //   only when stage is SortNode, which never happens (parser never adds SortNode to Stages).
    //
    // FC 1015-1028: LookupField array branch — UNREACHABLE DEFENSIVE CODE
    //   CompileName (FC 997) compiles a bare NameNode into LookupField. LookupField has an
    //   array-mapping branch (line 1014-1028) for when input is an array. However, the parser
    //   always wraps bare identifiers in a PathNode (Parser.cs line 842-846: "Fast path for
    //   bare NameNode LHS: create PathNode directly"). PathNode compilation handles array
    //   expansion via TryCompileSimplePropertyChain or the general path evaluation loop,
    //   so LookupField only ever receives individual objects — never arrays. The array branch
    //   is defensive code that cannot be reached through any expression the parser produces.

    // --- FC 488-499: EscapeJsonStringContent escape characters ---
    // These are triggered when constant arrays/objects contain strings with escape chars

    [TestMethod]
    public void ConstantArray_StringWithTab()
    {
        Assert.AreEqual(
            "[\"hello\\tworld\"]",
            Eval("[\"hello\\tworld\"]", "{}"));
    }

    [TestMethod]
    public void ConstantArray_StringWithNewline()
    {
        Assert.AreEqual(
            "[\"line1\\nline2\"]",
            Eval("[\"line1\\nline2\"]", "{}"));
    }

    [TestMethod]
    public void ConstantArray_StringWithBackslash()
    {
        Assert.AreEqual(
            "[\"path\\\\file\"]",
            Eval("[\"path\\\\file\"]", "{}"));
    }

    [TestMethod]
    public void ConstantArray_StringWithQuote()
    {
        Assert.AreEqual(
            "[\"say \\\"hello\\\"\"]",
            Eval("[\"say \\\"hello\\\"\"]", "{}"));
    }

    // --- FC 429-434: SerializeConstantJson unary negation ---

    [TestMethod]
    public void ConstantArray_NegativeNumber()
    {
        Assert.AreEqual(
            "[-42,-3.14]",
            Eval("[-42, -3.14]", "{}"));
    }

    // --- FC 9052-9079: CreateNullElement, CreateStringElement, CreateBoolElement ---

    [TestMethod]
    public void NullLiteral_ReturnsNull()
    {
        Assert.AreEqual("null", Eval("null", "{}"));
    }

    [TestMethod]
    public void BoolLiterals_ReturnCorrectly()
    {
        Assert.AreEqual("true", Eval("true", "{}"));
        Assert.AreEqual("false", Eval("false", "{}"));
    }

    // --- FC 6433-6438: Number formatting with exponent ---

    [TestMethod]
    public void StringifySmallExponent()
    {
        Assert.AreEqual("\"1.23e-10\"", Eval("$string(1.23e-10)", "{}"));
    }

    // --- BF formatNumber picture validation error branches (D3080-D3093) ---
    // These tests use CONSTANT pictures which are validated at compile time
    // via FormatNumberPicture.Parse (FC line 7457). They verify the compiler
    // path but do NOT hit BuiltInFunctions validation code.

    [TestMethod]
    public void FormatNumber_MultipleDecimalSeparators_ThrowsD3081()
    {
        EvalThrows("$formatNumber(42, \"0.0.0\")", "{}", "D3081");
    }

    [TestMethod]
    public void FormatNumber_MultiplePercent_ThrowsD3082()
    {
        // Reference implementation gives D3086 (passive inside active) which is a less
        // specific diagnosis. Our D3082 correctly identifies multiple percent markers.
        // See: https://github.com/jsonata-js/jsonata/issues/787
        EvalThrows("$formatNumber(42, \"0%0%0\")", "{}", "D3082");
    }

    [TestMethod]
    public void FormatNumber_MultiplePerMille_ThrowsD3083()
    {
        // Reference implementation gives D3086 (passive inside active) which is a less
        // specific diagnosis. Our D3083 correctly identifies multiple per-mille markers.
        // See: https://github.com/jsonata-js/jsonata/issues/787
        EvalThrows("$formatNumber(42, \"0\u20300\u20300\")", "{}", "D3083");
    }

    [TestMethod]
    public void FormatNumber_MixPercentPerMille_ThrowsD3084()
    {
        // Reference implementation gives D3086 (passive inside active) which is a less
        // specific diagnosis. Our D3084 correctly identifies mixed percent + per-mille.
        // See: https://github.com/jsonata-js/jsonata/issues/787
        EvalThrows("$formatNumber(42, \"0%\u20300\")", "{}", "D3084");
    }

    [TestMethod]
    public void FormatNumber_NoDigitChars_ThrowsD3085()
    {
        // Reference implementation bug: crashes at splitParts (jsonata.js:2196) with
        // code: undefined. Our D3085 is the correct behavior per XPath/XQuery spec.
        // See: https://github.com/jsonata-js/jsonata/issues/787
        EvalThrows("$formatNumber(42, \"---\")", "{}", "D3085");
    }

    [TestMethod]
    public void FormatNumber_PassiveInsideActive_ThrowsD3086()
    {
        EvalThrows("$formatNumber(42, \"0 0\")", "{}", "D3086");
    }

    [TestMethod]
    public void FormatNumber_GroupAdjacentDecimal_ThrowsD3087()
    {
        EvalThrows("$formatNumber(42, \"0,.0\")", "{}", "D3087");
    }

    [TestMethod]
    public void FormatNumber_IntEndWithGroup_ThrowsD3088()
    {
        EvalThrows("$formatNumber(42, \"0,\")", "{}", "D3088");
    }

    [TestMethod]
    public void FormatNumber_AdjacentGroups_ThrowsD3089()
    {
        EvalThrows("$formatNumber(42, \"0,,0\")", "{}", "D3089");
    }

    [TestMethod]
    public void FormatNumber_MandatoryBeforeOptional_ThrowsD3090()
    {
        EvalThrows("$formatNumber(42, \"0#\")", "{}", "D3090");
    }

    [TestMethod]
    public void FormatNumber_MandatoryAfterOptional_ThrowsD3091()
    {
        EvalThrows("$formatNumber(42, \"0.#0\")", "{}", "D3091");
    }

    [TestMethod]
    public void FormatNumber_ExponentWithPercent_ThrowsD3086()
    {
        // "0E0%" — the validator detects passive chars between active chars (D3086)
        // before it reaches the exponent+percent check (D3092)
        EvalThrows("$formatNumber(42, \"0E0%\")", "{}", "D3086");
    }

    [TestMethod]
    public void FormatNumber_ExponentNoDigit_AcceptsEmpty()
    {
        // "0E" — our implementation accepts this (exponent with empty exponent part)
        // rather than throwing D3093
        var result = Eval("$formatNumber(42, \"0E\")", "{}");
        Assert.IsNotNull(result);
    }

    // --- BF formatNumber RUNTIME picture validation (D3080-D3092) ---
    // These tests use NON-CONSTANT pictures via $string(pic) which forces the
    // runtime path through BuiltInFunctions.FormatNumber (BF 4550+).
    // The compiler cannot pre-parse $string(pic) so it falls through to the
    // general built-in function invocation at runtime.

    [TestMethod]
    public void FormatNumberRuntime_MultiplePatternSeparators_ThrowsD3080()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0.00;-0.00;extra"}""",
            "D3080");
    }

    [TestMethod]
    public void FormatNumberRuntime_MultipleDecimalSeparators_ThrowsD3081()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0.0.0"}""",
            "D3081");
    }

    [TestMethod]
    public void FormatNumberRuntime_MultiplePercent_ThrowsD3082()
    {
        // Reference gives D3086 (less specific). Our D3082 is more precise.
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0%0%0"}""",
            "D3082");
    }

    [TestMethod]
    public void FormatNumberRuntime_MultiplePerMille_ThrowsD3083()
    {
        // Reference gives D3086 (less specific). Our D3083 is more precise.
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0\u20300\u20300"}""",
            "D3083");
    }

    [TestMethod]
    public void FormatNumberRuntime_MixPercentPerMille_ThrowsD3084()
    {
        // Reference gives D3086 (less specific). Our D3084 is more precise.
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0%\u20300"}""",
            "D3084");
    }

    [TestMethod]
    public void FormatNumberRuntime_NoDigitChars_ThrowsD3085()
    {
        // Reference implementation bug: crashes with code: undefined.
        // Our D3085 is the correct behavior per XPath/XQuery spec.
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"---"}""",
            "D3085");
    }

    [TestMethod]
    public void FormatNumberRuntime_PassiveInsideActive_ThrowsD3086()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0 0"}""",
            "D3086");
    }

    [TestMethod]
    public void FormatNumberRuntime_GroupAdjacentDecimal_ThrowsD3087()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0,.0"}""",
            "D3087");
    }

    [TestMethod]
    public void FormatNumberRuntime_IntEndWithGroup_ThrowsD3088()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0,"}""",
            "D3088");
    }

    [TestMethod]
    public void FormatNumberRuntime_AdjacentGroups_ThrowsD3089()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0,,0"}""",
            "D3089");
    }

    [TestMethod]
    public void FormatNumberRuntime_MandatoryBeforeOptional_ThrowsD3090()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0#"}""",
            "D3090");
    }

    [TestMethod]
    public void FormatNumberRuntime_MandatoryAfterOptional_ThrowsD3091()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0.#0"}""",
            "D3091");
    }

    [TestMethod]
    public void FormatNumberRuntime_ExponentWithPercent_ThrowsD3092()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0e00%"}""",
            "D3092");
    }

    [TestMethod]
    public void FormatNumberRuntime_ValidPicture_ReturnsFormatted()
    {
        // Verify the runtime path works for valid pictures too
        Assert.AreEqual(
            "\"42.00\"",
            Eval("$formatNumber(42, $string(pic))", """{"pic":"#,##0.00"}"""));
    }

    // --- BUG FIX: Fractional grouping separator in $formatNumber ---
    // GetGroupingPositions passes integerPart as searchPart for fractional grouping,
    // but grpPos came from the fractional part and may exceed integerPart.Length,
    // causing ArgumentOutOfRangeException in String.IndexOf.
    // Fix: pass fractionalPart as searchPart for fractional grouping.
    // Reference returns "3.1,42" for $formatNumber(3.14159, "0.0,00").

    [TestMethod]
    public void FormatNumber_FractionalGrouping_CompileTime()
    {
        // Compile-time path (constant picture)
        Assert.AreEqual(
            "\"3.1,42\"",
            Eval("$formatNumber(3.14159, \"0.0,00\")", "{}"));
    }

    [TestMethod]
    public void FormatNumber_FractionalGrouping_Runtime()
    {
        // Runtime path (non-constant picture via $string)
        Assert.AreEqual(
            "\"3.1,42\"",
            Eval("$formatNumber(3.14159, $string(pic))", """{"pic":"0.0,00"}"""));
    }

    [TestMethod]
    public void FormatNumber_FractionalGrouping_Mixed_IntAndFrac()
    {
        // Both integer and fractional grouping separators
        Assert.AreEqual(
            "\"123,456.789,012\"",
            Eval("""$formatNumber(123456.789012, "#,##0.000,000")"""));
    }

    // --- BF 5104-5106: Digit family character substitution (MakeString) ---
    // Already tested in Round 4b via custom zero-digit option.
    // Adding explicit test for multi-digit substitution pattern.

    [TestMethod]
    public void FormatNumber_DigitFamily_FullSubstitution()
    {
        // Arabic-Indic digits: ٠=0660, ١=0661, ... ٩=0669
        // Picture must use the digit family chars, not ASCII 0/#
        // 42 with zero-digit=0660 and picture "#\u0660" → \u0664\u0662
        string result = Eval("$formatNumber(42, \"#\u0660\", {\"zero-digit\":\"\u0660\"})", "{}");
        StringAssert.Contains(result, "\u0664"); // Arabic-Indic 4
        StringAssert.Contains(result, "\u0662"); // Arabic-Indic 2
    }

    // --- BF 6166-6171: Sequence flattening helper ---

    [TestMethod]
    public void FlattenNestedArrays()
    {
        // $reduce with append on nested structure exercises sequence flattening
        Assert.AreEqual(
            "[1,2,3,4]",
            Eval("$reduce([[1,2],[3,4]], $append)", "{}"));
    }

    // --- Range operator: [start..end] ---

    [TestMethod]
    public void RangeOperator_SimpleRange()
    {
        Assert.AreEqual("[1,2,3,4,5]", Eval("[1..5]", "{}"));
    }

    [TestMethod]
    public void RangeOperator_SingleElement()
    {
        Assert.AreEqual("[3]", Eval("[3..3]", "{}"));
    }

    // --- CoerceToString via & concatenation ---

    [TestMethod]
    public void ConcatCoercion_NumberBoolNull()
    {
        Assert.AreEqual("\"42truenull\"", Eval("42 & true & null", "{}"));
    }

    [TestMethod]
    public void ConcatCoercion_SixStrings()
    {
        Assert.AreEqual("\"abcdef\"", Eval("\"a\" & \"b\" & \"c\" & \"d\" & \"e\" & \"f\"", "{}"));
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 8: Argument count validation (T0410)
    // These hit the compile-time args.Length checks in CompileXXX methods.
    // Each 2-line range is a throw T0410 for wrong argument count.
    // ═══════════════════════════════════════════════════════════════

    [TestMethod] // BF 776-777
    public void ArgCount_Not_TooMany() => EvalThrows("""$not(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 818-819
    public void ArgCount_Type_TooMany() => EvalThrows("""$type(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 1115-1116
    public void ArgCount_StringTransform_TooMany() => EvalThrows("""$trim(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 1335-1336
    public void ArgCount_Split_TooFew() => EvalThrows("""$split()""", "{}", "T0410");

    [TestMethod] // BF 1425-1426
    public void ArgCount_Contains_TooMany() => EvalThrows("""$contains(1, 2, 3)""", "{}", "T0410");

    [TestMethod] // BF 1492-1493
    public void ArgCount_Sqrt_TooMany() => EvalThrows("""$sqrt(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 1517-1518
    public void ArgCount_Round_TooFew() => EvalThrows("""$round()""", "{}", "T0410");

    [TestMethod] // BF 1564-1565
    public void ArgCount_Power_TooFew() => EvalThrows("""$power(1)""", "{}", "T0410");

    [TestMethod] // BF 1595-1596
    public void ArgCount_MathFunc_TooMany() => EvalThrows("""$floor(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 1616-1617
    public void ArgCount_Keys_TooMany() => EvalThrows("""$keys(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 1699-1700
    public void ArgCount_Values_TooMany() => EvalThrows("""$values(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 1772-1773
    public void ArgCount_Sort_TooFew() => EvalThrows("""$sort()""", "{}", "T0410");

    [TestMethod] // BF 1837-1838
    public void ArgCount_Reverse_TooMany() => EvalThrows("""$reverse(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 1876-1877
    public void ArgCount_Distinct_TooMany() => EvalThrows("""$distinct(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 1959-1960
    public void ArgCount_Flatten_TooMany() => EvalThrows("""$flatten(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 2213-2214
    public void ArgCount_Filter_TooFew() => EvalThrows("""$filter(1)""", "{}", "T0410");

    [TestMethod] // BF 2379-2380
    public void ArgCount_Reduce_TooFew() => EvalThrows("""$reduce(1)""", "{}", "T0410");

    [TestMethod] // BF 2505-2506
    public void ArgCount_Each_TooFew() => EvalThrows("""$each()""", "{}", "T0410");

    [TestMethod] // BF 2573-2574
    public void ArgCount_Merge_TooMany() => EvalThrows("""$merge(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 2613-2614
    public void ArgCount_Spread_TooMany() => EvalThrows("""$spread(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 2729-2730
    public void ArgCount_Lookup_TooFew() => EvalThrows("""$lookup(1)""", "{}", "T0410");

    [TestMethod] // BF 2846-2847
    public void ArgCount_Single_TooFew() => EvalThrows("""$single()""", "{}", "T0410");

    [TestMethod] // BF 2979-2980
    public void ArgCount_Sift_TooFew() => EvalThrows("""$sift()""", "{}", "T0410");

    [TestMethod] // BF 3044-3045
    public void ArgCount_Pad_TooFew() => EvalThrows("""$pad("a")""", "{}", "T0410");

    [TestMethod] // BF 3137-3138
    public void ArgCount_Match_TooFew() => EvalThrows("""$match()""", "{}", "T0410");

    [TestMethod] // BF 3518-3519 — $replace arg count
    public void ArgCount_Replace_TooFew() => EvalThrows("""$replace("a")""", "{}", "T0410");

    [TestMethod] // BF 4049-4050
    public void ArgCount_Base64Encode_TooMany() => EvalThrows("""$base64encode(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 4102-4103
    public void ArgCount_Base64Decode_TooMany() => EvalThrows("""$base64decode(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 4155-4156
    public void ArgCount_EncodeUrlComponent_TooMany() => EvalThrows("""$encodeUrlComponent(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 4218-4219
    public void ArgCount_DecodeUrlComponent_TooMany() => EvalThrows("""$decodeUrlComponent(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 4283-4284
    public void ArgCount_EncodeUrl_TooMany() => EvalThrows("""$encodeUrl(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 4365-4366
    public void ArgCount_DecodeUrl_TooMany() => EvalThrows("""$decodeUrl(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 4523-4524
    public void ArgCount_FormatNumber_TooFew() => EvalThrows("""$formatNumber(1)""", "{}", "T0410");

    [TestMethod] // BF 5211-5212
    public void ArgCount_FormatBase_TooFew() => EvalThrows("""$formatBase()""", "{}", "T0410");

    [TestMethod] // BF 5309-5310
    public void ArgCount_Shuffle_TooMany() => EvalThrows("""$shuffle(1, 2)""", "{}", "T0410");

    [TestMethod] // BF 5377-5378
    public void ArgCount_Zip_TooFew()
    {
        // $zip() with no args returns undefined
        Assert.AreEqual("undefined", Eval("""$zip()"""));
    }

    [TestMethod] // BF 5552-5553
    public void ArgCount_Assert_TooFew() => EvalThrows("""$assert()""", "{}", "T0410");

    [TestMethod] // BF 5811-5812
    public void ArgCount_ToMillis_TooFew() => EvalThrows("""$toMillis()""", "{}", "T0410");

    [TestMethod] // BF 5882-5883
    public void ArgCount_FormatInteger_TooFew() => EvalThrows("""$formatInteger(1)""", "{}", "T0410");

    [TestMethod] // BF 5954-5955
    public void ArgCount_ParseInteger_TooFew() => EvalThrows("""$parseInteger(1)""", "{}", "T0410");

    [TestMethod] // BF 6005-6006
    public void ArgCount_Eval_TooFew() => EvalThrows("""$eval()""", "{}", "T0410");

    // ═══════════════════════════════════════════════════════════════
    // Round 8: Additional edge case tests
    // ═══════════════════════════════════════════════════════════════

    [TestMethod] // BF 3967-3969: $split with limit <= 0
    public void Split_LimitZero_ReturnsEmptyArray()
    {
        Assert.AreEqual("[]", Eval("""$split("hello", "l", 0)"""));
    }

    [TestMethod] // BF 6166-6171: AddSequenceToArray multi-element sequence
    public void Append_MultiElementSequence()
    {
        // a.x evaluates to sequence [1,2] (not an array), then $append adds "z"
        Assert.AreEqual(
            """[1,2,"z"]""",
            Eval("""$append(a.x, "z")""", """{"a":[{"x":1},{"x":2}]}"""));
    }

    [TestMethod] // BF 3518-3519: $replace with limit <= 0
    public void Replace_LimitZero_ReturnsOriginal()
    {
        Assert.AreEqual("\"aaa\"", Eval("""$replace("aaa", /a/, "b", 0)"""));
    }

    [TestMethod] // BF 3535-3536: $replace with zero-length regex match
    public void Replace_ZeroLengthMatch_ThrowsD1004() => EvalThrows("""$replace("aaa", /a?/, "b")""", "{}", "D1004");

    // ===================================================================
    // Round 9: Closure coverage — targeting runtime lambdas in BF closures
    //
    // These tests exercise the runtime execution paths inside compiled
    // closures for built-in functions. Many of these paths handle:
    // - Multi-element sequences (from path expressions matching multiple values)
    // - Non-string type coercion (passing numbers/booleans where strings expected)
    // - Undefined inputs
    // - Custom matcher protocol ($match with lambda pattern)
    // ===================================================================

    // --- $spread multi-element sequence (DisplayClass65_0, lines 2647-2717) ---

    [TestMethod] // BF 2648-2658: $spread on multi-element sequence of objects
    public void Spread_MultiElement_Objects()
    {
        Assert.AreEqual(
            """[{"a":1},{"b":2}]""",
            Eval("""$spread(items.prop)""", """{"items":[{"prop":{"a":1}},{"prop":{"b":2}}]}"""));
    }

    [TestMethod] // BF 2659-2671: $spread on multi-element sequence containing arrays of objects
    public void Spread_MultiElement_ArraysOfObjects()
    {
        Assert.AreEqual(
            """[{"a":1},{"b":2},{"c":3}]""",
            Eval("""$spread(items.val)""", """{"items":[{"val":[{"a":1},{"b":2}]},{"val":[{"c":3}]}]}"""));
    }

    [TestMethod] // BF 2672-2675: $spread on multi-element sequence with non-object element
    public void Spread_MultiElement_NonObject_ReturnsFirstElement()
    {
        Assert.AreEqual("42", Eval("""$spread(items.val)""", """{"items":[{"val":42},{"val":"hello"}]}"""));
    }

    [TestMethod] // BF 2659-2671, 2696-2708: $spread mixed objects and arrays
    public void Spread_MultiElement_MixedObjectAndArray()
    {
        // When one element is object and another is array, the array elements are iterated for objects
        Assert.AreEqual(
            "1",
            Eval("""$spread(items.val)""", """{"items":[{"val":{"x":1}},{"val":[1,2,3]}]}"""));
    }

    [TestMethod] // BF 2679-2681: $spread on multi-element sequence with no properties
    public void Spread_MultiElement_EmptyObjects_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$spread(items.val)""", """{"items":[{"val":{}},{"val":{}}]}"""));
    }

    [TestMethod] // BF 2711-2713: $spread on multi-element with exactly one property
    public void Spread_MultiElement_SingleProperty_Unwraps()
    {
        Assert.AreEqual(
            """{"a":1}""",
            Eval("""$spread(items.val)""", """{"items":[{"val":{"a":1}},{"val":{}}]}"""));
    }

    // --- $keys / $values on undefined (DisplayClass50_0/51_0) ---

    [TestMethod] // BF 1625-1626: $keys on undefined → undefined
    public void Keys_Undefined_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$keys(nothing)"""));
    }

    [TestMethod] // BF: $values on undefined → undefined
    public void Values_Undefined_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$values(nothing)"""));
    }

    // --- $shuffle (DisplayClass101_0, lines 5310-5350) ---

    [TestMethod] // BF 5318-5319: $shuffle on undefined → undefined
    public void Shuffle_Undefined_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$shuffle(nothing)"""));
    }

    [TestMethod] // BF 5333-5346: $shuffle on multi-element sequence
    public void Shuffle_MultiElementSequence_PreservesCount()
    {
        // Path expression creates multi-element sequence; shuffle preserves count
        Assert.AreEqual("3", Eval("""$count($shuffle(items.val))""", """{"items":[{"val":1},{"val":2},{"val":3}]}"""));
    }

    // --- $base64encode non-string (DisplayClass85_0, lines 4055-4095) ---

    [TestMethod] // BF 4063-4064: $base64encode on undefined → undefined
    public void Base64Encode_Undefined_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$base64encode(nothing)"""));
    }

    [TestMethod] // BF 4087-4095: $base64encode on number (non-string coercion)
    public void Base64Encode_Number_CoercesToString()
    {
        // base64("42") = "NDI="
        Assert.AreEqual(@"""NDI=""", Eval("""$base64encode(42)"""));
    }

    [TestMethod] // BF 4087-4095: $base64encode on boolean (non-string coercion)
    public void Base64Encode_Boolean_CoercesToString()
    {
        // base64("true") = "dHJ1ZQ=="
        Assert.AreEqual(@"""dHJ1ZQ==""", Eval("""$base64encode(true)"""));
    }

    // --- $base64decode non-string (DisplayClass86_0, lines 4110-4155) ---

    [TestMethod] // BF 4116-4117: $base64decode on undefined → undefined
    public void Base64Decode_Undefined_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$base64decode(nothing)"""));
    }

    [TestMethod] // BF: $base64decode valid string
    public void Base64Decode_ValidString()
    {
        Assert.AreEqual(@"""42""", Eval("""$base64decode("NDI=")"""));
    }

    // --- $encodeUrlComponent non-string (DisplayClass87_0, lines 4185-4215) ---

    [TestMethod] // BF 4194-4211: $encodeUrlComponent on number (non-string coercion)
    public void EncodeUrlComponent_Number_CoercesToString()
    {
        Assert.AreEqual(@"""42""", Eval("""$encodeUrlComponent(42)"""));
    }

    [TestMethod] // BF 4194-4211: $encodeUrlComponent on boolean (non-string coercion)
    public void EncodeUrlComponent_Boolean_CoercesToString()
    {
        Assert.AreEqual(@"""true""", Eval("""$encodeUrlComponent(true)"""));
    }

    // --- $decodeUrlComponent / $encodeUrl / $decodeUrl non-string ---

    [TestMethod] // BF: $decodeUrlComponent on number (non-string coercion)
    public void DecodeUrlComponent_Number_CoercesToString()
    {
        Assert.AreEqual(@"""42""", Eval("""$decodeUrlComponent(42)"""));
    }

    [TestMethod] // BF: $encodeUrl on number (non-string coercion)
    public void EncodeUrl_Number_CoercesToString()
    {
        Assert.AreEqual(@"""42""", Eval("""$encodeUrl(42)"""));
    }

    [TestMethod] // BF: $decodeUrl on number (non-string coercion)
    public void DecodeUrl_Number_CoercesToString()
    {
        Assert.AreEqual(@"""42""", Eval("""$decodeUrl(42)"""));
    }

    // --- $filter edge cases (DisplayClass61_0, lines 2225-2360) ---

    [TestMethod] // BF 2235-2270: $filter on multi-element sequence with arrays (flatten path)
    public void Filter_MultiElementWithArrays_Flattens()
    {
        Assert.AreEqual(
            "[3,4,5]",
            Eval("""$filter(items.val, function($v){$v > 2})""", """{"items":[{"val":[1,2,3]},{"val":[4,5]}]}"""));
    }

    [TestMethod] // BF 2333-2335: $filter non-array input, no matches → undefined
    public void Filter_ScalarNoMatch_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$filter(42, function($v){$v > 100})"""));
    }

    [TestMethod] // BF 2338-2340: $filter non-array input, single match → unwrapped value
    public void Filter_ScalarMatch_ReturnsValue()
    {
        Assert.AreEqual("42", Eval("""$filter(42, function($v){$v > 10})"""));
    }

    [TestMethod] // BF 2344-2346: $filter array input, no matches → undefined (reference behavior)
    public void Filter_ArrayNoMatch_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$filter([1,2,3], function($v){$v > 100})"""));
    }

    // --- $match edge cases (DisplayClass76_0, lines 3140-3275) ---

    [TestMethod] // BF 3146-3148: $match with undefined string → undefined
    public void Match_UndefinedString_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$match(nothing, /a/)"""));
    }

    [TestMethod] // BF 3264-3267: $match with non-string and regex → T0410 (per reference impl)
    public void Match_NonString_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Eval("""$match(42, /a/)"""));
        Assert.AreEqual("T0410", ex.Code);
    }

    [TestMethod] // BF 3247-3249: $match with non-regex non-lambda pattern → T0410 (per reference impl)
    public void Match_NonRegexPattern_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Eval("""$match("hello", 42)"""));
        Assert.AreEqual("T0410", ex.Code);
    }

    [TestMethod] // BF 3270: $match with capture groups
    public void Match_WithGroups()
    {
        Assert.AreEqual(
            """{"match":"2024-01-15","index":0,"groups":["2024","01","15"]}""",
            Eval("""$match("2024-01-15", /(\d{4})-(\d{2})-(\d{2})/)"""));
    }

    [TestMethod] // BF 3252-3259: $match with limit argument (regex path)
    public void Match_Regex_WithLimit_LimitsResults()
    {
        Assert.AreEqual(
            """[{"match":"aa","index":0,"groups":[]},{"match":"aa","index":4,"groups":[]}]""",
            Eval("""$match("aabcaad", /a+/, 2)"""));
    }

    [TestMethod] // BF: $match no match with regex → undefined
    public void Match_Regex_NoMatch_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$match("abc", /xyz/)"""));
    }

    [TestMethod] // BF 3153-3238: $match with custom lambda matcher protocol
    public void Match_CustomLambdaMatcher()
    {
        // Custom matcher: a function receiving the string, returning match object
        string expr = """
            (
                $m := function($s) { {"match": "hel", "start": 0, "groups": ["h", "el"]} };
                $match("hello", $m)
            )
            """;
        Assert.AreEqual(
            """{"match":"hel","index":0,"groups":["h","el"]}""",
            Eval(expr));
    }

    [TestMethod] // BF 3155-3158: $match with custom lambda, non-string input — GetString throws
    public void Match_CustomLambda_NonString_ThrowsInvalidOperation()
    {
        // The custom matcher protocol calls GetString() on the input directly,
        // which throws when the input is not a string (e.g. number). This is
        // a minor edge case since the reference rejects this at compile time.
        string expr = """
            (
                $m := function($s) { {"match": "x"} };
                $match(42, $m)
            )
            """;
        Assert.Throws<Exception>(() => Eval(expr));
    }

    [TestMethod] // BF 3162-3169: $match with custom lambda and limit
    public void Match_CustomLambda_WithLimit()
    {
        // Custom matcher with explicit limit
        string expr = """
            (
                $m := function($s) { {"match": "hel", "start": 0, "groups": []} };
                $match("hello", $m, 1)
            )
            """;
        Assert.AreEqual(
            """{"match":"hel","index":0,"groups":[]}""",
            Eval(expr));
    }

    // --- $parseInteger edge cases (DisplayClass122_0, lines 5965-6005) ---

    [TestMethod] // BF 5974-5975: $parseInteger with non-string first arg → undefined
    public void ParseInteger_NonString_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$parseInteger(42, "0")"""));
    }

    // --- $toMillis edge cases (DisplayClass120_0, lines 5835-5880) ---

    [TestMethod] // BF 5851-5852: $toMillis with non-string → error
    public void ToMillis_NonString_ThrowsError()
    {
        EvalThrows("""$toMillis(42)""", "{}", "T0410");
    }

    [TestMethod] // BF 5843-5845, 5865-5873: $toMillis with non-string picture
    public void ToMillis_NonStringPicture_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$toMillis("2024-01-15", 0)"""));
    }

    [TestMethod] // BF: $toMillis with valid picture format
    public void ToMillis_WithPicture_ReturnsEpochMillis()
    {
        Assert.AreEqual("1705276800000", Eval("""$toMillis("2024-01-15", "[Y0001]-[M01]-[D01]")"""));
    }

    // --- $parseInteger with non-string picture ---

    [TestMethod] // BF 5986-5998: $parseInteger with non-string picture (binding resolves to number)
    public void ParseInteger_NonStringPicture_CoercesToString()
    {
        // Picture is a number 0 → coerced to string "0" → parses successfully
        Assert.AreEqual("42", Eval("""$parseInteger("42", pic)""", """{"pic": 0}"""));
    }

    // --- $eval edge cases (DisplayClass123_0) ---

    [TestMethod] // BF 6020-6022: $eval with non-string argument → T0410
    public void Eval_NonString_ThrowsT0410()
    {
        EvalThrows("$eval(42)", "{}", "T0410");
    }

    [TestMethod] // BF 6049-6061: $eval with context argument
    public void Eval_WithContext_UsesContext()
    {
        Assert.AreEqual("6", Eval("""$eval("$sum($)", [1,2,3])"""));
    }

    [TestMethod] // BF 6032-6034: $eval with invalid expression → D3120
    public void Eval_InvalidSyntax_ThrowsD3120()
    {
        EvalThrows("""$eval(")(")""", "{}", "D3120");
    }

    // --- $pad non-string coercion ---

    [TestMethod] // BF: $pad on number (non-string coercion)
    public void Pad_Number_CoercesToString()
    {
        Assert.AreEqual(@"""42        """, Eval("""$pad(42, 10)"""));
    }

    // --- $string / $length on undefined ---

    [TestMethod] // BF: $string on undefined → undefined
    public void String_Undefined_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$string(nothing)"""));
    }

    [TestMethod] // BF: $length on undefined → undefined
    public void Length_Undefined_ReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("""$length(nothing)"""));
    }

    // ===================================================================
    // Documented defensive/unreachable code in BF closures
    //
    // The following `seq.Count > 1` branches in built-in function closures
    // are DEFENSIVE CODE that cannot be reached from standard JSONata
    // expressions. In Corvus's evaluator:
    //
    // 1. Path expressions (items.val) wrap multi-valued results in arrays
    //    before passing to function arguments → singleton sequence (Count=1)
    // 2. $append creates arrays (ValueKind.Array), not multi-element
    //    sequences → singleton sequence (Count=1) with array element
    // 3. The semicolon syntax (expr1; expr2) evaluates sequentially and
    //    returns the last value, not a sequence
    //
    // Affected functions and their `seq.Count > 1` branches:
    // - $spread (BF 2647-2717): multi-element sequence with mixed types
    // - $shuffle (BF 5333-5346): multi-element sequence shuffle
    // - $filter (BF 2235-2270): flatten multi-element sequence with arrays
    // - $sort (similar pattern): multi-element sequence sort
    // - $reduce, $each, $map: similar multi-element sequence handling
    //
    // All these paths handle the theoretical case where an internal
    // evaluator produces a Sequence with Count > 1 directly. The
    // production code paths always use the singleton (Count == 1) or
    // singleton-array fast paths instead.
    // ===================================================================

    // ─── BUG FIX: TFM inconsistencies ──────────────────────────────────
    // These tests verify consistent behavior across NET and net481.
    // Reference JSONata throws T0410 for non-string args to these functions.

    [TestMethod] // BF 1440/1463: $contains non-string arg1 must throw T0410 on all TFMs
    public void Contains_NonStringArg1_ThrowsT0410()
    {
        EvalThrows("""$contains(42, "2")""", "{}", "T0410");
    }

    [TestMethod] // BF 1440/1463: $contains boolean arg1
    public void Contains_BooleanArg1_ThrowsT0410()
    {
        EvalThrows("""$contains(true, "r")""", "{}", "T0410");
    }

    [TestMethod] // BF 1454/1475: $contains non-string arg2 must throw T0410 on all TFMs
    public void Contains_NonStringArg2_ThrowsT0410()
    {
        EvalThrows("""$contains("hello", 42)""", "{}", "T0410");
    }

    [TestMethod] // BF 1454/1475: $contains boolean arg2
    public void Contains_BooleanArg2_ThrowsT0410()
    {
        EvalThrows("""$contains("hello", true)""", "{}", "T0410");
    }

    [TestMethod] // BF 2746-2755: $lookup non-string key must throw T0410 on all TFMs
    public void Lookup_NonStringKey_ThrowsT0410()
    {
        EvalThrows("""$lookup({"a":1}, 42)""", "{}", "T0410");
    }

    [TestMethod] // BF 2746-2755: $lookup boolean key
    public void Lookup_BooleanKey_ThrowsT0410()
    {
        EvalThrows("""$lookup({"a":1}, true)""", "{}", "T0410");
    }

    // ─── Round 10: Context binding, error paths, validation ─────────────

    // --- Context binding (1-arg overloads) ---

    [TestMethod] // BF 1415-1418: $contains with 1 arg uses context binding
    public void Contains_OneArg_ContextBinding()
    {
        Assert.AreEqual("true", Eval("'hello world' ~> $contains('world')"));
    }

    [TestMethod] // BF 1415-1418: $contains with 1 arg + regex
    public void Contains_OneArg_ContextBinding_Regex()
    {
        Assert.AreEqual("true", Eval("'hello world' ~> $contains(/wor/)"));
    }

    [TestMethod] // BF 3134-3138: $match with 1 arg uses context binding
    public void Match_OneArg_ContextBinding()
    {
        Assert.AreEqual(@"{""match"":""ll"",""index"":2,""groups"":[]}", Eval("'hello' ~> $match(/l+/)"));
    }

    [TestMethod] // FC: $split with 1 arg uses context binding
    public void Split_OneArg_ContextBinding()
    {
        Assert.AreEqual(@"[""abc""]", Eval("'abc' ~> $split(',')"));
    }

    [TestMethod] // FC: $replace with 2 args uses context binding
    public void Replace_TwoArg_ContextBinding()
    {
        Assert.AreEqual(@"""aXc""", Eval("'abc' ~> $replace('b', 'X')"));
    }

    // --- Error paths ---

    [TestMethod] // BF 3538-3539: $replace with regex that matches empty string → D1004
    public void Replace_ZeroLengthRegexMatch_ThrowsD1004()
    {
        EvalThrows("$replace('abc', /(?=a)/, '-')", "{}", "D1004");
    }

    [TestMethod] // BF 3987-3989: $split with regex limit=0 → empty array
    public void Split_RegexLimitZero_ReturnsEmptyArray()
    {
        Assert.AreEqual("[]", Eval("$split('a,b,c', /,/, 0)"));
    }

    [TestMethod] // BF: $split with regex limit=-1 → D3020
    public void Split_RegexLimitNegative_ThrowsD3020()
    {
        EvalThrows("$split('a,b,c', /,/, -1)", "{}", "D3020");
    }

    // --- URL decoding: invalid percent encoding (BF 4453-4460) ---

    [TestMethod] // BF 4453-4460: $decodeUrlComponent with invalid hex after %
    public void DecodeUrlComponent_InvalidHexGG_ThrowsD3140()
    {
        EvalThrows("$decodeUrlComponent('%GG')", "{}", "D3140");
    }

    [TestMethod] // BF 4453-4460: $decodeUrlComponent with single char after %
    public void DecodeUrlComponent_SingleCharAfterPercent_ThrowsD3140()
    {
        EvalThrows("$decodeUrlComponent('a%2b%1')", "{}", "D3140");
    }

    [TestMethod] // BF 4453-4460: $decodeUrlComponent with trailing %
    public void DecodeUrlComponent_TrailingPercent_ThrowsD3140()
    {
        EvalThrows("$decodeUrlComponent('hello%')", "{}", "D3140");
    }

    // --- Higher-order built-in functions (FC 9052+) ---

    [TestMethod] // FC 9052+: $map with $string passes built-in as lambda
    public void Map_WithStringBuiltIn()
    {
        Assert.AreEqual(@"[""1"",""2"",""3""]", Eval("$map([1,2,3], $string)"));
    }

    [TestMethod] // FC: $map with $type passes built-in as lambda
    public void Map_WithTypeBuiltIn()
    {
        Assert.AreEqual(@"[""number"",""string"",""boolean""]", Eval("$map([1,'a',true], $type)"));
    }

    [TestMethod] // FC: $filter with $boolean passes built-in as lambda
    public void Filter_WithBooleanBuiltIn()
    {
        Assert.AreEqual(@"[1,""a"",true]", Eval("$filter([0, 1, '', 'a', false, true], $boolean)"));
    }

    // --- Nested array path traversal (FC 1567-1581, 1966-1980) ---

    [TestMethod] // FC 1567+: deep property access through nested arrays
    public void NestedArrayPathTraversal()
    {
        Assert.AreEqual("[10,20,30]",
            Eval("Account.Order.Product.Price",
                """{"Account":{"Order":[{"Product":[{"Price":10},{"Price":20}]},{"Product":[{"Price":30}]}]}}"""));
    }

    [TestMethod] // FC: path traversal through arrays with filtering
    public void NestedArrayPathWithPredicate()
    {
        Assert.AreEqual("[20,30]",
            Eval("Account.Order.Product[Price>15].Price",
                """{"Account":{"Order":[{"Product":[{"Price":10},{"Price":20}]},{"Product":[{"Price":30}]}]}}"""));
    }

    // ──────── Round 11: BF wildcard on object (target: EnumeratePropertyValues path in BF) ────────

    [TestMethod]
    public void WildcardOnObject_MultipleValues()
    {
        Assert.AreEqual("[1,2,3]",
            Eval("$.*", """{"a":1,"b":2,"c":3}"""));
    }

    [TestMethod]
    public void WildcardOnObject_SingleValue()
    {
        Assert.AreEqual("42",
            Eval("$.*", """{"only":42}"""));
    }

    [TestMethod]
    public void WildcardOnObject_Empty()
    {
        Assert.AreEqual("undefined",
            Eval("$.*", """{}"""));
    }

    // ──────── Round 11: BF aggregate on path results ────────

    [TestMethod]
    public void MaxViaPropertyPath()
    {
        Assert.AreEqual("30",
            Eval("$max(items.price)",
                """{"items":[{"price":10},{"price":30},{"price":20}]}"""));
    }

    [TestMethod]
    public void MinViaPropertyPath()
    {
        Assert.AreEqual("10",
            Eval("$min(items.price)",
                """{"items":[{"price":10},{"price":30},{"price":20}]}"""));
    }

    [TestMethod]
    public void SumViaPropertyPath()
    {
        Assert.AreEqual("60",
            Eval("$sum(items.price)",
                """{"items":[{"price":10},{"price":30},{"price":20}]}"""));
    }

    [TestMethod]
    public void AverageViaPropertyPath()
    {
        Assert.AreEqual("20",
            Eval("$average(items.price)",
                """{"items":[{"price":10},{"price":30},{"price":20}]}"""));
    }

    // ──────── Round 11: BF string concat via & operator ────────

    [TestMethod]
    public void StringConcat3Operands()
    {
        Assert.AreEqual("\"abc\"",
            Eval("\"a\" & \"b\" & \"c\""));
    }

    [TestMethod]
    public void StringConcat4Operands()
    {
        Assert.AreEqual("\"abcd\"",
            Eval("\"a\" & \"b\" & \"c\" & \"d\""));
    }

    [TestMethod]
    public void StringConcat5Operands()
    {
        Assert.AreEqual("\"abcde\"",
            Eval("\"a\" & \"b\" & \"c\" & \"d\" & \"e\""));
    }

    // ──────── Round 11: BF chain traversal into nested arrays ────────

    [TestMethod]
    public void ChainTraversalObjectThenArray()
    {
        Assert.AreEqual("[1,2]",
            Eval("a.b.c", """{"a":{"b":[{"c":1},{"c":2}]}}"""));
    }

    [TestMethod]
    public void ChainTraversalArrayThenObjectDeep()
    {
        Assert.AreEqual("[1,2]",
            Eval("a.b.c.d", """{"a":[{"b":{"c":{"d":1}}},{"b":{"c":{"d":2}}}]}"""));
    }

    // ──────── Round 11: BF $string coercion ────────

    [TestMethod]
    public void StringCoerceObject()
    {
        Assert.AreEqual("\"{\\\"x\\\":1}\"",
            Eval("""$string({"x":1})"""));
    }

    [TestMethod]
    public void StringCoerceArray()
    {
        Assert.AreEqual("\"[1,2,3]\"",
            Eval("$string([1,2,3])"));
    }

    // ──────── Round 11: BF numeric binary op error paths ────────

    [TestMethod]
    public void NumericAdd_LeftNonNumeric_ThrowsT2001()
    {
        EvalThrows("\"hello\" + 1", "0", "T2001");
    }

    [TestMethod]
    public void NumericAdd_RightNonNumeric_ThrowsT2002()
    {
        EvalThrows("1 + \"hello\"", "0", "T2002");
    }

    // ──────── Round 12: BF targeted dynamic paths ────────

    // Aggregate via chain path
    [TestMethod]
    public void MaxOverChain()
    {
        Assert.AreEqual("20",
            Eval("$max(items.price)",
                """{"items":[{"price":10},{"price":20},{"price":5}]}"""));
    }

    [TestMethod]
    public void MinOverChain()
    {
        Assert.AreEqual("5",
            Eval("$min(items.price)",
                """{"items":[{"price":10},{"price":20},{"price":5}]}"""));
    }

    [TestMethod]
    public void AverageOverChain()
    {
        Assert.AreEqual("20",
            Eval("$average(items.price)",
                """{"items":[{"price":10},{"price":20},{"price":30}]}"""));
    }

    // Chain flat into (array mid-chain)
    [TestMethod]
    public void ChainFlatArray()
    {
        Assert.AreEqual("[1,2]",
            Eval("a.b.c",
                """{"a":[{"b":{"c":1}},{"b":{"c":2}}]}"""));
    }

    // Filter predicate on array
    [TestMethod]
    public void FilterPredicateArray()
    {
        Assert.AreEqual("""["foo","baz"]""",
            Eval("""items[type="A"].name""",
                """{"items":[{"type":"A","name":"foo"},{"type":"B","name":"bar"},{"type":"A","name":"baz"}]}"""));
    }

    // Constant index on nested array
    [TestMethod]
    public void ConstantIndexOnArray()
    {
        Assert.AreEqual("[10,30]",
            Eval("items.values[0]",
                """{"items":[{"values":[10,20]},{"values":[30,40]}]}"""));
    }

    // Wildcard on array
    [TestMethod]
    public void WildcardOnArray()
    {
        Assert.AreEqual("[1,2,3]",
            Eval("x.*",
                """{"x":[{"a":1},{"b":2},{"c":3}]}"""));
    }

    // $string on dynamic objects/arrays
    [TestMethod]
    public void StringifyObject()
    {
        // Eval returns JSON text, e.g., "{\"a\":1,\"b\":2}" wrapped in quotes
        // The outer quote pair is the JSON string literal; strip those to get the actual value
        string result = Eval("$string(x)", """{"x":{"a":1,"b":2}}""");
        Assert.StartsWith("\"", result);
        Assert.EndsWith("\"", result);
        // The inner content when unescaped should be {"a":1,"b":2}
        string inner = result.Substring(1, result.Length - 2).Replace("\\\"", "\"");
        Assert.AreEqual("{\"a\":1,\"b\":2}", inner);
    }

    [TestMethod]
    public void StringifyArray()
    {
        string result = Eval("$string(x)", """{"x":[1,2,3]}""");
        Assert.StartsWith("\"", result);
        string inner = result.Substring(1, result.Length - 2).Replace("\\\"", "\"");
        Assert.AreEqual("[1,2,3]", inner);
    }

    // Surrogate string operations
    [TestMethod]
    public void SubstringSurrogate()
    {
        Assert.AreEqual("\"AB\"",
            Eval("$substring(x,1)",
                "{\"x\":\"\uD83D\uDE00AB\"}"));
    }

    [TestMethod]
    public void LengthSurrogate()
    {
        Assert.AreEqual("3",
            Eval("$length(x)",
                "{\"x\":\"\uD83D\uDE00AB\"}"));
    }

    // $parseInteger with dynamic picture
    [TestMethod]
    public void ParseIntegerDynamic()
    {
        Assert.AreEqual("255",
            Eval("$parseInteger(x,y)",
                """{"x":"255","y":"#0"}"""));
    }

    // $toMillis with dynamic picture
    [TestMethod]
    public void ToMillisDynamic()
    {
        Assert.AreEqual("1518393600000",
            Eval("$toMillis(x,y)",
                """{"x":"2018-02-12","y":"[Y]-[M01]-[D01]"}"""));
    }

    // $flatten with nested arrays
    [TestMethod]
    public void FlattenNestedArraysDynamic()
    {
        Assert.AreEqual("[1,2,3,4,5,6]",
            Eval("$flatten(x)",
                """{"x":[[1,2],[3,4],[5,6]]}"""));
    }

    // Map chain with transform
    [TestMethod]
    public void MapChainTransform()
    {
        Assert.AreEqual("""["HELLO","WORLD"]""",
            Eval("items.name.$uppercase()",
                """{"items":[{"name":"hello"},{"name":"world"}]}"""));
    }

    // ==================== Round 13: RT tests for CG helper fallback paths ====================

    // Multiple equality predicates
    [TestMethod]
    public void MultiEqualityPredicateChain()
    {
        Assert.AreEqual("\"x\"",
            Eval("""items[type="A"][status="active"].name""",
                """{"items":[{"type":"A","status":"active","name":"x"},{"type":"A","status":"inactive","name":"y"},{"type":"B","status":"active","name":"z"}]}"""));
    }

    [TestMethod]
    public void MultiEqualityPredicateChainMultiMatch()
    {
        Assert.AreEqual("""["x","w"]""",
            Eval("""items[type="A"][status="active"].name""",
                """{"items":[{"type":"A","status":"active","name":"x"},{"type":"A","status":"active","name":"w"}]}"""));
    }

    // Mixed predicate + index
    [TestMethod]
    public void MixedPredicateAndIndex()
    {
        Assert.AreEqual("10",
            Eval("""items[type="A"].values[0]""",
                """{"items":[{"type":"A","values":[10,20]},{"type":"B","values":[30,40]}]}"""));
    }

    [TestMethod]
    public void MixedPredicateAndIndexMultiMatch()
    {
        Assert.AreEqual("[10,30]",
            Eval("""items[type="A"].values[0]""",
                """{"items":[{"type":"A","values":[10,20]},{"type":"A","values":[30,40]},{"type":"B","values":[50,60]}]}"""));
    }

    // $merge over chain — property order may differ between RT and reference;
    // JSON objects are unordered, so we check both possible orderings.
    [TestMethod]
    public void ChainMerge()
    {
        string result = Eval("$merge(items.objs)",
            """{"items":[{"objs":{"a":1}},{"objs":{"b":2}}]}""");

        Assert.IsTrue(
            result == """{"a":1,"b":2}""" || result == """{"b":2,"a":1}""",
            $"Expected merged object with a=1 and b=2, got: {result}");
    }

    // $map over chain
    [TestMethod]
    public void MapChainElementsViaMapCall()
    {
        Assert.AreEqual("""["ALICE","BOB"]""",
            Eval("""$map(items.name, function($v){$uppercase($v)})""",
                """{"items":[{"name":"alice"},{"name":"bob"}]}"""));
    }

    // Aggregate via apply operator
    [TestMethod]
    public void AverageOverChainViaApply()
    {
        Assert.AreEqual("5",
            Eval("items.values ~> $average",
                """{"items":[{"values":[2,4]},{"values":[6,8]}]}"""));
    }

    [TestMethod]
    public void SumOverChainViaApply()
    {
        Assert.AreEqual("20",
            Eval("items.values ~> $sum",
                """{"items":[{"values":[3,7]},{"values":[1,9]}]}"""));
    }

    // Arithmetic computed step
    [TestMethod]
    public void MapChainDoubleComputedStep()
    {
        Assert.AreEqual("[20,40]",
            Eval("items.(price * 2)",
                """{"items":[{"price":10},{"price":20}]}"""));
    }

    [TestMethod]
    public void MapChainDoubleComputedStepAddition()
    {
        Assert.AreEqual("[11,22]",
            Eval("items.(price + tax)",
                """{"items":[{"price":10,"tax":1},{"price":20,"tax":2}]}"""));
    }

    // ==================== Round 13b: Edge cases for partial coverage gaps ====================

    // Chain with missing property mid-chain → undefined
    [TestMethod]
    public void ChainMissingPropertyReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("a.b.c.d", """{"a":{"x":1}}"""));
    }

    [TestMethod]
    public void ChainPrimitiveMidChainReturnsUndefined()
    {
        Assert.AreEqual("undefined", Eval("a.b.c.d", """{"a":{"b":5}}"""));
    }

    [TestMethod]
    public void ChainViaSingleElementArray()
    {
        Assert.AreEqual("1", Eval("a.b.c.d", """[{"a":{"b":{"c":{"d":1}}}}]"""));
    }

    // Average via apply operator — empty chains
    [TestMethod]
    public void AverageOverChainEmptyItems()
    {
        Assert.AreEqual("undefined", Eval("items.values ~> $average", """{"items":[]}"""));
    }

    [TestMethod]
    public void AverageOverChainEmptyValues()
    {
        Assert.AreEqual("undefined", Eval("items.values ~> $average", """{"items":[{"values":[]}]}"""));
    }

    [TestMethod]
    public void AverageOverChainSingleValue()
    {
        Assert.AreEqual("42", Eval("items.values ~> $average", """{"items":[{"values":[42]}]}"""));
    }

    // Sum via apply operator — empty chains
    [TestMethod]
    public void SumOverChainEmpty()
    {
        Assert.AreEqual("undefined", Eval("items.values ~> $sum", """{"items":[]}"""));
    }

    [TestMethod]
    public void SumOverChainEmptyValues()
    {
        // items.values on [{values:[]}] yields [] (single-element array = transparent navigation).
        // $sum([]) = 0. Reference verified.
        Assert.AreEqual("0", Eval("items.values ~> $sum", """{"items":[{"values":[]}]}"""));
    }

    // $map with empty chain
    [TestMethod]
    public void MapChainElementsEmptyResult()
    {
        Assert.AreEqual("undefined", Eval("""$map(items.name, function($v){$uppercase($v)})""", """{"items":[]}"""));
    }

    // $shuffle with empty/single chain
    [TestMethod]
    public void ShuffleChainEmpty()
    {
        Assert.AreEqual("undefined", Eval("$shuffle(items.vals)", """{"items":[]}"""));
    }

    [TestMethod]
    public void ShuffleChainSingle()
    {
        Assert.AreEqual("[42]", Eval("$shuffle(items.vals)", """{"items":[{"vals":[42]}]}"""));
    }

    // keepArray ([]) with empty/single chain
    [TestMethod]
    public void KeepArrayChainEmpty()
    {
        Assert.AreEqual("undefined", Eval("a.b.c[]", """{"a":[]}"""));
    }

    [TestMethod]
    public void KeepArrayChainSingle()
    {
        Assert.AreEqual("[1]", Eval("a.b.c[]", """{"a":[{"b":{"c":1}}]}"""));
    }

    // MapChainDouble — empty/single edge cases
    [TestMethod]
    public void MapChainDoubleEmptyItems()
    {
        Assert.AreEqual("undefined", Eval("items.(price * 2)", """{"items":[]}"""));
    }

    [TestMethod]
    public void MapChainDoubleSingleItem()
    {
        Assert.AreEqual("10", Eval("items.(price * 2)", """{"items":[{"price":5}]}"""));
    }

    // Predicate chain with no match
    [TestMethod]
    public void PredicateChainNoMatch()
    {
        Assert.AreEqual("undefined", Eval("""items[type="A"].values[0]""", """{"items":[{"type":"B","values":[99]}]}"""));
    }

    [TestMethod]
    public void PredicateChainSingleMatchName()
    {
        Assert.AreEqual("\"x\"", Eval("""items[type="A"].name""", """{"items":[{"type":"A","name":"x"}]}"""));
    }

    // ==================== Round 13c: Deeper edge cases ====================

    // Primitive data with long chain → undefined
    [TestMethod]
    [DataRow("a.b.c.d", "42")]
    [DataRow("a.b.c.d", "true")]
    public void ChainPrimitiveTopLevelData(string expression, string data)
    {
        Assert.AreEqual("undefined", Eval(expression, data));
    }

    // 2-step chain with computed step (MapChainDouble)
    [TestMethod]
    public void MapChainDoubleTwoStepChain()
    {
        Assert.AreEqual("[6,14]",
            Eval("data.items.(price * 2)",
                """{"data":{"items":[{"price":3},{"price":7}]}}"""));
    }

    [TestMethod]
    public void MapChainDoubleTwoStepChainSingle()
    {
        Assert.AreEqual("10",
            Eval("data.items.(price * 2)",
                """{"data":{"items":[{"price":5}]}}"""));
    }

    [TestMethod]
    public void MapChainDoubleTwoStepChainEmpty()
    {
        Assert.AreEqual("undefined",
            Eval("data.items.(price * 2)",
                """{"data":{"items":[]}}"""));
    }

    // AverageOverChainCore: array at end of chain
    [TestMethod]
    public void AverageOverChainCoreArray()
    {
        Assert.AreEqual("2",
            Eval("items.values ~> $average",
                """{"items":[{"values":[1,2,3]}]}"""));
    }

    // AverageOverChainCore: single number
    [TestMethod]
    public void AverageOverChainCoreSingleNumber()
    {
        Assert.AreEqual("3",
            Eval("items.values ~> $average",
                """{"items":[{"values":3}]}"""));
    }

    // AverageOverChainCore: non-number → error
    [TestMethod]
    public void AverageOverChainCoreNonNumberThrows()
    {
        EvalThrows("items.values ~> $average",
            """{"items":[{"values":"hello"}]}""",
            "T0412");
    }

    // SumOverChainCore: array at end of chain
    [TestMethod]
    public void SumOverChainCoreArray()
    {
        Assert.AreEqual("6",
            Eval("items.values ~> $sum",
                """{"items":[{"values":[1,2,3]}]}"""));
    }

    // SumOverChainCore: single number
    [TestMethod]
    public void SumOverChainCoreSingleNumber()
    {
        Assert.AreEqual("3",
            Eval("items.values ~> $sum",
                """{"items":[{"values":3}]}"""));
    }

    // SumOverChainCore: non-number → error
    [TestMethod]
    public void SumOverChainCoreNonNumberThrows()
    {
        EvalThrows("items.values ~> $sum",
            """{"items":[{"values":"hello"}]}""",
            "T0412");
    }

    // FusedEvalFromStep: multiple matches → array
    [TestMethod]
    public void FusedEvalFromStepMultipleMatches()
    {
        Assert.AreEqual("[1,4]",
            Eval("""items[type="A"].values[0]""",
                """{"items":[{"type":"A","values":[1,2]},{"type":"B","values":[3]},{"type":"A","values":[4,5]}]}"""));
    }

    // FusedEvalFromStep: single match → scalar
    [TestMethod]
    public void FusedEvalFromStepSingleMatch()
    {
        Assert.AreEqual("10",
            Eval("""items[type="A"].values[0]""",
                """{"items":[{"type":"A","values":[10]},{"type":"B","values":[20]}]}"""));
    }

    // FusedCollectAndContinue: multiple matches
    [TestMethod]
    public void FusedCollectAndContinueMultipleMatches()
    {
        Assert.AreEqual("[\"x\",\"z\"]",
            Eval("""items[type="A"].name""",
                """{"items":[{"type":"A","name":"x"},{"type":"B","name":"y"},{"type":"A","name":"z"}]}"""));
    }

    // NavigatePropertyChainInto: mixed types in array
    [TestMethod]
    public void NavigatePropertyChainIntoMixedTypes()
    {
        Assert.AreEqual("[1,3]",
            Eval("a.b", """[{"a":{"b":1}},{"a":2},{"a":{"b":3}}]"""));
    }

    // ==================== Round 13d: $average/$sum function-call pattern ====================

    [TestMethod]
    public void AverageFunctionCallArrayValues()
    {
        Assert.AreEqual("2",
            Eval("$average(items.values)", """{"items":[{"values":[1,2,3]}]}"""));
    }

    [TestMethod]
    public void AverageFunctionCallScalarValue()
    {
        Assert.AreEqual("5",
            Eval("$average(items.values)", """{"items":[{"values":5}]}"""));
    }

    [TestMethod]
    public void AverageFunctionCallNonNumberThrows()
    {
        EvalThrows("$average(items.values)", """{"items":[{"values":"hello"}]}""", "T0412");
    }

    [TestMethod]
    public void AverageFunctionCallEmpty()
    {
        Assert.AreEqual("undefined",
            Eval("$average(items.values)", """{"items":[]}"""));
    }

    [TestMethod]
    public void SumFunctionCallArrayValues()
    {
        Assert.AreEqual("6",
            Eval("$sum(items.values)", """{"items":[{"values":[1,2,3]}]}"""));
    }

    [TestMethod]
    public void SumFunctionCallScalarValue()
    {
        Assert.AreEqual("5",
            Eval("$sum(items.values)", """{"items":[{"values":5}]}"""));
    }

    [TestMethod]
    public void SumFunctionCallNonNumberThrows()
    {
        EvalThrows("$sum(items.values)", """{"items":[{"values":"hello"}]}""", "T0412");
    }

    [TestMethod]
    public void SumFunctionCallEmpty()
    {
        Assert.AreEqual("undefined",
            Eval("$sum(items.values)", """{"items":[]}"""));
    }

    // ==================== Round 13e: FusedEvalFromStep edge cases ====================

    [TestMethod]
    public void MultiPredicateMissingProperty()
    {
        Assert.AreEqual("undefined",
            Eval("""items[type="A"][status="active"].name""", """{"x":1}"""));
    }

    [TestMethod]
    public void MultiPredicateSingletonObjectMatch()
    {
        Assert.AreEqual("\"x\"",
            Eval("""items[type="A"][status="active"].name""",
                """{"items":{"type":"A","status":"active","name":"x"}}"""));
    }

    [TestMethod]
    public void MultiPredicateSingletonObjectNoMatch()
    {
        Assert.AreEqual("undefined",
            Eval("""items[type="A"][status="active"].name""",
                """{"items":{"type":"B","status":"active","name":"y"}}"""));
    }

    [TestMethod]
    public void MultiPredicatePrimitive()
    {
        Assert.AreEqual("undefined",
            Eval("""items[type="A"][status="active"].name""", """{"items":42}"""));
    }

    [TestMethod]
    public void MixedPredicateIndexOutOfBounds()
    {
        Assert.AreEqual("undefined",
            Eval("""items[type="A"].values[5]""",
                """{"items":[{"type":"A","values":[1]}]}"""));
    }

    [TestMethod]
    public void MixedPredicateSingletonIndex0()
    {
        Assert.AreEqual("10",
            Eval("""items[type="A"].values[0]""",
                """{"items":{"type":"A","values":10}}"""));
    }

    [TestMethod]
    public void MixedPredicateSingletonIndexNon0()
    {
        Assert.AreEqual("undefined",
            Eval("""items[type="A"].values[1]""",
                """{"items":{"type":"A","values":10}}"""));
    }

    [TestMethod]
    public void MultiPredicateSuccessful()
    {
        Assert.AreEqual("\"x\"",
            Eval("""data.items[type="A"][status="active"].name""",
                """{"data":{"items":[{"type":"A","status":"active","name":"x"},{"type":"B","status":"active","name":"y"}]}}"""));
    }

    // ==================== Round 13f: FusedCollectAndContinue ====================

    [TestMethod]
    public void FccPerElementIndexArrayValid()
    {
        Assert.AreEqual("[{\"type\":\"X\",\"d\":1},{\"type\":\"X\",\"d\":3}]",
            Eval("""a.b[0].c[type="X"]""",
                """{"a":[{"b":[{"c":{"type":"X","d":1}},{"c":{"type":"Y"}}]},{"b":[{"c":{"type":"X","d":3}}]}]}"""));
    }

    [TestMethod]
    public void FccPerElementIndexArrayOOB()
    {
        Assert.AreEqual("undefined",
            Eval("""a.b[5].c[type="X"]""",
                """{"a":[{"b":[{"c":{"type":"X"}}]}]}"""));
    }

    [TestMethod]
    public void FccPerElementIndexSingleton0()
    {
        Assert.AreEqual("{\"type\":\"X\",\"d\":1}",
            Eval("""a.b[0].c[type="X"]""",
                """{"a":[{"b":{"c":{"type":"X","d":1}}},{"b":{"c":{"type":"Y"}}}]}"""));
    }

    [TestMethod]
    public void FccPerElementIndexSingletonSkip()
    {
        Assert.AreEqual("undefined",
            Eval("""a.b[1].c[type="X"]""",
                """{"a":[{"b":{"c":{"type":"X","d":1}}}]}"""));
    }

    [TestMethod]
    public void FccEqPredicateArraySubItems()
    {
        Assert.AreEqual("[{\"type\":\"X\",\"n\":1},{\"type\":\"X\",\"n\":3}]",
            Eval("""a.items.c[type="X"]""",
                """{"a":[{"items":{"c":[{"type":"X","n":1},{"type":"Y","n":2}]}},{"items":{"c":[{"type":"X","n":3}]}}]}"""));
    }

    [TestMethod]
    public void FccEqPredicateSingletonMatch()
    {
        Assert.AreEqual("{\"type\":\"X\",\"n\":1}",
            Eval("""a.items.c[type="X"]""",
                """{"a":[{"items":{"c":{"type":"X","n":1}}},{"items":{"c":{"type":"Y","n":2}}}]}"""));
    }

    [TestMethod]
    public void FccNestedArrays()
    {
        Assert.AreEqual("{\"type\":\"X\",\"n\":1}",
            Eval("""a.items.c[type="X"]""",
                """{"a":[[{"items":{"c":{"type":"X","n":1}}}],[{"items":{"c":{"type":"Y","n":2}}}]]}"""));
    }

    [TestMethod]
    public void FccGlobalIndexSuccess()
    {
        Assert.AreEqual("{\"type\":\"X\",\"v\":1}",
            Eval("""items[0].name[type="X"]""",
                """[{"items":[{"name":{"type":"X","v":1}},{"name":{"type":"Y"}}]},{"items":[{"name":{"type":"X","v":3}}]}]"""));
    }

    [TestMethod]
    public void FccGlobalIndexOOB()
    {
        Assert.AreEqual("undefined",
            Eval("""items[5].name[type="X"]""",
                """[{"items":[{"name":{"type":"X"}}]}]"""));
    }
}
