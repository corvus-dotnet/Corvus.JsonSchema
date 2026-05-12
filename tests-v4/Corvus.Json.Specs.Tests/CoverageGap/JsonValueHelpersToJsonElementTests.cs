// <copyright file="JsonValueHelpersToJsonElementTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class JsonValueHelpersToJsonElementTests
{
    [TestMethod]
    public void BoolToJsonElement_True()
    {
        JsonElement result = JsonValueHelpers.BoolToJsonElement(true);
        Assert.AreEqual(JsonValueKind.True, result.ValueKind);
    }

    [TestMethod]
    public void BoolToJsonElement_False()
    {
        JsonElement result = JsonValueHelpers.BoolToJsonElement(false);
        Assert.AreEqual(JsonValueKind.False, result.ValueKind);
    }

    [TestMethod]
    public void NumberToJsonElement_Integer()
    {
        BinaryJsonNumber num = new(42);
        JsonElement result = JsonValueHelpers.NumberToJsonElement(num);
        Assert.AreEqual(JsonValueKind.Number, result.ValueKind);
        Assert.AreEqual(42, result.GetInt32());
    }

    [TestMethod]
    public void NumberToJsonElement_Double()
    {
        BinaryJsonNumber num = new(3.14);
        JsonElement result = JsonValueHelpers.NumberToJsonElement(num);
        Assert.AreEqual(JsonValueKind.Number, result.ValueKind);
        Assert.AreEqual(3.14, result.GetDouble(), 0.001);
    }

    [TestMethod]
    public void NumberToJsonElement_Negative()
    {
        BinaryJsonNumber num = new(-99);
        JsonElement result = JsonValueHelpers.NumberToJsonElement(num);
        Assert.AreEqual(-99, result.GetInt32());
    }

    [TestMethod]
    public void ObjectToJsonElement_Empty()
    {
        ImmutableList<JsonObjectProperty> props = ImmutableList<JsonObjectProperty>.Empty;
        JsonElement result = JsonValueHelpers.ObjectToJsonElement(props);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        Assert.AreEqual(0, result.EnumerateObject().Count());
    }

    [TestMethod]
    public void ObjectToJsonElement_WithProperties()
    {
        var props = ImmutableList.Create(
            new JsonObjectProperty(new JsonPropertyName("name"), new JsonAny("Alice")),
            new JsonObjectProperty(new JsonPropertyName("age"), JsonAny.Parse("30")));
        JsonElement result = JsonValueHelpers.ObjectToJsonElement(props);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        Assert.AreEqual("Alice", result.GetProperty("name").GetString());
        Assert.AreEqual(30, result.GetProperty("age").GetInt32());
    }

    [TestMethod]
    public void ArrayToJsonElement_Empty()
    {
        ImmutableList<JsonAny> items = ImmutableList<JsonAny>.Empty;
        JsonElement result = JsonValueHelpers.ArrayToJsonElement(items);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(0, result.GetArrayLength());
    }

    [TestMethod]
    public void ArrayToJsonElement_WithItems()
    {
        var items = ImmutableList.Create(
            JsonAny.Parse("1"),
            new JsonAny("hello"),
            JsonAny.Null);
        JsonElement result = JsonValueHelpers.ArrayToJsonElement(items);
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(3, result.GetArrayLength());
        Assert.AreEqual(1, result[0].GetInt32());
        Assert.AreEqual("hello", result[1].GetString());
        Assert.AreEqual(JsonValueKind.Null, result[2].ValueKind);
    }

    [TestMethod]
    public void StringToJsonElement_Simple()
    {
        JsonElement result = JsonValueHelpers.StringToJsonElement("hello");
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
        Assert.AreEqual("hello", result.GetString());
    }

    [TestMethod]
    public void StringToJsonElement_Empty()
    {
        JsonElement result = JsonValueHelpers.StringToJsonElement(string.Empty);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
        Assert.AreEqual(string.Empty, result.GetString());
    }

    [TestMethod]
    public void StringToJsonElement_WithSpecialChars()
    {
        JsonElement result = JsonValueHelpers.StringToJsonElement("a\"b\\c\n");
        Assert.AreEqual("a\"b\\c\n", result.GetString());
    }

    [TestMethod]
    public void StringToJsonElement_Unicode()
    {
        JsonElement result = JsonValueHelpers.StringToJsonElement("\U0001D11E");
        Assert.AreEqual("\U0001D11E", result.GetString());
    }
}