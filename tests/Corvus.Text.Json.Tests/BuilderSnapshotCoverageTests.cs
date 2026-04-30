// <copyright file="BuilderSnapshotCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests targeting JsonDocumentBuilder snapshot/restore paths,
/// IJsonDocument interface methods on ParsedJsonDocument, and
/// TryReplaceProperty/RemoveRange/CopyValueToProperty builder internals.
/// </summary>
public static class BuilderSnapshotCoverageTests
{
    #region Snapshot/Restore with property map

    [Fact]
    public static void SnapshotAndRestore_WithPropertyMap_RestoresOriginalProperties()
    {
        // Creating a builder from a parsed document with properties,
        // then accessing properties by name triggers property map creation.
        // Taking a snapshot AFTER that should capture the property map/buckets/entries.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{"name":"Alice","age":30,"active":true}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        // Access properties to trigger property map build
        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal("Alice", root.GetProperty("name"u8).GetString());
        Assert.Equal(30, root.GetProperty("age"u8).GetInt32());
        Assert.True(root.GetProperty("active"u8).GetBoolean());

        // Ensure property map is built
        JsonElement.Mutable.EnsurePropertyMap(in root);

        // Now snapshot — should capture property map, buckets, entries
        using JsonDocumentBuilderSnapshot<JsonElement.Mutable> snapshot = builder.CreateSnapshot();

        // Mutate
        root = builder.RootElement;
        root.SetProperty("name"u8, "Bob");
        root.SetProperty("extra"u8, 999);
        root.RemoveProperty("active"u8);

        // Verify mutation
        root = builder.RootElement;
        Assert.Equal("Bob", root.GetProperty("name"u8).GetString());

        // Restore
        builder.Restore(snapshot);

        // Verify restoration
        root = builder.RootElement;
        Assert.Equal("Alice", root.GetProperty("name"u8).GetString());
        Assert.Equal(30, root.GetProperty("age"u8).GetInt32());
        Assert.True(root.GetProperty("active"u8).GetBoolean());
    }

    [Fact]
    public static void SnapshotAndRestore_ManyProperties_PropertyMapIntact()
    {
        // Use many properties to ensure buckets/entries are populated
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{"a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7,"h":8}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Access all properties to build the full property map
        for (int i = 0; i < 8; i++)
        {
            string propName = ((char)('a' + i)).ToString();
            Assert.Equal(i + 1, root.GetProperty(propName).GetInt32());
        }

        JsonElement.Mutable.EnsurePropertyMap(in root);

        // Snapshot with full property map
        using JsonDocumentBuilderSnapshot<JsonElement.Mutable> snapshot = builder.CreateSnapshot();

        // Mutate heavily
        root = builder.RootElement;
        root.SetProperty("a"u8, 100);
        root.SetProperty("z"u8, 999);
        root.RemoveProperty("d"u8);
        root.RemoveProperty("e"u8);

        // Restore
        builder.Restore(snapshot);

        // Verify all original properties are back
        root = builder.RootElement;
        Assert.Equal(1, root.GetProperty("a"u8).GetInt32());
        Assert.Equal(4, root.GetProperty("d"u8).GetInt32());
        Assert.Equal(5, root.GetProperty("e"u8).GetInt32());
    }

    [Fact]
    public static void SnapshotAndRestore_NestedObject_PropertyMapRestored()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{"outer":{"inner":"hello"},"other":42}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal("hello", root.GetProperty("outer"u8).GetProperty("inner"u8).GetString());
        JsonElement.Mutable.EnsurePropertyMap(in root);

        using JsonDocumentBuilderSnapshot<JsonElement.Mutable> snapshot = builder.CreateSnapshot();

        // Mutate the nested structure
        root = builder.RootElement;
        root.SetProperty("outer"u8, "replaced");
        Assert.Equal(JsonValueKind.String, builder.RootElement.GetProperty("outer"u8).ValueKind);

        // Restore
        builder.Restore(snapshot);

        // Verify nested structure is back
        root = builder.RootElement;
        Assert.Equal("hello", root.GetProperty("outer"u8).GetProperty("inner"u8).GetString());
    }

    #endregion

    #region TryReplaceProperty paths (unique: simple value + document source)

    [Fact]
    public static void TryReplaceProperty_SimpleValue_ReplacesExisting()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{"x":1,"y":2}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Replace with a simple value (implicit operator from int to Source)
        bool replaced = root.TryReplaceProperty("x"u8, 42);
        Assert.True(replaced);
        Assert.Equal(42, builder.RootElement.GetProperty("x"u8).GetInt32());
    }

    [Fact]
    public static void TryReplaceProperty_FromAnotherDocument_ReplacesExisting()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{"x":"old","y":2}""");
        using ParsedJsonDocument<JsonElement> replacement = ParsedJsonDocument<JsonElement>.Parse(
            """{"nested":"value"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool replaced = root.TryReplaceProperty("x"u8, replacement.RootElement);
        Assert.True(replaced);
        Assert.Equal(JsonValueKind.Object, builder.RootElement.GetProperty("x"u8).ValueKind);
    }

    #endregion

    #region RemoveProperty

    [Fact]
    public static void RemoveProperty_ExistingProperty_Removed()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{"a":1,"b":2,"c":3}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("b"u8);
        Assert.True(removed);

        string result = builder.RootElement.ToString();
        Assert.Contains("\"a\"", result);
        Assert.DoesNotContain("\"b\"", result);
        Assert.Contains("\"c\"", result);
    }

    [Fact]
    public static void RemoveProperty_NonExistingProperty_ReturnsFalse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("nonexistent"u8);
        Assert.False(removed);
    }

    #endregion

    #region Array operations (unique: simple Source path)

    [Fact]
    public static void SetItem_WithSource_ReplacesArrayElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(
            """[1,2,3]""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = source.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetItem(1, 99);

        Assert.Equal(1, builder.RootElement[0].GetInt32());
        Assert.Equal(99, builder.RootElement[1].GetInt32());
        Assert.Equal(3, builder.RootElement[2].GetInt32());
    }

    #endregion

    #region CreateBuilder with workspace capacity

    [Fact]
    public static void CreateBuilder_WithExplicitCapacity_Succeeds()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Exercise the CreateBuilder path with explicit initial capacity
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            workspace.CreateBuilder<JsonElement.Mutable>(initialCapacity: 50, initialValueBufferSize: 4096);

        // The builder was created successfully — verify it's not null and can be disposed
        Assert.NotNull(builder);
    }

    #endregion
}
