// <copyright file="JsonPathUtf8QueryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Tests for the UTF-8 <see cref="JsonPathEvaluator.QueryNodes(System.ReadOnlySpan{byte}, in JsonElement)"/>
/// overloads, which evaluate a query without materializing a managed string.
/// </summary>
[TestClass]
public class JsonPathUtf8QueryTests
{
    [TestMethod]
    public void Utf8QueryMatchesStringQuery()
    {
        JsonElement data = JsonElement.ParseValue("{\"items\":[1,2,3],\"name\":\"x\"}");

        using JsonPathResult viaString = JsonPathEvaluator.Default.QueryNodes("$.items[*]", data);
        using JsonPathResult viaUtf8 = JsonPathEvaluator.Default.QueryNodes("$.items[*]"u8, data);

        Assert.AreEqual(viaString.Count, viaUtf8.Count);
        Assert.AreEqual(3, viaUtf8.Count);
    }

    [TestMethod]
    public void Utf8QueryNoMatchIsEmpty()
    {
        JsonElement data = JsonElement.ParseValue("{\"items\":[]}");

        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[*]"u8, data);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Utf8QueryWithCallerBuffer()
    {
        JsonElement data = JsonElement.ParseValue("{\"items\":[1,2]}");
        Span<JsonElement> buffer = new JsonElement[8];

        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[*]"u8, data, buffer);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Utf8QueryFilterWithValue()
    {
        JsonElement data = JsonElement.ParseValue("[{\"status\":\"ok\"},{\"status\":\"bad\"}]");

        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?@.status == \"ok\"]"u8, data);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Utf8QueryIsCachedAcrossCalls()
    {
        JsonElement data = JsonElement.ParseValue("{\"items\":[1,2,3]}");

        for (int i = 0; i < 3; i++)
        {
            using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[*]"u8, data);
            Assert.AreEqual(3, result.Count);
        }
    }
}