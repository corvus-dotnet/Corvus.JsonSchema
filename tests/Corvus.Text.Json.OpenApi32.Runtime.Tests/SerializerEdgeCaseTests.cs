// <copyright file="SerializerEdgeCaseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

/// <summary>
/// Tests for uncovered edge-case branches in serializer classes:
/// <see cref="MultipartFormDataSerializer"/>, <see cref="FormUrlEncodedSerializer"/>,
/// <see cref="FormUrlEncodedQueryStringWriter"/>, <see cref="StyleValueSplitter"/>,
/// and <see cref="ThrowHelper"/>.
/// </summary>
[TestClass]
public class SerializerEdgeCaseTests
{
    [TestMethod]
    public void MultipartFormData_NonObjectThrows()
    {
        JsonElement array = JsonElement.ParseValue("""[1,2,3]"""u8);
        using MemoryStream stream = new();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => MultipartFormDataSerializer.Serialize(array, stream, "boundary", null, null));
    }

    [TestMethod]
    public void MultipartFormData_BinaryPartIsWritten()
    {
        JsonElement obj = JsonElement.ParseValue("""{"file":"placeholder","name":"test"}"""u8);
        using MemoryStream stream = new();
        byte[] binaryContent = [0x89, 0x50, 0x4E, 0x47];

        Dictionary<string, BinaryPartData> binaryParts = new()
        {
            ["file"] = new BinaryPartData(
                s => s.Write(binaryContent),
                "image/png",
                "image.png"),
        };

        MultipartFormDataSerializer.Serialize(obj, stream, "myboundary", null, binaryParts);

        string output = Encoding.UTF8.GetString(stream.ToArray());

        StringAssert.Contains(output, "Content-Disposition: form-data; name=\"file\"; filename=\"image.png\"");
        StringAssert.Contains(output, "Content-Type: image/png");
        StringAssert.Contains(output, "Content-Disposition: form-data; name=\"name\"");
    }

    [TestMethod]
    public void MultipartFormData_NumberWithContentTypeOverride()
    {
        JsonElement obj = JsonElement.ParseValue("""{"count":42}"""u8);
        using MemoryStream stream = new();

        Dictionary<string, PropertyEncoding> encodings = new()
        {
            ["count"] = new PropertyEncoding(ContentType: "text/plain"),
        };

        MultipartFormDataSerializer.Serialize(obj, stream, "boundary", encodings, null);

        string output = Encoding.UTF8.GetString(stream.ToArray());

        StringAssert.Contains(output, "Content-Type: text/plain");
        StringAssert.Contains(output, "42");
    }

    [TestMethod]
    public void MultipartFormData_BooleanWithContentTypeOverride()
    {
        JsonElement obj = JsonElement.ParseValue("""{"active":true}"""u8);
        using MemoryStream stream = new();

        Dictionary<string, PropertyEncoding> encodings = new()
        {
            ["active"] = new PropertyEncoding(ContentType: "text/plain"),
        };

        MultipartFormDataSerializer.Serialize(obj, stream, "boundary", encodings, null);

        string output = Encoding.UTF8.GetString(stream.ToArray());

        StringAssert.Contains(output, "Content-Type: text/plain");
        StringAssert.Contains(output, "True");
    }

    [TestMethod]
    public void MultipartFormData_NullWithContentTypeOverride()
    {
        JsonElement obj = JsonElement.ParseValue("""{"value":null}"""u8);
        using MemoryStream stream = new();

        Dictionary<string, PropertyEncoding> encodings = new()
        {
            ["value"] = new PropertyEncoding(ContentType: "application/octet-stream"),
        };

        MultipartFormDataSerializer.Serialize(obj, stream, "boundary", encodings, null);

        string output = Encoding.UTF8.GetString(stream.ToArray());

        StringAssert.Contains(output, "Content-Type: application/octet-stream");
    }

    [TestMethod]
    public void FormUrlEncodedSerializer_NonObjectThrows()
    {
        JsonElement array = JsonElement.ParseValue("""[1,2,3]"""u8);
        using MemoryStream stream = new();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => FormUrlEncodedSerializer.Serialize(array, stream, null));
    }

    [TestMethod]
    public void FormUrlEncodedSerializer_DefaultStyleForArray()
    {
        // An array with style "form" (the default) uses comma separator in non-exploded mode.
        // We need a property that is an array with a "form" style encoding (which hits the _ => "," path).
        JsonElement obj = JsonElement.ParseValue("""{"tags":["a","b"]}"""u8);
        using MemoryStream stream = new();

        Dictionary<string, PropertyEncoding> encodings = new()
        {
            ["tags"] = new PropertyEncoding(Style: "form", Explode: false),
        };

        FormUrlEncodedSerializer.Serialize(obj, stream, encodings);

        string output = Encoding.UTF8.GetString(stream.ToArray());

        // Non-exploded form style: tags=a,b (comma separated, single key)
        Assert.AreEqual("tags=a%2Cb", output);
    }

    [TestMethod]
    public void FormUrlEncodedSerializer_NullValueInArray()
    {
        // A null inside an array should use GetPrimitiveString returning empty string
        JsonElement obj = JsonElement.ParseValue("""{"items":[null,"x"]}"""u8);
        using MemoryStream stream = new();

        Dictionary<string, PropertyEncoding> encodings = new()
        {
            ["items"] = new PropertyEncoding(Style: "form", Explode: false),
        };

        FormUrlEncodedSerializer.Serialize(obj, stream, encodings);

        string output = Encoding.UTF8.GetString(stream.ToArray());

        // Non-exploded form style: items=,x (null → empty string, comma, then "x")
        Assert.AreEqual("items=%2Cx", output);
    }

    [TestMethod]
    public void FormUrlEncodedQueryStringWriter_NonObjectThrows()
    {
        JsonElement num = JsonElement.ParseValue("42"u8);
        ArrayBufferWriter<byte> writer = new();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => FormUrlEncodedQueryStringWriter.Write(num, writer));
    }

    [TestMethod]
    public void FormUrlEncodedQueryStringWriter_NullValueEncoded()
    {
        JsonElement obj = JsonElement.ParseValue("""{"key":null}"""u8);
        ArrayBufferWriter<byte> writer = new();

        int written = FormUrlEncodedQueryStringWriter.Write(obj, writer);

        string output = Encoding.UTF8.GetString(writer.WrittenSpan);

        // Null → empty string → "key=" (EscapeDataString of "" is "")
        Assert.AreEqual("key=", output);
        Assert.AreEqual(4, written);
    }

    [TestMethod]
    public void FormUrlEncodedQueryStringWriter_BooleanValuesEncoded()
    {
        JsonElement obj = JsonElement.ParseValue("""{"a":true,"b":false}"""u8);
        ArrayBufferWriter<byte> writer = new();

        FormUrlEncodedQueryStringWriter.Write(obj, writer);

        string output = Encoding.UTF8.GetString(writer.WrittenSpan);

        Assert.AreEqual("a=true&b=false", output);
    }

    [TestMethod]
    public void FormUrlEncodedQueryStringWriter_ObjectValueJsonStringified()
    {
        JsonElement obj = JsonElement.ParseValue("""{"data":{"x":1}}"""u8);
        ArrayBufferWriter<byte> writer = new();

        FormUrlEncodedQueryStringWriter.Write(obj, writer);

        string output = Encoding.UTF8.GetString(writer.WrittenSpan);

        // Object is JSON-stringified then percent-encoded
        StringAssert.StartsWith(output, "data=");
        StringAssert.Contains(output, "%7B"); // { percent-encoded
    }

    [TestMethod]
    public void StyleValueSplitter_EscapedCharInString()
    {
        // Input: "hello\"world",other
        // The escaped quote should not toggle inString, so the comma after "world" is the separator.
        ReadOnlySpan<char> input = "\"hello\\\"world\",other".AsSpan();

        int idx = StyleValueSplitter.NextSeparator(input);

        Assert.AreEqual(14, idx);
    }

    [TestMethod]
    public void StyleValueSplitter_BackslashSetsEscapeFlag()
    {
        // A backslash inside a quoted string marks the next char as escaped.
        // "a\\b",next → the \\ is an escaped backslash, then b" closes nothing,
        // actually: "a\\b" → chars: " a \ \ b " which is: open quote, a, escape-next, \, b, close-quote
        // so the next comma is at index 6.
        ReadOnlySpan<char> input = "\"a\\\\b\",next".AsSpan();

        int idx = StyleValueSplitter.NextSeparator(input);

        Assert.AreEqual(6, idx);
    }

    [TestMethod]
    public void ThrowHelper_ThrowNoHeaderParameters()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowNoHeaderParameters());
    }

    [TestMethod]
    public void ThrowHelper_ThrowRequestBodyValidationFailed_NoDetail()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowRequestBodyValidationFailed());
    }

    [TestMethod]
    public void ThrowHelper_ThrowRequestBodyValidationFailed_WithDetail()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowRequestBodyValidationFailed("some detail"));

        StringAssert.Contains(ex.Message, "some detail");
    }

    [TestMethod]
    public void ThrowHelper_ThrowUnableToResolveRequestBodyRef()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowUnableToResolveRequestBodyRef());
    }

    [TestMethod]
    public void ThrowHelper_ThrowUnableToResolveResponseRef()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowUnableToResolveResponseRef());
    }

    [TestMethod]
    public void ThrowHelper_ThrowUnableToResolveHeaderRef()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowUnableToResolveHeaderRef());
    }

    [TestMethod]
    public void ThrowHelper_ThrowFormBodyMustBeObject()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowFormBodyMustBeObject());
    }
}