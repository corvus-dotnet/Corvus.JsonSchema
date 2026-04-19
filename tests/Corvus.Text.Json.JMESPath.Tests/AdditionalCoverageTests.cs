// <copyright file="AdditionalCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using Xunit;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Additional coverage tests for JMESPath expression-level features: expression references,
/// logical operators, slicing, projections, multi-select, functions, and pipes.
/// </summary>
public class AdditionalCoverageTests
{
    // ─── Expression references ───────────────────────────────────────

    [Fact]
    public void SortBy_ExpressionReference()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items": [{"name":"c","age":30},{"name":"a","age":10},{"name":"b","age":20}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(items, &age)", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(10, result[0].GetProperty("age").GetDouble());
        Assert.Equal(20, result[1].GetProperty("age").GetDouble());
        Assert.Equal(30, result[2].GetProperty("age").GetDouble());
    }

    [Fact]
    public void Map_ExpressionReference()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"people": [{"name":"Alice"},{"name":"Bob"}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("map(&name, people)", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        Assert.Equal("Alice", result[0].GetString());
        Assert.Equal("Bob", result[1].GetString());
    }

    // ─── Logical operators ───────────────────────────────────────────

    [Fact]
    public void LogicalOr_FirstTruthyWins()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo":null,"bar":42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("foo || bar", data);
        Assert.Equal(42, result.GetDouble());
    }

    [Fact]
    public void LogicalAnd_ReturnsFalsyOrSecond()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo":true,"bar":42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("foo && bar", data);
        Assert.Equal(42, result.GetDouble());
    }

    [Fact]
    public void Not_NegatesTruthy()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo":true}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("!foo", data);
        Assert.Equal(JsonValueKind.False, result.ValueKind);
    }

    // ─── Slicing ─────────────────────────────────────────────────────

    [Fact]
    public void Slice_WithStep_EveryOther()
    {
        JsonElement data = JsonElement.ParseValue("""[0,1,2,3,4,5]"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[::2]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(0, result[0].GetDouble());
        Assert.Equal(2, result[1].GetDouble());
        Assert.Equal(4, result[2].GetDouble());
    }

    [Fact]
    public void Slice_NegativeStep_Reverses()
    {
        JsonElement data = JsonElement.ParseValue("""[0,1,2,3,4]"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[4:1:-1]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(4, result[0].GetDouble());
        Assert.Equal(3, result[1].GetDouble());
        Assert.Equal(2, result[2].GetDouble());
    }

    // ─── Literal ─────────────────────────────────────────────────────

    [Fact]
    public void ComplexJsonLiteral()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("""`{"key":"value"}`""", data);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("value", result.GetProperty("key").GetString());
    }

    // ─── Flatten projection ──────────────────────────────────────────

    [Fact]
    public void FlattenProjection_ExtractsNames()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items":[{"name":"a"},{"name":"b"},{"name":"c"}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[].name", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal("a", result[0].GetString());
        Assert.Equal("b", result[1].GetString());
        Assert.Equal("c", result[2].GetString());
    }

    // ─── Filter with comparison ──────────────────────────────────────

    [Fact]
    public void Filter_AgeGreaterThan18()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items":[{"age":10},{"age":25},{"age":17},{"age":30}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[?age > `18`]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        Assert.Equal(25, result[0].GetProperty("age").GetDouble());
        Assert.Equal(30, result[1].GetProperty("age").GetDouble());
    }

    // ─── Multi-select ────────────────────────────────────────────────

    [Fact]
    public void MultiSelectHash_NameAndCount()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"name":"Alice","items":[1,2,3]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("{name: name, count: length(items)}", data);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("Alice", result.GetProperty("name").GetString());
        Assert.Equal(3, result.GetProperty("count").GetDouble());
    }

    [Fact]
    public void MultiSelectList_NameAndAge()
    {
        JsonElement data = JsonElement.ParseValue("""{"name":"Alice","age":30}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[name, age]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        Assert.Equal("Alice", result[0].GetString());
        Assert.Equal(30, result[1].GetDouble());
    }

    // ─── Pipe ────────────────────────────────────────────────────────

    [Fact]
    public void Pipe_FirstOfProjection()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items":[{"name":"a"},{"name":"b"}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[*].name | [0]", data);
        Assert.Equal("a", result.GetString());
    }

    // ─── Built-in functions ──────────────────────────────────────────

    [Fact]
    public void Contains_ReturnsTrue()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("contains(`\"foobar\"`, `\"foo\"`)", data);
        Assert.Equal(JsonValueKind.True, result.ValueKind);
    }

    [Fact]
    public void StartsWith_ReturnsTrue()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("starts_with(`\"foobar\"`, `\"foo\"`)", data);
        Assert.Equal(JsonValueKind.True, result.ValueKind);
    }

    [Fact]
    public void EndsWith_ReturnsTrue()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("ends_with(`\"foobar\"`, `\"bar\"`)", data);
        Assert.Equal(JsonValueKind.True, result.ValueKind);
    }

    [Fact]
    public void Join_ConcatenatesWithSeparator()
    {
        JsonElement data = JsonElement.ParseValue("""{"items":["a","b","c"]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("join(', ', items)", data);
        Assert.Equal("a, b, c", result.GetString());
    }

    [Fact]
    public void Reverse_ReversesArray()
    {
        JsonElement data = JsonElement.ParseValue("""{"items":[1,2,3]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("reverse(items)", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
        Assert.Equal(3, result[0].GetDouble());
        Assert.Equal(2, result[1].GetDouble());
        Assert.Equal(1, result[2].GetDouble());
    }

    [Fact]
    public void ToString_ConvertsNumberToString()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("to_string(`42`)", data);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal("42", result.GetString());
    }

    [Fact]
    public void ToNumber_ConvertsStringToNumber()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("to_number(`\"42\"`)", data);
        Assert.Equal(42, result.GetDouble());
    }
}
