// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text.Json;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// <summary>
/// Tests for the MetadataDb-level Copy and Move operations on <see cref="JsonElement.Mutable"/>,
/// including <see cref="JsonElement.Mutable.CopyPropertyFrom"/>,
/// <see cref="JsonElement.Mutable.CopyItemFrom"/>, <see cref="JsonElement.Mutable.CopyItemAppendFrom"/>,
/// and the direct Move methods (<see cref="JsonElement.Mutable.MovePropertyToProperty"/>, etc.).
/// </summary>
public static class JsonElementMutableCopyMoveTests
{
    #region CopyPropertyFrom — primitive values

    [Fact]
    public static void CopyPropertyFrom_NewProperty_PrimitiveValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source = root["a"];

        root.CopyPropertyFrom("b"u8, in source);

        Assert.Equal(42, root["b"].GetInt32());
        // Original is preserved.
        Assert.Equal(42, root["a"].GetInt32());
    }

    [Fact]
    public static void CopyPropertyFrom_ReplaceExistingProperty_PrimitiveValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 42, "b": 99}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source = root["a"];

        root.CopyPropertyFrom("b"u8, in source);

        Assert.Equal(42, root["b"].GetInt32());
        Assert.Equal(42, root["a"].GetInt32());
    }

    [Fact]
    public static void CopyPropertyFrom_StringValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"x": "hello"}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source = root["x"];

        root.CopyPropertyFrom("y"u8, in source);

        Assert.Equal("hello", root["y"].GetString());
        Assert.Equal("hello", root["x"].GetString());
    }

    [Fact]
    public static void CopyPropertyFrom_BooleanValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"flag": true}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source1 = root["flag"];
        root.CopyPropertyFrom("copy"u8, in source1);

        Assert.True(root["copy"].GetBoolean());
    }

    [Fact]
    public static void CopyPropertyFrom_NullValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"n": null}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source2 = root["n"];
        root.CopyPropertyFrom("n2"u8, in source2);

        Assert.Equal(JsonValueKind.Null, root["n2"].ValueKind);
    }

    #endregion

    #region CopyPropertyFrom — complex values

    [Fact]
    public static void CopyPropertyFrom_NestedObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"x": 1, "y": 2}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source3 = root["a"];
        root.CopyPropertyFrom("b"u8, in source3);

        Assert.Equal(JsonValueKind.Object, root["b"].ValueKind);
        Assert.Equal(1, root["b"]["x"].GetInt32());
        Assert.Equal(2, root["b"]["y"].GetInt32());
        // Original unchanged.
        Assert.Equal(1, root["a"]["x"].GetInt32());
    }

    [Fact]
    public static void CopyPropertyFrom_NestedArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": [1, 2, 3]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source4 = root["a"];
        root.CopyPropertyFrom("b"u8, in source4);

        Assert.Equal(JsonValueKind.Array, root["b"].ValueKind);
        Assert.Equal(3, root["b"].GetArrayLength());
        Assert.Equal(1, root["b"][0].GetInt32());
        Assert.Equal(2, root["b"][1].GetInt32());
        Assert.Equal(3, root["b"][2].GetInt32());
    }

    [Fact]
    public static void CopyPropertyFrom_DeeplyNestedStructure()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"b": {"c": [1, {"d": true}]}}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source5 = root["a"];
        root.CopyPropertyFrom("copy"u8, in source5);

        Assert.Equal(JsonValueKind.Object, root["copy"].ValueKind);
        Assert.Equal(1, root["copy"]["b"]["c"][0].GetInt32());
        Assert.True(root["copy"]["b"]["c"][1]["d"].GetBoolean());
    }

    [Fact]
    public static void CopyPropertyFrom_ReplaceExistingWithComplex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"x": 1}, "b": 99}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source6 = root["a"];
        root.CopyPropertyFrom("b"u8, in source6);

        Assert.Equal(JsonValueKind.Object, root["b"].ValueKind);
        Assert.Equal(1, root["b"]["x"].GetInt32());
    }

    [Fact]
    public static void CopyPropertyFrom_ReplacePrimitiveWithComplex_AndViceVersa()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj": {"x": 1}, "val": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Replace primitive with complex.
        JsonElement.Mutable source7 = root["obj"];
        root.CopyPropertyFrom("val"u8, in source7);
        Assert.Equal(1, root["val"]["x"].GetInt32());

        // Replace complex with primitive — re-read source since document mutated.
        using var doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"obj": {"x": 1}, "val": {"x": 1}, "num": 7}""");
        using var builder2 = doc2.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root2 = builder2.RootElement;
        JsonElement.Mutable source8 = root2["num"];
        root2.CopyPropertyFrom("obj"u8, in source8);
        Assert.Equal(7, root2["obj"].GetInt32());
    }

    #endregion

    #region CopyPropertyFrom — self-referential (dest inside source)

    [Fact]
    public static void CopyPropertyFrom_CopyObjectIntoItself()
    {
        // RFC 6902 §4.5 allows copy where dest is inside source.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"x": 1}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable a = root["a"];

        // Copy "a" into itself as a child property "a.self".
        a.CopyPropertyFrom("self"u8, in a);

        Assert.Equal(JsonValueKind.Object, root["a"]["self"].ValueKind);
        Assert.Equal(1, root["a"]["self"]["x"].GetInt32());
        Assert.Equal(1, root["a"]["x"].GetInt32());
    }

    #endregion

    #region CopyItemFrom

    [Fact]
    public static void CopyItemFrom_InsertAtBeginning()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20, 30]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source9 = root[2];
        root.CopyItemFrom(0, in source9); // Copy 30 to index 0.

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(30, root[0].GetInt32());
        Assert.Equal(10, root[1].GetInt32());
        Assert.Equal(20, root[2].GetInt32());
        Assert.Equal(30, root[3].GetInt32());
    }

    [Fact]
    public static void CopyItemFrom_InsertInMiddle()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20, 30]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source10 = root[0];
        root.CopyItemFrom(1, in source10); // Copy 10 to index 1.

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(10, root[0].GetInt32());
        Assert.Equal(10, root[1].GetInt32());
        Assert.Equal(20, root[2].GetInt32());
        Assert.Equal(30, root[3].GetInt32());
    }

    [Fact]
    public static void CopyItemFrom_InsertAtEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20, 30]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source11 = root[1];
        root.CopyItemFrom(3, in source11); // Copy 20 to index 3 (end).

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(10, root[0].GetInt32());
        Assert.Equal(20, root[1].GetInt32());
        Assert.Equal(30, root[2].GetInt32());
        Assert.Equal(20, root[3].GetInt32());
    }

    [Fact]
    public static void CopyItemFrom_ComplexElement()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"a": 1}, "hello"]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source12 = root[0];
        root.CopyItemFrom(1, in source12); // Insert copy of {"a":1} at index 1.

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0]["a"].GetInt32());
        Assert.Equal(1, root[1]["a"].GetInt32());
        Assert.Equal("hello", root[2].GetString());
    }

    [Fact]
    public static void CopyItemFrom_NestedArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1, 2], [3, 4]]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source13 = root[0];
        root.CopyItemFrom(1, in source13); // Insert copy of [1,2] at index 1.

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(2, root[0].GetArrayLength());
        Assert.Equal(1, root[0][0].GetInt32());
        Assert.Equal(2, root[0][1].GetInt32());
        Assert.Equal(1, root[1][0].GetInt32());
        Assert.Equal(2, root[1][1].GetInt32());
        Assert.Equal(3, root[2][0].GetInt32());
    }

    #endregion

    #region CopyItemAppendFrom

    [Fact]
    public static void CopyItemAppendFrom_PrimitiveValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source14 = root[0];
        root.CopyItemAppendFrom(in source14); // Append copy of 1.

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
        Assert.Equal(1, root[3].GetInt32());
    }

    [Fact]
    public static void CopyItemAppendFrom_ComplexValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"k": "v"}, 99]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source15 = root[0];
        root.CopyItemAppendFrom(in source15); // Append copy of {"k":"v"}.

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("v", root[0]["k"].GetString());
        Assert.Equal(99, root[1].GetInt32());
        Assert.Equal("v", root[2]["k"].GetString());
    }

    [Fact]
    public static void CopyItemAppendFrom_EmptyArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr": [], "val": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source16 = root["val"];
        root["arr"].CopyItemAppendFrom(in source16);

        Assert.Equal(1, root["arr"].GetArrayLength());
        Assert.Equal(42, root["arr"][0].GetInt32());
    }

    #endregion
    #region MovePropertyToProperty (via snapshot-equivalent)

    [Fact]
    public static void MovePropertyToProperty_NewProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 42, "b": {}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable b = root["b"];
        root.MovePropertyToProperty("a"u8, in b, "moved"u8);

        root = builder.RootElement;
        Assert.False(root.TryGetProperty("a"u8, out _));
        Assert.Equal(42, root["b"]["moved"].GetInt32());
    }

    [Fact]
    public static void MovePropertyToProperty_ReplaceExisting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 42, "b": {"existing": 0}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable b = root["b"];
        root.MovePropertyToProperty("a"u8, in b, "existing"u8);

        root = builder.RootElement;
        Assert.Equal(42, root["b"]["existing"].GetInt32());
    }

    [Fact]
    public static void MovePropertyToProperty_ComplexValueMove()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"src": {"x": [1, 2], "y": true}, "dest": {}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable dest = root["dest"];
        root.MovePropertyToProperty("src"u8, in dest, "moved"u8);

        root = builder.RootElement;
        Assert.False(root.TryGetProperty("src"u8, out _));
        Assert.Equal(JsonValueKind.Object, root["dest"]["moved"].ValueKind);
        Assert.Equal(1, root["dest"]["moved"]["x"][0].GetInt32());
        Assert.Equal(2, root["dest"]["moved"]["x"][1].GetInt32());
        Assert.True(root["dest"]["moved"]["y"].GetBoolean());
    }

    #endregion

    #region MovePropertyToArray — Move property to array index

    [Fact]
    public static void MovePropertyToArray_AtBeginning()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": 99, "arr": [1, 2]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        root.MovePropertyToArray("val"u8, in arr, 0);

        root = builder.RootElement;
        Assert.False(root.TryGetProperty("val"u8, out _));
        Assert.Equal(3, root["arr"].GetArrayLength());
        Assert.Equal(99, root["arr"][0].GetInt32());
        Assert.Equal(1, root["arr"][1].GetInt32());
        Assert.Equal(2, root["arr"][2].GetInt32());
    }

    [Fact]
    public static void MoveItemToArray_InMiddle()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20, 30]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        // Move item[2] (30) to index 1 (post-removal).
        root.MoveItemToArray(2, in root, 1);

        root = builder.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(10, root[0].GetInt32());
        Assert.Equal(30, root[1].GetInt32());
        Assert.Equal(20, root[2].GetInt32());
    }

    [Fact]
    public static void MoveItemToArray_ComplexValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"a": 1}, "hello", true]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        // Move item[0] ({"a":1}) to post-removal index 1.
        root.MoveItemToArray(0, in root, 1);

        root = builder.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("hello", root[0].GetString());
        Assert.Equal(1, root[1]["a"].GetInt32());
        Assert.True(root[2].GetBoolean());
    }

    #endregion

    #region MovePropertyToArrayEnd — Move property to end of array

    [Fact]
    public static void MovePropertyToArrayEnd_PrimitiveValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": 42, "arr": [1, 2]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        root.MovePropertyToArrayEnd("val"u8, in arr);

        root = builder.RootElement;
        Assert.False(root.TryGetProperty("val"u8, out _));
        Assert.Equal(3, root["arr"].GetArrayLength());
        Assert.Equal(1, root["arr"][0].GetInt32());
        Assert.Equal(2, root["arr"][1].GetInt32());
        Assert.Equal(42, root["arr"][2].GetInt32());
    }

    [Fact]
    public static void MovePropertyToArrayEnd_ComplexValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj": {"x": 1}, "arr": []}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        root.MovePropertyToArrayEnd("obj"u8, in arr);

        root = builder.RootElement;
        Assert.Equal(1, root["arr"].GetArrayLength());
        Assert.Equal(1, root["arr"][0]["x"].GetInt32());
    }

    [Fact]
    public static void MovePropertyToArrayEnd_ToEmptyArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr": [], "val": "hello"}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        root.MovePropertyToArrayEnd("val"u8, in arr);

        root = builder.RootElement;
        Assert.Equal(1, root["arr"].GetArrayLength());
        Assert.Equal("hello", root["arr"][0].GetString());
    }

    #endregion
    #region Move workflow (direct Move methods)

    [Fact]
    public static void MoveWorkflow_ObjectPropertyToObjectProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"src": 42, "dest": {}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable dest = root["dest"];
        root.MovePropertyToProperty("src"u8, in dest, "val"u8);

        root = builder.RootElement;
        Assert.Equal("""{"dest":{"val":42}}""", root.ToString());
    }

    [Fact]
    public static void MoveWorkflow_ArrayItemToArrayItem()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3, 4, 5]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        // Move item[4] (5) to post-removal index 1.
        root.MoveItemToArray(4, in root, 1);

        root = builder.RootElement;
        Assert.Equal("[1,5,2,3,4]", root.ToString());
    }

    [Fact]
    public static void MoveWorkflow_ObjectPropertyToArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": "moved", "arr": [1]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        root.MovePropertyToArrayEnd("val"u8, in arr);

        root = builder.RootElement;
        Assert.Equal("""{"arr":[1,"moved"]}""", root.ToString());
    }

    [Fact]
    public static void MoveWorkflow_ArrayItemToObjectProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj": {}, "arr": [42, 99]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable obj = root["obj"];
        root["arr"].MoveItemToProperty(0, in obj, "x"u8);

        root = builder.RootElement;
        Assert.Equal(42, root["obj"]["x"].GetInt32());
        Assert.Equal(1, root["arr"].GetArrayLength());
        Assert.Equal(99, root["arr"][0].GetInt32());
    }

    [Fact]
    public static void MoveWorkflow_ComplexObjectBetweenContainers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"source": {"nested": {"a": 1, "b": [2, 3]}}, "target": {"items": []}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable items = root["target"]["items"];
        root["source"].MovePropertyToArrayEnd("nested"u8, in items);

        root = builder.RootElement;
        Assert.Equal(JsonValueKind.Object, root["source"].ValueKind);
        Assert.False(root["source"].TryGetProperty("nested"u8, out _));
        Assert.Equal(1, root["target"]["items"].GetArrayLength());
        Assert.Equal(1, root["target"]["items"][0]["a"].GetInt32());
        Assert.Equal(2, root["target"]["items"][0]["b"][0].GetInt32());
        Assert.Equal(3, root["target"]["items"][0]["b"][1].GetInt32());
    }

    #endregion
    #region Copy workflow (direct copy, no snapshot)

    [Fact]
    public static void CopyWorkflow_PropertyToProperty_PreservesSource()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": [1, 2, 3]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source17 = root["a"];
        root.CopyPropertyFrom("b"u8, in source17);

        // Both are independently valid.
        root = builder.RootElement;
        JsonElement.Mutable bVal = root["b"];
        Assert.Equal(3, bVal.GetArrayLength());
        Assert.Equal(1, bVal[0].GetInt32());
        Assert.Equal(2, bVal[1].GetInt32());
        Assert.Equal(3, bVal[2].GetInt32());
        // Original "a" still references the source document values.
        JsonElement.Mutable aVal = root["a"];
        Assert.Equal(3, aVal.GetArrayLength());
        Assert.Equal(1, aVal[0].GetInt32());
        Assert.Equal(2, aVal[1].GetInt32());
        Assert.Equal(3, aVal[2].GetInt32());
    }

    [Fact]
    public static void CopyWorkflow_PropertyToArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": 42, "arr": [1]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source18 = root["val"];
        root["arr"].CopyItemAppendFrom(in source18);

        Assert.Equal(42, root["val"].GetInt32()); // Source preserved.
        Assert.Equal(2, root["arr"].GetArrayLength());
        Assert.Equal(42, root["arr"][1].GetInt32());
    }

    [Fact]
    public static void CopyWorkflow_ArrayToArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source19 = root[0];
        root.CopyItemFrom(1, in source19); // Copy 10 to index 1.

        Assert.Equal("[10,10,20]", root.ToString());
    }

    #endregion

    #region Multiple sequential operations

    [Fact]
    public static void MultipleCopies_SameSource()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"src": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source20 = root["src"];
        root.CopyPropertyFrom("a"u8, in source20);
        JsonElement.Mutable source21 = root["src"];
        root.CopyPropertyFrom("b"u8, in source21);
        JsonElement.Mutable source22 = root["src"];
        root.CopyPropertyFrom("c"u8, in source22);

        Assert.Equal(42, root["src"].GetInt32());
        Assert.Equal(42, root["a"].GetInt32());
        Assert.Equal(42, root["b"].GetInt32());
        Assert.Equal(42, root["c"].GetInt32());
    }

    [Fact]
    public static void MultipleMoves_Sequential()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Move last item to beginning, twice.
        root.MoveItemToArray(2, in root, 0);

        // Now [3, 1, 2]. Move last to beginning again.
        root = builder.RootElement;
        root.MoveItemToArray(2, in root, 0);

        root = builder.RootElement;
        Assert.Equal("[2,3,1]", root.ToString());
    }

    [Fact]
    public static void CopyThenMutate_IndependentValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"x": 1}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source23 = root["a"];
        root.CopyPropertyFrom("b"u8, in source23);

        // Mutate the copy — should not affect original.
        root["b"].SetProperty("x"u8, 999);

        Assert.Equal(1, root["a"]["x"].GetInt32());
        Assert.Equal(999, root["b"]["x"].GetInt32());
    }

    #endregion

    #region Serialization round-trip

    [Theory]
    [InlineData("""{"a": 1}""", """{"a":1,"b":1}""")]
    [InlineData("""{"a": "hello"}""", """{"a":"hello","b":"hello"}""")]
    [InlineData("""{"a": true}""", """{"a":true,"b":true}""")]
    [InlineData("""{"a": null}""", """{"a":null,"b":null}""")]
    [InlineData("""{"a": [1, 2]}""", """{"a":[1,2],"b":[1,2]}""")]
    [InlineData("""{"a": {"x": 1}}""", """{"a":{"x":1},"b":{"x":1}}""")]
    public static void CopyPropertyFrom_SerializesCorrectly(string input, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(input);
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source24 = root["a"];
        root.CopyPropertyFrom("b"u8, in source24);

        Assert.Equal(expected, root.ToString());
    }

    [Theory]
    [InlineData("""[1, 2]""", """[1, 2, 1]""")]
    [InlineData("""["a", "b"]""", """["a","b","a"]""")]
    [InlineData("""[true]""", """[true,true]""")]
    [InlineData("""[[1, 2]]""", """[[1,2],[1,2]]""")]
    public static void CopyItemAppendFrom_SerializesCorrectly(string input, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(input);
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source25 = root[0];
        root.CopyItemAppendFrom(in source25);

        // Normalize expected by removing extra whitespace.
        string normalizedExpected = expected.Replace(" ", string.Empty);
        Assert.Equal(normalizedExpected, root.ToString());
    }

    #endregion

    #region Edge cases

    [Fact]
    public static void CopyPropertyFrom_EmptyObject_AddsFirstProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"empty": {}, "val": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source26 = root["val"];
        root["empty"].CopyPropertyFrom("x"u8, in source26);

        Assert.Equal(42, root["empty"]["x"].GetInt32());
    }

    [Fact]
    public static void CopyItemFrom_SingleElementArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[42]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source27 = root[0];
        root.CopyItemFrom(0, in source27); // Duplicate.

        Assert.Equal("[42,42]", root.ToString());
    }

    [Fact]
    public static void MovePropertyToProperty_RenameInSameObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"only": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.MovePropertyToProperty("only"u8, in root, "renamed"u8);

        root = builder.RootElement;
        Assert.Equal("""{"renamed":42}""", root.ToString());
    }

    [Fact]
    public static void CopyPropertyFrom_PropertyNameWithSpecialChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a/b": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source28 = root["a/b"];
        root.CopyPropertyFrom("c/d"u8, in source28);

        Assert.Equal(1, root["c/d"].GetInt32());
    }

    [Fact]
    public static void CopyPropertyFrom_LargeNestedStructure()
    {
        // Test with a structure deep enough to exercise multi-row copying.
        string json = """
            {
                "data": {
                    "level1": {
                        "level2": {
                            "level3": {
                                "values": [1, 2, 3, 4, 5],
                                "flag": true,
                                "name": "deep"
                            }
                        }
                    }
                }
            }
            """;

        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source29 = root["data"];
        root.CopyPropertyFrom("backup"u8, in source29);

        Assert.Equal("deep", root["backup"]["level1"]["level2"]["level3"]["name"].GetString());
        Assert.Equal(5, root["backup"]["level1"]["level2"]["level3"]["values"].GetArrayLength());
        Assert.True(root["backup"]["level1"]["level2"]["level3"]["flag"].GetBoolean());
        // Original still intact.
        Assert.Equal("deep", root["data"]["level1"]["level2"]["level3"]["name"].GetString());
    }

    #endregion

    #region Direct Move methods (MovePropertyTo*, MoveItemTo*)

    [Fact]
    public static void MovePropertyToProperty_SimpleValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"src": {"val": 42}, "dest": {}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable dest = root["dest"];
        root["src"].MovePropertyToProperty("val"u8, in dest, "moved"u8);

        root = builder.RootElement;
        Assert.False(root["src"].TryGetProperty("val"u8, out _));
        Assert.Equal(42, root["dest"]["moved"].GetInt32());
    }

    [Fact]
    public static void MovePropertyToProperty_SamePropertyName_NoOp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"foo": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool result = root.MovePropertyToProperty("foo"u8, in root, "foo"u8);

        Assert.True(result);
        root = builder.RootElement;
        Assert.Equal(1, root["foo"].GetInt32());
    }

    [Fact]
    public static void MovePropertyToArray_InsertAtIndex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": 99, "arr": [1, 2]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        root.MovePropertyToArray("val"u8, in arr, 1);

        root = builder.RootElement;
        Assert.False(root.TryGetProperty("val"u8, out _));
        Assert.Equal(3, root["arr"].GetArrayLength());
        Assert.Equal(1, root["arr"][0].GetInt32());
        Assert.Equal(99, root["arr"][1].GetInt32());
        Assert.Equal(2, root["arr"][2].GetInt32());
    }

    [Fact]
    public static void MovePropertyToArrayEnd_AppendsValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": "hello", "arr": [1]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        root.MovePropertyToArrayEnd("val"u8, in arr);

        root = builder.RootElement;
        Assert.False(root.TryGetProperty("val"u8, out _));
        Assert.Equal(2, root["arr"].GetArrayLength());
        Assert.Equal("hello", root["arr"][1].GetString());
    }

    [Fact]
    public static void MoveItemToArray_DifferentArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": [10, 20], "b": [30]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable b = root["b"];
        root["a"].MoveItemToArray(0, in b, 0);

        root = builder.RootElement;
        Assert.Equal(1, root["a"].GetArrayLength());
        Assert.Equal(20, root["a"][0].GetInt32());
        Assert.Equal(2, root["b"].GetArrayLength());
        Assert.Equal(10, root["b"][0].GetInt32());
        Assert.Equal(30, root["b"][1].GetInt32());
    }

    [Fact]
    public static void MoveItemToArray_SameArray_ForwardMove()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3, 4]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        // Move index 1 to post-removal index 3 (append).
        root.MoveItemToArray(1, in root, 3);

        root = builder.RootElement;
        Assert.Equal("[1,3,4,2]", root.ToString());
    }

    [Fact]
    public static void MoveItemToArray_SameArray_BackwardMove()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3, 4]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        // Move index 3 to index 1.
        root.MoveItemToArray(3, in root, 1);

        root = builder.RootElement;
        Assert.Equal("[1,4,2,3]", root.ToString());
    }

    [Fact]
    public static void MoveItemToArrayEnd_SameArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.MoveItemToArrayEnd(0, in root);

        root = builder.RootElement;
        Assert.Equal("[2,3,1]", root.ToString());
    }

    [Fact]
    public static void MoveItemToProperty_CreatesNewProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj": {}, "arr": [42, 99]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable obj = root["obj"];
        root["arr"].MoveItemToProperty(0, in obj, "x"u8);

        root = builder.RootElement;
        Assert.Equal(42, root["obj"]["x"].GetInt32());
        Assert.Equal(1, root["arr"].GetArrayLength());
        Assert.Equal(99, root["arr"][0].GetInt32());
    }

    [Fact]
    public static void MovePropertyToProperty_ComplexValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"source": {"nested": {"a": 1, "b": [2, 3]}}, "target": {}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable target = root["target"];
        root["source"].MovePropertyToProperty("nested"u8, in target, "moved"u8);

        root = builder.RootElement;
        Assert.False(root["source"].TryGetProperty("nested"u8, out _));
        Assert.Equal(1, root["target"]["moved"]["a"].GetInt32());
        Assert.Equal(2, root["target"]["moved"]["b"].GetArrayLength());
    }

    [Fact]
    public static void MovePropertyToProperty_ReplacesExisting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 42, "b": 99}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.MovePropertyToProperty("a"u8, in root, "b"u8);

        root = builder.RootElement;
        Assert.False(root.TryGetProperty("a"u8, out _));
        Assert.Equal(42, root["b"].GetInt32());
    }

    #endregion
}