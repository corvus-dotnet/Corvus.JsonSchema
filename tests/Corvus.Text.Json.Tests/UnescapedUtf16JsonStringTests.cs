// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class UnescapedUtf16JsonStringTests
{
    #region GetUtf16String on JsonElement (immutable)

    [TestMethod]
    [DataRow("\"hello\"", "hello")]
    [DataRow("\"\"", "")]
    [DataRow("\"hello world\"", "hello world")]
    public void GetUtf16String_SimpleString_ReturnsExpected(string json, string expected)
    {
        var element = JsonElement.ParseValue(json);
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.AreEqual(expected, result.Span.ToString());
    }

    [TestMethod]
    [DataRow("\"hello\\nworld\"", "hello\nworld")]
    [DataRow("\"hello\\tworld\"", "hello\tworld")]
    [DataRow("\"hello\\\\world\"", "hello\\world")]
    [DataRow("\"hello\\\"world\"", "hello\"world")]
    [DataRow("\"hello\\/world\"", "hello/world")]
    [DataRow("\"hello\\u0041world\"", "helloAworld")]
    public void GetUtf16String_EscapedString_ReturnsUnescaped(string json, string expected)
    {
        var element = JsonElement.ParseValue(json);
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.AreEqual(expected, result.Span.ToString());
    }

    [TestMethod]
    public void GetUtf16String_UnicodeContent_ReturnsExpected()
    {
        var element = JsonElement.ParseValue("\"caf\\u00E9\"");
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.AreEqual("café", result.Span.ToString());
    }

    [TestMethod]
    public void GetUtf16String_NonStringElement_ThrowsInvalidOperationException()
    {
        var element = JsonElement.ParseValue("42");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using UnescapedUtf16JsonString _ = element.GetUtf16String();
        });
    }

    [TestMethod]
    public void GetUtf16String_SpanAndMemory_AreConsistent()
    {
        var element = JsonElement.ParseValue("\"test value\"");
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.AreEqual(result.Span.ToString(), result.Memory.Span.ToString());
    }

    #endregion

    #region GetUtf16String on JsonElement.Mutable

    [TestMethod]
    [DataRow("\"hello\"", "hello")]
    [DataRow("\"\"", "")]
    [DataRow("\"hello world\"", "hello world")]
    public void GetUtf16String_Mutable_SimpleString_ReturnsExpected(string json, string expected)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.AreEqual(expected, result.Span.ToString());
    }

    [TestMethod]
    [DataRow("\"hello\\nworld\"", "hello\nworld")]
    [DataRow("\"hello\\tworld\"", "hello\tworld")]
    [DataRow("\"hello\\\\world\"", "hello\\world")]
    [DataRow("\"hello\\\"world\"", "hello\"world")]
    public void GetUtf16String_Mutable_EscapedString_ReturnsUnescaped(string json, string expected)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.AreEqual(expected, result.Span.ToString());
    }

    [TestMethod]
    public void GetUtf16String_Mutable_NonStringElement_ThrowsInvalidOperationException()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue("42"));
        JsonElement.Mutable element = doc.RootElement;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using UnescapedUtf16JsonString _ = element.GetUtf16String();
        });
    }

    #endregion

    #region UnescapedUtf16JsonString struct behavior

    [TestMethod]
    public void TakeOwnership_TransfersRentedArray()
    {
        var element = JsonElement.ParseValue("\"owned\"");
        UnescapedUtf16JsonString result = element.GetUtf16String();

        ReadOnlyMemory<char> memory = result.TakeOwnership(out char[]? rentedChars);
        Assert.AreEqual("owned", memory.Span.ToString());

        // After TakeOwnership, Dispose should be safe (no double-return).
        result.Dispose();

        // Clean up the rented array ourselves since we took ownership.
        if (rentedChars != null)
        {
            System.Buffers.ArrayPool<char>.Shared.Return(rentedChars);
        }
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        var element = JsonElement.ParseValue("\"dispose test\"");
        UnescapedUtf16JsonString result = element.GetUtf16String();
        result.Dispose();
        result.Dispose(); // Should not throw.
    }

    #endregion

    #region GetUtf16String matches GetString

    [TestMethod]
    [DataRow("\"hello\"")]
    [DataRow("\"\"")]
    [DataRow("\"hello\\nworld\"")]
    [DataRow("\"hello\\u0041world\"")]
    [DataRow("\"caf\\u00E9\"")]
    [DataRow("\"line1\\r\\nline2\"")]
    public void GetUtf16String_MatchesGetString_Immutable(string json)
    {
        var element = JsonElement.ParseValue(json);
        string expected = element.GetString();
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.AreEqual(expected, result.Span.ToString());
    }

    [TestMethod]
    [DataRow("\"hello\"")]
    [DataRow("\"\"")]
    [DataRow("\"hello\\nworld\"")]
    [DataRow("\"hello\\u0041world\"")]
    [DataRow("\"caf\\u00E9\"")]
    public void GetUtf16String_MatchesGetString_Mutable(string json)
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable element = doc.RootElement;

        string expected = element.GetString();
        using UnescapedUtf16JsonString result = element.GetUtf16String();
        Assert.AreEqual(expected, result.Span.ToString());
    }

    #endregion

    #region Property access via GetUtf16String

    [TestMethod]
    public void GetUtf16String_ObjectProperty_ReturnsExpected()
    {
        var element = JsonElement.ParseValue("{\"name\":\"hello\\nworld\"}");
        JsonElement nameElement = element.GetProperty("name");
        using UnescapedUtf16JsonString result = nameElement.GetUtf16String();
        Assert.AreEqual("hello\nworld", result.Span.ToString());
    }

    [TestMethod]
    public void GetUtf16String_ArrayItem_ReturnsExpected()
    {
        var element = JsonElement.ParseValue("[\"alpha\",\"beta\\tgamma\"]");
        JsonElement item = element[1];
        using UnescapedUtf16JsonString result = item.GetUtf16String();
        Assert.AreEqual("beta\tgamma", result.Span.ToString());
    }

    #endregion
}
