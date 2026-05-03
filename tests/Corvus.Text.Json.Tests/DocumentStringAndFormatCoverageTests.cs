// Copyright (c) Endjin Limited. All rights reserved.

using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests targeting ParsedJsonDocument string handling, TextEquals,
/// and TryFormat paths through the public JsonElement API.
/// </summary>
public static class DocumentStringAndFormatCoverageTests
{
    #region ValueEquals with long text (ArrayPool rent path in base class)

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_LongText_Matching()
    {
        // A string > 85 chars forces ArrayPool rent in TextEqualsUnsafe(char)
        // because length * MaxExpansionFactorWhileTranscoding (3) > StackallocByteThreshold (256)
        string longString = new('a', 100);
        string json = $"\"{longString}\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.True(element.ValueEquals(longString.AsSpan()));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_LongText_NotMatching()
    {
        string longString = new('a', 100);
        string json = $"\"{longString}\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        string differentLong = new('b', 100);
        Assert.False(element.ValueEquals(differentLong.AsSpan()));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_LongText_String_Matching()
    {
        string longString = new('x', 100);
        string json = $"\"{longString}\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.True(element.ValueEquals(longString));
    }

    #endregion

    #region ValueEquals with escaped strings

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_EscapedString_Matching()
    {
        // JSON with escape sequence: stored as hello\nworld in JSON text
        string json = "\"hello\\nworld\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        // The actual value after unescaping is "hello" + newline + "world"
        Assert.True(element.ValueEquals("hello\nworld".AsSpan()));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_EscapedString_PrefixMismatch()
    {
        string json = "\"hello\\nworld\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        // The prefix "XXXXX" doesn't match "hello" before the escape
        Assert.False(element.ValueEquals("XXXXX\nworld".AsSpan()));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_EscapedString_TooShort()
    {
        // JSON has \uXXXX escapes which expand to 6 bytes each in the stored form
        // If the target is shorter than segment.Length / MaxExpansionFactorWhileEscaping, returns false
        string json = "\"abc\\u0041\\u0042\\u0043def\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        // Target "x" is way shorter than minimum possible unescaped length
        Assert.False(element.ValueEquals("x".AsSpan()));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_EscapedString_SuffixMismatch()
    {
        string json = "\"hello\\tworld\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        // Prefix "hello" matches, but after the escape, "Xorld" != "world"
        Assert.False(element.ValueEquals("hello\tXorld".AsSpan()));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_Utf8_EscapedString_Matching()
    {
        string json = "\"hello\\nworld\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.True(element.ValueEquals("hello\nworld"u8));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_Utf8_EscapedString_PrefixMismatch()
    {
        string json = "\"hello\\nworld\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.False(element.ValueEquals("XXXXX\nworld"u8));
    }

    #endregion

    #region ValueEquals with text longer than stored value

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_TextLongerThanStored_ReturnsFalse()
    {
        string json = "\"short\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.False(element.ValueEquals("this is much longer than the stored short string".AsSpan()));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_Utf8_TextLongerThanStored_ReturnsFalse()
    {
        string json = "\"short\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.False(element.ValueEquals("this is much longer than the stored short string"u8));
    }

    #endregion

    #region TryFormat(byte[]) with too-small destination

    [Fact]
    [Trait("category", "coverage")]
    public static void TryFormat_Utf8_True_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[1]; // "true" needs 4 bytes
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void TryFormat_Utf8_False_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[1]; // "false" needs 5 bytes
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void TryFormat_Utf8_Array_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[1];
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void TryFormat_Utf8_Object_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[1];
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void TryFormat_Utf8_String_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello world\"");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[1];
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void TryFormat_Utf8_Null_Succeeds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[0];
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.True(result);
        Assert.Equal(0, bytesWritten);
    }

    #endregion

    #region TryFormat(char[]) with too-small destination

    [Fact]
    [Trait("category", "coverage")]
    public static void TryFormat_Char_True_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        JsonElement element = doc.RootElement;

        Span<char> destination = stackalloc char[1];
        bool result = element.TryFormat(destination, out int charsWritten, default, null);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void TryFormat_Char_False_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        JsonElement element = doc.RootElement;

        Span<char> destination = stackalloc char[1];
        bool result = element.TryFormat(destination, out int charsWritten, default, null);

        Assert.False(result);
        Assert.Equal(0, charsWritten);
    }

    #endregion

    #region ValueEquals on Null elements

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_Null_WithNullString_ReturnsTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        JsonElement element = doc.RootElement;

        Assert.True(element.ValueEquals((string?)null));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_Null_WithNonNullString_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        JsonElement element = doc.RootElement;

        Assert.False(element.ValueEquals("hello"));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_Null_WithUtf8_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        JsonElement element = doc.RootElement;

        // For null elements, ValueEquals(ReadOnlySpan<byte>) returns true only if the span is default
        Assert.False(element.ValueEquals("hello"u8));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_Null_WithCharSpan_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        JsonElement element = doc.RootElement;

        Assert.False(element.ValueEquals("hello".AsSpan()));
    }

    #endregion

    #region Escaped property name comparison

    [Fact]
    [Trait("category", "coverage")]
    public static void TryGetProperty_EscapedPropertyName_CharSpan()
    {
        string json = "{\"hello\\nworld\": 42}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.True(element.TryGetProperty("hello\nworld", out JsonElement value));
        Assert.Equal(42, value.GetInt32());
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void TryGetProperty_EscapedPropertyName_Utf8()
    {
        string json = "{\"hello\\tworld\": 42}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.True(element.TryGetProperty("hello\tworld"u8, out JsonElement value));
        Assert.Equal(42, value.GetInt32());
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void TryGetProperty_EscapedPropertyName_NoMatch()
    {
        string json = "{\"hello\\nworld\": 42}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.False(element.TryGetProperty("nomatch", out _));
    }

    #endregion

    #region Long escaped strings (ArrayPool + escape combined)

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_LongEscapedString_Matching()
    {
        // String with escapes and > 85 chars to trigger both ArrayPool AND escape paths
        string padding = new('a', 90);
        string json = $"\"{padding}\\n{padding}\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        string expected = padding + "\n" + padding;
        Assert.True(element.ValueEquals(expected.AsSpan()));
    }

    [Fact]
    [Trait("category", "coverage")]
    public static void ValueEquals_LongEscapedString_NotMatching()
    {
        string padding = new('a', 90);
        string json = $"\"{padding}\\n{padding}\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        string notExpected = padding + "\t" + padding;
        Assert.False(element.ValueEquals(notExpected.AsSpan()));
    }

    #endregion
}
