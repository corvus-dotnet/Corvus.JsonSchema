// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

public static class UnescapedUtf16JsonStringTests
{
    #region GetUtf16String on JsonElement (immutable)

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("\"\"", "")]
    [InlineData("\"hello world\"", "hello world")]
    public static void GetUtf16String_SimpleString_ReturnsExpected(string json, string expected)
    {
        var element = JsonElement.ParseValue(json);
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.Equal(expected, result.Span.ToString());
    }

    [Theory]
    [InlineData("\"hello\\nworld\"", "hello\nworld")]
    [InlineData("\"hello\\tworld\"", "hello\tworld")]
    [InlineData("\"hello\\\\world\"", "hello\\world")]
    [InlineData("\"hello\\\"world\"", "hello\"world")]
    [InlineData("\"hello\\/world\"", "hello/world")]
    [InlineData("\"hello\\u0041world\"", "helloAworld")]
    public static void GetUtf16String_EscapedString_ReturnsUnescaped(string json, string expected)
    {
        var element = JsonElement.ParseValue(json);
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.Equal(expected, result.Span.ToString());
    }

    [Fact]
    public static void GetUtf16String_UnicodeContent_ReturnsExpected()
    {
        var element = JsonElement.ParseValue("\"caf\\u00E9\"");
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.Equal("café", result.Span.ToString());
    }

    [Fact]
    public static void GetUtf16String_NonStringElement_ThrowsInvalidOperationException()
    {
        var element = JsonElement.ParseValue("42");
        Assert.Throws<InvalidOperationException>(() =>
        {
            using UnescapedUtf16JsonString _ = element.GetUtf16String();
        });
    }

    [Fact]
    public static void GetUtf16String_SpanAndMemory_AreConsistent()
    {
        var element = JsonElement.ParseValue("\"test value\"");
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.Equal(result.Span.ToString(), result.Memory.Span.ToString());
    }

    #endregion

    #region GetUtf16String on JsonElement.Mutable

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("\"\"", "")]
    [InlineData("\"hello world\"", "hello world")]
    public static void GetUtf16String_Mutable_SimpleString_ReturnsExpected(string json, string expected)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.Equal(expected, result.Span.ToString());
    }

    [Theory]
    [InlineData("\"hello\\nworld\"", "hello\nworld")]
    [InlineData("\"hello\\tworld\"", "hello\tworld")]
    [InlineData("\"hello\\\\world\"", "hello\\world")]
    [InlineData("\"hello\\\"world\"", "hello\"world")]
    public static void GetUtf16String_Mutable_EscapedString_ReturnsUnescaped(string json, string expected)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.Equal(expected, result.Span.ToString());
    }

    [Fact]
    public static void GetUtf16String_Mutable_NonStringElement_ThrowsInvalidOperationException()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue("42"));
        JsonElement.Mutable element = doc.RootElement;

        Assert.Throws<InvalidOperationException>(() =>
        {
            using UnescapedUtf16JsonString _ = element.GetUtf16String();
        });
    }

    #endregion

    #region UnescapedUtf16JsonString struct behavior

    [Fact]
    public static void TakeOwnership_TransfersRentedArray()
    {
        var element = JsonElement.ParseValue("\"owned\"");
        UnescapedUtf16JsonString result = element.GetUtf16String();

        ReadOnlyMemory<char> memory = result.TakeOwnership(out char[]? rentedChars);
        Assert.Equal("owned", memory.Span.ToString());

        // After TakeOwnership, Dispose should be safe (no double-return).
        result.Dispose();

        // Clean up the rented array ourselves since we took ownership.
        if (rentedChars != null)
        {
            System.Buffers.ArrayPool<char>.Shared.Return(rentedChars);
        }
    }

    [Fact]
    public static void Dispose_IsIdempotent()
    {
        var element = JsonElement.ParseValue("\"dispose test\"");
        UnescapedUtf16JsonString result = element.GetUtf16String();
        result.Dispose();
        result.Dispose(); // Should not throw.
    }

    #endregion

    #region GetUtf16String matches GetString

    [Theory]
    [InlineData("\"hello\"")]
    [InlineData("\"\"")]
    [InlineData("\"hello\\nworld\"")]
    [InlineData("\"hello\\u0041world\"")]
    [InlineData("\"caf\\u00E9\"")]
    [InlineData("\"line1\\r\\nline2\"")]
    public static void GetUtf16String_MatchesGetString_Immutable(string json)
    {
        var element = JsonElement.ParseValue(json);
        string expected = element.GetString();
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.Equal(expected, result.Span.ToString());
    }

    [Theory]
    [InlineData("\"hello\"")]
    [InlineData("\"\"")]
    [InlineData("\"hello\\nworld\"")]
    [InlineData("\"hello\\u0041world\"")]
    [InlineData("\"caf\\u00E9\"")]
    public static void GetUtf16String_MatchesGetString_Mutable(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        string expected = element.GetString();
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.Equal(expected, result.Span.ToString());
    }

    #endregion

    #region Property access via GetUtf16String

    [Fact]
    public static void GetUtf16String_ObjectProperty_ReturnsExpected()
    {
        var element = JsonElement.ParseValue("{\"name\":\"hello\\nworld\"}");
        JsonElement nameElement = element.GetProperty("name");
        using UnescapedUtf16JsonString result = nameElement.GetUtf16String();
        Assert.Equal("hello\nworld", result.Span.ToString());
    }

    [Fact]
    public static void GetUtf16String_ArrayItem_ReturnsExpected()
    {
        var element = JsonElement.ParseValue("[\"alpha\",\"beta\\tgamma\"]");
        JsonElement item = element[1];
        using UnescapedUtf16JsonString result = item.GetUtf16String();
        Assert.Equal("beta\tgamma", result.Span.ToString());
    }

    #endregion
}