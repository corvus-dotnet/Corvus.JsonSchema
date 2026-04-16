// <copyright file="BasicEvaluatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using Xunit;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Basic integration tests for the JMESPath evaluator: lex → parse → compile → evaluate.
/// </summary>
public class BasicEvaluatorTests
{
    [Fact]
    public void SimpleIdentifier()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo": "bar"}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("foo", data);
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal("bar", result.GetString());
    }

    [Fact]
    public void SubExpression()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo": {"bar": "baz"}}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("foo.bar", data);
        Assert.Equal("baz", result.GetString());
    }

    [Fact]
    public void IndexExpression()
    {
        JsonElement data = JsonElement.ParseValue("""{"items": [10, 20, 30]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[1]", data);
        Assert.Equal(20, result.GetDouble());
    }

    [Fact]
    public void NegativeIndex()
    {
        JsonElement data = JsonElement.ParseValue("""{"items": [10, 20, 30]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[-1]", data);
        Assert.Equal(30, result.GetDouble());
    }

    [Fact]
    public void MissingPropertyReturnsDefault()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo": "bar"}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("missing", data);
        Assert.True(result.IsUndefined());
    }

    [Fact]
    public void ListWildcard()
    {
        JsonElement data = JsonElement.ParseValue("""{"items": [{"name": "a"}, {"name": "b"}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[*].name", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        Assert.Equal("a", result[0].GetString());
        Assert.Equal("b", result[1].GetString());
    }

    [Fact]
    public void ObjectWildcard()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": 1, "b": 2, "c": 3}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("*", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(3, result.GetArrayLength());
    }

    [Fact]
    public void FilterExpression()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items": [{"age": 10}, {"age": 30}, {"age": 20}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[?age > `15`]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
    }

    [Fact]
    public void PipeExpression()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items": [{"name": "a"}, {"name": "b"}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[*].name | [0]", data);
        Assert.Equal("a", result.GetString());
    }

    [Fact]
    public void MultiSelectList()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": 1, "b": 2, "c": 3}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[a, b]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        Assert.Equal(1, result[0].GetDouble());
        Assert.Equal(2, result[1].GetDouble());
    }

    [Fact]
    public void MultiSelectHash()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": 1, "b": 2, "c": 3}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("{x: a, y: b}", data);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.True(result.TryGetProperty("x", out JsonElement x));
        Assert.Equal(1, x.GetDouble());
        Assert.True(result.TryGetProperty("y", out JsonElement y));
        Assert.Equal(2, y.GetDouble());
    }

    [Fact]
    public void RawStringLiteral()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("'hello world'", data);
        Assert.Equal("hello world", result.GetString());
    }

    [Fact]
    public void JsonLiteral()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("`42`", data);
        Assert.Equal(42, result.GetDouble());
    }

    [Fact]
    public void CurrentNode()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": 1}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("@", data);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
    }

    [Fact]
    public void LogicalOr()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": null, "b": 42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("a || b", data);
        Assert.Equal(42, result.GetDouble());
    }

    [Fact]
    public void LogicalAnd()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": true, "b": 42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("a && b", data);
        Assert.Equal(42, result.GetDouble());
    }

    [Fact]
    public void NotExpression()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": true}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("!a", data);
        Assert.Equal(JsonValueKind.False, result.ValueKind);
    }

    [Fact]
    public void ComparisonEqual()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": 1}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("a == `1`", data);
        Assert.Equal(JsonValueKind.True, result.ValueKind);
    }

    [Fact]
    public void Flatten()
    {
        JsonElement data = JsonElement.ParseValue("""[[1, 2], [3, 4], [5]]"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(5, result.GetArrayLength());
    }

    [Fact]
    public void Slice()
    {
        JsonElement data = JsonElement.ParseValue("""[0, 1, 2, 3, 4]"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[1:3]", data);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, result.GetArrayLength());
        Assert.Equal(1, result[0].GetDouble());
        Assert.Equal(2, result[1].GetDouble());
    }

    [Fact]
    public void QuotedIdentifier()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo-bar": 42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("\"foo-bar\"", data);
        Assert.Equal(42, result.GetDouble());
    }
}
