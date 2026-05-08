// <copyright file="JsonValueHelpersComparisonsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class JsonValueHelpersComparisonsTests
{
    [TestMethod]
    public void CompareWithString_StringValue_Matches()
    {
        JsonString value = new("hello");
        Assert.IsTrue(JsonValueHelpers.CompareWithString(value, "hello".AsSpan()));
    }

    [TestMethod]
    public void CompareWithString_StringValue_DoesNotMatch()
    {
        JsonString value = new("hello");
        Assert.IsFalse(JsonValueHelpers.CompareWithString(value, "world".AsSpan()));
    }

    [TestMethod]
    public void CompareWithString_NonStringValue_ReturnsFalse()
    {
        JsonInteger value = new(42);
        Assert.IsFalse(JsonValueHelpers.CompareWithString(value, "42".AsSpan()));
    }

    [TestMethod]
    public void CompareWithString_JsonElement_StringMatches()
    {
        using var doc = JsonDocument.Parse("\"test\"");
        JsonString value = JsonString.FromJson(doc.RootElement);
        Assert.IsTrue(JsonValueHelpers.CompareWithString(value, "test".AsSpan()));
    }

    [TestMethod]
    public void CompareWithUtf8Bytes_StringValue_Matches()
    {
        JsonString value = new("hello");
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("hello");
        Assert.IsTrue(JsonValueHelpers.CompareWithUtf8Bytes(value, utf8));
    }

    [TestMethod]
    public void CompareWithUtf8Bytes_StringValue_DoesNotMatch()
    {
        JsonString value = new("hello");
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("world");
        Assert.IsFalse(JsonValueHelpers.CompareWithUtf8Bytes(value, utf8));
    }

    [TestMethod]
    public void CompareWithUtf8Bytes_NonStringValue_ReturnsFalse()
    {
        JsonInteger value = new(42);
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("42");
        Assert.IsFalse(JsonValueHelpers.CompareWithUtf8Bytes(value, utf8));
    }

    [TestMethod]
    public void CompareValues_SameStrings_ReturnsTrue()
    {
        JsonString a = new("same");
        JsonString b = new("same");
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_DifferentStrings_ReturnsFalse()
    {
        JsonString a = new("one");
        JsonString b = new("two");
        Assert.IsFalse(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_DifferentKinds_ReturnsFalse()
    {
        JsonString a = new("42");
        JsonInteger b = new(42);
        Assert.IsFalse(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_BothNull_ReturnsTrue()
    {
        JsonNull a = JsonNull.Null;
        JsonNull b = JsonNull.Null;
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_BothTrue_ReturnsTrue()
    {
        JsonBoolean a = new(true);
        JsonBoolean b = new(true);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_TrueAndFalse_DifferentKinds()
    {
        JsonBoolean a = new(true);
        JsonBoolean b = new(false);
        Assert.IsFalse(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_SameNumbers_ReturnsTrue()
    {
        JsonNumber a = new(3.14);
        JsonNumber b = new(3.14);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_DifferentNumbers_ReturnsFalse()
    {
        JsonNumber a = new(1.0);
        JsonNumber b = new(2.0);
        Assert.IsFalse(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_SameArrays_ReturnsTrue()
    {
        using var doc1 = JsonDocument.Parse("[1,2,3]");
        using var doc2 = JsonDocument.Parse("[1,2,3]");
        JsonArray a = JsonArray.FromJson(doc1.RootElement);
        JsonArray b = JsonArray.FromJson(doc2.RootElement);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_DifferentArrays_ReturnsFalse()
    {
        using var doc1 = JsonDocument.Parse("[1,2,3]");
        using var doc2 = JsonDocument.Parse("[1,2,4]");
        JsonArray a = JsonArray.FromJson(doc1.RootElement);
        JsonArray b = JsonArray.FromJson(doc2.RootElement);
        Assert.IsFalse(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_SameObjects_ReturnsTrue()
    {
        using var doc1 = JsonDocument.Parse("{\"a\":1}");
        using var doc2 = JsonDocument.Parse("{\"a\":1}");
        JsonObject a = JsonObject.FromJson(doc1.RootElement);
        JsonObject b = JsonObject.FromJson(doc2.RootElement);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_DifferentObjects_ReturnsFalse()
    {
        using var doc1 = JsonDocument.Parse("{\"a\":1}");
        using var doc2 = JsonDocument.Parse("{\"a\":2}");
        JsonObject a = JsonObject.FromJson(doc1.RootElement);
        JsonObject b = JsonObject.FromJson(doc2.RootElement);
        Assert.IsFalse(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_WithJsonAny_SameStrings()
    {
        JsonString a = new("test");
        JsonAny b = JsonAny.FromJson(JsonDocument.Parse("\"test\"").RootElement);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_WithJsonAny_DifferentKinds()
    {
        JsonString a = new("42");
        JsonAny b = JsonAny.FromJson(JsonDocument.Parse("42").RootElement);
        Assert.IsFalse(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_JsonAnyFirst_SameValues()
    {
        JsonAny a = JsonAny.FromJson(JsonDocument.Parse("\"test\"").RootElement);
        JsonString b = new("test");
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_JsonAnyBoth_SameValues()
    {
        JsonAny a = JsonAny.FromJson(JsonDocument.Parse("\"test\"").RootElement);
        JsonAny b = JsonAny.FromJson(JsonDocument.Parse("\"test\"").RootElement);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_JsonAnyBoth_DifferentValues()
    {
        JsonAny a = JsonAny.FromJson(JsonDocument.Parse("\"one\"").RootElement);
        JsonAny b = JsonAny.FromJson(JsonDocument.Parse("\"two\"").RootElement);
        Assert.IsFalse(JsonValueHelpers.CompareValues(a, b));
    }
}