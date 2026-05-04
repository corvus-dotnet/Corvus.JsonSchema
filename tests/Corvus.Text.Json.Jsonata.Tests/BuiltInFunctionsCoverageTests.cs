// <copyright file="BuiltInFunctionsCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Coverage tests for <see cref="BuiltInFunctions"/> targeting uncovered branches
/// in $map, $filter, $spread, $shuffle, $fromMillis, $base64decode,
/// $encodeUrlComponent, and $decodeUrlComponent.
/// </summary>
[Trait("Category", "coverage")]
public class BuiltInFunctionsCoverageTests
{
    private static string Eval(string expression, string data = "null")
    {
        return JsonataEvaluator.Default.EvaluateToString(expression, data) ?? "undefined";
    }

    // ─── $map with undefined function (L2072-2074) ───────────────────────

    [Fact]
    public void Map_UndefinedFunction_ReturnsUndefined()
    {
        // When function argument is undefined, $map returns undefined
        Assert.Equal("undefined", Eval("$map([1,2,3], nonexistent)", "{}"));
    }

    // ─── $map with multi-element sequence containing arrays (L2095-2104) ──

    [Fact]
    public void Map_MultiElementSequenceWithArrays_FlattensInput()
    {
        // Path expression producing multi-element sequences with arrays
        string json = """{"orders":[{"items":[1,2]},{"items":[3,4]}]}""";
        string result = Eval("$map(orders.items, function($v){$v * 10})", json);
        Assert.Equal("[10,20,30,40]", result);
    }

    // ─── $map with non-number results causing double→element switch (L2174-2176) ──

    [Fact]
    public void Map_MixedResults_SwitchesFromDoubleToElement()
    {
        // First items return numbers (fast double path), then a string forces mode switch
        string result = Eval("""$map([1, 2, "x", 4], function($v){$type($v) = "number" ? $v * 2 : $v})""");
        Assert.Equal("""[2,4,"x",8]""", result);
    }

    // ─── $map with many items causing double-array resize (L2155-2160) ────

    [Fact]
    public void Map_ManyDoubleResults_ResizesBuffer()
    {
        // >16 items so the initial pooled double[] needs resizing
        string result = Eval("$map([1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20], function($v){$v * 2})");
        Assert.Contains("40", result);
    }

    // ─── $map zero results (L2197-2198) ──────────────────────────────────

    [Fact]
    public void Map_EmptyArray_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("$map([], function($v){$v})"));
    }

    // ─── $filter with undefined function (L2286-2287) ────────────────────

    [Fact]
    public void Filter_UndefinedFunction_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("$filter([1,2,3], nonexistent)", "{}"));
    }

    // ─── $filter with multi-element sequence containing arrays (L2304-2333) ──

    [Fact]
    public void Filter_MultiElementSequenceWithArrays_FlattensFirst()
    {
        // Multi-element sequence with array elements triggers flatten path
        string json = """{"data":[{"vals":[1,2,3]},{"vals":[4,5,6]}]}""";
        string result = Eval("$filter(data.vals, function($v){$v > 3})", json);
        Assert.Equal("[4,5,6]", result);
    }

    // ─── $spread with multi-element sequence containing arrays (L2738-2786) ──

    [Fact]
    public void Spread_ArrayOfObjects_SpreadsAll()
    {
        // Array of objects spreads each property into array items
        string result = Eval("""$spread([{"a":1,"b":2},{"c":3}])""");
        Assert.Contains("""{"a":1}""", result);
    }

    // ─── $shuffle with multi-element sequence (L5432-5440) ───────────────

    [Fact]
    public void Shuffle_MultiElementSequence_Shuffles()
    {
        // Path expression producing multi-element sequence
        string json = """{"items":[{"v":1},{"v":2},{"v":3},{"v":4},{"v":5}]}""";
        string result = Eval("$count($shuffle(items.v))", json);
        Assert.Equal("5", result);
    }

    // ─── $fromMillis with timezone and no picture (L5753-5754) ────────────

    [Fact]
    public void FromMillis_WithTimezoneNoPicture_FormatsIsoWithOffset()
    {
        string result = Eval("""$fromMillis(1510067557121, undefined, "+0500")""");
        Assert.Contains("+05:00", result);
    }

    // ─── $fromMillis undefined picture with timezone (L5769) ─────────────

    [Fact]
    public void FromMillis_UndefinedPictureWithTimezone_FormatsIso()
    {
        // picture argument evaluates to undefined → ISO 8601 with timezone
        string result = Eval("""$fromMillis(1510067557121, nothing, "+0100")""", "{}");
        Assert.Contains("+01:00", result);
    }

    // ─── $fromMillis non-numeric milliseconds (L5727-5729) ───────────────

    [Fact]
    public void FromMillis_NonNumericInput_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("""$fromMillis("not-a-number")"""));
    }

    // ─── $fromMillis with picture format (valid path) ────────────────────

    [Fact]
    public void FromMillis_WithPicture_Formats()
    {
        string result = Eval("""$fromMillis(1510067557121, "[Y]-[M01]-[D01]")""");
        Assert.Equal("""
"2017-11-07"
""".Trim(), result);
    }

    // ─── $base64decode with large string (L4235-4237 rent path) ──────────

    [Fact]
    public void Base64Decode_LargeString_UsesRentedBuffer()
    {
        // 400 'A' chars base64-encoded → decode produces 300 bytes > StackallocByteThreshold(256)
        string longText = new string('A', 400);
        string base64 = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(longText));
        string result = Eval($"""$base64decode("{base64}")""");
        Assert.Contains("AAAA", result);
    }

    // ─── $encodeUrlComponent with large string (L4289-4291 rent path) ────

    [Fact]
    public void EncodeUrlComponent_LargeString_UsesRentedBuffer()
    {
        // Many special chars → encoded result > 256 bytes
        // Use a string with 100 spaces: each ' ' → %20 = 3 bytes → 300 bytes
        string input = new string(' ', 100);
        string result = Eval($"""$encodeUrlComponent("{input}")""");
        Assert.Contains("%20", result);
    }

    // ─── $decodeUrlComponent with large string ───────────────────────────

    [Fact]
    public void DecodeUrlComponent_LargeString_UsesRentedBuffer()
    {
        // 50 × '%C3%A9' = 300 encoded bytes → decode to 50 × 'é'
        string input = string.Concat(System.Linq.Enumerable.Repeat("%C3%A9", 50));
        string result = Eval($"""$decodeUrlComponent("{input}")""");
        // Should contain the decoded 'é' characters
        Assert.NotEqual("undefined", result);
    }

    // ─── $sort with multi-element sequence ───────────────────────────────

    [Fact]
    public void Sort_MultiElementSequence_Sorts()
    {
        string json = """{"items":[{"vals":[5,3]},{"vals":[1,4]}]}""";
        string result = Eval("$sort(items.vals)", json);
        Assert.Equal("[1,3,4,5]", result);
    }

    // ─── $reduce with multi-element sequence ─────────────────────────────

    [Fact]
    public void Reduce_MultiElementSequence_Reduces()
    {
        string json = """{"items":[{"v":10},{"v":20},{"v":30}]}""";
        string result = Eval("$reduce(items.v, function($prev, $curr){$prev + $curr})", json);
        Assert.Equal("60", result);
    }

    // ─── $map with element-results resize (L2182-2187) ───────────────────

    [Fact]
    public void Map_ManyStringResults_ResizesElementBuffer()
    {
        // Map producing many non-numeric results to trigger element array resize
        string result = Eval("""$map([1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20], function($v){$string($v)})""");
        Assert.Contains(""""20"""", result);
    }

    // ─── $fromMillis with undefined picture, no timezone (L5769) ─────────

    [Fact]
    public void FromMillis_UndefinedPictureNoTimezone_FormatsIsoUtc()
    {
        // 2-arg form where picture is undefined → ISO 8601 UTC (no offset)
        string result = Eval("""$fromMillis(1510067557121, nothing)""", "{}");
        Assert.Contains("2017-11-07", result);
        Assert.Contains("Z", result);
    }

    // ─── $shuffle with undefined input (L5416-5417) ──────────────────────

    [Fact]
    public void Shuffle_UndefinedInput_ReturnsUndefined()
    {
        Assert.Equal("undefined", Eval("$shuffle(nonexistent)", "{}"));
    }

    // ─── $base64decode with non-string input fallback (L4241-4243) ───────

    [Fact]
    public void Base64Decode_NumberInput_CoercesAndDecodes()
    {
        // Non-string input that can be coerced: number → string → base64decode
        // "MTIz" is base64 for "123"
        string result = Eval("""$base64decode("MTIz")""");
        Assert.Contains("123", result);
    }

    // ─── $encodeUrlComponent with non-string input (L4300-4302) ──────────

    [Fact]
    public void EncodeUrlComponent_NonStringInput_CoercesToString()
    {
        // Number input gets coerced to string then encoded
        string result = Eval("$encodeUrlComponent(123)");
        Assert.Contains("123", result);
    }

    // ─── $decodeUrlComponent with non-string input ───────────────────────

    [Fact]
    public void DecodeUrlComponent_NonStringInput_CoercesToString()
    {
        string result = Eval("$decodeUrlComponent(123)");
        Assert.Contains("123", result);
    }
}
