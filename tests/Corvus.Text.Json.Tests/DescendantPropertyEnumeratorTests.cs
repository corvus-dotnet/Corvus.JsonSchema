// <copyright file="DescendantPropertyEnumeratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="DescendantPropertyEnumerator"/> and
/// <see cref="JsonElement.EnumerateDescendantProperties(System.ReadOnlySpan{byte})"/>.
/// </summary>
public static class DescendantPropertyEnumeratorTests
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

    [Fact]
    public static void SingleMatchAtTopLevel()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"name": "Alice", "age": 30}""");

        List<string> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("name"u8))
        {
            results.Add(value.GetString());
        }

        Assert.Single(results);
        Assert.Equal("Alice", results[0]);
    }

    [Fact]
    public static void MultipleMatchesAtSameLevel()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a": {"x": 1}, "b": {"x": 2}}""");

        List<int> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("x"u8))
        {
            results.Add(value.GetInt32());
        }

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0]);
        Assert.Equal(2, results[1]);
    }

    [Fact]
    public static void NestedMatchAtMultipleDepths()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"v": 1, "inner": {"v": 2}}""");

        List<int> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("v"u8))
        {
            results.Add(value.GetInt32());
        }

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0]);
        Assert.Equal(2, results[1]);
    }

    [Fact]
    public static void DeeplyNestedMatch()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a": {"b": {"c": {"target": "found"}}}}""");

        List<string> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("target"u8))
        {
            results.Add(value.GetString());
        }

        Assert.Single(results);
        Assert.Equal("found", results[0]);
    }

    [Fact]
    public static void ArrayContainingObjects()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """[{"name": "a"}, {"name": "b"}, {"other": "c"}]""");

        List<string> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("name"u8))
        {
            results.Add(value.GetString());
        }

        Assert.Equal(2, results.Count);
        Assert.Equal("a", results[0]);
        Assert.Equal("b", results[1]);
    }

    [Fact]
    public static void NoMatches()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"a": 1, "b": {"c": 2}}""");

        int count = 0;
        foreach (JsonElement _ in doc.RootElement.EnumerateDescendantProperties("missing"u8))
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    [Fact]
    public static void EmptyObject()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}");

        int count = 0;
        foreach (JsonElement _ in doc.RootElement.EnumerateDescendantProperties("x"u8))
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    [Fact]
    public static void EmptyArray()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("[]");

        int count = 0;
        foreach (JsonElement _ in doc.RootElement.EnumerateDescendantProperties("x"u8))
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    [Fact]
    public static void ScalarElement()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"key": "value"}""");

        JsonElement scalar = doc.RootElement.GetProperty("key"u8);

        int count = 0;
        foreach (JsonElement _ in scalar.EnumerateDescendantProperties("anything"u8))
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    [Fact]
    public static void BookstoreAuthors()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(BookstoreJson);

        List<string> authors = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("author"u8))
        {
            authors.Add(value.GetString());
        }

        Assert.Equal(4, authors.Count);
        Assert.Equal("Nigel Rees", authors[0]);
        Assert.Equal("Evelyn Waugh", authors[1]);
        Assert.Equal("Herman Melville", authors[2]);
        Assert.Equal("J. R. R. Tolkien", authors[3]);
    }

    [Fact]
    public static void BookstorePrices()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(BookstoreJson);

        List<decimal> prices = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("price"u8))
        {
            prices.Add(value.GetDecimal());
        }

        Assert.Equal(5, prices.Count);
        Assert.Equal(8.95m, prices[0]);
        Assert.Equal(12.99m, prices[1]);
        Assert.Equal(8.99m, prices[2]);
        Assert.Equal(22.99m, prices[3]);
        Assert.Equal(399m, prices[4]);
    }

    [Fact]
    public static void MatchedValueIsObject()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"data": {"nested": {"inner": true}}}""");

        List<JsonElement> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("nested"u8))
        {
            results.Add(value);
        }

        Assert.Single(results);
        Assert.Equal(JsonValueKind.Object, results[0].ValueKind);
        Assert.True(results[0].GetProperty("inner"u8).GetBoolean());
    }

    [Fact]
    public static void MatchedValueIsArray()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"items": [1, 2, 3]}""");

        List<JsonElement> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("items"u8))
        {
            results.Add(value);
        }

        Assert.Single(results);
        Assert.Equal(JsonValueKind.Array, results[0].ValueKind);
        Assert.Equal(3, results[0].GetArrayLength());
    }

    [Fact]
    public static void EscapedPropertyName()
    {
        // JSON property name with escape: "na\u006De" is "name" unescaped
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"na\u006De": "escaped"}""");

        List<string> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("name"u8))
        {
            results.Add(value.GetString());
        }

        Assert.Single(results);
        Assert.Equal("escaped", results[0]);
    }

    [Fact]
    public static void SubtreeSearch()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(BookstoreJson);

        JsonElement store = doc.RootElement.GetProperty("store"u8);

        List<string> authors = [];
        foreach (JsonElement value in store.EnumerateDescendantProperties("author"u8))
        {
            authors.Add(value.GetString());
        }

        Assert.Equal(4, authors.Count);
    }

    [Fact]
    public static void SameNameAtDifferentNestingLevels()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"x": 0, "a": {"x": 1, "b": {"x": 2}}}""");

        List<int> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("x"u8))
        {
            results.Add(value.GetInt32());
        }

        Assert.Equal(3, results.Count);
        Assert.Equal(0, results[0]);
        Assert.Equal(1, results[1]);
        Assert.Equal(2, results[2]);
    }

    [Fact]
    public static void ObjectInsideArray()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """[[{"k": 1}], [{"k": 2}, {"k": 3}]]""");

        List<int> results = [];
        foreach (JsonElement value in doc.RootElement.EnumerateDescendantProperties("k"u8))
        {
            results.Add(value.GetInt32());
        }

        Assert.Equal(3, results.Count);
        Assert.Equal(1, results[0]);
        Assert.Equal(2, results[1]);
        Assert.Equal(3, results[2]);
    }
}