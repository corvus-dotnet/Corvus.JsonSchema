// <copyright file="JsonDocumentBuilderRangeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Xunit;

namespace Corvus.Text.Json.Tests;
public static class JsonDocumentBuilderRangeTests
{
    #region InsertRange Tests

    [Fact]
    public static void InsertRange_AtBeginning_InsertsItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.InsertRange(0, static (ref b) =>
        {
            b.AddItem(10);
            b.AddItem(20);
        });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(5, root.GetArrayLength());
        Assert.Equal(10, root[0].GetInt32());
        Assert.Equal(20, root[1].GetInt32());
        Assert.Equal(1, root[2].GetInt32());
        Assert.Equal(2, root[3].GetInt32());
        Assert.Equal(3, root[4].GetInt32());
    }

    [Fact]
    public static void InsertRange_InMiddle_InsertsItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.InsertRange(1, static (ref b) =>
        {
            b.AddItem(10);
            b.AddItem(20);
        });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(5, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(10, root[1].GetInt32());
        Assert.Equal(20, root[2].GetInt32());
        Assert.Equal(2, root[3].GetInt32());
        Assert.Equal(3, root[4].GetInt32());
    }

    [Fact]
    public static void InsertRange_AtEnd_InsertsItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.InsertRange(3, static (ref b) =>
        {
            b.AddItem(10);
            b.AddItem(20);
        });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(5, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
        Assert.Equal(10, root[3].GetInt32());
        Assert.Equal(20, root[4].GetInt32());
    }

    [Fact]
    public static void InsertRange_EmptyRange_IsNoOp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.InsertRange(0, static (ref b) =>
        {
            // No items added.
        });

        Assert.Equal(3, builderDoc.RootElement.GetArrayLength());
        Assert.Equal("[1,2,3]", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void InsertRange_MixedTypes_InsertsCorrectly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.InsertRange(0, static (ref b) =>
        {
            b.AddItem("hello"u8);
            b.AddItem(true);
            b.AddItemNull();
            b.AddItem(42.5);
        });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(5, root.GetArrayLength());
        Assert.Equal("hello", root[0].GetString());
        Assert.True(root[1].GetBoolean());
        Assert.Equal(JsonValueKind.Null, root[2].ValueKind);
        Assert.Equal(42.5, root[3].GetDouble());
        Assert.Equal(1, root[4].GetInt32());
    }

    [Fact]
    public static void InsertRange_WithObjects_InsertsCorrectly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.InsertRange(0, static (ref b) =>
        {
            b.AddItem(static (ref ob) =>
            {
                ob.AddProperty("name"u8, "Alice"u8);
            });
            b.AddItem(static (ref ob) =>
            {
                ob.AddProperty("name"u8, "Bob"u8);
            });
        });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("{\"name\":\"Alice\"}", root[0].ToString());
        Assert.Equal("{\"name\":\"Bob\"}", root[1].ToString());
    }

    [Fact]
    public static void InsertRange_WithContext_InsertsItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        int[] values = [10, 20, 30];
        builderDoc.RootElement.InsertRange(
            1,
            values,
            static (in ctx, ref b) =>
            {
                for (int i = 0; i < ctx.Length; i++)
                {
                    b.AddItem(ctx[i]);
                }
            });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(6, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(10, root[1].GetInt32());
        Assert.Equal(20, root[2].GetInt32());
        Assert.Equal(30, root[3].GetInt32());
        Assert.Equal(2, root[4].GetInt32());
        Assert.Equal(3, root[5].GetInt32());
    }

    #endregion

    #region AddRange Tests

    [Fact]
    public static void AddRange_AppendsToEnd()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddRange(static (ref b) =>
        {
            b.AddItem(3);
            b.AddItem(4);
        });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
        Assert.Equal(4, root[3].GetInt32());
    }

    [Fact]
    public static void AddRange_ToEmptyArray_AddsItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddRange(static (ref b) =>
        {
            b.AddItem(1);
            b.AddItem(2);
            b.AddItem(3);
        });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
    }

    [Fact]
    public static void AddRange_EmptyRange_IsNoOp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddRange(static (ref b) =>
        {
            // No items added.
        });

        Assert.Equal(3, builderDoc.RootElement.GetArrayLength());
        Assert.Equal("[1,2,3]", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void AddRange_WithContext_AppendsItems()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        int[] values = [2, 3, 4];
        builderDoc.RootElement.AddRange(
            values,
            static (in ctx, ref b) =>
            {
                for (int i = 0; i < ctx.Length; i++)
                {
                    b.AddItem(ctx[i]);
                }
            });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
        Assert.Equal(4, root[3].GetInt32());
    }

    [Fact]
    public static void AddRange_MultipleCallsPreserveOrder()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.AddRange(static (ref b) =>
        {
            b.AddItem(1);
            b.AddItem(2);
        });

        builderDoc.RootElement.AddRange(static (ref b) =>
        {
            b.AddItem(3);
            b.AddItem(4);
        });

        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
        Assert.Equal(4, root[3].GetInt32());
    }

    [Fact]
    public static void InsertRange_ThenSerialize_ProducesValidJson()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a\", \"d\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        builderDoc.RootElement.InsertRange(1, static (ref b) =>
        {
            b.AddItem("b"u8);
            b.AddItem("c"u8);
        });

        string result = builderDoc.RootElement.ToString();
        Assert.Equal("[\"a\",\"b\",\"c\",\"d\"]", result);
    }

    #endregion
}
