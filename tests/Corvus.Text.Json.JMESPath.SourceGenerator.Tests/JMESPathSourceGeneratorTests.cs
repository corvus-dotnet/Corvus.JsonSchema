// <copyright file="JMESPathSourceGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JMESPath.SourceGenerator.Tests;

/// <summary>
/// Integration tests for the JMESPath source generator.
/// Each test exercises a source-generated evaluator by providing data and asserting on the result.
/// </summary>
[TestClass]
public class JMESPathSourceGeneratorTests
{
    [TestMethod]
    public void PropertyPath_ExtractsNestedValue()
    {
        // foo.bar
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"foo":{"bar":"baz"}}""");
        JsonElement result = PropertyPathExpr.Evaluate(doc.RootElement, workspace);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
        Assert.AreEqual("baz", result.GetString());
    }

    [TestMethod]
    public void PropertyPath_ReturnsDefaultForMissing()
    {
        // foo.bar on {"foo":{}}
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"foo":{}}""");
        JsonElement result = PropertyPathExpr.Evaluate(doc.RootElement, workspace);
        Assert.IsTrue(result.IsNullOrUndefined());
    }

    [TestMethod]
    public void ListProjection_ExtractsNamesFromArray()
    {
        // people[*].name
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"people":[{"name":"Alice"},{"name":"Bob"},{"name":"Charlie"}]}""");
        JsonElement result = ListProjectionExpr.Evaluate(doc.RootElement, workspace);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual("""["Alice","Bob","Charlie"]""", result.GetRawText());
    }

    [TestMethod]
    public void FilterProjection_FiltersAndProjects()
    {
        // people[?age > `20`].name
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"people":[{"name":"Alice","age":25},{"name":"Bob","age":18},{"name":"Charlie","age":30}]}""");
        JsonElement result = FilterProjectionExpr.Evaluate(doc.RootElement, workspace);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual("""["Alice","Charlie"]""", result.GetRawText());
    }

    [TestMethod]
    public void SortBy_SortsByExpression()
    {
        // sort_by(people, &age)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"people":[{"name":"Charlie","age":30},{"name":"Alice","age":20},{"name":"Bob","age":25}]}""");
        JsonElement result = SortByExpr.Evaluate(doc.RootElement, workspace);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);

        // Should be sorted by age: Alice(20), Bob(25), Charlie(30)
        Assert.AreEqual(3, result.GetArrayLength());
        Assert.AreEqual("Alice", result[0].TryGetProperty("name"u8, out var n0) ? n0.GetString() : null);
        Assert.AreEqual("Bob", result[1].TryGetProperty("name"u8, out var n1) ? n1.GetString() : null);
        Assert.AreEqual("Charlie", result[2].TryGetProperty("name"u8, out var n2) ? n2.GetString() : null);
    }

    [TestMethod]
    public void MultiSelectHash_CreatesObject()
    {
        // {total: length(items), names: items[*].name}
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"items":[{"name":"A"},{"name":"B"},{"name":"C"}]}""");
        JsonElement result = MultiSelectHashExpr.Evaluate(doc.RootElement, workspace);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        Assert.IsTrue(result.TryGetProperty("total"u8, out JsonElement total));
        Assert.AreEqual(3, total.GetDouble());
        Assert.IsTrue(result.TryGetProperty("names"u8, out JsonElement names));
        Assert.AreEqual("""["A","B","C"]""", names.GetRawText());
    }

    [TestMethod]
    public void Slice_ReturnsSubArray()
    {
        // items[0:3]
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"items":[10,20,30,40,50]}""");
        JsonElement result = SliceExpr.Evaluate(doc.RootElement, workspace);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual("[10,20,30]", result.GetRawText());
    }
}
