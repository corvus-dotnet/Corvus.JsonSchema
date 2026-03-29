// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 numeric array (vector) operations produce equivalent results.
/// </summary>
/// <remarks>
/// <para>Both V4 and V5: <c>vector.TryGetNumericValues(Span&lt;int&gt;, out int written)</c></para>
/// <para>V4: <c>Vector.FromValues(ReadOnlySpan&lt;int&gt;)</c></para>
/// <para>V5: <c>Vector.Build(ReadOnlySpan&lt;int&gt;)</c> or <c>Vector.CreateBuilder(workspace, ReadOnlySpan&lt;int&gt;)</c></para>
/// </remarks>
public class NumericArrayEquivalenceTests
{
    private const string VectorJson = """[10,20,30]""";

    [Fact]
    public void V4_TryGetNumericValues()
    {
        var v4 = V4.MigrationIntVector.Parse(VectorJson);
        Span<int> values = stackalloc int[3];
        Assert.True(v4.TryGetNumericValues(values, out int written));
        Assert.Equal(3, written);
        Assert.Equal(10, values[0]);
        Assert.Equal(20, values[1]);
        Assert.Equal(30, values[2]);
    }

    [Fact]
    public void V4_TryGetNumericValues_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationIntVector>.Parse(VectorJson);
        V4.MigrationIntVector v4 = parsedV4.Instance;
        Span<int> values = stackalloc int[3];
        Assert.True(v4.TryGetNumericValues(values, out int written));
        Assert.Equal(3, written);
        Assert.Equal(10, values[0]);
        Assert.Equal(20, values[1]);
        Assert.Equal(30, values[2]);
    }

    [Fact]
    public void V5_TryGetNumericValues()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationIntVector>.Parse(VectorJson);
        V5.MigrationIntVector v5 = parsedV5.RootElement;
        Span<int> values = stackalloc int[3];
        Assert.True(v5.TryGetNumericValues(values, out int written));
        Assert.Equal(3, written);
        Assert.Equal(10, values[0]);
        Assert.Equal(20, values[1]);
        Assert.Equal(30, values[2]);
    }

    [Fact]
    public void V4_ElementAccess()
    {
        var v4 = V4.MigrationIntVector.Parse(VectorJson);
        Assert.Equal(10, (int)v4[0]);
        Assert.Equal(20, (int)v4[1]);
        Assert.Equal(30, (int)v4[2]);
    }

    [Fact]
    public void V4_ElementAccess_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationIntVector>.Parse(VectorJson);
        V4.MigrationIntVector v4 = parsedV4.Instance;
        Assert.Equal(10, (int)v4[0]);
        Assert.Equal(20, (int)v4[1]);
        Assert.Equal(30, (int)v4[2]);
    }

    [Fact]
    public void V5_ElementAccess()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationIntVector>.Parse(VectorJson);
        V5.MigrationIntVector v5 = parsedV5.RootElement;
        Assert.Equal(10, (int)v5[0]);
        Assert.Equal(20, (int)v5[1]);
        Assert.Equal(30, (int)v5[2]);
    }

    [Fact]
    public void V4_DimensionAndRank()
    {
        Assert.Equal(1, V4.MigrationIntVector.Rank);
        Assert.Equal(3, V4.MigrationIntVector.Dimension);
        Assert.Equal(3, V4.MigrationIntVector.ValueBufferSize);
    }

    [Fact]
    public void V5_DimensionAndRank()
    {
        Assert.Equal(1, V5.MigrationIntVector.Rank);
        Assert.Equal(3, V5.MigrationIntVector.Dimension);
        Assert.Equal(3, V5.MigrationIntVector.ValueBufferSize);
    }

    [Fact]
    public void V4_GetArrayLength()
    {
        var v4 = V4.MigrationIntVector.Parse(VectorJson);
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V4_GetArrayLength_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationIntVector>.Parse(VectorJson);
        V4.MigrationIntVector v4 = parsedV4.Instance;
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V5_GetArrayLength()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationIntVector>.Parse(VectorJson);
        V5.MigrationIntVector v5 = parsedV5.RootElement;
        Assert.Equal(3, v5.GetArrayLength());
    }

    [Fact]
    public void BothEngines_SameNumericValues()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationIntVector>.Parse(VectorJson);
        V4.MigrationIntVector v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationIntVector>.Parse(VectorJson);
        V5.MigrationIntVector v5 = parsedV5.RootElement;

        Span<int> v4Values = stackalloc int[3];
        Span<int> v5Values = stackalloc int[3];

        Assert.True(v4.TryGetNumericValues(v4Values, out int v4Written));
        Assert.True(v5.TryGetNumericValues(v5Values, out int v5Written));

        Assert.Equal(v4Written, v5Written);
        for (int i = 0; i < v4Written; i++)
        {
            Assert.Equal(v4Values[i], v5Values[i]);
        }
    }

    #region Construct from span — V4 FromValues vs V5 Build / CreateBuilder

    [Fact]
    public void V4_FromValues()
    {
        // V4: static FromValues(ReadOnlySpan<int>) creates a vector directly.
        var v4 = V4.MigrationIntVector.FromValues([10, 20, 30]);
        Assert.Equal(3, v4.GetArrayLength());
        Assert.Equal(10, (int)v4[0]);
        Assert.Equal(20, (int)v4[1]);
        Assert.Equal(30, (int)v4[2]);
    }

    [Fact]
    public void V5_BuildFromSpan()
    {
        // V5: Build(ReadOnlySpan<int>) returns a Source; pass to CreateBuilder.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        V5.MigrationIntVector.Source source = V5.MigrationIntVector.Build([10, 20, 30]);
        using JsonDocumentBuilder<V5.MigrationIntVector.Mutable> builder =
            V5.MigrationIntVector.CreateBuilder(workspace, source);
        V5.MigrationIntVector v5 = builder.RootElement;

        Assert.Equal(3, v5.GetArrayLength());
        Assert.Equal(10, (int)v5[0]);
        Assert.Equal(20, (int)v5[1]);
        Assert.Equal(30, (int)v5[2]);
    }

    [Fact]
    public void V5_CreateBuilderFromSpan()
    {
        // V5: CreateBuilder(workspace, ReadOnlySpan<int>) — single-call convenience.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationIntVector.Mutable> builder =
            V5.MigrationIntVector.CreateBuilder(workspace, [10, 20, 30]);
        V5.MigrationIntVector v5 = builder.RootElement;

        Assert.Equal(3, v5.GetArrayLength());
        Assert.Equal(10, (int)v5[0]);
        Assert.Equal(20, (int)v5[1]);
        Assert.Equal(30, (int)v5[2]);
    }

    [Fact]
    public void BothEngines_ConstructFromSpan_SameValues()
    {
        // V4: FromValues
        var v4 = V4.MigrationIntVector.FromValues([10, 20, 30]);

        // V5: CreateBuilder from span
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationIntVector.Mutable> builder =
            V5.MigrationIntVector.CreateBuilder(workspace, [10, 20, 30]);
        V5.MigrationIntVector v5 = builder.RootElement;

        // Extract and compare numeric values
        Span<int> v4Values = stackalloc int[3];
        Span<int> v5Values = stackalloc int[3];
        Assert.True(v4.TryGetNumericValues(v4Values, out int v4Written));
        Assert.True(v5.TryGetNumericValues(v5Values, out int v5Written));

        Assert.Equal(v4Written, v5Written);
        for (int i = 0; i < v4Written; i++)
        {
            Assert.Equal(v4Values[i], v5Values[i]);
        }
    }

    #endregion
}