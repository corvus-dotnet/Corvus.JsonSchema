// <copyright file="JsonPropertyNameTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class JsonPropertyNameTests
{
    [TestMethod]
    public void StringBacking_HasStringBacking_True()
    {
        JsonPropertyName name = new("hello");
        Assert.IsTrue(name.HasStringBacking);
        Assert.IsFalse(name.HasJsonElementBacking);
    }

    [TestMethod]
    public void JsonElementBacking_HasJsonElementBacking_True()
    {
        using var doc = JsonDocument.Parse("\"hello\"");
        JsonPropertyName name = new(doc.RootElement);
        Assert.IsTrue(name.HasJsonElementBacking);
        Assert.IsFalse(name.HasStringBacking);
    }

    [TestMethod]
    public void ImplicitConversion_FromString()
    {
        JsonPropertyName name = "test";
        Assert.IsTrue(name.HasStringBacking);
        Assert.AreEqual("test", (string)name);
    }

    [TestMethod]
    public void ExplicitConversion_ToString()
    {
        JsonPropertyName name = new("value");
        string result = (string)name;
        Assert.AreEqual("value", result);
    }

    [TestMethod]
    public void Equals_StringBacking_StringBacking_Same()
    {
        JsonPropertyName a = new("abc");
        JsonPropertyName b = new("abc");
        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    [TestMethod]
    public void Equals_StringBacking_StringBacking_Different()
    {
        JsonPropertyName a = new("abc");
        JsonPropertyName b = new("xyz");
        Assert.IsFalse(a.Equals(b));
        Assert.IsFalse(a == b);
        Assert.IsTrue(a != b);
    }

    [TestMethod]
    public void Equals_JsonElementBacking_JsonElementBacking_Same()
    {
        using var doc1 = JsonDocument.Parse("\"hello\"");
        using var doc2 = JsonDocument.Parse("\"hello\"");
        JsonPropertyName a = new(doc1.RootElement);
        JsonPropertyName b = new(doc2.RootElement);
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_JsonElementBacking_StringBacking_Same()
    {
        using var doc = JsonDocument.Parse("\"world\"");
        JsonPropertyName a = new(doc.RootElement);
        JsonPropertyName b = new("world");
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_StringBacking_JsonElementBacking_Same()
    {
        using var doc = JsonDocument.Parse("\"world\"");
        JsonPropertyName a = new("world");
        JsonPropertyName b = new(doc.RootElement);
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void CompareTo_StringBacking_StringBacking_Less()
    {
        JsonPropertyName a = new("aaa");
        JsonPropertyName b = new("bbb");
        Assert.IsTrue(a.CompareTo(b) < 0);
        Assert.IsTrue(a < b);
        Assert.IsTrue(a <= b);
    }

    [TestMethod]
    public void CompareTo_StringBacking_StringBacking_Greater()
    {
        JsonPropertyName a = new("zzz");
        JsonPropertyName b = new("aaa");
        Assert.IsTrue(a.CompareTo(b) > 0);
        Assert.IsTrue(a > b);
        Assert.IsTrue(a >= b);
    }

    [TestMethod]
    public void CompareTo_StringBacking_StringBacking_Equal()
    {
        JsonPropertyName a = new("same");
        JsonPropertyName b = new("same");
        Assert.AreEqual(0, a.CompareTo(b));
        Assert.IsTrue(a <= b);
        Assert.IsTrue(a >= b);
    }

    [TestMethod]
    public void CompareTo_JsonElementBacking_StringBacking()
    {
        using var doc = JsonDocument.Parse("\"abc\"");
        JsonPropertyName a = new(doc.RootElement);
        JsonPropertyName b = new("xyz");
        Assert.IsTrue(a.CompareTo(b) < 0);
    }

    [TestMethod]
    public void CompareTo_JsonElementBacking_JsonElementBacking()
    {
        using var doc1 = JsonDocument.Parse("\"abc\"");
        using var doc2 = JsonDocument.Parse("\"xyz\"");
        JsonPropertyName a = new(doc1.RootElement);
        JsonPropertyName b = new(doc2.RootElement);
        Assert.IsTrue(a.CompareTo(b) < 0);
    }

    [TestMethod]
    public void CompareTo_StringBacking_JsonElementBacking()
    {
        using var doc = JsonDocument.Parse("\"xyz\"");
        JsonPropertyName a = new("abc");
        JsonPropertyName b = new(doc.RootElement);
        Assert.IsTrue(a.CompareTo(b) < 0);
    }

    [TestMethod]
    public void EqualsObject_JsonPropertyName()
    {
        JsonPropertyName a = new("test");
        object b = new JsonPropertyName("test");
        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void EqualsObject_String()
    {
        JsonPropertyName a = new("test");
        Assert.IsTrue(a.Equals((object)"test"));
    }

    [TestMethod]
    public void EqualsObject_CharArray()
    {
        JsonPropertyName a = new("hello");
        Assert.IsTrue(a.Equals((object)new char[] { 'h', 'e', 'l', 'l', 'o' }));
    }

    [TestMethod]
    public void EqualsObject_ByteArray()
    {
        JsonPropertyName a = new("hello");
        byte[] utf8 = Encoding.UTF8.GetBytes("hello");
        Assert.IsTrue(a.Equals((object)utf8));
    }

    [TestMethod]
    public void EqualsObject_JsonProperty()
    {
        using var doc = JsonDocument.Parse("{\"foo\": 1}");
        JsonPropertyName a = new("foo");
        foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
        {
            Assert.IsTrue(a.Equals((object)prop));
        }
    }

    [TestMethod]
    public void EqualsObject_DifferentType_ReturnsFalse()
    {
        JsonPropertyName a = new("test");
        Assert.IsFalse(a.Equals((object)42));
    }

    [TestMethod]
    public void EqualsJsonElement_StringBacking()
    {
        using var doc = JsonDocument.Parse("\"match\"");
        JsonPropertyName a = new("match");
        Assert.IsTrue(a.EqualsJsonElement(doc.RootElement));
    }

    [TestMethod]
    public void EqualsJsonElement_NonString_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse("42");
        JsonPropertyName a = new("42");
        Assert.IsFalse(a.EqualsJsonElement(doc.RootElement));
    }

    [TestMethod]
    public void EqualsJsonElement_JsonElementBacking()
    {
        using var doc1 = JsonDocument.Parse("\"name\"");
        using var doc2 = JsonDocument.Parse("\"name\"");
        JsonPropertyName a = new(doc1.RootElement);
        Assert.IsTrue(a.EqualsJsonElement(doc2.RootElement));
    }

    [TestMethod]
    public void EqualsPropertyNameOf_StringBacking()
    {
        using var doc = JsonDocument.Parse("{\"myProp\": 1}");
        JsonPropertyName a = new("myProp");
        foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
        {
            Assert.IsTrue(a.EqualsPropertyNameOf(prop));
        }
    }

    [TestMethod]
    public void EqualsPropertyNameOf_JsonElementBacking()
    {
        using var doc = JsonDocument.Parse("{\"myProp\": 1}");
        using var nameDoc = JsonDocument.Parse("\"myProp\"");
        JsonPropertyName a = new(nameDoc.RootElement);
        foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
        {
            Assert.IsTrue(a.EqualsPropertyNameOf(prop));
        }
    }

    [TestMethod]
    public void EqualsString_StringBacking()
    {
        JsonPropertyName a = new("test");
        Assert.IsTrue(a.EqualsString("test".AsSpan()));
        Assert.IsFalse(a.EqualsString("other".AsSpan()));
    }

    [TestMethod]
    public void EqualsString_JsonElementBacking()
    {
        using var doc = JsonDocument.Parse("\"test\"");
        JsonPropertyName a = new(doc.RootElement);
        Assert.IsTrue(a.EqualsString("test".AsSpan()));
        Assert.IsFalse(a.EqualsString("other".AsSpan()));
    }

    [TestMethod]
    public void EqualsUtf8String_StringBacking()
    {
        JsonPropertyName a = new("hello");
        byte[] utf8 = Encoding.UTF8.GetBytes("hello");
        Assert.IsTrue(a.EqualsUtf8String(utf8));
        Assert.IsFalse(a.EqualsUtf8String(Encoding.UTF8.GetBytes("world")));
    }

    [TestMethod]
    public void EqualsUtf8String_JsonElementBacking()
    {
        using var doc = JsonDocument.Parse("\"hello\"");
        JsonPropertyName a = new(doc.RootElement);
        byte[] utf8 = Encoding.UTF8.GetBytes("hello");
        Assert.IsTrue(a.EqualsUtf8String(utf8));
    }

    [TestMethod]
    public void Equals_Utf8Name_And_StringName_StringBacking()
    {
        JsonPropertyName a = new("test");
        Assert.IsTrue(a.Equals(Encoding.UTF8.GetBytes("test"), "test"));
        Assert.IsFalse(a.Equals(Encoding.UTF8.GetBytes("other"), "other"));
    }

    [TestMethod]
    public void Equals_Utf8Name_And_StringName_JsonElementBacking()
    {
        using var doc = JsonDocument.Parse("\"test\"");
        JsonPropertyName a = new(doc.RootElement);
        Assert.IsTrue(a.Equals(Encoding.UTF8.GetBytes("test"), "test"));
    }

    [TestMethod]
    public void GetHashCode_StringBacking()
    {
        JsonPropertyName a = new("test");
        JsonPropertyName b = new("test");
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void GetHashCode_JsonElementBacking()
    {
        using var doc1 = JsonDocument.Parse("\"test\"");
        using var doc2 = JsonDocument.Parse("\"test\"");
        JsonPropertyName a = new(doc1.RootElement);
        JsonPropertyName b = new(doc2.RootElement);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void ToString_StringBacking()
    {
        JsonPropertyName a = new("hello");
        Assert.AreEqual("hello", a.ToString());
    }

    [TestMethod]
    public void ToString_JsonElementBacking()
    {
        using var doc = JsonDocument.Parse("\"hello\"");
        JsonPropertyName a = new(doc.RootElement);
        Assert.AreEqual("hello", a.ToString());
    }

    [TestMethod]
    public void GetString_StringBacking()
    {
        JsonPropertyName a = new("value");
        Assert.AreEqual("value", a.GetString());
    }

    [TestMethod]
    public void GetString_JsonElementBacking()
    {
        using var doc = JsonDocument.Parse("\"value\"");
        JsonPropertyName a = new(doc.RootElement);
        Assert.AreEqual("value", a.GetString());
    }

    [TestMethod]
    public void TryGetString_StringBacking()
    {
        JsonPropertyName a = new("result");
        Assert.IsTrue(a.TryGetString(out string? value));
        Assert.AreEqual("result", value);
    }

    [TestMethod]
    public void TryGetString_JsonElementBacking()
    {
        using var doc = JsonDocument.Parse("\"result\"");
        JsonPropertyName a = new(doc.RootElement);
        Assert.IsTrue(a.TryGetString(out string? value));
        Assert.AreEqual("result", value);
    }

    [TestMethod]
    public void ParseValue_FromCharSpan()
    {
        JsonPropertyName result = JsonPropertyName.ParseValue("\"test\"".AsSpan());
        Assert.AreEqual("test", result.GetString());
    }

    [TestMethod]
    public void ParseValue_FromUtf8Span()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("\"test\"");
        JsonPropertyName result = JsonPropertyName.ParseValue(utf8);
        Assert.AreEqual("test", result.GetString());
    }

    [TestMethod]
    public void IsMatch_StringBacking_Matches()
    {
        JsonPropertyName a = new("hello123");
        Regex regex = new("^hello\\d+$");
        Assert.IsTrue(a.IsMatch(regex));
    }

    [TestMethod]
    public void IsMatch_StringBacking_NoMatch()
    {
        JsonPropertyName a = new("world");
        Regex regex = new("^hello\\d+$");
        Assert.IsFalse(a.IsMatch(regex));
    }

    [TestMethod]
    public void IsMatch_JsonElementBacking_Matches()
    {
        using var doc = JsonDocument.Parse("\"hello123\"");
        JsonPropertyName a = new(doc.RootElement);
        Regex regex = new("^hello\\d+$");
        Assert.IsTrue(a.IsMatch(regex));
    }

    [TestMethod]
    public void WriteTo_StringBacking()
    {
        JsonPropertyName name = new("myProp");
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            name.WriteTo(writer);
            writer.WriteNumberValue(42);
            writer.WriteEndObject();
        }

        string json = Encoding.UTF8.GetString(ms.ToArray());
        Assert.AreEqual("{\"myProp\":42}", json);
    }

    [TestMethod]
    public void WriteTo_JsonElementBacking()
    {
        using var doc = JsonDocument.Parse("\"myProp\"");
        JsonPropertyName name = new(doc.RootElement);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            name.WriteTo(writer);
            writer.WriteNumberValue(42);
            writer.WriteEndObject();
        }

        string json = Encoding.UTF8.GetString(ms.ToArray());
        Assert.AreEqual("{\"myProp\":42}", json);
    }

    [TestMethod]
    public void EqualsJsonString_StringBacking()
    {
        JsonPropertyName a = new("test");
        JsonString js = new("test");
        Assert.IsTrue(a.EqualsJsonString(js));
    }

    [TestMethod]
    public void EqualsJsonString_JsonElementBacking()
    {
        using var doc = JsonDocument.Parse("\"test\"");
        JsonPropertyName a = new(doc.RootElement);
        JsonString js = new("test");
        Assert.IsTrue(a.EqualsJsonString(js));
    }

    [TestMethod]
    public void EqualsJsonString_WithJsonElementBacking_InJsonString()
    {
        using var doc = JsonDocument.Parse("\"test\"");
        JsonPropertyName a = new("test");
        JsonString js = JsonString.FromJson(doc.RootElement);
        Assert.IsTrue(a.EqualsJsonString(js));
    }

    [TestMethod]
    public void TryGetProperty_StringBacking()
    {
        using var doc = JsonDocument.Parse("{\"name\": \"value\"}");
        JsonPropertyName name = new("name");
        Assert.IsTrue(name.TryGetProperty(doc.RootElement, out JsonElement value));
        Assert.AreEqual("value", value.GetString());
    }

    [TestMethod]
    public void FromJsonString_WithJsonElementBacking()
    {
        using var doc = JsonDocument.Parse("\"propname\"");
        JsonString js = JsonString.FromJson(doc.RootElement);
        JsonPropertyName result = JsonPropertyName.FromJsonString(js);
        Assert.IsTrue(result.HasJsonElementBacking);
        Assert.AreEqual("propname", result.GetString());
    }

    [TestMethod]
    public void FromJsonString_WithStringBacking()
    {
        JsonString js = new("propname");
        JsonPropertyName result = JsonPropertyName.FromJsonString(js);
        Assert.IsTrue(result.HasStringBacking);
        Assert.AreEqual("propname", result.GetString());
    }
}