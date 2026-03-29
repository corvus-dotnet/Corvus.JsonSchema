// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for tuple types composed via allOf alongside additional constraints
/// such as contains, items, or unevaluatedItems.
/// Covers the closed/open matrix for composed vs local type constraints.
/// </summary>
public class GeneratedComposedTupleTests
{
    #region RefTupleWithContains — closed composed tuple (items:false) + contains — pure tuple

    [Fact]
    public void ClosedComposed_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");

        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void ClosedComposed_IndexAccess_ReturnsJsonElement()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");

        Assert.Equal("hello", doc.RootElement[0].ToString());
        Assert.Equal("42", doc.RootElement[1].ToString());
    }

    [Fact]
    public void ClosedComposed_TryGetAsAllOf0Array_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");

        Assert.True(doc.RootElement.TryGetAsAllOf0Array(out RefTupleWithContains.AllOf0Array allOf0));

        var item0 =
            JsonString.From(allOf0[0]);
        Assert.Equal("hello", (string)item0);

        var item1 =
            JsonInt32.From(allOf0[1]);
        Assert.Equal(42, (int)item1);
    }

    [Fact]
    public void ClosedComposed_EnumerateArray()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");

        int count = 0;
        foreach (JsonElement item in doc.RootElement.EnumerateArray())
        {
            count++;
        }

        Assert.Equal(2, count);
    }

    [Fact]
    public void ClosedComposed_Build_CreateTuple()
    {
        using var workspace = JsonWorkspace.Create();

        RefTupleWithContains.Source source = RefTupleWithContains.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("world", 99);
            });

        using JsonDocumentBuilder<RefTupleWithContains.Mutable> doc =
            RefTupleWithContains.CreateBuilder(workspace, source);
        RefTupleWithContains.Mutable root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("world", root[0].ToString());
        Assert.Equal("99", root[1].ToString());
    }

    [Fact]
    public void ClosedComposed_Build_CreateTuple_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        RefTupleWithContains.Source source = RefTupleWithContains.Build(
            static (ref b) =>
            {
                b.CreateTuple("hello", 42);
            });

        using JsonDocumentBuilder<RefTupleWithContains.Mutable> doc =
            RefTupleWithContains.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();
        Assert.Contains("hello", json);
        Assert.Contains("42", json);

        using var reparsed =
            ParsedJsonDocument<RefTupleWithContains>.Parse(json);
        Assert.Equal(2, reparsed.RootElement.GetArrayLength());
        Assert.Equal("hello", reparsed.RootElement[0].ToString());
        Assert.Equal("42", reparsed.RootElement[1].ToString());
    }

    [Fact]
    public void ClosedComposed_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<RefTupleWithContains>.Parse(json);
        Assert.Equal(2, roundTrip.RootElement.GetArrayLength());
        Assert.Equal("hello", roundTrip.RootElement[0].ToString());
        Assert.Equal("42", roundTrip.RootElement[1].ToString());
    }

    #endregion

    #region AllOfOpenTupleClosedLocally — open composed + items:false locally — pure tuple

    [Fact]
    public void OpenComposedClosedLocally_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse("""["hello",3.14]""");

        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void OpenComposedClosedLocally_IndexAccess_ReturnsJsonElement()
    {
        using var doc =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse("""["hello",3.14]""");

        Assert.Equal("hello", doc.RootElement[0].ToString());
        Assert.Equal("3.14", doc.RootElement[1].ToString());
    }

    [Fact]
    public void OpenComposedClosedLocally_TryGetAsAllOf0Array_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse("""["hello",3.14]""");

        Assert.True(doc.RootElement.TryGetAsAllOf0Array(out AllOfOpenTupleClosedLocally.AllOf0Array allOf0));

        var item0 =
            JsonString.From(allOf0[0]);
        Assert.Equal("hello", (string)item0);

        var item1 =
            JsonNumber.From(allOf0[1]);
        Assert.Equal(3.14, (double)item1, 2);
    }

    [Fact]
    public void OpenComposedClosedLocally_Build_CreateTuple()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfOpenTupleClosedLocally.Source source = AllOfOpenTupleClosedLocally.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("world", 2.718);
            });

        using JsonDocumentBuilder<AllOfOpenTupleClosedLocally.Mutable> doc =
            AllOfOpenTupleClosedLocally.CreateBuilder(workspace, source);
        AllOfOpenTupleClosedLocally.Mutable root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("world", root[0].ToString());
        Assert.Equal("2.718", root[1].ToString());
    }

    [Fact]
    public void OpenComposedClosedLocally_Build_CreateTuple_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfOpenTupleClosedLocally.Source source = AllOfOpenTupleClosedLocally.Build(
            static (ref b) =>
            {
                b.CreateTuple("hello", 1.5);
            });

        using JsonDocumentBuilder<AllOfOpenTupleClosedLocally.Mutable> doc =
            AllOfOpenTupleClosedLocally.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse(json);
        Assert.Equal(2, reparsed.RootElement.GetArrayLength());
        Assert.Equal("hello", reparsed.RootElement[0].ToString());
        Assert.Equal("1.5", reparsed.RootElement[1].ToString());
    }

    [Fact]
    public void OpenComposedClosedLocally_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse("""["hello",3.14]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse(json);
        Assert.Equal(2, roundTrip.RootElement.GetArrayLength());
        Assert.Equal("hello", roundTrip.RootElement[0].ToString());
        Assert.Equal("3.14", roundTrip.RootElement[1].ToString());
    }

    #endregion

    #region RefTupleWithAdditionalItems — open composed + items:boolean — tuple with additional items

    [Fact]
    public void OpenComposedWithItems_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        Assert.Equal(4, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void OpenComposedWithItems_IndexAccess_ReturnsItemsEntity()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        Assert.Equal("hello", doc.RootElement[0].ToString());
        Assert.Equal("42", doc.RootElement[1].ToString());
        Assert.Equal("True", doc.RootElement[2].ToString());
        Assert.Equal("False", doc.RootElement[3].ToString());
    }

    [Fact]
    public void OpenComposedWithItems_TryGetAsAllOf0Array_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true]""");

        Assert.True(doc.RootElement.TryGetAsAllOf0Array(out RefTupleWithAdditionalItems.AllOf0Array allOf0));

        var item0 =
            JsonString.From(allOf0[0]);
        Assert.Equal("hello", (string)item0);

        var item1 =
            JsonInt32.From(allOf0[1]);
        Assert.Equal(42, (int)item1);
    }

    [Fact]
    public void OpenComposedWithItems_EnumerateArray()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        int count = 0;
        foreach (JsonBoolean item in doc.RootElement.EnumerateArray())
        {
            count++;
        }

        Assert.Equal(4, count);
    }

    [Fact]
    public void OpenComposedWithItems_Build_CreateTupleThenAdd()
    {
        using var workspace = JsonWorkspace.Create();

        RefTupleWithAdditionalItems.Source source = RefTupleWithAdditionalItems.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("hello", 42);
                builder.AddItem(true);
                builder.AddItem(false);
            });

        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> doc =
            RefTupleWithAdditionalItems.CreateBuilder(workspace, source);
        RefTupleWithAdditionalItems.Mutable root = doc.RootElement;

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal("hello", root[0].ToString());
        Assert.Equal("42", root[1].ToString());
        Assert.Equal("True", root[2].ToString());
        Assert.Equal("False", root[3].ToString());
    }

    [Fact]
    public void OpenComposedWithItems_Build_CreateTupleOnly()
    {
        using var workspace = JsonWorkspace.Create();

        RefTupleWithAdditionalItems.Source source = RefTupleWithAdditionalItems.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("world", 99);
            });

        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> doc =
            RefTupleWithAdditionalItems.CreateBuilder(workspace, source);
        RefTupleWithAdditionalItems.Mutable root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("world", root[0].ToString());
        Assert.Equal("99", root[1].ToString());
    }

    [Fact]
    public void OpenComposedWithItems_Build_AddBeforeCreateTuple_Throws()
    {
        using var workspace = JsonWorkspace.Create();

        RefTupleWithAdditionalItems.Source source = RefTupleWithAdditionalItems.Build(
            static (ref builder) =>
            {
                builder.AddItem(true);
            });

        try
        {
            using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> doc =
                RefTupleWithAdditionalItems.CreateBuilder(workspace, source);

            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected: must call CreateTuple before Add
        }
    }

    [Fact]
    public void OpenComposedWithItems_Build_CreateTuple_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        RefTupleWithAdditionalItems.Source source = RefTupleWithAdditionalItems.Build(
            static (ref b) =>
            {
                b.CreateTuple("hello", 42);
                b.AddItem(true);
            });

        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> doc =
            RefTupleWithAdditionalItems.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse(json);
        Assert.Equal(3, reparsed.RootElement.GetArrayLength());
        Assert.Equal("hello", reparsed.RootElement[0].ToString());
        Assert.Equal("42", reparsed.RootElement[1].ToString());
        Assert.Equal("True", reparsed.RootElement[2].ToString());
    }

    [Fact]
    public void OpenComposedWithItems_Mutable_SetItem()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefTupleWithAdditionalItems.Mutable root = builderDoc.RootElement;
        root.SetItem(2, false);

        Assert.Equal("False", root[2].ToString());
    }

    [Fact]
    public void OpenComposedWithItems_Mutable_InsertItem()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefTupleWithAdditionalItems.Mutable root = builderDoc.RootElement;
        root.InsertItem(2, false);

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal("False", root[2].ToString());
        Assert.Equal("True", root[3].ToString());
    }

    [Fact]
    public void OpenComposedWithItems_Mutable_Remove()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefTupleWithAdditionalItems.Mutable root = builderDoc.RootElement;
        root.RemoveAt(3);

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("True", root[2].ToString());
    }

    [Fact]
    public void OpenComposedWithItems_Mutable_SetItemUndefined_Removes()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefTupleWithAdditionalItems.Mutable root = builderDoc.RootElement;
        root.SetItem(3, default);

        Assert.Equal(3, root.GetArrayLength());
    }

    [Fact]
    public void OpenComposedWithItems_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse(json);
        Assert.Equal(4, roundTrip.RootElement.GetArrayLength());
        Assert.Equal("hello", roundTrip.RootElement[0].ToString());
        Assert.Equal("42", roundTrip.RootElement[1].ToString());
        Assert.Equal("True", roundTrip.RootElement[2].ToString());
        Assert.Equal("False", roundTrip.RootElement[3].ToString());
    }

    #endregion

    #region AllOfInlineTupleWithUnevaluated — open composed + unevaluatedItems:boolean — tuple with additional items

    [Fact]
    public void OpenComposedWithUnevaluated_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true]""");

        Assert.Equal(3, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void OpenComposedWithUnevaluated_IndexAccess_ReturnsUnevaluatedItemsEntity()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true,false]""");

        Assert.Equal("hello", doc.RootElement[0].ToString());
        Assert.Equal("3.14", doc.RootElement[1].ToString());
        Assert.Equal("True", doc.RootElement[2].ToString());
        Assert.Equal("False", doc.RootElement[3].ToString());
    }

    [Fact]
    public void OpenComposedWithUnevaluated_TryGetAsAllOf0Entity_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true]""");

        Assert.True(doc.RootElement.TryGetAsAllOf0Entity(out AllOfInlineTupleWithUnevaluated.AllOf0Entity entity));

        var item0 =
            JsonString.From(entity[0]);
        Assert.Equal("hello", (string)item0);

        var item1 =
            JsonNumber.From(entity[1]);
        Assert.Equal(3.14, (double)item1, 2);
    }

    [Fact]
    public void OpenComposedWithUnevaluated_EnumerateArray()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true,false]""");

        int count = 0;
        foreach (JsonBoolean item in doc.RootElement.EnumerateArray())
        {
            count++;
        }

        Assert.Equal(4, count);
    }

    [Fact]
    public void OpenComposedWithUnevaluated_Build_CreateTupleThenAdd()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfInlineTupleWithUnevaluated.Source source = AllOfInlineTupleWithUnevaluated.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("hello", 3.14);
                builder.AddItem(true);
                builder.AddItem(false);
            });

        using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> doc =
            AllOfInlineTupleWithUnevaluated.CreateBuilder(workspace, source);
        AllOfInlineTupleWithUnevaluated.Mutable root = doc.RootElement;

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal("hello", root[0].ToString());
        Assert.Equal("3.14", root[1].ToString());
        Assert.Equal("True", root[2].ToString());
        Assert.Equal("False", root[3].ToString());
    }

    [Fact]
    public void OpenComposedWithUnevaluated_Build_CreateTupleOnly()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfInlineTupleWithUnevaluated.Source source = AllOfInlineTupleWithUnevaluated.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("world", 2.718);
            });

        using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> doc =
            AllOfInlineTupleWithUnevaluated.CreateBuilder(workspace, source);
        AllOfInlineTupleWithUnevaluated.Mutable root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("world", root[0].ToString());
        Assert.Equal("2.718", root[1].ToString());
    }

    [Fact]
    public void OpenComposedWithUnevaluated_Build_AddBeforeCreateTuple_Throws()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfInlineTupleWithUnevaluated.Source source = AllOfInlineTupleWithUnevaluated.Build(
            static (ref builder) =>
            {
                builder.AddItem(true);
            });

        try
        {
            using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> doc =
                AllOfInlineTupleWithUnevaluated.CreateBuilder(workspace, source);

            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected: must call CreateTuple before Add
        }
    }

    [Fact]
    public void OpenComposedWithUnevaluated_Build_CreateTuple_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfInlineTupleWithUnevaluated.Source source = AllOfInlineTupleWithUnevaluated.Build(
            static (ref b) =>
            {
                b.CreateTuple("hello", 3.14);
                b.AddItem(true);
            });

        using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> doc =
            AllOfInlineTupleWithUnevaluated.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse(json);
        Assert.Equal(3, reparsed.RootElement.GetArrayLength());
        Assert.Equal("hello", reparsed.RootElement[0].ToString());
        Assert.Equal("3.14", reparsed.RootElement[1].ToString());
        Assert.Equal("True", reparsed.RootElement[2].ToString());
    }

    [Fact]
    public void OpenComposedWithUnevaluated_Mutable_SetItem()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfInlineTupleWithUnevaluated.Mutable root = builderDoc.RootElement;
        root.SetItem(2, false);

        Assert.Equal("False", root[2].ToString());
    }

    [Fact]
    public void OpenComposedWithUnevaluated_Mutable_InsertItem()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfInlineTupleWithUnevaluated.Mutable root = builderDoc.RootElement;
        root.InsertItem(2, true);

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("True", root[2].ToString());
    }

    [Fact]
    public void OpenComposedWithUnevaluated_Mutable_Remove()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfInlineTupleWithUnevaluated.Mutable root = builderDoc.RootElement;
        root.RemoveAt(3);

        Assert.Equal(3, root.GetArrayLength());
    }

    [Fact]
    public void OpenComposedWithUnevaluated_Mutable_RemoveWhere()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfInlineTupleWithUnevaluated.Mutable root = builderDoc.RootElement;
        root.RemoveWhere(static (in item) =>
            item.ToString() == "True");

        Assert.Equal(3, root.GetArrayLength());
    }

    [Fact]
    public void OpenComposedWithUnevaluated_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true,false]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse(json);
        Assert.Equal(4, roundTrip.RootElement.GetArrayLength());
        Assert.Equal("hello", roundTrip.RootElement[0].ToString());
        Assert.Equal("3.14", roundTrip.RootElement[1].ToString());
        Assert.Equal("True", roundTrip.RootElement[2].ToString());
        Assert.Equal("False", roundTrip.RootElement[3].ToString());
    }

    #endregion

    #region RefClosedTupleWithContains — $ref-based closed tuple (items:false) + contains — pure tuple

    [Fact]
    public void RefClosedComposed_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse("""["hello",42]""");

        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void RefClosedComposed_IndexAccess_ReturnsJsonElement()
    {
        using var doc =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse("""["hello",42]""");

        Assert.Equal("hello", doc.RootElement[0].ToString());
        Assert.Equal("42", doc.RootElement[1].ToString());
    }

    [Fact]
    public void RefClosedComposed_TryGetAsBaseTuple_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse("""["hello",42]""");

        Assert.True(doc.RootElement.TryGetAsBaseTuple(out RefClosedTupleWithContains.BaseTuple baseTuple));

        var item0 =
            JsonString.From(baseTuple[0]);
        Assert.Equal("hello", (string)item0);

        var item1 =
            JsonInt32.From(baseTuple[1]);
        Assert.Equal(42, (int)item1);
    }

    [Fact]
    public void RefClosedComposed_Build_CreateTuple()
    {
        using var workspace = JsonWorkspace.Create();

        RefClosedTupleWithContains.Source source = RefClosedTupleWithContains.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("world", 99);
            });

        using JsonDocumentBuilder<RefClosedTupleWithContains.Mutable> doc =
            RefClosedTupleWithContains.CreateBuilder(workspace, source);
        RefClosedTupleWithContains.Mutable root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("world", root[0].ToString());
        Assert.Equal("99", root[1].ToString());
    }

    [Fact]
    public void RefClosedComposed_Build_CreateTuple_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        RefClosedTupleWithContains.Source source = RefClosedTupleWithContains.Build(
            static (ref b) =>
            {
                b.CreateTuple("hello", 42);
            });

        using JsonDocumentBuilder<RefClosedTupleWithContains.Mutable> doc =
            RefClosedTupleWithContains.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse(json);
        Assert.Equal(2, reparsed.RootElement.GetArrayLength());
        Assert.Equal("hello", reparsed.RootElement[0].ToString());
        Assert.Equal("42", reparsed.RootElement[1].ToString());
    }

    [Fact]
    public void RefClosedComposed_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse("""["hello",42]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse(json);
        Assert.Equal(2, roundTrip.RootElement.GetArrayLength());
        Assert.Equal("hello", roundTrip.RootElement[0].ToString());
        Assert.Equal("42", roundTrip.RootElement[1].ToString());
    }

    #endregion

    #region RefOpenTupleWithItems — $ref-based open tuple + items:boolean — tuple with additional items

    [Fact]
    public void RefOpenComposedWithItems_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true,false]""");

        Assert.Equal(4, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void RefOpenComposedWithItems_IndexAccess_ReturnsItemsEntity()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true,false]""");

        Assert.Equal("hello", doc.RootElement[0].ToString());
        Assert.Equal("42", doc.RootElement[1].ToString());
        Assert.Equal("True", doc.RootElement[2].ToString());
        Assert.Equal("False", doc.RootElement[3].ToString());
    }

    [Fact]
    public void RefOpenComposedWithItems_TryGetAsBaseTuple_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true]""");

        Assert.True(doc.RootElement.TryGetAsBaseTuple(out RefOpenTupleWithItems.BaseTuple baseTuple));

        var item0 =
            JsonString.From(baseTuple[0]);
        Assert.Equal("hello", (string)item0);

        var item1 =
            JsonInt32.From(baseTuple[1]);
        Assert.Equal(42, (int)item1);
    }

    [Fact]
    public void RefOpenComposedWithItems_Build_CreateTupleThenAdd()
    {
        using var workspace = JsonWorkspace.Create();

        RefOpenTupleWithItems.Source source = RefOpenTupleWithItems.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("hello", 42);
                builder.AddItem(true);
                builder.AddItem(false);
            });

        using JsonDocumentBuilder<RefOpenTupleWithItems.Mutable> doc =
            RefOpenTupleWithItems.CreateBuilder(workspace, source);
        RefOpenTupleWithItems.Mutable root = doc.RootElement;

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal("hello", root[0].ToString());
        Assert.Equal("42", root[1].ToString());
        Assert.Equal("True", root[2].ToString());
        Assert.Equal("False", root[3].ToString());
    }

    [Fact]
    public void RefOpenComposedWithItems_Build_CreateTupleOnly()
    {
        using var workspace = JsonWorkspace.Create();

        RefOpenTupleWithItems.Source source = RefOpenTupleWithItems.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("world", 99);
            });

        using JsonDocumentBuilder<RefOpenTupleWithItems.Mutable> doc =
            RefOpenTupleWithItems.CreateBuilder(workspace, source);
        RefOpenTupleWithItems.Mutable root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("world", root[0].ToString());
        Assert.Equal("99", root[1].ToString());
    }

    [Fact]
    public void RefOpenComposedWithItems_Build_AddBeforeCreateTuple_Throws()
    {
        using var workspace = JsonWorkspace.Create();

        RefOpenTupleWithItems.Source source = RefOpenTupleWithItems.Build(
            static (ref builder) =>
            {
                builder.AddItem(true);
            });

        try
        {
            using JsonDocumentBuilder<RefOpenTupleWithItems.Mutable> doc =
                RefOpenTupleWithItems.CreateBuilder(workspace, source);

            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected: must call CreateTuple before Add
        }
    }

    [Fact]
    public void RefOpenComposedWithItems_Build_CreateTuple_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        RefOpenTupleWithItems.Source source = RefOpenTupleWithItems.Build(
            static (ref b) =>
            {
                b.CreateTuple("hello", 42);
                b.AddItem(true);
            });

        using JsonDocumentBuilder<RefOpenTupleWithItems.Mutable> doc =
            RefOpenTupleWithItems.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse(json);
        Assert.Equal(3, reparsed.RootElement.GetArrayLength());
        Assert.Equal("hello", reparsed.RootElement[0].ToString());
        Assert.Equal("42", reparsed.RootElement[1].ToString());
        Assert.Equal("True", reparsed.RootElement[2].ToString());
    }

    [Fact]
    public void RefOpenComposedWithItems_Mutable_SetItem()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefOpenTupleWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefOpenTupleWithItems.Mutable root = builderDoc.RootElement;
        root.SetItem(2, false);

        Assert.Equal("False", root[2].ToString());
    }

    [Fact]
    public void RefOpenComposedWithItems_Mutable_InsertItem()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefOpenTupleWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefOpenTupleWithItems.Mutable root = builderDoc.RootElement;
        root.InsertItem(2, false);

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal("False", root[2].ToString());
        Assert.Equal("True", root[3].ToString());
    }

    [Fact]
    public void RefOpenComposedWithItems_Mutable_Remove()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefOpenTupleWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefOpenTupleWithItems.Mutable root = builderDoc.RootElement;
        root.RemoveAt(3);

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("True", root[2].ToString());
    }

    [Fact]
    public void RefOpenComposedWithItems_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true,false]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse(json);
        Assert.Equal(4, roundTrip.RootElement.GetArrayLength());
        Assert.Equal("hello", roundTrip.RootElement[0].ToString());
        Assert.Equal("42", roundTrip.RootElement[1].ToString());
        Assert.Equal("True", roundTrip.RootElement[2].ToString());
        Assert.Equal("False", roundTrip.RootElement[3].ToString());
    }

    #endregion
}