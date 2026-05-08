// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the property indexers on <see cref="JsonElement"/> and <see cref="JsonElement.Mutable"/>:
/// <c>this[string]</c>, <c>this[ReadOnlySpan&lt;char&gt;]</c>, and <c>this[ReadOnlySpan&lt;byte&gt;]</c>.
/// </summary>
[TestClass]
public class JsonElementPropertyIndexerTests
{
    private const string SampleObjectJson =
        """
        {"name":"Alice","age":30,"active":true}
        """;

    private const string NestedObjectJson =
        """
        {"outer":{"inner":"value"}}
        """;

    #region Immutable — this[string]

    [TestMethod]
    public void StringIndexer_ExistingStringProperty_ReturnsValue()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.AreEqual("Alice", element["name"].GetString());
    }

    [TestMethod]
    public void StringIndexer_ExistingNumberProperty_ReturnsValue()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.AreEqual(30, element["age"].GetInt32());
    }

    [TestMethod]
    public void StringIndexer_ExistingBooleanProperty_ReturnsValue()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.IsTrue(element["active"].GetBoolean());
    }

    [TestMethod]
    public void StringIndexer_MissingProperty_ReturnsDefault()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.AreEqual(JsonValueKind.Undefined, element["nonexistent"].ValueKind);
    }

    [TestMethod]
    public void StringIndexer_NestedAccess_ReturnsInnerValue()
    {
        var element = JsonElement.ParseValue(NestedObjectJson);
        Assert.AreEqual("value", element["outer"]["inner"].GetString());
    }

    #endregion

    #region Immutable — this[ReadOnlySpan<char>]

    [TestMethod]
    public void CharSpanIndexer_ExistingProperty_ReturnsValue()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.AreEqual("Alice", element["name".AsSpan()].GetString());
    }

    [TestMethod]
    public void CharSpanIndexer_MissingProperty_ReturnsDefault()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.AreEqual(JsonValueKind.Undefined, element["nonexistent".AsSpan()].ValueKind);
    }

    [TestMethod]
    public void CharSpanIndexer_NestedAccess_ReturnsInnerValue()
    {
        var element = JsonElement.ParseValue(NestedObjectJson);
        Assert.AreEqual("value", element["outer".AsSpan()]["inner".AsSpan()].GetString());
    }

    #endregion

    #region Immutable — this[ReadOnlySpan<byte>]

    [TestMethod]
    public void Utf8Indexer_ExistingProperty_ReturnsValue()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.AreEqual("Alice", element["name"u8].GetString());
    }

    [TestMethod]
    public void Utf8Indexer_MissingProperty_ReturnsDefault()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.AreEqual(JsonValueKind.Undefined, element["nonexistent"u8].ValueKind);
    }

    [TestMethod]
    public void Utf8Indexer_NestedAccess_ReturnsInnerValue()
    {
        var element = JsonElement.ParseValue(NestedObjectJson);
        Assert.AreEqual("value", element["outer"u8]["inner"u8].GetString());
    }

    #endregion

    #region Mutable — this[string]

    [TestMethod]
    public void Mutable_StringIndexer_ExistingProperty_ReturnsValue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.AreEqual("Alice", root["name"].GetString());
        Assert.AreEqual(30, root["age"].GetInt32());
        Assert.IsTrue(root["active"].GetBoolean());
    }

    [TestMethod]
    public void Mutable_StringIndexer_MissingProperty_ReturnsDefault()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Undefined, root["nonexistent"].ValueKind);
    }

    [TestMethod]
    public void Mutable_StringIndexer_NestedAccess_ReturnsInnerValue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(NestedObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.AreEqual("value", root["outer"]["inner"].GetString());
    }

    #endregion

    #region Mutable — this[ReadOnlySpan<char>]

    [TestMethod]
    public void Mutable_CharSpanIndexer_ExistingProperty_ReturnsValue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.AreEqual("Alice", root["name".AsSpan()].GetString());
    }

    [TestMethod]
    public void Mutable_CharSpanIndexer_MissingProperty_ReturnsDefault()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Undefined, root["nonexistent".AsSpan()].ValueKind);
    }

    #endregion

    #region Mutable — this[ReadOnlySpan<byte>]

    [TestMethod]
    public void Mutable_Utf8Indexer_ExistingProperty_ReturnsValue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.AreEqual("Alice", root["name"u8].GetString());
    }

    [TestMethod]
    public void Mutable_Utf8Indexer_MissingProperty_ReturnsDefault()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Undefined, root["nonexistent"u8].ValueKind);
    }

    #endregion
}
