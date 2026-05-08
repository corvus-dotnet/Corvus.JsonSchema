// <copyright file="StandardBase64Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class StandardBase64Tests
{
    // =====================
    // EncodeToString(JsonDocument)
    // =====================
    [TestMethod]
    public void EncodeToString_JsonDocument()
    {
        // EncodeToString expects a string-valued document (e.g., base64 content);
        // on .NET 8+ it uses ProcessRawText which strips quotes via [1..^1].
        // On net481, it writes the document and base64-encodes the written bytes.
        // Use a simple string value to test both paths.
        using var doc = JsonDocument.Parse("\"hello world\"");
        string result = StandardBase64.EncodeToString(doc);
        Assert.IsNotNull(result);

        // The result should be non-empty and valid base64
        byte[] decoded = Convert.FromBase64String(result);
        Assert.IsTrue(decoded.Length > 0);
    }

    // =====================
    // EncodeToString(ReadOnlySpan<byte>)
    // =====================
    [TestMethod]
    public void EncodeToString_ByteSpan()
    {
        byte[] utf8 = "hello world"u8.ToArray();
        string result = StandardBase64.EncodeToString(utf8);
        Assert.AreEqual("hello world", result);
    }

    // =====================
    // GetDecodedBufferSize(string)
    // =====================
    [TestMethod]
    public void GetDecodedBufferSize_String()
    {
        Assert.AreEqual(12, StandardBase64.GetDecodedBufferSize("aGVsbG8gd29y"));
    }

    // =====================
    // GetDecodedBufferSize(JsonElement)
    // =====================
    [TestMethod]
    public void GetDecodedBufferSize_JsonElement()
    {
        using var doc = JsonDocument.Parse("\"aGVsbG8=\"");
        int size = StandardBase64.GetDecodedBufferSize(doc.RootElement);
        Assert.IsTrue(size > 0);
    }

    // =====================
    // HasBase64Bytes(JsonElement)
    // =====================
    [TestMethod]
    public void HasBase64Bytes_JsonElement_Valid()
    {
        string base64 = Convert.ToBase64String("hello"u8.ToArray());
        using var doc = JsonDocument.Parse($"\"{base64}\"");
        Assert.IsTrue(StandardBase64.HasBase64Bytes(doc.RootElement));
    }

    [TestMethod]
    public void HasBase64Bytes_JsonElement_Invalid()
    {
        using var doc = JsonDocument.Parse("\"not!!valid!!base64\"");
        Assert.IsFalse(StandardBase64.HasBase64Bytes(doc.RootElement));
    }

    // =====================
    // Decode(JsonElement, Span<byte>, out int)
    // =====================
    [TestMethod]
    public void Decode_JsonElement_Success()
    {
        string base64 = Convert.ToBase64String("hello"u8.ToArray());
        using var doc = JsonDocument.Parse($"\"{base64}\"");

        byte[] buffer = new byte[100];
        Assert.IsTrue(StandardBase64.Decode(doc.RootElement, buffer, out int written));
        Assert.AreEqual("hello", Encoding.UTF8.GetString(buffer, 0, written));
    }

    [TestMethod]
    public void Decode_JsonElement_BufferTooSmall()
    {
        string base64 = Convert.ToBase64String("hello world this is a longer string"u8.ToArray());
        using var doc = JsonDocument.Parse($"\"{base64}\"");

        Span<byte> buffer = stackalloc byte[2];
        Assert.IsFalse(StandardBase64.Decode(doc.RootElement, buffer, out _));
    }

#if NET8_0_OR_GREATER
    // =====================
    // HasBase64Bytes(string)
    // =====================
    [TestMethod]
    public void HasBase64Bytes_String_Null()
    {
        Assert.IsFalse(StandardBase64.HasBase64Bytes((string?)null));
    }

    [TestMethod]
    public void HasBase64Bytes_String_Valid()
    {
        string base64 = Convert.ToBase64String("test"u8.ToArray());
        Assert.IsTrue(StandardBase64.HasBase64Bytes(base64));
    }

    [TestMethod]
    public void HasBase64Bytes_String_Invalid()
    {
        Assert.IsFalse(StandardBase64.HasBase64Bytes("not!!base64!!"));
    }

    // =====================
    // Decode(string, Span<byte>, out int)
    // =====================
    [TestMethod]
    public void Decode_String_Null()
    {
        Span<byte> buffer = stackalloc byte[10];
        Assert.IsFalse(StandardBase64.Decode((string?)null, buffer, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void Decode_String_Valid()
    {
        string base64 = Convert.ToBase64String("hello"u8.ToArray());
        Span<byte> buffer = stackalloc byte[100];
        Assert.IsTrue(StandardBase64.Decode(base64, buffer, out int written));
        Assert.AreEqual("hello", Encoding.UTF8.GetString(buffer.Slice(0, written)));
    }

    [TestMethod]
    public void Decode_String_BufferTooSmall()
    {
        string base64 = Convert.ToBase64String("hello world"u8.ToArray());
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(StandardBase64.Decode(base64, buffer, out _));
    }
#endif
}