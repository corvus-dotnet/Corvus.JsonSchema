// <copyright file="PropertyBackingExtensionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class PropertyBackingExtensionsTests
{
    private static JsonObjectProperty MakeProp(string name, string value)
    {
        using var doc = JsonDocument.Parse($"\"{value}\"");
        return new JsonObjectProperty(new JsonPropertyName(name), JsonAny.FromJson(doc.RootElement));
    }

    // =====================
    // ImmutableList SetItem
    // =====================
    [TestMethod]
    public void SetItem_ImmutableList_Existing_ReplacesValue()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"), MakeProp("b", "2"));

        using var newValDoc = JsonDocument.Parse("\"99\"");
        var updated = list.SetItem(new JsonPropertyName("a"), JsonAny.FromJson(newValDoc.RootElement));

        Assert.AreEqual(2, updated.Count);
        Assert.IsTrue(updated[0].NameEquals("a"));
    }

    [TestMethod]
    public void SetItem_ImmutableList_NotFound_AddsProperty()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));

        using var newValDoc = JsonDocument.Parse("\"99\"");
        var updated = list.SetItem(new JsonPropertyName("c"), JsonAny.FromJson(newValDoc.RootElement));

        Assert.AreEqual(2, updated.Count);
    }

    // =====================
    // ImmutableList Remove (JsonPropertyName)
    // =====================
    [TestMethod]
    public void Remove_ImmutableList_JsonPropertyName_Found()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"), MakeProp("b", "2"));
        var result = list.Remove(new JsonPropertyName("a"));
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result[0].NameEquals("b"));
    }

    [TestMethod]
    public void Remove_ImmutableList_JsonPropertyName_NotFound()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));
        var result = list.Remove(new JsonPropertyName("z"));
        Assert.AreEqual(1, result.Count);
    }

    // =====================
    // ImmutableList Remove (string)
    // =====================
    [TestMethod]
    public void Remove_ImmutableList_String_Found()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"), MakeProp("b", "2"));
        var result = list.Remove("b");
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Remove_ImmutableList_String_NotFound()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));
        var result = list.Remove("z");
        Assert.AreEqual(1, result.Count);
    }

    // =====================
    // ImmutableList Remove (ReadOnlySpan<char>)
    // =====================
    [TestMethod]
    public void Remove_ImmutableList_SpanChar_Found()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"), MakeProp("b", "2"));
        var result = list.Remove("a".AsSpan());
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Remove_ImmutableList_SpanChar_NotFound()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));
        var result = list.Remove("z".AsSpan());
        Assert.AreEqual(1, result.Count);
    }

    // =====================
    // ImmutableList Remove (ReadOnlySpan<byte>)
    // =====================
    [TestMethod]
    public void Remove_ImmutableList_SpanByte_Found()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"), MakeProp("b", "2"));
        var result = list.Remove("a"u8);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Remove_ImmutableList_SpanByte_NotFound()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));
        var result = list.Remove("z"u8);
        Assert.AreEqual(1, result.Count);
    }

    // =====================
    // Builder SetItem
    // =====================
    [TestMethod]
    public void SetItem_Builder_Existing_ReplacesValue()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(MakeProp("a", "1"));
        builder.Add(MakeProp("b", "2"));

        using var newValDoc = JsonDocument.Parse("\"99\"");
        builder.SetItem(new JsonPropertyName("a"), JsonAny.FromJson(newValDoc.RootElement));

        Assert.AreEqual(2, builder.Count);
    }

    [TestMethod]
    public void SetItem_Builder_NotFound_AddsProperty()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(MakeProp("a", "1"));

        using var newValDoc = JsonDocument.Parse("\"99\"");
        builder.SetItem(new JsonPropertyName("c"), JsonAny.FromJson(newValDoc.RootElement));

        Assert.AreEqual(2, builder.Count);
    }

    // =====================
    // Builder Add
    // =====================
    [TestMethod]
    public void Add_Builder()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();

        using var valDoc = JsonDocument.Parse("\"hello\"");
        builder.Add(new JsonPropertyName("x"), JsonAny.FromJson(valDoc.RootElement));

        Assert.AreEqual(1, builder.Count);
    }

    // =====================
    // Builder Remove (JsonPropertyName)
    // =====================
    [TestMethod]
    public void Remove_Builder_JsonPropertyName_Found()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(MakeProp("a", "1"));
        builder.Add(MakeProp("b", "2"));

        builder.Remove(new JsonPropertyName("a"));

        Assert.AreEqual(1, builder.Count);
        Assert.IsTrue(builder[0].NameEquals("b"));
    }

    [TestMethod]
    public void Remove_Builder_JsonPropertyName_NotFound()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(MakeProp("a", "1"));

        builder.Remove(new JsonPropertyName("z"));

        Assert.AreEqual(1, builder.Count);
    }

    // =====================
    // Builder Remove (string)
    // =====================
    [TestMethod]
    public void Remove_Builder_String_Found()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(MakeProp("a", "1"));
        builder.Add(MakeProp("b", "2"));

        builder.Remove("b");

        Assert.AreEqual(1, builder.Count);
    }

    [TestMethod]
    public void Remove_Builder_String_NotFound()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(MakeProp("a", "1"));

        builder.Remove("z");

        Assert.AreEqual(1, builder.Count);
    }

    // =====================
    // Builder Remove (ReadOnlySpan<char>)
    // =====================
    [TestMethod]
    public void Remove_Builder_SpanChar_Found()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(MakeProp("a", "1"));
        builder.Add(MakeProp("b", "2"));

        builder.Remove("a".AsSpan());

        Assert.AreEqual(1, builder.Count);
    }

    [TestMethod]
    public void Remove_Builder_SpanChar_NotFound()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(MakeProp("a", "1"));

        builder.Remove("z".AsSpan());

        Assert.AreEqual(1, builder.Count);
    }

    // =====================
    // Builder Remove (ReadOnlySpan<byte>)
    // =====================
    [TestMethod]
    public void Remove_Builder_SpanByte_Found()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(MakeProp("a", "1"));
        builder.Add(MakeProp("b", "2"));

        builder.Remove("a"u8);

        Assert.AreEqual(1, builder.Count);
    }

    [TestMethod]
    public void Remove_Builder_SpanByte_NotFound()
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(MakeProp("a", "1"));

        builder.Remove("z"u8);

        Assert.AreEqual(1, builder.Count);
    }

    // =====================
    // TryGetValue
    // =====================
    [TestMethod]
    public void TryGetValue_JsonPropertyName_Found()
    {
        var list = ImmutableList.Create(MakeProp("a", "hello"));
        Assert.IsTrue(list.TryGetValue(new JsonPropertyName("a"), out _));
    }

    [TestMethod]
    public void TryGetValue_JsonPropertyName_NotFound()
    {
        var list = ImmutableList.Create(MakeProp("a", "hello"));
        Assert.IsFalse(list.TryGetValue(new JsonPropertyName("z"), out _));
    }

    [TestMethod]
    public void TryGetValue_String_Found()
    {
        var list = ImmutableList.Create(MakeProp("a", "hello"));
        Assert.IsTrue(list.TryGetValue("a", out _));
    }

    [TestMethod]
    public void TryGetValue_String_NotFound()
    {
        var list = ImmutableList.Create(MakeProp("a", "hello"));
        Assert.IsFalse(list.TryGetValue("z", out _));
    }

    // =====================
    // ContainsKey
    // =====================
    [TestMethod]
    public void ContainsKey_JsonPropertyName_Found()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));
        Assert.IsTrue(list.ContainsKey(new JsonPropertyName("a")));
    }

    [TestMethod]
    public void ContainsKey_JsonPropertyName_NotFound()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));
        Assert.IsFalse(list.ContainsKey(new JsonPropertyName("z")));
    }

    [TestMethod]
    public void ContainsKey_SpanChar_Found()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));
        Assert.IsTrue(list.ContainsKey("a".AsSpan()));
    }

    [TestMethod]
    public void ContainsKey_SpanChar_NotFound()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));
        Assert.IsFalse(list.ContainsKey("z".AsSpan()));
    }

    [TestMethod]
    public void ContainsKey_SpanByte_Found()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));
        Assert.IsTrue(list.ContainsKey("a"u8));
    }

    [TestMethod]
    public void ContainsKey_SpanByte_NotFound()
    {
        var list = ImmutableList.Create(MakeProp("a", "1"));
        Assert.IsFalse(list.ContainsKey("z"u8));
    }
}