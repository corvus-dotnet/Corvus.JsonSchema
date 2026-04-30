// <copyright file="OrderingRegressionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonPath;
using Xunit;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Regression tests for result ordering. These exercise edge cases around
/// union selectors, descendant + multi-selector, and chained multi-result
/// segments to guard against ordering regressions during refactoring.
/// </summary>
public class OrderingRegressionTests
{
    /// <summary>
    /// Union selector: $[0,1] must return elements in selector declaration order.
    /// </summary>
    [Fact]
    public void UnionSelectorPreservesOrder()
    {
        JsonElement data = JsonElement.ParseValue("""["a","b","c"]"""u8);
        JsonElement result = JsonPathEvaluator.Default.Query("$[0,1]", data);
        Assert.Equal("""["a","b"]""", result.ToString());
    }

    /// <summary>
    /// Union selector chained with name: $[0,1].x must produce results
    /// in input-node order (node 0 first, then node 1).
    /// </summary>
    [Fact]
    public void UnionSelectorChainedWithName()
    {
        JsonElement data = JsonElement.ParseValue(
            """[{"x":1},{"x":2},{"x":3}]"""u8);
        JsonElement result = JsonPathEvaluator.Default.Query("$[0,1].x", data);
        Assert.Equal("[1,2]", result.ToString());
    }

    /// <summary>
    /// Descendant with multi-selector: $..['a','b'] must visit each node
    /// in document order and within each node apply selectors in declaration order.
    /// </summary>
    [Fact]
    public void DescendantMultiSelector()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"a":1,"b":2,"c":{"a":3,"b":4}}"""u8);
        JsonElement result = JsonPathEvaluator.Default.Query("""$..['a','b']""", data);
        Assert.Equal("[1,2,3,4]", result.ToString());
    }

    /// <summary>
    /// Descendant + wildcard + name: $..book[*].author must produce results
    /// in document order (books in array order, author for each).
    /// </summary>
    [Fact]
    public void DescendantWildcardName()
    {
        JsonElement data = JsonElement.ParseValue("""
            {
              "store": {
                "book": [
                  {"author": "A", "title": "T1"},
                  {"author": "B", "title": "T2"}
                ]
              }
            }
            """u8);
        JsonElement result = JsonPathEvaluator.Default.Query("$..book[*].author", data);
        Assert.Equal("""["A","B"]""", result.ToString());
    }

    /// <summary>
    /// Descendant name selector: $..author must collect in document order
    /// across the full tree.
    /// </summary>
    [Fact]
    public void DescendantNameDocumentOrder()
    {
        JsonElement data = JsonElement.ParseValue("""
            {
              "a": {"author": "X"},
              "b": {"author": "Y"},
              "c": {"d": {"author": "Z"}}
            }
            """u8);
        JsonElement result = JsonPathEvaluator.Default.Query("$..author", data);
        Assert.Equal("""["X","Y","Z"]""", result.ToString());
    }

    /// <summary>
    /// Filter with missing property: $[?@.x &lt; 10] on elements where .x is missing
    /// must not throw and must return false for the comparison.
    /// </summary>
    [Fact]
    public void FilterMissingPropertyDoesNotThrow()
    {
        JsonElement data = JsonElement.ParseValue(
            """[{"x":5},{"y":20},{"x":15}]"""u8);
        JsonElement result = JsonPathEvaluator.Default.Query("$[?@.x < 10]", data);
        Assert.Equal("""[{"x":5}]""", result.ToString());
    }

    /// <summary>
    /// Filter with non-numeric comparison: $[?@.x &lt; 10] where @.x is a string
    /// must return false (type mismatch → false per RFC 9535).
    /// </summary>
    [Fact]
    public void FilterNonNumericComparisonReturnsFalse()
    {
        JsonElement data = JsonElement.ParseValue(
            """[{"x":"hello"},{"x":5}]"""u8);
        JsonElement result = JsonPathEvaluator.Default.Query("$[?@.x < 10]", data);
        Assert.Equal("""[{"x":5}]""", result.ToString());
    }

    /// <summary>
    /// Wildcard followed by filter: $[*][?@.price &lt; 10] must process
    /// each wildcard result in order.
    /// </summary>
    [Fact]
    public void WildcardFollowedByFilter()
    {
        JsonElement data = JsonElement.ParseValue("""
            {
              "cheap": [{"price": 5}, {"price": 15}],
              "expensive": [{"price": 20}, {"price": 3}]
            }
            """u8);
        JsonElement result = JsonPathEvaluator.Default.Query("$.*[?@.price < 10]", data);
        Assert.Equal("""[{"price":5},{"price":3}]""", result.ToString());
    }

    /// <summary>
    /// Slice followed by name: $[0:2].x must return results for each sliced element.
    /// </summary>
    [Fact]
    public void SliceFollowedByName()
    {
        JsonElement data = JsonElement.ParseValue(
            """[{"x":10},{"x":20},{"x":30}]"""u8);
        JsonElement result = JsonPathEvaluator.Default.Query("$[0:2].x", data);
        Assert.Equal("[10,20]", result.ToString());
    }

    /// <summary>
    /// Reverse slice ordering: $[2:0:-1] must return elements in reverse order.
    /// </summary>
    [Fact]
    public void ReverseSliceOrdering()
    {
        JsonElement data = JsonElement.ParseValue("""[0,1,2,3,4]"""u8);
        JsonElement result = JsonPathEvaluator.Default.Query("$[2:0:-1]", data);
        Assert.Equal("[2,1]", result.ToString());
    }

    /// <summary>
    /// Descendant producing duplicates: $.a..b where the tree has nested b's
    /// must produce all matches in document order including nested ones.
    /// </summary>
    [Fact]
    public void DescendantNestedDuplicates()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"a":{"b":{"b":1}}}"""u8);
        JsonElement result = JsonPathEvaluator.Default.Query("$.a..b", data);
        Assert.Equal("""[{"b":1},1]""", result.ToString());
    }
}
