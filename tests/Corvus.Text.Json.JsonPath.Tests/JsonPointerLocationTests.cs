// <copyright file="JsonPointerLocationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Tests for retrieving the location of JSONPath query results as JSON Pointers
/// relative to the queried document root (the issue #942 scenario).
/// </summary>
[TestClass]
public class JsonPointerLocationTests
{
    private const string StoreDocument = """
        {"store":{"book":[{"title":"A","price":10},{"title":"B","price":5}]}}
        """;

    [TestMethod]
    public void QueryNodes_FilteredResult_ReportsItsPointerFromRoot()
    {
        using ParsedJsonDocument<JsonElement> dataDoc = ParsedJsonDocument<JsonElement>.Parse(StoreDocument);

        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.store.book[?@.price < 10].title",
            dataDoc.RootElement);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("/store/book/1/title", result[0].GetJsonPointer());
    }

    [TestMethod]
    public void QueryNodes_DescendantResults_ReportDistinctPointers()
    {
        using ParsedJsonDocument<JsonElement> dataDoc = ParsedJsonDocument<JsonElement>.Parse(StoreDocument);

        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$..price",
            dataDoc.RootElement);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("/store/book/0/price", result[0].GetJsonPointer());
        Assert.AreEqual("/store/book/1/price", result[1].GetJsonPointer());
    }

    [TestMethod]
    public void QueryNodes_PointersResolveBackToTheMatchedValues()
    {
        using ParsedJsonDocument<JsonElement> dataDoc = ParsedJsonDocument<JsonElement>.Parse(StoreDocument);

        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$..price",
            dataDoc.RootElement);

        for (int i = 0; i < result.Count; i++)
        {
            Assert.IsTrue(dataDoc.RootElement.TryResolvePointer(result[i].GetJsonPointer(), out JsonElement resolved));
            Assert.AreEqual(result[i].GetInt32(), resolved.GetInt32());
        }
    }
}