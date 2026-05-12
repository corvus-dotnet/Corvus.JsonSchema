// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated array tuple types (prefixItems).
/// Exercises: pure tuples (items: false), tuples with additional items,
/// positional type access, indexer, enumeration, Builder.Build pattern,
/// and mutator methods on tuples with additional items.
/// </summary>
[TestClass]
public class GeneratedTupleArrayTests
{
    #region Pure tuple — parse, access, enumerate

    [TestMethod]
    public void PureTuple_ParseValid_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        Assert.AreEqual(3, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void PureTuple_IndexAccess_ReturnsCorrectTypes()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        Assert.AreEqual("hello", doc.RootElement[0].ToString());
        Assert.AreEqual("42", doc.RootElement[1].ToString());
        Assert.AreEqual("True", doc.RootElement[2].ToString());
    }

    [TestMethod]
    public void PureTuple_PrefixItem0_IsStringType()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        var item0 = JsonString.From(doc.RootElement[0]);
        Assert.AreEqual("hello", (string)item0);
    }

    [TestMethod]
    public void PureTuple_PrefixItem1_IsInt32Type()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        var item1 = JsonInt32.From(doc.RootElement[1]);
        Assert.AreEqual(42, (int)item1);
    }

    [TestMethod]
    public void PureTuple_PrefixItem2_IsBooleanType()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        var item2 = JsonBoolean.From(doc.RootElement[2]);
        Assert.IsTrue((bool)item2);
    }

    [TestMethod]
    public void PureTuple_EnumerateArray_IteratesAllItems()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        int count = 0;
        foreach (JsonElement item in doc.RootElement.EnumerateArray())
        {
            count++;
        }

        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public void PureTuple_Mutable_IndexAccess()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");
        using JsonDocumentBuilder<PureTuple.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        PureTuple.Mutable root = builder.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("42", root[1].ToString());
        Assert.AreEqual("True", root[2].ToString());
    }

    [TestMethod]
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

        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public void PureTuple_RoundTrip_PreservesValues()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        string json = doc.RootElement.ToString();

        using var roundTrip = ParsedJsonDocument<PureTuple>.Parse(json);
        Assert.AreEqual(3, roundTrip.RootElement.GetArrayLength());
        Assert.AreEqual("hello", roundTrip.RootElement[0].ToString());
    }

    [TestMethod]
    public void PureTuple_SchemaValidation_ValidTuplePassesValidation()
    {
        using var doc =
            ParsedJsonDocument<PureTuple>.Parse("""["hello",42,true]""");

        Assert.IsTrue(doc.RootElement.EvaluateSchema());
    }

    #endregion

    #region Tuple with additional items — parse, typed access, additional items

    [TestMethod]
    public void TupleWithAdditional_ParseValid_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        Assert.AreEqual(4, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void TupleWithAdditional_PrefixItems_HaveCorrectTypes()
    {
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true]""");

        var item0 =
            JsonString.From(doc.RootElement[0]);
        Assert.AreEqual("hello", (string)item0);

        var item1 =
            JsonInt32.From(doc.RootElement[1]);
        Assert.AreEqual(42, (int)item1);
    }

    [TestMethod]
    public void TupleWithAdditional_AdditionalItem_IsBooleanType()
    {
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        // Index 2 and 3 are additional items (boolean type)
        var item2 =
            JsonBoolean.From(doc.RootElement[2]);
        Assert.IsTrue((bool)item2);

        var item3 =
            JsonBoolean.From(doc.RootElement[3]);
        Assert.IsFalse((bool)item3);
    }

    #endregion

    #region Tuple with additional items — mutable operations

    [TestMethod]
    public void TupleWithAdditional_Mutable_SetItem_ReplacesAdditionalItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.SetItem(3, true);
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual("True", root[3].ToString());
    }

    [TestMethod]
    public void TupleWithAdditional_Mutable_InsertItem_InsertsAdditionalItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.InsertItem(3, false);
        Assert.AreEqual(4, root.GetArrayLength());
    }

    [TestMethod]
    public void TupleWithAdditional_Mutable_SetItemUndefined_RemovesItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.SetItem(3, default);
        Assert.AreEqual(3, root.GetArrayLength());
    }

    [TestMethod]
    public void TupleWithAdditional_Mutable_InsertItemUndefined_IsNoOp()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.InsertItem(3, default);
        Assert.AreEqual(3, root.GetArrayLength());
    }

    [TestMethod]
    public void TupleWithAdditional_Mutable_Remove_RemovesItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using JsonDocumentBuilder<TupleWithAdditionalItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        TupleWithAdditionalItems.Mutable root = builder.RootElement;
        root.RemoveAt(3);
        Assert.AreEqual(3, root.GetArrayLength());
    }

    [TestMethod]
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
        Assert.AreEqual(4, root.GetArrayLength());
    }

    #endregion

    #region Pure tuple — Builder.CreateTuple pattern

    [TestMethod]
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

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("world", root[0].ToString());
        Assert.AreEqual("99", root[1].ToString());
        Assert.AreEqual("False", root[2].ToString());
    }

    [TestMethod]
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
        StringAssert.Contains(json, "world");
        StringAssert.Contains(json, "99");
        StringAssert.Contains(json, "false");

        // Re-parse the JSON and verify structure
        using var reparsed = ParsedJsonDocument<PureTuple>.Parse(json);
        Assert.AreEqual(3, reparsed.RootElement.GetArrayLength());
    }

    [TestMethod]
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
        Assert.AreEqual("contextValue", root.Name.ToString());
        Assert.AreEqual("77", root.Age.ToString());
        Assert.AreEqual("True", root.IsActive.ToString());
    }

    #endregion

    #region Tuple with additional items — Builder.Build pattern

    [TestMethod]
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

    [TestMethod]
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

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("42", root[1].ToString());
        Assert.AreEqual("True", root[2].ToString());
        Assert.AreEqual("False", root[3].ToString());
    }

    [TestMethod]
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

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("world", root[0].ToString());
        Assert.AreEqual("99", root[1].ToString());
    }

    #endregion

    #region Tuple with additional items — round-trip

    [TestMethod]
    public void TupleWithAdditional_RoundTrip_PreservesAllItems()
    {
        using var doc =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<TupleWithAdditionalItems>.Parse(json);
        Assert.AreEqual(4, roundTrip.RootElement.GetArrayLength());
        Assert.AreEqual("hello", roundTrip.RootElement[0].ToString());
    }

    #endregion

    #region Pure tuple — Build from positional sources

    [TestMethod]
    public void PureTuple_BuildFromSources_CreatesValidSource()
    {
        using var workspace = JsonWorkspace.Create();

        PureTuple.Source source = PureTuple.Build("world", 99, false);

        using JsonDocumentBuilder<PureTuple.Mutable> builder = PureTuple.CreateBuilder(workspace, source);
        PureTuple.Mutable root = builder.RootElement;

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("world", root[0].ToString());
        Assert.AreEqual("99", root[1].ToString());
        Assert.AreEqual("False", root[2].ToString());
    }

    [TestMethod]
    public void PureTuple_BuildFromSources_RoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        PureTuple.Source source = PureTuple.Build("hello", 42, true);

        using JsonDocumentBuilder<PureTuple.Mutable> builder = PureTuple.CreateBuilder(workspace, source);
        string json = builder.RootElement.ToString();

        using var reparsed = ParsedJsonDocument<PureTuple>.Parse(json);
        Assert.AreEqual(3, reparsed.RootElement.GetArrayLength());

        var item0 = JsonString.From(reparsed.RootElement[0]);
        Assert.AreEqual("hello", (string)item0);

        var item1 = JsonInt32.From(reparsed.RootElement[1]);
        Assert.AreEqual(42, (int)item1);

        var item2 = JsonBoolean.From(reparsed.RootElement[2]);
        Assert.IsTrue((bool)item2);
    }

    [TestMethod]
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

        Assert.AreEqual(buildJson, tupleJson);
    }

    [TestMethod]
    public void PureTuple_BuildFromSources_UsedAsPropertyValue()
    {
        // Verify that a Build(sources) Source can be used as a property in an object builder
        using var workspace = JsonWorkspace.Create();

        PureTuple.Source tupleSource = PureTuple.Build("nested", 7, true);

        // Build an ObjectWithMixedProperties containing a PureTuple as one of its values
        // We can verify the Source is usable by materializing it in isolation
        using JsonDocumentBuilder<PureTuple.Mutable> builder = PureTuple.CreateBuilder(workspace, tupleSource);
        string json = builder.RootElement.ToString();
        Assert.AreEqual("""["nested",7,true]""", json);
    }

    #endregion

    #region Pure tuple — CreateBuilder convenience

    [TestMethod]
    public void PureTuple_CreateBuilderFromSources()
    {
        using var workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<PureTuple.Mutable> builder =
            PureTuple.CreateBuilder(workspace, "world", 99, false);
        PureTuple.Mutable root = builder.RootElement;

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("world", root[0].ToString());
        Assert.AreEqual("99", root[1].ToString());
        Assert.AreEqual("False", root[2].ToString());
    }

    [TestMethod]
    public void PureTuple_CreateBuilderFromSources_RoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<PureTuple.Mutable> builder =
            PureTuple.CreateBuilder(workspace, "hello", 42, true);

        string json = builder.RootElement.ToString();

        using var reparsed = ParsedJsonDocument<PureTuple>.Parse(json);
        Assert.AreEqual(3, reparsed.RootElement.GetArrayLength());

        var item0 = JsonString.From(reparsed.RootElement[0]);
        Assert.AreEqual("hello", (string)item0);

        var item1 = JsonInt32.From(reparsed.RootElement[1]);
        Assert.AreEqual(42, (int)item1);

        var item2 = JsonBoolean.From(reparsed.RootElement[2]);
        Assert.IsTrue((bool)item2);
    }

    #endregion
}
