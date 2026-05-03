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
}
