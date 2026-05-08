// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated mutable array types with typed items.
/// Exercises: SetItem, InsertItem, Remove, RemoveRange, RemoveWhere,
/// IsUndefined guards (SetItem→remove, InsertItem→no-op),
/// GetArrayLength, and EnumerateArray.
/// </summary>
[TestClass]
public class GeneratedArrayMutationTests
{
    private const string SampleJson =
        """
        [{"id":1,"label":"first"},{"id":2,"label":"second"},{"id":3,"label":"third"}]
        """;

    #region SetItem

    [TestMethod]
    public void SetItem_AtValidIndex_ReplacesItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        using var itemDoc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":99,"label":"replaced"}""");
        root.SetItem(1, itemDoc.RootElement);

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(99, (int)root[1].Id);
    }

    [TestMethod]
    public void SetItem_AtArrayLength_AppendsItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        using var itemDoc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":4,"label":"fourth"}""");
        root.SetItem(3, itemDoc.RootElement);

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(4, (int)root[3].Id);
    }

    [TestMethod]
    public void SetItem_WithUndefinedSource_RemovesItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;
        root.SetItem(1, default);
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(1, (int)root[0].Id);
        Assert.AreEqual(3, (int)root[1].Id);
    }

    #endregion

    #region InsertItem

    [TestMethod]
    public void InsertItem_AtIndex_InsertsItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        using var itemDoc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":0,"label":"inserted"}""");
        root.InsertItem(1, itemDoc.RootElement);

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(1, (int)root[0].Id);
        Assert.AreEqual(0, (int)root[1].Id);
        Assert.AreEqual(2, (int)root[2].Id);
    }

    [TestMethod]
    public void InsertItem_WithUndefinedSource_IsNoOp()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;
        root.InsertItem(1, default);
        Assert.AreEqual(3, root.GetArrayLength());
    }

    #endregion

    #region AddItem

    [TestMethod]
    public void AddItem_AppendsItemToEnd()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        using var itemDoc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":4,"label":"fourth"}""");
        root.AddItem(itemDoc.RootElement);

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(4, (int)root[3].Id);
        Assert.AreEqual("fourth", (string)root[3].Label);
    }

    [TestMethod]
    public void AddItem_MultipleAppends_PreservesOrder()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        using var item4Doc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":4,"label":"fourth"}""");
        using var item5Doc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":5,"label":"fifth"}""");
        root.AddItem(item4Doc.RootElement);
        root.AddItem(item5Doc.RootElement);

        Assert.AreEqual(5, root.GetArrayLength());
        Assert.AreEqual(4, (int)root[3].Id);
        Assert.AreEqual(5, (int)root[4].Id);
    }

    [TestMethod]
    public void AddItem_WithUndefinedSource_IsNoOp()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;
        root.AddItem(default);
        Assert.AreEqual(3, root.GetArrayLength());
    }

    #endregion

    #region Remove, RemoveRange, RemoveWhere

    [TestMethod]
    public void Remove_AtIndex_RemovesItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;
        root.RemoveAt(0);
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(2, (int)root[0].Id);
    }

    [TestMethod]
    public void RemoveRange_RemovesMultipleItems()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;
        root.RemoveRange(0, 2);
        Assert.AreEqual(1, root.GetArrayLength());
        Assert.AreEqual(3, (int)root[0].Id);
    }

    [TestMethod]
    public void RemoveWhere_RemovesMatchingItems()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;
        root.RemoveWhere(static (in item) => (int)item.Id > 1);
        Assert.AreEqual(1, root.GetArrayLength());
        Assert.AreEqual(1, (int)root[0].Id);
    }

    #endregion

    #region Replace

    [TestMethod]
    public void Replace_FindsAndReplacesFirstMatchingItem()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        using var oldDoc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":2,"label":"second"}""");
        ArrayOfItems.RequiredId oldItem = oldDoc.RootElement;
        using var newDoc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":99,"label":"replaced"}""");
        ArrayOfItems.RequiredId newItem = newDoc.RootElement;
        bool replaced = root.Replace(oldItem, newItem);

        Assert.IsTrue(replaced);
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(99, (int)root[1].Id);
        Assert.AreEqual("replaced", (string)root[1].Label);
    }

    [TestMethod]
    public void Replace_ReturnsFalse_WhenItemNotFound()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        using var oldDoc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":99,"label":"missing"}""");
        ArrayOfItems.RequiredId oldItem = oldDoc.RootElement;
        using var newDoc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":100,"label":"new"}""");
        ArrayOfItems.RequiredId newItem = newDoc.RootElement;
        bool replaced = root.Replace(oldItem, newItem);

        Assert.IsFalse(replaced);
        Assert.AreEqual(3, root.GetArrayLength());
    }

    [TestMethod]
    public void Replace_WithUndefinedSource_RemovesMatch()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        using var oldDoc = ParsedJsonDocument<ArrayOfItems.RequiredId>.Parse("""{"id":2,"label":"second"}""");
        ArrayOfItems.RequiredId oldItem = oldDoc.RootElement;
        bool replaced = root.Replace(oldItem, default);

        Assert.IsTrue(replaced);
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(1, (int)root[0].Id);
        Assert.AreEqual(3, (int)root[1].Id);
    }

    #endregion

    #region GetArrayLength and EnumerateArray

    [TestMethod]
    public void GetArrayLength_ReturnsCorrectCount()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
    }

    [TestMethod]
    public void EnumerateArray_IteratesAllItems()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;
        int count = 0;
        foreach (ArrayOfItems.RequiredId.Mutable item in root.EnumerateArray())
        {
            count++;
        }

        Assert.AreEqual(3, count);
    }

    #endregion

    #region InsertRange

    [TestMethod]
    public void InsertRange_InsertsMultipleItems()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        root.InsertRange(1, static (ref b) =>
        {
            b.AddItem(static (ref ob) =>
            {
                ob.AddProperty("id"u8, 10);
                ob.AddProperty("label"u8, "ten"u8);
            });
            b.AddItem(static (ref ob) =>
            {
                ob.AddProperty("id"u8, 20);
                ob.AddProperty("label"u8, "twenty"u8);
            });
        });

        Assert.AreEqual(5, root.GetArrayLength());
        Assert.AreEqual(1, (int)root[0].Id);
        Assert.AreEqual(10, (int)root[1].Id);
        Assert.AreEqual(20, (int)root[2].Id);
        Assert.AreEqual(2, (int)root[3].Id);
        Assert.AreEqual(3, (int)root[4].Id);
    }

    [TestMethod]
    public void InsertRange_WithContext_InsertsMultipleItems()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        int[] ids = [10, 20];
        root.InsertRange(0, ids, static (in ctx, ref b) =>
        {
            for (int i = 0; i < ctx.Length; i++)
            {
                int id = ctx[i];
                b.AddItem(id, static (in id, ref ob) =>
                {
                    ob.AddProperty("id"u8, id);
                    ob.AddProperty("label"u8, "new"u8);
                });
            }
        });

        Assert.AreEqual(5, root.GetArrayLength());
        Assert.AreEqual(10, (int)root[0].Id);
        Assert.AreEqual(20, (int)root[1].Id);
        Assert.AreEqual(1, (int)root[2].Id);
    }

    #endregion

    #region AddRange

    [TestMethod]
    public void AddRange_AppendsMultipleItems()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ArrayOfItems>.Parse(SampleJson);
        using JsonDocumentBuilder<ArrayOfItems.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ArrayOfItems.Mutable root = builder.RootElement;

        root.AddRange(static (ref b) =>
        {
            b.AddItem(static (ref ob) =>
            {
                ob.AddProperty("id"u8, 4);
                ob.AddProperty("label"u8, "fourth"u8);
            });
            b.AddItem(static (ref ob) =>
            {
                ob.AddProperty("id"u8, 5);
                ob.AddProperty("label"u8, "fifth"u8);
            });
        });

        Assert.AreEqual(5, root.GetArrayLength());
        Assert.AreEqual(4, (int)root[3].Id);
        Assert.AreEqual(5, (int)root[4].Id);
    }

    #endregion
}
