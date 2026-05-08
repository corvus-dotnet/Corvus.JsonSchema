// <copyright file="FixedStringJsonDocumentCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Linq;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for <see cref="FixedStringJsonDocument{T}"/>.
/// Targets uncovered IJsonDocument interface methods including
/// wrong-type throws, escape handling, TextEquals edge cases,
/// and TryGetValue for base64 with escapes.
/// </summary>
[TestCategory("coverage")]
[TestClass]
public class FixedStringJsonDocumentCoverageTests
{
    // --- Factory / Construction ---

    [TestMethod]
    public void Parse_CreatesDocument()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        Assert.AreEqual(JsonTokenType.String, d.GetJsonTokenType(0));
    }

    [TestMethod]
    public void Constructor_WithUnescaping()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: true);
        IJsonDocument d = doc;
        Assert.AreEqual(JsonTokenType.String, d.GetJsonTokenType(0));
    }

    // --- RootElement ---

    [TestMethod]
    public void RootElement_ReturnsJsonElement()
    {
        byte[] raw = "\"world\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        JsonElement root = doc.RootElement;
        Assert.AreEqual(JsonValueKind.String, root.ValueKind);
    }

    // --- Metadata operations throw NotSupportedException ---

    [TestMethod]
    public void WriteElementToMetadataDb_Throws()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        using JsonWorkspace workspace = JsonWorkspace.Create();
        MetadataDb db = MetadataDb.CreateRented(16, false);
        try
        {
            Assert.ThrowsExactly<NotSupportedException>(() => d.WriteElementToMetadataDb(0, workspace, ref db, 0));
        }
        finally
        {
            db.Dispose();
        }
    }

    [TestMethod]
    public void BuildRentedMetadataDb_Throws()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.ThrowsExactly<NotSupportedException>(() => d.BuildRentedMetadataDb(0, workspace, out _));
    }

    // --- CloneElement ---

    [TestMethod]
    public void CloneElement_ReturnsEquivalent()
    {
        byte[] raw = "\"test\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        JsonElement clone = d.CloneElement<JsonElement>(0);
        Assert.AreEqual(JsonValueKind.String, clone.ValueKind);
    }

    // --- EnsurePropertyMap (no-op) ---

    [TestMethod]
    public void EnsurePropertyMap_DoesNotThrow()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        d.EnsurePropertyMap(0);
    }

    // --- Object operations throw ---

    [TestMethod]
    public void GetPropertyName_Throws()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetPropertyName(0));
    }

    [TestMethod]
    public void GetPropertyNameRaw_Span_Throws()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetPropertyNameRaw(0));
    }

    [TestMethod]
    public void GetPropertyNameRaw_Memory_Throws()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetPropertyNameRaw(0, includeQuotes: true));
    }

    [TestMethod]
    public void GetPropertyNameUnescaped_Throws()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetPropertyNameUnescaped(0));
    }

    // --- TextEquals with escaped content ---

    [TestMethod]
    public void TextEquals_CharSpan_WithEscapedContent_Matches()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: true);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TextEquals(0, "a\nb".AsSpan(), isPropertyName: false));
    }

    [TestMethod]
    public void TextEquals_CharSpan_WithEscapedContent_Mismatch()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: true);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TextEquals(0, "x\ny".AsSpan(), isPropertyName: false));
    }

    [TestMethod]
    public void TextEquals_CharSpan_LongText_CoversArrayPoolPath()
    {
        string longValue = new string('z', 300);
        byte[] raw = Encoding.UTF8.GetBytes($"\"{longValue}\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TextEquals(0, longValue.AsSpan(), isPropertyName: false));
    }

    [TestMethod]
    public void TextEquals_ByteSpan_WithEscapedContent_Matches()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: true);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TextEquals(0, Encoding.UTF8.GetBytes("a\nb"), isPropertyName: false, shouldUnescape: true));
    }

    [TestMethod]
    public void TextEquals_ByteSpan_EscapedContent_TooShortForMatch()
    {
        // The unescaped text is shorter than segment/MaxExpansionFactor threshold
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\u0062c\\u0064e\\u0066\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: true);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TextEquals(0, "x"u8, isPropertyName: false, shouldUnescape: true));
    }

    [TestMethod]
    public void TextEquals_ByteSpan_EscapedContent_PrefixMismatch()
    {
        // Content starts with "a\" but comparison starts with "x"
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: true);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TextEquals(0, Encoding.UTF8.GetBytes("x\nb"), isPropertyName: false, shouldUnescape: true));
    }

    // --- TryGetValue(byte[]) with unescaping ---

    [TestMethod]
    public void TryGetValue_ByteArray_WithUnescaping()
    {
        // Base64 string "AQID" = [1, 2, 3], with escape sequences
        // Use \\u0041\\u0051\\u0049\\u0044 = \u0041\u0051\u0049\u0044 = "AQID"
        byte[] raw = Encoding.UTF8.GetBytes("\"\\u0041\\u0051\\u0049\\u0044\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: true);
        IJsonDocument d = doc;
        bool result = d.TryGetValue(0, out byte[]? value);
        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, value);
    }

    [TestMethod]
    public void TryGetValue_ByteArray_WithoutUnescaping()
    {
        // Base64 string "AQID" = [1, 2, 3]
        byte[] raw = "\"AQID\""u8.ToArray();
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        bool result = d.TryGetValue(0, out byte[]? value);
        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, value);
    }

    // --- TryGetLineAndOffsetForPointer ---

    [TestMethod]
    public void TryGetLineAndOffsetForPointer_ReturnsFalse()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedStringJsonDocument<JsonElement> doc = FixedStringJsonDocument<JsonElement>.Parse(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetLineAndOffsetForPointer("/"u8, 0, out _, out _, out _));
    }

    // --- GetUtf8JsonString with escaped content (catch block is defensive) ---

    [TestMethod]
    public void GetUtf8JsonString_WithEscapes_ReturnsUnescaped()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: true);
        IJsonDocument d = doc;
        using UnescapedUtf8JsonString result = d.GetUtf8JsonString(0, JsonTokenType.String);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("a\nb"), result.Span.ToArray());
    }

    // --- GetUtf16JsonString paths ---

    [TestMethod]
    public void GetUtf16JsonString_WithEscapes_ReturnsUnescaped()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\tb\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: true);
        IJsonDocument d = doc;
        using UnescapedUtf16JsonString result = d.GetUtf16JsonString(0, JsonTokenType.String);
        Assert.AreEqual("a\tb", result.Span.ToString());
    }

    [TestMethod]
    public void GetUtf16JsonString_WithoutEscapes_ReturnsValue()
    {
        byte[] raw = "\"simple\""u8.ToArray();
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: false);
        IJsonDocument d = doc;
        using UnescapedUtf16JsonString result = d.GetUtf16JsonString(0, JsonTokenType.String);
        Assert.AreEqual("simple", result.Span.ToString());
    }

    [TestMethod]
    public void GetUtf16JsonString_WithLongEscapes_CoversRentPath()
    {
        // Create escaped content > 256 bytes to trigger ArrayPool rent
        string longEscaped = string.Concat(Enumerable.Repeat("\\u0061", 50)); // 50 × \u0061 = 300 escaped bytes
        byte[] raw = Encoding.UTF8.GetBytes($"\"{longEscaped}\"");
        using var doc = new FixedStringJsonDocument<JsonElement>(raw, requiresUnescaping: true);
        IJsonDocument d = doc;
        using UnescapedUtf16JsonString result = d.GetUtf16JsonString(0, JsonTokenType.String);
        Assert.AreEqual(new string('a', 50), result.Span.ToString());
    }
}
