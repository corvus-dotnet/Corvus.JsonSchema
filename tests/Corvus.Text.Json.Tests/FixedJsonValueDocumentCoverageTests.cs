// <copyright file="FixedJsonValueDocumentCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for <see cref="FixedJsonValueDocument{T}"/>.
/// Targets uncovered IJsonDocument interface methods:
/// wrong-type throws, TryGetValue negative paths, string escape handling,
/// TextEquals, and various accessor methods.
/// </summary>
[TestCategory("coverage")]
[TestClass]
public class FixedJsonValueDocumentCoverageTests
{
    // --- Factory methods (ForNumber, ForString, ForNumberFromSpan, ForStringFromSpan) ---

    [TestMethod]
    public void ForNumber_CreatesNumberDocument()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.AreEqual(JsonTokenType.Number, d.GetJsonTokenType(0));
    }

    [TestMethod]
    public void ForString_CreatesStringDocument()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.AreEqual(JsonTokenType.String, d.GetJsonTokenType(0));
    }

    [TestMethod]
    public void ForNumberFromSpan_CreatesNumberDocument()
    {
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumberFromSpan("99"u8);
        IJsonDocument d = doc;
        Assert.AreEqual(JsonTokenType.Number, d.GetJsonTokenType(0));
    }

    [TestMethod]
    public void ForStringFromSpan_CreatesStringDocument()
    {
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForStringFromSpan("\"test\""u8);
        IJsonDocument d = doc;
        Assert.AreEqual(JsonTokenType.String, d.GetJsonTokenType(0));
    }

    // --- RootElement ---

    [TestMethod]
    public void RootElement_Number_ReturnsJsonElement()
    {
        byte[] raw = "123"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        JsonElement root = doc.RootElement;
        Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
    }

    [TestMethod]
    public void RootElement_String_ReturnsJsonElement()
    {
        byte[] raw = "\"world\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        JsonElement root = doc.RootElement;
        Assert.AreEqual(JsonValueKind.String, root.ValueKind);
    }

    // --- Array operations throw for number document ---

    [TestMethod]
    public void GetArrayIndexElement_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetArrayIndexElement(0, 0));
    }

    [TestMethod]
    public void GetArrayIndexElement_Generic_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetArrayIndexElement<JsonElement>(0, 0));
    }

    [TestMethod]
    public void GetArrayIndexElement_OutParams_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetArrayIndexElement(0, 0, out _, out _));
    }

    [TestMethod]
    public void GetArrayInsertionIndex_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetArrayInsertionIndex(0, 0));
    }

    [TestMethod]
    public void GetArrayLength_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetArrayLength(0));
    }

    // --- Array operations throw for string document ---

    [TestMethod]
    public void GetArrayLength_Throws_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetArrayLength(0));
    }

    // --- Object operations throw for number document ---

    [TestMethod]
    public void GetNameOfPropertyValue_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetNameOfPropertyValue(0));
    }

    [TestMethod]
    public void GetPropertyCount_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetPropertyCount(0));
    }

    [TestMethod]
    public void GetPropertyName_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetPropertyName(0));
    }

    [TestMethod]
    public void GetPropertyNameRaw_Span_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetPropertyNameRaw(0));
    }

    [TestMethod]
    public void GetPropertyNameRaw_Memory_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetPropertyNameRaw(0, includeQuotes: true));
    }

    [TestMethod]
    public void GetPropertyNameUnescaped_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetPropertyNameUnescaped(0));
    }

    [TestMethod]
    public void GetPropertyRawValueAsString_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetPropertyRawValueAsString(0));
    }

    // --- TryGetNamedPropertyValue overloads throw ---

    [TestMethod]
    public void TryGetNamedPropertyValue_CharSpan_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.TryGetNamedPropertyValue(0, "foo".AsSpan(), out JsonElement _));
    }

    [TestMethod]
    public void TryGetNamedPropertyValue_ByteSpan_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.TryGetNamedPropertyValue(0, "foo"u8, out JsonElement _));
    }

    [TestMethod]
    public void TryGetNamedPropertyValue_GenericByte_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.TryGetNamedPropertyValue<JsonElement>(0, "foo"u8, out _));
    }

    [TestMethod]
    public void TryGetNamedPropertyValue_GenericChar_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.TryGetNamedPropertyValue<JsonElement>(0, "foo".AsSpan(), out _));
    }

    [TestMethod]
    public void TryGetNamedPropertyValue_CharSpan_OutDoc_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.TryGetNamedPropertyValue(0, "foo".AsSpan(), out IJsonDocument? _, out int _));
    }

    [TestMethod]
    public void TryGetNamedPropertyValue_ByteSpan_OutDoc_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.TryGetNamedPropertyValue(0, "foo"u8, out IJsonDocument? _, out int _));
    }

    // --- GetString / TryGetString ---

    [TestMethod]
    public void GetString_ReturnsString_ForStringDocument()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.AreEqual("hello", d.GetString(0, JsonTokenType.String));
    }

    [TestMethod]
    public void GetString_Throws_ForNumberDocument()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetString(0, JsonTokenType.String));
    }

    [TestMethod]
    public void GetString_UnescapesContent()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        string result = d.GetString(0, JsonTokenType.String)!;
        Assert.AreEqual("hello\nworld", result);
    }

    [TestMethod]
    public void TryGetString_ReturnsTrue_ForString()
    {
        byte[] raw = "\"abc\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TryGetString(0, JsonTokenType.String, out string? result));
        Assert.AreEqual("abc", result);
    }

    [TestMethod]
    public void TryGetString_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetString(0, JsonTokenType.String, out string? result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryGetString_UnescapesContent()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"tab\\there\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TryGetString(0, JsonTokenType.String, out string? result));
        Assert.AreEqual("tab\there", result);
    }

    // --- GetUtf8JsonString ---

    [TestMethod]
    public void GetUtf8JsonString_ReturnsValue_ForString()
    {
        byte[] raw = "\"test\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        using UnescapedUtf8JsonString result = d.GetUtf8JsonString(0, JsonTokenType.String);
        CollectionAssert.AreEqual("test"u8.ToArray(), result.Span.ToArray());
    }

    [TestMethod]
    public void GetUtf8JsonString_Throws_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetUtf8JsonString(0, JsonTokenType.String));
    }

    [TestMethod]
    public void GetUtf8JsonString_UnescapesContent()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        using UnescapedUtf8JsonString result = d.GetUtf8JsonString(0, JsonTokenType.String);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("a\nb"), result.Span.ToArray());
    }

    // --- GetUtf16JsonString ---

    [TestMethod]
    public void GetUtf16JsonString_ReturnsValue_ForString()
    {
        byte[] raw = "\"test\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        using UnescapedUtf16JsonString result = d.GetUtf16JsonString(0, JsonTokenType.String);
        Assert.AreEqual("test", result.Span.ToString());
    }

    [TestMethod]
    public void GetUtf16JsonString_Throws_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => d.GetUtf16JsonString(0, JsonTokenType.String));
    }

    [TestMethod]
    public void GetUtf16JsonString_UnescapesContent()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"x\\ty\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        using UnescapedUtf16JsonString result = d.GetUtf16JsonString(0, JsonTokenType.String);
        Assert.AreEqual("x\ty", result.Span.ToString());
    }

    // --- TextEquals (char span) ---

    [TestMethod]
    public void TextEquals_CharSpan_ReturnsTrue_ForMatchingString()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TextEquals(0, "hello".AsSpan(), isPropertyName: false));
    }

    [TestMethod]
    public void TextEquals_CharSpan_ReturnsFalse_ForMismatch()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TextEquals(0, "world".AsSpan(), isPropertyName: false));
    }

    [TestMethod]
    public void TextEquals_CharSpan_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TextEquals(0, "42".AsSpan(), isPropertyName: false));
    }

    [TestMethod]
    public void TextEquals_CharSpan_WithEscapedContent_Matches()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TextEquals(0, "a\nb".AsSpan(), isPropertyName: false));
    }

    // --- TextEquals (byte span) ---

    [TestMethod]
    public void TextEquals_ByteSpan_ReturnsTrue_ForMatchingString()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TextEquals(0, "hello"u8, isPropertyName: false, shouldUnescape: false));
    }

    [TestMethod]
    public void TextEquals_ByteSpan_ReturnsFalse_ForMismatch()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TextEquals(0, "world"u8, isPropertyName: false, shouldUnescape: false));
    }

    [TestMethod]
    public void TextEquals_ByteSpan_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TextEquals(0, "42"u8, isPropertyName: false, shouldUnescape: false));
    }

    [TestMethod]
    public void TextEquals_ByteSpan_WithEscapedContent_Matches()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TextEquals(0, Encoding.UTF8.GetBytes("a\nb"), isPropertyName: false, shouldUnescape: false));
    }

    // --- TryGetValue for numeric types on a STRING document (returns false) ---

    [TestMethod]
    public void TryGetValue_Sbyte_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out sbyte _));
    }

    [TestMethod]
    public void TryGetValue_Byte_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out byte _));
    }

    [TestMethod]
    public void TryGetValue_Short_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out short _));
    }

    [TestMethod]
    public void TryGetValue_Ushort_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out ushort _));
    }

    [TestMethod]
    public void TryGetValue_Int_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out int _));
    }

    [TestMethod]
    public void TryGetValue_Uint_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out uint _));
    }

    [TestMethod]
    public void TryGetValue_Long_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out long _));
    }

    [TestMethod]
    public void TryGetValue_Ulong_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out ulong _));
    }

    [TestMethod]
    public void TryGetValue_Double_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out double _));
    }

    [TestMethod]
    public void TryGetValue_Float_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out float _));
    }

    [TestMethod]
    public void TryGetValue_Decimal_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out decimal _));
    }

    [TestMethod]
    public void TryGetValue_BigInteger_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out System.Numerics.BigInteger _));
    }

    [TestMethod]
    public void TryGetValue_BigNumber_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out Corvus.Numerics.BigNumber _));
    }

    // --- TryGetValue for date/time types on a NUMBER document (returns false) ---

    [TestMethod]
    public void TryGetValue_DateTime_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out DateTime _));
    }

    [TestMethod]
    public void TryGetValue_DateTimeOffset_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out DateTimeOffset _));
    }

    [TestMethod]
    public void TryGetValue_Guid_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out Guid _));
    }

    [TestMethod]
    public void TryGetValue_OffsetDateTime_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out NodaTime.OffsetDateTime _));
    }

    [TestMethod]
    public void TryGetValue_OffsetDate_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out NodaTime.OffsetDate _));
    }

    [TestMethod]
    public void TryGetValue_OffsetTime_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out NodaTime.OffsetTime _));
    }

    [TestMethod]
    public void TryGetValue_LocalDate_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out NodaTime.LocalDate _));
    }

    // Note: TryGetValue(int, out Period) is omitted because NodaTime.Period (reference type)
    // causes overload resolution issues in the test project's non-nullable context.

#if NET
    [TestMethod]
    public void TryGetValue_Int128_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out Int128 _));
    }

    [TestMethod]
    public void TryGetValue_UInt128_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out UInt128 _));
    }

    [TestMethod]
    public void TryGetValue_Half_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out Half _));
    }

    [TestMethod]
    public void TryGetValue_DateOnly_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out DateOnly _));
    }

    [TestMethod]
    public void TryGetValue_TimeOnly_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out TimeOnly _));
    }
#endif

    // --- TryGetValue for byte array ---

    [TestMethod]
    public void TryGetValue_ByteArray_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetValue(0, out byte[]? _));
    }

    // --- GetRawSimpleValue ---

    [TestMethod]
    public void GetRawSimpleValue_Number_ReturnsRaw()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        CollectionAssert.AreEqual(raw, d.GetRawSimpleValue(0).ToArray());
    }

    [TestMethod]
    public void GetRawSimpleValue_String_WithQuotes()
    {
        byte[] raw = "\"hi\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        CollectionAssert.AreEqual(raw, d.GetRawSimpleValue(0, includeQuotes: true).ToArray());
    }

    [TestMethod]
    public void GetRawSimpleValue_String_WithoutQuotes()
    {
        byte[] raw = "\"hi\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        CollectionAssert.AreEqual("hi"u8.ToArray(), d.GetRawSimpleValue(0, includeQuotes: false).ToArray());
    }

    // --- GetHashCode ---

    [TestMethod]
    public void GetHashCode_Number_ReturnsConsistentValue()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        int hash1 = d.GetHashCode(0);
        int hash2 = d.GetHashCode(0);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void GetHashCode_String_ReturnsConsistentValue()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        int hash1 = d.GetHashCode(0);
        int hash2 = d.GetHashCode(0);
        Assert.AreEqual(hash1, hash2);
    }

    // --- ToString ---

    [TestMethod]
    public void ToString_Number_ReturnsNumberString()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.AreEqual("42", d.ToString(0));
    }

    [TestMethod]
    public void ToString_String_ReturnsUnescapedString()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.AreEqual("hello", d.ToString(0));
    }

    [TestMethod]
    public void ToString_String_UnescapesContent()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.AreEqual("a\nb", d.ToString(0));
    }

    // --- TryFindNextDescendantPropertyValue ---

    [TestMethod]
    public void TryFindNextDescendantPropertyValue_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        int scanIndex = 0;
        Assert.IsFalse(d.TryFindNextDescendantPropertyValue(0, ref scanIndex, "x"u8, out int valueIndex));
        Assert.AreEqual(-1, valueIndex);
    }

    // --- WriteElementTo ---

    [TestMethod]
    public void WriteElementTo_Number_WritesNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartArray();
        d.WriteElementTo(0, writer);
        writer.WriteEndArray();
        writer.Flush();
        Assert.AreEqual("[42]", Encoding.UTF8.GetString(buffer.WrittenMemory.Span.ToArray()));
    }

    [TestMethod]
    public void WriteElementTo_String_WritesString()
    {
        byte[] raw = "\"test\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartArray();
        d.WriteElementTo(0, writer);
        writer.WriteEndArray();
        writer.Flush();
        Assert.AreEqual("[\"test\"]", Encoding.UTF8.GetString(buffer.WrittenMemory.Span.ToArray()));
    }

    // --- EnsurePropertyMap (no-op) ---

    [TestMethod]
    public void EnsurePropertyMap_DoesNotThrow()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        d.EnsurePropertyMap(0); // Should be a no-op
    }

    // --- ValueIsEscaped ---

    [TestMethod]
    public void ValueIsEscaped_ReturnsFalse()
    {
        byte[] raw = "\"hi\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.ValueIsEscaped(0, isPropertyName: false));
    }

    // --- TryResolveJsonPointer ---

    [TestMethod]
    public void TryResolveJsonPointer_EmptyPointer_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryResolveJsonPointer<JsonElement>("/foo"u8, 0, out _));
    }

    [TestMethod]
    public void TryResolveJsonPointer_RootSlash_ReturnsTrue()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TryResolveJsonPointer<JsonElement>("/"u8, 0, out _));
    }

    [TestMethod]
    public void TryResolveJsonPointer_Hash_ReturnsTrue()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TryResolveJsonPointer<JsonElement>("#"u8, 0, out _));
    }

    // --- TryGetLineAndOffset / TryGetLine ---

    [TestMethod]
    public void TryGetLineAndOffset_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetLineAndOffset(0, out _, out _, out _));
    }

    [TestMethod]
    public void TryGetLineAndOffsetForPointer_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetLineAndOffsetForPointer("/"u8, 0, out _, out _, out _));
    }

    [TestMethod]
    public void TryGetLine_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsFalse(d.TryGetLine(0, out ReadOnlyMemory<byte> _));
    }

    // --- CloneElement ---

    [TestMethod]
    public void CloneElement_Number_ReturnsEquivalent()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        JsonElement clone = d.CloneElement(0);
        Assert.AreEqual(JsonValueKind.Number, clone.ValueKind);
    }

    [TestMethod]
    public void CloneElement_Generic_ReturnsEquivalent()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        JsonElement clone = d.CloneElement<JsonElement>(0);
        Assert.AreEqual(JsonValueKind.Number, clone.ValueKind);
    }

    // --- GetRawValueAsString ---

    [TestMethod]
    public void GetRawValueAsString_Number()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.AreEqual("42", d.GetRawValueAsString(0));
    }

    [TestMethod]
    public void GetRawValueAsString_String()
    {
        byte[] raw = "\"hi\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.AreEqual("\"hi\"", d.GetRawValueAsString(0));
    }

    // --- GetDbSize ---

    [TestMethod]
    public void GetDbSize_ReturnsDbRowSize()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        int size = d.GetDbSize(0, includeEndElement: false);
        Assert.IsTrue(size > 0);
    }

    // --- GetStartIndex ---

    [TestMethod]
    public void GetStartIndex_ReturnsZero()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.AreEqual(0, d.GetStartIndex(0));
    }

    // --- IsDisposable / IsImmutable ---

    [TestMethod]
    public void IsDisposable_ReturnsTrue()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.IsDisposable);
    }

    [TestMethod]
    public void IsImmutable_ReturnsTrue()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.IsImmutable);
    }

    // --- GetRawValue ---

    [TestMethod]
    public void GetRawValue_Number_IncludeQuotes()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        RawUtf8JsonString result = d.GetRawValue(0, includeQuotes: true);
        CollectionAssert.AreEqual(raw, result.Span.ToArray());
    }

    [TestMethod]
    public void GetRawValue_String_ExcludeQuotes()
    {
        byte[] raw = "\"hi\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        RawUtf8JsonString result = d.GetRawValue(0, includeQuotes: false);
        CollectionAssert.AreEqual("hi"u8.ToArray(), result.Span.ToArray());
    }

    // --- TextEquals with escaped content > StackallocByteThreshold (256 bytes) ---
    // Covers ArrayPool rent/return paths in TextEquals

    [TestMethod]
    public void TextEquals_ByteSpan_LongEscapedContent_Matches()
    {
        // Create a string document with >256 bytes of escaped content to trigger ArrayPool path
        string longValue = new string('x', 300);
        string escapedValue = longValue.Replace("x", "\\u0078"); // Each char → 6 bytes of escape
        byte[] raw = Encoding.UTF8.GetBytes($"\"{escapedValue}\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TextEquals(0, Encoding.UTF8.GetBytes(longValue), isPropertyName: false, shouldUnescape: false));
    }

    [TestMethod]
    public void TextEquals_CharSpan_LongEscapedContent_Matches()
    {
        string longValue = new string('y', 300);
        string escapedValue = longValue.Replace("y", "\\u0079");
        byte[] raw = Encoding.UTF8.GetBytes($"\"{escapedValue}\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TextEquals(0, longValue.AsSpan(), isPropertyName: false));
    }

    [TestMethod]
    public void TextEquals_CharSpan_LongText_NoEscape()
    {
        // Long non-escaped string to trigger ArrayPool path for the comparison text
        string longValue = new string('z', 300);
        byte[] raw = Encoding.UTF8.GetBytes($"\"{longValue}\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.IsTrue(d.TextEquals(0, longValue.AsSpan(), isPropertyName: false));
    }

    // --- GetUtf16JsonString with non-escaped content (covers non-escape path) ---

    [TestMethod]
    public void GetUtf16JsonString_NoEscapes_ReturnsValue()
    {
        byte[] raw = "\"simple\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        using UnescapedUtf16JsonString result = d.GetUtf16JsonString(0, JsonTokenType.String);
        Assert.AreEqual("simple", result.Span.ToString());
    }
}
