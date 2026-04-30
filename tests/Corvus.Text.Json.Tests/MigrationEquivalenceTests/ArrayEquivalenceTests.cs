// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Xunit;

using V4 = Corvus.Text.Json.Tests.MigrationModels.V4;
using V5 = Corvus.Text.Json.Tests.MigrationModels.V5;

/// <summary>
/// Verifies that V4 immutable array ops and V5 mutable array ops produce equivalent results.
/// </summary>
/// <remarks>
/// <para>V4: <c>array.Add(item)</c> returns new array — functional style</para>
/// <para>V5: <c>mutable.AddItem(source)</c> mutates in-place — imperative style via <see cref="Corvus.Text.Json.JsonWorkspace"/></para>
/// </remarks>
public class ArrayEquivalenceTests
{
    private const string ArrayJson = """[{"id":1,"label":"first"},{"id":2,"label":"second"},{"id":3,"label":"third"}]""";
    private const string NewItemJson = """{"id":99,"label":"new"}""";
    private const string SecondItemJson = """{"id":2,"label":"second"}""";

    [Fact]
    public void V4_IndexElement()
    {
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        Assert.Equal(1, (int)v4[0].Id);
        Assert.Equal(2, (int)v4[1].Id);
        Assert.Equal(3, (int)v4[2].Id);
    }

    [Fact]
    public void V4_IndexElement_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        Assert.Equal(1, (int)v4[0].Id);
        Assert.Equal(2, (int)v4[1].Id);
        Assert.Equal(3, (int)v4[2].Id);
    }

    [Fact]
    public void V5_IndexElement()
    {
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        V5.MigrationItemArray v5 = doc.RootElement;
        Assert.Equal(1, (int)v5[0].Id);
        Assert.Equal(2, (int)v5[1].Id);
        Assert.Equal(3, (int)v5[2].Id);
    }

    [Fact]
    public void V4_GetArrayLength()
    {
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V4_GetArrayLength_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V5_GetArrayLength()
    {
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        V5.MigrationItemArray v5 = doc.RootElement;
        Assert.Equal(3, v5.GetArrayLength());
    }

    [Fact]
    public void V4_EnumerateArray()
    {
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        int count = 0;
        foreach (V4.MigrationItemArray.RequiredId item in v4.EnumerateArray())
        {
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void V4_EnumerateArray_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        int count = 0;
        foreach (V4.MigrationItemArray.RequiredId item in v4.EnumerateArray())
        {
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void V5_EnumerateArray()
    {
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        V5.MigrationItemArray v5 = doc.RootElement;
        int count = 0;
        foreach (V5.MigrationItemArray.RequiredId item in v5.EnumerateArray())
        {
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void V4_AddItem()
    {
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        var newItem = V4.MigrationItemArray.RequiredId.Parse(NewItemJson);
        V4.MigrationItemArray updated = v4.Add(newItem);
        Assert.Equal(4, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[3].Id);
    }

    [Fact]
    public void V4_AddItem_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        using var parsedV4NewItem = Corvus.Json.ParsedValue<V4.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V4.MigrationItemArray.RequiredId newItem = parsedV4NewItem.Instance;
        V4.MigrationItemArray updated = v4.Add(newItem);
        Assert.Equal(4, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[3].Id);
    }

    [Fact]
    public void V5_AddItem()
    {
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        using var newItemDoc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V5.MigrationItemArray.RequiredId newItem = newItemDoc.RootElement;
        root.AddItem(newItem);

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(99, (int)root[3].Id);
    }

    [Fact]
    public void BothEngines_AddItem_SameResult()
    {
        // V4: functional Add returns new array
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        using var parsedV4NewItem = Corvus.Json.ParsedValue<V4.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V4.MigrationItemArray v4Updated = v4.Add(parsedV4NewItem.Instance);

        // V5: imperative AddItem mutates in place
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        using var newItemDoc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        root.AddItem(newItemDoc.RootElement);

        Assert.Equal(v4Updated.GetArrayLength(), root.GetArrayLength());
        Assert.Equal((int)v4Updated[3].Id, (int)root[3].Id);
    }

    [Fact]
    public void V4_InsertItem()
    {
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        var newItem = V4.MigrationItemArray.RequiredId.Parse(NewItemJson);
        V4.MigrationItemArray updated = v4.Insert(1, newItem);
        Assert.Equal(4, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[1].Id);
        Assert.Equal(2, (int)updated[2].Id);
    }

    [Fact]
    public void V4_InsertItem_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        using var parsedV4NewItem = Corvus.Json.ParsedValue<V4.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V4.MigrationItemArray.RequiredId newItem = parsedV4NewItem.Instance;
        V4.MigrationItemArray updated = v4.Insert(1, newItem);
        Assert.Equal(4, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[1].Id);
        Assert.Equal(2, (int)updated[2].Id);
    }

    [Fact]
    public void V5_InsertItem()
    {
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        using var newItemDoc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V5.MigrationItemArray.RequiredId newItem = newItemDoc.RootElement;
        root.InsertItem(1, newItem);

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(99, (int)root[1].Id);
        Assert.Equal(2, (int)root[2].Id);
    }

    [Fact]
    public void V4_SetItem()
    {
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        var newItem = V4.MigrationItemArray.RequiredId.Parse(NewItemJson);
        V4.MigrationItemArray updated = v4.SetItem(1, newItem);
        Assert.Equal(3, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[1].Id);
    }

    [Fact]
    public void V4_SetItem_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        using var parsedV4NewItem = Corvus.Json.ParsedValue<V4.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V4.MigrationItemArray.RequiredId newItem = parsedV4NewItem.Instance;
        V4.MigrationItemArray updated = v4.SetItem(1, newItem);
        Assert.Equal(3, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[1].Id);
    }

    [Fact]
    public void V5_SetItem()
    {
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        using var newItemDoc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V5.MigrationItemArray.RequiredId newItem = newItemDoc.RootElement;
        root.SetItem(1, newItem);

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(99, (int)root[1].Id);
    }

    [Fact]
    public void BothEngines_SetItem_SameResult()
    {
        // V4: functional SetItem returns new array
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        using var parsedV4NewItem = Corvus.Json.ParsedValue<V4.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V4.MigrationItemArray v4Updated = v4.SetItem(1, parsedV4NewItem.Instance);

        // V5: imperative SetItem mutates in place
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        using var newItemDoc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        root.SetItem(1, newItemDoc.RootElement);

        Assert.Equal(v4Updated.GetArrayLength(), root.GetArrayLength());
        Assert.Equal((int)v4Updated[1].Id, (int)root[1].Id);
    }

    [Fact]
    public void V4_RemoveAt()
    {
        // V4: RemoveAt by index — now a public method.
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        V4.MigrationItemArray updated = v4.RemoveAt(1);
        Assert.Equal(2, updated.GetArrayLength());
        Assert.Equal(1, (int)updated[0].Id);
        Assert.Equal(3, (int)updated[1].Id);
    }

    [Fact]
    public void V4_RemoveAt_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        // V4: RemoveAt by index — now a public method.
        V4.MigrationItemArray updated = v4.RemoveAt(1);
        Assert.Equal(2, updated.GetArrayLength());
        Assert.Equal(1, (int)updated[0].Id);
        Assert.Equal(3, (int)updated[1].Id);
    }

    [Fact]
    public void V5_RemoveAt()
    {
        // V5: RemoveAt by index — removes the item at the specified position.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        root.RemoveAt(1);
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(1, (int)root[0].Id);
        Assert.Equal(3, (int)root[1].Id);
    }

    [Fact]
    public void V4_RemoveByValue()
    {
        // V4: Remove by value — finds and removes the first matching item.
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        V4.MigrationItemArray updated = v4.Remove(v4[1]);
        Assert.Equal(2, updated.GetArrayLength());
        Assert.Equal(1, (int)updated[0].Id);
        Assert.Equal(3, (int)updated[1].Id);
    }

    [Fact]
    public void V4_RemoveByValue_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        // V4: Remove by value — finds and removes the first matching item.
        V4.MigrationItemArray updated = v4.Remove(v4[1]);
        Assert.Equal(2, updated.GetArrayLength());
        Assert.Equal(1, (int)updated[0].Id);
        Assert.Equal(3, (int)updated[1].Id);
    }

    [Fact]
    public void V5_RemoveByValue()
    {
        // V5: Remove by value — finds and removes the first matching item.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        using var itemDoc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray.RequiredId>.Parse(SecondItemJson);
        V5.MigrationItemArray.RequiredId itemToRemove = itemDoc.RootElement;
        bool removed = root.Remove(itemToRemove);

        Assert.True(removed);
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(1, (int)root[0].Id);
        Assert.Equal(3, (int)root[1].Id);
    }

    [Fact]
    public void BothEngines_RemoveByValue_SameResult()
    {
        // V4: functional Remove returns new array
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        V4.MigrationItemArray v4Updated = v4.Remove(v4[1]);

        // V5: imperative Remove mutates in place
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        using var itemDoc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray.RequiredId>.Parse(SecondItemJson);
        root.Remove(itemDoc.RootElement);

        Assert.Equal(v4Updated.GetArrayLength(), root.GetArrayLength());
        Assert.Equal((int)v4Updated[0].Id, (int)root[0].Id);
        Assert.Equal((int)v4Updated[1].Id, (int)root[1].Id);
    }

    [Fact]
    public void V4_RemoveRange()
    {
        // V4: functional RemoveRange(index, count) returns new array without the range.
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        V4.MigrationItemArray updated = v4.RemoveRange(0, 2);
        Assert.Equal(1, updated.GetArrayLength());
        Assert.Equal(3, (int)updated[0].Id);
    }

    [Fact]
    public void V4_RemoveRange_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        V4.MigrationItemArray updated = v4.RemoveRange(0, 2);
        Assert.Equal(1, updated.GetArrayLength());
        Assert.Equal(3, (int)updated[0].Id);
    }

    [Fact]
    public void V5_RemoveRange()
    {
        // V5: imperative RemoveRange(startIndex, count) on the mutable type.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        root.RemoveRange(0, 2);
        Assert.Equal(1, root.GetArrayLength());
        Assert.Equal(3, (int)root[0].Id);
    }

    [Fact]
    public void V4_Replace()
    {
        // V4: functional Replace(old, new) — finds first matching item and replaces it.
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        V4.MigrationItemArray.RequiredId oldItem = v4[1];
        var newItem = V4.MigrationItemArray.RequiredId.Parse(NewItemJson);
        V4.MigrationItemArray updated = v4.Replace(oldItem, newItem);
        Assert.Equal(3, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[1].Id);
    }

    [Fact]
    public void V4_Replace_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        V4.MigrationItemArray.RequiredId oldItem = v4[1];
        using var parsedNew = Corvus.Json.ParsedValue<V4.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V4.MigrationItemArray updated = v4.Replace(oldItem, parsedNew.Instance);
        Assert.Equal(3, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[1].Id);
    }

    [Fact]
    public void V5_Replace()
    {
        // V5: no direct Replace method — find the item index then SetItem.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        using var newItemDoc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray.RequiredId>.Parse(NewItemJson);

        // Find the item with id==2 and replace it
        int indexToReplace = -1;
        for (int i = 0; i < root.GetArrayLength(); i++)
        {
            if ((int)root[i].Id == 2)
            {
                indexToReplace = i;
                break;
            }
        }

        Assert.True(indexToReplace >= 0);
        root.SetItem(indexToReplace, newItemDoc.RootElement);
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(99, (int)root[1].Id);
    }

    [Fact]
    public void V4_AddRange()
    {
        // V4: functional AddRange(IEnumerable) appends multiple items.
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        var newItem = V4.MigrationItemArray.RequiredId.Parse(NewItemJson);
        V4.MigrationItemArray updated = v4.AddRange(new[] { newItem, newItem });
        Assert.Equal(5, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[3].Id);
        Assert.Equal(99, (int)updated[4].Id);
    }

    [Fact]
    public void V4_AddRange_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        using var parsedNew = Corvus.Json.ParsedValue<V4.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V4.MigrationItemArray updated = v4.AddRange(new[] { parsedNew.Instance, parsedNew.Instance });
        Assert.Equal(5, updated.GetArrayLength());
    }

    [Fact]
    public void V4_InsertRange()
    {
        // V4: functional InsertRange(index, items) inserts multiple items at position.
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        var newItem = V4.MigrationItemArray.RequiredId.Parse(NewItemJson);
        V4.MigrationItemArray updated = v4.InsertRange(1, new[] { newItem, newItem });
        Assert.Equal(5, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[1].Id);
        Assert.Equal(99, (int)updated[2].Id);
        Assert.Equal(2, (int)updated[3].Id);
    }

    [Fact]
    public void V4_InsertRange_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        using var parsedNew = Corvus.Json.ParsedValue<V4.MigrationItemArray.RequiredId>.Parse(NewItemJson);
        V4.MigrationItemArray updated = v4.InsertRange(1, new[] { parsedNew.Instance, parsedNew.Instance });
        Assert.Equal(5, updated.GetArrayLength());
        Assert.Equal(99, (int)updated[1].Id);
    }

    [Fact]
    public void V4_FromItems()
    {
        // V4: static FromItems(...) creates array from individual items.
        var item1 = V4.MigrationItemArray.RequiredId.Parse("""{"id":1,"label":"first"}""");
        var item2 = V4.MigrationItemArray.RequiredId.Parse("""{"id":2,"label":"second"}""");
        var v4 = V4.MigrationItemArray.FromItems(item1, item2);
        Assert.Equal(2, v4.GetArrayLength());
        Assert.Equal(1, (int)v4[0].Id);
        Assert.Equal(2, (int)v4[1].Id);
    }

    [Fact]
    public void V5_CreateFromItems()
    {
        // V5: create an empty array builder, then AddItem — equivalent to V4 FromItems.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder = V5.MigrationItemArray.CreateBuilder(workspace);
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        using var doc1 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray.RequiredId>.Parse("""{"id":1,"label":"first"}""");
        using var doc2 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray.RequiredId>.Parse("""{"id":2,"label":"second"}""");
        root.AddItem(doc1.RootElement);
        root.AddItem(doc2.RootElement);

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(1, (int)root[0].Id);
        Assert.Equal(2, (int)root[1].Id);
    }
}