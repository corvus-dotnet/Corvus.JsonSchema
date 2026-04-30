// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated read-only indexers on array-only and object-only types.
/// Covers: array this[int] (immutable + mutable), object this[string] / this[ReadOnlySpan&lt;byte&gt;] /
/// this[ReadOnlySpan&lt;char&gt;] (immutable + mutable), and missing-property-returns-default.
/// </summary>
public class GeneratedIndexerTests
{
    #region Array immutable indexer — this[int]

    [Fact]
    public void ArrayOfItems_ImmutableIndexer_ReturnsTypedElements()
    {
        using var doc =
            ParsedJsonDocument<ArrayOfItems>.Parse("""[{"id":1,"label":"first"},{"id":2,"label":"second"},{"id":3}]""");

        ArrayOfItems root = doc.RootElement;

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, (int)root[0].Id);
        Assert.Equal("first", (string)root[0].Label);
        Assert.Equal(2, (int)root[1].Id);
        Assert.Equal("second", (string)root[1].Label);
        Assert.Equal(3, (int)root[2].Id);
    }

    [Fact]
    public void AllOfArrayWithItems_ImmutableIndexer_ReturnsStringElements()
    {
        using var doc =
            ParsedJsonDocument<AllOfArrayWithItems>.Parse("""["hello","world","test"]""");

        AllOfArrayWithItems root = doc.RootElement;

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("hello", root[0].GetString());
        Assert.Equal("world", root[1].GetString());
        Assert.Equal("test", root[2].GetString());
    }

    [Fact]
    public void RefArrayWithItems_ImmutableIndexer_ReturnsStringElements()
    {
        using var doc =
            ParsedJsonDocument<RefArrayWithItems>.Parse("""["alpha","beta"]""");

        RefArrayWithItems root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("alpha", root[0].GetString());
        Assert.Equal("beta", root[1].GetString());
    }

    #endregion

    #region Array mutable indexer — this[int]

    [Fact]
    public void ArrayOfItems_MutableIndexer_ReturnsTypedMutableElements()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<ArrayOfItems>.Parse("""[{"id":10,"label":"a"},{"id":20,"label":"b"}]""");
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(10, (int)root[0].Id);
        Assert.Equal("a", (string)root[0].Label);
        Assert.Equal(20, (int)root[1].Id);
        Assert.Equal("b", (string)root[1].Label);
    }

    [Fact]
    public void AllOfArrayWithItems_MutableIndexer_ReturnsStringMutableElements()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfArrayWithItems>.Parse("""["foo","bar"]""");
        using JsonDocumentBuilder<AllOfArrayWithItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfArrayWithItems.Mutable root = builder.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("foo", root[0].GetString());
        Assert.Equal("bar", root[1].GetString());
    }

    [Fact]
    public void RefArrayWithItems_MutableIndexer_ReturnsStringMutableElements()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<RefArrayWithItems>.Parse("""["x","y","z"]""");
        using JsonDocumentBuilder<RefArrayWithItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        RefArrayWithItems.Mutable root = builder.RootElement;

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("x", root[0].GetString());
        Assert.Equal("y", root[1].GetString());
        Assert.Equal("z", root[2].GetString());
    }

    #endregion

    #region Object immutable indexer — this[string]

    [Fact]
    public void AllOfObjectWithProperties_StringIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice","age":30,"email":"a@b.com"}""");

        AllOfObjectWithProperties root = doc.RootElement;

        Assert.Equal("Alice", root["name"].GetString());
        Assert.Equal(30, root["age"].GetInt32());
        Assert.Equal("a@b.com", root["email"].GetString());
    }

    [Fact]
    public void RefObjectWithProperties_StringIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Bob","age":25,"email":"bob@test.com"}""");

        RefObjectWithProperties root = doc.RootElement;

        Assert.Equal("Bob", root["name"].GetString());
        Assert.Equal(25, root["age"].GetInt32());
        Assert.Equal("bob@test.com", root["email"].GetString());
    }

    [Fact]
    public void NestedObject_StringIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"123 Main","city":"Springfield","zip":"62701"},"notes":"test"}""");

        NestedObject root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root["address"].ValueKind);
        Assert.Equal("test", root["notes"].GetString());
    }

    [Fact]
    public void AllOfObjectWithProperties_StringIndexer_MissingProperty_ReturnsDefault()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice"}""");

        AllOfObjectWithProperties root = doc.RootElement;

        JsonElement missing = root["nonexistent"];
        Assert.Equal(JsonValueKind.Undefined, missing.ValueKind);
    }

    #endregion

    #region Object immutable indexer — this[ReadOnlySpan<byte>] (UTF-8)

    [Fact]
    public void AllOfObjectWithProperties_Utf8Indexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice","age":30}""");

        AllOfObjectWithProperties root = doc.RootElement;

        Assert.Equal("Alice", root["name"u8].GetString());
        Assert.Equal(30, root["age"u8].GetInt32());
    }

    [Fact]
    public void RefObjectWithProperties_Utf8Indexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Carol","email":"c@d.com"}""");

        RefObjectWithProperties root = doc.RootElement;

        Assert.Equal("Carol", root["name"u8].GetString());
        Assert.Equal("c@d.com", root["email"u8].GetString());
    }

    [Fact]
    public void NestedObject_Utf8Indexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"Elm St"},"notes":"info"}""");

        NestedObject root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root["address"u8].ValueKind);
        Assert.Equal("info", root["notes"u8].GetString());
    }

    [Fact]
    public void AllOfObjectWithProperties_Utf8Indexer_MissingProperty_ReturnsDefault()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice"}""");

        AllOfObjectWithProperties root = doc.RootElement;

        JsonElement missing = root["nonexistent"u8];
        Assert.Equal(JsonValueKind.Undefined, missing.ValueKind);
    }

    #endregion

    #region Object immutable indexer — this[ReadOnlySpan<char>]

    [Fact]
    public void AllOfObjectWithProperties_CharSpanIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice","age":30}""");

        AllOfObjectWithProperties root = doc.RootElement;

        Assert.Equal("Alice", root["name".AsSpan()].GetString());
        Assert.Equal(30, root["age".AsSpan()].GetInt32());
    }

    [Fact]
    public void RefObjectWithProperties_CharSpanIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Dave","email":"d@e.com"}""");

        RefObjectWithProperties root = doc.RootElement;

        Assert.Equal("Dave", root["name".AsSpan()].GetString());
        Assert.Equal("d@e.com", root["email".AsSpan()].GetString());
    }

    [Fact]
    public void NestedObject_CharSpanIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"Oak Ave"},"notes":"memo"}""");

        NestedObject root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root["address".AsSpan()].ValueKind);
        Assert.Equal("memo", root["notes".AsSpan()].GetString());
    }

    [Fact]
    public void AllOfObjectWithProperties_CharSpanIndexer_MissingProperty_ReturnsDefault()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice"}""");

        AllOfObjectWithProperties root = doc.RootElement;

        JsonElement missing = root["nonexistent".AsSpan()];
        Assert.Equal(JsonValueKind.Undefined, missing.ValueKind);
    }

    #endregion

    #region Object mutable indexer — all three overloads

    [Fact]
    public void AllOfObjectWithProperties_MutableStringIndexer_ReturnsPropertyValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Eve","age":40,"email":"eve@f.com"}""");
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfObjectWithProperties.Mutable root = builder.RootElement;

        Assert.Equal("Eve", root["name"].GetString());
        Assert.Equal(40, root["age"].GetInt32());
        Assert.Equal("eve@f.com", root["email"].GetString());
    }

    [Fact]
    public void AllOfObjectWithProperties_MutableUtf8Indexer_ReturnsPropertyValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Frank","age":50}""");
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfObjectWithProperties.Mutable root = builder.RootElement;

        Assert.Equal("Frank", root["name"u8].GetString());
        Assert.Equal(50, root["age"u8].GetInt32());
    }

    [Fact]
    public void AllOfObjectWithProperties_MutableCharSpanIndexer_ReturnsPropertyValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Grace","age":60}""");
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfObjectWithProperties.Mutable root = builder.RootElement;

        Assert.Equal("Grace", root["name".AsSpan()].GetString());
        Assert.Equal(60, root["age".AsSpan()].GetInt32());
    }

    [Fact]
    public void AllOfObjectWithProperties_MutableStringIndexer_MissingProperty_ReturnsDefault()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice"}""");
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfObjectWithProperties.Mutable root = builder.RootElement;

        JsonElement.Mutable missing = root["nonexistent"];
        Assert.Equal(JsonValueKind.Undefined, missing.ValueKind);
    }

    [Fact]
    public void RefObjectWithProperties_MutableStringIndexer_ReturnsPropertyValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Hank","age":35,"email":"h@i.com"}""");
        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        RefObjectWithProperties.Mutable root = builder.RootElement;

        Assert.Equal("Hank", root["name"].GetString());
        Assert.Equal(35, root["age"].GetInt32());
        Assert.Equal("h@i.com", root["email"].GetString());
    }

    [Fact]
    public void NestedObject_MutableStringIndexer_ReturnsPropertyValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"Pine Rd","city":"Salem"},"notes":"ok"}""");
        using JsonDocumentBuilder<NestedObject.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        NestedObject.Mutable root = builder.RootElement;

        Assert.Equal(JsonValueKind.Object, root["address"].ValueKind);
        Assert.Equal("ok", root["notes"].GetString());
    }

    #endregion
}