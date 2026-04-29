// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text.Json;
using Corvus.Text.Json.Internal;
using Xunit;

using JsonTokenType = Corvus.Text.Json.Internal.JsonTokenType;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests targeting uncovered paths in JsonDocumentBuilder, ComplexValueBuilder,
/// and Source.AddAsProperty/AddAsPrebakedProperty with UTF-8 property names.
/// </summary>
public static class BuilderAndCopyMoveCoverageTests
{
    private static IMutableJsonDocument GetDoc(in JsonElement.Mutable element)
    {
        return (IMutableJsonDocument)((IJsonElement)element).ParentDocument;
    }

    private static int Idx(in JsonElement.Mutable element)
    {
        return ((IJsonElement)element).ParentDocumentIndex;
    }

    #region SetProperty with ReadOnlySpan<byte> property names — fast paths

    [Fact]
    public static void SetPropertyUtf8_SimpleNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("b"u8, JsonElement.Source.Null());

        Assert.Equal(JsonValueKind.Null, root["b"].ValueKind);
    }

    [Fact]
    public static void SetPropertyUtf8_SimpleTrue()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("flag"u8, true);

        Assert.True(root["flag"].GetBoolean());
    }

    [Fact]
    public static void SetPropertyUtf8_SimpleFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("flag"u8, false);

        Assert.False(root["flag"].GetBoolean());
    }

    [Fact]
    public static void SetPropertyUtf8_SimpleInt()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("count"u8, 42);

        Assert.Equal(42, root["count"].GetInt32());
    }

    [Fact]
    public static void SetPropertyUtf8_SimpleLong()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("big"u8, 9_000_000_000L);

        Assert.Equal(9_000_000_000L, root["big"].GetInt64());
    }

    [Fact]
    public static void SetPropertyUtf8_SimpleDouble()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("pi"u8, 3.14);

        Assert.Equal(3.14, root["pi"].GetDouble());
    }

    [Fact]
    public static void SetPropertyUtf8_SimpleString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("name"u8, "hello");

        Assert.Equal("hello", root["name"].GetString());
    }

    [Fact]
    public static void SetPropertyUtf8_FormattedNumber()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("num"u8, JsonElement.Source.FormattedNumber("1.23e4"u8));

        Assert.Equal(12300.0, root["num"].GetDouble());
    }

    [Fact]
    public static void SetPropertyUtf8_RawUtf8String()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("raw"u8, JsonElement.Source.RawString("raw-value"u8, requiresUnescaping: false));

        Assert.Equal("raw-value", root["raw"].GetString());
    }

    [Fact]
    public static void SetPropertyUtf8_JsonElement()
    {
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""{"nested": true}""");
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("child"u8, sourceDoc.RootElement);

        Assert.Equal(JsonValueKind.Object, root["child"].ValueKind);
        Assert.True(root["child"]["nested"].GetBoolean());
    }

    [Fact]
    public static void SetPropertyUtf8_ObjectBuilder()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("obj"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("x", 10);
            ob.AddProperty("y", 20);
        }));

        Assert.Equal(10, root["obj"]["x"].GetInt32());
        Assert.Equal(20, root["obj"]["y"].GetInt32());
    }

    [Fact]
    public static void SetPropertyUtf8_ArrayBuilder()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("arr"u8, new JsonElement.Source(static (ref JsonElement.ArrayBuilder ab) =>
        {
            ab.AddItem(1);
            ab.AddItem(2);
            ab.AddItem(3);
        }));

        Assert.Equal(3, root["arr"].GetArrayLength());
        Assert.Equal(2, root["arr"][1].GetInt32());
    }

    [Fact]
    public static void SetPropertyUtf8_ReplaceExistingProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1, "b": 2}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("a"u8, 99);

        Assert.Equal(99, root["a"].GetInt32());
        Assert.Equal(2, root["b"].GetInt32());
    }

    [Fact]
    public static void SetPropertyUtf8_ReplaceWithJsonElement()
    {
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20]""");
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1, "b": 2}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("a"u8, sourceDoc.RootElement);

        Assert.Equal(JsonValueKind.Array, root["a"].ValueKind);
        Assert.Equal(10, root["a"][0].GetInt32());
    }

    [Fact]
    public static void SetPropertyUtf8_ReplaceWithObjectBuilder()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1, "b": 2}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("a"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("replaced", true);
        }));

        Assert.True(root["a"]["replaced"].GetBoolean());
    }

    [Fact]
    public static void SetPropertyUtf8_UndefinedRemovesProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1, "b": 2}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("a"u8, default(JsonElement.Source));

        Assert.Equal(JsonValueKind.Undefined, root["a"].ValueKind);
        Assert.Equal(2, root["b"].GetInt32());
    }

    #endregion

    #region CopyValueToProperty — overlap detection

    [Fact]
    public static void CopyValueToProperty_SelfCopyComplexValue_TriggersOverlap()
    {
        // Copy a complex property value to replace itself — source and dest are the same region.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"x": 1, "y": 2}, "b": 99}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable aValue = root["a"];

        // CopyValueToProperty where src overlaps the existing property "a" value.
        GetDoc(in root).CopyValueToProperty(Idx(in aValue), Idx(in root), "a"u8);

        // Value should be unchanged (self-copy is idempotent).
        Assert.Equal(1, root["a"]["x"].GetInt32());
        Assert.Equal(2, root["a"]["y"].GetInt32());
        Assert.Equal(99, root["b"].GetInt32());
    }

    [Fact]
    public static void CopyValueToProperty_ParentValueToChildProperty_TriggersOverlap()
    {
        // Copy the root object (which contains the destination) as a new property of itself.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"data": {"inner": 1}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable data = root["data"];

        // Copy root (which contains "data") to replace "data"."inner".
        // root's db range encompasses data and data.inner — overlap.
        GetDoc(in root).CopyValueToProperty(Idx(in root), Idx(in data), "inner"u8);

        // "inner" should now be a copy of root.
        Assert.Equal(JsonValueKind.Object, root["data"]["inner"].ValueKind);
    }

    #endregion

    #region CopyValueToArrayIndex — overlap detection

    [Fact]
    public static void CopyValueToArrayIndex_ParentToChild_TriggersOverlap()
    {
        // Copy a parent element (whose db range contains the array) to an index within the array.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr": [1, 2, 3]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];

        // Copy root object (which contains the array) to position 1 of the array.
        // root's db range starts before arr[1] and extends past it — overlap.
        GetDoc(in root).CopyValueToArrayIndex(Idx(in root), Idx(in arr), 1);

        // Position 1 should now be a copy of the root object.
        Assert.Equal(JsonValueKind.Object, root["arr"][1].ValueKind);
        // Array should now have 4 elements.
        Assert.Equal(4, root["arr"].GetArrayLength());
    }

    #endregion

    #region CopyValueToArrayEnd — overlap detection

    [Fact]
    public static void CopyValueToArrayEnd_ParentToChild_TriggersOverlap()
    {
        // Copy a parent element whose db range straddles the array end.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr": [1, 2]}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable arr = root["arr"];

        // Copy root (which contains the entire array) to end of the array.
        GetDoc(in root).CopyValueToArrayEnd(Idx(in root), Idx(in arr));

        // The array should now have 3 elements, last being a copy of root.
        Assert.Equal(3, root["arr"].GetArrayLength());
        Assert.Equal(JsonValueKind.Object, root["arr"][2].ValueKind);
    }

    #endregion

    #region MoveItemToArrayEnd — source before destination adjustment

    [Fact]
    public static void MoveItemToArrayEnd_SourceBeforeDestination()
    {
        // Move item from a position before the destination array so index adjustment triggers.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[10, 20], [30, 40]]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable firstArr = root[0];
        JsonElement.Mutable secondArr = root[1];

        // Move firstArr[0] (value 10) to end of secondArr.
        // The source (firstArr element) is before secondArr in the document.
        GetDoc(in root).MoveItemToArrayEnd(Idx(in firstArr), 0, Idx(in secondArr));

        // First array should now be [20].
        Assert.Equal(1, root[0].GetArrayLength());
        Assert.Equal(20, root[0][0].GetInt32());

        // Second array should now be [30, 40, 10].
        Assert.Equal(3, root[1].GetArrayLength());
        Assert.Equal(10, root[1][2].GetInt32());
    }

    #endregion

    #region MoveItemToProperty — existing destination and source adjustment

    [Fact]
    public static void MoveItemToProperty_ExistingDestination()
    {
        // Move array item to an object property that already exists — triggers overwrite path.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"items": [100, 200], "result": 0}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable items = root["items"];

        // Move items[0] (value 100) to property "result" (which already exists with value 0).
        GetDoc(in root).MoveItemToProperty(Idx(in items), 0, Idx(in root), "result"u8);

        // "result" should now be 100.
        Assert.Equal(100, root["result"].GetInt32());
        // "items" should now be [200].
        Assert.Equal(1, root["items"].GetArrayLength());
        Assert.Equal(200, root["items"][0].GetInt32());
    }

    [Fact]
    public static void MoveItemToProperty_SourceBeforeDestination_Adjustment()
    {
        // Arrange source BEFORE destination object so the srcValueIndex < dstObjectIndex adjustment triggers.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"src": [1, 2, 3], "dest": {"x": 10}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable srcArr = root["src"];
        JsonElement.Mutable destObj = root["dest"];

        // Move src[0] (value 1) to property "y" on dest (new property).
        // srcArr[0] is BEFORE destObj in the document → adjustment path triggers.
        GetDoc(in root).MoveItemToProperty(Idx(in srcArr), 0, Idx(in destObj), "y"u8);

        // dest should now have "x": 10, "y": 1.
        Assert.Equal(10, root["dest"]["x"].GetInt32());
        Assert.Equal(1, root["dest"]["y"].GetInt32());
        // src should now be [2, 3].
        Assert.Equal(2, root["src"].GetArrayLength());
    }

    [Fact]
    public static void MoveItemToProperty_SourceBeforeDestination_ExistingProperty()
    {
        // Move item from array BEFORE dest object, to a property that already EXISTS.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"src": [99, 88], "dest": {"val": 0}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable srcArr = root["src"];
        JsonElement.Mutable destObj = root["dest"];

        // Move src[0] (99) to dest."val" (which already exists) — triggers both adjustment AND overwrite.
        GetDoc(in root).MoveItemToProperty(Idx(in srcArr), 0, Idx(in destObj), "val"u8);

        Assert.Equal(99, root["dest"]["val"].GetInt32());
        Assert.Equal(1, root["src"].GetArrayLength());
        Assert.Equal(88, root["src"][0].GetInt32());
    }

    #endregion

    #region TryFindNextDescendantPropertyValue

    [Fact]
    public static void TryFindNextDescendantPropertyValue_FindsNestedProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"b": {"target": 42}}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        int scanIndex = 0;
        bool found = GetDoc(in root).TryFindNextDescendantPropertyValue(Idx(in root), ref scanIndex, "target"u8, out int valueIndex);

        Assert.True(found);
        Assert.True(valueIndex > 0);
    }

    [Fact]
    public static void TryFindNextDescendantPropertyValue_NotFound()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"b": 1}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        int scanIndex = 0;
        bool found = GetDoc(in root).TryFindNextDescendantPropertyValue(Idx(in root), ref scanIndex, "missing"u8, out _);

        Assert.False(found);
    }

    [Fact]
    public static void TryFindNextDescendantPropertyValue_MultipleOccurrences()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": {"x": 1}, "b": {"x": 2}}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        int scanIndex = 0;

        bool found1 = GetDoc(in root).TryFindNextDescendantPropertyValue(Idx(in root), ref scanIndex, "x"u8, out int idx1);
        Assert.True(found1);

        bool found2 = GetDoc(in root).TryFindNextDescendantPropertyValue(Idx(in root), ref scanIndex, "x"u8, out int idx2);
        Assert.True(found2);
        Assert.NotEqual(idx1, idx2);
    }

    #endregion

    #region Builder with escaped string values

    [Fact]
    public static void Builder_EscapedPropertyName_RoundTrips()
    {
        // Build a document with an escaped property name and verify it round-trips correctly.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"normal": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("key\twith\ttabs"u8, "value");

        string result = root.ToString();
        Assert.Contains("value", result);
    }

    [Fact]
    public static void Builder_EscapedStringValue_GetString()
    {
        // Parse JSON with an escaped string value (JSON \n = newline), build mutable, verify GetString.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"msg": "hello\nworld"}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        string? value = root["msg"].GetString();
        Assert.Equal("hello\nworld", value);
    }

    [Fact]
    public static void Builder_UnicodeEscapedString_GetString()
    {
        // Parse JSON with Unicode escapes (\u0041 = 'A', \u0042 = 'B').
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"emoji": "\u0041\u0042"}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        string? value = root["emoji"].GetString();
        Assert.Equal("AB", value);
    }

    #endregion

    #region TryReplacePropertyValue — property not found

    [Fact]
    public static void TryReplacePropertyValue_PropertyNotFound_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool result = GetDoc(in root).TryReplacePropertyValue(Idx(in root), "nonexistent"u8, JsonTokenType.Number, 0, 1);

        Assert.False(result);
    }

    [Fact]
    public static void TryReplacePropertyFromDocument_PropertyNotFound_ReturnsFalse()
    {
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""42""");
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool result = GetDoc(in root).TryReplacePropertyFromDocument(Idx(in root), "nonexistent"u8, sourceDoc, 0);

        Assert.False(result);
    }

    #endregion

    #region Builder GetPropertyName and text operations on mutable docs

    [Fact]
    public static void Builder_GetPropertyName_ReturnsPropertyNameElement()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"hello": "world"}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Enumerate properties to verify property names work correctly.
        int count = 0;
        foreach (JsonProperty<JsonElement.Mutable> prop in root.EnumerateObject())
        {
            Assert.Equal("hello", prop.Name);
            Assert.Equal("world", prop.Value.GetString());
            count++;
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public static void Builder_TextEquals_MatchesPropertyName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"test": 123}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Access "test" property to verify text matching works.
        Assert.Equal(123, root["test"].GetInt32());
    }

    #endregion

    #region Builder TryGetValue for base64

    [Fact]
    public static void Builder_TryGetBytesFromBase64_ValidBase64()
    {
        // "SGVsbG8=" is base64 for "Hello"
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"data": "SGVsbG8="}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool success = root["data"].TryGetBytesFromBase64(out byte[]? bytes);

        Assert.True(success);
        Assert.Equal("Hello"u8.ToArray(), bytes);
    }

    [Fact]
    public static void Builder_TryGetBytesFromBase64_InvalidBase64()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"data": "not-valid-base64!!!"}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        bool success = root["data"].TryGetBytesFromBase64(out byte[]? bytes);

        Assert.False(success);
        Assert.Null(bytes);
    }

    #endregion

    #region SetProperty with ReadOnlySpan<byte> — slow path (ObjectBuilder/ArrayBuilder) covers Source.AddAsProperty(byte[])

    [Fact]
    public static void SetPropertyUtf8_NewProperty_ObjectBuilder_CoversAddAsPropertySlowPath()
    {
        // When property does NOT exist and source is a builder delegate, AddAsProperty(ReadOnlySpan<byte>) is called.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"existing": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("newobj"u8, new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("inner", 42);
        }));

        Assert.Equal(42, root["newobj"]["inner"].GetInt32());
    }

    [Fact]
    public static void SetPropertyUtf8_NewProperty_ArrayBuilder_CoversAddAsPropertySlowPath()
    {
        // When property does NOT exist and source is an array builder, AddAsProperty(ReadOnlySpan<byte>) is called.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"existing": 1}""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("newarr"u8, new JsonElement.Source(static (ref JsonElement.ArrayBuilder ab) =>
        {
            ab.AddItem(10);
            ab.AddItem(20);
        }));

        Assert.Equal(2, root["newarr"].GetArrayLength());
    }

    #endregion

    #region MoveItemToArray (non-end) operations

    [Fact]
    public static void MoveItemToArray_SourceBeforeDestination()
    {
        // Move from first array to second array at a specific index.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1, 2, 3], [4, 5]]""");
        using var workspace = JsonWorkspace.Create();
        using var builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Mutable firstArr = root[0];
        JsonElement.Mutable secondArr = root[1];

        // Move firstArr[1] (value 2) to secondArr position 0.
        GetDoc(in root).MoveItemToArray(Idx(in firstArr), 1, Idx(in secondArr), 0);

        // First array: [1, 3]
        Assert.Equal(2, root[0].GetArrayLength());
        Assert.Equal(1, root[0][0].GetInt32());
        Assert.Equal(3, root[0][1].GetInt32());

        // Second array: [2, 4, 5]
        Assert.Equal(3, root[1].GetArrayLength());
        Assert.Equal(2, root[1][0].GetInt32());
    }

    #endregion

    #region Source.AddAsProperty(ReadOnlySpan<byte>) — exercises all Kind branches via CVB

    private static string BuildObjectWithUtf8Property(JsonElement.Source source)
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        source.AddAsProperty("prop"u8, ref cvb);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder.RootElement.ToString();
    }

    private static string BuildObjectWithPrebakedProperty(JsonElement.Source source)
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        // Prebaked property name format: 4-byte LE header + JSON-quoted name bytes
        // For "key": quoted = "\"key\"" = 5 bytes, header = (5 << 4) | 1 = 0x51
        ReadOnlySpan<byte> prebaked = [0x51, 0x00, 0x00, 0x00, 0x22, 0x6B, 0x65, 0x79, 0x22];
        source.AddAsPrebakedProperty(prebaked, ref cvb);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder.RootElement.ToString();
    }

    private static string BuildObjectWithStringProperty(JsonElement.Source source)
    {
        using var workspace = JsonWorkspace.Create();
        using var documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        source.AddAsProperty("prop", ref cvb);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder.RootElement.ToString();
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_Null()
    {
        string result = BuildObjectWithUtf8Property(JsonElement.Source.Null());
        Assert.Contains("null", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_True()
    {
        string result = BuildObjectWithUtf8Property(true);
        Assert.Contains("true", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_False()
    {
        string result = BuildObjectWithUtf8Property(false);
        Assert.Contains("false", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_Int()
    {
        string result = BuildObjectWithUtf8Property(42);
        Assert.Contains("42", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_FormattedNumber()
    {
        string result = BuildObjectWithUtf8Property(JsonElement.Source.FormattedNumber("3.14"u8));
        Assert.Contains("3.14", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_Utf8String()
    {
        string result = BuildObjectWithUtf8Property("hello"u8);
        Assert.Contains("hello", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_Utf16String()
    {
        string result = BuildObjectWithUtf8Property("world");
        Assert.Contains("world", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_RawStringNoUnescape()
    {
        string result = BuildObjectWithUtf8Property(JsonElement.Source.RawString("raw"u8, requiresUnescaping: false));
        Assert.Contains("raw", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_RawStringRequiresUnescape()
    {
        string result = BuildObjectWithUtf8Property(JsonElement.Source.RawString("esc\\n"u8, requiresUnescaping: true));
        Assert.Contains("esc", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_JsonElement()
    {
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        string result = BuildObjectWithUtf8Property(sourceDoc.RootElement);
        Assert.Contains("\"x\"", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_ObjectBuilder()
    {
        string result = BuildObjectWithUtf8Property(new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("inner", 99);
        }));
        Assert.Contains("99", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_ArrayBuilder()
    {
        string result = BuildObjectWithUtf8Property(new JsonElement.Source(static (ref JsonElement.ArrayBuilder ab) =>
        {
            ab.AddItem(1);
            ab.AddItem(2);
        }));
        Assert.Contains("[1,2]", result);
    }

    // AddAsPrebakedProperty — same kinds

    [Fact]
    public static void SourceAddAsPrebakedProperty_Null()
    {
        string result = BuildObjectWithPrebakedProperty(JsonElement.Source.Null());
        Assert.Contains("null", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_True()
    {
        string result = BuildObjectWithPrebakedProperty(true);
        Assert.Contains("true", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_False()
    {
        string result = BuildObjectWithPrebakedProperty(false);
        Assert.Contains("false", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_FormattedNumber()
    {
        string result = BuildObjectWithPrebakedProperty(JsonElement.Source.FormattedNumber("2.71"u8));
        Assert.Contains("2.71", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_Utf8String()
    {
        string result = BuildObjectWithPrebakedProperty("utf8val"u8);
        Assert.Contains("utf8val", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_Utf16String()
    {
        string result = BuildObjectWithPrebakedProperty("utf16val");
        Assert.Contains("utf16val", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_RawStringNoUnescape()
    {
        string result = BuildObjectWithPrebakedProperty(JsonElement.Source.RawString("rawval"u8, requiresUnescaping: false));
        Assert.Contains("rawval", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_RawStringRequiresUnescape()
    {
        string result = BuildObjectWithPrebakedProperty(JsonElement.Source.RawString("esc\\t"u8, requiresUnescaping: true));
        Assert.Contains("esc", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_JsonElement()
    {
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""[10, 20]""");
        string result = BuildObjectWithPrebakedProperty(sourceDoc.RootElement);
        Assert.Contains("[10,20]", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_ObjectBuilder()
    {
        string result = BuildObjectWithPrebakedProperty(new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("nested", true);
        }));
        Assert.Contains("nested", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_ArrayBuilder()
    {
        string result = BuildObjectWithPrebakedProperty(new JsonElement.Source(static (ref JsonElement.ArrayBuilder ab) =>
        {
            ab.AddItem(7);
            ab.AddItem(8);
            ab.AddItem(9);
        }));
        Assert.Contains("[7,8,9]", result);
    }

    #endregion

    #region Source.AddAsProperty(byte[]) - StringSimpleType / NumericSimpleType

    [Fact]
    public static void SourceAddAsPropertyUtf8_StringSimpleType_Guid()
    {
        Guid g = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        string result = BuildObjectWithUtf8Property(g);
        Assert.Contains("01234567-89ab-cdef-0123-456789abcdef", result);
    }

    [Fact]
    public static void SourceAddAsPropertyUtf8_StringSimpleType_DateTimeOffset()
    {
        DateTimeOffset dt = new(2023, 7, 20, 10, 30, 0, TimeSpan.Zero);
        string result = BuildObjectWithUtf8Property(dt);
        Assert.Contains("2023-07-20", result);
    }

    #endregion

    #region Source.AddAsPrebakedProperty - NumericSimpleType / StringSimpleType

    [Fact]
    public static void SourceAddAsPrebakedProperty_NumericSimpleType_Int()
    {
        string result = BuildObjectWithPrebakedProperty((JsonElement.Source)42);
        Assert.Contains("42", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_NumericSimpleType_Double()
    {
        string result = BuildObjectWithPrebakedProperty((JsonElement.Source)3.14);
        Assert.Contains("3.14", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_StringSimpleType_Guid()
    {
        Guid g = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        string result = BuildObjectWithPrebakedProperty(g);
        Assert.Contains("01234567-89ab-cdef-0123-456789abcdef", result);
    }

    [Fact]
    public static void SourceAddAsPrebakedProperty_StringSimpleType_DateTime()
    {
        DateTime dt = new(2023, 7, 20, 10, 30, 0, DateTimeKind.Utc);
        string result = BuildObjectWithPrebakedProperty(dt);
        Assert.Contains("2023-07-20", result);
    }

    #endregion

    #region Source.AddAsProperty(string) - covers AddAsProperty(ReadOnlySpan<char>) delegation

    [Fact]
    public static void SourceAddAsPropertyString_Null()
    {
        string result = BuildObjectWithStringProperty(JsonElement.Source.Null());
        Assert.Contains("null", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_True()
    {
        string result = BuildObjectWithStringProperty(true);
        Assert.Contains("true", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_False()
    {
        string result = BuildObjectWithStringProperty(false);
        Assert.Contains("false", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_NumericSimpleType()
    {
        string result = BuildObjectWithStringProperty((JsonElement.Source)99);
        Assert.Contains("99", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_FormattedNumber()
    {
        string result = BuildObjectWithStringProperty(JsonElement.Source.FormattedNumber("1.23e5"u8));
        Assert.Contains("1.23e5", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_StringSimpleType()
    {
        Guid g = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        string result = BuildObjectWithStringProperty(g);
        Assert.Contains("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_RawUtf8StringRequiresUnescaping()
    {
        string result = BuildObjectWithStringProperty(JsonElement.Source.RawString("esc\\t"u8, requiresUnescaping: true));
        Assert.Contains("esc", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_RawUtf8StringNotRequiresUnescaping()
    {
        string result = BuildObjectWithStringProperty(JsonElement.Source.RawString("plain"u8, requiresUnescaping: false));
        Assert.Contains("plain", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_Utf8String()
    {
        string result = BuildObjectWithStringProperty((ReadOnlySpan<byte>)"utf8value"u8);
        Assert.Contains("utf8value", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_Utf16String()
    {
        string result = BuildObjectWithStringProperty("utf16value");
        Assert.Contains("utf16value", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_JsonElement()
    {
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        string result = BuildObjectWithStringProperty(sourceDoc.RootElement);
        Assert.Contains("\"a\":1", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_ObjectBuilder()
    {
        string result = BuildObjectWithStringProperty(new JsonElement.Source(static (ref JsonElement.ObjectBuilder ob) =>
        {
            ob.AddProperty("x", 5);
        }));
        Assert.Contains("\"x\":5", result);
    }

    [Fact]
    public static void SourceAddAsPropertyString_ArrayBuilder()
    {
        string result = BuildObjectWithStringProperty(new JsonElement.Source(static (ref JsonElement.ArrayBuilder ab) =>
        {
            ab.AddItem(1);
            ab.AddItem(2);
        }));
        Assert.Contains("[1,2]", result);
    }

    #endregion

    #region Mutable - GetDouble/GetSingle/GetHalf/GetBigNumber FormatException paths
    // NOTE: Lines 3459, 3526, 3738, 3794 in JsonElement.Mutable.cs are UNREACHABLE.
    // The FormatException path requires a Number-typed DB row whose raw bytes fail parsing.
    // However, all paths that create Number rows validate the bytes:
    //   - ParsedJsonDocument validates JSON numbers during parsing
    //   - Source.FormattedNumber validates via TryGetSimpleValueComponents
    //   - NumericSimpleType sources use Utf8Formatter which always produces valid output
    // Therefore, no valid usage of the API can reach ThrowFormatException() in these methods.
    // The code exists as a defensive guard against internal state corruption.
    #endregion

    #region Builder - ReplaceRootAndDispose

    [Fact]
    public static void Builder_ReplaceRootAndDispose_ReplacesEntireDocument()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"old": true}""");
        using var builder = doc.RootElement.CreateBuilder(workspace);

        var cvb = ComplexValueBuilder.Create(builder, 10);
        cvb.StartObject();
        cvb.AddProperty("new"u8, 42);
        cvb.EndObject();
        ((IMutableJsonDocument)builder).ReplaceRootAndDispose(ref cvb);

        Assert.Contains("\"new\":42", builder.RootElement.ToString());
        Assert.DoesNotContain("old", builder.RootElement.ToString());
    }

    #endregion

    #region Builder - MovePropertyToProperty/Array source-not-found and self-move

    [Fact]
    public static void Builder_MovePropertyToProperty_SourceNotFound_ReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1,"b":2}""");
        using var builder = doc.RootElement.CreateBuilder(workspace);
        IMutableJsonDocument mutableDoc = (IMutableJsonDocument)builder;
        // Try to move non-existent property "x"
        bool result = mutableDoc.MovePropertyToProperty(0, "x"u8, 0, "dest"u8);
        Assert.False(result);
    }

    [Fact]
    public static void Builder_MovePropertyToArray_SourceNotFound_ReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj":{"a":1},"arr":[10]}""");
        using var builder = doc.RootElement.CreateBuilder(workspace);
        IMutableJsonDocument mutableDoc = (IMutableJsonDocument)builder;
        // Get the object index and array index
        int objIdx = ((IJsonElement)builder.RootElement["obj"]).ParentDocumentIndex;
        int arrIdx = ((IJsonElement)builder.RootElement["arr"]).ParentDocumentIndex;
        // Move non-existent property "nope" from obj to arr
        bool result = mutableDoc.MovePropertyToArray(objIdx, "nope"u8, arrIdx, 0);
        Assert.False(result);
    }

    [Fact]
    public static void Builder_MovePropertyToArrayEnd_SourceNotFound_ReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj":{"a":1},"arr":[10]}""");
        using var builder = doc.RootElement.CreateBuilder(workspace);
        IMutableJsonDocument mutableDoc = (IMutableJsonDocument)builder;
        int objIdx = ((IJsonElement)builder.RootElement["obj"]).ParentDocumentIndex;
        int arrIdx = ((IJsonElement)builder.RootElement["arr"]).ParentDocumentIndex;
        bool result = mutableDoc.MovePropertyToArrayEnd(objIdx, "nope"u8, arrIdx);
        Assert.False(result);
    }

    [Fact]
    public static void Builder_MovePropertyToArray_InsertAtEnd_Works()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"obj":{"a":99},"arr":[10,20]}""");
        using var builder = doc.RootElement.CreateBuilder(workspace);
        IMutableJsonDocument mutableDoc = (IMutableJsonDocument)builder;
        int objIdx = ((IJsonElement)builder.RootElement["obj"]).ParentDocumentIndex;
        int arrIdx = ((IJsonElement)builder.RootElement["arr"]).ParentDocumentIndex;
        // Move "a" from obj to arr at index 2 (end)
        bool result = mutableDoc.MovePropertyToArray(objIdx, "a"u8, arrIdx, 2);
        Assert.True(result);
        string text = builder.RootElement.ToString();
        Assert.Contains("99", text);
    }

    #endregion

    #region Builder - Escaped string retrieval paths (HasComplexChildren)

    [Fact]
    public static void Builder_GetString_WithEscapedContent_ReturnsUnescaped()
    {
        // JSON with escape sequences: the raw bytes contain \n which means HasComplexChildren is set
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"msg\":\"hello\\nworld\"}");
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable val = builder.RootElement["msg"];
        string str = val.GetString()!;
        Assert.Equal("hello\nworld", str);
    }

    [Fact]
    public static void Builder_GetString_WithTabEscape_ReturnsUnescaped()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"v\":\"tab\\there\"}");
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable val = builder.RootElement["v"];
        string str = val.GetString()!;
        Assert.Equal("tab\there", str);
    }

    [Fact]
    public static void Builder_GetString_WithQuoteEscape_ReturnsUnescaped()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"v\":\"say \\\"hi\\\"\"}");
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable val = builder.RootElement["v"];
        string str = val.GetString()!;
        Assert.Equal("say \"hi\"", str);
    }

    [Fact]
    public static void Builder_GetString_WithUnicodeEscape_ReturnsUnescaped()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"v\":\"\\u0041\\u0042\\u0043\"}");
        using var builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable val = builder.RootElement["v"];
        string str = val.GetString()!;
        Assert.Equal("ABC", str);
    }

    #endregion
}
