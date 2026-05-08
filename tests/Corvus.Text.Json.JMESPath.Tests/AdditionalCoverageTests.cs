// <copyright file="AdditionalCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Additional coverage tests for JMESPath expression-level features: expression references,
/// logical operators, slicing, projections, multi-select, functions, and pipes.
/// </summary>
[TestClass]
public class AdditionalCoverageTests
{
    // ─── Expression references ───────────────────────────────────────

    [TestMethod]
    public void SortBy_ExpressionReference()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items": [{"name":"c","age":30},{"name":"a","age":10},{"name":"b","age":20}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("sort_by(items, &age)", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(3, result.GetArrayLength());
        Assert.AreEqual(10, result[0].GetProperty("age").GetDouble());
        Assert.AreEqual(20, result[1].GetProperty("age").GetDouble());
        Assert.AreEqual(30, result[2].GetProperty("age").GetDouble());
    }

    [TestMethod]
    public void Map_ExpressionReference()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"people": [{"name":"Alice"},{"name":"Bob"}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("map(&name, people)", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(2, result.GetArrayLength());
        Assert.AreEqual("Alice", result[0].GetString());
        Assert.AreEqual("Bob", result[1].GetString());
    }

    // ─── Logical operators ───────────────────────────────────────────

    [TestMethod]
    public void LogicalOr_FirstTruthyWins()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo":null,"bar":42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("foo || bar", data);
        Assert.AreEqual(42, result.GetDouble());
    }

    [TestMethod]
    public void LogicalAnd_ReturnsFalsyOrSecond()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo":true,"bar":42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("foo && bar", data);
        Assert.AreEqual(42, result.GetDouble());
    }

    [TestMethod]
    public void Not_NegatesTruthy()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo":true}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("!foo", data);
        Assert.AreEqual(JsonValueKind.False, result.ValueKind);
    }

    // ─── Slicing ─────────────────────────────────────────────────────

    [TestMethod]
    public void Slice_WithStep_EveryOther()
    {
        JsonElement data = JsonElement.ParseValue("""[0,1,2,3,4,5]"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[::2]", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(3, result.GetArrayLength());
        Assert.AreEqual(0, result[0].GetDouble());
        Assert.AreEqual(2, result[1].GetDouble());
        Assert.AreEqual(4, result[2].GetDouble());
    }

    [TestMethod]
    public void Slice_NegativeStep_Reverses()
    {
        JsonElement data = JsonElement.ParseValue("""[0,1,2,3,4]"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[4:1:-1]", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(3, result.GetArrayLength());
        Assert.AreEqual(4, result[0].GetDouble());
        Assert.AreEqual(3, result[1].GetDouble());
        Assert.AreEqual(2, result[2].GetDouble());
    }

    // ─── Literal ─────────────────────────────────────────────────────

    [TestMethod]
    public void ComplexJsonLiteral()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("""`{"key":"value"}`""", data);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        Assert.AreEqual("value", result.GetProperty("key").GetString());
    }

    // ─── Flatten projection ──────────────────────────────────────────

    [TestMethod]
    public void FlattenProjection_ExtractsNames()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items":[{"name":"a"},{"name":"b"},{"name":"c"}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[].name", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(3, result.GetArrayLength());
        Assert.AreEqual("a", result[0].GetString());
        Assert.AreEqual("b", result[1].GetString());
        Assert.AreEqual("c", result[2].GetString());
    }

    // ─── Filter with comparison ──────────────────────────────────────

    [TestMethod]
    public void Filter_AgeGreaterThan18()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items":[{"age":10},{"age":25},{"age":17},{"age":30}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[?age > `18`]", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(2, result.GetArrayLength());
        Assert.AreEqual(25, result[0].GetProperty("age").GetDouble());
        Assert.AreEqual(30, result[1].GetProperty("age").GetDouble());
    }

    // ─── Multi-select ────────────────────────────────────────────────

    [TestMethod]
    public void MultiSelectHash_NameAndCount()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"name":"Alice","items":[1,2,3]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("{name: name, count: length(items)}", data);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        Assert.AreEqual("Alice", result.GetProperty("name").GetString());
        Assert.AreEqual(3, result.GetProperty("count").GetDouble());
    }

    [TestMethod]
    public void MultiSelectList_NameAndAge()
    {
        JsonElement data = JsonElement.ParseValue("""{"name":"Alice","age":30}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[name, age]", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(2, result.GetArrayLength());
        Assert.AreEqual("Alice", result[0].GetString());
        Assert.AreEqual(30, result[1].GetDouble());
    }

    // ─── Pipe ────────────────────────────────────────────────────────

    [TestMethod]
    public void Pipe_FirstOfProjection()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items":[{"name":"a"},{"name":"b"}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[*].name | [0]", data);
        Assert.AreEqual("a", result.GetString());
    }

    // ─── Built-in functions ──────────────────────────────────────────

    [TestMethod]
    public void Contains_ReturnsTrue()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("contains(`\"foobar\"`, `\"foo\"`)", data);
        Assert.AreEqual(JsonValueKind.True, result.ValueKind);
    }

    [TestMethod]
    public void StartsWith_ReturnsTrue()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("starts_with(`\"foobar\"`, `\"foo\"`)", data);
        Assert.AreEqual(JsonValueKind.True, result.ValueKind);
    }

    [TestMethod]
    public void EndsWith_ReturnsTrue()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("ends_with(`\"foobar\"`, `\"bar\"`)", data);
        Assert.AreEqual(JsonValueKind.True, result.ValueKind);
    }

    [TestMethod]
    public void Join_ConcatenatesWithSeparator()
    {
        JsonElement data = JsonElement.ParseValue("""{"items":["a","b","c"]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("join(', ', items)", data);
        Assert.AreEqual("a, b, c", result.GetString());
    }

    [TestMethod]
    public void Reverse_ReversesArray()
    {
        JsonElement data = JsonElement.ParseValue("""{"items":[1,2,3]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("reverse(items)", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(3, result.GetArrayLength());
        Assert.AreEqual(3, result[0].GetDouble());
        Assert.AreEqual(2, result[1].GetDouble());
        Assert.AreEqual(1, result[2].GetDouble());
    }

    [TestMethod]
    public void ToString_ConvertsNumberToString()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("to_string(`42`)", data);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
        Assert.AreEqual("42", result.GetString());
    }

    [TestMethod]
    public void ToNumber_ConvertsStringToNumber()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("to_number(`\"42\"`)", data);
        Assert.AreEqual(42, result.GetDouble());
    }
}
