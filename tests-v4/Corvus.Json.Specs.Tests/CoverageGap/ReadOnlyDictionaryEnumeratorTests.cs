// <copyright file="ReadOnlyDictionaryEnumeratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;

namespace Corvus.Json.Specs.Tests.CoverageGap;

/// <summary>
/// Tests for <see cref="ReadOnlyDictionaryJsonObjectEnumerator{T}"/> covering both
/// JsonElement-backed and ImmutableList-backed enumeration paths.
/// Targets 61 uncovered lines (0% coverage).
/// </summary>
[TestClass]
public class ReadOnlyDictionaryEnumeratorTests
{
    // ===================================
    // JsonElement-backed enumeration
    // ===================================
    [TestMethod]
    public void JsonElementBacked_EnumerateProperties()
    {
        using var doc = JsonDocument.Parse("""{"name":"Alice","age":30}""");
        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(doc.RootElement);

        var items = new List<KeyValuePair<JsonPropertyName, JsonAny>>();
        while (enumerator.MoveNext())
        {
            items.Add(enumerator.Current);
        }

        Assert.AreEqual(2, items.Count);
        Assert.AreEqual("name", items[0].Key.ToString());
        Assert.AreEqual("age", items[1].Key.ToString());

        enumerator.Dispose();
    }

    [TestMethod]
    public void JsonElementBacked_Reset_RestartsEnumeration()
    {
        using var doc = JsonDocument.Parse("""{"x":1,"y":2}""");
        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(doc.RootElement);

        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("x", enumerator.Current.Key.ToString());

        enumerator.Reset();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("x", enumerator.Current.Key.ToString());

        enumerator.Dispose();
    }

    [TestMethod]
    public void JsonElementBacked_GetEnumerator_ReturnsResetCopy()
    {
        using var doc = JsonDocument.Parse("""{"a":1}""");
        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(doc.RootElement);

        // Advance
        Assert.IsTrue(enumerator.MoveNext());

        // GetEnumerator returns a reset copy
        var enumerator2 = enumerator.GetEnumerator();
        Assert.IsTrue(enumerator2.MoveNext());
        Assert.AreEqual("a", enumerator2.Current.Key.ToString());

        enumerator.Dispose();
        enumerator2.Dispose();
    }

    [TestMethod]
    public void JsonElementBacked_IEnumerable_GetEnumerator()
    {
        using var doc = JsonDocument.Parse("""{"k":"v"}""");
        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(doc.RootElement);

        // Non-generic IEnumerable
        IEnumerable nonGeneric = enumerator;
        var nonGenericEnumerator = nonGeneric.GetEnumerator();
        Assert.IsTrue(nonGenericEnumerator.MoveNext());

        enumerator.Dispose();
    }

    [TestMethod]
    public void JsonElementBacked_IEnumerableGeneric_GetEnumerator()
    {
        using var doc = JsonDocument.Parse("""{"k":"v"}""");
        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(doc.RootElement);

        // Generic IEnumerable
        IEnumerable<KeyValuePair<JsonPropertyName, JsonAny>> generic = enumerator;
        var genericEnumerator = generic.GetEnumerator();
        Assert.IsTrue(genericEnumerator.MoveNext());

        enumerator.Dispose();
    }

    [TestMethod]
    public void JsonElementBacked_IEnumerator_Current()
    {
        using var doc = JsonDocument.Parse("""{"prop":"val"}""");
        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(doc.RootElement);

        enumerator.MoveNext();

        // Non-generic IEnumerator.Current
        IEnumerator nonGeneric = enumerator;
        object current = nonGeneric.Current;
        Assert.IsInstanceOfType<KeyValuePair<JsonPropertyName, JsonAny>>(current);

        enumerator.Dispose();
    }

    [TestMethod]
    public void JsonElementBacked_EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(doc.RootElement);

        Assert.IsFalse(enumerator.MoveNext());

        enumerator.Dispose();
    }

    // ===================================
    // PropertyBacking enumeration
    // ===================================
    [TestMethod]
    public void PropertyBacking_EnumerateProperties()
    {
        using var doc = JsonDocument.Parse("""{"name":"Bob","score":42}""");
        var props = ImmutableList.CreateBuilder<JsonObjectProperty>();
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            props.Add(new JsonObjectProperty(p));
        }

        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(props.ToImmutable());

        var items = new List<KeyValuePair<JsonPropertyName, JsonAny>>();
        while (enumerator.MoveNext())
        {
            items.Add(enumerator.Current);
        }

        Assert.AreEqual(2, items.Count);
        Assert.AreEqual("name", items[0].Key.ToString());
        Assert.AreEqual("score", items[1].Key.ToString());

        enumerator.Dispose();
    }

    [TestMethod]
    public void PropertyBacking_Reset_RestartsEnumeration()
    {
        using var doc = JsonDocument.Parse("""{"x":1}""");
        var props = ImmutableList.Create(new JsonObjectProperty(doc.RootElement.EnumerateObject().First()));
        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(props);

        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("x", enumerator.Current.Key.ToString());

        enumerator.Reset();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.AreEqual("x", enumerator.Current.Key.ToString());

        enumerator.Dispose();
    }

    [TestMethod]
    public void PropertyBacking_GetEnumerator()
    {
        using var doc = JsonDocument.Parse("""{"a":1}""");
        var props = ImmutableList.Create(new JsonObjectProperty(doc.RootElement.EnumerateObject().First()));
        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(props);

        enumerator.MoveNext();
        var enumerator2 = enumerator.GetEnumerator();
        Assert.IsTrue(enumerator2.MoveNext());

        enumerator.Dispose();
        enumerator2.Dispose();
    }

    [TestMethod]
    public void PropertyBacking_EmptyList()
    {
        var enumerator = new ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>(ImmutableList<JsonObjectProperty>.Empty);
        Assert.IsFalse(enumerator.MoveNext());
        enumerator.Dispose();
    }

    // ===================================
    // Default enumerator (Undefined backing)
    // ===================================
    [TestMethod]
    public void Default_MoveNext_ReturnsFalse()
    {
        var enumerator = default(ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>);
        Assert.IsFalse(enumerator.MoveNext());
    }

    [TestMethod]
    public void Default_Current_ReturnsDefaultKeyValuePair()
    {
        var enumerator = default(ReadOnlyDictionaryJsonObjectEnumerator<JsonAny>);
        var current = enumerator.Current;

        // Default backing: Current returns a KeyValuePair with default key and value
        Assert.AreEqual(JsonValueKind.Undefined, current.Value.ValueKind);
    }
}