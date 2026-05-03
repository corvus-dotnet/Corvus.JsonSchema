// <copyright file="BuiltInFunctionCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests targeting uncovered branches in <see cref="BuiltInFunctions"/>,
/// identified from merged Cobertura coverage data.
/// </summary>
public class BuiltInFunctionCoverageTests
{
    private static string Eval(string expression, string data = "null")
    {
        return JsonataEvaluator.Default.EvaluateToString(expression, data) ?? "undefined";
    }

    // ─── $filter with array flattening (lines 2244-2280) ──────────────

    [Fact]
    public void Filter_MultiValuedSequenceContainingArrays_Flattens()
    {
        // Multi-valued sequence with arrays triggers the flatten path in $filter
        string data = """
        {
          "Account": {
            "Order": [
              {"Product": [{"Price": 5, "Name": "Cheap"}, {"Price": 15, "Name": "Mid"}]},
              {"Product": {"Price": 25, "Name": "Expensive"}}
            ]
          }
        }
        """;
        string result = Eval(
            """$filter(Account.Order.Product, function($v){$v.Price > 10})""",
            data);
        Assert.Contains("Mid", result);
        Assert.Contains("Expensive", result);
        Assert.DoesNotContain("Cheap", result);
    }

    [Fact]
    public void Filter_MultiValuedWithArrays_PreservesNonArrayElements()
    {
        string data = """
        {
          "groups": [
            {"items": [1, 2, 3]},
            {"items": [4, 5, 6]}
          ]
        }
        """;
        string result = Eval(
            """$filter(groups.items, function($v){$v > 3})""",
            data);
        Assert.Equal("""[4,5,6]""", result);
    }

    // ─── $spread multi-element sequence with arrays (lines 2669-2718) ──

    [Fact]
    public void Spread_MultiElementSequenceWithArrays()
    {
        // Multi-valued sequence where elements are arrays of objects
        // departments.people produces [array1, array2] as multi-value sequence
        string data = """
        {
          "departments": [
            {"people": [{"name": "Alice"}, {"name": "Bob"}]},
            {"people": [{"name": "Charlie"}]}
          ]
        }
        """;
        // $spread on a path producing multi-valued sequence with arrays
        string result = Eval("$spread(departments.people)", data);
        Assert.NotNull(result);
        Assert.Contains("name", result);
    }

    [Fact]
    public void Spread_MultiElementSequenceObjects()
    {
        // Multi-valued sequence of objects (no arrays) — exercises the object path (2699-2704)
        string data = """
        {
          "items": [
            {"a": 1, "b": 2},
            {"c": 3}
          ]
        }
        """;
        string result = Eval("$spread(items)", data);
        Assert.Contains("\"a\"", result);
    }

    [Fact]
    public void Spread_SingleObject()
    {
        string result = Eval("""$spread({"x":1,"y":2})""");
        Assert.Contains("\"x\"", result);
        Assert.Contains("\"y\"", result);
    }

    // ─── $single without predicate, multi-valued sequence (lines 2896-2906) ────

    [Fact]
    public void Single_NoPredicate_MultiValuedSequenceOfOne()
    {
        // Path producing single-element multi-valued sequence → returns the element (line 2906)
        string data = """{"items": [{"name": "only"}]}""";
        string result = Eval("$single(items)", data);
        Assert.Equal("""{"name": "only"}""", result);
    }

    [Fact]
    public void Single_NoPredicate_UndefinedInput_ReturnsUndefined()
    {
        string result = Eval("$single(nothing)", """{"something": 1}""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void Single_NoPredicate_MultiValuedSequenceMultiple_ThrowsD3138()
    {
        // Path producing multi-valued sequence with Count > 1 (line 2901-2903)
        string data = """
        {
          "groups": [
            {"value": 1},
            {"value": 2}
          ]
        }
        """;
        var ex = Assert.Throws<JsonataException>(
            () => Eval("$single(groups.value)", data));
        Assert.Equal("D3138", ex.Code);
    }

    [Fact]
    public void Single_NoPredicate_EmptyMultiValuedSequence_ThrowsD3139()
    {
        // Path producing an empty array → throws D3139 (line 2896-2898)
        string data = """{"items": []}""";
        var ex = Assert.Throws<JsonataException>(
            () => Eval("$single(items)", data));
        Assert.Equal("D3139", ex.Code);
    }

    // ─── $shuffle multi-element sequence (lines 5353-5366) ─────────────

    [Fact]
    public void Shuffle_MultiElementSequence()
    {
        // Multi-valued sequence (not a singleton array) triggers the sequence path
        string data = """
        {
          "groups": [
            {"items": [1, 2]},
            {"items": [3, 4]}
          ]
        }
        """;
        string result = Eval("$count($shuffle(groups.items))", data);
        // Should have all 4 elements regardless of order
        Assert.Equal("4", result);
    }

    [Fact]
    public void Shuffle_SingletonNonArray_ReturnsAsIs()
    {
        string result = Eval("""$shuffle(42)""");
        Assert.Equal("42", result);
    }

    // ─── $map with index parameter ─────────────────────────────────────

    [Fact]
    public void Map_WithIndexParameter()
    {
        string result = Eval("""$map([10,20,30], function($v, $i){$i})""");
        Assert.Equal("[0,1,2]", result);
    }

    // ─── $reduce edge cases ────────────────────────────────────────────

    [Fact]
    public void Reduce_SingleElement_ReturnsElement()
    {
        string result = Eval("""$reduce([42], function($prev, $curr){$prev + $curr})""");
        Assert.Equal("42", result);
    }

    // ─── $zip ──────────────────────────────────────────────────────────

    [Fact]
    public void Zip_UnequalLengthArrays()
    {
        // JSONata $zip truncates to the shortest array
        string result = Eval("""$zip([1,2,3], ["a","b"])""");
        Assert.Equal("""[[1,"a"],[2,"b"]]""", result);
    }

    // ─── $sort ─────────────────────────────────────────────────────────

    [Fact]
    public void Sort_WithCustomComparator()
    {
        string result = Eval("""$sort([3,1,2], function($a,$b){$a > $b})""");
        Assert.Equal("[1,2,3]", result);
    }

    [Fact]
    public void Sort_StringsDefaultOrder()
    {
        string result = Eval("""$sort(["banana", "apple", "cherry"])""");
        Assert.Equal("""["apple","banana","cherry"]""", result);
    }

    // ─── Context-argument patterns (lines 1414-1418, 3133-3138) ───────

    [Fact]
    public void Contains_ContextArgPattern()
    {
        // $contains with 1 arg uses context as the string (lines 1414-1418)
        // Input is the string, expression uses implicit context
        string result = Eval("$contains(\"world\")", "\"hello world\"");
        Assert.Equal("true", result);
    }

    [Fact]
    public void Contains_ContextArgNoMatch()
    {
        string result = Eval("$contains(\"xyz\")", "\"hello\"");
        Assert.Equal("false", result);
    }

    [Fact]
    public void Split_ContextArgPattern()
    {
        // $split with 1 arg uses context as the string (lines 1316-1321)
        string result = Eval("$split(\",\")", "\"a,b,c\"");
        Assert.Equal("""["a","b","c"]""", result);
    }

    [Fact]
    public void Split_ContextArgWithLimit()
    {
        // $split with 2 args — still needs context (2 args = str + pattern)
        string data = """{"text": "one-two-three"}""";
        string result = Eval("$split(text, \"-\", 2)", data);
        Assert.Equal("""["one","two"]""", result);
    }

    [Fact]
    public void Match_ContextArgPattern()
    {
        // $match with 1 arg uses context as the string (lines 3133-3138)
        string result = Eval("$match(/[0-9]+/)", "\"abc123def456\"");
        Assert.Contains("\"match\"", result);
        Assert.Contains("123", result);
    }

    // ─── $number with hex/binary/octal (TryParseSpecialRadix) ─────────

    [Fact]
    public void Number_HexPrefix()
    {
        // Covers TryParseSpecialRadix hex path (line 8744)
        string result = Eval("""$number("0xFF")""");
        Assert.Equal("255", result);
    }

    [Fact]
    public void Number_HexUpperCase()
    {
        string result = Eval("""$number("0XAB")""");
        Assert.Equal("171", result);
    }

    [Fact]
    public void Number_BinaryPrefix()
    {
        // Covers TryParseSpecialRadix binary path (line 8756)
        string result = Eval("""$number("0b1010")""");
        Assert.Equal("10", result);
    }

    [Fact]
    public void Number_OctalPrefix()
    {
        // Covers TryParseSpecialRadix octal path (line 8775)
        string result = Eval("""$number("0o17")""");
        Assert.Equal("15", result);
    }

    [Fact]
    public void Number_InvalidBinary_ThrowsD3030()
    {
        // Invalid binary digits (not 0 or 1) — error path (line 8763)
        var ex = Assert.Throws<JsonataException>(() => Eval("""$number("0b1234")"""));
        Assert.StartsWith("D3030", ex.Message);
    }

    [Fact]
    public void Number_InvalidOctal_ThrowsD3030()
    {
        // Invalid octal digits (8, 9) — error path (line 8781)
        var ex = Assert.Throws<JsonataException>(() => Eval("""$number("0o89")"""));
        Assert.StartsWith("D3030", ex.Message);
    }

    [Fact]
    public void Number_InvalidHex_ThrowsD3030()
    {
        // Invalid hex string — error path (line 8752)
        var ex = Assert.Throws<JsonataException>(() => Eval("""$number("0xGG")"""));
        Assert.StartsWith("D3030", ex.Message);
    }

    [Fact]
    public void Number_EmptyBinary_ThrowsD3030()
    {
        // Empty binary after prefix — (line 8772 digits.Length == 0)
        var ex = Assert.Throws<JsonataException>(() => Eval("""$number("0b")"""));
        Assert.StartsWith("D3030", ex.Message);
    }

    // ─── $substring with supplementary Unicode (CodePointsToString) ───

    [Fact]
    public void Substring_SupplementaryUnicode()
    {
        // Characters above U+FFFF trigger supplementary code point path
        // 🎉 is U+1F389 (above BMP), needs surrogate pair handling
        string result = Eval("""$substring("A🎉B", 1, 1)""");
        Assert.Equal("\"🎉\"", result);
    }

    [Fact]
    public void Substring_MultipleSurrogates()
    {
        // Multiple supplementary characters
        string result = Eval("""$substring("🌍🌎🌏", 1, 2)""");
        Assert.Equal("\"🌎🌏\"", result);
    }

    // ─── $replace on multi-value pattern ──────────────────────────────

    [Fact]
    public void Replace_ContextArgPattern()
    {
        // $replace with context arg (2 args: pattern + replacement, context provides string)
        string result = Eval("$replace(\"world\", \"earth\")", "\"hello world\"");
        Assert.Equal("\"hello earth\"", result);
    }
}
