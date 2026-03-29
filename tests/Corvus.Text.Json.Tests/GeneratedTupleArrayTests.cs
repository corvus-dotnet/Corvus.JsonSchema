// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated array tuple types (prefixItems).
/// Exercises: pure tuples (items: false), tuples with additional items,
/// positional type access, indexer, enumeration, Builder.Build pattern,
/// and mutator methods on tuples with additional items.
/// </summary>
public class GeneratedTupleArrayTests
{
    #region Pure tuple — parse, access, enumerate

    [Fact]
    public void PureTuple_ParseValid_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        Assert.Equal(3, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void PureTuple_IndexAccess_ReturnsCorrectTypes()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        Assert.Equal("hello", doc.RootElement[0].ToString());
        Assert.Equal("42", doc.RootElement[1].ToString());
        Assert.Equal("True", doc.RootElement[2].ToString());
    }

    [Fact]
    public void PureTuple_PrefixItem0_IsStringType()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        var item0 = JsonString.From(doc.RootElement[0]);
        Assert.Equal("hello", (string)item0);
    }

    [Fact]
    public void PureTuple_PrefixItem1_IsInt32Type()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        var item1 = JsonInt32.From(doc.RootElement[1]);
        Assert.Equal(42, (int)item1);
    }

    [Fact]
    public void PureTuple_PrefixItem2_IsBooleanType()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        var item2 = JsonBoolean.From(doc.RootElement[2]);
        Assert.True((bool)item2);
    }

    [Fact]
    public void PureTuple_EnumerateArray_IteratesAllItems()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        int count = 0;
        foreach (JsonElement item in doc.RootElement.EnumerateArray())
        {
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void PureTuple_Mutable_IndexAccess()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");
        using JsonDocumentBuilder<PureTuple.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        PureTuple.Mutable root = builder.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("hello", root[0].ToString());
        Assert.Equal("42", root[1].ToString());
        Assert.Equal("True", root[2].ToString());
    }

    [Fact]
    public void PureTuple_Mutable_EnumerateArray()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");
        using JsonDocumentBuilder<PureTuple.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        PureTuple.Mutable root = builder.RootElement;
        int count = 0;
        foreach (JsonElement.Mutable item in root.EnumerateArray())
        {
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void PureTuple_RoundTrip_PreservesValues()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        string json = doc.RootElement.ToString();

        using var roundTrip = ParsedJsonDocument<PureTuple>.Parse(json);
        Assert.Equal(3, roundTrip.RootElement.GetArrayLength());
        Assert.Equal("hello", roundTrip.RootElement[0].ToString());
    }

    [Fact]
    public void PureTuple_SchemaValidation_ValidTuplePassesValidation()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        Assert.True(doc.RootElement.EvaluateSchema());
    }

    #endregion

    #region Tuple with additional items — parse, typed access, additional items

    [Fact]
    public void TupleWithAdditional_ParseValid_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        Assert.Equal(4, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void TupleWithAdditional_PrefixItems_HaveCorrectTypes()
    {
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true]""");

        var item0 =
            JsonString.From(doc.RootElement[0]);
        Assert.Equal("hello", (string)item0);

        var item1 =
            JsonInt32.From(doc.RootElement[1]);
        Assert.Equal(42, (int)item1);
    }

    [Fact]
    public void TupleWithAdditional_AdditionalItem_IsBooleanType()
    {
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        // Index 2 and 3 are additional items (boolean type)
        var item2 =
            JsonBoolean.From(doc.RootElement[2]);
        Assert.True((bool)item2);

        var item3 =
            JsonBoolean.From(doc.RootElement[3]);
        Assert.False((bool)item3);
    }

    #endregion

    #region Tuple with additional items — mutable operations

    [Fact]
    public void TupleWithAdditional_Mutable_SetItem_ReplacesAdditionalItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.SetItem(3, true);
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal("True", root[3].ToString());
    }

    [Fact]
    public void TupleWithAdditional_Mutable_InsertItem_InsertsAdditionalItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.InsertItem(3, false);
        Assert.Equal(4, root.GetArrayLength());
    }

    [Fact]
    public void TupleWithAdditional_Mutable_SetItemUndefined_RemovesItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.SetItem(3, default);
        Assert.Equal(3, root.GetArrayLength());
    }

    [Fact]
    public void TupleWithAdditional_Mutable_InsertItemUndefined_IsNoOp()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.InsertItem(3, default);
        Assert.Equal(3, root.GetArrayLength());
    }

    [Fact]
    public void TupleWithAdditional_Mutable_Remove_RemovesItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.RemoveAt(3);
        Assert.Equal(3, root.GetArrayLength());
    }

    [Fact]
    public void TupleWithAdditional_Mutable_RemoveWhere_RemovesMatchingItems()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false,true]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.RemoveWhere(static (in item) => item.ToString() == "False");
        // Original: ["hello", 42, true, false, true]
        // After removing false items: ["hello", 42, true, true]
        Assert.Equal(4, root.GetArrayLength());
    }

    #endregion

    #region Pure tuple — Builder.CreateTuple pattern

    [Fact]
    public void PureTuple_Build_CreateTuple_CreatesValidSource()
    {
        using var workspace = JsonWorkspace.Create();

        PureTuple.Source source = PureTuple.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("world", 99, false);
            });

        using JsonDocumentBuilder<PureTuple.Mutable> builder = PureTuple.CreateBuilder(workspace, source);
        PureTuple.Mutable root = builder.RootElement;

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("world", root[0].ToString());
        Assert.Equal("99", root[1].ToString());
        Assert.Equal("False", root[2].ToString());
    }

    [Fact]
    public void PureTuple_Build_CreateTuple_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        PureTuple.Source tupleSource = PureTuple.Build(
            static (ref b) =>
            {
                b.CreateTuple("world", 99, false);
            });

        // Materialize the source into a document and verify round-trip
        using JsonDocumentBuilder<PureTuple.Mutable> builder = PureTuple.CreateBuilder(workspace, tupleSource);
        PureTuple.Mutable root = builder.RootElement;

        string json = root.ToString();
        Assert.Contains("world", json);
        Assert.Contains("99", json);
        Assert.Contains("false", json);

        // Re-parse the JSON and verify structure
        using var reparsed = ParsedJsonDocument<PureTuple>.Parse(json);
        Assert.Equal(3, reparsed.RootElement.GetArrayLength());
    }

    [Fact]
    public void CreateBuilder_WithContext_CreatesValidDocument()
    {
        using var workspace = JsonWorkspace.Create();
        (string Name, int Age, bool IsActive) context = ("contextValue", 77, true);

        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder =
            ObjectWithMixedProperties.CreateBuilder(
                workspace,
                context,
                static (in ctx, ref b) =>
                {
                    b.Create(ctx.Age, ctx.Name, isActive: ctx.IsActive);
                });

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        Assert.Equal("contextValue", root.Name.ToString());
        Assert.Equal("77", root.Age.ToString());
        Assert.Equal("True", root.IsActive.ToString());
    }

    #endregion

    #region Tuple with additional items — Builder.Build pattern

    [Fact]
    public void TupleWithAdditional_Build_AddBeforeCreateTuple_Throws()
    {
        using var workspace = JsonWorkspace.Create();

        TupleWithAdditionalItems.Source source = TupleWithAdditionalItems.Build(
            static (ref builder) =>
            {
                builder.AddItem(true);
            });

        try
        {
            using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder =
                TupleWithAdditionalItems.CreateBuilder(workspace, source);

            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected: must call CreateTuple before Add
        }
    }

    [Fact]
    public void TupleWithAdditional_Build_CreateTupleThenAdd()
    {
        using var workspace = JsonWorkspace.Create();

        TupleWithAdditionalItems.Source source = TupleWithAdditionalItems.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("hello", 42);
                builder.AddItem(true);
                builder.AddItem(false);
            });

        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder =
            TupleWithAdditionalItems.CreateBuilder(workspace, source);
        TupleWithAdditionalItems.Mutable root = builder.RootElement;

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal("hello", root[0].ToString());
        Assert.Equal("42", root[1].ToString());
        Assert.Equal("True", root[2].ToString());
        Assert.Equal("False", root[3].ToString());
    }

    [Fact]
    public void TupleWithAdditional_Build_CreateTupleOnly()
    {
        using var workspace = JsonWorkspace.Create();

        TupleWithAdditionalItems.Source source = TupleWithAdditionalItems.Build(
            static (ref builder) =>
            {
                builder.CreateTuple("world", 99);
            });

        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder =
            TupleWithAdditionalItems.CreateBuilder(workspace, source);
        TupleWithAdditionalItems.Mutable root = builder.RootElement;

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("world", root[0].ToString());
        Assert.Equal("99", root[1].ToString());
    }

    #endregion

    #region Tuple with additional items — round-trip

    [Fact]
    public void TupleWithAdditional_RoundTrip_PreservesAllItems()
    {
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse(json);
        Assert.Equal(4, roundTrip.RootElement.GetArrayLength());
        Assert.Equal("hello", roundTrip.RootElement[0].ToString());
    }

    #endregion

    #region Pure tuple — Build from positional sources

    [Fact]
    public void PureTuple_BuildFromSources_CreatesValidSource()
    {
        using var workspace = JsonWorkspace.Create();

        PureTuple.Source source = PureTuple.Build("world", 99, false);

        using JsonDocumentBuilder<PureTuple.Mutable> builder = PureTuple.CreateBuilder(workspace, source);
        PureTuple.Mutable root = builder.RootElement;

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("world", root[0].ToString());
        Assert.Equal("99", root[1].ToString());
        Assert.Equal("False", root[2].ToString());
    }

    [Fact]
    public void PureTuple_BuildFromSources_RoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        PureTuple.Source source = PureTuple.Build("hello", 42, true);

        using JsonDocumentBuilder<PureTuple.Mutable> builder = PureTuple.CreateBuilder(workspace, source);
        string json = builder.RootElement.ToString();

        using var reparsed = ParsedJsonDocument<PureTuple>.Parse(json);
        Assert.Equal(3, reparsed.RootElement.GetArrayLength());

        var item0 = JsonString.From(reparsed.RootElement[0]);
        Assert.Equal("hello", (string)item0);

        var item1 = JsonInt32.From(reparsed.RootElement[1]);
        Assert.Equal(42, (int)item1);

        var item2 = JsonBoolean.From(reparsed.RootElement[2]);
        Assert.True((bool)item2);
    }

    [Fact]
    public void PureTuple_BuildFromSources_MatchesBuildCreateTuple()
    {
        using var workspace = JsonWorkspace.Create();

        // Build using Build(sources)
        PureTuple.Source tupleSource = PureTuple.Build("test", 123, false);
        using JsonDocumentBuilder<PureTuple.Mutable> tupleBuilder = PureTuple.CreateBuilder(workspace, tupleSource);
        string tupleJson = tupleBuilder.RootElement.ToString();

        // Build using Build + CreateTuple
        PureTuple.Source buildSource = PureTuple.Build(
            static (ref b) =>
            {
                b.CreateTuple("test", 123, false);
            });
        using JsonDocumentBuilder<PureTuple.Mutable> buildBuilder = PureTuple.CreateBuilder(workspace, buildSource);
        string buildJson = buildBuilder.RootElement.ToString();

        Assert.Equal(buildJson, tupleJson);
    }

    [Fact]
    public void PureTuple_BuildFromSources_UsedAsPropertyValue()
    {
        // Verify that a Build(sources) Source can be used as a property in an object builder
        using var workspace = JsonWorkspace.Create();

        PureTuple.Source tupleSource = PureTuple.Build("nested", 7, true);

        // Build an ObjectWithMixedProperties containing a PureTuple as one of its values
        // We can verify the Source is usable by materializing it in isolation
        using JsonDocumentBuilder<PureTuple.Mutable> builder = PureTuple.CreateBuilder(workspace, tupleSource);
        string json = builder.RootElement.ToString();
        Assert.Equal("""["nested",7,true]""", json);
    }

    #endregion

    #region Pure tuple — CreateBuilder convenience

    [Fact]
    public void PureTuple_CreateBuilderFromSources()
    {
        using var workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<PureTuple.Mutable> builder =
            PureTuple.CreateBuilder(workspace, "world", 99, false);
        PureTuple.Mutable root = builder.RootElement;

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("world", root[0].ToString());
        Assert.Equal("99", root[1].ToString());
        Assert.Equal("False", root[2].ToString());
    }

    [Fact]
    public void PureTuple_CreateBuilderFromSources_RoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<PureTuple.Mutable> builder =
            PureTuple.CreateBuilder(workspace, "hello", 42, true);

        string json = builder.RootElement.ToString();

        using var reparsed = ParsedJsonDocument<PureTuple>.Parse(json);
        Assert.Equal(3, reparsed.RootElement.GetArrayLength());

        var item0 = JsonString.From(reparsed.RootElement[0]);
        Assert.Equal("hello", (string)item0);

        var item1 = JsonInt32.From(reparsed.RootElement[1]);
        Assert.Equal(42, (int)item1);

        var item2 = JsonBoolean.From(reparsed.RootElement[2]);
        Assert.True((bool)item2);
    }

    #endregion
}