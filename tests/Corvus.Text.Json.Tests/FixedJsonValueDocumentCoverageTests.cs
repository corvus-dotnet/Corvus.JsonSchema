// <copyright file="FixedJsonValueDocumentCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for <see cref="FixedJsonValueDocument{T}"/>.
/// Targets uncovered IJsonDocument interface methods:
/// wrong-type throws, TryGetValue negative paths, string escape handling,
/// TextEquals, and various accessor methods.
/// </summary>
[Trait("category", "coverage")]
public static class FixedJsonValueDocumentCoverageTests
{
    // --- Factory methods (ForNumber, ForString, ForNumberFromSpan, ForStringFromSpan) ---

    [Fact]
    public static void ForNumber_CreatesNumberDocument()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Equal(JsonTokenType.Number, d.GetJsonTokenType(0));
    }

    [Fact]
    public static void ForString_CreatesStringDocument()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.Equal(JsonTokenType.String, d.GetJsonTokenType(0));
    }

    [Fact]
    public static void ForNumberFromSpan_CreatesNumberDocument()
    {
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumberFromSpan("99"u8);
        IJsonDocument d = doc;
        Assert.Equal(JsonTokenType.Number, d.GetJsonTokenType(0));
    }

    [Fact]
    public static void ForStringFromSpan_CreatesStringDocument()
    {
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForStringFromSpan("\"test\""u8);
        IJsonDocument d = doc;
        Assert.Equal(JsonTokenType.String, d.GetJsonTokenType(0));
    }

    // --- RootElement ---

    [Fact]
    public static void RootElement_Number_ReturnsJsonElement()
    {
        byte[] raw = "123"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Number, root.ValueKind);
    }

    [Fact]
    public static void RootElement_String_ReturnsJsonElement()
    {
        byte[] raw = "\"world\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.String, root.ValueKind);
    }

    // --- Array operations throw for number document ---

    [Fact]
    public static void GetArrayIndexElement_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetArrayIndexElement(0, 0));
    }

    [Fact]
    public static void GetArrayIndexElement_Generic_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetArrayIndexElement<JsonElement>(0, 0));
    }

    [Fact]
    public static void GetArrayIndexElement_OutParams_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetArrayIndexElement(0, 0, out _, out _));
    }

    [Fact]
    public static void GetArrayInsertionIndex_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetArrayInsertionIndex(0, 0));
    }

    [Fact]
    public static void GetArrayLength_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetArrayLength(0));
    }

    // --- Array operations throw for string document ---

    [Fact]
    public static void GetArrayLength_Throws_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetArrayLength(0));
    }

    // --- Object operations throw for number document ---

    [Fact]
    public static void GetNameOfPropertyValue_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetNameOfPropertyValue(0));
    }

    [Fact]
    public static void GetPropertyCount_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetPropertyCount(0));
    }

    [Fact]
    public static void GetPropertyName_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetPropertyName(0));
    }

    [Fact]
    public static void GetPropertyNameRaw_Span_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetPropertyNameRaw(0));
    }

    [Fact]
    public static void GetPropertyNameRaw_Memory_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetPropertyNameRaw(0, includeQuotes: true));
    }

    [Fact]
    public static void GetPropertyNameUnescaped_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetPropertyNameUnescaped(0));
    }

    [Fact]
    public static void GetPropertyRawValueAsString_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetPropertyRawValueAsString(0));
    }

    // --- TryGetNamedPropertyValue overloads throw ---

    [Fact]
    public static void TryGetNamedPropertyValue_CharSpan_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.TryGetNamedPropertyValue(0, "foo".AsSpan(), out JsonElement _));
    }

    [Fact]
    public static void TryGetNamedPropertyValue_ByteSpan_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.TryGetNamedPropertyValue(0, "foo"u8, out JsonElement _));
    }

    [Fact]
    public static void TryGetNamedPropertyValue_GenericByte_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.TryGetNamedPropertyValue<JsonElement>(0, "foo"u8, out _));
    }

    [Fact]
    public static void TryGetNamedPropertyValue_GenericChar_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.TryGetNamedPropertyValue<JsonElement>(0, "foo".AsSpan(), out _));
    }

    [Fact]
    public static void TryGetNamedPropertyValue_CharSpan_OutDoc_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.TryGetNamedPropertyValue(0, "foo".AsSpan(), out IJsonDocument? _, out int _));
    }

    [Fact]
    public static void TryGetNamedPropertyValue_ByteSpan_OutDoc_Throws_ForNumber()
    {
        byte[] raw = "1"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.TryGetNamedPropertyValue(0, "foo"u8, out IJsonDocument? _, out int _));
    }

    // --- GetString / TryGetString ---

    [Fact]
    public static void GetString_ReturnsString_ForStringDocument()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.Equal("hello", d.GetString(0, JsonTokenType.String));
    }

    [Fact]
    public static void GetString_Throws_ForNumberDocument()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetString(0, JsonTokenType.String));
    }

    [Fact]
    public static void GetString_UnescapesContent()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        string result = d.GetString(0, JsonTokenType.String)!;
        Assert.Equal("hello\nworld", result);
    }

    [Fact]
    public static void TryGetString_ReturnsTrue_ForString()
    {
        byte[] raw = "\"abc\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.True(d.TryGetString(0, JsonTokenType.String, out string? result));
        Assert.Equal("abc", result);
    }

    [Fact]
    public static void TryGetString_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetString(0, JsonTokenType.String, out string? result));
        Assert.Null(result);
    }

    [Fact]
    public static void TryGetString_UnescapesContent()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"tab\\there\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.True(d.TryGetString(0, JsonTokenType.String, out string? result));
        Assert.Equal("tab\there", result);
    }

    // --- GetUtf8JsonString ---

    [Fact]
    public static void GetUtf8JsonString_ReturnsValue_ForString()
    {
        byte[] raw = "\"test\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        using UnescapedUtf8JsonString result = d.GetUtf8JsonString(0, JsonTokenType.String);
        Assert.Equal("test"u8.ToArray(), result.Span.ToArray());
    }

    [Fact]
    public static void GetUtf8JsonString_Throws_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetUtf8JsonString(0, JsonTokenType.String));
    }

    [Fact]
    public static void GetUtf8JsonString_UnescapesContent()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        using UnescapedUtf8JsonString result = d.GetUtf8JsonString(0, JsonTokenType.String);
        Assert.Equal(Encoding.UTF8.GetBytes("a\nb"), result.Span.ToArray());
    }

    // --- GetUtf16JsonString ---

    [Fact]
    public static void GetUtf16JsonString_ReturnsValue_ForString()
    {
        byte[] raw = "\"test\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        using UnescapedUtf16JsonString result = d.GetUtf16JsonString(0, JsonTokenType.String);
        Assert.Equal("test", result.Span.ToString());
    }

    [Fact]
    public static void GetUtf16JsonString_Throws_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Throws<InvalidOperationException>(() => d.GetUtf16JsonString(0, JsonTokenType.String));
    }

    [Fact]
    public static void GetUtf16JsonString_UnescapesContent()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"x\\ty\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        using UnescapedUtf16JsonString result = d.GetUtf16JsonString(0, JsonTokenType.String);
        Assert.Equal("x\ty", result.Span.ToString());
    }

    // --- TextEquals (char span) ---

    [Fact]
    public static void TextEquals_CharSpan_ReturnsTrue_ForMatchingString()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.True(d.TextEquals(0, "hello".AsSpan(), isPropertyName: false));
    }

    [Fact]
    public static void TextEquals_CharSpan_ReturnsFalse_ForMismatch()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TextEquals(0, "world".AsSpan(), isPropertyName: false));
    }

    [Fact]
    public static void TextEquals_CharSpan_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TextEquals(0, "42".AsSpan(), isPropertyName: false));
    }

    [Fact]
    public static void TextEquals_CharSpan_WithEscapedContent_Matches()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.True(d.TextEquals(0, "a\nb".AsSpan(), isPropertyName: false));
    }

    // --- TextEquals (byte span) ---

    [Fact]
    public static void TextEquals_ByteSpan_ReturnsTrue_ForMatchingString()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.True(d.TextEquals(0, "hello"u8, isPropertyName: false, shouldUnescape: false));
    }

    [Fact]
    public static void TextEquals_ByteSpan_ReturnsFalse_ForMismatch()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TextEquals(0, "world"u8, isPropertyName: false, shouldUnescape: false));
    }

    [Fact]
    public static void TextEquals_ByteSpan_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TextEquals(0, "42"u8, isPropertyName: false, shouldUnescape: false));
    }

    [Fact]
    public static void TextEquals_ByteSpan_WithEscapedContent_Matches()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.True(d.TextEquals(0, Encoding.UTF8.GetBytes("a\nb"), isPropertyName: false, shouldUnescape: false));
    }

    // --- TryGetValue for numeric types on a STRING document (returns false) ---

    [Fact]
    public static void TryGetValue_Sbyte_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out sbyte _));
    }

    [Fact]
    public static void TryGetValue_Byte_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out byte _));
    }

    [Fact]
    public static void TryGetValue_Short_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out short _));
    }

    [Fact]
    public static void TryGetValue_Ushort_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out ushort _));
    }

    [Fact]
    public static void TryGetValue_Int_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out int _));
    }

    [Fact]
    public static void TryGetValue_Uint_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out uint _));
    }

    [Fact]
    public static void TryGetValue_Long_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out long _));
    }

    [Fact]
    public static void TryGetValue_Ulong_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out ulong _));
    }

    [Fact]
    public static void TryGetValue_Double_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out double _));
    }

    [Fact]
    public static void TryGetValue_Float_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out float _));
    }

    [Fact]
    public static void TryGetValue_Decimal_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out decimal _));
    }

    [Fact]
    public static void TryGetValue_BigInteger_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out System.Numerics.BigInteger _));
    }

    [Fact]
    public static void TryGetValue_BigNumber_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out Corvus.Numerics.BigNumber _));
    }

    // --- TryGetValue for date/time types on a NUMBER document (returns false) ---

    [Fact]
    public static void TryGetValue_DateTime_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out DateTime _));
    }

    [Fact]
    public static void TryGetValue_DateTimeOffset_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out DateTimeOffset _));
    }

    [Fact]
    public static void TryGetValue_Guid_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out Guid _));
    }

    [Fact]
    public static void TryGetValue_OffsetDateTime_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out NodaTime.OffsetDateTime _));
    }

    [Fact]
    public static void TryGetValue_OffsetDate_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out NodaTime.OffsetDate _));
    }

    [Fact]
    public static void TryGetValue_OffsetTime_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out NodaTime.OffsetTime _));
    }

    [Fact]
    public static void TryGetValue_LocalDate_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out NodaTime.LocalDate _));
    }

    // Note: TryGetValue(int, out Period) is omitted because NodaTime.Period (reference type)
    // causes overload resolution issues in the test project's non-nullable context.

#if NET
    [Fact]
    public static void TryGetValue_Int128_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out Int128 _));
    }

    [Fact]
    public static void TryGetValue_UInt128_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out UInt128 _));
    }

    [Fact]
    public static void TryGetValue_Half_ReturnsFalse_ForString()
    {
        byte[] raw = "\"x\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out Half _));
    }

    [Fact]
    public static void TryGetValue_DateOnly_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out DateOnly _));
    }

    [Fact]
    public static void TryGetValue_TimeOnly_ReturnsFalse_ForNumber()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out TimeOnly _));
    }
#endif

    // --- TryGetValue for byte array ---

    [Fact]
    public static void TryGetValue_ByteArray_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetValue(0, out byte[]? _));
    }

    // --- GetRawSimpleValue ---

    [Fact]
    public static void GetRawSimpleValue_Number_ReturnsRaw()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Equal(raw, d.GetRawSimpleValue(0).ToArray());
    }

    [Fact]
    public static void GetRawSimpleValue_String_WithQuotes()
    {
        byte[] raw = "\"hi\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.Equal(raw, d.GetRawSimpleValue(0, includeQuotes: true).ToArray());
    }

    [Fact]
    public static void GetRawSimpleValue_String_WithoutQuotes()
    {
        byte[] raw = "\"hi\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.Equal("hi"u8.ToArray(), d.GetRawSimpleValue(0, includeQuotes: false).ToArray());
    }

    // --- GetHashCode ---

    [Fact]
    public static void GetHashCode_Number_ReturnsConsistentValue()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        int hash1 = d.GetHashCode(0);
        int hash2 = d.GetHashCode(0);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public static void GetHashCode_String_ReturnsConsistentValue()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        int hash1 = d.GetHashCode(0);
        int hash2 = d.GetHashCode(0);
        Assert.Equal(hash1, hash2);
    }

    // --- ToString ---

    [Fact]
    public static void ToString_Number_ReturnsNumberString()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Equal("42", d.ToString(0));
    }

    [Fact]
    public static void ToString_String_ReturnsUnescapedString()
    {
        byte[] raw = "\"hello\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.Equal("hello", d.ToString(0));
    }

    [Fact]
    public static void ToString_String_UnescapesContent()
    {
        byte[] raw = Encoding.UTF8.GetBytes("\"a\\nb\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.Equal("a\nb", d.ToString(0));
    }

    // --- TryFindNextDescendantPropertyValue ---

    [Fact]
    public static void TryFindNextDescendantPropertyValue_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        int scanIndex = 0;
        Assert.False(d.TryFindNextDescendantPropertyValue(0, ref scanIndex, "x"u8, out int valueIndex));
        Assert.Equal(-1, valueIndex);
    }

    // --- WriteElementTo ---

    [Fact]
    public static void WriteElementTo_Number_WritesNumber()
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
        Assert.Equal("[42]", Encoding.UTF8.GetString(buffer.WrittenMemory.Span.ToArray()));
    }

    [Fact]
    public static void WriteElementTo_String_WritesString()
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
        Assert.Equal("[\"test\"]", Encoding.UTF8.GetString(buffer.WrittenMemory.Span.ToArray()));
    }

    // --- EnsurePropertyMap (no-op) ---

    [Fact]
    public static void EnsurePropertyMap_DoesNotThrow()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        d.EnsurePropertyMap(0); // Should be a no-op
    }

    // --- ValueIsEscaped ---

    [Fact]
    public static void ValueIsEscaped_ReturnsFalse()
    {
        byte[] raw = "\"hi\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.False(d.ValueIsEscaped(0, isPropertyName: false));
    }

    // --- TryResolveJsonPointer ---

    [Fact]
    public static void TryResolveJsonPointer_EmptyPointer_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryResolveJsonPointer<JsonElement>("/foo"u8, 0, out _));
    }

    [Fact]
    public static void TryResolveJsonPointer_RootSlash_ReturnsTrue()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.True(d.TryResolveJsonPointer<JsonElement>("/"u8, 0, out _));
    }

    [Fact]
    public static void TryResolveJsonPointer_Hash_ReturnsTrue()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.True(d.TryResolveJsonPointer<JsonElement>("#"u8, 0, out _));
    }

    // --- TryGetLineAndOffset / TryGetLine ---

    [Fact]
    public static void TryGetLineAndOffset_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetLineAndOffset(0, out _, out _, out _));
    }

    [Fact]
    public static void TryGetLineAndOffsetForPointer_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetLineAndOffsetForPointer("/"u8, 0, out _, out _, out _));
    }

    [Fact]
    public static void TryGetLine_ReturnsFalse()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.False(d.TryGetLine(0, out ReadOnlyMemory<byte> _));
    }

    // --- CloneElement ---

    [Fact]
    public static void CloneElement_Number_ReturnsEquivalent()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        JsonElement clone = d.CloneElement(0);
        Assert.Equal(JsonValueKind.Number, clone.ValueKind);
    }

    [Fact]
    public static void CloneElement_Generic_ReturnsEquivalent()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        JsonElement clone = d.CloneElement<JsonElement>(0);
        Assert.Equal(JsonValueKind.Number, clone.ValueKind);
    }

    // --- GetRawValueAsString ---

    [Fact]
    public static void GetRawValueAsString_Number()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Equal("42", d.GetRawValueAsString(0));
    }

    [Fact]
    public static void GetRawValueAsString_String()
    {
        byte[] raw = "\"hi\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.Equal("\"hi\"", d.GetRawValueAsString(0));
    }

    // --- GetDbSize ---

    [Fact]
    public static void GetDbSize_ReturnsDbRowSize()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        int size = d.GetDbSize(0, includeEndElement: false);
        Assert.True(size > 0);
    }

    // --- GetStartIndex ---

    [Fact]
    public static void GetStartIndex_ReturnsZero()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.Equal(0, d.GetStartIndex(0));
    }

    // --- IsDisposable / IsImmutable ---

    [Fact]
    public static void IsDisposable_ReturnsTrue()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.True(d.IsDisposable);
    }

    [Fact]
    public static void IsImmutable_ReturnsTrue()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        Assert.True(d.IsImmutable);
    }

    // --- GetRawValue ---

    [Fact]
    public static void GetRawValue_Number_IncludeQuotes()
    {
        byte[] raw = "42"u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumber(raw);
        IJsonDocument d = doc;
        RawUtf8JsonString result = d.GetRawValue(0, includeQuotes: true);
        Assert.Equal(raw, result.Span.ToArray());
    }

    [Fact]
    public static void GetRawValue_String_ExcludeQuotes()
    {
        byte[] raw = "\"hi\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        RawUtf8JsonString result = d.GetRawValue(0, includeQuotes: false);
        Assert.Equal("hi"u8.ToArray(), result.Span.ToArray());
    }

    // --- TextEquals with escaped content > StackallocByteThreshold (256 bytes) ---
    // Covers ArrayPool rent/return paths in TextEquals

    [Fact]
    public static void TextEquals_ByteSpan_LongEscapedContent_Matches()
    {
        // Create a string document with >256 bytes of escaped content to trigger ArrayPool path
        string longValue = new string('x', 300);
        string escapedValue = longValue.Replace("x", "\\u0078"); // Each char → 6 bytes of escape
        byte[] raw = Encoding.UTF8.GetBytes($"\"{escapedValue}\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.True(d.TextEquals(0, Encoding.UTF8.GetBytes(longValue), isPropertyName: false, shouldUnescape: false));
    }

    [Fact]
    public static void TextEquals_CharSpan_LongEscapedContent_Matches()
    {
        string longValue = new string('y', 300);
        string escapedValue = longValue.Replace("y", "\\u0079");
        byte[] raw = Encoding.UTF8.GetBytes($"\"{escapedValue}\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.True(d.TextEquals(0, longValue.AsSpan(), isPropertyName: false));
    }

    [Fact]
    public static void TextEquals_CharSpan_LongText_NoEscape()
    {
        // Long non-escaped string to trigger ArrayPool path for the comparison text
        string longValue = new string('z', 300);
        byte[] raw = Encoding.UTF8.GetBytes($"\"{longValue}\"");
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        Assert.True(d.TextEquals(0, longValue.AsSpan(), isPropertyName: false));
    }

    // --- GetUtf16JsonString with non-escaped content (covers non-escape path) ---

    [Fact]
    public static void GetUtf16JsonString_NoEscapes_ReturnsValue()
    {
        byte[] raw = "\"simple\""u8.ToArray();
        using FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForString(raw);
        IJsonDocument d = doc;
        using UnescapedUtf16JsonString result = d.GetUtf16JsonString(0, JsonTokenType.String);
        Assert.Equal("simple", result.Span.ToString());
    }
}
