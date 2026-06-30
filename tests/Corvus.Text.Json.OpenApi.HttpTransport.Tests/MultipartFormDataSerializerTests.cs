// <copyright file="MultipartFormDataSerializerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi.HttpTransport.Tests;

[TestClass]
public class MultipartFormDataSerializerTests
{
    private const string Boundary = "test-boundary";

    // ── Basic serialization ───────────────────────────────────────────────
    [TestMethod]
    public void Serialize_StringProperty()
    {
        string result = SerializeMultipart("""{"name":"Alice"}""");

        StringAssert.Contains(result, "name=\"name\"");
        StringAssert.Contains(result, "\r\n\r\nAlice\r\n");
        Assert.IsTrue(result.TrimEnd().EndsWith("--test-boundary--"));
    }

    [TestMethod]
    public void Serialize_NumberProperty()
    {
        string result = SerializeMultipart("""{"age":30}""");

        StringAssert.Contains(result, "name=\"age\"");
        StringAssert.Contains(result, "\r\n\r\n30\r\n");
    }

    [TestMethod]
    public void Serialize_BooleanProperty()
    {
        string result = SerializeMultipart("""{"active":true}""");

        StringAssert.Contains(result, "name=\"active\"");
        StringAssert.Contains(result, "\r\n\r\ntrue\r\n");
    }

    [TestMethod]
    public void Serialize_NullProperty()
    {
        string result = SerializeMultipart("""{"empty":null}""");

        StringAssert.Contains(result, "name=\"empty\"");

        // Null values produce empty body between headers and boundary.
        StringAssert.Contains(result, "\"empty\"\r\n\r\n\r\n--test-boundary--");
    }

    [TestMethod]
    public void Serialize_ObjectPropertyAsJson()
    {
        string result = SerializeMultipart("""{"meta":{"key":"val"}}""");

        StringAssert.Contains(result, "Content-Type: application/json");
        StringAssert.Contains(result, "{\"key\":\"val\"}");
    }

    [TestMethod]
    public void Serialize_ArrayPropertyAsJson()
    {
        string result = SerializeMultipart("""{"items":[1,2,3]}""");

        StringAssert.Contains(result, "Content-Type: application/json");
        StringAssert.Contains(result, "[1,2,3]");
    }

    // ── Error handling ────────────────────────────────────────────────────
    [TestMethod]
    public void Serialize_NonObjectThrows()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SerializeMultipart("""[1,2,3]"""));
    }

    [TestMethod]
    public void SerializeWithBinaryParts_NonObjectThrows()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SerializeMultipart("""[1,2,3]""", binaryParts: new Dictionary<string, BinaryPartData>()));
    }

    // ── Content-Type overrides ────────────────────────────────────────────
    [TestMethod]
    public void Serialize_ContentTypeOverrideForString()
    {
        string result = SerializeMultipart(
            """{"bio":"Hello"}""",
            encodings: new Dictionary<string, PropertyEncoding>
            {
                ["bio"] = new(ContentType: "text/plain"),
            });

        StringAssert.Contains(result, "Content-Type: text/plain\r\n\r\nHello");
    }

    [TestMethod]
    public void Serialize_ContentTypeOverrideForNumber()
    {
        string result = SerializeMultipart(
            """{"score":42}""",
            encodings: new Dictionary<string, PropertyEncoding>
            {
                ["score"] = new(ContentType: "text/plain"),
            });

        StringAssert.Contains(result, "Content-Type: text/plain\r\n\r\n42");
    }

    [TestMethod]
    public void Serialize_ContentTypeOverrideForBoolean()
    {
        string result = SerializeMultipart(
            """{"flag":false}""",
            encodings: new Dictionary<string, PropertyEncoding>
            {
                ["flag"] = new(ContentType: "text/plain"),
            });

        StringAssert.Contains(result, "Content-Type: text/plain\r\n\r\nfalse");
    }

    [TestMethod]
    public void Serialize_ContentTypeOverrideForNull()
    {
        string result = SerializeMultipart(
            """{"empty":null}""",
            encodings: new Dictionary<string, PropertyEncoding>
            {
                ["empty"] = new(ContentType: "text/plain"),
            });

        StringAssert.Contains(result, "Content-Type: text/plain\r\n\r\n\r\n");
    }

    [TestMethod]
    public void Serialize_ContentTypeOverrideForObject()
    {
        string result = SerializeMultipart(
            """{"meta":{"k":"v"}}""",
            encodings: new Dictionary<string, PropertyEncoding>
            {
                ["meta"] = new(ContentType: "application/xml"),
            });

        StringAssert.Contains(result, "Content-Type: application/xml\r\n\r\n");
    }

    // ── Binary parts ──────────────────────────────────────────────────────
    [TestMethod]
    public void Serialize_BinaryPartMatchingJsonProperty()
    {
        byte[] imageData = [0x89, 0x50, 0x4E, 0x47];
        string result = SerializeMultipart(
            """{"avatar":"placeholder"}""",
            binaryParts: new Dictionary<string, BinaryPartData>
            {
                ["avatar"] = new((s, ct) => { s.Write(imageData); return default; }, "image/png", "photo.png"),
            });

        StringAssert.Contains(result, "name=\"avatar\"; filename=\"photo.png\"");
        StringAssert.Contains(result, "Content-Type: image/png");

        // Binary part should replace the JSON property value.
        Assert.IsFalse(result.Contains("placeholder"));
    }

    [TestMethod]
    public void Serialize_BinaryPartWithoutFilename()
    {
        byte[] data = [0x01, 0x02, 0x03];
        string result = SerializeMultipart(
            """{"data":"placeholder"}""",
            binaryParts: new Dictionary<string, BinaryPartData>
            {
                ["data"] = new((s, ct) => { s.Write(data); return default; }),
            });

        StringAssert.Contains(result, "name=\"data\"");
        StringAssert.Contains(result, "Content-Type: application/octet-stream");

        // Should not have filename= in Content-Disposition.
        Assert.IsFalse(result.Contains("filename="));
    }

    [TestMethod]
    public void Serialize_StandaloneBinaryPartNotInJson()
    {
        byte[] fileData = [0xFF, 0xD8, 0xFF, 0xE0];
        string result = SerializeMultipart(
            """{"name":"Alice"}""",
            binaryParts: new Dictionary<string, BinaryPartData>
            {
                ["attachment"] = new((s, ct) => { s.Write(fileData); return default; }, "image/jpeg", "image.jpg"),
            });

        // Both the JSON property and standalone binary part should appear.
        StringAssert.Contains(result, "name=\"name\"");
        StringAssert.Contains(result, "name=\"attachment\"; filename=\"image.jpg\"");
        StringAssert.Contains(result, "Content-Type: image/jpeg");
    }

    [TestMethod]
    public void Serialize_StreamingBinaryPartFromCallback()
    {
        // Verify the callback pattern works — simulate streaming from a source.
        byte[] source = Encoding.UTF8.GetBytes("Hello from stream!");
        string result = SerializeMultipart(
            """{"file":"ignored"}""",
            binaryParts: new Dictionary<string, BinaryPartData>
            {
                ["file"] = new(
                    (s, ct) =>
                    {
                        using MemoryStream ms = new(source);
                        ms.CopyTo(s);
                        return default;
                    },
                    "text/plain",
                    "hello.txt"),
            });

        StringAssert.Contains(result, "Hello from stream!");
        StringAssert.Contains(result, "filename=\"hello.txt\"");
    }

    // ── Multiple properties ───────────────────────────────────────────────
    [TestMethod]
    public void Serialize_MultiplePropertiesWithMixedTypes()
    {
        string result = SerializeMultipart(
            """{"name":"Alice","age":30,"tags":["a","b"]}""");

        StringAssert.Contains(result, "name=\"name\"");
        StringAssert.Contains(result, "name=\"age\"");
        StringAssert.Contains(result, "name=\"tags\"");

        // Ends with closing boundary.
        Assert.IsTrue(result.TrimEnd().EndsWith("--test-boundary--"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static string SerializeMultipart(
        string json,
        Dictionary<string, PropertyEncoding>? encodings = null,
        Dictionary<string, BinaryPartData>? binaryParts = null)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            Encoding.UTF8.GetBytes(json));

        using MemoryStream ms = new();

        if (binaryParts is not null)
        {
            MultipartFormDataSerializer.SerializeAsync(doc.RootElement, ms, Boundary, encodings, binaryParts).AsTask().GetAwaiter().GetResult();
        }
        else
        {
            MultipartFormDataSerializer.Serialize(doc.RootElement, ms, Boundary, encodings);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}