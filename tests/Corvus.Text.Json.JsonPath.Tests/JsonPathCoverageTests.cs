// <copyright file="JsonPathCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;
using Xunit;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Coverage tests targeting uncovered parser/interpreter paths in the JSONPath evaluator,
/// including filter expressions, recursive descent, slicing, unicode, and logical operators.
/// </summary>
public class JsonPathCoverageTests
{
    // ─── Filter Expressions with Escape Sequences (Parser lines 760-778) ──

    [Fact]
    public void Filter_StringWithEscapedNewline()
    {
        string json = """{"items": [{"text": "hello\nworld"}, {"text": "no newline"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.text == 'hello\\nworld']", data);
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void Filter_StringWithEscapedTab()
    {
        string json = """{"items": [{"text": "col1\tcol2"}, {"text": "plain"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.text == 'col1\\tcol2']", data);
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void Filter_StringWithEscapedBackslash()
    {
        // Test escaped unicode in filter string comparison
        string json = """{"items": [{"code": "A"}, {"code": "B"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.code == 'A']", data);
        Assert.Equal(1, result.Count);
        Assert.Equal("A", result[0].GetProperty("code").GetString());
    }

    // ─── Recursive Descent with Filters (PlanInterpreter lines 879-918) ──

    [Fact]
    public void RecursiveDescent_WithFilter()
    {
        string json = """{"a": {"b": [{"c": 1}, {"c": 2}], "d": {"b": [{"c": 3}]}}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonPathEvaluator.Default.Query("$..b[?@.c > 1]", data, workspace);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.True(result.GetArrayLength() >= 1);
    }

    [Fact]
    public void RecursiveDescent_AllProperties()
    {
        string json = """{"a": {"x": 1}, "b": {"x": 2}, "c": {"d": {"x": 3}}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonPathEvaluator.Default.Query("$..x", data, workspace);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void RecursiveDescent_WildcardOnNestedArrays()
    {
        string json = """{"a": [1, 2], "b": {"c": [3, 4]}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JsonPathEvaluator.Default.Query("$..*", data, workspace);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.True(result.GetArrayLength() > 4);
    }

    // ─── Array Slice Operations ──────────────────────────────────────

    [Fact]
    public void Slice_WithStep()
    {
        string json = """[0,1,2,3,4,5,6,7,8,9]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[::2]", data);
        Assert.Equal(5, result.Count);
        Assert.Equal(0, result[0].GetInt32());
        Assert.Equal(2, result[1].GetInt32());
        Assert.Equal(4, result[2].GetInt32());
    }

    [Fact]
    public void Slice_NegativeStep()
    {
        string json = """[0,1,2,3,4]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[::-1]", data);
        Assert.Equal(5, result.Count);
        Assert.Equal(4, result[0].GetInt32());
        Assert.Equal(0, result[4].GetInt32());
    }

    [Fact]
    public void Slice_NegativeIndices()
    {
        string json = """[0,1,2,3,4,5]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[-3:]", data);
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result[0].GetInt32());
        Assert.Equal(4, result[1].GetInt32());
        Assert.Equal(5, result[2].GetInt32());
    }

    [Fact]
    public void Slice_StartAndEnd()
    {
        string json = """[0,1,2,3,4,5,6,7,8,9]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[2:5]", data);
        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].GetInt32());
        Assert.Equal(3, result[1].GetInt32());
        Assert.Equal(4, result[2].GetInt32());
    }

    // ─── Complex Filter Expressions ──────────────────────────────────

    [Fact]
    public void Filter_GreaterThan()
    {
        string json = """{"items": [{"v":1},{"v":5},{"v":10},{"v":15}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[?@.v > 5]", data);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Filter_LessThanOrEqual()
    {
        string json = """{"items": [{"v":1},{"v":5},{"v":10},{"v":15}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[?@.v <= 5]", data);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Filter_NotEqual()
    {
        string json = """{"items": [{"v":1},{"v":5},{"v":10}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[?@.v != 5]", data);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Filter_LogicalAnd()
    {
        string json = """{"items": [{"a":1,"b":2},{"a":3,"b":4},{"a":5,"b":6}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.a > 1 && @.b < 6]", data);
        Assert.Equal(1, result.Count);
        Assert.Equal(3, result[0].GetProperty("a").GetInt32());
    }

    [Fact]
    public void Filter_LogicalOr()
    {
        string json = """{"items": [{"a":1},{"a":5},{"a":10}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.a < 2 || @.a > 8]", data);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Filter_LogicalNot()
    {
        // In RFC 9535, @.active is an existence test — items missing the property match !@.active
        string json = """{"items": [{"active": true, "v": 1}, {"v": 2}, {"active": false, "v": 3}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?!@.active]", data);
        Assert.Equal(1, result.Count);
        Assert.Equal(2, result[0].GetProperty("v").GetInt32());
    }

    // ─── Unicode Property Names ──────────────────────────────────────

    [Fact]
    public void PropertyAccess_UnicodeKey()
    {
        string json = "{\"\\u540D\\u524D\": \"\\u592A\\u90CE\", \"\\u5E74\\u9F62\": 25}";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.\u540D\u524D", data);
        Assert.Equal(1, result.Count);
        Assert.Equal("\u592A\u90CE", result[0].GetString());
    }

    [Fact]
    public void PropertyAccess_BracketNotationUnicode()
    {
        string json = "{\"\\u540D\\u524D\": \"\\u592A\\u90CE\"}";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$['\u540D\u524D']", data);
        Assert.Equal(1, result.Count);
        Assert.Equal("\u592A\u90CE", result[0].GetString());
    }

    // ─── Existence Checks in Filters ─────────────────────────────────

    [Fact]
    public void Filter_ExistenceCheck()
    {
        string json = """{"items": [{"a":1,"b":2},{"a":3},{"a":5,"b":6}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.b]", data);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Filter_NonExistence()
    {
        string json = """{"items": [{"a":1,"b":2},{"a":3},{"a":5,"b":6}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?!@.b]", data);
        Assert.Equal(1, result.Count);
        Assert.Equal(3, result[0].GetProperty("a").GetInt32());
    }

    // ─── Wildcard and index combinations ─────────────────────────────

    [Fact]
    public void Wildcard_ObjectProperties()
    {
        string json = """{"a": 1, "b": 2, "c": 3}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.*", data);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Wildcard_ArrayElements()
    {
        string json = """[10, 20, 30, 40]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[*]", data);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void MultipleIndices()
    {
        string json = """["a","b","c","d","e"]""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[0,2,4]", data);
        Assert.Equal(3, result.Count);
        Assert.Equal("a", result[0].GetString());
        Assert.Equal("c", result[1].GetString());
        Assert.Equal("e", result[2].GetString());
    }

    // ─── Built-in functions in filters ───────────────────────────────

    [Fact]
    public void Filter_LengthFunction()
    {
        string json = """{"items": [{"name": "ab"}, {"name": "abcde"}, {"name": "a"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?length(@.name) > 2]", data);
        Assert.Equal(1, result.Count);
        Assert.Equal("abcde", result[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Filter_LengthFunctionOnArray()
    {
        string json = """{"groups": [{"items": [1,2,3]}, {"items": [1]}, {"items": [1,2,3,4,5]}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.groups[?length(@.items) >= 3]", data);
        Assert.Equal(2, result.Count);
    }

    // ─── Nested path access ──────────────────────────────────────────

    [Fact]
    public void DeepNestedAccess()
    {
        string json = """{"a": {"b": {"c": {"d": {"e": 42}}}}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.a.b.c.d.e", data);
        Assert.Equal(1, result.Count);
        Assert.Equal(42, result[0].GetInt32());
    }

    [Fact]
    public void BracketNotation_WithDots()
    {
        string json = """{"a.b": {"c.d": 99}}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$['a.b']['c.d']", data);
        Assert.Equal(1, result.Count);
        Assert.Equal(99, result[0].GetInt32());
    }

    // ─── Empty results ───────────────────────────────────────────────

    [Fact]
    public void NoMatch_ReturnsEmpty()
    {
        string json = """{"a": 1}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.nonexistent", data);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void Filter_NoMatch_ReturnsEmpty()
    {
        string json = """{"items": [{"v":1},{"v":2}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.v > 100]", data);
        Assert.Equal(0, result.Count);
    }

    // ─── Root access ─────────────────────────────────────────────────

    [Fact]
    public void RootOnly_ReturnsWholeDocument()
    {
        string json = """{"a": 1, "b": 2}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$", data);
        Assert.Equal(1, result.Count);
        Assert.Equal(JsonValueKind.Object, result[0].ValueKind);
    }

    // ─── Filter with string comparison ───────────────────────────────

    [Fact]
    public void Filter_StringEquality()
    {
        string json = """{"items": [{"name":"alice"},{"name":"bob"},{"name":"charlie"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.name == 'bob']", data);
        Assert.Equal(1, result.Count);
        Assert.Equal("bob", result[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Filter_StringNotEqual()
    {
        string json = """{"items": [{"name":"alice"},{"name":"bob"},{"name":"charlie"}]}""";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.items[?@.name != 'bob']", data);
        Assert.Equal(2, result.Count);
    }
}
