// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

public static class SourceLocationTests
{
    [Fact]
    public static void TryGetLineAndOffset_SingleLineObject_ReturnsLine1()
    {
        string json = """{"name":"value"}""";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.Equal(1, line);
        Assert.Equal(1, charOffset);
    }

    [Fact]
    public static void TryGetLineAndOffset_MultiLineObject_PropertyOnCorrectLine()
    {
        string json = "{\n  \"name\": \"value\",\n  \"age\": 42\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // "age" property value is on line 3
        JsonElement ageElement = root.GetProperty("age"u8);
        Assert.True(ageElement.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.Equal(3, line);
    }

    [Fact]
    public static void TryGetLineAndOffset_MultiLineObject_RootIsLine1()
    {
        string json = "{\n  \"name\": \"value\"\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.Equal(1, line);
        Assert.Equal(1, charOffset);
    }

    [Fact]
    public static void TryGetLineAndOffset_NestedObject_DeepPropertyLine()
    {
        string json = "{\n  \"outer\": {\n    \"inner\": {\n      \"deep\": true\n    }\n  }\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        JsonElement deepElement = root.GetProperty("outer"u8).GetProperty("inner"u8).GetProperty("deep"u8);
        Assert.True(deepElement.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.Equal(4, line);
    }

    [Fact]
    public static void TryGetLineAndOffset_Array_ElementsOnSeparateLines()
    {
        string json = "[\n  1,\n  2,\n  3\n]";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        int index = 0;
        foreach (JsonElement item in root.EnumerateArray())
        {
            Assert.True(item.TryGetLineAndOffset(out int line, out _));
            Assert.Equal(index + 2, line); // items on lines 2, 3, 4
            index++;
        }

        Assert.Equal(3, index);
    }

    [Fact]
    public static void TryGetLineAndOffset_CrLfLineEndings()
    {
        string json = "{\r\n  \"name\": \"value\",\r\n  \"age\": 42\r\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        JsonElement ageElement = root.GetProperty("age"u8);
        Assert.True(ageElement.TryGetLineAndOffset(out int line, out _));
        Assert.Equal(3, line);
    }

    [Fact]
    public static void TryGetLineAndOffset_CharOffset_AccountsForIndentation()
    {
        string json = "{\n  \"name\": \"value\"\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // "name" property value should be after '  "name": '
        JsonElement nameElement = root.GetProperty("name"u8);
        Assert.True(nameElement.TryGetLineAndOffset(out int line, out int charOffset));
        Assert.Equal(2, line);
        // The char offset is 1-based. The value starts after '  "name": '
        Assert.True(charOffset > 1);
    }

    [Fact]
    public static void TryGetLine_ReturnsCorrectLineBytes()
    {
        string json = "{\n  \"name\": \"value\",\n  \"age\": 42\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // Verify the byte overload returns data for valid line numbers
        Assert.True(root.TryGetLine(1, out ReadOnlyMemory<byte> line1));
        Assert.True(line1.Length > 0);

        Assert.True(root.TryGetLine(2, out ReadOnlyMemory<byte> line2));
        Assert.True(line2.Length > 0);

        Assert.True(root.TryGetLine(3, out ReadOnlyMemory<byte> line3));
        Assert.True(line3.Length > 0);

        Assert.True(root.TryGetLine(4, out ReadOnlyMemory<byte> line4));
        Assert.True(line4.Length > 0);

        // And returns false for out-of-range
        Assert.False(root.TryGetLine(5, out ReadOnlyMemory<byte> _));
    }

    [Fact]
    public static void TryGetLine_ReturnsCorrectLineString()
    {
        string json = "{\n  \"name\": \"value\",\n  \"age\": 42\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetLine(1, out string? line1));
        Assert.Equal("{", line1);

        Assert.True(root.TryGetLine(2, out string? line2));
        Assert.Equal("  \"name\": \"value\",", line2);

        Assert.True(root.TryGetLine(3, out string? line3));
        Assert.Equal("  \"age\": 42", line3);
    }

    [Fact]
    public static void TryGetLine_CrLf_StripsCarriageReturn()
    {
        string json = "{\r\n  \"a\": 1\r\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetLine(1, out string? line1));
        Assert.Equal("{", line1);

        Assert.True(root.TryGetLine(2, out string? line2));
        Assert.Equal("  \"a\": 1", line2);
    }

    [Fact]
    public static void TryGetLine_OutOfRange_ReturnsFalse()
    {
        string json = "{\"a\": 1}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.False(root.TryGetLine(0, out string? _));
        Assert.False(root.TryGetLine(2, out string? _)); // single-line document
        Assert.False(root.TryGetLine(-1, out string? _));
    }

    [Fact]
    public static void TryGetLine_SingleLine_ReturnsEntireDocument()
    {
        string json = """{"key":"val"}""";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetLine(1, out string? line1));
        Assert.Equal("""{"key":"val"}""", line1);

        Assert.False(root.TryGetLine(2, out string? _));
    }

    [Fact]
    public static void Utf8JsonPointer_TryGetLineAndOffset_ResolvesPointer()
    {
        string json = "{\n  \"name\": \"value\",\n  \"age\": 42\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.True(Utf8JsonPointer.TryCreateJsonPointer("/age"u8, out Utf8JsonPointer pointer));
        Assert.True(pointer.TryGetLineAndOffset(in root, out int line, out int charOffset, out long lineByteOffset));
        Assert.Equal(3, line);
        Assert.True(charOffset >= 1);
        Assert.True(lineByteOffset > 0);
    }

    [Fact]
    public static void Utf8JsonPointer_TryGetLineAndOffset_ArrayElement()
    {
        string json = "{\n  \"items\": [\n    1,\n    2,\n    3\n  ]\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // Pointer /items/1 should resolve to the element "2" on line 4
        Assert.True(Utf8JsonPointer.TryCreateJsonPointer("/items/1"u8, out Utf8JsonPointer pointer));
        Assert.True(pointer.TryGetLineAndOffset(in root, out int line, out _, out _));
        Assert.Equal(4, line);
    }

    [Fact]
    public static void Utf8JsonPointer_TryGetLineAndOffset_InvalidPointer_ReturnsFalse()
    {
        string json = """{"name":"value"}""";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.True(Utf8JsonPointer.TryCreateJsonPointer("/nonexistent"u8, out Utf8JsonPointer pointer));
        Assert.False(pointer.TryGetLineAndOffset(in root, out _, out _, out _));
    }

    [Fact]
    public static void TryGetLineAndOffset_WithLineByteOffset_ReturnsCorrectValues()
    {
        string json = "{\n  \"name\": \"value\"\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // Root element on line 1
        Assert.True(root.TryGetLineAndOffset(out int line, out int charOffset, out long lineByteOffset));
        Assert.Equal(1, line);
        Assert.Equal(1, charOffset);
        Assert.Equal(0, lineByteOffset);
    }

    [Fact]
    public static void TryGetLineAndOffset_SecondLine_CorrectByteOffset()
    {
        string json = "{\n  \"name\": \"value\"\n}";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        JsonElement nameElement = root.GetProperty("name"u8);
        Assert.True(nameElement.TryGetLineAndOffset(out int line, out _, out long lineByteOffset));
        Assert.Equal(2, line);
        // lineByteOffset should be right after the \n on line 1 — byte index 2 ('{' '\n')
        Assert.Equal(2, lineByteOffset);
    }
}