// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the property indexers on <see cref="JsonElement"/> and <see cref="JsonElement.Mutable"/>:
/// <c>this[string]</c>, <c>this[ReadOnlySpan&lt;char&gt;]</c>, and <c>this[ReadOnlySpan&lt;byte&gt;]</c>.
/// </summary>
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

    [Fact]
    public void StringIndexer_ExistingStringProperty_ReturnsValue()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.Equal("Alice", element["name"].GetString());
    }

    [Fact]
    public void StringIndexer_ExistingNumberProperty_ReturnsValue()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.Equal(30, element["age"].GetInt32());
    }

    [Fact]
    public void StringIndexer_ExistingBooleanProperty_ReturnsValue()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.True(element["active"].GetBoolean());
    }

    [Fact]
    public void StringIndexer_MissingProperty_ReturnsDefault()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.Equal(JsonValueKind.Undefined, element["nonexistent"].ValueKind);
    }

    [Fact]
    public void StringIndexer_NestedAccess_ReturnsInnerValue()
    {
        var element = JsonElement.ParseValue(NestedObjectJson);
        Assert.Equal("value", element["outer"]["inner"].GetString());
    }

    #endregion

    #region Immutable — this[ReadOnlySpan<char>]

    [Fact]
    public void CharSpanIndexer_ExistingProperty_ReturnsValue()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.Equal("Alice", element["name".AsSpan()].GetString());
    }

    [Fact]
    public void CharSpanIndexer_MissingProperty_ReturnsDefault()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.Equal(JsonValueKind.Undefined, element["nonexistent".AsSpan()].ValueKind);
    }

    [Fact]
    public void CharSpanIndexer_NestedAccess_ReturnsInnerValue()
    {
        var element = JsonElement.ParseValue(NestedObjectJson);
        Assert.Equal("value", element["outer".AsSpan()]["inner".AsSpan()].GetString());
    }

    #endregion

    #region Immutable — this[ReadOnlySpan<byte>]

    [Fact]
    public void Utf8Indexer_ExistingProperty_ReturnsValue()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.Equal("Alice", element["name"u8].GetString());
    }

    [Fact]
    public void Utf8Indexer_MissingProperty_ReturnsDefault()
    {
        var element = JsonElement.ParseValue(SampleObjectJson);
        Assert.Equal(JsonValueKind.Undefined, element["nonexistent"u8].ValueKind);
    }

    [Fact]
    public void Utf8Indexer_NestedAccess_ReturnsInnerValue()
    {
        var element = JsonElement.ParseValue(NestedObjectJson);
        Assert.Equal("value", element["outer"u8]["inner"u8].GetString());
    }

    #endregion

    #region Mutable — this[string]

    [Fact]
    public void Mutable_StringIndexer_ExistingProperty_ReturnsValue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.Equal("Alice", root["name"].GetString());
        Assert.Equal(30, root["age"].GetInt32());
        Assert.True(root["active"].GetBoolean());
    }

    [Fact]
    public void Mutable_StringIndexer_MissingProperty_ReturnsDefault()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.Equal(JsonValueKind.Undefined, root["nonexistent"].ValueKind);
    }

    [Fact]
    public void Mutable_StringIndexer_NestedAccess_ReturnsInnerValue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(NestedObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.Equal("value", root["outer"]["inner"].GetString());
    }

    #endregion

    #region Mutable — this[ReadOnlySpan<char>]

    [Fact]
    public void Mutable_CharSpanIndexer_ExistingProperty_ReturnsValue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.Equal("Alice", root["name".AsSpan()].GetString());
    }

    [Fact]
    public void Mutable_CharSpanIndexer_MissingProperty_ReturnsDefault()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.Equal(JsonValueKind.Undefined, root["nonexistent".AsSpan()].ValueKind);
    }

    #endregion

    #region Mutable — this[ReadOnlySpan<byte>]

    [Fact]
    public void Mutable_Utf8Indexer_ExistingProperty_ReturnsValue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.Equal("Alice", root["name"u8].GetString());
    }

    [Fact]
    public void Mutable_Utf8Indexer_MissingProperty_ReturnsDefault()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(SampleObjectJson));
        JsonElement.Mutable root = doc.RootElement;

        Assert.Equal(JsonValueKind.Undefined, root["nonexistent"u8].ValueKind);
    }

    #endregion
}