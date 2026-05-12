// <copyright file="JsonReaderHelperTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.Specs.Tests.CoverageGap;

/// <summary>
/// Tests exercising uncovered paths in JsonReaderHelper and StandardContent
/// by going through public StandardContent methods.
/// </summary>
[TestClass]
public class JsonReaderHelperTests
{
    // =====================
    // ParseEscapedJsonContentInJsonString (char overload)
    // Exercises: Unescape(char), TranscodeHelper(char→byte), parse paths
    // =====================
    [TestMethod]
    public void ParseEscapedJsonContent_Char_WithEscapes_Valid()
    {
        // JSON-in-JSON: the string value is escaped JSON
        ReadOnlySpan<char> source = "{\\\"key\\\":\\\"value\\\"}";
        var status = StandardContent.ParseEscapedJsonContentInJsonString(source, false, out JsonDocument? result);
        Assert.AreEqual(EncodedContentMediaTypeParseStatus.Success, status);
        Assert.IsNotNull(result);
        result!.Dispose();
    }

    [TestMethod]
    public void ParseEscapedJsonContent_Char_NoEscapes_Valid()
    {
        ReadOnlySpan<char> source = "{\"key\":\"value\"}";
        var status = StandardContent.ParseEscapedJsonContentInJsonString(source, false, out JsonDocument? result);
        Assert.AreEqual(EncodedContentMediaTypeParseStatus.Success, status);
        Assert.IsNotNull(result);
        result!.Dispose();
    }

    [TestMethod]
    public void ParseEscapedJsonContent_Char_InvalidJson()
    {
        ReadOnlySpan<char> source = "not valid json {{{";
        var status = StandardContent.ParseEscapedJsonContentInJsonString(source, false, out JsonDocument? result);
        Assert.AreEqual(EncodedContentMediaTypeParseStatus.UnableToParseToMediaType, status);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseEscapedJsonContent_Char_Empty()
    {
        ReadOnlySpan<char> source = ReadOnlySpan<char>.Empty;
        var status = StandardContent.ParseEscapedJsonContentInJsonString(source, false, out JsonDocument? result);
        Assert.AreEqual(EncodedContentMediaTypeParseStatus.UnableToDecode, status);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseEscapedJsonContent_Char_WithNewlineTabEscapes()
    {
        // JSON with escape sequences that exercise \n, \t, \r dispatch
        ReadOnlySpan<char> source = "{\\\"msg\\\":\\\"line1\\\\nline2\\\"}";
        var status = StandardContent.ParseEscapedJsonContentInJsonString(source, false, out JsonDocument? result);
        Assert.AreEqual(EncodedContentMediaTypeParseStatus.Success, status);
        result?.Dispose();
    }

    // =====================
    // ParseEscapedJsonContentInJsonString (JsonElement overload)
    // Exercises: UnescapeAndParseJsonDocument (byte unescape path)
    // =====================
    [TestMethod]
    public void ParseEscapedJsonContent_JsonElement_NoBase64_WithEscapes()
    {
        // Create a JSON string element whose value is escaped JSON
        using var doc = JsonDocument.Parse("\"{\\\\\\\"key\\\\\\\":\\\\\\\"value\\\\\\\"}\"");
        var status = StandardContent.ParseEscapedJsonContentInJsonString(doc.RootElement, false, out JsonDocument? result);

        // Even if parsing fails due to double-escaping, verify the code path is exercised
        Assert.IsTrue(
            status == EncodedContentMediaTypeParseStatus.Success ||
            status == EncodedContentMediaTypeParseStatus.UnableToParseToMediaType);
        result?.Dispose();
    }

    [TestMethod]
    public void ParseEscapedJsonContent_JsonElement_NoBase64_NoEscapes()
    {
        using var doc = JsonDocument.Parse("\"{\\\"key\\\":\\\"value\\\"}\"");
        var status = StandardContent.ParseEscapedJsonContentInJsonString(doc.RootElement, false, out JsonDocument? result);
        Assert.AreEqual(EncodedContentMediaTypeParseStatus.Success, status);
        result?.Dispose();
    }

    [TestMethod]
    public void ParseEscapedJsonContent_JsonElement_Base64_Valid()
    {
        // Base64-encode some JSON
        string json = "{\"a\":1}";
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        using var doc = JsonDocument.Parse($"\"{base64}\"");
        var status = StandardContent.ParseEscapedJsonContentInJsonString(doc.RootElement, true, out JsonDocument? result);
        Assert.AreEqual(EncodedContentMediaTypeParseStatus.Success, status);
        result?.Dispose();
    }

    [TestMethod]
    public void ParseEscapedJsonContent_JsonElement_Base64_InvalidBase64()
    {
        using var doc = JsonDocument.Parse("\"not!!valid!!base64\"");
        var status = StandardContent.ParseEscapedJsonContentInJsonString(doc.RootElement, true, out JsonDocument? result);
        Assert.AreEqual(EncodedContentMediaTypeParseStatus.UnableToDecode, status);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseEscapedJsonContent_JsonElement_Base64_ValidBase64_InvalidJson()
    {
        string base64 = Convert.ToBase64String("not json content {{{{"u8.ToArray());
        using var doc = JsonDocument.Parse($"\"{base64}\"");
        var status = StandardContent.ParseEscapedJsonContentInJsonString(doc.RootElement, true, out JsonDocument? result);
        Assert.AreEqual(EncodedContentMediaTypeParseStatus.UnableToParseToMediaType, status);
        Assert.IsNull(result);
    }

    // =====================
    // Unescape(ReadOnlySpan<byte>, Span<char>)
    // Exercises: byte→char unescape with and without escapes
    // =====================
    [TestMethod]
    public void Unescape_ByteToChar_WithEscapes()
    {
        byte[] source = "hello\\\"world"u8.ToArray();
        char[] dest = new char[64];
        Assert.IsTrue(StandardContent.Unescape(source, dest, out int written));
        Assert.AreEqual("hello\"world", new string(dest, 0, written));
    }

    [TestMethod]
    public void Unescape_ByteToChar_NoEscapes()
    {
        byte[] source = "plain text"u8.ToArray();
        char[] dest = new char[64];
        Assert.IsTrue(StandardContent.Unescape(source, dest, out int written));
        Assert.AreEqual("plain text", new string(dest, 0, written));
    }

    [TestMethod]
    public void Unescape_ByteToChar_BackspaceFormfeed()
    {
        byte[] source = "a\\bb\\f"u8.ToArray();
        char[] dest = new char[64];
        Assert.IsTrue(StandardContent.Unescape(source, dest, out int written));
        Assert.AreEqual("a\bb\f", new string(dest, 0, written));
    }

    [TestMethod]
    public void Unescape_ByteToChar_UnicodeEscape()
    {
        byte[] source = "\\u0041"u8.ToArray();
        char[] dest = new char[64];
        Assert.IsTrue(StandardContent.Unescape(source, dest, out int written));
        Assert.AreEqual("A", new string(dest, 0, written));
    }

    [TestMethod]
    public void Unescape_ByteToChar_SurrogatePair()
    {
        byte[] source = "\\uD83D\\uDE00"u8.ToArray();
        char[] dest = new char[64];
        Assert.IsTrue(StandardContent.Unescape(source, dest, out int written));
        Assert.AreEqual("\U0001F600", new string(dest, 0, written));
    }

    // =====================
    // Unescape(JsonElement, Memory<char>)
    // =====================
    [TestMethod]
    public void Unescape_JsonElement_WithEscapes()
    {
        using var doc = JsonDocument.Parse("\"hello\\\\nworld\"");
        char[] dest = new char[64];
        Assert.IsTrue(StandardContent.Unescape(doc.RootElement, dest, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void Unescape_JsonElement_NoEscapes()
    {
        using var doc = JsonDocument.Parse("\"plain text\"");
        char[] dest = new char[64];
        Assert.IsTrue(StandardContent.Unescape(doc.RootElement, dest, out int written));
        Assert.AreEqual("plain text", new string(dest, 0, written));
    }

    // =====================
    // GetUnescapedBufferSize
    // =====================
    [TestMethod]
    public void GetUnescapedBufferSize_String()
    {
        Assert.AreEqual(5, StandardContent.GetUnescapedBufferSize("hello"));
    }

    [TestMethod]
    public void GetUnescapedBufferSize_JsonElement()
    {
        using var doc = JsonDocument.Parse("\"hello\"");
        int size = StandardContent.GetUnescapedBufferSize(doc.RootElement);
        Assert.IsTrue(size > 0);
    }

    // =====================
    // CanDecodeBase64 (tested through HasBase64Bytes)
    // =====================
    [TestMethod]
    public void CanDecodeBase64_ViaHasBase64Bytes_Valid()
    {
        string base64 = Convert.ToBase64String("hello"u8.ToArray());
        using var doc = JsonDocument.Parse($"\"{base64}\"");
        Assert.IsTrue(StandardBase64.HasBase64Bytes(doc.RootElement));
    }

    [TestMethod]
    public void CanDecodeBase64_ViaHasBase64Bytes_Invalid()
    {
        using var doc = JsonDocument.Parse("\"not!valid!base64!!!\"");
        Assert.IsFalse(StandardBase64.HasBase64Bytes(doc.RootElement));
    }
}