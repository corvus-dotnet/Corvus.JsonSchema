// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for mutable document operations that exercise previously-uncovered TContext
/// code paths in SetProperty, InsertItem, and AddItem overloads.
/// Identified through dotnet-coverage analysis.
/// </summary>
public static class MutableDocumentCoverageTests
{
    #region SetProperty<TContext> with ObjectBuilder — string overload (insert + replace branches)

    [Fact]
    public static void SetProperty_WithContextObjectBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int multiplier = 10;

        doc.RootElement.SetProperty(
            "nested",
            multiplier,
            static (in int ctx, ref JsonElement.ObjectBuilder ob) =>
            {
                ob.AddProperty("a"u8, ctx * 1);
                ob.AddProperty("b"u8, ctx * 2);
            });

        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.GetProperty("nested"u8).ValueKind);
        Assert.Equal(10, root.GetProperty("nested"u8).GetProperty("a"u8).GetInt32());
        Assert.Equal(20, root.GetProperty("nested"u8).GetProperty("b"u8).GetInt32());
    }

    [Fact]
    public static void SetProperty_ReplaceExisting_WithContextObjectBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 42}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        string prefix = "value_";

        doc.RootElement.SetProperty(
            "x",
            prefix,
            static (in string ctx, ref JsonElement.ObjectBuilder ob) =>
            {
                ob.AddProperty("name"u8, ctx + "one");
            });

        JsonElement.Mutable x = doc.RootElement.GetProperty("x"u8);
        Assert.Equal(JsonValueKind.Object, x.ValueKind);
        Assert.Equal("value_one", x.GetProperty("name"u8).GetString());
    }

    #endregion

    #region SetProperty<TContext> with ArrayBuilder — string overload (insert + replace branches)

    [Fact]
    public static void SetProperty_WithContextArrayBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int offset = 100;

        doc.RootElement.SetProperty(
            "items",
            offset,
            static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
            {
                ab.AddItem(ctx + 1);
                ab.AddItem(ctx + 2);
                ab.AddItem(ctx + 3);
            });

        JsonElement.Mutable items = doc.RootElement.GetProperty("items"u8);
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Equal(3, items.GetArrayLength());
        Assert.Equal(101, items[0].GetInt32());
        Assert.Equal(102, items[1].GetInt32());
        Assert.Equal(103, items[2].GetInt32());
    }

    [Fact]
    public static void SetProperty_ReplaceExisting_WithContextArrayBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 42}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int factor = 5;

        doc.RootElement.SetProperty(
            "x",
            factor,
            static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
            {
                ab.AddItem(ctx * 10);
                ab.AddItem(ctx * 20);
            });

        JsonElement.Mutable x = doc.RootElement.GetProperty("x"u8);
        Assert.Equal(JsonValueKind.Array, x.ValueKind);
        Assert.Equal(50, x[0].GetInt32());
        Assert.Equal(100, x[1].GetInt32());
    }

    #endregion

    #region SetProperty<TContext> with ObjectBuilder — UTF-8 overload (insert + replace branches)

    [Fact]
    public static void SetProperty_Utf8_WithContextObjectBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        double scale = 2.5;

        doc.RootElement.SetProperty(
            "nested"u8,
            scale,
            static (in double ctx, ref JsonElement.ObjectBuilder ob) =>
            {
                ob.AddProperty("scaled"u8, ctx * 4);
            });

        Assert.Equal(10.0, doc.RootElement.GetProperty("nested"u8).GetProperty("scaled"u8).GetDouble());
    }

    [Fact]
    public static void SetProperty_Utf8_ReplaceExisting_WithContextObjectBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 42}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int v = 7;

        doc.RootElement.SetProperty(
            "x"u8,
            v,
            static (in int ctx, ref JsonElement.ObjectBuilder ob) =>
            {
                ob.AddProperty("replaced"u8, ctx);
            });

        Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("x"u8).ValueKind);
        Assert.Equal(7, doc.RootElement.GetProperty("x"u8).GetProperty("replaced"u8).GetInt32());
    }

    #endregion

    #region SetProperty<TContext> with ArrayBuilder — UTF-8 overload (insert + replace branches)

    [Fact]
    public static void SetProperty_Utf8_WithContextArrayBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int start = 0;

        doc.RootElement.SetProperty(
            "items"u8,
            start,
            static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
            {
                for (int i = ctx; i < ctx + 5; i++)
                {
                    ab.AddItem(i);
                }
            });

        Assert.Equal(5, doc.RootElement.GetProperty("items"u8).GetArrayLength());
    }

    [Fact]
    public static void SetProperty_Utf8_ReplaceExisting_WithContextArrayBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("""{"x": 42}""");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int count = 3;

        doc.RootElement.SetProperty(
            "x"u8,
            count,
            static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
            {
                for (int i = 0; i < ctx; i++)
                {
                    ab.AddItem(i * 10);
                }
            });

        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("x"u8).ValueKind);
        Assert.Equal(3, doc.RootElement.GetProperty("x"u8).GetArrayLength());
        Assert.Equal(20, doc.RootElement.GetProperty("x"u8)[2].GetInt32());
    }

    #endregion

    #region InsertItem<TContext> + AddItem<TContext> with ArrayBuilder

    [Fact]
    public static void InsertItem_WithContextArrayBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int offset = 10;

        doc.RootElement.InsertItem(
            0,
            offset,
            static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
            {
                ab.AddItem(ctx);
                ab.AddItem(ctx + 1);
            });

        Assert.Equal(4, doc.RootElement.GetArrayLength());
        Assert.Equal(JsonValueKind.Array, doc.RootElement[0].ValueKind);
        Assert.Equal(2, doc.RootElement[0].GetArrayLength());
    }

    [Fact]
    public static void AddItem_WithContextArrayBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);

        int count = 3;

        doc.RootElement.AddItem(
            count,
            static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
            {
                for (int i = 0; i < ctx; i++)
                {
                    ab.AddItem(i);
                }
            });

        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal(3, doc.RootElement[1].GetArrayLength());
    }

    #endregion
}
