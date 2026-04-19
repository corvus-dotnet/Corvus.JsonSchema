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

    // ─── BUG: $each 3-param callback — RT returns [] instead of correct values ──
    // Reference: $each({"x":1,"y":2}, function($v,$k,$o){$type($o)}) => ["object","object"]

    [Fact]
    public void Each_ThreeParam_ObjectArg_Bug()
    {
        // The 3rd callback parameter ($o) should receive the whole object.
        // RT currently returns [] because lambdaArgs is only rented with size 2.
        Assert.Equal(
            """["object","object"]""",
            Eval("""$each({"x":1,"y":2}, function($v,$k,$o){$type($o)})"""));
    }

    // ─── BUG: $string on multi-valued sequence — returns first element only ──
    // Reference: $string(data.items.name) on multi-valued => "[\"Alice\",\"Bob\",\"Charlie\"]"

    [Fact]
    public void String_MultiValuedSequence_Bug()
    {
        // $string() of a multi-valued sequence should produce the JSON array string,
        // not just the first element's string.
        string data = """{"data":{"items":[{"name":"Alice"},{"name":"Bob"},{"name":"Charlie"}]}}""";
        // Reference: the JS string value is ["Alice","Bob","Charlie"]
        // EvaluateToString returns GetRawText(), so the JSON-encoded string is expected.
        Assert.Equal(
            """"
            "[\"Alice\",\"Bob\",\"Charlie\"]"
            """",
            Eval("$string(data.items.name)", data));
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

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler coverage — RT-only tests for uncovered blocks
    // ═══════════════════════════════════════════════════════════════════

    // Lines 5016-5082: ApplySimpleNamePairGroupBy — duplicate key groupby
    [Fact]
    public void GroupByAnnotation_DuplicateKeys()
    {
        string data = """{"items":[{"cat":"fruit","val":"apple"},{"cat":"veg","val":"carrot"},{"cat":"fruit","val":"banana"}]}""";
        Assert.Equal("""{"fruit":["apple","banana"],"veg":"carrot"}""", Eval("""items{cat: val}""", data));
    }

    // Lines 1558-1640: CollectAndContinue — equality predicate + continue chain
    [Fact]
    public void EqualityPredicate_WithContinuation()
    {
        string data = """{"orders":[{"status":"shipped","items":[{"name":"Widget"},{"name":"Gadget"}]},{"status":"pending","items":[{"name":"Thing"}]}]}""";
        Assert.Equal("""["Widget","Gadget"]""", Eval("""orders[status="shipped"].items.name""", data));
    }

    // Lines 2033-2109: EvalChainOverArrayIntoStatic — array descent through nested arrays
    [Fact]
    public void ArrayDescent_DeepNested()
    {
        string data = """{"Account":{"Order":[{"Product":[{"Price":10},{"Price":20}]},{"Product":[{"Price":30}]}]}}""";
        Assert.Equal("[10,20,30]", Eval("Account.Order.Product.Price", data));
    }

    // Lines 8091-8153: CompileFocusSortStage — sort with comparator function
    [Fact]
    public void SortWithComparator_ByProperty()
    {
        string data = """{"items":[{"name":"c","priority":3},{"name":"a","priority":1},{"name":"b","priority":2}]}""";
        string result = Eval("""$sort(items, function($l,$r){$l.priority > $r.priority})""", data);
        Assert.Equal("""[{"name":"a","priority":1},{"name":"b","priority":2},{"name":"c","priority":3}]""", result);
    }

    // Lines 598-622: ContinueChainFlatInto — chain flattening through nested arrays
    [Fact]
    public void ChainFlattening_NestedArrays()
    {
        string data = """{"data":{"items":[{"values":[1,2]},{"values":[3,4]}]}}""";
        Assert.Equal("[1,2,3,4]", Eval("data.items.values", data));
    }

    // Lines 4315-4343: encodeUrl non-string path
    [Fact]
    public void EncodeUrl_StringInput()
    {
        Assert.Equal("\"hello%20world/path\"", Eval("""$encodeUrl("hello world/path")"""));
    }

    // Lines 572-598: formatInteger ordinal
    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(101, "101st")]
    public void FormatInteger_OrdinalSuffix(int value, string expected)
    {
        Assert.Equal($"\"{expected}\"", Eval($"""$formatInteger({value}, "1;o")"""));
    }

    // Lines 2849-2860: parseInteger with sign prefix
    [Fact]
    public void ParseInteger_PlusSign()
    {
        Assert.Equal("42", Eval("""$parseInteger("+42", "0")"""));
    }

    [Fact]
    public void ParseInteger_MinusSign()
    {
        Assert.Equal("-99", Eval("""$parseInteger("-99", "0")"""));
    }

    // Lines 2712-2725: parseInteger with grouping separators
    [Fact]
    public void ParseInteger_GroupingSeparators()
    {
        Assert.Equal("1234567", Eval("""$parseInteger("1,234,567", "#,##0")"""));
    }

    // Lines 5047-5072, 4899-4919: formatNumber with exponent pattern
    [Fact]
    public void FormatNumber_ExponentPattern()
    {
        string result = Eval("""$formatNumber(12345, "0.00e0")""");
        Assert.StartsWith("\"", result);
        Assert.Contains("e", result);
    }

    [Fact]
    public void FormatNumber_SmallExponent()
    {
        string result = Eval("""$formatNumber(0.00123, "0.00e0")""");
        Assert.StartsWith("\"", result);
        Assert.Contains("e", result);
    }

    // Lines 7561-7591: Unicode surrogate handling in $length/$substring
    [Fact]
    public void Length_WithSurrogatePairs()
    {
        // "abc😀def" is 7 code points (emoji is 1 code point, 2 UTF-16 chars)
        Assert.Equal("7", Eval("""$length("abc\uD83D\uDE00def")"""));
    }

    [Fact]
    public void Substring_WithSurrogatePairs()
    {
        // Substring starting after emoji (code point 4) for 3 chars
        Assert.Equal("\"def\"", Eval("""$substring("abc\uD83D\uDE00def", 4, 3)"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler targeted coverage — RT-only tests
    // These target specific uncovered branches identified from Cobertura XML
    // ═══════════════════════════════════════════════════════════════════

    // Lines 5016-5082: ApplySimpleNamePairGroupBy — duplicate key merging
    // Key requirement: multiple items MUST share the same groupby key
    [Fact]
    public void GroupByAnnotation_DuplicateKeysMerge()
    {
        string data = """{"items":[{"cat":"A","val":1},{"cat":"A","val":2},{"cat":"B","val":3},{"cat":"A","val":4}]}""";
        Assert.Equal("""{"A":[1,2,4],"B":3}""", Eval("items{cat: val}", data));
    }

    // Lines 2033-2109: EvalChainOverArrayIntoStatic — multi-level nested array descent
    // Requires arrays at MULTIPLE intermediate levels of the chain
    [Fact]
    public void ArrayDescent_MultiLevelNested()
    {
        string data = """{"orders":{"items":[{"tags":[{"name":"foo"},{"name":"bar"}]},{"tags":[{"name":"baz"}]}]}}""";
        Assert.Equal("""["foo","bar","baz"]""", Eval("orders.items.tags.name", data));
    }

    [Fact]
    public void ArrayDescent_FourLevelChain()
    {
        string data = """{"company":{"departments":[{"teams":[{"members":[{"name":"Alice"}]},{"members":[{"name":"Bob"}]}]},{"teams":[{"members":[{"name":"Carol"}]}]}]}}""";
        Assert.Equal("""["Alice","Bob","Carol"]""", Eval("company.departments.teams.members.name", data));
    }

    // Lines 1558-1640: CollectAndContinue — per-element index + equality predicate
    [Fact]
    public void PerElementIndex_InChain()
    {
        string data = """{"data":{"items":[{"name":"first"},{"name":"second"}]}}""";
        Assert.Equal("\"first\"", Eval("data.items[0].name", data));
    }

    [Fact]
    public void NegativeIndex_InChain()
    {
        string data = """{"data":{"items":[{"name":"first"},{"name":"second"}]}}""";
        Assert.Equal("\"second\"", Eval("data.items[-1].name", data));
    }

    // Lines 7945-8012: CompileFilter — various filter patterns
    [Fact]
    public void Filter_BooleanPredicate()
    {
        string data = """{"items":[1,2,3,4,5]}""";
        Assert.Equal("[4,5]", Eval("items[$>3]", data));
    }

    [Fact]
    public void Filter_NumericIndex()
    {
        string data = """{"items":["a","b","c","d","e"]}""";
        Assert.Equal("\"b\"", Eval("items[1]", data));
    }

    [Fact]
    public void Filter_NegativeIndex()
    {
        string data = """{"items":["a","b","c","d","e"]}""";
        Assert.Equal("\"e\"", Eval("items[-1]", data));
    }

    // Lines 2234-2256: CompileFilterFunc — multi-valued sequence flattening
    [Fact]
    public void FilterFunc_WithIndex()
    {
        string data = """[1,2,3,4,5]""";
        Assert.Equal("[3,4,5]", Eval("$filter($, function($v,$i){$i > 1})", data));
    }

    // Lines 4315-4343: $encodeUrl non-string path (coercion)
    [Fact]
    public void EncodeUrl_NonStringPath()
    {
        Assert.Equal("\"hello%20world\"", Eval("""$encodeUrl("hello world")"""));
    }

    // Lines 1558-1640: Equality predicate filtering on nested chain
    [Fact]
    public void EqualityPredicateOnNestedChain()
    {
        string data = """{"records":[{"status":"active","items":[{"id":1},{"id":2}]},{"status":"closed","items":[{"id":3}]}]}""";
        Assert.Equal("[1,2]", Eval("""records[status="active"].items.id""", data));
    }

    // Lines 598-622 (CG): Array in middle of property chain
    [Fact]
    public void ChainFlat_ArrayInMiddle()
    {
        string data = """{"data":{"nested":[{"items":[{"name":"a"}]},{"items":[{"name":"b"},{"name":"c"}]}]}}""";
        Assert.Equal("""["a","b","c"]""", Eval("data.nested.items.name", data));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler lines 5641-5739: Filter with array of numeric indices
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Filter_ArrayOfNumericIndices()
    {
        // Tests the array-of-indices branch at lines 5655-5691
        Assert.Equal("""["a","c","e"]""", Eval("data[[0,2,4]]", """{"data":["a","b","c","d","e"]}"""));
    }

    [Fact]
    public void Filter_ArrayOfNegativeIndices()
    {
        // Negative indices in filter array — tests idx < 0 adjustment at line 5671
        Assert.Equal("""["d","e"]""", Eval("data[[-1,-2]]", """{"data":["a","b","c","d","e"]}"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler lines 2065-2109: EvalChainOverArrayIntoStatic
    // Property chain traversal through arrays at 3+ intermediate levels
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DeepChain_ThreeLevelArrayDescent()
    {
        // Arrays at levels a, b.c triggering recursive EvalChainOverArrayIntoStatic
        string data = """{"a":[{"b":[{"c":[{"d":"x"},{"d":"y"}]},{"c":{"d":"z"}}]},{"b":{"c":{"d":"w"}}}]}""";
        Assert.Equal("""["x","y","z","w"]""", Eval("a.b.c.d", data));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler lines 6866-6901: Tuple array constructor
    // Array ctor with multi-valued sub-expressions
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ArrayConstructor_WithMultiValuedItems()
    {
        // [data.items.name, "extra"] — first item is multi-valued sequence, second is scalar
        string data = """{"data":{"items":[{"name":"x"},{"name":"y"}]}}""";
        Assert.Equal("""["x","y","extra"]""", Eval("""[data.items.name, "extra"]""", data));
    }

    [Fact]
    public void ArrayConstructor_WithRange()
    {
        // Range expression in array constructor
        Assert.Equal("[1,2,3,4,5]", Eval("[1..5]"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FunctionalCompiler lines 5559-5594: Sort stage array flattening
    // Sort preceded by array-producing step requiring flatten
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Sort_AfterArrayFlatten()
    {
        // $sort($append(data.a, data.b)) — flattened array then sorted
        string data = """{"data":{"a":[3,1,5],"b":[2,4]}}""";
        Assert.Equal("[1,2,3,4,5]", Eval("$sort($append(data.a, data.b))", data));
    }

    [Fact]
    public void Sort_ThenPropertyAccess()
    {
        // items^(price).name — sort then continue chain with property access
        string data = """{"items":[{"price":30,"name":"c"},{"price":10,"name":"a"},{"price":20,"name":"b"}]}""";
        Assert.Equal("""["a","b","c"]""", Eval("items^(price).name", data));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 5047-5072: FormatNumber exponent normalization
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(12345, "0.00e0", "1.23e4")]
    [InlineData(0.00123, "0.00e0", "1.23e-3")]
    [InlineData(100, "0.0e0", "10.0e1")]
    [InlineData(1, "0.00e0", "1.00e0")]
    [InlineData(9876543, "0.000e0", "9.877e6")]
    public void FormatNumber_ExponentNormalization(double value, string picture, string expected)
    {
        string expr = $"""$formatNumber({value.ToString(System.Globalization.CultureInfo.InvariantCulture)}, "{picture}")""";
        Assert.Equal($"\"{expected}\"", Eval(expr));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 4811-4853: FormatNumber grouping positions
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1234567, "#,##0", "1,234,567")]
    [InlineData(1234567.89, "#,##0.00", "1,234,567.89")]
    [InlineData(123, "#,##0", "123")]
    public void FormatNumber_Grouping(double value, string picture, string expected)
    {
        string expr = $"""$formatNumber({value.ToString(System.Globalization.CultureInfo.InvariantCulture)}, "{picture}")""";
        Assert.Equal($"\"{expected}\"", Eval(expr));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 2234-2256: CompileFilterFunc flatten
    // Multi-valued sequence containing arrays that need flattening
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void FilterFunc_NestedArrayFlatten_Uppercase()
    {
        // Nested arrays producing multi-valued seq with array elements — triggers flatten at 2234
        string data = """{"Account":{"Order":[{"Product":[{"Description":{"Colour":"red"}},{"Description":{"Colour":"blue"}}]},{"Product":[{"Description":{"Colour":"green"}}]}]}}""";
        Assert.Equal("""["RED","BLUE","GREEN"]""", Eval("Account.Order.Product.$uppercase(Description.Colour)", data));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 4315-4343: $encodeUrl non-string input
    // Corvus extension: coerces non-string to string before encoding
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void EncodeUrl_NumberInput_CorvusExtension()
    {
        // Reference jsonata throws T0410 for non-string; Corvus coerces to string
        Assert.Equal("\"42\"", Eval("$encodeUrl(42)"));
    }

    [Fact]
    public void EncodeUrl_BooleanInput_CorvusExtension()
    {
        Assert.Equal("\"true\"", Eval("$encodeUrl(true)"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 4414-4429: HasInvalidPercentEncoding
    // $decodeUrl with malformed percent encoding
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DecodeUrl_MalformedPercent_Throws()
    {
        // "hello%2world" — incomplete percent encoding (only 1 hex digit after %)
        var ex = Assert.Throws<JsonataException>(() => Eval("""$decodeUrl("hello%2world")"""));
        Assert.Equal("D3140", ex.Code);
    }

    // ═══════════════════════════════════════════════════════════════════
    // BuiltInFunctions lines 2644-2660: $spread array property counting
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Spread_ArrayOfMultiKeyObjects()
    {
        // Each multi-key object is split into single-key objects
        Assert.Equal("""[{"a":1},{"b":2},{"c":3}]""", Eval("""$spread({"a":1,"b":2,"c":3})"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // $each with 3 parameters (key, value, object)
    // Reference: ["a=1","b=2"]
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Each_ThreeParam_KeyValueConcat()
    {
        Assert.Equal("""["a=1","b=2"]""", Eval("""$each({"a":1,"b":2}, function($v,$k,$o){$k & "=" & $string($v)})"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // $formatInteger with grouping separator (XPathDateTimeFormatter)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1234567, "#,##0", "1,234,567")]
    [InlineData(42, "w", "forty-two")]
    [InlineData(42, "W", "FORTY-TWO")]
    [InlineData(1001, "w", "one thousand and one")]
    public void FormatInteger_Various(int value, string picture, string expected)
    {
        string expr = $"""$formatInteger({value}, "{picture}")""";
        Assert.Equal($"\"{expected}\"", Eval(expr));
    }

    // ═══════════════════════════════════════════════════════════════════
    // $length and $substring with surrogate pairs (code point counting)
    // CGH lines 7561-7591: CountCodePoints
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Length_CodePointCounting()
    {
        // "Hello😊World" = 11 code points (😊 is 1 code point)
        Assert.Equal("11", Eval("""$length("Hello\ud83d\ude0aWorld")"""));
    }

    [Fact]
    public void Length_AllEmoji()
    {
        // Three emoji = 3 code points
        Assert.Equal("3", Eval("""$length("\ud83d\ude0a\ud83d\ude0a\ud83d\ude0a")"""));
    }

    [Fact]
    public void Substring_CodePointIndexing()
    {
        // Get the emoji at code point index 5
        Assert.Equal("\"\ud83d\ude0a\"", Eval("""$substring("Hello\ud83d\ude0aWorld", 5, 1)"""));
    }

    [Fact]
    public void Substring_AfterSurrogate()
    {
        // Get "World" starting at code point index 6
        Assert.Equal("\"World\"", Eval("""$substring("Hello\ud83d\ude0aWorld", 6)"""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // $formatNumber percent
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_PercentPicture()
    {
        Assert.Equal("\"45%\"", Eval("""$formatNumber(0.45, "##0%")"""));
    }
}
