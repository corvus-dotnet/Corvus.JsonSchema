// <copyright file="DescendantPropertyEnumeratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="DescendantPropertyEnumerator"/> and
/// <see cref="JsonElement.EnumerateDescendantProperties(System.ReadOnlySpan{byte})"/>.
/// </summary>
[TestClass]
public class DescendantPropertyEnumeratorTests
{
    private const string BookstoreJson = """
        {
            "store": {
                "book": [
                    {
                        "category": "reference",
                        "author": "Nigel Rees",
                        "title": "Sayings of the Century",
                        "price": 8.95
                    },
                    {
                        "category": "fiction",
                        "author": "Evelyn Waugh",
                        "title": "Sword of Honour",
                        "price": 12.99
                    },
                    {
                        "category": "fiction",
                        "author": "Herman Melville",
                        "title": "Moby Dick",
                        "isbn": "0-553-21311-3",
                        "price": 8.99
                    },
                    {
                        "category": "fiction",
                        "author": "J. R. R. Tolkien",
                        "title": "The Lord of the Rings",
                        "isbn": "0-395-19395-8",
                        "price": 22.99
                    }
                ],
                "bicycle": {
                    "color": "red",
                    "price": 399
                }
            }
        }
        """;

    [TestMethod]
    public void SingleMatchAtTopLevel()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"name": "Alice", "age": 30}""");

        List<string> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("name"u8))
        {
            results.Add(value.GetString());
        }

        Assert.AreEqual(1, (results).Count);
        Assert.AreEqual("Alice", results[0]);
    }

    [TestMethod]
    public void MultipleMatchesAtSameLevel()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a": {"x": 1}, "b": {"x": 2}}""");

        List<int> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("x"u8))
        {
            results.Add(value.GetInt32());
        }

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual(1, results[0]);
        Assert.AreEqual(2, results[1]);
    }

    [TestMethod]
    public void NestedMatchAtMultipleDepths()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"v": 1, "inner": {"v": 2}}""");

        List<int> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("v"u8))
        {
            results.Add(value.GetInt32());
        }

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual(1, results[0]);
        Assert.AreEqual(2, results[1]);
    }

    [TestMethod]
    public void DeeplyNestedMatch()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a": {"b": {"c": {"target": "found"}}}}""");

        List<string> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("target"u8))
        {
            results.Add(value.GetString());
        }

        Assert.AreEqual(1, (results).Count);
        Assert.AreEqual("found", results[0]);
    }

    [TestMethod]
    public void ArrayContainingObjects()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """[{"name": "a"}, {"name": "b"}, {"other": "c"}]""");

        List<string> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("name"u8))
        {
            results.Add(value.GetString());
        }

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("a", results[0]);
        Assert.AreEqual("b", results[1]);
    }

    [TestMethod]
    public void NoMatches()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a": 1, "b": {"c": 2}}""");

        int count = 0;
        foreach (JsonElement _ in doc.RootElement.EnumerateDescendantProperties("missing"u8))
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void EmptyObject()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}");

        int count = 0;
        foreach (JsonElement _ in doc.RootElement.EnumerateDescendantProperties("x"u8))
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void EmptyArray()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("[]");

        int count = 0;
        foreach (JsonElement _ in doc.RootElement.EnumerateDescendantProperties("x"u8))
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void ScalarElement()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"key": "value"}""");

        JsonElement scalar = doc.RootElement.GetProperty("key"u8);

        int count = 0;
        foreach (JsonElement _ in scalar.EnumerateDescendantProperties("anything"u8))
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void BookstoreAuthors()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(BookstoreJson);

        List<string> authors = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("author"u8))
        {
            authors.Add(value.GetString());
        }

        Assert.AreEqual(4, authors.Count);
        Assert.AreEqual("Nigel Rees", authors[0]);
        Assert.AreEqual("Evelyn Waugh", authors[1]);
        Assert.AreEqual("Herman Melville", authors[2]);
        Assert.AreEqual("J. R. R. Tolkien", authors[3]);
    }

    [TestMethod]
    public void BookstorePrices()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(BookstoreJson);

        List<decimal> prices = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("price"u8))
        {
            prices.Add(value.GetDecimal());
        }

        Assert.AreEqual(5, prices.Count);
        Assert.AreEqual(8.95m, prices[0]);
        Assert.AreEqual(12.99m, prices[1]);
        Assert.AreEqual(8.99m, prices[2]);
        Assert.AreEqual(22.99m, prices[3]);
        Assert.AreEqual(399m, prices[4]);
    }

    [TestMethod]
    public void MatchedValueIsObject()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"data": {"nested": {"inner": true}}}""");

        List<JsonElement> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("nested"u8))
        {
            results.Add(value);
        }

        Assert.AreEqual(1, (results).Count);
        Assert.AreEqual(JsonValueKind.Object, results[0].ValueKind);
        Assert.IsTrue(results[0].GetProperty("inner"u8).GetBoolean());
    }

    [TestMethod]
    public void MatchedValueIsArray()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"items": [1, 2, 3]}""");

        List<JsonElement> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("items"u8))
        {
            results.Add(value);
        }

        Assert.AreEqual(1, (results).Count);
        Assert.AreEqual(JsonValueKind.Array, results[0].ValueKind);
        Assert.AreEqual(3, results[0].GetArrayLength());
    }

    [TestMethod]
    public void EscapedPropertyName()
    {
        // JSON property name with escape: "na\u006De" is "name" unescaped
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"na\u006De": "escaped"}""");

        List<string> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("name"u8))
        {
            results.Add(value.GetString());
        }

        Assert.AreEqual(1, (results).Count);
        Assert.AreEqual("escaped", results[0]);
    }

    [TestMethod]
    public void SubtreeSearch()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(BookstoreJson);

        JsonElement store = doc.RootElement.GetProperty("store"u8);

        List<string> authors = [];
        foreach (JsonElement value in store.EnumerateDescendantProperties("author"u8))
        {
            authors.Add(value.GetString());
        }

        Assert.AreEqual(4, authors.Count);
    }

    [TestMethod]
    public void SameNameAtDifferentNestingLevels()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"x": 0, "a": {"x": 1, "b": {"x": 2}}}""");

        List<int> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("x"u8))
        {
            results.Add(value.GetInt32());
        }

        Assert.AreEqual(3, results.Count);
        Assert.AreEqual(0, results[0]);
        Assert.AreEqual(1, results[1]);
        Assert.AreEqual(2, results[2]);
    }

    [TestMethod]
    public void ObjectInsideArray()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """[[{"k": 1}], [{"k": 2}, {"k": 3}]]""");

        List<int> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("k"u8))
        {
            results.Add(value.GetInt32());
        }

        Assert.AreEqual(3, results.Count);
        Assert.AreEqual(1, results[0]);
        Assert.AreEqual(2, results[1]);
        Assert.AreEqual(3, results[2]);
    }
}
