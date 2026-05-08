// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated read-only indexers on array-only and object-only types.
/// Covers: array this[int] (immutable + mutable), object this[string] / this[ReadOnlySpan&lt;byte&gt;] /
/// this[ReadOnlySpan&lt;char&gt;] (immutable + mutable), and missing-property-returns-default.
/// </summary>
[TestClass]
public class GeneratedIndexerTests
{
    #region Array immutable indexer — this[int]

    [TestMethod]
    public void ArrayOfItems_ImmutableIndexer_ReturnsTypedElements()
    {
        using var doc =
            ParsedJsonDocument<ArrayOfItems>.Parse("""[{"id":1,"label":"first"},{"id":2,"label":"second"},{"id":3}]""");

        ArrayOfItems root = doc.RootElement;

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, (int)root[0].Id);
        Assert.AreEqual("first", (string)root[0].Label);
        Assert.AreEqual(2, (int)root[1].Id);
        Assert.AreEqual("second", (string)root[1].Label);
        Assert.AreEqual(3, (int)root[2].Id);
    }

    [TestMethod]
    public void AllOfArrayWithItems_ImmutableIndexer_ReturnsStringElements()
    {
        using var doc =
            ParsedJsonDocument<AllOfArrayWithItems>.Parse("""["hello","world","test"]""");

        AllOfArrayWithItems root = doc.RootElement;

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].GetString());
        Assert.AreEqual("world", root[1].GetString());
        Assert.AreEqual("test", root[2].GetString());
    }

    [TestMethod]
    public void RefArrayWithItems_ImmutableIndexer_ReturnsStringElements()
    {
        using var doc =
            ParsedJsonDocument<RefArrayWithItems>.Parse("""["alpha","beta"]""");

        RefArrayWithItems root = doc.RootElement;

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("alpha", root[0].GetString());
        Assert.AreEqual("beta", root[1].GetString());
    }

    #endregion

    #region Array mutable indexer — this[int]

    [TestMethod]
    public void ArrayOfItems_MutableIndexer_ReturnsTypedMutableElements()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<ArrayOfItems>.Parse("""[{"id":10,"label":"a"},{"id":20,"label":"b"}]""");
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(10, (int)root[0].Id);
        Assert.AreEqual("a", (string)root[0].Label);
        Assert.AreEqual(20, (int)root[1].Id);
        Assert.AreEqual("b", (string)root[1].Label);
    }

    [TestMethod]
    public void AllOfArrayWithItems_MutableIndexer_ReturnsStringMutableElements()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfArrayWithItems>.Parse("""["foo","bar"]""");
        using JsonDocumentBuilder<AllOfArrayWithItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfArrayWithItems.Mutable root = builder.RootElement;

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("foo", root[0].GetString());
        Assert.AreEqual("bar", root[1].GetString());
    }

    [TestMethod]
    public void RefArrayWithItems_MutableIndexer_ReturnsStringMutableElements()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<RefArrayWithItems>.Parse("""["x","y","z"]""");
        using JsonDocumentBuilder<RefArrayWithItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        RefArrayWithItems.Mutable root = builder.RootElement;

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("x", root[0].GetString());
        Assert.AreEqual("y", root[1].GetString());
        Assert.AreEqual("z", root[2].GetString());
    }

    #endregion

    #region Object immutable indexer — this[string]

    [TestMethod]
    public void AllOfObjectWithProperties_StringIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice","age":30,"email":"a@b.com"}""");

        AllOfObjectWithProperties root = doc.RootElement;

        Assert.AreEqual("Alice", root["name"].GetString());
        Assert.AreEqual(30, root["age"].GetInt32());
        Assert.AreEqual("a@b.com", root["email"].GetString());
    }

    [TestMethod]
    public void RefObjectWithProperties_StringIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Bob","age":25,"email":"bob@test.com"}""");

        RefObjectWithProperties root = doc.RootElement;

        Assert.AreEqual("Bob", root["name"].GetString());
        Assert.AreEqual(25, root["age"].GetInt32());
        Assert.AreEqual("bob@test.com", root["email"].GetString());
    }

    [TestMethod]
    public void NestedObject_StringIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"123 Main","city":"Springfield","zip":"62701"},"notes":"test"}""");

        NestedObject root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root["address"].ValueKind);
        Assert.AreEqual("test", root["notes"].GetString());
    }

    [TestMethod]
    public void AllOfObjectWithProperties_StringIndexer_MissingProperty_ReturnsDefault()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice"}""");

        AllOfObjectWithProperties root = doc.RootElement;

        JsonElement missing = root["nonexistent"];
        Assert.AreEqual(JsonValueKind.Undefined, missing.ValueKind);
    }

    #endregion

    #region Object immutable indexer — this[ReadOnlySpan<byte>] (UTF-8)

    [TestMethod]
    public void AllOfObjectWithProperties_Utf8Indexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice","age":30}""");

        AllOfObjectWithProperties root = doc.RootElement;

        Assert.AreEqual("Alice", root["name"u8].GetString());
        Assert.AreEqual(30, root["age"u8].GetInt32());
    }

    [TestMethod]
    public void RefObjectWithProperties_Utf8Indexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Carol","email":"c@d.com"}""");

        RefObjectWithProperties root = doc.RootElement;

        Assert.AreEqual("Carol", root["name"u8].GetString());
        Assert.AreEqual("c@d.com", root["email"u8].GetString());
    }

    [TestMethod]
    public void NestedObject_Utf8Indexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"Elm St"},"notes":"info"}""");

        NestedObject root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root["address"u8].ValueKind);
        Assert.AreEqual("info", root["notes"u8].GetString());
    }

    [TestMethod]
    public void AllOfObjectWithProperties_Utf8Indexer_MissingProperty_ReturnsDefault()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice"}""");

        AllOfObjectWithProperties root = doc.RootElement;

        JsonElement missing = root["nonexistent"u8];
        Assert.AreEqual(JsonValueKind.Undefined, missing.ValueKind);
    }

    #endregion

    #region Object immutable indexer — this[ReadOnlySpan<char>]

    [TestMethod]
    public void AllOfObjectWithProperties_CharSpanIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice","age":30}""");

        AllOfObjectWithProperties root = doc.RootElement;

        Assert.AreEqual("Alice", root["name".AsSpan()].GetString());
        Assert.AreEqual(30, root["age".AsSpan()].GetInt32());
    }

    [TestMethod]
    public void RefObjectWithProperties_CharSpanIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Dave","email":"d@e.com"}""");

        RefObjectWithProperties root = doc.RootElement;

        Assert.AreEqual("Dave", root["name".AsSpan()].GetString());
        Assert.AreEqual("d@e.com", root["email".AsSpan()].GetString());
    }

    [TestMethod]
    public void NestedObject_CharSpanIndexer_ReturnsPropertyValues()
    {
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"Oak Ave"},"notes":"memo"}""");

        NestedObject root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root["address".AsSpan()].ValueKind);
        Assert.AreEqual("memo", root["notes".AsSpan()].GetString());
    }

    [TestMethod]
    public void AllOfObjectWithProperties_CharSpanIndexer_MissingProperty_ReturnsDefault()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice"}""");

        AllOfObjectWithProperties root = doc.RootElement;

        JsonElement missing = root["nonexistent".AsSpan()];
        Assert.AreEqual(JsonValueKind.Undefined, missing.ValueKind);
    }

    #endregion

    #region Object mutable indexer — all three overloads

    [TestMethod]
    public void AllOfObjectWithProperties_MutableStringIndexer_ReturnsPropertyValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Eve","age":40,"email":"eve@f.com"}""");
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfObjectWithProperties.Mutable root = builder.RootElement;

        Assert.AreEqual("Eve", root["name"].GetString());
        Assert.AreEqual(40, root["age"].GetInt32());
        Assert.AreEqual("eve@f.com", root["email"].GetString());
    }

    [TestMethod]
    public void AllOfObjectWithProperties_MutableUtf8Indexer_ReturnsPropertyValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Frank","age":50}""");
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfObjectWithProperties.Mutable root = builder.RootElement;

        Assert.AreEqual("Frank", root["name"u8].GetString());
        Assert.AreEqual(50, root["age"u8].GetInt32());
    }

    [TestMethod]
    public void AllOfObjectWithProperties_MutableCharSpanIndexer_ReturnsPropertyValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Grace","age":60}""");
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfObjectWithProperties.Mutable root = builder.RootElement;

        Assert.AreEqual("Grace", root["name".AsSpan()].GetString());
        Assert.AreEqual(60, root["age".AsSpan()].GetInt32());
    }

    [TestMethod]
    public void AllOfObjectWithProperties_MutableStringIndexer_MissingProperty_ReturnsDefault()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Alice"}""");
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfObjectWithProperties.Mutable root = builder.RootElement;

        JsonElement.Mutable missing = root["nonexistent"];
        Assert.AreEqual(JsonValueKind.Undefined, missing.ValueKind);
    }

    [TestMethod]
    public void RefObjectWithProperties_MutableStringIndexer_ReturnsPropertyValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Hank","age":35,"email":"h@i.com"}""");
        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        RefObjectWithProperties.Mutable root = builder.RootElement;

        Assert.AreEqual("Hank", root["name"].GetString());
        Assert.AreEqual(35, root["age"].GetInt32());
        Assert.AreEqual("h@i.com", root["email"].GetString());
    }

    [TestMethod]
    public void NestedObject_MutableStringIndexer_ReturnsPropertyValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"Pine Rd","city":"Salem"},"notes":"ok"}""");
        using JsonDocumentBuilder<NestedObject.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        NestedObject.Mutable root = builder.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root["address"].ValueKind);
        Assert.AreEqual("ok", root["notes"].GetString());
    }

    #endregion
}
