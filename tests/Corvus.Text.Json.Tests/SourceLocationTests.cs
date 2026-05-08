// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class SourceLocationTests
{
    [TestMethod]
    public void TryGetLineAndOffset_SingleLineObject_ReturnsLine1()
    {
        string json = """{"name":"value"}""";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.IsTrue(root.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.AreEqual(1, line);
        Assert.AreEqual(1, charOffset);
    }

    [TestMethod]
    public void TryGetLineAndOffset_MultiLineObject_PropertyOnCorrectLine()
    {
        string json = "{\n  \"name\": \"value\",\n  \"age\": 42\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // "age" property value is on line 3
        JsonElement ageElement = root.GetProperty("age"u8);
        Assert.IsTrue(ageElement.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.AreEqual(3, line);
    }

    [TestMethod]
    public void TryGetLineAndOffset_MultiLineObject_RootIsLine1()
    {
        string json = "{\n  \"name\": \"value\"\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.IsTrue(root.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.AreEqual(1, line);
        Assert.AreEqual(1, charOffset);
    }

    [TestMethod]
    public void TryGetLineAndOffset_NestedObject_DeepPropertyLine()
    {
        string json = "{\n  \"outer\": {\n    \"inner\": {\n      \"deep\": true\n    }\n  }\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        JsonElement deepElement = root.GetProperty("outer"u8).GetProperty("inner"u8).GetProperty("deep"u8);
        Assert.IsTrue(deepElement.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.AreEqual(4, line);
    }

    [TestMethod]
    public void TryGetLineAndOffset_Array_ElementsOnSeparateLines()
    {
        string json = "[\n  1,\n  2,\n  3\n]";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        int index = 0;
        foreach (JsonElement item in root.EnumerateArray())
        {
            Assert.IsTrue(item.TryGetLineAndOffset(out int line, out _));
            Assert.AreEqual(index + 2, line); // items on lines 2, 3, 4
            index++;
        }

        Assert.AreEqual(3, index);
    }

    [TestMethod]
    public void TryGetLineAndOffset_CrLfLineEndings()
    {
        string json = "{\r\n  \"name\": \"value\",\r\n  \"age\": 42\r\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        JsonElement ageElement = root.GetProperty("age"u8);
        Assert.IsTrue(ageElement.TryGetLineAndOffset(out int line, out _));
        Assert.AreEqual(3, line);
    }

    [TestMethod]
    public void TryGetLineAndOffset_CharOffset_AccountsForIndentation()
    {
        string json = "{\n  \"name\": \"value\"\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // "name" property value should be after '  "name": '
        JsonElement nameElement = root.GetProperty("name"u8);
        Assert.IsTrue(nameElement.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.AreEqual(2, line);
        // The char offset is 1-based. The value starts after '  "name": '
        Assert.IsTrue(charOffset > 1);
    }

    [TestMethod]
    public void TryGetLine_ReturnsCorrectLineBytes()
    {
        string json = "{\n  \"name\": \"value\",\n  \"age\": 42\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // Verify the byte overload returns data for valid line numbers
        Assert.IsTrue(root.TryGetLine(1, out ReadOnlyMemory<byte> line1));
        Assert.IsTrue(line1.Length > 0);

        Assert.IsTrue(root.TryGetLine(2, out ReadOnlyMemory<byte> line2));
        Assert.IsTrue(line2.Length > 0);

        Assert.IsTrue(root.TryGetLine(3, out ReadOnlyMemory<byte> line3));
        Assert.IsTrue(line3.Length > 0);

        Assert.IsTrue(root.TryGetLine(4, out ReadOnlyMemory<byte> line4));
        Assert.IsTrue(line4.Length > 0);

        // And returns false for out-of-range
        Assert.IsFalse(root.TryGetLine(5, out ReadOnlyMemory<byte> _));
    }

    [TestMethod]
    public void TryGetLine_ReturnsCorrectLineString()
    {
        string json = "{\n  \"name\": \"value\",\n  \"age\": 42\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.IsTrue(root.TryGetLine(1, out string? line1));
        Assert.AreEqual("{", line1);

        Assert.IsTrue(root.TryGetLine(2, out string? line2));
        Assert.AreEqual("  \"name\": \"value\",", line2);

        Assert.IsTrue(root.TryGetLine(3, out string? line3));
        Assert.AreEqual("  \"age\": 42", line3);
    }

    [TestMethod]
    public void TryGetLine_CrLf_StripsCarriageReturn()
    {
        string json = "{\r\n  \"a\": 1\r\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.IsTrue(root.TryGetLine(1, out string? line1));
        Assert.AreEqual("{", line1);

        Assert.IsTrue(root.TryGetLine(2, out string? line2));
        Assert.AreEqual("  \"a\": 1", line2);
    }

    [TestMethod]
    public void TryGetLine_OutOfRange_ReturnsFalse()
    {
        string json = "{\"a\": 1}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.IsFalse(root.TryGetLine(0, out string? _));
        Assert.IsFalse(root.TryGetLine(2, out string? _)); // single-line document
        Assert.IsFalse(root.TryGetLine(-1, out string? _));
    }

    [TestMethod]
    public void TryGetLine_SingleLine_ReturnsEntireDocument()
    {
        string json = """{"key":"val"}""";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.IsTrue(root.TryGetLine(1, out string? line1));
        Assert.AreEqual("""{"key":"val"}""", line1);

        Assert.IsFalse(root.TryGetLine(2, out string? _));
    }

    [TestMethod]
    public void Utf8JsonPointer_TryGetLineAndOffset_ResolvesPointer()
    {
        string json = "{\n  \"name\": \"value\",\n  \"age\": 42\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.IsTrue(Utf8JsonPointer.TryCreateJsonPointer("/age"u8, out Utf8JsonPointer pointer));
        Assert.IsTrue(pointer.TryGetLineAndOffset(in root, out int line, out int charOffset, out long lineByteOffset));
        Assert.AreEqual(3, line);
        Assert.IsTrue(charOffset >= 1);
        Assert.IsTrue(lineByteOffset > 0);
    }

    [TestMethod]
    public void Utf8JsonPointer_TryGetLineAndOffset_ArrayElement()
    {
        string json = "{\n  \"items\": [\n    1,\n    2,\n    3\n  ]\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // Pointer /items/1 should resolve to the element "2" on line 4
        Assert.IsTrue(Utf8JsonPointer.TryCreateJsonPointer("/items/1"u8, out Utf8JsonPointer pointer));
        Assert.IsTrue(pointer.TryGetLineAndOffset(in root, out int line, out _, out _));
        Assert.AreEqual(4, line);
    }

    [TestMethod]
    public void Utf8JsonPointer_TryGetLineAndOffset_InvalidPointer_ReturnsFalse()
    {
        string json = """{"name":"value"}""";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.IsTrue(Utf8JsonPointer.TryCreateJsonPointer("/nonexistent"u8, out Utf8JsonPointer pointer));
        Assert.IsFalse(pointer.TryGetLineAndOffset(in root, out _, out _, out _));
    }

    [TestMethod]
    public void TryGetLineAndOffset_WithLineByteOffset_ReturnsCorrectValues()
    {
        string json = "{\n  \"name\": \"value\"\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // Root element on line 1
        Assert.IsTrue(root.TryGetLineAndOffset(out int line, out int charOffset, out long lineByteOffset));
        Assert.AreEqual(1, line);
        Assert.AreEqual(1, charOffset);
        Assert.AreEqual(0, lineByteOffset);
    }

    [TestMethod]
    public void TryGetLineAndOffset_SecondLine_CorrectByteOffset()
    {
        string json = "{\n  \"name\": \"value\"\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        JsonElement nameElement = root.GetProperty("name"u8);
        Assert.IsTrue(nameElement.TryGetLineAndOffset(out int line, out _, out long lineByteOffset));
        Assert.AreEqual(2, line);
        // lineByteOffset should be right after the \n on line 1 — byte index 2 ('{' '\n')
        Assert.AreEqual(2, lineByteOffset);
    }

    [TestMethod]
    public void Utf8JsonPointer_TryResolve_InvalidPointer_ReturnsFalse()
    {
        string json = """{"name":"value"}""";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // "invalid" doesn't start with '/' so the pointer is structurally invalid
        Assert.IsFalse(Utf8JsonPointer.TryCreateJsonPointer("invalid"u8, out Utf8JsonPointer pointer));
        Assert.IsFalse(pointer.IsValid);
        Assert.IsFalse(pointer.TryResolve<JsonElement, JsonElement>(in root, out JsonElement value));
        Assert.AreEqual(default, value);
    }

    [TestMethod]
    public void Utf8JsonPointer_TryGetLineAndOffset_StructurallyInvalidPointer_ReturnsFalse()
    {
        string json = """{"name":"value"}""";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // "no-slash" doesn't start with '/' so the pointer is structurally invalid
        Assert.IsFalse(Utf8JsonPointer.TryCreateJsonPointer("no-slash"u8, out Utf8JsonPointer pointer));
        Assert.IsFalse(pointer.IsValid);
        Assert.IsFalse(pointer.TryGetLineAndOffset(in root, out int line, out int charOffset, out long lineByteOffset));
        Assert.AreEqual(0, line);
        Assert.AreEqual(0, charOffset);
        Assert.AreEqual(0, lineByteOffset);
    }

    [TestMethod]
    public void DecodeSegment_ValidEscapes_DecodesCorrectly()
    {
        // ~0 decodes to ~, ~1 decodes to /
        ReadOnlySpan<byte> encoded = "foo~0bar~1baz"u8;
        Span<byte> decoded = stackalloc byte[encoded.Length];

        int written = Utf8JsonPointer.DecodeSegment(encoded, decoded);

        CollectionAssert.AreEqual("foo~bar/baz"u8.ToArray(), decoded.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void DecodeSegment_TildeAtEnd_Throws()
    {
        // ~ at end of segment should throw (incomplete escape sequence)
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            ReadOnlySpan<byte> encoded = "foo~"u8;
            byte[] decoded = new byte[encoded.Length];
            Utf8JsonPointer.DecodeSegment(encoded, decoded);
        });
    }

    [TestMethod]
    public void DecodeSegment_TildeFollowedByInvalidDigit_Throws()
    {
        // ~2 is not a valid escape (only ~0 and ~1 are valid per RFC 6901)
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            ReadOnlySpan<byte> encoded = "foo~2bar"u8;
            byte[] decoded = new byte[encoded.Length];
            Utf8JsonPointer.DecodeSegment(encoded, decoded);
        });
    }
}
