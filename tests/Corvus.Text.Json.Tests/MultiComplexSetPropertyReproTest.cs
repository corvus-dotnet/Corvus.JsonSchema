// <copyright file="MultiComplexSetPropertyReproTest.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Corvus.Text.Json;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Reproduces a bug where setting multiple distinct properties with complex values
/// (arrays/objects from other builder documents) on the same object builder causes
/// Debug.Assert failures in the backward property search of TryGetNamedPropertyValueIndexUnsafe.
///
/// The root cause: external rows store WorkspaceDocumentId in the same bits as NumberOfRows.
/// The backward search reads NumberOfRows to skip past complex values, but for external rows
/// this field contains the workspace doc index instead of the actual row count.
/// </summary>
public class MultiComplexSetPropertyReproTest
{
    /// <summary>
    /// Basic: two array-builder values set as distinct properties on an object builder.
    /// </summary>
    [Fact]
    public void SetProperty_MultipleComplexValuesFromBuilders_Basic()
    {
        using var workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<JsonElement.Mutable> arrDoc1 = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[1,2]".AsMemory());
        using JsonDocumentBuilder<JsonElement.Mutable> arrDoc2 = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[3,4]".AsMemory());

        using JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}".AsMemory());
        JsonElement.Mutable objRoot = objDoc.RootElement;

        objRoot.SetProperty("a"u8, (JsonElement)arrDoc1.RootElement);
        objRoot.SetProperty("b"u8, (JsonElement)arrDoc2.RootElement);

        JsonElement result = objRoot.Freeze();
        Assert.Equal("[1,2]", result.GetProperty("a").GetRawText());
        Assert.Equal("[3,4]", result.GetProperty("b").GetRawText());
    }

    /// <summary>
    /// Items come from a parsed document (simulates JSONata group-by where
    /// grouped elements originate in the input JSON).
    /// </summary>
    [Fact]
    public void SetProperty_MultipleArraysWithItemsFromParsedDocument()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> inputDoc = JsonDocumentBuilder<JsonElement.Mutable>.Parse(
            workspace,
            """[{"k":"a","v":1},{"k":"a","v":2},{"k":"b","v":3},{"k":"b","v":4}]""".AsMemory());

        // Group elements by "k" value, building an array per group
        JsonDocumentBuilder<JsonElement.Mutable> arrA = JsonElement.CreateArrayBuilder(workspace, 2);
        JsonDocumentBuilder<JsonElement.Mutable> arrB = JsonElement.CreateArrayBuilder(workspace, 2);

        foreach (JsonElement item in ((JsonElement)inputDoc.RootElement).EnumerateArray())
        {
            string key = item.GetProperty("k").GetString()!;
            JsonElement val = item.GetProperty("v");
            if (key == "a")
            {
                arrA.RootElement.AddItem(val);
            }
            else
            {
                arrB.RootElement.AddItem(val);
            }
        }

        // Set both arrays as properties on an object builder
        using JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}".AsMemory());
        JsonElement.Mutable objRoot = objDoc.RootElement;

        objRoot.SetProperty("a"u8, (JsonElement)arrA.RootElement);
        objRoot.SetProperty("b"u8, (JsonElement)arrB.RootElement);

        JsonElement result = objRoot.Freeze();
        Assert.Equal("[1,2]", result.GetProperty("a").GetRawText());
        Assert.Equal("[3,4]", result.GetProperty("b").GetRawText());
    }

    /// <summary>
    /// Three or more distinct properties with complex values, exercising
    /// the backward metadata search across multiple complex entries.
    /// </summary>
    [Fact]
    public void SetProperty_ThreeComplexValuesInLoop()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> inputDoc = JsonDocumentBuilder<JsonElement.Mutable>.Parse(
            workspace,
            """{"x":[10,20],"y":[30,40],"z":[50,60]}""".AsMemory());

        using JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}".AsMemory());
        JsonElement.Mutable objRoot = objDoc.RootElement;

        // Set each parsed array as a property value, mimicking a group-by loop
        foreach (JsonProperty<JsonElement> prop in ((JsonElement)inputDoc.RootElement).EnumerateObject())
        {
            // Build a new array from the parsed array items
            JsonDocumentBuilder<JsonElement.Mutable> arrDoc = JsonElement.CreateArrayBuilder(workspace, 2);
            foreach (JsonElement item in prop.Value.EnumerateArray())
            {
                arrDoc.RootElement.AddItem(item);
            }

            objRoot.SetProperty(prop.Name, (JsonElement)arrDoc.RootElement);
        }

        JsonElement result = objRoot.Freeze();
        Assert.Equal("[10,20]", result.GetProperty("x").GetRawText());
        Assert.Equal("[30,40]", result.GetProperty("y").GetRawText());
        Assert.Equal("[50,60]", result.GetProperty("z").GetRawText());
    }

    /// <summary>
    /// Nested builders: object properties whose values are objects built from
    /// yet other builders, creating multi-level cross-builder chains.
    /// </summary>
    [Fact]
    public void SetProperty_NestedObjectBuildersAsValues()
    {
        using var workspace = JsonWorkspace.Create();

        // Build inner objects
        using JsonDocumentBuilder<JsonElement.Mutable> inner1 = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"value":1}""".AsMemory());
        using JsonDocumentBuilder<JsonElement.Mutable> inner2 = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"value":2}""".AsMemory());

        // Outer object with both inner objects as properties
        using JsonDocumentBuilder<JsonElement.Mutable> outer = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}".AsMemory());
        outer.RootElement.SetProperty("first"u8, (JsonElement)inner1.RootElement);
        outer.RootElement.SetProperty("second"u8, (JsonElement)inner2.RootElement);

        JsonElement result = outer.RootElement.Freeze();
        Assert.Equal("""{"value":1}""", result.GetProperty("first").GetRawText());
        Assert.Equal("""{"value":2}""", result.GetProperty("second").GetRawText());
    }

    /// <summary>
    /// Mix of simple and complex values — complex first, then simple, then complex again.
    /// </summary>
    [Fact]
    public void SetProperty_MixedSimpleAndComplexValues()
    {
        using var workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<JsonElement.Mutable> arr1 = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """["hello"]""".AsMemory());
        using JsonDocumentBuilder<JsonElement.Mutable> arr2 = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """["world"]""".AsMemory());

        using JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}".AsMemory());
        JsonElement.Mutable objRoot = objDoc.RootElement;

        objRoot.SetProperty("arr1"u8, (JsonElement)arr1.RootElement);
        objRoot.SetProperty("simple"u8, JsonElement.ParseValue("42"u8));
        objRoot.SetProperty("arr2"u8, (JsonElement)arr2.RootElement);

        JsonElement result = objRoot.Freeze();
        Assert.Equal("""["hello"]""", result.GetProperty("arr1").GetRawText());
        Assert.Equal("42", result.GetProperty("simple").GetRawText());
        Assert.Equal("""["world"]""", result.GetProperty("arr2").GetRawText());
    }

    /// <summary>
    /// Exact reproduction of JSONata group-by pattern using string keys.
    /// </summary>
    [Fact]
    public void SetProperty_JsonataGroupByExactPattern()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> inputDoc = JsonDocumentBuilder<JsonElement.Mutable>.Parse(
            workspace,
            """{"Account":{"Order":[{"OrderID":"order103","Product":[{"Product Name":"Bowler Hat"},{"Product Name":"Trilby hat"}]},{"OrderID":"order104","Product":[{"Product Name":"Bowler Hat"},{"Product Name":"Cloak"}]}]}}""".AsMemory());

        // Simulate group-by: group products by OrderID
        var groupData = new Dictionary<string, List<JsonElement>>();
        JsonElement account = ((JsonElement)inputDoc.RootElement).GetProperty("Account");
        foreach (JsonElement order in account.GetProperty("Order").EnumerateArray())
        {
            string orderId = order.GetProperty("OrderID").GetString()!;
            if (!groupData.TryGetValue(orderId, out var list))
            {
                list = new List<JsonElement>();
                groupData[orderId] = list;
            }

            foreach (JsonElement product in order.GetProperty("Product").EnumerateArray())
            {
                list.Add(product.GetProperty("Product Name"));
            }
        }

        // Build the result object (like ApplyGroupBy does)
        JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(workspace, groupData.Count);
        JsonElement.Mutable objRoot = objDoc.RootElement;

        foreach (var kvp in groupData)
        {
            JsonDocumentBuilder<JsonElement.Mutable> arrDoc = JsonElement.CreateArrayBuilder(workspace, kvp.Value.Count);
            JsonElement.Mutable arrRoot = arrDoc.RootElement;
            foreach (JsonElement item in kvp.Value)
            {
                arrRoot.AddItem(item);
            }

            // SetProperty with string key and builder-backed array value
            objRoot.SetProperty(kvp.Key, (JsonElement)arrRoot);
        }

        JsonElement result = objRoot.Freeze();
        Assert.Equal("""["Bowler Hat","Trilby hat"]""", result.GetProperty("order103").GetRawText());
        Assert.Equal("""["Bowler Hat","Cloak"]""", result.GetProperty("order104").GetRawText());
    }

    /// <summary>
    /// Verifies cross-builder SetProperty with many registered workspace documents.
    /// With large workspace doc indices, the backward skip overshoots entirely and
    /// exits the loop — the search returns false (property not found) which is correct
    /// for new-property insertion but masks the underlying bug.
    /// </summary>
    [Fact]
    public void SetProperty_MultipleComplexValues_LargeWorkspaceDocIndex()
    {
        using var workspace = JsonWorkspace.Create();

        // Register many dummy builders to push workspace doc indices up.
        for (int i = 0; i < 20; i++)
        {
            JsonElement.CreateArrayBuilder(workspace, 1);
        }

        using JsonDocumentBuilder<JsonElement.Mutable> inputDoc = JsonDocumentBuilder<JsonElement.Mutable>.Parse(
            workspace,
            """[{"k":"a","v":1},{"k":"a","v":2},{"k":"b","v":3},{"k":"b","v":4}]""".AsMemory());

        var groupData = new Dictionary<string, List<JsonElement>>();
        foreach (JsonElement item in ((JsonElement)inputDoc.RootElement).EnumerateArray())
        {
            string key = item.GetProperty("k").GetString()!;
            if (!groupData.TryGetValue(key, out var list))
            {
                list = new List<JsonElement>();
                groupData[key] = list;
            }

            list.Add(item.GetProperty("v"));
        }

        JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(workspace, groupData.Count);
        JsonElement.Mutable objRoot = objDoc.RootElement;

        foreach (var kvp in groupData)
        {
            JsonDocumentBuilder<JsonElement.Mutable> arrDoc = JsonElement.CreateArrayBuilder(workspace, kvp.Value.Count);
            JsonElement.Mutable arrRoot = arrDoc.RootElement;
            foreach (JsonElement val in kvp.Value)
            {
                arrRoot.AddItem(val);
            }

            objRoot.SetProperty(kvp.Key, (JsonElement)arrRoot);
        }

        JsonElement result = objRoot.Freeze();
        Assert.Equal("[1,2]", result.GetProperty("a").GetRawText());
        Assert.Equal("[3,4]", result.GetProperty("b").GetRawText());
    }

    /// <summary>
    /// With only a few registered documents, the workspace document index stored in
    /// external EndArray/EndObject rows is small enough that the backward skip lands
    /// on a wrong row WITHIN the object, triggering Debug.Assert(row.TokenType == PropertyName).
    /// </summary>
    [Fact]
    public void SetProperty_MultipleComplexValues_SmallWorkspaceDocIndex()
    {
        using var workspace = JsonWorkspace.Create();

        // Register just 2 dummy builders as padding
        JsonElement.CreateArrayBuilder(workspace, 1);
        JsonElement.CreateArrayBuilder(workspace, 1);

        using JsonDocumentBuilder<JsonElement.Mutable> inputDoc = JsonDocumentBuilder<JsonElement.Mutable>.Parse(
            workspace,
            """[{"k":"a","v":1},{"k":"a","v":2},{"k":"b","v":3},{"k":"b","v":4}]""".AsMemory());

        var groupData = new Dictionary<string, List<JsonElement>>();
        foreach (JsonElement item in ((JsonElement)inputDoc.RootElement).EnumerateArray())
        {
            string key = item.GetProperty("k").GetString()!;
            if (!groupData.TryGetValue(key, out var list))
            {
                list = new List<JsonElement>();
                groupData[key] = list;
            }

            list.Add(item.GetProperty("v"));
        }

        JsonDocumentBuilder<JsonElement.Mutable> objDoc = JsonElement.CreateObjectBuilder(workspace, groupData.Count);
        JsonElement.Mutable objRoot = objDoc.RootElement;

        foreach (var kvp in groupData)
        {
            JsonDocumentBuilder<JsonElement.Mutable> arrDoc = JsonElement.CreateArrayBuilder(workspace, kvp.Value.Count);
            JsonElement.Mutable arrRoot = arrDoc.RootElement;
            foreach (JsonElement val in kvp.Value)
            {
                arrRoot.AddItem(val);
            }

            objRoot.SetProperty(kvp.Key, (JsonElement)arrRoot);
        }

        JsonElement result = objRoot.Freeze();
        Assert.Equal("[1,2]", result.GetProperty("a").GetRawText());
        Assert.Equal("[3,4]", result.GetProperty("b").GetRawText());
    }
}
