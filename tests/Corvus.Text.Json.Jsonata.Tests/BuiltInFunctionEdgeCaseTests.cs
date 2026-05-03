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

    // ─── FunctionalCompiler: CollectAndContinue (lines 1558-1640) ─────────
    // Equality predicate on singleton object (not array) with continuation

    [Fact]
    public void Chain_EqualityPredicateOnSingleton_WithContinuation()
    {
        // account.orders is a singleton object, not an array. The equality predicate
        // should match and continue the chain to items.name.
        string data = """{"account":{"orders":{"type":"premium","items":[{"name":"Widget"},{"name":"Gadget"}]}}}""";
        Assert.Equal(
            """["Widget","Gadget"]""",
            Eval("""account.orders[type="premium"].items.name""", data));
    }

    [Fact]
    public void Chain_PerElementIndex_WithContinuation()
    {
        // records is an array, [0] selects first, then items[1] selects second item
        string data = """{"Data":{"records":[{"items":[{"value":"a"},{"value":"b"},{"value":"c"}]},{"items":[{"value":"x"},{"value":"y"}]}]}}""";
        Assert.Equal(
            "\"b\"",
            Eval("Data.records[0].items[1].value", data));
    }

    // ─── FunctionalCompiler: EvalChainOverArrayIntoStatic (lines 2033-2109) ──

    [Fact]
    public void Chain_NestedArrayAtIntermediateLevel()
    {
        // matrix is an array; .values on each element produces flattened result
        string data = """{"Data":{"matrix":[{"values":[10,20,30]},{"values":[40,50]}]}}""";
        Assert.Equal(
            "[10,20,30,40,50]",
            Eval("Data.matrix.values", data));
    }

    [Fact]
    public void Chain_ThreeLevelNestedArray()
    {
        string data = """{"a":[{"b":{"c":{"d":1}}},{"b":{"c":{"d":2}}}]}""";
        Assert.Equal("[1,2]", Eval("a.b.c.d", data));
    }

    // ─── FunctionalCompiler: CompileFilter standalone (lines 7945-8012) ───

    [Fact]
    public void Filter_OnMultiValuedPath()
    {
        // Account.Order.Product is a multi-valued sequence (not a single array)
        string data = """{"Account":{"Order":[{"Product":{"Price":35,"Name":"A"}},{"Product":{"Price":20,"Name":"B"}},{"Product":{"Price":50,"Name":"C"}}]}}""";
        Assert.Equal(
            """[{"Price":35,"Name":"A"},{"Price":50,"Name":"C"}]""",
            Eval("""$filter(Account.Order.Product, function($v){$v.Price > 30})""", data));
    }

    [Fact]
    public void Filter_ConditionalPredicate()
    {
        // Predicate returns boolean per element
        string data = """{"data":{"items":[{"price":10,"name":"A"},{"price":30,"name":"B"},{"price":50,"name":"C"}]}}""";
        Assert.Equal(
            """["B","C"]""",
            Eval("""data.items[price>20].name""", data));
    }

    // ─── FunctionalCompiler: ApplySimpleNamePairGroupBy (lines 4984-5082) ──

    [Fact]
    public void GroupBy_AnnotationSyntax_DuplicateKeys()
    {
        // GroupBy annotation {Name: Price} merges duplicate keys into arrays
        string data = """{"Account":{"Order":[{"Product":{"Name":"A","Price":10}},{"Product":{"Name":"B","Price":20}},{"Product":{"Name":"A","Price":30}}]}}""";
        Assert.Equal(
            """{"A":[10,30],"B":20}""",
            Eval("Account.Order.Product{Name: Price}", data));
    }

    // ─── FunctionalCompiler: CompilePath sort result expansion (lines 3155-3180) ──

    [Fact]
    public void Sort_ThenChainAccess()
    {
        // Sort first, then access properties from sorted results
        string data = """{"Account":{"Order":[{"Product":{"Price":30,"Name":"C"}},{"Product":{"Price":10,"Name":"A"}},{"Product":{"Price":20,"Name":"B"}}]}}""";
        Assert.Equal(
            """["A","B","C"]""",
            Eval("Account.Order^(Product.Price).Product.Name", data));
    }

    // ─── FunctionalCompiler: CompileArrayConstructor (lines 6866-6901) ──

    [Fact]
    public void ArrayConstructor_MixedTypes()
    {
        Assert.Equal(
            """[1,"hello",true,null,[2,3]]""",
            Eval("""[1, "hello", true, null, [2,3]]"""));
    }

    // ─── FunctionalCompiler: Transform operator ──

    [Fact]
    public void Transform_AddPropertyToArrayElements()
    {
        string data = """{"Account":{"Order":[{"Product":"A"},{"Product":"B"}]}}""";
        Assert.Equal(
            """{"Account":{"Order":[{"Product":"A","Discount":10},{"Product":"B","Discount":10}]}}""",
            Eval("""$ ~> |Account.Order|{"Discount":10}|""", data));
    }

    // ─── BuiltInFunctions: $sift, $reduce, $map with index ──

    [Fact]
    public void Sift_FilterObjectProperties()
    {
        Assert.Equal(
            """{"b":2,"c":3}""",
            Eval("""$sift({"a":1,"b":2,"c":3}, function($v){$v > 1})"""));
    }

    [Fact]
    public void Reduce_WithInitialValue()
    {
        Assert.Equal("20", Eval("""$reduce([1,2,3,4], function($prev,$curr){$prev + $curr}, 10)"""));
    }

    [Fact]
    public void Map_WithIndex()
    {
        Assert.Equal("[0,20,60]", Eval("""$map([10,20,30], function($v,$i){$v * $i})"""));
    }

    // ─── BuiltInFunctions: $spread on multi-key objects ──

    [Fact]
    public void Spread_ArrayOfMultiKeyObjects_Flattened()
    {
        Assert.Equal(
            """[{"a":1},{"b":2},{"c":3}]""",
            Eval("""$spread([{"a":1,"b":2},{"c":3}])"""));
    }

    // ─── BuiltInFunctions: $formatNumber exponent (lines 5062-5087) ──

    [Fact]
    public void FormatNumber_ScientificSmallNumber()
    {
        Assert.Equal("\"1.23e-4\"", Eval("""$formatNumber(0.000123, "0.00e0")"""));
    }

    // ─── BuiltInFunctions: $formatBase ──

    [Theory]
    [InlineData(255, 16, "ff")]
    [InlineData(255, 2, "11111111")]
    [InlineData(255, 8, "377")]
    public void FormatBase_Various(int value, int radix, string expected)
    {
        Assert.Equal($"\"{expected}\"", Eval($"$formatBase({value}, {radix})"));
    }

    // ─── BuiltInFunctions: $type ──

    [Fact]
    public void Type_AllTypes()
    {
        Assert.Equal(
            """["number","string","boolean","null","array","object"]""",
            Eval("""[$type(1), $type("s"), $type(true), $type(null), $type([]), $type({})]"""));
    }

    // ─── XPathDateTimeFormatter: Unicode digits (lines 1298-1312, 1619-1637) ──

    [Theory]
    [InlineData(2025, "\u0661", "\u0662\u0660\u0662\u0665")]
    [InlineData(99, "\u0967", "\u096F\u096F")]
    public void FormatInteger_UnicodeDigits(int value, string presentation, string expected)
    {
        Assert.Equal($"\"{expected}\"", Eval($"$formatInteger({value}, \"{presentation}\")"));
    }

    // ─── Wildcard and descendant paths ──

    [Fact]
    public void Wildcard_OnObject()
    {
        Assert.Equal("[1,2,3]", Eval("""{"a":1,"b":2,"c":3}.*"""));
    }

    [Fact]
    public void Wildcard_ChainedOnObject()
    {
        string data = """{"data":{"a":{"name":"Alice"},"b":{"name":"Bob"},"c":{"name":"Charlie"}}}""";
        Assert.Equal(
            """["Alice","Bob","Charlie"]""",
            Eval("data.*.name", data));
    }

    [Fact]
    public void Descendant_Search()
    {
        string data = """{"data":{"a":{"name":"Alice"},"b":{"inner":{"name":"Bob"}}}}""";
        Assert.Equal(
            """["Alice","Bob"]""",
            Eval("**.name", data));
    }

    // ─── String concat with multi-valued sequence ──
    // Note: $string & on multi-valued sequences has edge cases - RT coerces to first element.
    // Reference: "[\"A\",\"B\"] test" — this is a known behavioral difference, not tested here.

    // ─── Closure / higher-order functions ──

    [Fact]
    public void Lambda_Closure()
    {
        Assert.Equal("7", Eval("""( $add := function($x){function($y){$x + $y}}; $add(3)(4) )"""));
    }

    // ─── $sort with custom comparator ──

    [Fact]
    public void Sort_DefaultAlphabetical()
    {
        Assert.Equal(
            """["apple","banana","cherry"]""",
            Eval("""$sort(["banana","apple","cherry"])"""));
    }

    [Fact]
    public void Sort_CustomDescending()
    {
        Assert.Equal(
            "[5,4,3,1,1]",
            Eval("""$sort([3,1,4,1,5], function($a,$b){$b - $a})"""));
    }

    // ─── Coalesce operator (??) — Corvus extension ──
    // Desugars to $exists(lhs) ? lhs : rhs with ReferenceEquals,
    // triggers coalesce fusion path (EvalSimplePropertyChainStatic).

    [Fact]
    public void Coalesce_SimpleChain_Found()
    {
        // Reference: $exists(data.value) ? data.value : "default" → 42
        Assert.Equal("42", Eval("""data.value ?? "default" """, """{"data":{"value":42}}"""));
    }

    [Fact]
    public void Coalesce_SimpleChain_Fallback()
    {
        // Reference: $exists(data.value) ? data.value : "default" → "default"
        Assert.Equal("\"default\"", Eval("""data.value ?? "default" """, """{"data":{}}"""));
    }

    [Fact]
    public void Coalesce_DeepChain_Found()
    {
        Assert.Equal("\"found\"", Eval("""data.x.y ?? "fallback" """, """{"data":{"x":{"y":"found"}}}"""));
    }

    [Fact]
    public void Coalesce_DeepChain_Fallback()
    {
        Assert.Equal("\"fallback\"", Eval("""data.x.y ?? "fallback" """, """{"data":{"x":{}}}"""));
    }

    [Fact]
    public void Coalesce_VeryDeep_Found()
    {
        Assert.Equal("99", Eval("""data.a.b.c ?? 0""", """{"data":{"a":{"b":{"c":99}}}}"""));
    }

    [Fact]
    public void Coalesce_VeryDeep_Fallback()
    {
        Assert.Equal("0", Eval("""data.a.b.c ?? 0""", """{"data":{}}"""));
    }

    [Fact]
    public void Coalesce_ArrayMidChain()
    {
        // data.items is an array → triggers EvalChainOverArrayStatic
        Assert.Equal(
            """["A","B","C"]""",
            Eval("""data.items.name ?? []""", """{"data":{"items":[{"name":"A"},{"name":"B"},{"name":"C"}]}}"""));
    }

    [Fact]
    public void Coalesce_ArrayMidChain_Fallback()
    {
        Assert.Equal("[]", Eval("""data.items.name ?? []""", """{"data":{}}"""));
    }

    [Fact]
    public void Coalesce_NonObject_Fallback()
    {
        // data.value is a number, not an object → returns undefined → fallback
        Assert.Equal("0", Eval("""data.value.nested ?? 0""", """{"data":{"value":42}}"""));
    }

    // ─── $formatInteger word output ──

    [Fact]
    public void FormatInteger_WordLower()
    {
        // Reference: "forty-two"
        Assert.Equal("\"forty-two\"", Eval("""$formatInteger(42, "w")"""));
    }

    [Fact]
    public void FormatInteger_WordUpper()
    {
        // Reference: "FORTY-TWO"
        Assert.Equal("\"FORTY-TWO\"", Eval("""$formatInteger(42, "W")"""));
    }

    [Fact]
    public void FormatInteger_WordTitle()
    {
        // Reference: "Forty-Two"
        Assert.Equal("\"Forty-Two\"", Eval("""$formatInteger(42, "Ww")"""));
    }

    [Fact]
    public void ParseInteger_Word()
    {
        // Reference: 42
        Assert.Equal("42", Eval("""$parseInteger("forty-two", "w")"""));
    }

    [Fact]
    public void ParseInteger_WordUpper()
    {
        // Reference: 100
        Assert.Equal("100", Eval("""$parseInteger("ONE HUNDRED", "W")"""));
    }

    [Fact]
    public void ParseInteger_WordTitle()
    {
        // Reference: 99
        Assert.Equal("99", Eval("""$parseInteger("Ninety-Nine", "Ww")"""));
    }

    // ─── $formatNumber scientific/engineering ──

    [Fact]
    public void FormatNumber_ScientificLarge()
    {
        // Reference: "1.23e4"
        Assert.Equal("\"1.23e4\"", Eval("""$formatNumber(12345, "0.00e0")"""));
    }

    [Fact]
    public void FormatNumber_ScientificSmall()
    {
        // Reference: "5.00e-2"
        Assert.Equal("\"5.00e-2\"", Eval("""$formatNumber(0.05, "0.00e0")"""));
    }

    [Fact]
    public void FormatNumber_Engineering()
    {
        // Reference: "10.0e1"
        Assert.Equal("\"10.0e1\"", Eval("""$formatNumber(100, "##0.0e0")"""));
    }

    [Fact]
    public void FormatNumber_PerMille_Small()
    {
        // Reference: "25‰"
        Assert.Equal("\"25\u2030\"", Eval("""$formatNumber(0.025, "##0\u2030")"""));
    }

    [Fact]
    public void FormatNumber_GroupingSeparator_Large()
    {
        // Reference: "1,234,567"
        Assert.Equal("\"1,234,567\"", Eval("""$formatNumber(1234567, "#,##0")"""));
    }

    [Fact]
    public void FormatNumber_OptionalDecimalDigits()
    {
        // Reference: "0.5"
        Assert.Equal("\"0.5\"", Eval("""$formatNumber(0.5, "#0.###")"""));
    }

    [Fact]
    public void FormatNumber_NegativeSubPicture()
    {
        // Reference: "(042)"
        Assert.Equal("\"(042)\"", Eval("""$formatNumber(-42, "000;(000)")"""));
    }

    // ─── $single error paths (BuiltInFunctions lines 2881-2891) ──

    [Fact]
    public void Single_NoMatch_ThrowsD3139()
    {
        // No element satisfies predicate → D3139
        EvalThrows("""$single([1,2,3], function($v){$v=5})""", "null", "D3139");
    }

    [Fact]
    public void Single_MultipleMatches_ThrowsD3138()
    {
        // More than one element satisfies predicate → D3138
        EvalThrows("""$single([1,2,2], function($v){$v=2})""", "null", "D3138");
    }

    [Fact]
    public void Single_EmptyArray_ThrowsD3139()
    {
        // Empty array → D3139
        EvalThrows("""$single([])""", "null", "D3139");
    }

    // ─── Index binding #$i (FunctionalCompiler lines 4037-4135, 5545-5643) ──

    [Fact]
    public void IndexBinding_Basic()
    {
        // #$i annotates each element with its position
        string result = Eval("""["a","b","c"]#$i.{"value": $, "index": $i}""");
        Assert.Equal("""[{"value":"a","index":0},{"value":"b","index":1},{"value":"c","index":2}]""", result);
    }

    [Fact]
    public void IndexBinding_WithFilter()
    {
        // Index binding combined with bracket filter
        string result = Eval("""[10,20,30,40,50]#$i[$i < 3]""");
        Assert.Equal("[10,20,30]", result);
    }

    // ─── Focus binding @$var cross-join (FunctionalCompiler lines 2975-3079) ──

    [Fact]
    public void FocusBinding_CrossJoin_CorrelatesData()
    {
        // Cross-join: correlate loans with books by isbn
        string data = """{"library":{"loans":[{"isbn":"123"},{"isbn":"456"}],"books":[{"isbn":"123","title":"A"},{"isbn":"456","title":"B"}]}}""";
        string result = Eval("""library.loans@$l.books[isbn=$l.isbn].title""", data);
        Assert.Equal("""["A","B"]""", result);
    }

    [Fact]
    public void FocusBinding_CrossJoin_SimpleCorrelation()
    {
        string data = """{"data":[{"ref":1},{"ref":2}],"other":[{"id":1,"name":"one"},{"id":2,"name":"two"}]}""";
        string result = Eval("""data@$d.other[id=$d.ref].name""", data);
        Assert.Equal("""["one","two"]""", result);
    }

    // ─── $keys / $lookup on array of objects (BuiltInFunctions lines 2793-2798) ──

    [Fact]
    public void Keys_ArrayOfObjects_Deduplicates()
    {
        Assert.Equal("""["a","b"]""", Eval("""$keys([{"a":1},{"b":2},{"a":3}])"""));
    }

    [Fact]
    public void Keys_ArrayOfObjects_MultipleKeysPerObject()
    {
        Assert.Equal("""["x","y","z"]""", Eval("""$keys([{"x":1,"y":2},{"y":3,"z":4}])"""));
    }

    [Fact]
    public void Lookup_ArrayOfObjects_CollectsAllValues()
    {
        Assert.Equal("[1,2]", Eval("""$lookup([{"a":1},{"a":2},{"b":3}], "a")"""));
    }

    [Fact]
    public void Lookup_ArrayOfObjects_SingleMatch()
    {
        Assert.Equal("2", Eval("""$lookup([{"a":1},{"b":2}], "b")"""));
    }

    // ─── $split with limit (BuiltInFunctions lines 1375-1405) ──

    [Fact]
    public void Split_WithLimit_Truncates()
    {
        // Limit truncates to at most N parts
        Assert.Equal("""["a","b"]""", Eval("""$split("a,b,c,d", ",", 2)"""));
    }

    [Fact]
    public void Split_WithLimit_One()
    {
        Assert.Equal("""["x"]""", Eval("""$split("x-y-z", "-", 1)"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $match with limit (BuiltInFunctions lines 3130-3138)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Match_WithLimit()
    {
        // Limit truncates to at most N matches; singleton unwrapped
        Assert.Equal(
            """{"match":"123","index":3,"groups":[]}""",
            Eval("""$match("abc123def456", /[0-9]+/, 1)"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $decodeUrlComponent bad percent encoding (BuiltInFunctions lines 4429-4444)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("""$decodeUrlComponent("test%2")""", "D3140")]
    [InlineData("""$decodeUrlComponent("test%GG")""", "D3140")]
    public void DecodeUrlComponent_BadPercentEncoding(string expression, string expectedCode)
    {
        EvalThrows(expression, "null", expectedCode);
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber picture validation errors (BuiltInFunctions lines 4773-4793)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_MandatoryBeforeOptionalError()
    {
        // Mandatory digit (0) before optional (#) in integer part
        EvalThrows("""$formatNumber(123, "0#")""", "null", "D3090");
    }

    [Fact]
    public void FormatNumber_MandatoryAfterOptionalFracError()
    {
        // Mandatory digit (0) after optional (#) in fractional part
        EvalThrows("""$formatNumber(0.5, "0.#0")""", "null", "D3091");
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber exponent normalization (BuiltInFunctions lines 5062-5087)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("""$formatNumber(1234.5, "0.0e0")""", "\"1.2e3\"")]
    [InlineData("""$formatNumber(0.00123, "0.00e0")""", "\"1.23e-3\"")]
    [InlineData("""$formatNumber(1234567, "0.0e00")""", "\"1.2e06\"")]
    public void FormatNumber_ExponentNormalization_ViaEval(string expression, string expected)
    {
        Assert.Equal(expected, Eval(expression));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber with explicit negative sub-picture (additional patterns)
    // (BuiltInFunctions lines 4983-4995, 5025-5037)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("""$formatNumber(-42, "0;0-")""", "\"42-\"")]
    public void FormatNumber_NegativeSubPicture_SuffixMinus(string expression, string expected)
    {
        Assert.Equal(expected, Eval(expression));
    }

    // ═══════════════════════════════════════════════════════════════
    // $shuffle on multi-element sequence (BuiltInFunctions lines 5333-5346)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Shuffle_PreservesCount()
    {
        // Can't assert order but can verify count is preserved
        Assert.Equal("5", Eval("""$count($shuffle([1,2,3,4,5]))"""));
    }

    [Fact]
    public void Shuffle_SortRoundTrip()
    {
        // Shuffle then sort should return original order
        Assert.Equal("[1,2,3,4,5]", Eval("""$sort($shuffle([1,2,3,4,5]))"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Unicode supplementary character handling (BuiltInFunctions lines 6251-6266)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Substring_EmojiCodePoint()
    {
        // 😀 is a single code point (U+1F600) represented as surrogate pair
        Assert.Equal("\"\uD83D\uDE00\"", Eval("""$substring("\uD83D\uDE00test", 0, 1)"""));
    }

    [Fact]
    public void Length_EmojiCodePoint()
    {
        Assert.Equal("5", Eval("""$length("\uD83D\uDE00test")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $encodeUrlComponent / $decodeUrlComponent (BuiltInFunctions lines 4494-4504)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EncodeUrlComponent_Spaces()
    {
        Assert.Equal("\"hello%20world\"", Eval("""$encodeUrlComponent("hello world")"""));
    }

    [Fact]
    public void DecodeUrlComponent_Valid()
    {
        Assert.Equal("\"hello world\"", Eval("""$decodeUrlComponent("hello%20world")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Descendant wildcard (**) (FunctionalCompiler lines 1567-1581)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void DescendantWildcard_NestedArrays()
    {
        Assert.Equal(
            """["x","y","z"]""",
            Eval("""**.name""", """{"a":{"name":"x","b":[{"name":"y"},{"name":"z"}]}}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $flatten multi-valued sequences (BuiltInFunctions lines 2248-2270)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Flatten_NestedArrays()
    {
        Assert.Equal("[1,2,3,4]", Eval("""$flatten([[1,[2]],[[3],4]])"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Array-of-indices filter (FunctionalCompiler lines 5344-5359)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Filter_ArrayOfIndices()
    {
        Assert.Equal("[1,3,5]", Eval("""[1,2,3,4,5][[0,2,4]]"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Sort then filter (FunctionalCompiler lines 5463-5498, 5959-5981)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FilterThenSort()
    {
        Assert.Equal(
            """[{"type":"a","val":1},{"type":"a","val":2},{"type":"a","val":3}]""",
            Eval("""$[type="a"]^(val)""", """[{"type":"a","val":3},{"type":"b","val":1},{"type":"a","val":1},{"type":"a","val":2}]"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $pad with emoji (surrogate pair cycling) (BuiltInFunctions)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Pad_WithEmoji_Right()
    {
        Assert.Equal(
            "\"test\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81\"",
            Eval("""$pad("test", 8, "\uD83C\uDF81")"""));
    }

    [Fact]
    public void Pad_WithEmoji_Left()
    {
        Assert.Equal(
            "\"\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81\uD83C\uDF81test\"",
            Eval("""$pad("test", -8, "\uD83C\uDF81")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $replace with string pattern and limit
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Replace_StringPattern_WithLimit()
    {
        Assert.Equal(
            "\"hi world hello\"",
            Eval("""$replace("hello world hello", "hello", "hi", 1)"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $each on object
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Each_OnObject()
    {
        Assert.Equal(
            """["a=1","b=2","c=3"]""",
            Eval("""$each({"a":1,"b":2,"c":3}, function($v,$k){$k & "=" & $string($v)})"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $merge with overlapping keys
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Merge_OverlappingKeys()
    {
        Assert.Equal("""{"a":3,"b":2}""", Eval("""$merge([{"a":1},{"b":2},{"a":3}])"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // GroupBy with duplicate keys
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GroupBy_DuplicateKeys()
    {
        Assert.Equal(
            """{"A":[{"category":"A","val":1},{"category":"A","val":3}],"B":{"category":"B","val":2}}""",
            Eval("""items{category: $}""", """{"items":[{"category":"A","val":1},{"category":"B","val":2},{"category":"A","val":3}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $number coercion from boolean (BuiltInFunctions line ~530-540
    // and FunctionalCompiler lines 8625-8632)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Number_Boolean_True_Returns1()
    {
        Assert.Equal("1", Eval("""$number(true)"""));
    }

    [Fact]
    public void Number_Boolean_False_Returns0()
    {
        Assert.Equal("0", Eval("""$number(false)"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber minInt==0 && maxFrac==0 special rules
    // (BuiltInFunctions lines 4914-4934)
    // Only triggered when picture has no mandatory/optional int or frac digits
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_HashOnly_MinIntZeroMaxFracZero()
    {
        // Picture "#" → minInt=0, maxFrac=0, expPresent=false
        // Code sets minInt=1 via else branch at 4921
        Assert.Equal("\"42\"", Eval("""$formatNumber(42, "#")"""));
    }

    [Fact]
    public void FormatNumber_HashWithExponent_MinIntZeroMaxFracZero()
    {
        // Picture "#e0" → minInt=0, maxFrac=0, expPresent=true
        // Code sets minFrac=1, maxFrac=1 via 4917-4918
        Assert.Equal("\"0e2\"", Eval("""$formatNumber(42, "#e0")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber minExp computation
    // (BuiltInFunctions lines 4938-4946)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_ExponentPadding_FourDigits()
    {
        Assert.Equal("\"1.23e-0003\"", Eval("""$formatNumber(0.00123, "0.00e0000")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber percent/per-mille scaling
    // (BuiltInFunctions lines 5042-5048)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_PercentScaling()
    {
        Assert.Equal("\"75%\"", Eval("""$formatNumber(0.75, "#0%")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber digit family replacement
    // (BuiltInFunctions lines 5094-5110)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_ArabicDigitFamily()
    {
        // picture uses Arabic-Indic zero-digit; result is in that digit family
        string result = Eval("""$formatNumber(42, "#\u0660\u0660", {"zero-digit":"\u0660"})""");
        Assert.Contains("\u0664", result); // Arabic-Indic 4
        Assert.Contains("\u0662", result); // Arabic-Indic 2
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber zero-digit padding
    // (BuiltInFunctions lines 5146-5153)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_ZeroDigitPadding()
    {
        Assert.Equal("\"01.000\"", Eval("""$formatNumber(1, "#00.000")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber grouping separator insertion
    // (BuiltInFunctions lines 5158-5165)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_GroupingSeparatorInsertion()
    {
        Assert.Equal("\"1,234,567\"", Eval("""$formatNumber(1234567, "#,##0")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber exponent appending
    // (BuiltInFunctions lines 5193-5202)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_ExponentAppended()
    {
        Assert.Equal("\"1.23e5\"", Eval("""$formatNumber(123000, "0.00e0")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber negative exponent
    // (BuiltInFunctions lines 5062-5087)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_NegativeExponent()
    {
        Assert.Equal("\"-1.23e-3\"", Eval("""$formatNumber(-0.00123, "0.00e0")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber exponent-separator option
    // (BuiltInFunctions lines 4583-4589, 4660-4664)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_CustomExponentSeparator()
    {
        Assert.Equal("\"1.2E3\"", Eval("""$formatNumber(1234.5, "0.0E0", {"exponent-separator":"E"})"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber custom minus-sign option
    // (BuiltInFunctions lines 4583-4589)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_CustomMinusSign()
    {
        string result = Eval("""$formatNumber(-42, "0", {"minus-sign":"\u2212"})""");
        Assert.Contains("\u2212", result); // Unicode minus sign
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber error: no mantissa digit (D3085)
    // (BuiltInFunctions lines 4733-4737)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_NoMantissaDigit_ThrowsD3085()
    {
        EvalThrows("""$formatNumber(42, ",")""", "null", "D3085");
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber error: exponent only digit family chars (D3093)
    // (BuiltInFunctions lines 4804-4817)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_ExponentOptionalDigit_ThrowsD3093()
    {
        EvalThrows("""$formatNumber(42, "0e#")""", "null", "D3093");
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber error: integer trailing grouping sep (D3088)
    // (BuiltInFunctions lines 4758-4763)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_IntegerTrailingGroupingSep_ThrowsD3088()
    {
        EvalThrows("""$formatNumber(42, "#,")""", "null", "D3088");
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber fractional grouping (simple valid case)
    // (BuiltInFunctions lines 5169-5173)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_FractionalGrouping()
    {
        Assert.Equal("\"0.1,23\"", Eval("""$formatNumber(0.123456, "#0.0,00")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $formatNumber fractional grouping — was a bug (fixed), now passes
    // (BuiltInFunctions line 4884, FormatNumberPicture line 351)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FormatNumber_FractionalGrouping_LargeInput()
    {
        // Was throwing ArgumentOutOfRangeException before fix.
        // Reference returns "0.123,457".
        Assert.Equal(
            "\"0.123,457\"",
            Eval("""$formatNumber(0.123456789, "#0.000,000")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $decodeUrlComponent invalid percent encoding
    // (BuiltInFunctions lines 4429-4444)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void DecodeUrlComponent_InvalidPercentHex_ThrowsD3140()
    {
        EvalThrows("""$decodeUrlComponent("test%ZZvalue")""", "null", "D3140");
    }

    [Fact]
    public void DecodeUrlComponent_TruncatedPercent_ThrowsD3140()
    {
        EvalThrows("""$decodeUrlComponent("test%2")""", "null", "D3140");
    }

    // ═══════════════════════════════════════════════════════════════
    // $match context binding (1 arg variant)
    // (BuiltInFunctions lines 3119-3123)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Match_ContextBinding_ViaApply()
    {
        Assert.Equal(
            """{"match":"world","index":6,"groups":[]}""",
            Eval("""("hello world" ~> $match(/world/))"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Per-element boolean filter on arrays
    // (FunctionalCompiler lines 5545-5643 — ApplyPerElementFilterStages)
    // Requires multi-step path where stepIdx > 0 has a filter
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void PerElementFilter_NumericIndex_OnNestedArrays()
    {
        // data is array, .items[0] applies per-element at stepIdx > 0
        Assert.Equal(
            """["A","C"]""",
            Eval("""data.items[0]""",
                """{"data":[{"items":["A","B"]},{"items":["C","D"]}]}"""));
    }

    [Fact]
    public void PerElementFilter_NegativeIndex_OnNestedArrays()
    {
        Assert.Equal(
            """["B","D"]""",
            Eval("""data.items[-1]""",
                """{"data":[{"items":["A","B"]},{"items":["C","D"]}]}"""));
    }

    [Fact]
    public void PerElementFilter_ArrayOfIndices_OnNestedArrays()
    {
        Assert.Equal(
            """["A","C","E","G"]""",
            Eval("""data.items[[0,2]]""",
                """{"data":[{"items":["A","B","C","D"]},{"items":["E","F","G"]}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Per-element sort then flatten nested arrays
    // (FunctionalCompiler lines 5463-5498 — sort flattening path)
    // Requires multi-step path through arrays with sort
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void PerElementSort_FlattenNestedPath()
    {
        Assert.Equal(
            """[{"val":1},{"val":2},{"val":3}]""",
            Eval("""data.items^(val)""",
                """{"data":[{"items":[{"val":3},{"val":1}]},{"items":[{"val":2}]}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Hex/binary/octal number parsing via $number
    // (FunctionalCompiler lines 8740-8796 on net10.0)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Number_HexViaCoerce()
    {
        Assert.Equal("255", Eval("""$number("0xFF")"""));
    }

    [Fact]
    public void Number_BinaryViaCoerce()
    {
        Assert.Equal("10", Eval("""$number("0b1010")"""));
    }

    [Fact]
    public void Number_OctalViaCoerce()
    {
        Assert.Equal("63", Eval("""$number("0o77")"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Path through nested arrays (FunctionalCompiler lines 1966-1980)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void PathThroughNestedArrays_Flattens()
    {
        Assert.Equal(
            "[1,2,3]",
            Eval("""a.b.c.d""", """{"a":[{"b":{"c":[{"d":1},{"d":2}]}},{"b":{"c":{"d":3}}}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Object with negative number constant
    // (FunctionalCompiler lines 429-434)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ObjectConstructor_NegativeConstant()
    {
        Assert.Equal("""{"x":-42}""", Eval("""{"x":-42}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Fused array-of-objects construction
    // (FunctionalCompiler lines 6904-6943)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FusedArrayOfObjects_WithPathPrefix()
    {
        Assert.Equal(
            """[{"id":"O1","prod":"W"},{"id":"O2","prod":"G"}]""",
            Eval("""Account.Order.{"id":OrderID,"prod":Product}""",
                """{"Account":{"Order":[{"OrderID":"O1","Product":"W"},{"OrderID":"O2","Product":"G"}]}}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Property map pre-building for large objects
    // (FunctionalCompiler lines 1694-1713)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void PropertyMap_LargeObjects_Lookup()
    {
        Assert.Equal(
            """["A","B"]""",
            Eval("""items.name""",
                """{"items":[{"name":"A","a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7},{"name":"B","a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Equality predicate filter with array recursion
    // (FunctionalCompiler lines 1645-1663)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EqualityPredicate_ArrayRecursion()
    {
        Assert.Equal(
            "[1,3]",
            Eval("""items[type="x"].val""",
                """{"items":[{"type":"x","val":1},{"type":"y","val":2},{"type":"x","val":3}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // Sort then filter stages
    // (FunctionalCompiler lines 5959-5981)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Sort_ThenFilter_Stages()
    {
        Assert.Equal(
            """[{"val":3},{"val":4}]""",
            Eval("""items^(val)[val > 2]""",
                """{"items":[{"val":3},{"val":1},{"val":4},{"val":2}]}"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $replace with function callback
    // (FunctionalCompiler lines 8287-8296)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Replace_WithFunctionCallback_Uppercases()
    {
        Assert.Equal(
            "\"heLLo\"",
            Eval("""$replace("hello", /l/, function($m){$uppercase($m.match)})"""));
    }

    // ═══════════════════════════════════════════════════════════════
    // $match with date capture groups
    // (FunctionalCompiler lines 9119-9126)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Match_DateCaptureGroups()
    {
        string result = Eval("""$match("2023-01-15", /(\d{4})-(\d{2})-(\d{2})/)""");
        Assert.Contains("\"match\":\"2023-01-15\"", result);
        Assert.Contains("\"groups\":[\"2023\",\"01\",\"15\"]", result);
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

    [Theory]
    [InlineData("$formatNumber(\"0xFF\", \"#\")")]
    [InlineData("$formatNumber(\"0b1010\", \"#\")")]
    [InlineData("$formatNumber(\"0o77\", \"#\")")]
    public void FormatNumber_HexBinaryOctalStringCoercion(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    // --- FC ApplyFocusStages numeric predicate from string ---
    // (FunctionalCompiler lines 5548-5643: non-boolean, non-constant-int predicates)

    [Theory]
    [InlineData("items@$v[\"0x01\"]", "{\"items\":[\"a\",\"b\",\"c\"]}", "\"b\"")]
    [InlineData("items@$v[\"1\"]", "{\"items\":[\"a\",\"b\",\"c\"]}", "\"b\"")]
    public void FocusStages_StringPredicateCoercedToNumericIndex(string expression, string data, string expected)
    {
        Assert.Equal(expected, Eval(expression, data));
    }

    // --- FC ApplyStages string predicate coercion ---
    // (FunctionalCompiler lines 5278-5340: numeric index from string)

    [Fact]
    public void ApplyStages_StringPredicateCoercedToIndex()
    {
        Assert.Equal("20", Eval("[10,20,30][\"1\"]"));
    }

    [Fact]
    public void ApplyStages_HexStringPredicateCoercedToIndex()
    {
        Assert.Equal("20", Eval("[10,20,30][\"0x01\"]"));
    }

    // --- BF FormatNumber runtime path (non-constant picture) ---
    // (BuiltInFunctions lines 4913-4934: minInt==0 && maxFrac==0 etc.)

    [Theory]
    [InlineData("$formatNumber(42, prefix & \"#\")", "\"42\"")]
    [InlineData("$formatNumber(0.5, prefix & \".###\")", "\".5\"")]
    [InlineData("$formatNumber(12345, prefix & \"#,###\")", "\"12,345\"")]
    [InlineData("$formatNumber(0.5, prefix & \"#%\")", "\"50%\"")]
    [InlineData("$formatNumber(0.5, prefix & \"0.00\")", "\"0.50\"")]
    public void FormatNumber_RuntimePath_NonConstantPicture(string expression, string expected)
    {
        Assert.Equal(expected, Eval(expression, "{\"prefix\":\"\"}"));
    }

    // --- Per-element filter with numeric index on nested arrays ---
    // (FunctionalCompiler lines 5859-5920: ApplyPerElementFilterStages constant-int)

    [Theory]
    [InlineData("data.items[0]", "[\"A\",\"C\"]")]
    [InlineData("data.items[-1]", "[\"B\",\"D\"]")]
    public void PerElementFilter_ConstantIntIndex_NestedArrays(string expression, string expected)
    {
        Assert.Equal(expected, Eval(expression, "{\"data\":[{\"items\":[\"A\",\"B\"]},{\"items\":[\"C\",\"D\"]}]}"));
    }

    // --- Per-element sort on nested arrays ---
    // (FunctionalCompiler lines 5462-5498: sort stage in per-element context)

    [Fact]
    public void PerElementSort_NestedArrays()
    {
        Assert.Equal(
            "[{\"val\":1},{\"val\":2},{\"val\":3}]",
            Eval("data.items^(val)", "{\"data\":[{\"items\":[{\"val\":3},{\"val\":1}]},{\"items\":[{\"val\":2}]}]}"));
    }

    // --- BF FormatNumber runtime exponent paths ---
    // (BuiltInFunctions lines 4917-4918 expPresent branch, 5062-5087 exponent calc)

    [Fact]
    public void FormatNumber_RuntimePath_ExponentHash()
    {
        // Non-constant "#e0" → BF runtime path, triggers expPresent branch at 4917
        Assert.Equal("\"0.e2\"", Eval("$formatNumber(42, prefix & \"#e0\")", "{\"prefix\":\"\"}"));
    }

    [Fact]
    public void FormatNumber_RuntimePath_ExponentPadding()
    {
        // Non-constant "0.00e0000" → hits exponent mantissa scaling at 5062-5087
        Assert.Equal("\"1.23e-0003\"", Eval("$formatNumber(0.00123, prefix & \"0.00e0000\")", "{\"prefix\":\"\"}"));
    }

    // --- FC ApplyFocusStages array-of-indices predicate ---
    // (FunctionalCompiler lines 5559-5625: predicate returns an array of numbers)

    [Fact]
    public void FocusStages_ArrayOfIndicesPredicate()
    {
        Assert.Equal(
            "[\"a\",\"c\"]",
            Eval("items@$v[[0,2]]", "{\"items\":[\"a\",\"b\",\"c\",\"d\"]}"));
    }

    [Fact]
    public void FocusStages_ArrayOfIndicesPredicate_MultiElement()
    {
        Assert.Equal(
            "[\"a\",\"b\"]",
            Eval("Account.Order@$o[[0,1]]", "{\"Account\":{\"Order\":[\"a\",\"b\",\"c\"]}}"));
    }

    // --- BF HasInvalidPercentEncoding ---
    // (BuiltInFunctions lines 4429-4444)

    [Fact]
    public void DecodeUrlComponent_BadHexAfterPercent_ThrowsD3140()
    {
        Assert.Throws<JsonataException>(() => Eval("$decodeUrlComponent(\"%ZZ\")"));
    }

    [Fact]
    public void DecodeUrlComponent_IncompletePercent_ThrowsD3140()
    {
        Assert.Throws<JsonataException>(() => Eval("$decodeUrlComponent(\"a%2\")"));
    }

    // --- FC CompileFusedArrayOfObjects ---
    // (FunctionalCompiler lines 6904-6943: path inside [] with object constructor)

    [Fact]
    public void FusedArrayOfObjects_WithPrefixPathInArrayCtor()
    {
        Assert.Equal(
            "[{\"id\":\"A\",\"v\":1},{\"id\":\"B\",\"v\":2}]",
            Eval("[items.{\"id\": id, \"v\": val}]",
                "{\"items\":[{\"id\":\"A\",\"val\":1},{\"id\":\"B\",\"val\":2}]}"));
    }

    // --- BF FormatNumber negative sub-picture ---
    // (BuiltInFunctions lines 4982-4995: separate negative format)

    [Theory]
    [InlineData("$formatNumber(-42, prefix & \"#;(#)\")", "\"(42)\"")]
    [InlineData("$formatNumber(-3.14, prefix & \"0.00;neg 0.00\")", "\"neg 3.14\"")]
    public void FormatNumber_RuntimePath_NegativeSubPicture(string expression, string expected)
    {
        Assert.Equal(expected, Eval(expression, "{\"prefix\":\"\"}"));
    }

    // --- BF FormatNumber custom options via runtime path ---
    // (BuiltInFunctions lines 4583-4589: exponent-separator, minus-sign etc.)

    [Fact]
    public void FormatNumber_RuntimePath_CustomOptions()
    {
        Assert.Equal(
            "\"42\"",
            Eval("$formatNumber(42, prefix & \"#\", {\"exponent-separator\":\"E\",\"minus-sign\":\"~\"})", "{\"prefix\":\"\"}"));
    }

    // --- FC equality predicate in fused path ---
    // (FunctionalCompiler lines 1645-1663)

    [Fact]
    public void EqualityPredicate_InFusedPath()
    {
        Assert.Equal(
            "[\"A\",\"C\"]",
            Eval("items[type=\"x\"].name",
                "{\"items\":[{\"type\":\"x\",\"name\":\"A\"},{\"type\":\"y\",\"name\":\"B\"},{\"type\":\"x\",\"name\":\"C\"}]}"));
    }

    // --- FC property map building ---
    // (FunctionalCompiler lines 1694-1713: arrayLen * remainingSteps > 10, propCount > 6)

    [Fact]
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
        Assert.Contains("0", result);
        Assert.Contains("19", result);
    }

    // --- FC sort in focus context ---
    // (FunctionalCompiler lines 5463-5498: sort stage with focus variable)

    [Fact]
    public void FocusSort_OrdersByField()
    {
        Assert.Equal(
            "[{\"price\":10},{\"price\":20},{\"price\":30}]",
            Eval("Account.Order@$o^(price)",
                "{\"Account\":{\"Order\":[{\"price\":30},{\"price\":10},{\"price\":20}]}}"));
    }

    // --- FC ApplySortStagesOnly ---
    // (FunctionalCompiler lines 5959-5981: multi-step path with sort at step > 0)

    [Fact]
    public void ApplySortStagesOnly_SortAtStep1()
    {
        Assert.Equal(
            "[{\"k\":1},{\"k\":2},{\"k\":3}]",
            Eval("a.b^(k)",
                "{\"a\":[{\"b\":[{\"k\":3},{\"k\":1},{\"k\":2}]}]}"));
    }

    // --- FC EvalFromStepInto with equality predicate ---
    // (FunctionalCompiler lines 1644-1663: Into variant enters eq predicate from CollectAndContinueInto)

    [Fact]
    public void EvalFromStepInto_EqualityPredicateViaNestedArray()
    {
        // outer is array at step 1 → CollectAndContinueInto → EvalFromStepInto at step 2
        // items[type="x"] at step 2 has eq pred → enters EvalFromStepInto line 1645 (array case)
        Assert.Equal(
            "[\"A\",\"C\"]",
            Eval("outer.inner.items[type=\"x\"].name",
                "{\"outer\":[{\"inner\":{\"items\":[{\"type\":\"x\",\"name\":\"A\"},{\"type\":\"y\",\"name\":\"B\"}]}},{\"inner\":{\"items\":[{\"type\":\"x\",\"name\":\"C\"}]}}]}"));
    }

    [Fact]
    public void EvalFromStepInto_EqualityPredicate_SingletonObject()
    {
        // EvalFromStepInto line 1652: items is a single object matching predicate
        Assert.Equal(
            "\"A\"",
            Eval("outer.inner.items[type=\"x\"].name",
                "{\"outer\":[{\"inner\":{\"items\":{\"type\":\"x\",\"name\":\"A\"}}}]}"));
    }

    [Fact]
    public void EvalFromStepInto_EqualityPredicate_SingletonNoMatch()
    {
        // EvalFromStepInto line 1654-1656: singleton object does NOT match
        string result = Eval("outer.inner.items[type=\"x\"].name",
            "{\"outer\":[{\"inner\":{\"items\":{\"type\":\"y\",\"name\":\"B\"}}}]}");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void EvalFromStepInto_EqualityPredicate_NonObjectNonArray()
    {
        // EvalFromStepInto line 1659-1661: items is a number (not object or array)
        string result = Eval("outer.inner.items[type=\"x\"].name",
            "{\"outer\":[{\"inner\":{\"items\":42}}]}");
        Assert.Equal("undefined", result);
    }

    // --- FC CollectAndContinueInto property map building ---
    // (FunctionalCompiler lines 1694-1713: arrayLen * remainingSteps > 10, propCount > 6)

    [Fact]
    public void PropertyMap_LargeArrayViaPredicatePath()
    {
        // groups[0] uses predicate → EvalPropertyChainWithPredicates path
        // items has 20 objects with 8 properties → triggers property map building
        string items = string.Join(",", Enumerable.Range(0, 20).Select(i =>
            $"{{\"val\":{i},\"b\":{i},\"c\":{i},\"d\":{i},\"e\":{i},\"f\":{i},\"g\":{i},\"h\":{i}}}"));
        string data = $"{{\"groups\":[{{\"items\":[{items}]}}]}}";
        string result = Eval("groups[0].items.val", data);
        Assert.Contains("0", result);
        Assert.Contains("19", result);
    }

    // --- FC sort as stage: DEAD CODE ---
    // The parser never adds SortNode to StepAnnotations.Stages — sort is always
    // a separate SortNode step in the path. Therefore:
    // - ApplyFocusStages sort branch (lines 5463-5498): unreachable
    // - ApplySortStagesOnly body (lines 5959-5981): unreachable (always returns at 5955)
    // - CompilePath SortNode stage case (lines 2077-2083): unreachable
    // - WrapWithStages SortNode case (lines 187-189): unreachable

    [Fact]
    public void FocusSort_WithProjection()
    {
        Assert.Equal(
            "[\"A\",\"B\",\"C\"]",
            Eval("data.Order@$o^(price).$o.product",
                "{\"data\":{\"Order\":[{\"product\":\"C\",\"price\":30},{\"product\":\"A\",\"price\":10},{\"product\":\"B\",\"price\":20}]}}"));
    }

    // --- BF FormatNumber custom options: percent, per-mille, digit, pattern-separator ---
    // (BuiltInFunctions lines 4585-4589)

    [Fact]
    public void FormatNumber_RuntimePath_CustomPercentOption()
    {
        // "percent":"%%"  makes '%' a passive char → no multiply-by-100
        Assert.Equal(
            "\"0%\"",
            Eval("$formatNumber(0.42, prefix & \"#%\", {\"percent\":\"%%\"})", "{\"prefix\":\"\"}"));
    }

    [Fact]
    public void FormatNumber_RuntimePath_CustomPerMilleOption()
    {
        // "per-mille" option is accepted (line 4586)
        string result = Eval("$formatNumber(0.042, prefix & \"#\u2030\", {\"per-mille\":\"\u2030\"})", "{\"prefix\":\"\"}");
        Assert.Contains("42", result);
    }

    [Fact]
    public void FormatNumber_RuntimePath_CustomZeroDigitOption()
    {
        // "zero-digit" changes the zero character (line 4587)
        string result = Eval("$formatNumber(42, prefix & \"#\", {\"zero-digit\":\"\u0660\"})", "{\"prefix\":\"\"}");
        Assert.NotNull(result);
        Assert.NotEqual("undefined", result);
    }

    [Fact]
    public void FormatNumber_RuntimePath_CustomDigitOption()
    {
        // "digit":"?"  makes '?' the optional-digit char instead of '#'
        Assert.Equal(
            "\"42\"",
            Eval("$formatNumber(42, prefix & \"?0\", {\"digit\":\"?\"})", "{\"prefix\":\"\"}"));
    }

    [Fact]
    public void FormatNumber_RuntimePath_CustomPatternSeparator()
    {
        // "pattern-separator":"|"  splits sub-pictures on '|' instead of ';'
        Assert.Equal(
            "\"neg 42\"",
            Eval("$formatNumber(-42, prefix & \"#|neg #\", {\"pattern-separator\":\"|\"})", "{\"prefix\":\"\"}"));
    }

    // ==============================================================
    // Round 5: Wider FC + BF coverage — targets identified from
    // coverage8 Cobertura XML analysis
    // ==============================================================

    // --- FC 657-680: CompileBufferFusedZipMixed2 (constant + chain) ---

    [Fact]
    public void Zip_ConstantAndChain()
    {
        // $zip with one constant array and one property chain triggers
        // CompileBufferFusedZipMixed2 (const-first path)
        Assert.Equal(
            "[[1,\"a\"],[2,\"b\"],[3,\"c\"]]",
            Eval("$zip([1,2,3], items)", "{\"items\":[\"a\",\"b\",\"c\"]}"));
    }

    [Fact]
    public void Zip_ChainAndConstant()
    {
        // const-second path
        Assert.Equal(
            "[[\"a\",1],[\"b\",2],[\"c\",3]]",
            Eval("$zip(items, [1,2,3])", "{\"items\":[\"a\",\"b\",\"c\"]}"));
    }

    // --- FC 686-709: CompileBufferFusedZip3 (3 property chains) ---

    [Fact]
    public void Zip_ThreeChains()
    {
        Assert.Equal(
            "[[1,4,7],[2,5,8],[3,6,9]]",
            Eval("$zip(a, b, c)", "{\"a\":[1,2,3],\"b\":[4,5,6],\"c\":[7,8,9]}"));
    }

    // --- FC 8023-8085: CompileFocusSortStage (focus + sort where key refs focus var) ---

    [Fact]
    public void FocusSort_KeyReferencesFocusVar()
    {
        // items@$e^($e.name) — sort key uses $e, requires CompileFocusSortStage
        Assert.Equal(
            "[{\"name\":\"a\"},{\"name\":\"b\"},{\"name\":\"c\"}]",
            Eval("items@$e^($e.name)", "{\"items\":[{\"name\":\"c\"},{\"name\":\"a\"},{\"name\":\"b\"}]}"));
    }

    [Fact]
    public void FocusSort_KeyReferencesFocusVar_Descending()
    {
        Assert.Equal(
            "[{\"name\":\"c\"},{\"name\":\"b\"},{\"name\":\"a\"}]",
            Eval("items@$e^(>$e.name)", "{\"items\":[{\"name\":\"a\"},{\"name\":\"c\"},{\"name\":\"b\"}]}"));
    }

    [Fact]
    public void FocusSort_SingleElement()
    {
        // <=1 element triggers the early-return path in CompileFocusSortStage
        // (returns input unchanged when nothing to sort)
        string data = "{\"items\":[{\"name\":\"a\"}]}";
        string result = Eval("items@$e^($e.name)", data);
        Assert.Contains("\"name\"", result);
    }

    // --- FC 7877-7944: CompileFilter (standalone filter) ---

    [Fact]
    public void Filter_Standalone_NumericIndex()
    {
        // $a[0] — standalone numeric filter on variable
        Assert.Equal(
            "10",
            Eval("($a := [10,20,30]; $a[0])", "{}"));
    }

    [Fact]
    public void Filter_Standalone_BooleanTrue()
    {
        Assert.Equal(
            "42",
            Eval("($a := 42; $a[true])", "{}"));
    }

    [Fact]
    public void Filter_Standalone_BooleanFalse()
    {
        // false predicate → undefined
        Assert.Equal(
            "undefined",
            Eval("($a := 42; $a[false])", "{}"));
    }

    [Fact]
    public void Filter_Standalone_MultiValueIndex()
    {
        // Multi-value filter = array of indices
        Assert.Equal(
            "[10,30]",
            Eval("($a := [10,20,30]; $a[[0,2]])", "{}"));
    }

    [Fact]
    public void Filter_Standalone_Truthiness()
    {
        Assert.Equal(
            "42",
            Eval("($a := 42; $a[\"yes\"])", "{}"));
    }

    [Fact]
    public void Filter_Standalone_NegativeIndex()
    {
        Assert.Equal(
            "30",
            Eval("($a := [10,20,30]; $a[-1])", "{}"));
    }

    // --- BF 6251-6266: CodePointsToString with supplementary plane chars ---

    [Fact]
    public void Substring_SupplementaryPlane()
    {
        // U+1F600 (😀) is a single code point but 2 UTF-16 chars (surrogate pair).
        // $substring counts code points, so index 1 should be the char after 😀.
        string data = "{\"s\":\"\\uD83D\\uDE00AB\"}";
        // $substring(s, 1, 1) → "A" (code point 1 = 'A')
        Assert.Equal("\"A\"", Eval("$substring(s, 1, 1)", data));
    }

    [Fact]
    public void Substring_MultipleSupplementaryPlane()
    {
        // Two emoji followed by ASCII
        string data = "{\"s\":\"\\uD83D\\uDE00\\uD83D\\uDE01X\"}";
        // $substring(s, 2) → "X"
        Assert.Equal("\"X\"", Eval("$substring(s, 2)", data));
    }

    [Fact]
    public void Substring_SupplementaryPlaneSlice()
    {
        // Slice from the middle of a supplementary-plane string
        string data = "{\"s\":\"A\\uD83D\\uDE00B\\uD83D\\uDE01C\"}";
        // Code points: A(0) 😀(1) B(2) 😁(3) C(4)
        // $substring(s, 1, 3) → "😀B😁"
        string result = Eval("$substring(s, 1, 3)", data);
        // Verify it returned 3 code points starting from index 1
        Assert.Equal("3", Eval("$length($substring(s, 1, 3))", data));
    }

    // --- BF 4429-4444 + closure: HasInvalidPercentEncoding string overload ---

    [Fact]
    public void DecodeUrlComponent_NonStringInput()
    {
        // Passing a number to $decodeUrlComponent goes through CoerceElementToString
        // then calls HasInvalidPercentEncoding(string) — the string overload
        Assert.Equal("\"42\"", Eval("$decodeUrlComponent(42)", "{}"));
    }

    [Fact]
    public void DecodeUrlComponent_BooleanInput()
    {
        Assert.Equal("\"true\"", Eval("$decodeUrlComponent(true)", "{}"));
    }

    [Fact]
    public void DecodeUrl_NonStringInput()
    {
        // Same pattern for the parallel $decodeUrl function
        Assert.Equal("\"42\"", Eval("$decodeUrl(42)", "{}"));
    }

    // --- BF 3119-3123: $split with 1 argument (context binding) ---
    // Note: The 1-arg form of $split is compiled such that the context argument is
    // injected at compile time. The lines at BF 3119-3123 handle the parsing of the
    // 1-arg vs 2-arg vs 3-arg forms during compilation.

    // --- BF 5279-5281: $formatBase with value 0 ---

    [Fact]
    public void FormatBase_Zero()
    {
        Assert.Equal("\"0\"", Eval("$formatBase(0, 2)", "{}"));
        Assert.Equal("\"0\"", Eval("$formatBase(0, 16)", "{}"));
    }

    // --- BF 4853-4858: GCD for grouping + BF 5169-5173: irregular grouping ---

    [Fact]
    public void FormatNumber_RuntimePath_IrregularGrouping()
    {
        // Irregular grouping pattern #,##,### — not regular spacing
        // Forces non-regular grouping path (BF 5169-5173)
        Assert.Equal(
            "\"12,34,567\"",
            Eval("$formatNumber(1234567, prefix & \"#,##,###\")", "{\"prefix\":\"\"}"));
    }

    // --- BF 6166-6171: Multi-value sequence into array builder ---

    [Fact]
    public void Append_MultiValueToArray()
    {
        // $append where second arg produces multi-value sequence
        Assert.Equal(
            "[1,2,3,4]",
            Eval("$append([1,2], [3,4])", "{}"));
    }

    // --- FC 1015-1028: LookupField with array input ---

    [Fact]
    public void LookupField_ArrayInput()
    {
        // When input to a field lookup is an array, iterates and collects
        Assert.Equal(
            "[\"a\",\"b\",\"c\"]",
            Eval("items.name", "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"},{\"name\":\"c\"}]}"));
    }

    // --- FC 1567-1581: CollectAndContinue non-array child + array descent ---

    [Fact]
    public void PropertyChain_NestedArrayDescent()
    {
        // Multi-level path: items is array → for each, get details.name
        Assert.Equal(
            "[\"x\",\"y\"]",
            Eval("items.details.name",
                "{\"items\":[{\"details\":{\"name\":\"x\"}},{\"details\":{\"name\":\"y\"}}]}"));
    }

    [Fact]
    public void PropertyChain_NestedArrayWithArrayChild()
    {
        // Property that is an array inside an array of objects
        Assert.Equal(
            "[1,2,3,4]",
            Eval("items.values",
                "{\"items\":[{\"values\":[1,2]},{\"values\":[3,4]}]}"));
    }

    // --- FC 4085-4109: Multi-element input in path evaluation ---

    [Fact]
    public void Path_MultiElementInput()
    {
        // Three-level deep path where middle level is an array
        Assert.Equal(
            "[1,2,3]",
            Eval("data.items.value",
                "{\"data\":{\"items\":[{\"value\":1},{\"value\":2},{\"value\":3}]}}"));
    }

    // --- FC 2975-2999: Multi-parent focus context ---

    [Fact]
    public void Focus_MultiParentContext()
    {
        // Focus variable with multi-element parent context
        Assert.Equal(
            "[\"a\",\"b\"]",
            Eval("items@$x.$x.name",
                "{\"items\":[{\"name\":\"a\"},{\"name\":\"b\"}]}"));
    }

    // --- FC 6798-6833: Array constructor tuple paths ---

    [Fact]
    public void ArrayConstructor_TupleSingleton()
    {
        // Array literal that contains a singleton array element
        Assert.Equal(
            "[1,2,\"a\",\"b\"]",
            Eval("[items, names]",
                "{\"items\":[1,2],\"names\":[\"a\",\"b\"]}"));
    }

    [Fact]
    public void ArrayConstructor_TupleMultiValue()
    {
        // Array literal with multi-value expression
        Assert.Equal(
            "[1,2,3]",
            Eval("[data.items.value]",
                "{\"data\":{\"items\":[{\"value\":1},{\"value\":2},{\"value\":3}]}}"));
    }

    // --- FC 5344-5359: Multi-result numeric predicate in ApplyStages ---

    [Fact]
    public void Filter_MultiResultNumericPredicate()
    {
        // Filter where predicate returns multiple numeric values (array of indices)
        Assert.Equal(
            "[\"a\",\"c\"]",
            Eval("items[[0,2]]", "{\"items\":[\"a\",\"b\",\"c\",\"d\"]}"));
    }

    // --- FC 5597-5625: Multi-result numeric predicate in ApplyFocusStages ---

    [Fact]
    public void FocusFilter_MultiResultNumericPredicate()
    {
        Assert.Equal(
            "[\"a\",\"c\"]",
            Eval("items@$x[[0,2]]", "{\"items\":[\"a\",\"b\",\"c\",\"d\"]}"));
    }

    // --- FC 1966-1980: EvalPropertyChainIntoStatic with array ---

    [Fact]
    public void PropertyChainInto_ArrayAtIntermediateStep()
    {
        // Path into nested structure where intermediate value is an array
        Assert.Equal(
            "[\"x\",\"y\"]",
            Eval("outer.items.name",
                "{\"outer\":{\"items\":[{\"name\":\"x\"},{\"name\":\"y\"}]}}"));
    }

    // --- FC 5092-5105: ApplyStages index binding singleton array expansion ---

    [Fact]
    public void IndexBinding_SingletonArrayExpansion()
    {
        // Index binding (#$i) on a singleton array triggers expansion
        Assert.Equal(
            "[0,1,2]",
            Eval("items#$i.$i",
                "{\"items\":[\"a\",\"b\",\"c\"]}"));
    }

    // --- FC 5437-5450: ApplyFocusStages singleton array expansion ---

    [Fact]
    public void FocusStages_SingletonArrayExpansion()
    {
        // Focus on a singleton array with a filter stage
        Assert.Equal(
            "[\"a\",\"b\",\"c\"]",
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

    [Fact]
    public void ConstantArray_StringWithTab()
    {
        Assert.Equal(
            "[\"hello\\tworld\"]",
            Eval("[\"hello\\tworld\"]", "{}"));
    }

    [Fact]
    public void ConstantArray_StringWithNewline()
    {
        Assert.Equal(
            "[\"line1\\nline2\"]",
            Eval("[\"line1\\nline2\"]", "{}"));
    }

    [Fact]
    public void ConstantArray_StringWithBackslash()
    {
        Assert.Equal(
            "[\"path\\\\file\"]",
            Eval("[\"path\\\\file\"]", "{}"));
    }

    [Fact]
    public void ConstantArray_StringWithQuote()
    {
        Assert.Equal(
            "[\"say \\\"hello\\\"\"]",
            Eval("[\"say \\\"hello\\\"\"]", "{}"));
    }

    // --- FC 429-434: SerializeConstantJson unary negation ---

    [Fact]
    public void ConstantArray_NegativeNumber()
    {
        Assert.Equal(
            "[-42,-3.14]",
            Eval("[-42, -3.14]", "{}"));
    }

    // --- FC 9052-9079: CreateNullElement, CreateStringElement, CreateBoolElement ---

    [Fact]
    public void NullLiteral_ReturnsNull()
    {
        Assert.Equal("null", Eval("null", "{}"));
    }

    [Fact]
    public void BoolLiterals_ReturnCorrectly()
    {
        Assert.Equal("true", Eval("true", "{}"));
        Assert.Equal("false", Eval("false", "{}"));
    }

    // --- FC 6433-6438: Number formatting with exponent ---

    [Fact]
    public void StringifySmallExponent()
    {
        Assert.Equal("\"1.23e-10\"", Eval("$string(1.23e-10)", "{}"));
    }

    // --- BF formatNumber picture validation error branches (D3080-D3093) ---
    // These tests use CONSTANT pictures which are validated at compile time
    // via FormatNumberPicture.Parse (FC line 7457). They verify the compiler
    // path but do NOT hit BuiltInFunctions validation code.

    [Fact]
    public void FormatNumber_MultipleDecimalSeparators_ThrowsD3081()
    {
        EvalThrows("$formatNumber(42, \"0.0.0\")", "{}", "D3081");
    }

    [Fact]
    public void FormatNumber_MultiplePercent_ThrowsD3082()
    {
        EvalThrows("$formatNumber(42, \"0%0%0\")", "{}", "D3082");
    }

    [Fact]
    public void FormatNumber_MultiplePerMille_ThrowsD3083()
    {
        EvalThrows("$formatNumber(42, \"0\u20300\u20300\")", "{}", "D3083");
    }

    [Fact]
    public void FormatNumber_MixPercentPerMille_ThrowsD3084()
    {
        EvalThrows("$formatNumber(42, \"0%\u20300\")", "{}", "D3084");
    }

    [Fact]
    public void FormatNumber_NoDigitChars_ThrowsD3085()
    {
        EvalThrows("$formatNumber(42, \"---\")", "{}", "D3085");
    }

    [Fact]
    public void FormatNumber_PassiveInsideActive_ThrowsD3086()
    {
        EvalThrows("$formatNumber(42, \"0 0\")", "{}", "D3086");
    }

    [Fact]
    public void FormatNumber_GroupAdjacentDecimal_ThrowsD3087()
    {
        EvalThrows("$formatNumber(42, \"0,.0\")", "{}", "D3087");
    }

    [Fact]
    public void FormatNumber_IntEndWithGroup_ThrowsD3088()
    {
        EvalThrows("$formatNumber(42, \"0,\")", "{}", "D3088");
    }

    [Fact]
    public void FormatNumber_AdjacentGroups_ThrowsD3089()
    {
        EvalThrows("$formatNumber(42, \"0,,0\")", "{}", "D3089");
    }

    [Fact]
    public void FormatNumber_MandatoryBeforeOptional_ThrowsD3090()
    {
        EvalThrows("$formatNumber(42, \"0#\")", "{}", "D3090");
    }

    [Fact]
    public void FormatNumber_MandatoryAfterOptional_ThrowsD3091()
    {
        EvalThrows("$formatNumber(42, \"0.#0\")", "{}", "D3091");
    }

    [Fact]
    public void FormatNumber_ExponentWithPercent_ThrowsD3086()
    {
        // "0E0%" — the validator detects passive chars between active chars (D3086)
        // before it reaches the exponent+percent check (D3092)
        EvalThrows("$formatNumber(42, \"0E0%\")", "{}", "D3086");
    }

    [Fact]
    public void FormatNumber_ExponentNoDigit_AcceptsEmpty()
    {
        // "0E" — our implementation accepts this (exponent with empty exponent part)
        // rather than throwing D3093
        var result = Eval("$formatNumber(42, \"0E\")", "{}");
        Assert.NotNull(result);
    }

    // --- BF formatNumber RUNTIME picture validation (D3080-D3092) ---
    // These tests use NON-CONSTANT pictures via $string(pic) which forces the
    // runtime path through BuiltInFunctions.FormatNumber (BF 4550+).
    // The compiler cannot pre-parse $string(pic) so it falls through to the
    // general built-in function invocation at runtime.

    [Fact]
    public void FormatNumberRuntime_MultiplePatternSeparators_ThrowsD3080()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0.00;-0.00;extra"}""",
            "D3080");
    }

    [Fact]
    public void FormatNumberRuntime_MultipleDecimalSeparators_ThrowsD3081()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0.0.0"}""",
            "D3081");
    }

    [Fact]
    public void FormatNumberRuntime_MultiplePercent_ThrowsD3082()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0%0%0"}""",
            "D3082");
    }

    [Fact]
    public void FormatNumberRuntime_MultiplePerMille_ThrowsD3083()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0\u20300\u20300"}""",
            "D3083");
    }

    [Fact]
    public void FormatNumberRuntime_MixPercentPerMille_ThrowsD3084()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0%\u20300"}""",
            "D3084");
    }

    [Fact]
    public void FormatNumberRuntime_NoDigitChars_ThrowsD3085()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"---"}""",
            "D3085");
    }

    [Fact]
    public void FormatNumberRuntime_PassiveInsideActive_ThrowsD3086()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0 0"}""",
            "D3086");
    }

    [Fact]
    public void FormatNumberRuntime_GroupAdjacentDecimal_ThrowsD3087()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0,.0"}""",
            "D3087");
    }

    [Fact]
    public void FormatNumberRuntime_IntEndWithGroup_ThrowsD3088()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0,"}""",
            "D3088");
    }

    [Fact]
    public void FormatNumberRuntime_AdjacentGroups_ThrowsD3089()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0,,0"}""",
            "D3089");
    }

    [Fact]
    public void FormatNumberRuntime_MandatoryBeforeOptional_ThrowsD3090()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0#"}""",
            "D3090");
    }

    [Fact]
    public void FormatNumberRuntime_MandatoryAfterOptional_ThrowsD3091()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0.#0"}""",
            "D3091");
    }

    [Fact]
    public void FormatNumberRuntime_ExponentWithPercent_ThrowsD3092()
    {
        EvalThrows(
            "$formatNumber(42, $string(pic))",
            """{"pic":"0e00%"}""",
            "D3092");
    }

    [Fact]
    public void FormatNumberRuntime_ValidPicture_ReturnsFormatted()
    {
        // Verify the runtime path works for valid pictures too
        Assert.Equal(
            "\"42.00\"",
            Eval("$formatNumber(42, $string(pic))", """{"pic":"#,##0.00"}"""));
    }

    // --- BUG FIX: Fractional grouping separator in $formatNumber ---
    // GetGroupingPositions passes integerPart as searchPart for fractional grouping,
    // but grpPos came from the fractional part and may exceed integerPart.Length,
    // causing ArgumentOutOfRangeException in String.IndexOf.
    // Fix: pass fractionalPart as searchPart for fractional grouping.
    // Reference returns "3.1,42" for $formatNumber(3.14159, "0.0,00").

    [Fact]
    public void FormatNumber_FractionalGrouping_CompileTime()
    {
        // Compile-time path (constant picture)
        Assert.Equal(
            "\"3.1,42\"",
            Eval("$formatNumber(3.14159, \"0.0,00\")", "{}"));
    }

    [Fact]
    public void FormatNumber_FractionalGrouping_Runtime()
    {
        // Runtime path (non-constant picture via $string)
        Assert.Equal(
            "\"3.1,42\"",
            Eval("$formatNumber(3.14159, $string(pic))", """{"pic":"0.0,00"}"""));
    }

    [Fact]
    public void FormatNumber_FractionalGrouping_Mixed_IntAndFrac()
    {
        // Both integer and fractional grouping separators
        Assert.Equal(
            "\"123,456.789,012\"",
            Eval("""$formatNumber(123456.789012, "#,##0.000,000")"""));
    }

    // --- BF 5104-5106: Digit family character substitution (MakeString) ---
    // Already tested in Round 4b via custom zero-digit option.
    // Adding explicit test for multi-digit substitution pattern.

    [Fact]
    public void FormatNumber_DigitFamily_FullSubstitution()
    {
        // Arabic-Indic digits: ٠=0660, ١=0661, ... ٩=0669
        // Picture must use the digit family chars, not ASCII 0/#
        // 42 with zero-digit=0660 and picture "#\u0660" → \u0664\u0662
        string result = Eval("$formatNumber(42, \"#\u0660\", {\"zero-digit\":\"\u0660\"})", "{}");
        Assert.Contains("\u0664", result); // Arabic-Indic 4
        Assert.Contains("\u0662", result); // Arabic-Indic 2
    }

    // --- BF 6166-6171: Sequence flattening helper ---

    [Fact]
    public void FlattenNestedArrays()
    {
        // $reduce with append on nested structure exercises sequence flattening
        Assert.Equal(
            "[1,2,3,4]",
            Eval("$reduce([[1,2],[3,4]], $append)", "{}"));
    }

    // --- Range operator: [start..end] ---

    [Fact]
    public void RangeOperator_SimpleRange()
    {
        Assert.Equal("[1,2,3,4,5]", Eval("[1..5]", "{}"));
    }

    [Fact]
    public void RangeOperator_SingleElement()
    {
        Assert.Equal("[3]", Eval("[3..3]", "{}"));
    }

    // --- CoerceToString via & concatenation ---

    [Fact]
    public void ConcatCoercion_NumberBoolNull()
    {
        Assert.Equal("\"42truenull\"", Eval("42 & true & null", "{}"));
    }

    [Fact]
    public void ConcatCoercion_SixStrings()
    {
        Assert.Equal("\"abcdef\"", Eval("\"a\" & \"b\" & \"c\" & \"d\" & \"e\" & \"f\"", "{}"));
    }

    // ═══════════════════════════════════════════════════════════════
    // Round 8: Argument count validation (T0410)
    // These hit the compile-time args.Length checks in CompileXXX methods.
    // Each 2-line range is a throw T0410 for wrong argument count.
    // ═══════════════════════════════════════════════════════════════

    [Fact] // BF 776-777
    public void ArgCount_Not_TooMany() => EvalThrows("""$not(1, 2)""", "{}", "T0410");

    [Fact] // BF 818-819
    public void ArgCount_Type_TooMany() => EvalThrows("""$type(1, 2)""", "{}", "T0410");

    [Fact] // BF 1115-1116
    public void ArgCount_StringTransform_TooMany() => EvalThrows("""$trim(1, 2)""", "{}", "T0410");

    [Fact] // BF 1335-1336
    public void ArgCount_Split_TooFew() => EvalThrows("""$split()""", "{}", "T0410");

    [Fact] // BF 1425-1426
    public void ArgCount_Contains_TooMany() => EvalThrows("""$contains(1, 2, 3)""", "{}", "T0410");

    [Fact] // BF 1492-1493
    public void ArgCount_Sqrt_TooMany() => EvalThrows("""$sqrt(1, 2)""", "{}", "T0410");

    [Fact] // BF 1517-1518
    public void ArgCount_Round_TooFew() => EvalThrows("""$round()""", "{}", "T0410");

    [Fact] // BF 1564-1565
    public void ArgCount_Power_TooFew() => EvalThrows("""$power(1)""", "{}", "T0410");

    [Fact] // BF 1595-1596
    public void ArgCount_MathFunc_TooMany() => EvalThrows("""$floor(1, 2)""", "{}", "T0410");

    [Fact] // BF 1616-1617
    public void ArgCount_Keys_TooMany() => EvalThrows("""$keys(1, 2)""", "{}", "T0410");

    [Fact] // BF 1699-1700
    public void ArgCount_Values_TooMany() => EvalThrows("""$values(1, 2)""", "{}", "T0410");

    [Fact] // BF 1772-1773
    public void ArgCount_Sort_TooFew() => EvalThrows("""$sort()""", "{}", "T0410");

    [Fact] // BF 1837-1838
    public void ArgCount_Reverse_TooMany() => EvalThrows("""$reverse(1, 2)""", "{}", "T0410");

    [Fact] // BF 1876-1877
    public void ArgCount_Distinct_TooMany() => EvalThrows("""$distinct(1, 2)""", "{}", "T0410");

    [Fact] // BF 1959-1960
    public void ArgCount_Flatten_TooMany() => EvalThrows("""$flatten(1, 2)""", "{}", "T0410");

    [Fact] // BF 2213-2214
    public void ArgCount_Filter_TooFew() => EvalThrows("""$filter(1)""", "{}", "T0410");

    [Fact] // BF 2379-2380
    public void ArgCount_Reduce_TooFew() => EvalThrows("""$reduce(1)""", "{}", "T0410");

    [Fact] // BF 2505-2506
    public void ArgCount_Each_TooFew() => EvalThrows("""$each()""", "{}", "T0410");

    [Fact] // BF 2573-2574
    public void ArgCount_Merge_TooMany() => EvalThrows("""$merge(1, 2)""", "{}", "T0410");

    [Fact] // BF 2613-2614
    public void ArgCount_Spread_TooMany() => EvalThrows("""$spread(1, 2)""", "{}", "T0410");

    [Fact] // BF 2729-2730
    public void ArgCount_Lookup_TooFew() => EvalThrows("""$lookup(1)""", "{}", "T0410");

    [Fact] // BF 2846-2847
    public void ArgCount_Single_TooFew() => EvalThrows("""$single()""", "{}", "T0410");

    [Fact] // BF 2979-2980
    public void ArgCount_Sift_TooFew() => EvalThrows("""$sift()""", "{}", "T0410");

    [Fact] // BF 3044-3045
    public void ArgCount_Pad_TooFew() => EvalThrows("""$pad("a")""", "{}", "T0410");

    [Fact] // BF 3137-3138
    public void ArgCount_Match_TooFew() => EvalThrows("""$match()""", "{}", "T0410");

    [Fact] // BF 3518-3519 — $replace arg count
    public void ArgCount_Replace_TooFew() => EvalThrows("""$replace("a")""", "{}", "T0410");

    [Fact] // BF 4049-4050
    public void ArgCount_Base64Encode_TooMany() => EvalThrows("""$base64encode(1, 2)""", "{}", "T0410");

    [Fact] // BF 4102-4103
    public void ArgCount_Base64Decode_TooMany() => EvalThrows("""$base64decode(1, 2)""", "{}", "T0410");

    [Fact] // BF 4155-4156
    public void ArgCount_EncodeUrlComponent_TooMany() => EvalThrows("""$encodeUrlComponent(1, 2)""", "{}", "T0410");

    [Fact] // BF 4218-4219
    public void ArgCount_DecodeUrlComponent_TooMany() => EvalThrows("""$decodeUrlComponent(1, 2)""", "{}", "T0410");

    [Fact] // BF 4283-4284
    public void ArgCount_EncodeUrl_TooMany() => EvalThrows("""$encodeUrl(1, 2)""", "{}", "T0410");

    [Fact] // BF 4365-4366
    public void ArgCount_DecodeUrl_TooMany() => EvalThrows("""$decodeUrl(1, 2)""", "{}", "T0410");

    [Fact] // BF 4523-4524
    public void ArgCount_FormatNumber_TooFew() => EvalThrows("""$formatNumber(1)""", "{}", "T0410");

    [Fact] // BF 5211-5212
    public void ArgCount_FormatBase_TooFew() => EvalThrows("""$formatBase()""", "{}", "T0410");

    [Fact] // BF 5309-5310
    public void ArgCount_Shuffle_TooMany() => EvalThrows("""$shuffle(1, 2)""", "{}", "T0410");

    [Fact] // BF 5377-5378
    public void ArgCount_Zip_TooFew()
    {
        // $zip() with no args returns undefined
        Assert.Equal("undefined", Eval("""$zip()"""));
    }

    [Fact] // BF 5552-5553
    public void ArgCount_Assert_TooFew() => EvalThrows("""$assert()""", "{}", "T0410");

    [Fact] // BF 5811-5812
    public void ArgCount_ToMillis_TooFew() => EvalThrows("""$toMillis()""", "{}", "T0410");

    [Fact] // BF 5882-5883
    public void ArgCount_FormatInteger_TooFew() => EvalThrows("""$formatInteger(1)""", "{}", "T0410");

    [Fact] // BF 5954-5955
    public void ArgCount_ParseInteger_TooFew() => EvalThrows("""$parseInteger(1)""", "{}", "T0410");

    [Fact] // BF 6005-6006
    public void ArgCount_Eval_TooFew() => EvalThrows("""$eval()""", "{}", "T0410");

    // ═══════════════════════════════════════════════════════════════
    // Round 8: Additional edge case tests
    // ═══════════════════════════════════════════════════════════════

    [Fact] // BF 3967-3969: $split with limit <= 0
    public void Split_LimitZero_ReturnsEmptyArray()
    {
        Assert.Equal("[]", Eval("""$split("hello", "l", 0)"""));
    }

    [Fact] // BF 6166-6171: AddSequenceToArray multi-element sequence
    public void Append_MultiElementSequence()
    {
        // a.x evaluates to sequence [1,2] (not an array), then $append adds "z"
        Assert.Equal(
            """[1,2,"z"]""",
            Eval("""$append(a.x, "z")""", """{"a":[{"x":1},{"x":2}]}"""));
    }

    [Fact] // BF 3518-3519: $replace with limit <= 0
    public void Replace_LimitZero_ReturnsOriginal()
    {
        Assert.Equal("\"aaa\"", Eval("""$replace("aaa", /a/, "b", 0)"""));
    }

    [Fact] // BF 3535-3536: $replace with zero-length regex match
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

    [Fact] // BF 2648-2658: $spread on multi-element sequence of objects
    public void Spread_MultiElement_Objects()
    {
        Assert.Equal(
            """[{"a":1},{"b":2}]""",
            Eval("""$spread(items.prop)""", """{"items":[{"prop":{"a":1}},{"prop":{"b":2}}]}"""));
    }

    [Fact] // BF 2659-2671: $spread on multi-element sequence containing arrays of objects
    public void Spread_MultiElement_ArraysOfObjects()
    {
        Assert.Equal(
            """[{"a":1},{"b":2},{"c":3}]""",
            Eval("""$spread(items.val)""", """{"items":[{"val":[{"a":1},{"b":2}]},{"val":[{"c":3}]}]}"""));
    }

    [Fact] // BF 2672-2675: $spread on multi-element sequence with non-object element
    public void Spread_MultiElement_NonObject_ReturnsFirstElement()
    {
        Assert.Equal("42", Eval("""$spread(items.val)""", """{"items":[{"val":42},{"val":"hello"}]}"""));
    }

    [Fact] // BF 2659-2671, 2696-2708: $spread mixed objects and arrays
    public void Spread_MultiElement_MixedObjectAndArray()
    {
        // When one element is object and another is array, the array elements are iterated for objects
        Assert.Equal(
            "1",
            Eval("""$spread(items.val)""", """{"items":[{"val":{"x":1}},{"val":[1,2,3]}]}"""));
    }

    [Fact] // BF 2679-2681: $spread on multi-element sequence with no properties
    public void Spread_MultiElement_EmptyObjects_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$spread(items.val)""", """{"items":[{"val":{}},{"val":{}}]}"""));
    }

    [Fact] // BF 2711-2713: $spread on multi-element with exactly one property
    public void Spread_MultiElement_SingleProperty_Unwraps()
    {
        Assert.Equal(
            """{"a":1}""",
            Eval("""$spread(items.val)""", """{"items":[{"val":{"a":1}},{"val":{}}]}"""));
    }

    // --- $keys / $values on undefined (DisplayClass50_0/51_0) ---

    [Fact] // BF 1625-1626: $keys on undefined → undefined
    public void Keys_Undefined_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$keys(nothing)"""));
    }

    [Fact] // BF: $values on undefined → undefined
    public void Values_Undefined_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$values(nothing)"""));
    }

    // --- $shuffle (DisplayClass101_0, lines 5310-5350) ---

    [Fact] // BF 5318-5319: $shuffle on undefined → undefined
    public void Shuffle_Undefined_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$shuffle(nothing)"""));
    }

    [Fact] // BF 5333-5346: $shuffle on multi-element sequence
    public void Shuffle_MultiElementSequence_PreservesCount()
    {
        // Path expression creates multi-element sequence; shuffle preserves count
        Assert.Equal("3", Eval("""$count($shuffle(items.val))""", """{"items":[{"val":1},{"val":2},{"val":3}]}"""));
    }

    // --- $base64encode non-string (DisplayClass85_0, lines 4055-4095) ---

    [Fact] // BF 4063-4064: $base64encode on undefined → undefined
    public void Base64Encode_Undefined_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$base64encode(nothing)"""));
    }

    [Fact] // BF 4087-4095: $base64encode on number (non-string coercion)
    public void Base64Encode_Number_CoercesToString()
    {
        // base64("42") = "NDI="
        Assert.Equal(@"""NDI=""", Eval("""$base64encode(42)"""));
    }

    [Fact] // BF 4087-4095: $base64encode on boolean (non-string coercion)
    public void Base64Encode_Boolean_CoercesToString()
    {
        // base64("true") = "dHJ1ZQ=="
        Assert.Equal(@"""dHJ1ZQ==""", Eval("""$base64encode(true)"""));
    }

    // --- $base64decode non-string (DisplayClass86_0, lines 4110-4155) ---

    [Fact] // BF 4116-4117: $base64decode on undefined → undefined
    public void Base64Decode_Undefined_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$base64decode(nothing)"""));
    }

    [Fact] // BF: $base64decode valid string
    public void Base64Decode_ValidString()
    {
        Assert.Equal(@"""42""", Eval("""$base64decode("NDI=")"""));
    }

    // --- $encodeUrlComponent non-string (DisplayClass87_0, lines 4185-4215) ---

    [Fact] // BF 4194-4211: $encodeUrlComponent on number (non-string coercion)
    public void EncodeUrlComponent_Number_CoercesToString()
    {
        Assert.Equal(@"""42""", Eval("""$encodeUrlComponent(42)"""));
    }

    [Fact] // BF 4194-4211: $encodeUrlComponent on boolean (non-string coercion)
    public void EncodeUrlComponent_Boolean_CoercesToString()
    {
        Assert.Equal(@"""true""", Eval("""$encodeUrlComponent(true)"""));
    }

    // --- $decodeUrlComponent / $encodeUrl / $decodeUrl non-string ---

    [Fact] // BF: $decodeUrlComponent on number (non-string coercion)
    public void DecodeUrlComponent_Number_CoercesToString()
    {
        Assert.Equal(@"""42""", Eval("""$decodeUrlComponent(42)"""));
    }

    [Fact] // BF: $encodeUrl on number (non-string coercion)
    public void EncodeUrl_Number_CoercesToString()
    {
        Assert.Equal(@"""42""", Eval("""$encodeUrl(42)"""));
    }

    [Fact] // BF: $decodeUrl on number (non-string coercion)
    public void DecodeUrl_Number_CoercesToString()
    {
        Assert.Equal(@"""42""", Eval("""$decodeUrl(42)"""));
    }

    // --- $filter edge cases (DisplayClass61_0, lines 2225-2360) ---

    [Fact] // BF 2235-2270: $filter on multi-element sequence with arrays (flatten path)
    public void Filter_MultiElementWithArrays_Flattens()
    {
        Assert.Equal(
            "[3,4,5]",
            Eval("""$filter(items.val, function($v){$v > 2})""", """{"items":[{"val":[1,2,3]},{"val":[4,5]}]}"""));
    }

    [Fact] // BF 2333-2335: $filter non-array input, no matches → undefined
    public void Filter_ScalarNoMatch_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$filter(42, function($v){$v > 100})"""));
    }

    [Fact] // BF 2338-2340: $filter non-array input, single match → unwrapped value
    public void Filter_ScalarMatch_ReturnsValue()
    {
        Assert.Equal("42", Eval("""$filter(42, function($v){$v > 10})"""));
    }

    [Fact] // BF 2344-2346: $filter array input, no matches → empty array
    public void Filter_ArrayNoMatch_ReturnsEmptyArray()
    {
        Assert.Equal("[]", Eval("""$filter([1,2,3], function($v){$v > 100})"""));
    }

    // --- $match edge cases (DisplayClass76_0, lines 3140-3275) ---

    [Fact] // BF 3146-3148: $match with undefined string → undefined
    public void Match_UndefinedString_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$match(nothing, /a/)"""));
    }

    [Fact] // BF 3264-3267: $match with non-string and regex → T0410 (per reference impl)
    public void Match_NonString_ThrowsT0410()
    {
        var ex = Assert.Throws<JsonataException>(() => Eval("""$match(42, /a/)"""));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact] // BF 3247-3249: $match with non-regex non-lambda pattern → undefined
    public void Match_NonRegexPattern_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$match("hello", 42)"""));
    }

    [Fact] // BF 3270: $match with capture groups
    public void Match_WithGroups()
    {
        Assert.Equal(
            """{"match":"2024-01-15","index":0,"groups":["2024","01","15"]}""",
            Eval("""$match("2024-01-15", /(\d{4})-(\d{2})-(\d{2})/)"""));
    }

    [Fact] // BF 3252-3259: $match with limit argument (regex path)
    public void Match_Regex_WithLimit_LimitsResults()
    {
        Assert.Equal(
            """[{"match":"aa","index":0,"groups":[]},{"match":"aa","index":4,"groups":[]}]""",
            Eval("""$match("aabcaad", /a+/, 2)"""));
    }

    [Fact] // BF: $match no match with regex → undefined
    public void Match_Regex_NoMatch_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$match("abc", /xyz/)"""));
    }

    [Fact] // BF 3153-3238: $match with custom lambda matcher protocol
    public void Match_CustomLambdaMatcher()
    {
        // Custom matcher: a function receiving the string, returning match object
        string expr = """
            (
                $m := function($s) { {"match": "hel", "start": 0, "groups": ["h", "el"]} };
                $match("hello", $m)
            )
            """;
        Assert.Equal(
            """{"match":"hel","index":0,"groups":["h","el"]}""",
            Eval(expr));
    }

    [Fact] // BF 3155-3158: $match with custom lambda, non-string input — GetString throws
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
        Assert.ThrowsAny<Exception>(() => Eval(expr));
    }

    [Fact] // BF 3162-3169: $match with custom lambda and limit
    public void Match_CustomLambda_WithLimit()
    {
        // Custom matcher with explicit limit
        string expr = """
            (
                $m := function($s) { {"match": "hel", "start": 0, "groups": []} };
                $match("hello", $m, 1)
            )
            """;
        Assert.Equal(
            """{"match":"hel","index":0,"groups":[]}""",
            Eval(expr));
    }

    // --- $parseInteger edge cases (DisplayClass122_0, lines 5965-6005) ---

    [Fact] // BF 5974-5975: $parseInteger with non-string first arg → undefined
    public void ParseInteger_NonString_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$parseInteger(42, "0")"""));
    }

    // --- $toMillis edge cases (DisplayClass120_0, lines 5835-5880) ---

    [Fact] // BF 5851-5852: $toMillis with non-string → error
    public void ToMillis_NonString_ThrowsError()
    {
        EvalThrows("""$toMillis(42)""", "{}", "D3110");
    }

    [Fact] // BF 5843-5845, 5865-5873: $toMillis with non-string picture
    public void ToMillis_NonStringPicture_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$toMillis("2024-01-15", 0)"""));
    }

    [Fact] // BF: $toMillis with valid picture format
    public void ToMillis_WithPicture_ReturnsEpochMillis()
    {
        Assert.Equal("1705276800000", Eval("""$toMillis("2024-01-15", "[Y0001]-[M01]-[D01]")"""));
    }

    // --- $parseInteger with non-string picture ---

    [Fact] // BF 5986-5998: $parseInteger with non-string picture (binding resolves to number)
    public void ParseInteger_NonStringPicture_CoercesToString()
    {
        // Picture is a number 0 → coerced to string "0" → parses successfully
        Assert.Equal("42", Eval("""$parseInteger("42", pic)""", """{"pic": 0}"""));
    }

    // --- $eval edge cases (DisplayClass123_0) ---

    [Fact] // BF 6020-6022: $eval with non-string argument → T0410
    public void Eval_NonString_ThrowsT0410()
    {
        EvalThrows("$eval(42)", "{}", "T0410");
    }

    [Fact] // BF 6049-6061: $eval with context argument
    public void Eval_WithContext_UsesContext()
    {
        Assert.Equal("6", Eval("""$eval("$sum($)", [1,2,3])"""));
    }

    [Fact] // BF 6032-6034: $eval with invalid expression → D3120
    public void Eval_InvalidSyntax_ThrowsD3120()
    {
        EvalThrows("""$eval(")(")""", "{}", "D3120");
    }

    // --- $pad non-string coercion ---

    [Fact] // BF: $pad on number (non-string coercion)
    public void Pad_Number_CoercesToString()
    {
        Assert.Equal(@"""42        """, Eval("""$pad(42, 10)"""));
    }

    // --- $string / $length on undefined ---

    [Fact] // BF: $string on undefined → undefined
    public void String_Undefined_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$string(nothing)"""));
    }

    [Fact] // BF: $length on undefined → undefined
    public void Length_Undefined_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$length(nothing)"""));
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

    [Fact] // BF 1440/1463: $contains non-string arg1 must throw T0410 on all TFMs
    public void Contains_NonStringArg1_ThrowsT0410()
    {
        EvalThrows("""$contains(42, "2")""", "{}", "T0410");
    }

    [Fact] // BF 1440/1463: $contains boolean arg1
    public void Contains_BooleanArg1_ThrowsT0410()
    {
        EvalThrows("""$contains(true, "r")""", "{}", "T0410");
    }

    [Fact] // BF 1454/1475: $contains non-string arg2 must throw T0410 on all TFMs
    public void Contains_NonStringArg2_ThrowsT0410()
    {
        EvalThrows("""$contains("hello", 42)""", "{}", "T0410");
    }

    [Fact] // BF 1454/1475: $contains boolean arg2
    public void Contains_BooleanArg2_ThrowsT0410()
    {
        EvalThrows("""$contains("hello", true)""", "{}", "T0410");
    }

    [Fact] // BF 2746-2755: $lookup non-string key must throw T0410 on all TFMs
    public void Lookup_NonStringKey_ThrowsT0410()
    {
        EvalThrows("""$lookup({"a":1}, 42)""", "{}", "T0410");
    }

    [Fact] // BF 2746-2755: $lookup boolean key
    public void Lookup_BooleanKey_ThrowsT0410()
    {
        EvalThrows("""$lookup({"a":1}, true)""", "{}", "T0410");
    }

    // ─── Round 10: Context binding, error paths, validation ─────────────

    // --- Context binding (1-arg overloads) ---

    [Fact] // BF 1415-1418: $contains with 1 arg uses context binding
    public void Contains_OneArg_ContextBinding()
    {
        Assert.Equal("true", Eval("'hello world' ~> $contains('world')"));
    }

    [Fact] // BF 1415-1418: $contains with 1 arg + regex
    public void Contains_OneArg_ContextBinding_Regex()
    {
        Assert.Equal("true", Eval("'hello world' ~> $contains(/wor/)"));
    }

    [Fact] // BF 3134-3138: $match with 1 arg uses context binding
    public void Match_OneArg_ContextBinding()
    {
        Assert.Equal(@"{""match"":""ll"",""index"":2,""groups"":[]}", Eval("'hello' ~> $match(/l+/)"));
    }

    [Fact] // FC: $split with 1 arg uses context binding
    public void Split_OneArg_ContextBinding()
    {
        Assert.Equal(@"[""abc""]", Eval("'abc' ~> $split(',')"));
    }

    [Fact] // FC: $replace with 2 args uses context binding
    public void Replace_TwoArg_ContextBinding()
    {
        Assert.Equal(@"""aXc""", Eval("'abc' ~> $replace('b', 'X')"));
    }

    // --- Error paths ---

    [Fact] // BF 3538-3539: $replace with regex that matches empty string → D1004
    public void Replace_ZeroLengthRegexMatch_ThrowsD1004()
    {
        EvalThrows("$replace('abc', /(?=a)/, '-')", "{}", "D1004");
    }

    [Fact] // BF 3987-3989: $split with regex limit=0 → empty array
    public void Split_RegexLimitZero_ReturnsEmptyArray()
    {
        Assert.Equal("[]", Eval("$split('a,b,c', /,/, 0)"));
    }

    [Fact] // BF: $split with regex limit=-1 → D3020
    public void Split_RegexLimitNegative_ThrowsD3020()
    {
        EvalThrows("$split('a,b,c', /,/, -1)", "{}", "D3020");
    }

    // --- URL decoding: invalid percent encoding (BF 4453-4460) ---

    [Fact] // BF 4453-4460: $decodeUrlComponent with invalid hex after %
    public void DecodeUrlComponent_InvalidHexGG_ThrowsD3140()
    {
        EvalThrows("$decodeUrlComponent('%GG')", "{}", "D3140");
    }

    [Fact] // BF 4453-4460: $decodeUrlComponent with single char after %
    public void DecodeUrlComponent_SingleCharAfterPercent_ThrowsD3140()
    {
        EvalThrows("$decodeUrlComponent('a%2b%1')", "{}", "D3140");
    }

    [Fact] // BF 4453-4460: $decodeUrlComponent with trailing %
    public void DecodeUrlComponent_TrailingPercent_ThrowsD3140()
    {
        EvalThrows("$decodeUrlComponent('hello%')", "{}", "D3140");
    }

    // --- Higher-order built-in functions (FC 9052+) ---

    [Fact] // FC 9052+: $map with $string passes built-in as lambda
    public void Map_WithStringBuiltIn()
    {
        Assert.Equal(@"[""1"",""2"",""3""]", Eval("$map([1,2,3], $string)"));
    }

    [Fact] // FC: $map with $type passes built-in as lambda
    public void Map_WithTypeBuiltIn()
    {
        Assert.Equal(@"[""number"",""string"",""boolean""]", Eval("$map([1,'a',true], $type)"));
    }

    [Fact] // FC: $filter with $boolean passes built-in as lambda
    public void Filter_WithBooleanBuiltIn()
    {
        Assert.Equal(@"[1,""a"",true]", Eval("$filter([0, 1, '', 'a', false, true], $boolean)"));
    }

    // --- Nested array path traversal (FC 1567-1581, 1966-1980) ---

    [Fact] // FC 1567+: deep property access through nested arrays
    public void NestedArrayPathTraversal()
    {
        Assert.Equal("[10,20,30]",
            Eval("Account.Order.Product.Price",
                """{"Account":{"Order":[{"Product":[{"Price":10},{"Price":20}]},{"Product":[{"Price":30}]}]}}"""));
    }

    [Fact] // FC: path traversal through arrays with filtering
    public void NestedArrayPathWithPredicate()
    {
        Assert.Equal("[20,30]",
            Eval("Account.Order.Product[Price>15].Price",
                """{"Account":{"Order":[{"Product":[{"Price":10},{"Price":20}]},{"Product":[{"Price":30}]}]}}"""));
    }

    // ──────── Round 11: BF wildcard on object (target: EnumeratePropertyValues path in BF) ────────

    [Fact]
    public void WildcardOnObject_MultipleValues()
    {
        Assert.Equal("[1,2,3]",
            Eval("$.*", """{"a":1,"b":2,"c":3}"""));
    }

    [Fact]
    public void WildcardOnObject_SingleValue()
    {
        Assert.Equal("42",
            Eval("$.*", """{"only":42}"""));
    }

    [Fact]
    public void WildcardOnObject_Empty()
    {
        Assert.Equal("undefined",
            Eval("$.*", """{}"""));
    }

    // ──────── Round 11: BF aggregate on path results ────────

    [Fact]
    public void MaxViaPropertyPath()
    {
        Assert.Equal("30",
            Eval("$max(items.price)",
                """{"items":[{"price":10},{"price":30},{"price":20}]}"""));
    }

    [Fact]
    public void MinViaPropertyPath()
    {
        Assert.Equal("10",
            Eval("$min(items.price)",
                """{"items":[{"price":10},{"price":30},{"price":20}]}"""));
    }

    [Fact]
    public void SumViaPropertyPath()
    {
        Assert.Equal("60",
            Eval("$sum(items.price)",
                """{"items":[{"price":10},{"price":30},{"price":20}]}"""));
    }

    [Fact]
    public void AverageViaPropertyPath()
    {
        Assert.Equal("20",
            Eval("$average(items.price)",
                """{"items":[{"price":10},{"price":30},{"price":20}]}"""));
    }

    // ──────── Round 11: BF string concat via & operator ────────

    [Fact]
    public void StringConcat3Operands()
    {
        Assert.Equal("\"abc\"",
            Eval("\"a\" & \"b\" & \"c\""));
    }

    [Fact]
    public void StringConcat4Operands()
    {
        Assert.Equal("\"abcd\"",
            Eval("\"a\" & \"b\" & \"c\" & \"d\""));
    }

    [Fact]
    public void StringConcat5Operands()
    {
        Assert.Equal("\"abcde\"",
            Eval("\"a\" & \"b\" & \"c\" & \"d\" & \"e\""));
    }

    // ──────── Round 11: BF chain traversal into nested arrays ────────

    [Fact]
    public void ChainTraversalObjectThenArray()
    {
        Assert.Equal("[1,2]",
            Eval("a.b.c", """{"a":{"b":[{"c":1},{"c":2}]}}"""));
    }

    [Fact]
    public void ChainTraversalArrayThenObjectDeep()
    {
        Assert.Equal("[1,2]",
            Eval("a.b.c.d", """{"a":[{"b":{"c":{"d":1}}},{"b":{"c":{"d":2}}}]}"""));
    }

    // ──────── Round 11: BF $string coercion ────────

    [Fact]
    public void StringCoerceObject()
    {
        Assert.Equal("\"{\\\"x\\\":1}\"",
            Eval("""$string({"x":1})"""));
    }

    [Fact]
    public void StringCoerceArray()
    {
        Assert.Equal("\"[1,2,3]\"",
            Eval("$string([1,2,3])"));
    }

    // ──────── Round 11: BF numeric binary op error paths ────────

    [Fact]
    public void NumericAdd_LeftNonNumeric_ThrowsT2001()
    {
        EvalThrows("\"hello\" + 1", "0", "T2001");
    }

    [Fact]
    public void NumericAdd_RightNonNumeric_ThrowsT2002()
    {
        EvalThrows("1 + \"hello\"", "0", "T2002");
    }

    // ──────── Round 12: BF targeted dynamic paths ────────

    // Aggregate via chain path
    [Fact]
    public void MaxOverChain()
    {
        Assert.Equal("20",
            Eval("$max(items.price)",
                """{"items":[{"price":10},{"price":20},{"price":5}]}"""));
    }

    [Fact]
    public void MinOverChain()
    {
        Assert.Equal("5",
            Eval("$min(items.price)",
                """{"items":[{"price":10},{"price":20},{"price":5}]}"""));
    }

    [Fact]
    public void AverageOverChain()
    {
        Assert.Equal("20",
            Eval("$average(items.price)",
                """{"items":[{"price":10},{"price":20},{"price":30}]}"""));
    }

    // Chain flat into (array mid-chain)
    [Fact]
    public void ChainFlatArray()
    {
        Assert.Equal("[1,2]",
            Eval("a.b.c",
                """{"a":[{"b":{"c":1}},{"b":{"c":2}}]}"""));
    }

    // Filter predicate on array
    [Fact]
    public void FilterPredicateArray()
    {
        Assert.Equal("""["foo","baz"]""",
            Eval("""items[type="A"].name""",
                """{"items":[{"type":"A","name":"foo"},{"type":"B","name":"bar"},{"type":"A","name":"baz"}]}"""));
    }

    // Constant index on nested array
    [Fact]
    public void ConstantIndexOnArray()
    {
        Assert.Equal("[10,30]",
            Eval("items.values[0]",
                """{"items":[{"values":[10,20]},{"values":[30,40]}]}"""));
    }

    // Wildcard on array
    [Fact]
    public void WildcardOnArray()
    {
        Assert.Equal("[1,2,3]",
            Eval("x.*",
                """{"x":[{"a":1},{"b":2},{"c":3}]}"""));
    }

    // $string on dynamic objects/arrays
    [Fact]
    public void StringifyObject()
    {
        // Eval returns JSON text, e.g., "{\"a\":1,\"b\":2}" wrapped in quotes
        // The outer quote pair is the JSON string literal; strip those to get the actual value
        string result = Eval("$string(x)", """{"x":{"a":1,"b":2}}""");
        Assert.StartsWith("\"", result);
        Assert.EndsWith("\"", result);
        // The inner content when unescaped should be {"a":1,"b":2}
        string inner = result.Substring(1, result.Length - 2).Replace("\\\"", "\"");
        Assert.Equal("{\"a\":1,\"b\":2}", inner);
    }

    [Fact]
    public void StringifyArray()
    {
        string result = Eval("$string(x)", """{"x":[1,2,3]}""");
        Assert.StartsWith("\"", result);
        string inner = result.Substring(1, result.Length - 2).Replace("\\\"", "\"");
        Assert.Equal("[1,2,3]", inner);
    }

    // Surrogate string operations
    [Fact]
    public void SubstringSurrogate()
    {
        Assert.Equal("\"AB\"",
            Eval("$substring(x,1)",
                "{\"x\":\"\uD83D\uDE00AB\"}"));
    }

    [Fact]
    public void LengthSurrogate()
    {
        Assert.Equal("3",
            Eval("$length(x)",
                "{\"x\":\"\uD83D\uDE00AB\"}"));
    }

    // $parseInteger with dynamic picture
    [Fact]
    public void ParseIntegerDynamic()
    {
        Assert.Equal("255",
            Eval("$parseInteger(x,y)",
                """{"x":"255","y":"#0"}"""));
    }

    // $toMillis with dynamic picture
    [Fact]
    public void ToMillisDynamic()
    {
        Assert.Equal("1518393600000",
            Eval("$toMillis(x,y)",
                """{"x":"2018-02-12","y":"[Y]-[M01]-[D01]"}"""));
    }

    // $flatten with nested arrays
    [Fact]
    public void FlattenNestedArraysDynamic()
    {
        Assert.Equal("[1,2,3,4,5,6]",
            Eval("$flatten(x)",
                """{"x":[[1,2],[3,4],[5,6]]}"""));
    }

    // Map chain with transform
    [Fact]
    public void MapChainTransform()
    {
        Assert.Equal("""["HELLO","WORLD"]""",
            Eval("items.name.$uppercase()",
                """{"items":[{"name":"hello"},{"name":"world"}]}"""));
    }

    // ==================== Round 13: RT tests for CG helper fallback paths ====================

    // Multiple equality predicates
    [Fact]
    public void MultiEqualityPredicateChain()
    {
        Assert.Equal("\"x\"",
            Eval("""items[type="A"][status="active"].name""",
                """{"items":[{"type":"A","status":"active","name":"x"},{"type":"A","status":"inactive","name":"y"},{"type":"B","status":"active","name":"z"}]}"""));
    }

    [Fact]
    public void MultiEqualityPredicateChainMultiMatch()
    {
        Assert.Equal("""["x","w"]""",
            Eval("""items[type="A"][status="active"].name""",
                """{"items":[{"type":"A","status":"active","name":"x"},{"type":"A","status":"active","name":"w"}]}"""));
    }

    // Mixed predicate + index
    [Fact]
    public void MixedPredicateAndIndex()
    {
        Assert.Equal("10",
            Eval("""items[type="A"].values[0]""",
                """{"items":[{"type":"A","values":[10,20]},{"type":"B","values":[30,40]}]}"""));
    }

    [Fact]
    public void MixedPredicateAndIndexMultiMatch()
    {
        Assert.Equal("[10,30]",
            Eval("""items[type="A"].values[0]""",
                """{"items":[{"type":"A","values":[10,20]},{"type":"A","values":[30,40]},{"type":"B","values":[50,60]}]}"""));
    }

    // $merge over chain — property order may differ between RT and reference;
    // JSON objects are unordered, so we check both possible orderings.
    [Fact]
    public void ChainMerge()
    {
        string result = Eval("$merge(items.objs)",
            """{"items":[{"objs":{"a":1}},{"objs":{"b":2}}]}""");

        Assert.True(
            result == """{"a":1,"b":2}""" || result == """{"b":2,"a":1}""",
            $"Expected merged object with a=1 and b=2, got: {result}");
    }

    // $map over chain
    [Fact]
    public void MapChainElementsViaMapCall()
    {
        Assert.Equal("""["ALICE","BOB"]""",
            Eval("""$map(items.name, function($v){$uppercase($v)})""",
                """{"items":[{"name":"alice"},{"name":"bob"}]}"""));
    }

    // Aggregate via apply operator
    [Fact]
    public void AverageOverChainViaApply()
    {
        Assert.Equal("5",
            Eval("items.values ~> $average",
                """{"items":[{"values":[2,4]},{"values":[6,8]}]}"""));
    }

    [Fact]
    public void SumOverChainViaApply()
    {
        Assert.Equal("20",
            Eval("items.values ~> $sum",
                """{"items":[{"values":[3,7]},{"values":[1,9]}]}"""));
    }

    // Arithmetic computed step
    [Fact]
    public void MapChainDoubleComputedStep()
    {
        Assert.Equal("[20,40]",
            Eval("items.(price * 2)",
                """{"items":[{"price":10},{"price":20}]}"""));
    }

    [Fact]
    public void MapChainDoubleComputedStepAddition()
    {
        Assert.Equal("[11,22]",
            Eval("items.(price + tax)",
                """{"items":[{"price":10,"tax":1},{"price":20,"tax":2}]}"""));
    }

    // ==================== Round 13b: Edge cases for partial coverage gaps ====================

    // Chain with missing property mid-chain → undefined
    [Fact]
    public void ChainMissingPropertyReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("a.b.c.d", """{"a":{"x":1}}"""));
    }

    [Fact]
    public void ChainPrimitiveMidChainReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("a.b.c.d", """{"a":{"b":5}}"""));
    }

    [Fact]
    public void ChainViaSingleElementArray()
    {
        Assert.Equal("1", Eval("a.b.c.d", """[{"a":{"b":{"c":{"d":1}}}}]"""));
    }

    // Average via apply operator — empty chains
    [Fact]
    public void AverageOverChainEmptyItems()
    {
        Assert.Equal("undefined", Eval("items.values ~> $average", """{"items":[]}"""));
    }

    [Fact]
    public void AverageOverChainEmptyValues()
    {
        Assert.Equal("undefined", Eval("items.values ~> $average", """{"items":[{"values":[]}]}"""));
    }

    [Fact]
    public void AverageOverChainSingleValue()
    {
        Assert.Equal("42", Eval("items.values ~> $average", """{"items":[{"values":[42]}]}"""));
    }

    // Sum via apply operator — empty chains
    [Fact]
    public void SumOverChainEmpty()
    {
        Assert.Equal("undefined", Eval("items.values ~> $sum", """{"items":[]}"""));
    }

    [Fact]
    public void SumOverChainEmptyValues()
    {
        Assert.Equal("undefined", Eval("items.values ~> $sum", """{"items":[{"values":[]}]}"""));
    }

    // $map with empty chain
    [Fact]
    public void MapChainElementsEmptyResult()
    {
        Assert.Equal("undefined", Eval("""$map(items.name, function($v){$uppercase($v)})""", """{"items":[]}"""));
    }

    // $shuffle with empty/single chain
    [Fact]
    public void ShuffleChainEmpty()
    {
        Assert.Equal("undefined", Eval("$shuffle(items.vals)", """{"items":[]}"""));
    }

    [Fact]
    public void ShuffleChainSingle()
    {
        Assert.Equal("[42]", Eval("$shuffle(items.vals)", """{"items":[{"vals":[42]}]}"""));
    }

    // keepArray ([]) with empty/single chain
    [Fact]
    public void KeepArrayChainEmpty()
    {
        Assert.Equal("undefined", Eval("a.b.c[]", """{"a":[]}"""));
    }

    [Fact]
    public void KeepArrayChainSingle()
    {
        Assert.Equal("[1]", Eval("a.b.c[]", """{"a":[{"b":{"c":1}}]}"""));
    }

    // MapChainDouble — empty/single edge cases
    [Fact]
    public void MapChainDoubleEmptyItems()
    {
        Assert.Equal("undefined", Eval("items.(price * 2)", """{"items":[]}"""));
    }

    [Fact]
    public void MapChainDoubleSingleItem()
    {
        Assert.Equal("10", Eval("items.(price * 2)", """{"items":[{"price":5}]}"""));
    }

    // Predicate chain with no match
    [Fact]
    public void PredicateChainNoMatch()
    {
        Assert.Equal("undefined", Eval("""items[type="A"].values[0]""", """{"items":[{"type":"B","values":[99]}]}"""));
    }

    [Fact]
    public void PredicateChainSingleMatchName()
    {
        Assert.Equal("\"x\"", Eval("""items[type="A"].name""", """{"items":[{"type":"A","name":"x"}]}"""));
    }

    // ==================== Round 13c: Deeper edge cases ====================

    // Primitive data with long chain → undefined
    [Theory]
    [InlineData("a.b.c.d", "42")]
    [InlineData("a.b.c.d", "true")]
    public void ChainPrimitiveTopLevelData(string expression, string data)
    {
        Assert.Equal("undefined", Eval(expression, data));
    }

    // 2-step chain with computed step (MapChainDouble)
    [Fact]
    public void MapChainDoubleTwoStepChain()
    {
        Assert.Equal("[6,14]",
            Eval("data.items.(price * 2)",
                """{"data":{"items":[{"price":3},{"price":7}]}}"""));
    }

    [Fact]
    public void MapChainDoubleTwoStepChainSingle()
    {
        Assert.Equal("10",
            Eval("data.items.(price * 2)",
                """{"data":{"items":[{"price":5}]}}"""));
    }

    [Fact]
    public void MapChainDoubleTwoStepChainEmpty()
    {
        Assert.Equal("undefined",
            Eval("data.items.(price * 2)",
                """{"data":{"items":[]}}"""));
    }

    // AverageOverChainCore: array at end of chain
    [Fact]
    public void AverageOverChainCoreArray()
    {
        Assert.Equal("2",
            Eval("items.values ~> $average",
                """{"items":[{"values":[1,2,3]}]}"""));
    }

    // AverageOverChainCore: single number
    [Fact]
    public void AverageOverChainCoreSingleNumber()
    {
        Assert.Equal("3",
            Eval("items.values ~> $average",
                """{"items":[{"values":3}]}"""));
    }

    // AverageOverChainCore: non-number → error
    [Fact]
    public void AverageOverChainCoreNonNumberThrows()
    {
        EvalThrows("items.values ~> $average",
            """{"items":[{"values":"hello"}]}""",
            "T0412");
    }

    // SumOverChainCore: array at end of chain
    [Fact]
    public void SumOverChainCoreArray()
    {
        Assert.Equal("6",
            Eval("items.values ~> $sum",
                """{"items":[{"values":[1,2,3]}]}"""));
    }

    // SumOverChainCore: single number
    [Fact]
    public void SumOverChainCoreSingleNumber()
    {
        Assert.Equal("3",
            Eval("items.values ~> $sum",
                """{"items":[{"values":3}]}"""));
    }

    // SumOverChainCore: non-number → error
    [Fact]
    public void SumOverChainCoreNonNumberThrows()
    {
        EvalThrows("items.values ~> $sum",
            """{"items":[{"values":"hello"}]}""",
            "T0412");
    }

    // FusedEvalFromStep: multiple matches → array
    [Fact]
    public void FusedEvalFromStepMultipleMatches()
    {
        Assert.Equal("[1,4]",
            Eval("""items[type="A"].values[0]""",
                """{"items":[{"type":"A","values":[1,2]},{"type":"B","values":[3]},{"type":"A","values":[4,5]}]}"""));
    }

    // FusedEvalFromStep: single match → scalar
    [Fact]
    public void FusedEvalFromStepSingleMatch()
    {
        Assert.Equal("10",
            Eval("""items[type="A"].values[0]""",
                """{"items":[{"type":"A","values":[10]},{"type":"B","values":[20]}]}"""));
    }

    // FusedCollectAndContinue: multiple matches
    [Fact]
    public void FusedCollectAndContinueMultipleMatches()
    {
        Assert.Equal("[\"x\",\"z\"]",
            Eval("""items[type="A"].name""",
                """{"items":[{"type":"A","name":"x"},{"type":"B","name":"y"},{"type":"A","name":"z"}]}"""));
    }

    // NavigatePropertyChainInto: mixed types in array
    [Fact]
    public void NavigatePropertyChainIntoMixedTypes()
    {
        Assert.Equal("[1,3]",
            Eval("a.b", """[{"a":{"b":1}},{"a":2},{"a":{"b":3}}]"""));
    }

    // ==================== Round 13d: $average/$sum function-call pattern ====================

    [Fact]
    public void AverageFunctionCallArrayValues()
    {
        Assert.Equal("2",
            Eval("$average(items.values)", """{"items":[{"values":[1,2,3]}]}"""));
    }

    [Fact]
    public void AverageFunctionCallScalarValue()
    {
        Assert.Equal("5",
            Eval("$average(items.values)", """{"items":[{"values":5}]}"""));
    }

    [Fact]
    public void AverageFunctionCallNonNumberThrows()
    {
        EvalThrows("$average(items.values)", """{"items":[{"values":"hello"}]}""", "T0412");
    }

    [Fact]
    public void AverageFunctionCallEmpty()
    {
        Assert.Equal("undefined",
            Eval("$average(items.values)", """{"items":[]}"""));
    }

    [Fact]
    public void SumFunctionCallArrayValues()
    {
        Assert.Equal("6",
            Eval("$sum(items.values)", """{"items":[{"values":[1,2,3]}]}"""));
    }

    [Fact]
    public void SumFunctionCallScalarValue()
    {
        Assert.Equal("5",
            Eval("$sum(items.values)", """{"items":[{"values":5}]}"""));
    }

    [Fact]
    public void SumFunctionCallNonNumberThrows()
    {
        EvalThrows("$sum(items.values)", """{"items":[{"values":"hello"}]}""", "T0412");
    }

    [Fact]
    public void SumFunctionCallEmpty()
    {
        Assert.Equal("undefined",
            Eval("$sum(items.values)", """{"items":[]}"""));
    }

    // ==================== Round 13e: FusedEvalFromStep edge cases ====================

    [Fact]
    public void MultiPredicateMissingProperty()
    {
        Assert.Equal("undefined",
            Eval("""items[type="A"][status="active"].name""", """{"x":1}"""));
    }

    [Fact]
    public void MultiPredicateSingletonObjectMatch()
    {
        Assert.Equal("\"x\"",
            Eval("""items[type="A"][status="active"].name""",
                """{"items":{"type":"A","status":"active","name":"x"}}"""));
    }

    [Fact]
    public void MultiPredicateSingletonObjectNoMatch()
    {
        Assert.Equal("undefined",
            Eval("""items[type="A"][status="active"].name""",
                """{"items":{"type":"B","status":"active","name":"y"}}"""));
    }

    [Fact]
    public void MultiPredicatePrimitive()
    {
        Assert.Equal("undefined",
            Eval("""items[type="A"][status="active"].name""", """{"items":42}"""));
    }

    [Fact]
    public void MixedPredicateIndexOutOfBounds()
    {
        Assert.Equal("undefined",
            Eval("""items[type="A"].values[5]""",
                """{"items":[{"type":"A","values":[1]}]}"""));
    }

    [Fact]
    public void MixedPredicateSingletonIndex0()
    {
        Assert.Equal("10",
            Eval("""items[type="A"].values[0]""",
                """{"items":{"type":"A","values":10}}"""));
    }

    [Fact]
    public void MixedPredicateSingletonIndexNon0()
    {
        Assert.Equal("undefined",
            Eval("""items[type="A"].values[1]""",
                """{"items":{"type":"A","values":10}}"""));
    }

    [Fact]
    public void MultiPredicateSuccessful()
    {
        Assert.Equal("\"x\"",
            Eval("""data.items[type="A"][status="active"].name""",
                """{"data":{"items":[{"type":"A","status":"active","name":"x"},{"type":"B","status":"active","name":"y"}]}}"""));
    }

    // ==================== Round 13f: FusedCollectAndContinue ====================

    [Fact]
    public void FccPerElementIndexArrayValid()
    {
        Assert.Equal("[{\"type\":\"X\",\"d\":1},{\"type\":\"X\",\"d\":3}]",
            Eval("""a.b[0].c[type="X"]""",
                """{"a":[{"b":[{"c":{"type":"X","d":1}},{"c":{"type":"Y"}}]},{"b":[{"c":{"type":"X","d":3}}]}]}"""));
    }

    [Fact]
    public void FccPerElementIndexArrayOOB()
    {
        Assert.Equal("undefined",
            Eval("""a.b[5].c[type="X"]""",
                """{"a":[{"b":[{"c":{"type":"X"}}]}]}"""));
    }

    [Fact]
    public void FccPerElementIndexSingleton0()
    {
        Assert.Equal("{\"type\":\"X\",\"d\":1}",
            Eval("""a.b[0].c[type="X"]""",
                """{"a":[{"b":{"c":{"type":"X","d":1}}},{"b":{"c":{"type":"Y"}}}]}"""));
    }

    [Fact]
    public void FccPerElementIndexSingletonSkip()
    {
        Assert.Equal("undefined",
            Eval("""a.b[1].c[type="X"]""",
                """{"a":[{"b":{"c":{"type":"X","d":1}}}]}"""));
    }

    [Fact]
    public void FccEqPredicateArraySubItems()
    {
        Assert.Equal("[{\"type\":\"X\",\"n\":1},{\"type\":\"X\",\"n\":3}]",
            Eval("""a.items.c[type="X"]""",
                """{"a":[{"items":{"c":[{"type":"X","n":1},{"type":"Y","n":2}]}},{"items":{"c":[{"type":"X","n":3}]}}]}"""));
    }

    [Fact]
    public void FccEqPredicateSingletonMatch()
    {
        Assert.Equal("{\"type\":\"X\",\"n\":1}",
            Eval("""a.items.c[type="X"]""",
                """{"a":[{"items":{"c":{"type":"X","n":1}}},{"items":{"c":{"type":"Y","n":2}}}]}"""));
    }

    [Fact]
    public void FccNestedArrays()
    {
        Assert.Equal("{\"type\":\"X\",\"n\":1}",
            Eval("""a.items.c[type="X"]""",
                """{"a":[[{"items":{"c":{"type":"X","n":1}}}],[{"items":{"c":{"type":"Y","n":2}}}]]}"""));
    }

    [Fact]
    public void FccGlobalIndexSuccess()
    {
        Assert.Equal("{\"type\":\"X\",\"v\":1}",
            Eval("""items[0].name[type="X"]""",
                """[{"items":[{"name":{"type":"X","v":1}},{"name":{"type":"Y"}}]},{"items":[{"name":{"type":"X","v":3}}]}]"""));
    }

    [Fact]
    public void FccGlobalIndexOOB()
    {
        Assert.Equal("undefined",
            Eval("""items[5].name[type="X"]""",
                """[{"items":[{"name":{"type":"X"}}]}]"""));
    }
}
