// Copyright (c) Endjin Limited. All rights reserved.

using Corvus.Text.Json.Internal;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests targeting ParsedJsonDocument string handling, TextEquals,
/// and TryFormat paths through the public JsonElement API.
/// </summary>
[TestClass]
public class DocumentStringAndFormatCoverageTests
{
    #region ValueEquals with long text (ArrayPool rent path in base class)

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_LongText_Matching()
    {
        // A string > 85 chars forces ArrayPool rent in TextEqualsUnsafe(char)
        // because length * MaxExpansionFactorWhileTranscoding (3) > StackallocByteThreshold (256)
        string longString = new('a', 100);
        string json = $"\"{longString}\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.IsTrue(element.ValueEquals(longString.AsSpan()));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_LongText_NotMatching()
    {
        string longString = new('a', 100);
        string json = $"\"{longString}\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        string differentLong = new('b', 100);
        Assert.IsFalse(element.ValueEquals(differentLong.AsSpan()));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_LongText_String_Matching()
    {
        string longString = new('x', 100);
        string json = $"\"{longString}\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.IsTrue(element.ValueEquals(longString));
    }

    #endregion

    #region ValueEquals with escaped strings

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_EscapedString_Matching()
    {
        // JSON with escape sequence: stored as hello\nworld in JSON text
        string json = "\"hello\\nworld\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        // The actual value after unescaping is "hello" + newline + "world"
        Assert.IsTrue(element.ValueEquals("hello\nworld".AsSpan()));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_EscapedString_PrefixMismatch()
    {
        string json = "\"hello\\nworld\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        // The prefix "XXXXX" doesn't match "hello" before the escape
        Assert.IsFalse(element.ValueEquals("XXXXX\nworld".AsSpan()));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_EscapedString_TooShort()
    {
        // JSON has \uXXXX escapes which expand to 6 bytes each in the stored form
        // If the target is shorter than segment.Length / MaxExpansionFactorWhileEscaping, returns false
        string json = "\"abc\\u0041\\u0042\\u0043def\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        // Target "x" is way shorter than minimum possible unescaped length
        Assert.IsFalse(element.ValueEquals("x".AsSpan()));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_EscapedString_SuffixMismatch()
    {
        string json = "\"hello\\tworld\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        // Prefix "hello" matches, but after the escape, "Xorld" != "world"
        Assert.IsFalse(element.ValueEquals("hello\tXorld".AsSpan()));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_Utf8_EscapedString_Matching()
    {
        string json = "\"hello\\nworld\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.IsTrue(element.ValueEquals("hello\nworld"u8));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_Utf8_EscapedString_PrefixMismatch()
    {
        string json = "\"hello\\nworld\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.IsFalse(element.ValueEquals("XXXXX\nworld"u8));
    }

    #endregion

    #region ValueEquals with text longer than stored value

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_TextLongerThanStored_ReturnsFalse()
    {
        string json = "\"short\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.IsFalse(element.ValueEquals("this is much longer than the stored short string".AsSpan()));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_Utf8_TextLongerThanStored_ReturnsFalse()
    {
        string json = "\"short\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.IsFalse(element.ValueEquals("this is much longer than the stored short string"u8));
    }

    #endregion

    #region TryFormat(byte[]) with too-small destination

    [TestMethod]
    [TestCategory("coverage")]
    public void TryFormat_Utf8_True_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[1]; // "true" needs 4 bytes
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void TryFormat_Utf8_False_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[1]; // "false" needs 5 bytes
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void TryFormat_Utf8_Array_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[1];
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void TryFormat_Utf8_Object_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[1];
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void TryFormat_Utf8_String_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello world\"");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[1];
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void TryFormat_Utf8_Null_Succeeds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        JsonElement element = doc.RootElement;

        Span<byte> destination = stackalloc byte[0];
        bool result = element.TryFormat(destination, out int bytesWritten, default, null);

        Assert.IsTrue(result);
        Assert.AreEqual(0, bytesWritten);
    }

    #endregion

    #region TryFormat(char[]) with too-small destination

    [TestMethod]
    [TestCategory("coverage")]
    public void TryFormat_Char_True_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        JsonElement element = doc.RootElement;

        Span<char> destination = stackalloc char[1];
        bool result = element.TryFormat(destination, out int charsWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void TryFormat_Char_False_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        JsonElement element = doc.RootElement;

        Span<char> destination = stackalloc char[1];
        bool result = element.TryFormat(destination, out int charsWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    #endregion

    #region ValueEquals on Null elements

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_Null_WithNullString_ReturnsTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        JsonElement element = doc.RootElement;

        Assert.IsTrue(element.ValueEquals((string?)null));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_Null_WithNonNullString_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        JsonElement element = doc.RootElement;

        Assert.IsFalse(element.ValueEquals("hello"));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_Null_WithUtf8_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        JsonElement element = doc.RootElement;

        // For null elements, ValueEquals(ReadOnlySpan<byte>) returns true only if the span is default
        Assert.IsFalse(element.ValueEquals("hello"u8));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_Null_WithCharSpan_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        JsonElement element = doc.RootElement;

        Assert.IsFalse(element.ValueEquals("hello".AsSpan()));
    }

    #endregion

    #region Escaped property name comparison

    [TestMethod]
    [TestCategory("coverage")]
    public void TryGetProperty_EscapedPropertyName_CharSpan()
    {
        string json = "{\"hello\\nworld\": 42}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.IsTrue(element.TryGetProperty("hello\nworld", out JsonElement value));
        Assert.AreEqual(42, value.GetInt32());
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void TryGetProperty_EscapedPropertyName_Utf8()
    {
        string json = "{\"hello\\tworld\": 42}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.IsTrue(element.TryGetProperty("hello\tworld"u8, out JsonElement value));
        Assert.AreEqual(42, value.GetInt32());
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void TryGetProperty_EscapedPropertyName_NoMatch()
    {
        string json = "{\"hello\\nworld\": 42}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        Assert.IsFalse(element.TryGetProperty("nomatch", out _));
    }

    #endregion

    #region Long escaped strings (ArrayPool + escape combined)

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_LongEscapedString_Matching()
    {
        // String with escapes and > 85 chars to trigger both ArrayPool AND escape paths
        string padding = new('a', 90);
        string json = $"\"{padding}\\n{padding}\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        string expected = padding + "\n" + padding;
        Assert.IsTrue(element.ValueEquals(expected.AsSpan()));
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void ValueEquals_LongEscapedString_NotMatching()
    {
        string padding = new('a', 90);
        string json = $"\"{padding}\\n{padding}\"";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        string notExpected = padding + "\t" + padding;
        Assert.IsFalse(element.ValueEquals(notExpected.AsSpan()));
    }

    #endregion

    #region Builder TryFormat with too-small destination

    [TestMethod]
    [TestCategory("coverage")]
    public void Builder_TryFormat_Utf8_True_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Span<byte> destination = stackalloc byte[1];
        bool result = root.TryFormat(destination, out int bytesWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Builder_TryFormat_Utf8_False_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Span<byte> destination = stackalloc byte[1];
        bool result = root.TryFormat(destination, out int bytesWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Builder_TryFormat_Utf8_Array_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Span<byte> destination = stackalloc byte[1];
        bool result = root.TryFormat(destination, out int bytesWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Builder_TryFormat_Utf8_String_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"key\":\"hello world\"}");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // Get the string value element
        JsonElement.Mutable value = root.GetProperty("key"u8);
        Span<byte> destination = stackalloc byte[1];
        bool result = value.TryFormat(destination, out int bytesWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Builder_TryFormat_Char_True_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Span<char> destination = stackalloc char[1];
        bool result = root.TryFormat(destination, out int charsWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Builder_TryFormat_Char_False_DestinationTooSmall()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Span<char> destination = stackalloc char[1];
        bool result = root.TryFormat(destination, out int charsWritten, default, null);

        Assert.IsFalse(result);
        Assert.AreEqual(0, charsWritten);
    }

    #endregion

    #region Builder TryGetLine (always returns false for builders)

    [TestMethod]
    [TestCategory("coverage")]
    public void Builder_TryGetLine_ByteMemory_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        IJsonDocument builderDoc = builder;

        Assert.IsFalse(builderDoc.TryGetLine(0, out ReadOnlyMemory<byte> line));
        Assert.AreEqual(0, line.Length);
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Builder_TryGetLine_String_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        IJsonDocument builderDoc = builder;

        Assert.IsFalse(builderDoc.TryGetLine(0, out string? line));
        Assert.IsNull(line);
    }

    #endregion

    #region Writer large-escape buffer paths (ArrayPool rent for escaped property/value names)

    [TestMethod]
    [TestCategory("coverage")]
    public void Writer_LargeEscapedPropertyName_Utf8_ExceedsStackallocThreshold()
    {
        // Property name with 50 control chars (U+0001): each escapes to \u0001 (6 bytes) → 300 > 256 threshold
        string escapedChars = string.Concat(Enumerable.Repeat("\\u0001", 50));
        string json = $"{{\"{escapedChars}\": \"hello\"}}";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        // Use WriteTo to exercise the writer's escaping paths
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            element.WriteTo(writer);
        }

        string result = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        StringAssert.Contains(result, "hello");
        StringAssert.Contains(result, "\\u0001");
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Writer_LargeEscapedValue_ExceedsStackallocThreshold()
    {
        // Value with 50 control chars (U+0001): each escapes to \u0001 (6 bytes) → 300 > 256 threshold
        string escapedChars = string.Concat(Enumerable.Repeat("\\u0001", 50));
        string json = $"{{\"key\": \"{escapedChars}\"}}";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            element.WriteTo(writer);
        }

        string result = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        StringAssert.Contains(result, "\\u0001");
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Writer_LargeEscapedPropertyAndValue_BothExceedThreshold()
    {
        // Both property name and value exceed the threshold
        string escapedProp = string.Concat(Enumerable.Repeat("\\u0002", 50));
        string escapedValue = string.Concat(Enumerable.Repeat("\\u0003", 50));
        string json = $"{{\"{escapedProp}\": \"{escapedValue}\"}}";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement element = doc.RootElement;

        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            element.WriteTo(writer);
        }

        string result = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        StringAssert.Contains(result, "\\u0002");
        StringAssert.Contains(result, "\\u0003");
    }

    [TestMethod]
    [TestCategory("coverage")]
    public void Writer_LargeEscapedPropertyName_Builder_ExceedsStackallocThreshold()
    {
        // Same but via builder serialization path
        string escapedChars = string.Concat(Enumerable.Repeat("\\u0001", 50));
        string json = $"{{\"{escapedChars}\": \"world\"}}";

        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            root.WriteTo(writer);
        }

        string result = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        StringAssert.Contains(result, "\\u0001");
        StringAssert.Contains(result, "world");
    }

    #endregion
}
