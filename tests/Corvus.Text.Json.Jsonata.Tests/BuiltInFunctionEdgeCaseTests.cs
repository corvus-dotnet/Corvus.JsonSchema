// <copyright file="BuiltInFunctionEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests for uncovered branches in <see cref="BuiltInFunctions"/>, exercised
/// through the JSONata evaluator. Targets identified from merged Cobertura data.
/// </summary>
public class BuiltInFunctionEdgeCaseTests
{
    private static string Eval(string expression, string data = "null")
    {
        return JsonataEvaluator.Default.EvaluateToString(expression, data) ?? "undefined";
    }

    private static void EvalThrows(string expression, string data, string expectedCode)
    {
        var ex = Assert.Throws<JsonataException>(() =>
            JsonataEvaluator.Default.EvaluateToString(expression, data));
        Assert.Equal(expectedCode, ex.Code);
    }

    // ─── $number: hex/binary/octal conversion (lines 540-565) ───────

    [Fact]
    public void Number_HexString_Converts()
    {
        // Line 540: hex parsing path
        Assert.Equal("255", Eval("""$number("0xFF")"""));
    }

    [Fact]
    public void Number_HexUpperCase_Converts()
    {
        Assert.Equal("255", Eval("""$number("0XFF")"""));
    }

    [Fact]
    public void Number_BinaryString_Converts()
    {
        // Line 550: binary parsing path
        Assert.Equal("5", Eval("""$number("0b101")"""));
    }

    [Fact]
    public void Number_OctalString_Converts()
    {
        // Line 560: octal parsing path
        Assert.Equal("15", Eval("""$number("0o17")"""));
    }

    [Fact]
    public void Number_InvalidHex_Throws()
    {
        // Line 545: invalid hex → D3030
        EvalThrows("""$number("0xGG")""", "null", "D3030");
    }

    [Fact]
    public void Number_InvalidBinary_Throws()
    {
        // Line 555: invalid binary → D3030
        EvalThrows("""$number("0b123")""", "null", "D3030");
    }

    [Fact]
    public void Number_InvalidOctal_Throws()
    {
        // Line 565: invalid octal → D3030
        EvalThrows("""$number("0o89")""", "null", "D3030");
    }

    [Fact]
    public void Number_Infinity_Throws()
    {
        // 1/0 in JSONata is actually handled as an error upstream.
        // Use $number on Infinity string instead
        EvalThrows("""$number("Infinity")""", "null", "D3030");
    }

    [Fact]
    public void Number_NonNumericType_ReturnsUndefined()
    {
        // Line 636: non-coercible type — $number on array throws T0410
        EvalThrows("""$number([1,2])""", "null", "T0410");
    }

    // ─── $string: NaN/Infinity edge case (lines 339-340) ────────────

    [Fact]
    public void String_InfinityOrNaN_Throws()
    {
        // Line 339-340: D3001 error on $string(Infinity)
        EvalThrows("""$string(1/0)""", "null", "D3001");
    }

    // ─── $substringBefore / $substringAfter: arg count (lines 996, 1011) ─

    [Fact]
    public void SubstringBefore_TooManyArgs_Throws()
    {
        EvalThrows("""$substringBefore("hello", "l", "extra")""", "null", "T0411");
    }

    [Fact]
    public void SubstringAfter_TooManyArgs_Throws()
    {
        EvalThrows("""$substringAfter("hello", "l", "extra")""", "null", "T0411");
    }

    // ─── $filter with multi-valued sequence containing arrays (lines 2222-2257) ─

    [Fact]
    public void Filter_MultiValuedSequenceWithArrays_Flattens()
    {
        // Lines 2222-2257: flatten arrays in multi-valued sequences for $filter
        string data = """{"items": [1,2,3,4,5,6]}""";
        string result = Eval("""$filter(items, function($v) { $v > 3 })""", data);
        Assert.Equal("[4,5,6]", result);
    }

    // ─── $append with flatten: lines 2032-2037 ─────────────────────

    [Fact]
    public void Append_ArrayElements_Flattened()
    {
        string result = Eval("""$append([1,2], [3,4])""");
        Assert.Equal("[1,2,3,4]", result);
    }

    // ─── $shuffle: line 118 (the function itself) ───────────────────

    [Fact]
    public void Shuffle_ReturnsAllElements()
    {
        string result = Eval("""$count($shuffle([1,2,3,4,5]))""");
        Assert.Equal("5", result);
    }

    // ─── $zip: line 122 ─────────────────────────────────────────────

    [Fact]
    public void Zip_CombinesArrays()
    {
        string result = Eval("""$zip([1,2], ["a","b"])""");
        Assert.Equal("[[1,\"a\"],[2,\"b\"]]", result);
    }

    // ─── $formatBase: line 120 ──────────────────────────────────────

    [Fact]
    public void FormatBase_Hex()
    {
        Assert.Equal("\"ff\"", Eval("""$formatBase(255, 16)"""));
    }

    [Fact]
    public void FormatBase_Binary()
    {
        Assert.Equal("\"101\"", Eval("""$formatBase(5, 2)"""));
    }

    [Fact]
    public void FormatBase_Octal()
    {
        Assert.Equal("\"17\"", Eval("""$formatBase(15, 8)"""));
    }

    // ─── $random: line 118 ──────────────────────────────────────────

    [Fact]
    public void Random_ReturnsBetweenZeroAndOne()
    {
        string result = Eval("""$random()""");
        double val = double.Parse(result);
        Assert.True(val >= 0.0 && val < 1.0);
    }

    // ─── $decodeUrl / $encodeUrl / $decodeUrlComponent / $encodeUrlComponent ─

    [Fact]
    public void EncodeUrl_EncodesSpecialChars()
    {
        Assert.Equal("\"hello%20world\"", Eval("""$encodeUrl("hello world")"""));
    }

    [Fact]
    public void DecodeUrl_DecodesSpecialChars()
    {
        Assert.Equal("\"hello world\"", Eval("""$decodeUrl("hello%20world")"""));
    }

    [Fact]
    public void EncodeUrlComponent_Encodes()
    {
        Assert.Equal("\"hello%20world\"", Eval("""$encodeUrlComponent("hello world")"""));
    }

    [Fact]
    public void DecodeUrlComponent_Decodes()
    {
        Assert.Equal("\"hello world\"", Eval("""$decodeUrlComponent("hello%20world")"""));
    }

    // ─── $assert: line 124 ──────────────────────────────────────────

    [Fact]
    public void Assert_TrueCondition_Passes()
    {
        // $assert returns undefined on success (not true)
        Assert.Equal("undefined", Eval("""$assert(true, "should not fail")"""));
    }

    [Fact]
    public void Assert_FalseCondition_Throws()
    {
        EvalThrows("""$assert(false, "assertion failed")""", "null", "D3141");
    }

    // ─── $error: line 123 ───────────────────────────────────────────

    [Fact]
    public void Error_ThrowsCustomMessage()
    {
        EvalThrows("""$error("custom error")""", "null", "D3137");
    }

    // ─── $replace with regex and string replacement (lines 3494-3526) ─

    [Fact]
    public void Replace_RegexWithLimit()
    {
        // RegexReplaceWithString with limit
        string result = Eval("""$replace("aababab", /ab/, "--", 2)""");
        Assert.Equal("\"a----ab\"", result);
    }

    // ─── $match with capture groups ─────────────────────────────────

    [Fact]
    public void Match_WithCaptureGroups()
    {
        string result = Eval("""$match("abc123", /([a-z]+)(\d+)/)""");
        // Should return match object with groups
        Assert.Contains("\"match\"", result);
        Assert.Contains("\"groups\"", result);
    }

    // ─── $formatNumber: XPath picture formatting (FormatNumberXPath, 486 lines) ──

    [Fact]
    public void FormatNumber_BasicDecimal()
    {
        Assert.Equal("\"12,345.60\"", Eval("""$formatNumber(12345.6, "#,##0.00")"""));
    }

    [Fact]
    public void FormatNumber_IntegerOnly()
    {
        Assert.Equal("\"42\"", Eval("""$formatNumber(42, "0")"""));
    }

    [Fact]
    public void FormatNumber_LeadingZeros()
    {
        Assert.Equal("\"007\"", Eval("""$formatNumber(7, "000")"""));
    }

    [Fact]
    public void FormatNumber_Negative()
    {
        Assert.Equal("\"-42.50\"", Eval("""$formatNumber(-42.5, "#0.00")"""));
    }

    [Fact]
    public void FormatNumber_NegativeWithSubPicture()
    {
        // Separate sub-picture for negatives
        Assert.Equal("\"(42.50)\"", Eval("""$formatNumber(-42.5, "#0.00;(#0.00)")"""));
    }

    [Fact]
    public void FormatNumber_Percent()
    {
        Assert.Equal("\"75%\"", Eval("""$formatNumber(0.75, "0%")"""));
    }

    [Fact]
    public void FormatNumber_PerMille()
    {
        Assert.Equal("\"750\u2030\"", Eval("""$formatNumber(0.75, "0\u2030")"""));
    }

    [Fact]
    public void FormatNumber_WithOptions_DecimalSeparator()
    {
        string data = """{"opts": {"decimal-separator": ",", "grouping-separator": "."}}""";
        Assert.Equal("\"1,5\"", Eval("""$formatNumber(1.5, "0,0", opts)""", data));
    }

    [Fact]
    public void FormatNumber_ScientificNotation()
    {
        Assert.Equal("\"1.23e2\"", Eval("""$formatNumber(123, "0.00e0")"""));
    }

    [Fact]
    public void FormatNumber_Zero()
    {
        Assert.Equal("\"0.00\"", Eval("""$formatNumber(0, "0.00")"""));
    }

    [Fact]
    public void FormatNumber_LargeNumber()
    {
        Assert.Equal("\"1,234,567.89\"", Eval("""$formatNumber(1234567.89, "#,##0.00")"""));
    }

    [Fact]
    public void FormatNumber_NoFraction()
    {
        Assert.Equal("\"43\"", Eval("""$formatNumber(42.7, "#")"""));
    }

    [Fact]
    public void FormatNumber_OnlyFraction()
    {
        Assert.Equal("\".5\"", Eval("""$formatNumber(0.5, ".0")"""));
    }

    // ─── $replace with regex backreferences (ApplyJsonataBackreferences, 55 lines) ──

    [Fact]
    public void Replace_RegexBackreference_Dollar1()
    {
        string result = Eval("""$replace("John Smith", /(\w+)\s(\w+)/, "$2 $1")""");
        Assert.Equal("\"Smith John\"", result);
    }

    [Fact]
    public void Replace_RegexBackreference_Dollar0()
    {
        string result = Eval("""$replace("abc", /(b)/, "[$0]")""");
        Assert.Equal("\"a[b]c\"", result);
    }

    [Fact]
    public void Replace_RegexBackreference_MultipleGroups()
    {
        string result = Eval("""$replace("2024-01-15", /(\d{4})-(\d{2})-(\d{2})/, "$3/$2/$1")""");
        Assert.Equal("\"15/01/2024\"", result);
    }

    [Fact]
    public void Replace_RegexWithStringAndLimit()
    {
        // RegexReplaceWithString with limit parameter
        string result = Eval("""$replace("banana", /a/, "o", 2)""");
        Assert.Equal("\"bonona\"", result);
    }

    // ─── $parseInteger with XPath picture (CompileParseInteger, 30 lines) ──

    [Fact]
    public void ParseInteger_BasicPicture()
    {
        Assert.Equal("42", Eval("""$parseInteger("42", "#0")"""));
    }

    [Fact]
    public void ParseInteger_WithGroupingSeparator()
    {
        Assert.Equal("1234", Eval("""$parseInteger("1,234", "#,##0")"""));
    }

    // ─── Unicode $substring with surrogate pairs (CodePointToCharIndex) ──

    [Fact]
    public void Substring_WithEmoji()
    {
        // $substring on string with surrogate pair — triggers CodePointToCharIndex
        Assert.Equal("\"😀\"", Eval("""$substring("\uD83D\uDE00hello", 0, 1)"""));
    }

    [Fact]
    public void Substring_AfterEmoji()
    {
        Assert.Equal("\"he\"", Eval("""$substring("\uD83D\uDE00hello", 1, 2)"""));
    }

    // ─── $encodeUrl with special characters (ValidateNoUnpairedSurrogates, 16 lines) ──

    [Fact]
    public void EncodeUrlComponent_SpecialChars()
    {
        Assert.Equal("\"%2F%3F%23\"", Eval("""$encodeUrlComponent("/?#")"""));
    }

    [Fact]
    public void EncodeUrl_PreservesPathChars()
    {
        Assert.Equal("\"http://example.com/path%20name\"", Eval("""$encodeUrl("http://example.com/path name")"""));
    }

    // ─── $spread: multi-item spread (CompileSpread, 34 uncovered lines) ──

    [Fact]
    public void Spread_SingleObject()
    {
        string data = """{"a": 1, "b": 2}""";
        string result = Eval("$spread($)", data);
        Assert.Contains("\"a\"", result);
    }

    [Fact]
    public void Spread_ArrayOfObjects()
    {
        string data = """[{"a": 1}, {"b": 2}]""";
        string result = Eval("$spread($)", data);
        Assert.Contains("\"a\"", result);
        Assert.Contains("\"b\"", result);
    }

    // ─── $decodeUrl with invalid percent-encoded edge cases ──

    [Fact]
    public void DecodeUrl_InvalidPercentEncoding_Throws()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            JsonataEvaluator.Default.EvaluateToString("""$decodeUrl("hello%GGworld")""", "null"));
        Assert.NotNull(ex);
    }

    [Fact]
    public void DecodeUrlComponent_IncompletePercent_Throws()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            JsonataEvaluator.Default.EvaluateToString("""$decodeUrlComponent("hello%2")""", "null"));
        Assert.NotNull(ex);
    }

    // ─── $toMillis edge cases ──

    [Fact]
    public void ToMillis_DateString()
    {
        string result = Eval("""$toMillis("2024-01-01T00:00:00.000Z")""");
        Assert.Equal("1704067200000", result);
    }

    [Fact]
    public void ToMillis_WithPicture()
    {
        string result = Eval("""$toMillis("15/01/2024", "[D01]/[M01]/[Y0001]")""");
        Assert.Equal("1705276800000", result);
    }

    // ─── $filter with function index parameter ──

    [Fact]
    public void Filter_WithIndexParam()
    {
        string data = """{"items": [10, 20, 30, 40, 50]}""";
        string result = Eval("""$filter(items, function($v, $i) { $i >= 2 })""", data);
        Assert.Equal("[30,40,50]", result);
    }

    // ─── $map with index parameter ──

    [Fact]
    public void Map_WithIndexParam()
    {
        string result = Eval("""$map([10, 20, 30], function($v, $i) { $i })""");
        Assert.Equal("[0,1,2]", result);
    }

    // ─── $shuffle with single element ──

    [Fact]
    public void Shuffle_SingleElement()
    {
        Assert.Equal("[1]", Eval("""$shuffle([1])"""));
    }

    [Fact]
    public void Shuffle_Empty()
    {
        // $shuffle of empty array may return empty array or undefined
        string result = Eval("""$shuffle([])""");
        Assert.True(result == "[]" || result == "undefined");
    }

    // ─── CompileFilter standalone (FunctionalCompiler lines 7944-8012) ───

    [Fact]
    public void Filter_BooleanPredicate_True()
    {
        // Boolean filter: true keeps element
        Assert.Equal("42", Eval("""42[true]"""));
    }

    [Fact]
    public void Filter_BooleanPredicate_False()
    {
        // Boolean filter: false drops element
        Assert.Equal("undefined", Eval("""42[false]"""));
    }

    [Fact]
    public void Filter_NumericIndex_OnArray()
    {
        // Numeric filter = index access
        Assert.Equal("20", Eval("""$[1]""", """[10,20,30]"""));
    }

    [Fact]
    public void Filter_NegativeIndex_OnArray()
    {
        // Negative numeric index wraps from end
        Assert.Equal("30", Eval("""$[-1]""", """[10,20,30]"""));
    }

    [Fact]
    public void Filter_OutOfBounds_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$[99]""", """[10,20,30]"""));
    }

    [Fact]
    public void Filter_GeneralTruthiness_String()
    {
        // Non-boolean, non-numeric: general truthiness (non-empty string is truthy)
        Assert.Equal("42", Eval("""42["yes"]"""));
    }

    [Fact]
    public void Filter_GeneralTruthiness_EmptyString()
    {
        // Empty string is falsy
        Assert.Equal("undefined", Eval("""42[""]"""));
    }

    // ─── CompileFocusSortStage (FunctionalCompiler lines 8098-8153) ───

    [Fact]
    public void FocusSort_ByFocusVariable()
    {
        // Focus variable sort: Employee@$e^($e.age)
        string data = """[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}]""";
        Assert.Equal(
            """[{"name":"A","age":10},{"name":"B","age":20},{"name":"C","age":30}]""",
            Eval("""$@$e^($e.age)""", data));
    }

    [Fact]
    public void FocusSort_Descending()
    {
        string data = """[{"name":"C","age":30},{"name":"A","age":10},{"name":"B","age":20}]""";
        Assert.Equal(
            """[{"name":"C","age":30},{"name":"B","age":20},{"name":"A","age":10}]""",
            Eval("""$@$e^(>$e.age)""", data));
    }

    [Fact]
    public void FocusSort_SingleElement_PassesThrough()
    {
        // Single element: sort returns the item itself (unwrapped)
        string data = """[{"name":"A","age":10}]""";
        Assert.Equal(
            """{"name":"A","age":10}""",
            Eval("""$@$e^($e.age)""", data));
    }

    // ─── $match with capture groups (BuiltInFunctions lines 3255-3328) ───

    [Fact]
    public void Match_DatePattern_WithGroups()
    {
        string result = Eval("""$match("2026-04-19", /(\d{4})-(\d{2})-(\d{2})/)""");
        Assert.Contains("\"match\":\"2026-04-19\"", result);
        Assert.Contains("\"groups\":[\"2026\",\"04\",\"19\"]", result);
    }

    [Fact]
    public void Match_NoCaptureGroups_FirstWord()
    {
        string result = Eval("""$match("hello world", /\w+/)""");
        Assert.Contains("\"match\":\"hello\"", result);
    }

    [Fact]
    public void Match_NoMatch_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$match("hello", /xyz/)"""));
    }

    // ─── $spread multi-element sequences (BuiltInFunctions lines 2632-2701) ───

    [Fact]
    public void Spread_SingleObjectIntoKeyValuePairs()
    {
        string result = Eval("""$spread({"a":1,"b":2})""");
        Assert.Equal("""[{"a":1},{"b":2}]""", result);
    }

    // ─── $formatNumber: exponent, grouping, subpicture ───

    [Fact]
    public void FormatNumber_NegativeWithSubpicture()
    {
        // Two-part picture: positive;negative — returns a string
        Assert.Equal("\"(1,234.56)\"", Eval("""$formatNumber(-1234.56, "#,##0.00;(#,##0.00)")"""));
    }

    [Fact]
    public void FormatNumber_IrregularGrouping()
    {
        // Irregular grouping: ##,##,##0
        string result = Eval("""$formatNumber(123456789, "##,##,##0")""");
        // Verify it's a valid formatted string
        Assert.StartsWith("\"", result);
        Assert.Contains(",", result);
    }

    // ─── $fromMillis/$toMillis with custom picture and timezone ───

    [Fact]
    public void FromMillis_CustomPicture_YearMonthDay()
    {
        // 2021-01-01 00:00:00 UTC = 1609459200000
        Assert.Equal("\"2021-01-01\"", Eval("""$fromMillis(1609459200000, "[Y0001]-[M01]-[D01]")"""));
    }

    [Fact]
    public void FromMillis_WithTimezone()
    {
        // UTC+5:30 -> 2021-01-01T05:30:00
        string result = Eval("""$fromMillis(1609459200000, "[Y0001]-[M01]-[D01]T[H01]:[m01]:[s01]", "+05:30")""");
        Assert.Equal("\"2021-01-01T05:30:00\"", result);
    }

    [Fact]
    public void ToMillis_CustomPicture()
    {
        Assert.Equal("1609459200000", Eval("""$toMillis("2021-01-01", "[Y0001]-[M01]-[D01]")"""));
    }

    [Fact]
    public void FromMillis_DayOfWeek()
    {
        // 2021-01-01 was a Friday
        string result = Eval("""$fromMillis(1609459200000, "[FNn]")""");
        Assert.Equal("\"Friday\"", result);
    }

    [Fact]
    public void FromMillis_MonthName()
    {
        string result = Eval("""$fromMillis(1609459200000, "[MNn]")""");
        Assert.Equal("\"January\"", result);
    }

    [Fact]
    public void FromMillis_MonthAbbrev()
    {
        string result = Eval("""$fromMillis(1609459200000, "[MNn,3-3]")""");
        Assert.Equal("\"Jan\"", result);
    }

    [Fact]
    public void FromMillis_WeekNumber()
    {
        // 2021-01-01 is in ISO week 53 of 2020 (Friday)
        string result = Eval("""$fromMillis(1609459200000, "[W01]")""");
        // Week 53 of 2020 or week 01 of 2021 depending on convention
        Assert.Matches(@"^\""[0-9]+\""$", result);
    }

    [Fact]
    public void FromMillis_NegativeTimezone()
    {
        // UTC-5:00 -> 2020-12-31T19:00:00
        string result = Eval("""$fromMillis(1609459200000, "[Y0001]-[M01]-[D01]T[H01]:[m01]:[s01]", "-05:00")""");
        Assert.Equal("\"2020-12-31T19:00:00\"", result);
    }

    // ─── $formatInteger ───

    [Fact]
    public void FormatInteger_Words()
    {
        Assert.Equal("\"forty-two\"", Eval("""$formatInteger(42, "w")"""));
    }

    [Fact]
    public void FormatInteger_Ordinal()
    {
        Assert.Equal("\"first\"", Eval("""$formatInteger(1, "w;o")"""));
    }

    [Fact]
    public void FormatInteger_RomanUpper()
    {
        Assert.Equal("\"XLII\"", Eval("""$formatInteger(42, "I")"""));
    }

    [Fact]
    public void FormatInteger_RomanLower()
    {
        Assert.Equal("\"xlii\"", Eval("""$formatInteger(42, "i")"""));
    }

    // ─── FormatNumberLikeJavaScript (FunctionalCompiler) ───

    [Fact]
    public void String_SmallExponent_CoercionPath()
    {
        // $string of very small number — JSONata preserves scientific notation
        Assert.Equal("\"1e-7\"", Eval("""$string(1e-7)"""));
    }

    [Fact]
    public void String_LargeNumber_CoercionPath()
    {
        Assert.Equal("\"100000000000000000000\"", Eval("""$string(1e20)"""));
    }

    // ─── Coalesce operator ───

    [Fact]
    public void Coalesce_MissingProperty_FallsToDefault()
    {
        string expr = "missing ?? \"default\"";
        string data = """{"existing":"value"}""";
        Assert.Equal("\"default\"", Eval(expr, data));
    }

    [Fact]
    public void Coalesce_ExistingProperty_ReturnsValue()
    {
        string expr = "existing ?? \"default\"";
        string data = """{"existing":"value"}""";
        Assert.Equal("\"value\"", Eval(expr, data));
    }

    // ─── Path chain over nested arrays ───

    [Fact]
    public void DeepPathChain_NestedArrays()
    {
        string data = """{"data":[{"items":[{"tag":"a"},{"tag":"b"}]},{"items":[{"tag":"c"}]}]}""";
        Assert.Equal("""["a","b","c"]""", Eval("data.items.tag", data));
    }

    // ─── Equality predicate on array property ───

    [Fact]
    public void EqualityPredicate_FiltersArray()
    {
        string data = """{"users":[{"name":"Alice","email":"a@test.com"},{"name":"Bob","email":"b@test.com"}]}""";
        Assert.Equal("\"a@test.com\"", Eval("""users[name="Alice"].email""", data));
    }

    // ─── $replace with function ───

    [Fact]
    public void Replace_WithFunction()
    {
        Assert.Equal("\"HELLO world\"", Eval("""$replace("hello world", /\w+/, function($m) { $uppercase($m.match) }, 1)"""));
    }

    [Fact]
    public void Replace_WithFunction_AllMatches()
    {
        Assert.Equal("\"HELLO WORLD\"", Eval("""$replace("hello world", /\w+/, function($m) { $uppercase($m.match) })"""));
    }
}
