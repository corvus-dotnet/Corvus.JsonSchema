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
        Assert.AreEqual("active=true", result);
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
}