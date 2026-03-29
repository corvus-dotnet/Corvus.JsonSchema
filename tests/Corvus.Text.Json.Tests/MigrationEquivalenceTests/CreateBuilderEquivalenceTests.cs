// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Xunit;

using V5 = MigrationModels.V5;

/// <summary>
/// Tests for the empty <c>CreateBuilder()</c>, <c>CreateArrayBuilder()</c>,
/// and <c>CreateObjectBuilder()</c> factory methods emitted on generated types
/// and on <see cref="Corvus.Text.Json.JsonElement.Mutable"/>.
/// </summary>
/// <remarks>
/// <para>
/// Generated types whose implied core type is <b>array-only</b> (and are not tuples) get a static
/// <c>CreateBuilder(workspace)</c> that initializes an empty JSON array.
/// </para>
/// <para>
/// Generated types whose implied core type is <b>object-only</b> with no required properties
/// get a static <c>CreateBuilder(workspace)</c> that initializes an empty JSON object.
/// </para>
/// <para>
/// Tuple types and object types with required properties do <em>not</em> get an empty
/// <c>CreateBuilder</c>, as an empty instance would not be valid.
/// </para>
/// <para>
/// <see cref="Corvus.Text.Json.JsonElement.Mutable"/>, which can be any JSON type, gets both
/// <c>CreateArrayBuilder(workspace)</c> and <c>CreateObjectBuilder(workspace)</c>.
/// </para>
/// </remarks>
public class CreateBuilderEquivalenceTests
{
    #region Array types — CreateBuilder creates empty array

    [Fact]
    public void ItemArray_CreateBuilder_ReturnsEmptyArray()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder =
            V5.MigrationItemArray.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        Assert.Equal(Corvus.Text.Json.JsonValueKind.Array, root.ValueKind);
        Assert.Equal(0, root.GetArrayLength());
    }

    [Fact]
    public void ItemArray_CreateBuilder_ThenAddItems()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder =
            V5.MigrationItemArray.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        root.AddItem(V5.MigrationItemArray.RequiredId.Build(
            (ref b) => b.Create(id: 1, label: "first")));
        root.AddItem(V5.MigrationItemArray.RequiredId.Build(
            (ref b) => b.Create(id: 2, label: "second")));

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(1, (int)root[0].Id);
        Assert.Equal(2, (int)root[1].Id);
    }

    [Fact]
    public void ItemArray_CreateBuilder_RoundTrip()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder =
            V5.MigrationItemArray.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        root.AddItem(V5.MigrationItemArray.RequiredId.Build(
            (ref b) => b.Create(id: 1, label: "first")));

        string json = root.ToString();

        using var reparsed =
            ParsedJsonDocument<V5.MigrationItemArray>.Parse(json);

        Assert.Equal(1, reparsed.RootElement.GetArrayLength());
        Assert.Equal(1, (int)reparsed.RootElement[0].Id);
    }

    [Fact]
    public void IntVector_CreateBuilder_ReturnsEmptyArray()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationIntVector.Mutable> builder =
            V5.MigrationIntVector.CreateBuilder(workspace);
        V5.MigrationIntVector.Mutable root = builder.RootElement;

        Assert.Equal(Corvus.Text.Json.JsonValueKind.Array, root.ValueKind);
        Assert.Equal(0, root.GetArrayLength());
    }

    #endregion

    #region JsonElement.Mutable — CreateArrayBuilder and CreateObjectBuilder

    [Fact]
    public void JsonElementMutable_CreateArrayBuilder_ReturnsEmptyArray()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Corvus.Text.Json.JsonElement.Mutable> builder =
            Corvus.Text.Json.JsonElement.CreateArrayBuilder(workspace);
        Corvus.Text.Json.JsonElement.Mutable root = builder.RootElement;

        Assert.Equal(Corvus.Text.Json.JsonValueKind.Array, root.ValueKind);
        Assert.Equal(0, root.GetArrayLength());
    }

    [Fact]
    public void JsonElementMutable_CreateArrayBuilder_ThenAddItems()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Corvus.Text.Json.JsonElement.Mutable> builder =
            Corvus.Text.Json.JsonElement.CreateArrayBuilder(workspace);
        Corvus.Text.Json.JsonElement.Mutable root = builder.RootElement;

        root.AddItem(1);
        root.AddItem(2);
        root.AddItem(3);

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("[1,2,3]", root.ToString());
    }

    [Fact]
    public void JsonElementMutable_CreateObjectBuilder_ReturnsEmptyObject()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Corvus.Text.Json.JsonElement.Mutable> builder =
            Corvus.Text.Json.JsonElement.CreateObjectBuilder(workspace);
        Corvus.Text.Json.JsonElement.Mutable root = builder.RootElement;

        Assert.Equal(Corvus.Text.Json.JsonValueKind.Object, root.ValueKind);
        Assert.Equal(0, root.GetPropertyCount());
    }

    [Fact]
    public void JsonElementMutable_CreateObjectBuilder_ThenSetProperties()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Corvus.Text.Json.JsonElement.Mutable> builder =
            Corvus.Text.Json.JsonElement.CreateObjectBuilder(workspace);
        Corvus.Text.Json.JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("name", "Alice");
        root.SetProperty("age", 30);

        Assert.Equal(2, root.GetPropertyCount());
        Assert.Equal("Alice", root.GetProperty("name"u8).GetString());
        Assert.Equal(30, root.GetProperty("age"u8).GetInt32());
    }

    [Fact]
    public void JsonElementMutable_CreateObjectBuilder_RoundTrip()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Corvus.Text.Json.JsonElement.Mutable> builder =
            Corvus.Text.Json.JsonElement.CreateObjectBuilder(workspace);
        Corvus.Text.Json.JsonElement.Mutable root = builder.RootElement;

        root.SetProperty("active", true);

        string json = root.ToString();

        using var reparsed = ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(json);
        Assert.True(reparsed.RootElement.GetProperty("active"u8).GetBoolean());
    }

    #endregion
}