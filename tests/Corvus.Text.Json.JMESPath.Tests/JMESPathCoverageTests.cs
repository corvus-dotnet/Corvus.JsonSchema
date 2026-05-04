// <copyright file="JMESPathCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using Xunit;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Coverage tests targeting uncovered compiler/lexer paths in the JMESPath evaluator,
/// including nested projections, flatten, functions, unicode/escape sequences, and pipes.
/// </summary>
public class JMESPathCoverageTests
{
    // ─── Nested Array Projections (Compiler lines 1257-1308) ─────────

    [Fact]
    public void NestedArrayProjection_MultipleLevels()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"nested": [{"value": 1}, {"value": 2}]}, {"nested": [{"value": 3}]}]}"""));
        // With flatten ([]) instead of wildcard ([*]), nested arrays are flattened
        JsonElement result = JMESPathEvaluator.Default.Search("items[].nested[].value", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void NestedArrayProjection_WildcardNests()
    {
        // [*] projections nest: items[*].nested[*].value yields [[1,2],[3]]
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"nested": [{"value": 1}, {"value": 2}]}, {"nested": [{"value": 3}]}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("items[*].nested[*].value", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
    }

    [Fact]
    public void NestedArrayProjection_DeepNesting()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"a": [{"b": [{"c": [{"d": 1}, {"d": 2}]}, {"c": [{"d": 3}]}]}]}"""));
        // Flatten projections to get flat result
        JsonElement result = JMESPathEvaluator.Default.Search("a[].b[].c[].d", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    // ─── Flatten Identity Projection (Compiler lines 1105-1147) ──────

    [Fact]
    public void FlattenIdentity_NestedArrays()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[[1,2],[3,4],[5,6]]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("[]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(6, result.GetArrayLength());
    }

    [Fact]
    public void Flatten_ChainedOperations()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": [[1,2],[3,4]]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("data[]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(4, result.GetArrayLength());
    }

    [Fact]
    public void Flatten_WithProjection()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [[{"v":1},{"v":2}],[{"v":3}]]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("items[].v", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    // ─── sum() with MultiSelectList (Compiler.Functions lines 101-124) ──

    [Fact]
    public void Sum_MultiSelectList()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"a": 1, "b": 2, "c": 3}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sum([a, b, c])", data);
        Assert.Equal(6, result.GetDouble());
    }

    [Fact]
    public void Sum_WithArrayProjection()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"v": 10}, {"v": 20}, {"v": 30}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sum(items[*].v)", data);
        Assert.Equal(60, result.GetDouble());
    }

    // ─── Sort/Reverse on Non-Arrays (Compiler lines 206-226) ─────────

    [Fact]
    public void Sort_NullInput_ThrowsInvalidType()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": null}"""));
        Assert.Throws<JMESPathException>(() => JMESPathEvaluator.Default.Search("sort(data)", data));
    }

    [Fact]
    public void SortBy_NullInput_ThrowsInvalidType()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": null}"""));
        Assert.Throws<JMESPathException>(() => JMESPathEvaluator.Default.Search("sort_by(items, &name)", data));
    }

    [Fact]
    public void Reverse_String_ReversesChars()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"text": "hello"}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("reverse(text)", data);
        Assert.Equal("olleh", result.GetString());
    }

    [Fact]
    public void Sort_StringArray()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": ["cherry", "apple", "banana"]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort(items)", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal("apple", result[0].GetString());
        Assert.Equal("banana", result[1].GetString());
        Assert.Equal("cherry", result[2].GetString());
    }

    // ─── Unicode/Escape Sequences in Lexer (lines 395-418) ───────────

    [Fact]
    public void Literal_UnicodeEscape_InJsonLiteral()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        // Backtick-delimited JSON literal with unicode escape
        JsonElement result = JMESPathEvaluator.Default.Search("""`"caf\u00e9"`""", data);
        Assert.Equal("caf\u00e9", result.GetString());
    }

    [Fact]
    public void PropertyAccess_UnicodeKey()
    {
        // JSON property name with non-ASCII characters
        string json = "{\"caf\u00e9\": \"latte\"}";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        JsonElement result = JMESPathEvaluator.Default.Search("\"caf\u00e9\"", data);
        Assert.Equal("latte", result.GetString());
    }

    // ─── Long Raw String (Lexer lines 246-256 — buffer growth) ───────

    [Fact]
    public void LongProperty_BufferGrowth()
    {
        // Create a property value longer than initial buffer (>256 chars)
        string longValue = new string('a', 2000);
        string json = "{\"key\": \"" + longValue + "\"}";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        JsonElement result = JMESPathEvaluator.Default.Search("key", data);
        Assert.Equal(2000, result.GetString()!.Length);
    }

    [Fact]
    public void LongJsonLiteral_BufferGrowth()
    {
        // Force lexer buffer growth with a long JSON literal
        string longArray = "[" + string.Join(",", Enumerable.Range(0, 500)) + "]";
        string expression = "`" + longArray + "`";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes("""{}"""));
        JsonElement result = JMESPathEvaluator.Default.Search(expression, data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(500, result.GetArrayLength());
    }

    // ─── Raw String Escape Sequences (Lexer lines 237-242) ───────────

    [Fact]
    public void RawString_EscapedQuote()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        // Raw string with escaped single quote: 'it\'s here'
        JsonElement result = JMESPathEvaluator.Default.Search("'it\\'s here'", data);
        Assert.Equal("it's here", result.GetString());
    }

    [Fact]
    public void RawString_BackslashLiteral()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        // In JMESPath raw strings, backslash is literal (only \' is an escape)
        JsonElement result = JMESPathEvaluator.Default.Search("'C:\\Users'", data);
        Assert.Equal("C:\\Users", result.GetString());
    }

    // ─── Pipe expressions ────────────────────────────────────────────

    [Fact]
    public void Pipe_SortThenFirst()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [3,1,4,1,5,9]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort(items) | [0]", data);
        Assert.Equal(1, result.GetDouble());
    }

    [Fact]
    public void Pipe_ChainedMultiple()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"name":"z"},{"name":"a"},{"name":"m"}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("items[*].name | sort(@) | [0]", data);
        Assert.Equal("a", result.GetString());
    }

    [Fact]
    public void Pipe_LengthAfterProjection()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"a":1},{"a":2},{"a":3}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("items[*].a | length(@)", data);
        Assert.Equal(3, result.GetDouble());
    }

    // ─── Flatten with nested object projection ───────────────────────

    [Fact]
    public void ObjectValueProjection()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"ops": {"a": {"status": "ok"}, "b": {"status": "fail"}, "c": {"status": "ok"}}}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("ops.*.status", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void Filter_WithObjectWildcard()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"machines": {"a": {"state": "running"}, "b": {"state": "stopped"}, "c": {"state": "running"}}}"""));
        // Use pipe to stop projection before applying filter
        JsonElement result = JMESPathEvaluator.Default.Search("machines.* | [?state == 'running']", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
    }

    // ─── Additional function coverage ────────────────────────────────

    [Fact]
    public void MinBy_ExpressionReference()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"name":"b","age":30},{"name":"a","age":10},{"name":"c","age":20}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("min_by(items, &age)", data);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("a", result.GetProperty("name").GetString());
    }

    [Fact]
    public void MaxBy_ExpressionReference()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"name":"b","age":30},{"name":"a","age":10},{"name":"c","age":20}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("max_by(items, &age)", data);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("b", result.GetProperty("name").GetString());
    }

    [Fact]
    public void Keys_ReturnsPropertyNames()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"foo": 1, "bar": 2, "baz": 3}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("keys(@)", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void Values_ReturnsPropertyValues()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"foo": 1, "bar": 2, "baz": 3}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("values(@)", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void Merge_CombinesObjects()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"a": {"x": 1}, "b": {"y": 2}}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("merge(a, b)", data);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(1, result.GetProperty("x").GetDouble());
        Assert.Equal(2, result.GetProperty("y").GetDouble());
    }

    [Fact]
    public void Avg_ComputesAverage()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"nums": [10, 20, 30]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("avg(nums)", data);
        Assert.Equal(20, result.GetDouble());
    }

    [Fact]
    public void Floor_ReturnsFloorValue()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("floor(`3.7`)", data);
        Assert.Equal(3, result.GetDouble());
    }

    [Fact]
    public void Ceil_ReturnsCeilValue()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("ceil(`3.2`)", data);
        Assert.Equal(4, result.GetDouble());
    }

    [Fact]
    public void Abs_ReturnsAbsoluteValue()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("abs(`-5`)", data);
        Assert.Equal(5, result.GetDouble());
    }

    // ─── Multi-select with expressions ───────────────────────────────

    [Fact]
    public void MultiSelectHash_WithFunctionCalls()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [1,2,3,4,5]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search(
            "{total: sum(items), count: length(items), average: avg(items)}", data);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(15, result.GetProperty("total").GetDouble());
        Assert.Equal(5, result.GetProperty("count").GetDouble());
        Assert.Equal(3, result.GetProperty("average").GetDouble());
    }

    [Fact]
    public void MultiSelectList_WithProjections()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"people": [{"first":"Alice","last":"Smith"},{"first":"Bob","last":"Jones"}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("people[*].[first, last]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        // Each element is a two-element array
        Assert.Equal(2, result[0].GetArrayLength());
        Assert.Equal("Alice", result[0][0].GetString());
        Assert.Equal("Smith", result[0][1].GetString());
    }

    // ─── Not expression in filters ───────────────────────────────────

    [Fact]
    public void Filter_NotExpression()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"active": true}, {"active": false}, {"active": true}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("items[?!active]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(1, result.GetArrayLength());
    }

    [Fact]
    public void Filter_ComparisonLessThan()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"v":1},{"v":5},{"v":10}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("items[?v < `5`]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(1, result.GetArrayLength());
        Assert.Equal(1, result[0].GetProperty("v").GetDouble());
    }

    [Fact]
    public void Filter_ComparisonLessOrEqual()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"v":1},{"v":5},{"v":10}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("items[?v <= `5`]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
    }

    [Fact]
    public void Filter_ComparisonNotEqual()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"v":1},{"v":5},{"v":10}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("items[?v != `5`]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
    }

    // ─── Fused Pipeline Non-Array Error Handling (Compiler lines 206-226) ──

    [Fact]
    public void FusedPipeline_Sort_OnNonArray_Throws()
    {
        // Fused pipeline: sort(data) | [*].name. Source is non-array → Sort requires array.
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": "not an array"}"""));
        Assert.Throws<JMESPathException>(() => JMESPathEvaluator.Default.Search("sort(data) | [*].name", data));
    }

    [Fact]
    public void FusedPipeline_SortBy_OnNonArray_Throws()
    {
        // Fused pipeline: sort_by(data, &name) | [*].name. Source is non-array → SortBy requires array.
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": 42}"""));
        Assert.Throws<JMESPathException>(() => JMESPathEvaluator.Default.Search("sort_by(data, &name) | [*].name", data));
    }

    [Fact]
    public void FusedPipeline_Reverse_OnNonArray_Throws()
    {
        // Fused pipeline: reverse(data) | [*].name. Source is non-array → Reverse throws.
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": {"key": "value"}}"""));
        Assert.Throws<JMESPathException>(() => JMESPathEvaluator.Default.Search("reverse(data) | [*].name", data));
    }

    [Fact]
    public void FusedPipeline_NoBarrierOnNonArray_ReturnsDefault()
    {
        // Fused pipeline with Project + Filter stages but no Sort/SortBy/Reverse.
        // When source is non-array, falls through to return default (line 226).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": "hello"}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("data[*].name | [?@ != 'x']", data);
        Assert.True(result.IsNullOrUndefined());
    }

    // ─── Streaming Flatten with Projection (Compiler lines 423-444) ─────────

    [Fact]
    public void StreamingFlatten_WithProjection_ArrayResult()
    {
        // sort(items) | [].vals: after sort barrier, flatten stage with projection.
        // Each element's 'vals' is an array → iterate inner (lines 434-439).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"vals":[3,4]},{"vals":[1,2]}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(items, &vals[0]) | [].vals", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        // sorted by first val: [{vals:[1,2]},{vals:[3,4]}], flatten vals: [1,2,3,4]
        Assert.Equal(4, result.GetArrayLength());
        Assert.Equal(1, result[0].GetDouble());
        Assert.Equal(4, result[3].GetDouble());
    }

    [Fact]
    public void StreamingFlatten_WithProjection_ScalarResult()
    {
        // sort(items) | [].name: projected values are scalars (not arrays) → line 441-443.
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"name":"b","v":2},{"name":"a","v":1}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(items, &v) | [].name", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        Assert.Equal("a", result[0].GetString());
        Assert.Equal("b", result[1].GetString());
    }

    [Fact]
    public void StreamingFlatten_WithProjection_NullSkipped()
    {
        // sort(items) | [].missing: projected null/undefined → return early (lines 429-432).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"v":2},{"v":1,"name":"a"}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(items, &v) | [].name", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        // First element has no 'name' → skipped. Second has name → included.
        Assert.Equal(1, result.GetArrayLength());
        Assert.Equal("a", result[0].GetString());
    }

    // ─── Streaming HashProject Non-Terminal (Compiler lines 475-480) ─────────

    [Fact]
    public void StreamingPipeline_HashProject_NonTerminal()
    {
        // sort(items) | [*].{n: name} | [?n != 'z']: HashProject is not the terminal stage.
        // Hits line 475-480: BuildHashObject then continue to Filter.
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [{"name":"c","v":3},{"name":"a","v":1},{"name":"b","v":2}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(items, &v) | [*].{n: name, v: v} | [?v > `1`]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        Assert.Equal("b", result[0].GetProperty("n").GetString());
        Assert.Equal("c", result[1].GetProperty("n").GetString());
    }

    // ─── Streaming Flatten without Projection (Compiler lines 446-460) ──────

    [Fact]
    public void StreamingFlatten_WithoutProjection_Array()
    {
        // reverse(items) | []: Flatten without projection, elements are arrays → expand (lines 449-454).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [[3,4],[1,2],[5,6]]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("reverse(items) | []", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        // reversed: [[5,6],[1,2],[3,4]], flatten: [5,6,1,2,3,4]
        Assert.Equal(6, result.GetArrayLength());
        Assert.Equal(5, result[0].GetDouble());
        Assert.Equal(4, result[5].GetDouble());
    }

    [Fact]
    public void StreamingFlatten_WithoutProjection_NonArray()
    {
        // sort(items) | []: elements are non-array → pass through (lines 456-458).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": ["cherry","apple","banana"]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort(items) | []", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal("apple", result[0].GetString());
    }

    // ─── CompileSlice Negative Step (Compiler lines 1506-1537) ──────────────

    [Fact]
    public void Slice_NegativeStep()
    {
        // [::-1] triggers CompileSlice with step < 0 (lines 1532-1537).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"items": [1,2,3,4,5]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("items[::-1]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(5, result.GetArrayLength());
        Assert.Equal(5, result[0].GetDouble());
        Assert.Equal(1, result[4].GetDouble());
    }

    [Fact]
    public void Slice_NegativeStep_WithStartStop()
    {
        // [3:0:-1]: from index 3 down to (exclusive) 0
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[10,20,30,40,50]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("[3:0:-1]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        // indices 3,2,1 → [40,30,20]
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(40, result[0].GetDouble());
        Assert.Equal(20, result[2].GetDouble());
    }

    [Fact]
    public void Slice_PositiveStep()
    {
        // [::2] triggers CompileSlice with step > 0, step != 1 (lines 1525-1530).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[1,2,3,4,5]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("[::2]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        // indices 0,2,4 → [1,3,5]
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(1, result[0].GetDouble());
        Assert.Equal(3, result[1].GetDouble());
        Assert.Equal(5, result[2].GetDouble());
    }

    [Fact]
    public void Slice_WithStartStop()
    {
        // [1:4]: positive step with explicit bounds (lines 1503-1504).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[10,20,30,40,50]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("[1:4]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        // [20,30,40]
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(20, result[0].GetDouble());
        Assert.Equal(40, result[2].GetDouble());
    }

    // ─── Flatten Identity with ListProjection Left (Compiler lines 1104-1147) ─

    [Fact]
    public void FlattenIdentity_ListProjectionLeft_ArrayItems()
    {
        // data[*].items[] — FlattenProjection(Left=ListProjection(data, items), Right=@)
        // Projected values are arrays → flatten via inner iteration (lines 1128-1133).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": [{"items": [1,2]}, {"items": [3,4]}, {"items": [5]}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("data[*].items[]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(5, result.GetArrayLength());
        Assert.Equal(1, result[0].GetDouble());
        Assert.Equal(5, result[4].GetDouble());
    }

    [Fact]
    public void FlattenIdentity_ListProjectionLeft_NullSkipped()
    {
        // When projected value (items) is null → continue (lines 1123-1125).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": [{"items": [1,2]}, {"items": null}, {"items": [3]}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("data[*].items[]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void FlattenIdentity_ListProjectionLeft_MissingProperty()
    {
        // When element has no 'items' property → projected is undefined → continue.
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": [{"items": [1,2]}, {"other": "x"}, {"items": [3]}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("data[*].items[]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void FlattenIdentity_ListProjectionLeft_NonArrayAdded()
    {
        // When projected value is not array (scalar) → builder.Add directly (lines 1135-1137).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": [{"items": "scalar1"}, {"items": "scalar2"}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("data[*].items[]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        Assert.Equal("scalar1", result[0].GetString());
        Assert.Equal("scalar2", result[1].GetString());
    }

    // ─── Flatten with Projection + ListProjection Left (Compiler lines 1257-1308) ─

    [Fact]
    public void FlattenProjection_ListProjectionLeft_ArrayItems()
    {
        // data[*].items[].value — FlattenProjection(Left=ListProjection(data, items), Right=value)
        // Projected inner results are arrays → iterate + project (lines 1281-1290).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": [{"items": [{"value": 1}, {"value": 2}]}, {"items": [{"value": 3}]}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("data[*].items[].value", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(1, result[0].GetDouble());
        Assert.Equal(3, result[2].GetDouble());
    }

    [Fact]
    public void FlattenProjection_ListProjectionLeft_NullSkipped()
    {
        // When inner result is null → continue (lines 1276-1278).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": [{"items": [{"value": 1}]}, {"items": null}, {"items": [{"value": 2}]}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("data[*].items[].value", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
    }

    [Fact]
    public void FlattenProjection_ListProjectionLeft_NonArrayProjected()
    {
        // When inner result is non-array → project directly (lines 1292-1298).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"data": [{"items": "single"}, {"items": "other"}]}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("data[*].items[].length(@)", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
    }

    // ─── EscapeJsonString Control Characters (Compiler lines 91-101) ─────────

    [Fact]
    public void RawString_LiteralTab_EscapedInOutput()
    {
        // Raw string with a literal tab character → EscapeJsonString hits '\t' case (line 97).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("'hello\tworld'", data);
        Assert.Equal("hello\tworld", result.GetString());
    }

    [Fact]
    public void RawString_LiteralNewline_EscapedInOutput()
    {
        // Raw string with a literal newline → EscapeJsonString hits '\n' case (line 95).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("'line1\nline2'", data);
        Assert.Equal("line1\nline2", result.GetString());
    }

    [Fact]
    public void RawString_LiteralCarriageReturn_EscapedInOutput()
    {
        // Raw string with a literal CR → EscapeJsonString hits '\r' case (line 96).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("'cr\rhere'", data);
        Assert.Equal("cr\rhere", result.GetString());
    }

    [Fact]
    public void RawString_LiteralBackspace_EscapedInOutput()
    {
        // Raw string with a literal backspace → EscapeJsonString hits '\b' case (line 93).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("'bs\bhere'", data);
        Assert.Equal("bs\bhere", result.GetString());
    }

    [Fact]
    public void RawString_LiteralFormFeed_EscapedInOutput()
    {
        // Raw string with a literal form feed → EscapeJsonString hits '\f' case (line 94).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("'ff\fhere'", data);
        Assert.Equal("ff\fhere", result.GetString());
    }

    [Fact]
    public void RawString_ControlCharBelowSpace_EscapedAsUnicode()
    {
        // Raw string with a control char < 0x20 (not in named cases) → \uXXXX escape (lines 99-101).
        // Using \x01 (SOH character).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("'ctrl\x01here'", data);
        Assert.Equal("ctrl\x01here", result.GetString());
    }

    // ─── Streaming Pipeline MultiSelect (fused pipeline context) ─────────────

    [Fact]
    public void StreamingPipeline_MultiSelectHash_Terminal()
    {
        // sort_by(@, &name) | [*].{n: name, v: val} → terminal HashProject
        // Hits MaterializeHashProject (lines 620-670).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[{"name":"c","val":3},{"name":"a","val":1},{"name":"b","val":2}]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &name) | [*].{n: name, v: val}", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal("a", result[0].GetProperty("n").GetString());
        Assert.Equal(1, result[0].GetProperty("v").GetDouble());
        Assert.Equal("c", result[2].GetProperty("n").GetString());
    }

    [Fact]
    public void StreamingPipeline_ProjectAfterSort()
    {
        // sort_by(@, &name) | [*].name → fused pipeline with Project stage.
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[{"name":"c","val":3},{"name":"a","val":1},{"name":"b","val":2}]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &name) | [*].name", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal("a", result[0].GetString());
        Assert.Equal("b", result[1].GetString());
        Assert.Equal("c", result[2].GetString());
    }

    [Fact]
    public void StreamingPipeline_FilterAfterSort()
    {
        // sort(@) | [?@ > `2`] → fused pipeline with Sort barrier then Filter streaming.
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[3,1,4,1,5]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort(@) | [?@ > `2`]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(3, result[0].GetDouble());
        Assert.Equal(4, result[1].GetDouble());
        Assert.Equal(5, result[2].GetDouble());
    }

    [Fact]
    public void StreamingPipeline_MultiSelectList_InProjection()
    {
        // sort_by(@, &name) | [*].[name, val] → fused pipeline with MultiSelectList projection.
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[{"name":"c","val":3},{"name":"a","val":1}]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &name) | [*].[name, val]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        Assert.Equal("a", result[0][0].GetString());
        Assert.Equal(1, result[0][1].GetDouble());
        Assert.Equal("c", result[1][0].GetString());
        Assert.Equal(3, result[1][1].GetDouble());
    }

    // ─── Streaming Reverse barrier (Compiler lines 558-568) ─────────────────

    [Fact]
    public void StreamingPipeline_ReverseBarrier()
    {
        // reverse(@) | [*].name → fused pipeline with Reverse barrier.
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[{"name":"a"},{"name":"b"},{"name":"c"}]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("reverse(@) | [*].name", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal("c", result[0].GetString());
        Assert.Equal("b", result[1].GetString());
        Assert.Equal("a", result[2].GetString());
    }

    // ─── Slice on non-array returns default (Compiler line 1485-1487) ───────

    [Fact]
    public void Slice_OnNonArray_ReturnsDefault()
    {
        // Slice applied to non-array → returns default (line 1485-1487).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"text": "hello"}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("text[0:2]", data);
        Assert.True(result.IsNullOrUndefined());
    }

    // ─── CompileSlice step=0 throws (Compiler line 1493-1495) ───────────────

    [Fact]
    public void Slice_ZeroStep_Throws()
    {
        // Slice with step=0 → throws (lines 1493-1495).
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[1,2,3]"""));
        Assert.Throws<JMESPathException>(() => JMESPathEvaluator.Default.Search("[::0]", data));
    }

    // ─── Streaming HashProject — Terminal (Compiler lines 617-670) ────
    // Pattern: source | barrier | [*].{key: expr}
    // where the hash is the LAST streaming stage → MaterializeHashProject is called.

    [Fact]
    public void StreamingHashProject_Terminal_SortThenProject()
    {
        // sort(@) is barrier, [*].{n: name} is terminal HashProject
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[{"name":"c","v":3},{"name":"a","v":1},{"name":"b","v":2}]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &name) | [*].{n: name, val: v}", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal("a", result[0].GetProperty("n").GetString());
        Assert.Equal(1, result[0].GetProperty("val").GetDouble());
        Assert.Equal("b", result[1].GetProperty("n").GetString());
        Assert.Equal("c", result[2].GetProperty("n").GetString());
    }

    [Fact]
    public void StreamingHashProject_Terminal_EmptyArray()
    {
        // Empty array → MaterializeHashProject returns EmptyArrayElement (line 623-625)
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort(@) | [*].{n: name}", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(0, result.GetArrayLength());
    }

    [Fact]
    public void StreamingHashProject_Terminal_NullValueInHash()
    {
        // Hash key evaluates to null/undefined → replaced with JSON null (line 656)
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[{"name":"a"},{"name":"b","extra":"x"}]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &name) | [*].{n: name, e: extra}", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(JsonValueKind.Null, result[0].GetProperty("e").ValueKind);
        Assert.Equal("x", result[1].GetProperty("e").GetString());
    }

    // ─── Streaming HashProject — Non-Terminal (Compiler lines 475-481) ─
    // Pattern: source | barrier | [*].{key: expr} | [*].key
    // Hash is NOT last streaming stage → BuildHashObject (line 675-697) is used.

    [Fact]
    public void StreamingHashProject_NonTerminal_ProjectAfterHash()
    {
        // sort(@) | [*].{n: name, v: v} | [*].n → hash is non-terminal, then name projection
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[{"name":"c","v":3},{"name":"a","v":1},{"name":"b","v":2}]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &name) | [*].{n: name, v: v} | [*].n", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal("a", result[0].GetString());
        Assert.Equal("b", result[1].GetString());
        Assert.Equal("c", result[2].GetString());
    }

    [Fact]
    public void StreamingHashProject_NonTerminal_FilterAfterHash()
    {
        // sort(@) | [*].{n: name, v: v} | [?v > `1`] → hash non-terminal, then filter
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[{"name":"c","v":3},{"name":"a","v":1},{"name":"b","v":2}]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &name) | [*].{n: name, v: v} | [?v > `1`]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
    }

    // ─── Streaming NormalizeSliceIndex (Compiler lines 699-715) ───────
    // This function is used for the streaming Slice barrier stage.
    // PipeStage.Slice is never instantiated (dead code) so these lines
    // are unreachable from current code. Documenting rather than testing.

    // ─── Negative step in standalone slice (Compiler lines 1506-1551) ─
    [Fact]
    public void Slice_NegativeStep_ReversesArray()
    {
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[1,2,3,4,5]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("[::-1]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(5, result.GetArrayLength());
        Assert.Equal(5, result[0].GetDouble());
        Assert.Equal(1, result[4].GetDouble());
    }

    [Fact]
    public void Slice_NegativeStep_WithStartStop_Streaming()
    {
        // [3:0:-1] → elements at indices 3, 2, 1 → [4, 3, 2]
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[1,2,3,4,5]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("[3:0:-1]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(4, result[0].GetDouble());
        Assert.Equal(3, result[1].GetDouble());
        Assert.Equal(2, result[2].GetDouble());
    }

    [Fact]
    public void Slice_NegativeStep_NegativeIndices_Streaming()
    {
        // [-1:-4:-1] → starts at last, stops before index 1 → [5, 4, 3]
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""[1,2,3,4,5]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("[-1:-4:-1]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(5, result[0].GetDouble());
        Assert.Equal(4, result[1].GetDouble());
        Assert.Equal(3, result[2].GetDouble());
    }

    // ─── Lexer Error and Edge-Case Tests ──

    [Fact]
    public void Lexer_RawString_BackslashNotQuote()
    {
        // L237-242: In raw string, backslash not followed by ' emits literal backslash + char
        // JMESPath raw strings: 'text' where only \' is a real escape
        // Using \\ triggers materialization, and the second \ is "not a quote"
        JsonElement data = JsonElement.ParseValue("""{"a\\b": 1}"""u8);
        // In JMESPath, raw string 'a\\b' materializes as literal "a\b"
        // We need a quoted identifier to access the key: "a\\b"
        JsonElement result = JMESPathEvaluator.Default.Search("\"a\\\\b\"", data);
        Assert.Equal(1, result.GetDouble());
    }

    [Fact]
    public void Lexer_RawString_BackslashTriggersElseBranch()
    {
        // L237-242: raw string with \\ followed by \' to trigger materialization AND else branch
        // JMESPath raw string: 'foo\\\'bar' → materializes to foo\\'bar
        // The \\ hits L237 (else branch: emit backslash + char), \' hits L232 (escaped quote)
        string expr = """'foo\\\'bar'""";
        JsonElement data = JsonElement.ParseValue("{}"u8);
        JsonElement result = JMESPathEvaluator.Default.Search(expr, data);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        // Result: foo + \\ + ' + bar = "foo\\'bar"
        Assert.Equal("foo\\\\'bar", result.GetString());
    }

    [Fact]
    public void Lexer_RawString_LongTriggersBufferGrowth()
    {
        // L246-256, L264-266: Very long raw string with escapes → buffer growth + ArrayPool return
        // Need > 512 bytes of materialized content to trigger double growth (stackalloc 256 → rent 512 → rent 1024)
        // L250-252: Return previous rented array during second growth
        string longVal = new string('x', 520) + "\\'"; // 520 x's then escaped quote
        string expr = $"'{longVal}'";
        JsonElement data = JsonElement.ParseValue("{}"u8);
        JsonElement result = JMESPathEvaluator.Default.Search(expr, data);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        // The materialized value is 520 x's + single quote
        Assert.Equal(520 + 1, result.GetString()!.Length);
    }

    [Fact]
    public void Lexer_QuotedIdentifier_UnterminatedEscape_Throws()
    {
        // L301-303: Escape at end of quoted identifier
        Assert.Throws<JMESPathException>(() =>
            JMESPathEvaluator.Default.Search("\"abc\\", JsonElement.ParseValue("{}"u8)));
    }

    [Fact]
    public void Lexer_UnicodeEscape_AsciiCodePoint()
    {
        // L409-412: \u escape producing ASCII code point (< 0x80)
        // \u0041 = 'A'
        JsonElement data = JsonElement.ParseValue("""{"A": 42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("\"\\u0041\"", data);
        Assert.Equal(42, result.GetDouble());
    }

    [Fact]
    public void Lexer_UnicodeEscape_InvalidSurrogatePair_Throws()
    {
        // L395-397: High surrogate followed by invalid low surrogate
        Assert.Throws<JMESPathException>(() =>
            JMESPathEvaluator.Default.Search("\"\\uD800\\u0041\"", JsonElement.ParseValue("{}"u8)));
    }

    [Fact]
    public void Lexer_UnicodeEscape_UnpairedHighSurrogate_Throws()
    {
        // L401-403: High surrogate not followed by \u sequence
        Assert.Throws<JMESPathException>(() =>
            JMESPathEvaluator.Default.Search("\"\\uD800abc\"", JsonElement.ParseValue("{}"u8)));
    }

    [Fact]
    public void Lexer_InvalidHexDigit_Throws()
    {
        // L447: Invalid hex digit in \u escape
        Assert.Throws<JMESPathException>(() =>
            JMESPathEvaluator.Default.Search("\"\\uZZZZ\"", JsonElement.ParseValue("{}"u8)));
    }

    [Fact]
    public void Lexer_Literal_UnterminatedEscapeAtEnd()
    {
        // L478-479: Escape at very end of backtick literal
        Assert.Throws<JMESPathException>(() =>
            JMESPathEvaluator.Default.Search("`\"hello\\", JsonElement.ParseValue("{}"u8)));
    }

    [Fact]
    public void Lexer_QuotedIdentifier_LongTriggersGrowBuffer()
    {
        // L604-612: GrowBuffer path — quoted identifier with many escape sequences
        // Need > 512 bytes of OUTPUT to trigger double growth (L607-609: return previous rented)
        // 520 × \u0041 = 520 × 6 input bytes → 520 'A' output bytes > 512
        string longKey = string.Concat(Enumerable.Repeat("\\u0041", 520));
        string expectedKey = new('A', 520);
        string json = $"{{\"{expectedKey}\": 77}}";
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        JsonElement result = JMESPathEvaluator.Default.Search($"\"{longKey}\"", data);
        Assert.Equal(77, result.GetDouble());
    }

    // ─── sort_by with string comparisons (WriteJsonEscapedFromUtf8) ──────
    // Covers: Compiler.Functions.cs lines 989-1051

    [Fact]
    public void SortBy_StringValues_UsesUtf8Comparison()
    {
        // sort_by(@, &@) on strings triggers Utf8StringElementComparer
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""["banana", "apple", "cherry"]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &@)", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal("apple", result[0].GetString());
        Assert.Equal("banana", result[1].GetString());
        Assert.Equal("cherry", result[2].GetString());
    }

    [Fact]
    public void SortBy_StringsWithControlCharacters()
    {
        // Strings with control chars exercise WriteJsonEscapedFromUtf8 escape branches
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""["b\t1", "a\n2", "c\r3"]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &@)", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void SortBy_StringsWithBackslashAndQuotes()
    {
        // Backslash and quote exercise the escaping paths in WriteJsonEscapedFromUtf8
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""["c\\d", "a\"b", "e"]"""));
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(@, &@)", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    // ─── join with large output (EnsureJoinBuffer) ────────────────────
    // Covers: Compiler.Functions.cs lines 368-376

    [Fact]
    public void Join_LargeArray_TriggersBufferGrowth()
    {
        // Build a large array that produces a join result longer than initial buffer
        var sb = new StringBuilder("[");
        for (int i = 0; i < 200; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"item{i:D5}\""); // "item00000" etc
        }
        sb.Append(']');
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(sb.ToString()));
        JsonElement result = JMESPathEvaluator.Default.Search("join(',', @)", data);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        string joined = result.GetString()!;
        Assert.Contains("item00000", joined);
        Assert.Contains("item00199", joined);
    }

    // ─── Lexer: raw string with backslash (non-quote) ─────────────────
    // Covers: Lexer.cs lines 237-242

    [Fact]
    public void RawString_WithBackslash_EmitsLiteralBackslash()
    {
        // Raw strings ('...') treat backslash as literal except before single quote
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"a\\b": 42}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("\"a\\\\b\"", data);
        Assert.Equal(42, result.GetDouble());
    }

    // ─── Lexer: buffer growth in raw string ──────────────────────────
    // Covers: Lexer.cs lines 246-256

    [Fact]
    public void Lexer_LongIdentifier_TriggersBufferGrowth()
    {
        // A very long identifier forces the lexer buffer to grow
        string longKey = new string('x', 500);
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes($"{{\"{longKey}\": 99}}"));
        JsonElement result = JMESPathEvaluator.Default.Search(longKey, data);
        Assert.Equal(99, result.GetDouble());
    }

    // ─── Lexer: Unicode escape producing multi-byte UTF-8 ───────────
    // Covers: Lexer.cs lines 413-418

    [Fact]
    public void Lexer_UnicodeEscape_TwoByteUtf8()
    {
        // \u00E9 = 'é' (2-byte UTF-8: C3 A9)
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"café": true}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("\"caf\\u00e9\"", data);
        Assert.True(result.IsNotUndefined());
    }

    [Fact]
    public void Lexer_UnicodeEscape_ThreeByteUtf8()
    {
        // \u4e16 = '世' (3-byte UTF-8: E4 B8 96)
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes("""{"世界": "hello"}"""));
        JsonElement result = JMESPathEvaluator.Default.Search("\"\\u4e16\\u754c\"", data);
        Assert.Equal("hello", result.GetString());
    }

    // ─── Lexer: GrowBuffer general path ──────────────────────────────
    // Covers: Lexer.cs lines 604-612

    [Fact]
    public void Lexer_LongQuotedString_TriggersGrowBuffer()
    {
        // Very long quoted identifier triggers general GrowBuffer
        string longKey = new string('y', 600);
        JsonElement data = JsonElement.ParseValue(
            Encoding.UTF8.GetBytes($"{{\"{longKey}\": 42}}"));
        JsonElement result = JMESPathEvaluator.Default.Search($"\"{longKey}\"", data);
        Assert.Equal(42, result.GetDouble());
    }
}
