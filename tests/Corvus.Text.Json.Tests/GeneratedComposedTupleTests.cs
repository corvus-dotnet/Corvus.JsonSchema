// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for tuple types composed via allOf alongside additional constraints
/// such as contains, items, or unevaluatedItems.
/// Covers the closed/open matrix for composed vs local type constraints.
/// </summary>
[TestClass]
public class GeneratedComposedTupleTests
{
    #region RefTupleWithContains — closed composed tuple (items:false) + contains — pure tuple

    [TestMethod]
    public void ClosedComposed_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");

        Assert.AreEqual(2, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void ClosedComposed_IndexAccess_ReturnsJsonElement()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");

        Assert.AreEqual("hello", doc.RootElement[0].ToString());
        Assert.AreEqual("42", doc.RootElement[1].ToString());
    }

    [TestMethod]
    public void ClosedComposed_TryGetAsAllOf0Array_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");

        Assert.IsTrue(doc.RootElement.TryGetAsAllOf0Array(out RefTupleWithContains.AllOf0Array allOf0));

        var item0 =
            JsonString.From(allOf0[0]);
        Assert.AreEqual("hello", (string)item0);

        var item1 =
            JsonInt32.From(allOf0[1]);
        Assert.AreEqual(42, (int)item1);
    }

    [TestMethod]
    public void ClosedComposed_MutableTryGetAsAllOf0Array_ReturnsMutable()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefTupleWithContains.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefTupleWithContains.Mutable root = builderDoc.RootElement;
        Assert.IsTrue(root.TryGetAsAllOf0Array(out RefTupleWithContains.AllOf0Array.Mutable allOf0));

        JsonString.Mutable item0 = allOf0.Item1;
        JsonInt32.Mutable item1 = allOf0.Item2;
        Assert.AreEqual("hello", item0.ToString());
        Assert.AreEqual("42", item1.ToString());
    }

    [TestMethod]
    public void ClosedComposed_EnumerateArray()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");

        int count = 0;
        foreach (JsonElement item in doc.RootElement.EnumerateArray())
        {
            count++;
        }

        Assert.AreEqual(2, count);
    }

    [TestMethod]
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

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("world", root[0].ToString());
        Assert.AreEqual("99", root[1].ToString());
    }

    [TestMethod]
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
        StringAssert.Contains(json, "hello");
        StringAssert.Contains(json, "42");

        using var reparsed =
            ParsedJsonDocument<RefTupleWithContains>.Parse(json);
        Assert.AreEqual(2, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual("hello", reparsed.RootElement[0].ToString());
        Assert.AreEqual("42", reparsed.RootElement[1].ToString());
    }

    [TestMethod]
    public void ClosedComposed_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithContains>.Parse("""["hello",42]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<RefTupleWithContains>.Parse(json);
        Assert.AreEqual(2, roundTrip.RootElement.GetArrayLength());
        Assert.AreEqual("hello", roundTrip.RootElement[0].ToString());
        Assert.AreEqual("42", roundTrip.RootElement[1].ToString());
    }

    #endregion

    #region AllOfOpenTupleClosedLocally — open composed + items:false locally — pure tuple

    [TestMethod]
    public void OpenComposedClosedLocally_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse("""["hello",3.14]""");

        Assert.AreEqual(2, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void OpenComposedClosedLocally_IndexAccess_ReturnsJsonElement()
    {
        using var doc =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse("""["hello",3.14]""");

        Assert.AreEqual("hello", doc.RootElement[0].ToString());
        Assert.AreEqual("3.14", doc.RootElement[1].ToString());
    }

    [TestMethod]
    public void OpenComposedClosedLocally_TryGetAsAllOf0Array_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse("""["hello",3.14]""");

        Assert.IsTrue(doc.RootElement.TryGetAsAllOf0Array(out AllOfOpenTupleClosedLocally.AllOf0Array allOf0));

        var item0 =
            JsonString.From(allOf0[0]);
        Assert.AreEqual("hello", (string)item0);

        var item1 =
            JsonNumber.From(allOf0[1]);
        Assert.AreEqual(3.14, (double)item1, 2);
    }

    [TestMethod]
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

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("world", root[0].ToString());
        Assert.AreEqual("2.718", root[1].ToString());
    }

    [TestMethod]
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
        Assert.AreEqual(2, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual("hello", reparsed.RootElement[0].ToString());
        Assert.AreEqual("1.5", reparsed.RootElement[1].ToString());
    }

    [TestMethod]
    public void OpenComposedClosedLocally_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse("""["hello",3.14]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<AllOfOpenTupleClosedLocally>.Parse(json);
        Assert.AreEqual(2, roundTrip.RootElement.GetArrayLength());
        Assert.AreEqual("hello", roundTrip.RootElement[0].ToString());
        Assert.AreEqual("3.14", roundTrip.RootElement[1].ToString());
    }

    #endregion

    #region RefTupleWithAdditionalItems — open composed + items:boolean — tuple with additional items

    [TestMethod]
    public void OpenComposedWithItems_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        Assert.AreEqual(4, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void OpenComposedWithItems_IndexAccess_ReturnsItemsEntity()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        Assert.AreEqual("hello", doc.RootElement[0].ToString());
        Assert.AreEqual("42", doc.RootElement[1].ToString());
        Assert.AreEqual("True", doc.RootElement[2].ToString());
        Assert.AreEqual("False", doc.RootElement[3].ToString());
    }

    [TestMethod]
    public void OpenComposedWithItems_TryGetAsAllOf0Array_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true]""");

        Assert.IsTrue(doc.RootElement.TryGetAsAllOf0Array(out RefTupleWithAdditionalItems.AllOf0Array allOf0));

        var item0 =
            JsonString.From(allOf0[0]);
        Assert.AreEqual("hello", (string)item0);

        var item1 =
            JsonInt32.From(allOf0[1]);
        Assert.AreEqual(42, (int)item1);
    }

    [TestMethod]
    public void OpenComposedWithItems_EnumerateArray()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        int count = 0;
        foreach (JsonBoolean item in doc.RootElement.EnumerateArray())
        {
            count++;
        }

        Assert.AreEqual(4, count);
    }

    [TestMethod]
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

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("42", root[1].ToString());
        Assert.AreEqual("True", root[2].ToString());
        Assert.AreEqual("False", root[3].ToString());
    }

    [TestMethod]
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

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("world", root[0].ToString());
        Assert.AreEqual("99", root[1].ToString());
    }

    [TestMethod]
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

    [TestMethod]
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
        Assert.AreEqual(3, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual("hello", reparsed.RootElement[0].ToString());
        Assert.AreEqual("42", reparsed.RootElement[1].ToString());
        Assert.AreEqual("True", reparsed.RootElement[2].ToString());
    }

    [TestMethod]
    public void OpenComposedWithItems_Mutable_SetItem()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefTupleWithAdditionalItems.Mutable root = builderDoc.RootElement;
        root.SetItem(2, false);

        Assert.AreEqual("False", root[2].ToString());
    }

    [TestMethod]
    public void OpenComposedWithItems_Mutable_InsertItem()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefTupleWithAdditionalItems.Mutable root = builderDoc.RootElement;
        root.InsertItem(2, false);

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual("False", root[2].ToString());
        Assert.AreEqual("True", root[3].ToString());
    }

    [TestMethod]
    public void OpenComposedWithItems_Mutable_Remove()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefTupleWithAdditionalItems.Mutable root = builderDoc.RootElement;
        root.RemoveAt(3);

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("True", root[2].ToString());
    }

    [TestMethod]
    public void OpenComposedWithItems_Mutable_SetItemUndefined_Removes()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefTupleWithAdditionalItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefTupleWithAdditionalItems.Mutable root = builderDoc.RootElement;
        root.SetItem(3, default);

        Assert.AreEqual(3, root.GetArrayLength());
    }

    [TestMethod]
    public void OpenComposedWithItems_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse("""["hello",42,true,false]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<RefTupleWithAdditionalItems>.Parse(json);
        Assert.AreEqual(4, roundTrip.RootElement.GetArrayLength());
        Assert.AreEqual("hello", roundTrip.RootElement[0].ToString());
        Assert.AreEqual("42", roundTrip.RootElement[1].ToString());
        Assert.AreEqual("True", roundTrip.RootElement[2].ToString());
        Assert.AreEqual("False", roundTrip.RootElement[3].ToString());
    }

    #endregion

    #region AllOfInlineTupleWithUnevaluated — open composed + unevaluatedItems:boolean — tuple with additional items

    [TestMethod]
    public void OpenComposedWithUnevaluated_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true]""");

        Assert.AreEqual(3, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void OpenComposedWithUnevaluated_IndexAccess_ReturnsUnevaluatedItemsEntity()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true,false]""");

        Assert.AreEqual("hello", doc.RootElement[0].ToString());
        Assert.AreEqual("3.14", doc.RootElement[1].ToString());
        Assert.AreEqual("True", doc.RootElement[2].ToString());
        Assert.AreEqual("False", doc.RootElement[3].ToString());
    }

    [TestMethod]
    public void OpenComposedWithUnevaluated_TryGetAsAllOf0Entity_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true]""");

        Assert.IsTrue(doc.RootElement.TryGetAsAllOf0Entity(out AllOfInlineTupleWithUnevaluated.AllOf0Entity entity));

        var item0 =
            JsonString.From(entity[0]);
        Assert.AreEqual("hello", (string)item0);

        var item1 =
            JsonNumber.From(entity[1]);
        Assert.AreEqual(3.14, (double)item1, 2);
    }

    [TestMethod]
    public void OpenComposedWithUnevaluated_EnumerateArray()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true,false]""");

        int count = 0;
        foreach (JsonBoolean item in doc.RootElement.EnumerateArray())
        {
            count++;
        }

        Assert.AreEqual(4, count);
    }

    [TestMethod]
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

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("3.14", root[1].ToString());
        Assert.AreEqual("True", root[2].ToString());
        Assert.AreEqual("False", root[3].ToString());
    }

    [TestMethod]
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

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("world", root[0].ToString());
        Assert.AreEqual("2.718", root[1].ToString());
    }

    [TestMethod]
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

    [TestMethod]
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
        Assert.AreEqual(3, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual("hello", reparsed.RootElement[0].ToString());
        Assert.AreEqual("3.14", reparsed.RootElement[1].ToString());
        Assert.AreEqual("True", reparsed.RootElement[2].ToString());
    }

    [TestMethod]
    public void OpenComposedWithUnevaluated_Mutable_SetItem()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfInlineTupleWithUnevaluated.Mutable root = builderDoc.RootElement;
        root.SetItem(2, false);

        Assert.AreEqual("False", root[2].ToString());
    }

    [TestMethod]
    public void OpenComposedWithUnevaluated_Mutable_InsertItem()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfInlineTupleWithUnevaluated.Mutable root = builderDoc.RootElement;
        root.InsertItem(2, true);

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("True", root[2].ToString());
    }

    [TestMethod]
    public void OpenComposedWithUnevaluated_Mutable_Remove()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfInlineTupleWithUnevaluated.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfInlineTupleWithUnevaluated.Mutable root = builderDoc.RootElement;
        root.RemoveAt(3);

        Assert.AreEqual(3, root.GetArrayLength());
    }

    [TestMethod]
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

        Assert.AreEqual(3, root.GetArrayLength());
    }

    [TestMethod]
    public void OpenComposedWithUnevaluated_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse("""["hello",3.14,true,false]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<AllOfInlineTupleWithUnevaluated>.Parse(json);
        Assert.AreEqual(4, roundTrip.RootElement.GetArrayLength());
        Assert.AreEqual("hello", roundTrip.RootElement[0].ToString());
        Assert.AreEqual("3.14", roundTrip.RootElement[1].ToString());
        Assert.AreEqual("True", roundTrip.RootElement[2].ToString());
        Assert.AreEqual("False", roundTrip.RootElement[3].ToString());
    }

    #endregion

    #region RefClosedTupleWithContains — $ref-based closed tuple (items:false) + contains — pure tuple

    [TestMethod]
    public void RefClosedComposed_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse("""["hello",42]""");

        Assert.AreEqual(2, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void RefClosedComposed_IndexAccess_ReturnsJsonElement()
    {
        using var doc =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse("""["hello",42]""");

        Assert.AreEqual("hello", doc.RootElement[0].ToString());
        Assert.AreEqual("42", doc.RootElement[1].ToString());
    }

    [TestMethod]
    public void RefClosedComposed_TryGetAsBaseTuple_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse("""["hello",42]""");

        Assert.IsTrue(doc.RootElement.TryGetAsBaseTuple(out RefClosedTupleWithContains.BaseTuple baseTuple));

        var item0 =
            JsonString.From(baseTuple[0]);
        Assert.AreEqual("hello", (string)item0);

        var item1 =
            JsonInt32.From(baseTuple[1]);
        Assert.AreEqual(42, (int)item1);
    }

    [TestMethod]
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

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("world", root[0].ToString());
        Assert.AreEqual("99", root[1].ToString());
    }

    [TestMethod]
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
        Assert.AreEqual(2, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual("hello", reparsed.RootElement[0].ToString());
        Assert.AreEqual("42", reparsed.RootElement[1].ToString());
    }

    [TestMethod]
    public void RefClosedComposed_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse("""["hello",42]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<RefClosedTupleWithContains>.Parse(json);
        Assert.AreEqual(2, roundTrip.RootElement.GetArrayLength());
        Assert.AreEqual("hello", roundTrip.RootElement[0].ToString());
        Assert.AreEqual("42", roundTrip.RootElement[1].ToString());
    }

    #endregion

    #region RefOpenTupleWithItems — $ref-based open tuple + items:boolean — tuple with additional items

    [TestMethod]
    public void RefOpenComposedWithItems_Parse_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true,false]""");

        Assert.AreEqual(4, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void RefOpenComposedWithItems_IndexAccess_ReturnsItemsEntity()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true,false]""");

        Assert.AreEqual("hello", doc.RootElement[0].ToString());
        Assert.AreEqual("42", doc.RootElement[1].ToString());
        Assert.AreEqual("True", doc.RootElement[2].ToString());
        Assert.AreEqual("False", doc.RootElement[3].ToString());
    }

    [TestMethod]
    public void RefOpenComposedWithItems_TryGetAsBaseTuple_AccessesPrefixItems()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true]""");

        Assert.IsTrue(doc.RootElement.TryGetAsBaseTuple(out RefOpenTupleWithItems.BaseTuple baseTuple));

        var item0 =
            JsonString.From(baseTuple[0]);
        Assert.AreEqual("hello", (string)item0);

        var item1 =
            JsonInt32.From(baseTuple[1]);
        Assert.AreEqual(42, (int)item1);
    }

    [TestMethod]
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

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("42", root[1].ToString());
        Assert.AreEqual("True", root[2].ToString());
        Assert.AreEqual("False", root[3].ToString());
    }

    [TestMethod]
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

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("world", root[0].ToString());
        Assert.AreEqual("99", root[1].ToString());
    }

    [TestMethod]
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

    [TestMethod]
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
        Assert.AreEqual(3, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual("hello", reparsed.RootElement[0].ToString());
        Assert.AreEqual("42", reparsed.RootElement[1].ToString());
        Assert.AreEqual("True", reparsed.RootElement[2].ToString());
    }

    [TestMethod]
    public void RefOpenComposedWithItems_Mutable_SetItem()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefOpenTupleWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefOpenTupleWithItems.Mutable root = builderDoc.RootElement;
        root.SetItem(2, false);

        Assert.AreEqual("False", root[2].ToString());
    }

    [TestMethod]
    public void RefOpenComposedWithItems_Mutable_InsertItem()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefOpenTupleWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefOpenTupleWithItems.Mutable root = builderDoc.RootElement;
        root.InsertItem(2, false);

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual("False", root[2].ToString());
        Assert.AreEqual("True", root[3].ToString());
    }

    [TestMethod]
    public void RefOpenComposedWithItems_Mutable_Remove()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true,false]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefOpenTupleWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefOpenTupleWithItems.Mutable root = builderDoc.RootElement;
        root.RemoveAt(3);

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("True", root[2].ToString());
    }

    [TestMethod]
    public void RefOpenComposedWithItems_RoundTrip()
    {
        using var doc =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse("""["hello",42,true,false]""");

        string json = doc.RootElement.ToString();

        using var roundTrip =
            ParsedJsonDocument<RefOpenTupleWithItems>.Parse(json);
        Assert.AreEqual(4, roundTrip.RootElement.GetArrayLength());
        Assert.AreEqual("hello", roundTrip.RootElement[0].ToString());
        Assert.AreEqual("42", roundTrip.RootElement[1].ToString());
        Assert.AreEqual("True", roundTrip.RootElement[2].ToString());
        Assert.AreEqual("False", roundTrip.RootElement[3].ToString());
    }

    #endregion
}