// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text.Json;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the Copy and Move operations on <see cref="IMutableJsonDocument"/>,
/// including CopyValueToProperty, CopyValueToArrayIndex, CopyValueToArrayEnd,
/// and the Move methods (MovePropertyToProperty, MoveItemToArray, etc.).
/// </summary>
[TestClass]
public class JsonElementMutableCopyMoveTests
{
    private static IMutableJsonDocument GetDoc(in JsonElement.Mutable element)
    {
        return (IMutableJsonDocument)((IJsonElement)element).ParentDocument;
    }

    private static int Idx(in JsonElement.Mutable element)
    {
        return ((IJsonElement)element).ParentDocumentIndex;
    }

    #region CopyPropertyFrom — primitive values

    [TestMethod]
    public void CopyPropertyFrom_NewProperty_PrimitiveValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source = root["a"];

        GetDoc(in root).CopyValueToProperty(Idx(in source), Idx(in root), "b"u8);

        Assert.AreEqual(42, root["b"].GetInt32());
        // Original is preserved.
        Assert.AreEqual(42, root["a"].GetInt32());
    }

    [TestMethod]
    public void CopyPropertyFrom_ReplaceExistingProperty_PrimitiveValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 42, "b": 99}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source = root["a"];

        GetDoc(in root).CopyValueToProperty(Idx(in source), Idx(in root), "b"u8);

        Assert.AreEqual(42, root["b"].GetInt32());
        Assert.AreEqual(42, root["a"].GetInt32());
    }

    [TestMethod]
    public void CopyPropertyFrom_StringValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"x": "hello"}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source = root["x"];

        GetDoc(in root).CopyValueToProperty(Idx(in source), Idx(in root), "y"u8);

        Assert.AreEqual("hello", root["y"].GetString());
        Assert.AreEqual("hello", root["x"].GetString());
    }

    [TestMethod]
    public void CopyPropertyFrom_BooleanValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"flag": true}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source1 = root["flag"];
        GetDoc(in root).CopyValueToProperty(Idx(in source1), Idx(in root), "copy"u8);

        Assert.IsTrue(root["copy"].GetBoolean());
    }

    [TestMethod]
    public void CopyPropertyFrom_NullValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"n": null}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source2 = root["n"];
        GetDoc(in root).CopyValueToProperty(Idx(in source2), Idx(in root), "n2"u8);

        Assert.AreEqual(JsonValueKind.Null, root["n2"].ValueKind);
    }

    #endregion

    #region CopyPropertyFrom — complex values

    [TestMethod]
    public void CopyPropertyFrom_NestedObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"x": 1, "y": 2}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source3 = root["a"];
        GetDoc(in root).CopyValueToProperty(Idx(in source3), Idx(in root), "b"u8);

        Assert.AreEqual(JsonValueKind.Object, root["b"].ValueKind);
        Assert.AreEqual(1, root["b"]["x"].GetInt32());
        Assert.AreEqual(2, root["b"]["y"].GetInt32());
        // Original unchanged.
        Assert.AreEqual(1, root["a"]["x"].GetInt32());
    }

    [TestMethod]
    public void CopyPropertyFrom_NestedArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": [1, 2, 3]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source4 = root["a"];
        GetDoc(in root).CopyValueToProperty(Idx(in source4), Idx(in root), "b"u8);

        Assert.AreEqual(JsonValueKind.Array, root["b"].ValueKind);
        Assert.AreEqual(3, root["b"].GetArrayLength());
        Assert.AreEqual(1, root["b"][0].GetInt32());
        Assert.AreEqual(2, root["b"][1].GetInt32());
        Assert.AreEqual(3, root["b"][2].GetInt32());
    }

    [TestMethod]
    public void CopyPropertyFrom_DeeplyNestedStructure()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"b": {"c": [1, {"d": true}]}}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source5 = root["a"];
        GetDoc(in root).CopyValueToProperty(Idx(in source5), Idx(in root), "copy"u8);

        Assert.AreEqual(JsonValueKind.Object, root["copy"].ValueKind);
        Assert.AreEqual(1, root["copy"]["b"]["c"][0].GetInt32());
        Assert.IsTrue(root["copy"]["b"]["c"][1]["d"].GetBoolean());
    }

    [TestMethod]
    public void CopyPropertyFrom_ReplaceExistingWithComplex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"x": 1}, "b": 99}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source6 = root["a"];
        GetDoc(in root).CopyValueToProperty(Idx(in source6), Idx(in root), "b"u8);

        Assert.AreEqual(JsonValueKind.Object, root["b"].ValueKind);
        Assert.AreEqual(1, root["b"]["x"].GetInt32());
    }

    [TestMethod]
    public void CopyPropertyFrom_ReplacePrimitiveWithComplex_AndViceVersa()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj": {"x": 1}, "val": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Replace primitive with complex.
        JsonElement.Mutable source7 = root["obj"];
        GetDoc(in root).CopyValueToProperty(Idx(in source7), Idx(in root), "val"u8);

        Assert.AreEqual(1, root["val"]["x"].GetInt32());

        // Replace complex with primitive — re-read source since document mutated.
        using var doc2 = ParsedJsonDocument<JsonElement>.Parse("""{"obj": {"x": 1}, "val": {"x": 1}, "num": 7}""");
        using var builder2 = doc2.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root2 = builder2.RootElement;
        JsonElement.Mutable source8 = root2["num"];
        GetDoc(in root2).CopyValueToProperty(Idx(in source8), Idx(in root2), "obj"u8);

        Assert.AreEqual(7, root2["obj"].GetInt32());
    }

    #endregion

    #region CopyPropertyFrom — self-referential (dest inside source)

    [TestMethod]
    public void CopyPropertyFrom_CopyObjectIntoItself()
    {
        // RFC 6902 §4.5 allows copy where dest is inside source.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"x": 1}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable a = root["a"];

        // Copy "a" into itself as a child property "a.self".
        GetDoc(in a).CopyValueToProperty(Idx(in a), Idx(in a), "self"u8);

        Assert.AreEqual(JsonValueKind.Object, root["a"]["self"].ValueKind);
        Assert.AreEqual(1, root["a"]["self"]["x"].GetInt32());
        Assert.AreEqual(1, root["a"]["x"].GetInt32());
    }

    #endregion

    #region CopyItemFrom

    [TestMethod]
    public void CopyItemFrom_InsertAtBeginning()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20, 30]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source9 = root[2];
        GetDoc(in root).CopyValueToArrayIndex(Idx(in source9), Idx(in root), 0); // Copy 30 to index 0.

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(30, root[0].GetInt32());
        Assert.AreEqual(10, root[1].GetInt32());
        Assert.AreEqual(20, root[2].GetInt32());
        Assert.AreEqual(30, root[3].GetInt32());
    }

    [TestMethod]
    public void CopyItemFrom_InsertInMiddle()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20, 30]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source10 = root[0];
        GetDoc(in root).CopyValueToArrayIndex(Idx(in source10), Idx(in root), 1); // Copy 10 to index 1.

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(10, root[0].GetInt32());
        Assert.AreEqual(10, root[1].GetInt32());
        Assert.AreEqual(20, root[2].GetInt32());
        Assert.AreEqual(30, root[3].GetInt32());
    }

    [TestMethod]
    public void CopyItemFrom_InsertAtEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20, 30]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source11 = root[1];
        GetDoc(in root).CopyValueToArrayIndex(Idx(in source11), Idx(in root), 3); // Copy 20 to index 3 (end).

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(10, root[0].GetInt32());
        Assert.AreEqual(20, root[1].GetInt32());
        Assert.AreEqual(30, root[2].GetInt32());
        Assert.AreEqual(20, root[3].GetInt32());
    }

    [TestMethod]
    public void CopyItemFrom_ComplexElement()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"a": 1}, "hello"]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source12 = root[0];
        GetDoc(in root).CopyValueToArrayIndex(Idx(in source12), Idx(in root), 1); // Insert copy of {"a":1} at index 1.

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0]["a"].GetInt32());
        Assert.AreEqual(1, root[1]["a"].GetInt32());
        Assert.AreEqual("hello", root[2].GetString());
    }

    [TestMethod]
    public void CopyItemFrom_NestedArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1, 2], [3, 4]]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source13 = root[0];
        GetDoc(in root).CopyValueToArrayIndex(Idx(in source13), Idx(in root), 1); // Insert copy of [1,2] at index 1.

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(2, root[0].GetArrayLength());
        Assert.AreEqual(1, root[0][0].GetInt32());
        Assert.AreEqual(2, root[0][1].GetInt32());
        Assert.AreEqual(1, root[1][0].GetInt32());
        Assert.AreEqual(2, root[1][1].GetInt32());
        Assert.AreEqual(3, root[2][0].GetInt32());
    }

    #endregion

    #region CopyItemAppendFrom

    [TestMethod]
    public void CopyItemAppendFrom_PrimitiveValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source14 = root[0];
        GetDoc(in root).CopyValueToArrayEnd(Idx(in source14), Idx(in root)); // Append copy of 1.

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2, root[1].GetInt32());
        Assert.AreEqual(3, root[2].GetInt32());
        Assert.AreEqual(1, root[3].GetInt32());
    }

    [TestMethod]
    public void CopyItemAppendFrom_ComplexValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"k": "v"}, 99]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source15 = root[0];
        GetDoc(in root).CopyValueToArrayEnd(Idx(in source15), Idx(in root)); // Append copy of {"k":"v"}.

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("v", root[0]["k"].GetString());
        Assert.AreEqual(99, root[1].GetInt32());
        Assert.AreEqual("v", root[2]["k"].GetString());
    }

    [TestMethod]
    public void CopyItemAppendFrom_EmptyArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr": [], "val": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source16 = root["val"];
        JsonElement.Mutable target_arr = root["arr"];

        GetDoc(in target_arr).CopyValueToArrayEnd(Idx(in source16), Idx(in target_arr));

        Assert.AreEqual(1, root["arr"].GetArrayLength());
        Assert.AreEqual(42, root["arr"][0].GetInt32());
    }

    #endregion
    #region MovePropertyToProperty (via snapshot-equivalent)

    [TestMethod]
    public void MovePropertyToProperty_NewProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 42, "b": {}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable b = root["b"];
        GetDoc(in root).MovePropertyToProperty(Idx(in root), "a"u8, Idx(in b), "moved"u8);

        Assert.IsFalse(root.TryGetProperty("a"u8, out _));
        Assert.AreEqual(42, root["b"]["moved"].GetInt32());
    }

    [TestMethod]
    public void MovePropertyToProperty_ReplaceExisting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 42, "b": {"existing": 0}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable b = root["b"];
        GetDoc(in root).MovePropertyToProperty(Idx(in root), "a"u8, Idx(in b), "existing"u8);

        Assert.AreEqual(42, root["b"]["existing"].GetInt32());
    }

    [TestMethod]
    public void MovePropertyToProperty_ComplexValueMove()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"src": {"x": [1, 2], "y": true}, "dest": {}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable dest = root["dest"];
        GetDoc(in root).MovePropertyToProperty(Idx(in root), "src"u8, Idx(in dest), "moved"u8);

        Assert.IsFalse(root.TryGetProperty("src"u8, out _));
        Assert.AreEqual(JsonValueKind.Object, root["dest"]["moved"].ValueKind);
        Assert.AreEqual(1, root["dest"]["moved"]["x"][0].GetInt32());
        Assert.AreEqual(2, root["dest"]["moved"]["x"][1].GetInt32());
        Assert.IsTrue(root["dest"]["moved"]["y"].GetBoolean());
    }

    #endregion

    #region MovePropertyToArray — Move property to array index

    [TestMethod]
    public void MovePropertyToArray_AtBeginning()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": 99, "arr": [1, 2]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        GetDoc(in root).MovePropertyToArray(Idx(in root), "val"u8, Idx(in arr), 0);

        Assert.IsFalse(root.TryGetProperty("val"u8, out _));
        Assert.AreEqual(3, root["arr"].GetArrayLength());
        Assert.AreEqual(99, root["arr"][0].GetInt32());
        Assert.AreEqual(1, root["arr"][1].GetInt32());
        Assert.AreEqual(2, root["arr"][2].GetInt32());
    }

    [TestMethod]
    public void MoveItemToArray_InMiddle()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20, 30]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        // Move item[2] (30) to index 1 (post-removal).
        GetDoc(in root).MoveItemToArray(Idx(in root), 2, Idx(in root), 1);

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(10, root[0].GetInt32());
        Assert.AreEqual(30, root[1].GetInt32());
        Assert.AreEqual(20, root[2].GetInt32());
    }

    [TestMethod]
    public void MoveItemToArray_ComplexValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"a": 1}, "hello", true]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        // Move item[0] ({"a":1}) to post-removal index 1.
        GetDoc(in root).MoveItemToArray(Idx(in root), 0, Idx(in root), 1);

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].GetString());
        Assert.AreEqual(1, root[1]["a"].GetInt32());
        Assert.IsTrue(root[2].GetBoolean());
    }

    #endregion

    #region MovePropertyToArrayEnd — Move property to end of array

    [TestMethod]
    public void MovePropertyToArrayEnd_PrimitiveValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": 42, "arr": [1, 2]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        GetDoc(in root).MovePropertyToArrayEnd(Idx(in root), "val"u8, Idx(in arr));

        Assert.IsFalse(root.TryGetProperty("val"u8, out _));
        Assert.AreEqual(3, root["arr"].GetArrayLength());
        Assert.AreEqual(1, root["arr"][0].GetInt32());
        Assert.AreEqual(2, root["arr"][1].GetInt32());
        Assert.AreEqual(42, root["arr"][2].GetInt32());
    }

    [TestMethod]
    public void MovePropertyToArrayEnd_ComplexValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj": {"x": 1}, "arr": []}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        GetDoc(in root).MovePropertyToArrayEnd(Idx(in root), "obj"u8, Idx(in arr));

        Assert.AreEqual(1, root["arr"].GetArrayLength());
        Assert.AreEqual(1, root["arr"][0]["x"].GetInt32());
    }

    [TestMethod]
    public void MovePropertyToArrayEnd_ToEmptyArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr": [], "val": "hello"}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        GetDoc(in root).MovePropertyToArrayEnd(Idx(in root), "val"u8, Idx(in arr));

        Assert.AreEqual(1, root["arr"].GetArrayLength());
        Assert.AreEqual("hello", root["arr"][0].GetString());
    }

    #endregion
    #region Move workflow (direct Move methods)

    [TestMethod]
    public void MoveWorkflow_ObjectPropertyToObjectProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"src": 42, "dest": {}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable dest = root["dest"];
        GetDoc(in root).MovePropertyToProperty(Idx(in root), "src"u8, Idx(in dest), "val"u8);

        Assert.AreEqual("""{"dest":{"val":42}}""", root.ToString());
    }

    [TestMethod]
    public void MoveWorkflow_ArrayItemToArrayItem()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3, 4, 5]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        // Move item[4] (5) to post-removal index 1.
        GetDoc(in root).MoveItemToArray(Idx(in root), 4, Idx(in root), 1);

        Assert.AreEqual("[1,5,2,3,4]", root.ToString());
    }

    [TestMethod]
    public void MoveWorkflow_ObjectPropertyToArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": "moved", "arr": [1]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        GetDoc(in root).MovePropertyToArrayEnd(Idx(in root), "val"u8, Idx(in arr));

        Assert.AreEqual("""{"arr":[1,"moved"]}""", root.ToString());
    }

    [TestMethod]
    public void MoveWorkflow_ArrayItemToObjectProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj": {}, "arr": [42, 99]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable obj = root["obj"];
        JsonElement.Mutable src_arr = root["arr"];

        GetDoc(in src_arr).MoveItemToProperty(Idx(in src_arr), 0, Idx(in obj), "x"u8);

        Assert.AreEqual(42, root["obj"]["x"].GetInt32());
        Assert.AreEqual(1, root["arr"].GetArrayLength());
        Assert.AreEqual(99, root["arr"][0].GetInt32());
    }

    [TestMethod]
    public void MoveWorkflow_ComplexObjectBetweenContainers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"source": {"nested": {"a": 1, "b": [2, 3]}}, "target": {"items": []}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable items = root["target"]["items"];
        JsonElement.Mutable src_source = root["source"];

        GetDoc(in src_source).MovePropertyToArrayEnd(Idx(in src_source), "nested"u8, Idx(in items));

        Assert.AreEqual(JsonValueKind.Object, root["source"].ValueKind);
        Assert.IsFalse(root["source"].TryGetProperty("nested"u8, out _));
        Assert.AreEqual(1, root["target"]["items"].GetArrayLength());
        Assert.AreEqual(1, root["target"]["items"][0]["a"].GetInt32());
        Assert.AreEqual(2, root["target"]["items"][0]["b"][0].GetInt32());
        Assert.AreEqual(3, root["target"]["items"][0]["b"][1].GetInt32());
    }

    #endregion
    #region Copy workflow (direct copy, no snapshot)

    [TestMethod]
    public void CopyWorkflow_PropertyToProperty_PreservesSource()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": [1, 2, 3]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source17 = root["a"];
        GetDoc(in root).CopyValueToProperty(Idx(in source17), Idx(in root), "b"u8);

        // Both are independently valid.
        JsonElement.Mutable bVal = root["b"];
        Assert.AreEqual(3, bVal.GetArrayLength());
        Assert.AreEqual(1, bVal[0].GetInt32());
        Assert.AreEqual(2, bVal[1].GetInt32());
        Assert.AreEqual(3, bVal[2].GetInt32());
        // Original "a" still references the source document values.
        JsonElement.Mutable aVal = root["a"];
        Assert.AreEqual(3, aVal.GetArrayLength());
        Assert.AreEqual(1, aVal[0].GetInt32());
        Assert.AreEqual(2, aVal[1].GetInt32());
        Assert.AreEqual(3, aVal[2].GetInt32());
    }

    [TestMethod]
    public void CopyWorkflow_PropertyToArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": 42, "arr": [1]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source18 = root["val"];
        JsonElement.Mutable target_arr = root["arr"];

        GetDoc(in target_arr).CopyValueToArrayEnd(Idx(in source18), Idx(in target_arr));

        Assert.AreEqual(42, root["val"].GetInt32()); // Source preserved.
        Assert.AreEqual(2, root["arr"].GetArrayLength());
        Assert.AreEqual(42, root["arr"][1].GetInt32());
    }

    [TestMethod]
    public void CopyWorkflow_ArrayToArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source19 = root[0];
        GetDoc(in root).CopyValueToArrayIndex(Idx(in source19), Idx(in root), 1); // Copy 10 to index 1.

        Assert.AreEqual("[10,10,20]", root.ToString());
    }

    #endregion

    #region Multiple sequential operations

    [TestMethod]
    public void MultipleCopies_SameSource()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"src": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source20 = root["src"];
        GetDoc(in root).CopyValueToProperty(Idx(in source20), Idx(in root), "a"u8);

        JsonElement.Mutable source21 = root["src"];
        GetDoc(in root).CopyValueToProperty(Idx(in source21), Idx(in root), "b"u8);

        JsonElement.Mutable source22 = root["src"];
        GetDoc(in root).CopyValueToProperty(Idx(in source22), Idx(in root), "c"u8);

        Assert.AreEqual(42, root["src"].GetInt32());
        Assert.AreEqual(42, root["a"].GetInt32());
        Assert.AreEqual(42, root["b"].GetInt32());
        Assert.AreEqual(42, root["c"].GetInt32());
    }

    [TestMethod]
    public void MultipleMoves_Sequential()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Move last item to beginning, twice.
        GetDoc(in root).MoveItemToArray(Idx(in root), 2, Idx(in root), 0);

        // Now [3, 1, 2]. Move last to beginning again.
        GetDoc(in root).MoveItemToArray(Idx(in root), 2, Idx(in root), 0);

        Assert.AreEqual("[2,3,1]", root.ToString());
    }

    [TestMethod]
    public void CopyThenMutate_IndependentValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"x": 1}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source23 = root["a"];
        GetDoc(in root).CopyValueToProperty(Idx(in source23), Idx(in root), "b"u8);

        // Mutate the copy — should not affect original.
        root["b"].SetProperty("x"u8, 999);

        Assert.AreEqual(1, root["a"]["x"].GetInt32());
        Assert.AreEqual(999, root["b"]["x"].GetInt32());
    }

    #endregion

    #region Serialization round-trip

    [TestMethod]
    [DataRow("""{"a": 1}""", """{"a":1,"b":1}""")]
    [DataRow("""{"a": "hello"}""", """{"a":"hello","b":"hello"}""")]
    [DataRow("""{"a": true}""", """{"a":true,"b":true}""")]
    [DataRow("""{"a": null}""", """{"a":null,"b":null}""")]
    [DataRow("""{"a": [1, 2]}""", """{"a":[1,2],"b":[1,2]}""")]
    [DataRow("""{"a": {"x": 1}}""", """{"a":{"x":1},"b":{"x":1}}""")]
    public void CopyPropertyFrom_SerializesCorrectly(string input, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(input);
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source24 = root["a"];
        GetDoc(in root).CopyValueToProperty(Idx(in source24), Idx(in root), "b"u8);

        Assert.AreEqual(expected, root.ToString());
    }

    [TestMethod]
    [DataRow("""[1, 2]""", """[1, 2, 1]""")]
    [DataRow("""["a", "b"]""", """["a","b","a"]""")]
    [DataRow("""[true]""", """[true,true]""")]
    [DataRow("""[[1, 2]]""", """[[1,2],[1,2]]""")]
    public void CopyItemAppendFrom_SerializesCorrectly(string input, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(input);
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source25 = root[0];
        GetDoc(in root).CopyValueToArrayEnd(Idx(in source25), Idx(in root));

        // Normalize expected by removing extra whitespace.
        string normalizedExpected = expected.Replace(" ", string.Empty);
        Assert.AreEqual(normalizedExpected, root.ToString());
    }

    #endregion

    #region Edge cases

    [TestMethod]
    public void CopyPropertyFrom_EmptyObject_AddsFirstProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"empty": {}, "val": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source26 = root["val"];
        JsonElement.Mutable target_empty = root["empty"];

        GetDoc(in target_empty).CopyValueToProperty(Idx(in source26), Idx(in target_empty), "x"u8);

        Assert.AreEqual(42, root["empty"]["x"].GetInt32());
    }

    [TestMethod]
    public void CopyItemFrom_SingleElementArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[42]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source27 = root[0];
        GetDoc(in root).CopyValueToArrayIndex(Idx(in source27), Idx(in root), 0); // Duplicate.

        Assert.AreEqual("[42,42]", root.ToString());
    }

    [TestMethod]
    public void MovePropertyToProperty_RenameInSameObject()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"only": 42}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        GetDoc(in root).MovePropertyToProperty(Idx(in root), "only"u8, Idx(in root), "renamed"u8);

        Assert.AreEqual("""{"renamed":42}""", root.ToString());
    }

    [TestMethod]
    public void CopyPropertyFrom_PropertyNameWithSpecialChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a/b": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable source28 = root["a/b"];
        GetDoc(in root).CopyValueToProperty(Idx(in source28), Idx(in root), "c/d"u8);

        Assert.AreEqual(1, root["c/d"].GetInt32());
    }

    [TestMethod]
    public void CopyPropertyFrom_LargeNestedStructure()
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
        GetDoc(in root).CopyValueToProperty(Idx(in source29), Idx(in root), "backup"u8);

        Assert.AreEqual("deep", root["backup"]["level1"]["level2"]["level3"]["name"].GetString());
        Assert.AreEqual(5, root["backup"]["level1"]["level2"]["level3"]["values"].GetArrayLength());
        Assert.IsTrue(root["backup"]["level1"]["level2"]["level3"]["flag"].GetBoolean());
        // Original still intact.
        Assert.AreEqual("deep", root["data"]["level1"]["level2"]["level3"]["name"].GetString());
    }

    #endregion

    #region Direct Move methods (MovePropertyTo*, MoveItemTo*)

    [TestMethod]
    public void MovePropertyToProperty_SimpleValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"src": {"val": 42}, "dest": {}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable dest = root["dest"];
        JsonElement.Mutable src_src = root["src"];

        GetDoc(in src_src).MovePropertyToProperty(Idx(in src_src), "val"u8, Idx(in dest), "moved"u8);

        Assert.IsFalse(root["src"].TryGetProperty("val"u8, out _));
        Assert.AreEqual(42, root["dest"]["moved"].GetInt32());
    }

    [TestMethod]
    public void MovePropertyToProperty_SamePropertyName_NoOp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"foo": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool result = GetDoc(in root).MovePropertyToProperty(Idx(in root), "foo"u8, Idx(in root), "foo"u8);

        Assert.IsTrue(result);
        Assert.AreEqual(1, root["foo"].GetInt32());
    }

    [TestMethod]
    public void MovePropertyToArray_InsertAtIndex()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": 99, "arr": [1, 2]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        GetDoc(in root).MovePropertyToArray(Idx(in root), "val"u8, Idx(in arr), 1);

        Assert.IsFalse(root.TryGetProperty("val"u8, out _));
        Assert.AreEqual(3, root["arr"].GetArrayLength());
        Assert.AreEqual(1, root["arr"][0].GetInt32());
        Assert.AreEqual(99, root["arr"][1].GetInt32());
        Assert.AreEqual(2, root["arr"][2].GetInt32());
    }

    [TestMethod]
    public void MovePropertyToArrayEnd_AppendsValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"val": "hello", "arr": [1]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];
        GetDoc(in root).MovePropertyToArrayEnd(Idx(in root), "val"u8, Idx(in arr));

        Assert.IsFalse(root.TryGetProperty("val"u8, out _));
        Assert.AreEqual(2, root["arr"].GetArrayLength());
        Assert.AreEqual("hello", root["arr"][1].GetString());
    }

    [TestMethod]
    public void MoveItemToArray_DifferentArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": [10, 20], "b": [30]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable b = root["b"];
        JsonElement.Mutable src_a = root["a"];

        GetDoc(in src_a).MoveItemToArray(Idx(in src_a), 0, Idx(in b), 0);

        Assert.AreEqual(1, root["a"].GetArrayLength());
        Assert.AreEqual(20, root["a"][0].GetInt32());
        Assert.AreEqual(2, root["b"].GetArrayLength());
        Assert.AreEqual(10, root["b"][0].GetInt32());
        Assert.AreEqual(30, root["b"][1].GetInt32());
    }

    [TestMethod]
    public void MoveItemToArray_SameArray_ForwardMove()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3, 4]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        // Move index 1 to post-removal index 3 (append).
        GetDoc(in root).MoveItemToArray(Idx(in root), 1, Idx(in root), 3);

        Assert.AreEqual("[1,3,4,2]", root.ToString());
    }

    [TestMethod]
    public void MoveItemToArray_SameArray_BackwardMove()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3, 4]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        // Move index 3 to index 1.
        GetDoc(in root).MoveItemToArray(Idx(in root), 3, Idx(in root), 1);

        Assert.AreEqual("[1,4,2,3]", root.ToString());
    }

    [TestMethod]
    public void MoveItemToArrayEnd_SameArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[1, 2, 3]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        GetDoc(in root).MoveItemToArrayEnd(Idx(in root), 0, Idx(in root));

        Assert.AreEqual("[2,3,1]", root.ToString());
    }

    [TestMethod]
    public void MoveItemToProperty_CreatesNewProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj": {}, "arr": [42, 99]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable obj = root["obj"];
        JsonElement.Mutable src_arr = root["arr"];

        GetDoc(in src_arr).MoveItemToProperty(Idx(in src_arr), 0, Idx(in obj), "x"u8);

        Assert.AreEqual(42, root["obj"]["x"].GetInt32());
        Assert.AreEqual(1, root["arr"].GetArrayLength());
        Assert.AreEqual(99, root["arr"][0].GetInt32());
    }

    [TestMethod]
    public void MovePropertyToProperty_ComplexValue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"source": {"nested": {"a": 1, "b": [2, 3]}}, "target": {}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable target = root["target"];
        JsonElement.Mutable src_source = root["source"];

        GetDoc(in src_source).MovePropertyToProperty(Idx(in src_source), "nested"u8, Idx(in target), "moved"u8);

        Assert.IsFalse(root["source"].TryGetProperty("nested"u8, out _));
        Assert.AreEqual(1, root["target"]["moved"]["a"].GetInt32());
        Assert.AreEqual(2, root["target"]["moved"]["b"].GetArrayLength());
    }

    [TestMethod]
    public void MovePropertyToProperty_ReplacesExisting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 42, "b": 99}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        GetDoc(in root).MovePropertyToProperty(Idx(in root), "a"u8, Idx(in root), "b"u8);

        Assert.IsFalse(root.TryGetProperty("a"u8, out _));
        Assert.AreEqual(42, root["b"].GetInt32());
    }

    #endregion
}
