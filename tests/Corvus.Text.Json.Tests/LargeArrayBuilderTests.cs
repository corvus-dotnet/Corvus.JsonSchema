// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests;

using Xunit;

/// <summary>
/// Tests for building large arrays via the CVB (ComplexValueBuilder) pattern,
/// exercising value buffer growth when many values are added.
/// </summary>
/// <remarks>
/// <para>
/// These tests cover the scenario where the value buffer in the MetadataDb
/// must grow beyond its initial allocation (16KB) to accommodate many values.
/// Each double value requires a 4-byte header plus the UTF-8 formatted value
/// in the value buffer, so arrays with hundreds or thousands of doubles will
/// exceed the default allocation.
/// </para>
/// </remarks>
public class LargeArrayBuilderTests
{
    /// <summary>
    /// Verifies that building an array of 1000 doubles via CVB produces the
    /// correct array length and values, exercising value buffer growth.
    /// </summary>
    [Fact]
    public void CreateBuilder_LargeDoubleArray_1000Items()
    {
        const int count = 1000;

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            count,
            static (in int ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                for (int i = 0; i < ctx; i++)
                {
                    builder.AddItem((double)i);
                }
            },
            estimatedMemberCount: count + 2);

        JsonElement root = (JsonElement)doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(count, root.GetArrayLength());

        // Spot-check first, last, and middle values
        int idx = 0;
        foreach (JsonElement item in root.EnumerateArray())
        {
            if (idx == 0 || idx == count / 2 || idx == count - 1)
            {
                Assert.Equal((double)idx, item.GetDouble());
            }

            idx++;
        }

        Assert.Equal(count, idx);
    }

    /// <summary>
    /// Verifies that building an array of 10000 doubles via CVB works correctly,
    /// well beyond the initial 16KB value buffer allocation.
    /// </summary>
    [Fact]
    public void CreateBuilder_LargeDoubleArray_10000Items()
    {
        const int count = 10000;

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            count,
            static (in int ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                for (int i = 0; i < ctx; i++)
                {
                    builder.AddItem((double)i * 1.5);
                }
            },
            estimatedMemberCount: count + 2);

        JsonElement root = (JsonElement)doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(count, root.GetArrayLength());

        // Verify all values round-trip correctly
        int idx = 0;
        foreach (JsonElement item in root.EnumerateArray())
        {
            Assert.Equal(idx * 1.5, item.GetDouble());
            idx++;
        }

        Assert.Equal(count, idx);
    }

    /// <summary>
    /// Verifies that building a large array of strings via CVB exercises
    /// value buffer growth for non-numeric values.
    /// </summary>
    [Fact]
    public void CreateBuilder_LargeStringArray_1000Items()
    {
        const int count = 1000;
        string[] values = new string[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = $"item-{i:D4}";
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            values,
            static (in string[] ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                for (int i = 0; i < ctx.Length; i++)
                {
                    builder.AddItem(ctx[i]);
                }
            },
            estimatedMemberCount: count + 2);

        JsonElement root = (JsonElement)doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(count, root.GetArrayLength());

        int idx = 0;
        foreach (JsonElement item in root.EnumerateArray())
        {
            Assert.Equal(values[idx], item.GetString());
            idx++;
        }

        Assert.Equal(count, idx);
    }

    /// <summary>
    /// Verifies that building a large array of mixed types (doubles, booleans, strings)
    /// via CVB correctly handles value buffer growth across value types.
    /// </summary>
    [Fact]
    public void CreateBuilder_LargeMixedArray_3000Items()
    {
        const int count = 3000;

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            count,
            static (in int ctx, ref JsonElement.ArrayBuilder builder) =>
            {
                for (int i = 0; i < ctx; i++)
                {
                    switch (i % 3)
                    {
                        case 0:
                            builder.AddItem((double)i);
                            break;
                        case 1:
                            builder.AddItem(i % 2 == 0);
                            break;
                        case 2:
                            builder.AddItem($"s{i}");
                            break;
                    }
                }
            },
            estimatedMemberCount: count + 2);

        JsonElement root = (JsonElement)doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(count, root.GetArrayLength());

        int idx = 0;
        foreach (JsonElement item in root.EnumerateArray())
        {
            switch (idx % 3)
            {
                case 0:
                    Assert.Equal((double)idx, item.GetDouble());
                    break;
                case 1:
                    Assert.Equal(idx % 2 == 0, item.GetBoolean());
                    break;
                case 2:
                    Assert.Equal($"s{idx}", item.GetString());
                    break;
            }

            idx++;
        }

        Assert.Equal(count, idx);
    }
}
