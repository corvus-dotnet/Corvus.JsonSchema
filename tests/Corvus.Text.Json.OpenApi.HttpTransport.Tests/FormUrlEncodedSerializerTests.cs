// <copyright file="FormUrlEncodedSerializerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi.HttpTransport.Tests;

[TestClass]
public class FormUrlEncodedSerializerTests
{
    // ── Primitive values ──────────────────────────────────────────────────
    [TestMethod]
    public void Serialize_StringProperty()
    {
        string result = SerializeJson("""{"name":"Alice"}""");
        Assert.AreEqual("name=Alice", result);
    }

    [TestMethod]
    public void Serialize_NumberProperty()
    {
        string result = SerializeJson("""{"age":30}""");
        Assert.AreEqual("age=30", result);
    }

    [TestMethod]
    public void Serialize_BooleanProperty()
    {
        string result = SerializeJson("""{"active":true}""");
        Assert.AreEqual("active=True", result);
    }

    [TestMethod]
    public void Serialize_NullProperty()
    {
        string result = SerializeJson("""{"name":null}""");
        Assert.AreEqual("name=", result);
    }

    [TestMethod]
    public void Serialize_MultipleProperties()
    {
        string result = SerializeJson("""{"a":"1","b":"2"}""");
        Assert.AreEqual("a=1&b=2", result);
    }

    // ── Error handling ────────────────────────────────────────────────────
    [TestMethod]
    public void Serialize_NonObjectThrows()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SerializeJson("""[1,2,3]"""));
    }

    // ── Exploded arrays (explode=true) ────────────────────────────────────
    [TestMethod]
    public void Serialize_ExplodedArray()
    {
        string result = SerializeJson(
            """{"tags":["a","b","c"]}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["tags"] = new(Explode: true),
            });

        Assert.AreEqual("tags=a&tags=b&tags=c", result);
    }

    [TestMethod]
    public void Serialize_ExplodedArrayWithAllowReserved()
    {
        string result = SerializeJson(
            """{"path":["/api","/v2"]}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["path"] = new(Explode: true, AllowReserved: true),
            });

        Assert.AreEqual("path=/api&path=/v2", result);
    }

    // ── Non-exploded arrays ───────────────────────────────────────────────
    [TestMethod]
    public void Serialize_NonExplodedArrayCommaDelimited()
    {
        string result = SerializeJson(
            """{"tags":["a","b","c"]}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["tags"] = new(Explode: false),
            });

        Assert.AreEqual("tags=a%2Cb%2Cc", result);
    }

    [TestMethod]
    public void Serialize_ExplodedArrayWithNullElement()
    {
        string result = SerializeJson(
            """{"tags":["a",null,"c"]}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["tags"] = new(Explode: true),
            });

        Assert.AreEqual("tags=a&tags=&tags=c", result);
    }

    [TestMethod]
    public void Serialize_NonExplodedArraySpaceDelimited()
    {
        string result = SerializeJson(
            """{"tags":["a","b","c"]}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["tags"] = new(Style: "spaceDelimited", Explode: false),
            });

        Assert.AreEqual("tags=a%20b%20c", result);
    }

    [TestMethod]
    public void Serialize_NonExplodedArrayPipeDelimited()
    {
        string result = SerializeJson(
            """{"tags":["a","b","c"]}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["tags"] = new(Style: "pipeDelimited", Explode: false),
            });

        Assert.AreEqual("tags=a%7Cb%7Cc", result);
    }

    [TestMethod]
    public void Serialize_NonExplodedArrayTabDelimited()
    {
        // OpenAPI 2.0 (Swagger) collectionFormat: tsv on formData parameters.
        string result = SerializeJson(
            """{"tags":["a","b","c"]}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["tags"] = new(Style: "tabDelimited", Explode: false),
            });

        Assert.AreEqual("tags=a%09b%09c", result);
    }

    // ── Exploded objects ──────────────────────────────────────────────────
    [TestMethod]
    public void Serialize_ExplodedObject()
    {
        string result = SerializeJson(
            """{"filter":{"color":"red","size":"large"}}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["filter"] = new(Explode: true),
            });

        Assert.AreEqual("color=red&size=large", result);
    }

    [TestMethod]
    public void Serialize_ExplodedObjectWithAllowReserved()
    {
        string result = SerializeJson(
            """{"filter":{"path":"/api"}}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["filter"] = new(Explode: true, AllowReserved: true),
            });

        Assert.AreEqual("path=/api", result);
    }

    // ── Non-exploded objects ──────────────────────────────────────────────
    [TestMethod]
    public void Serialize_NonExplodedObject()
    {
        string result = SerializeJson(
            """{"filter":{"color":"red","size":"large"}}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["filter"] = new(Explode: false),
            });

        Assert.AreEqual("filter=color%2Cred%2Csize%2Clarge", result);
    }

    // ── Deep object ───────────────────────────────────────────────────────
    [TestMethod]
    public void Serialize_DeepObject()
    {
        string result = SerializeJson(
            """{"filter":{"color":"red","size":"large"}}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["filter"] = new(Style: "deepObject"),
            });

        Assert.AreEqual("filter[color]=red&filter[size]=large", result);
    }

    // ── Percent encoding ──────────────────────────────────────────────────
    [TestMethod]
    public void Serialize_PercentEncodesSpecialCharacters()
    {
        string result = SerializeJson("""{"q":"hello world&more"}""");
        Assert.AreEqual("q=hello%20world%26more", result);
    }

    [TestMethod]
    public void Serialize_AllowReservedPreservesReservedCharacters()
    {
        string result = SerializeJson(
            """{"q":"hello/world"}""",
            new Dictionary<string, PropertyEncoding>
            {
                ["q"] = new(AllowReserved: true),
            });

        Assert.AreEqual("q=hello/world", result);
    }

    // ── Encodings-aware deserialization (OpenAPI 2.0 collectionFormat fields) ──
    [TestMethod]
    public void Deserialize_PipeDelimitedFieldSplitsIntoArray()
    {
        string json = DeserializeForm(
            "flags=a%7Cb%7Cc",
            new Dictionary<string, PropertyEncoding>
            {
                ["flags"] = new(Style: "pipeDelimited", Explode: false),
            });

        Assert.AreEqual("""{"flags":["a","b","c"]}""", json);
    }

    [TestMethod]
    public void Deserialize_TabDelimitedFieldSplitsIntoArray()
    {
        string json = DeserializeForm(
            "labels=x%09y",
            new Dictionary<string, PropertyEncoding>
            {
                ["labels"] = new(Style: "tabDelimited", Explode: false),
            });

        Assert.AreEqual("""{"labels":["x","y"]}""", json);
    }

    [TestMethod]
    public void Deserialize_SpaceDelimitedFieldSplitsIntoArray()
    {
        string json = DeserializeForm(
            "tags=one%20two",
            new Dictionary<string, PropertyEncoding>
            {
                ["tags"] = new(Style: "spaceDelimited", Explode: false),
            });

        Assert.AreEqual("""{"tags":["one","two"]}""", json);
    }

    [TestMethod]
    public void Deserialize_FormNonExplodedFieldSplitsOnComma()
    {
        string json = DeserializeForm(
            "ids=1,2,3",
            new Dictionary<string, PropertyEncoding>
            {
                ["ids"] = new(Style: "form", Explode: false),
            });

        Assert.AreEqual("""{"ids":[1,2,3]}""", json);
    }

    [TestMethod]
    public void Deserialize_ExplodedEncodingEntryKeepsRepeatedKeyBehavior()
    {
        string json = DeserializeForm(
            "tags=a&tags=b",
            new Dictionary<string, PropertyEncoding>
            {
                ["tags"] = new(Style: "form", Explode: true),
            });

        Assert.AreEqual("""{"tags":["a","b"]}""", json);
    }

    [TestMethod]
    public void Deserialize_FieldWithoutEncodingEntryStaysScalar()
    {
        string json = DeserializeForm(
            "note=a,b",
            new Dictionary<string, PropertyEncoding>
            {
                ["other"] = new(Style: "pipeDelimited", Explode: false),
            });

        Assert.AreEqual("""{"note":"a,b"}""", json);
    }

    [TestMethod]
    public void Deserialize_SingleElementDelimitedFieldYieldsSingletonArray()
    {
        string json = DeserializeForm(
            "flags=only",
            new Dictionary<string, PropertyEncoding>
            {
                ["flags"] = new(Style: "pipeDelimited", Explode: false),
            });

        Assert.AreEqual("""{"flags":["only"]}""", json);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static string SerializeJson(
        string json,
        Dictionary<string, PropertyEncoding>? encodings = null)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes(json));

        using MemoryStream ms = new();
        FormUrlEncodedSerializer.Serialize(doc.RootElement, ms, encodings);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string DeserializeForm(
        string formBody,
        Dictionary<string, PropertyEncoding>? encodings = null)
    {
        using ParsedJsonDocument<JsonElement> doc = FormUrlEncodedSerializer.Deserialize<JsonElement>(
            Encoding.UTF8.GetBytes(formBody), encodings);
        return doc.RootElement.ToString();
    }
}