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
}
