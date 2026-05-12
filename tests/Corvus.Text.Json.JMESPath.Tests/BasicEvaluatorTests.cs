// <copyright file="BasicEvaluatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Basic integration tests for the JMESPath evaluator: lex → parse → compile → evaluate.
/// </summary>
[TestClass]
public class BasicEvaluatorTests
{
    [TestMethod]
    public void SimpleIdentifier()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo": "bar"}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("foo", data);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
        Assert.AreEqual("bar", result.GetString());
    }

    [TestMethod]
    public void SubExpression()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo": {"bar": "baz"}}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("foo.bar", data);
        Assert.AreEqual("baz", result.GetString());
    }

    [TestMethod]
    public void IndexExpression()
    {
        JsonElement data = JsonElement.ParseValue("""{"items": [10, 20, 30]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[1]", data);
        Assert.AreEqual(20, result.GetDouble());
    }

    [TestMethod]
    public void NegativeIndex()
    {
        JsonElement data = JsonElement.ParseValue("""{"items": [10, 20, 30]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[-1]", data);
        Assert.AreEqual(30, result.GetDouble());
    }

    [TestMethod]
    public void MissingPropertyReturnsNull()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo": "bar"}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("missing", data);
        Assert.AreEqual(JsonValueKind.Null, result.ValueKind);
    }

    [TestMethod]
    public void ListWildcard()
    {
        JsonElement data = JsonElement.ParseValue("""{"items": [{"name": "a"}, {"name": "b"}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[*].name", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(2, result.GetArrayLength());
        Assert.AreEqual("a", result[0].GetString());
        Assert.AreEqual("b", result[1].GetString());
    }

    [TestMethod]
    public void ObjectWildcard()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": 1, "b": 2, "c": 3}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("*", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(3, result.GetArrayLength());
    }

    [TestMethod]
    public void FilterExpression()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items": [{"age": 10}, {"age": 30}, {"age": 20}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[?age > `15`]", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(2, result.GetArrayLength());
    }

    [TestMethod]
    public void PipeExpression()
    {
        JsonElement data = JsonElement.ParseValue(
            """{"items": [{"name": "a"}, {"name": "b"}]}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("items[*].name | [0]", data);
        Assert.AreEqual("a", result.GetString());
    }

    [TestMethod]
    public void MultiSelectList()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": 1, "b": 2, "c": 3}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[a, b]", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(2, result.GetArrayLength());
        Assert.AreEqual(1, result[0].GetDouble());
        Assert.AreEqual(2, result[1].GetDouble());
    }

    [TestMethod]
    public void MultiSelectHash()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": 1, "b": 2, "c": 3}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("{x: a, y: b}", data);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        Assert.IsTrue(result.TryGetProperty("x", out JsonElement x));
        Assert.AreEqual(1, x.GetDouble());
        Assert.IsTrue(result.TryGetProperty("y", out JsonElement y));
        Assert.AreEqual(2, y.GetDouble());
    }

    [TestMethod]
    public void RawStringLiteral()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("'hello world'", data);
        Assert.AreEqual("hello world", result.GetString());
    }

    [TestMethod]
    public void JsonLiteral()
    {
        JsonElement data = JsonElement.ParseValue("""{}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("`42`", data);
        Assert.AreEqual(42, result.GetDouble());
    }

    [TestMethod]
    public void CurrentNode()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": 1}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("@", data);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void LogicalOr()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": null, "b": 42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("a || b", data);
        Assert.AreEqual(42, result.GetDouble());
    }

    [TestMethod]
    public void LogicalAnd()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": true, "b": 42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("a && b", data);
        Assert.AreEqual(42, result.GetDouble());
    }

    [TestMethod]
    public void NotExpression()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": true}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("!a", data);
        Assert.AreEqual(JsonValueKind.False, result.ValueKind);
    }

    [TestMethod]
    public void ComparisonEqual()
    {
        JsonElement data = JsonElement.ParseValue("""{"a": 1}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("a == `1`", data);
        Assert.AreEqual(JsonValueKind.True, result.ValueKind);
    }

    [TestMethod]
    public void Flatten()
    {
        JsonElement data = JsonElement.ParseValue("""[[1, 2], [3, 4], [5]]"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[]", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(5, result.GetArrayLength());
    }

    [TestMethod]
    public void Slice()
    {
        JsonElement data = JsonElement.ParseValue("""[0, 1, 2, 3, 4]"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("[1:3]", data);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(2, result.GetArrayLength());
        Assert.AreEqual(1, result[0].GetDouble());
        Assert.AreEqual(2, result[1].GetDouble());
    }

    [TestMethod]
    public void QuotedIdentifier()
    {
        JsonElement data = JsonElement.ParseValue("""{"foo-bar": 42}"""u8);
        JsonElement result = JMESPathEvaluator.Default.Search("\"foo-bar\"", data);
        Assert.AreEqual(42, result.GetDouble());
    }
}
