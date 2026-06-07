// <copyright file="JsonPathUtf8QueryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Tests for the UTF-8 <see cref="JsonPathEvaluator.QueryNodes(System.ReadOnlySpan{byte}, in JsonElement)"/>
/// overload (exercised here because the JsonPath compliance test project requires a submodule).
/// </summary>
[TestClass]
public class JsonPathUtf8QueryTests
{
    [TestMethod]
    public void Utf8_overload_matches_string_overload()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "items": [1, 2, 3], "name": "x" }""");
        JsonElement data = doc.RootElement;

        using JsonPathResult viaString = JsonPathEvaluator.Default.QueryNodes("$.items[*]", data);
        using JsonPathResult viaUtf8 = JsonPathEvaluator.Default.QueryNodes("$.items[*]"u8, data);

        viaUtf8.Count.ShouldBe(viaString.Count);
        viaUtf8.Count.ShouldBe(3);
    }

    [TestMethod]
    public void Utf8_overload_no_match_is_empty()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "items": [] }""");

        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[*]"u8, doc.RootElement);
        result.Count.ShouldBe(0);
    }

    [TestMethod]
    public void Utf8_overload_with_caller_buffer()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "items": [1, 2] }""");
        Span<JsonElement> buffer = new JsonElement[8];

        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[*]"u8, doc.RootElement, buffer);
        result.Count.ShouldBe(2);
    }

    [TestMethod]
    public void Utf8_overload_filter_with_value()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""[ { "status": "ok" }, { "status": "bad" } ]""");

        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$[?@.status == \"ok\"]"u8, doc.RootElement);
        result.Count.ShouldBe(1);
    }

    [TestMethod]
    public void Utf8_overload_caches_repeated_queries()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "items": [1, 2, 3] }""");
        JsonElement data = doc.RootElement;

        for (int i = 0; i < 3; i++)
        {
            using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.items[*]"u8, data);
            result.Count.ShouldBe(3);
        }
    }

    private static ParsedJsonDocument<JsonElement> Parse(string json)
        => ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
}