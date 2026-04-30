// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated fixed-size numeric array (tensor) types.
/// Covers rank 1 (vector), rank 2 (matrix), rank 3 (cube) with both double and int32 numeric types.
/// Tests static properties, CreateTensor, wrong-size validation, and round-trip materialisation.
/// </summary>
public class GeneratedNumericArrayTests
{
    #region Static properties

    [Fact]
    public void Rank1DoubleVector_StaticProperties()
    {
        Assert.Equal(1, Rank1DoubleVector.Rank);
        Assert.Equal(3, Rank1DoubleVector.Dimension);
        Assert.Equal(3, Rank1DoubleVector.ValueBufferSize);
    }

    [Fact]
    public void Rank1Int32Vector_StaticProperties()
    {
        Assert.Equal(1, Rank1Int32Vector.Rank);
        Assert.Equal(4, Rank1Int32Vector.Dimension);
        Assert.Equal(4, Rank1Int32Vector.ValueBufferSize);
    }

    [Fact]
    public void Rank2DoubleMatrix_StaticProperties()
    {
        Assert.Equal(2, Rank2DoubleMatrix.Rank);
        Assert.Equal(2, Rank2DoubleMatrix.Dimension);
        Assert.Equal(6, Rank2DoubleMatrix.ValueBufferSize);
    }

    [Fact]
    public void Rank3Int32Cube_StaticProperties()
    {
        Assert.Equal(3, Rank3Int32Cube.Rank);
        Assert.Equal(2, Rank3Int32Cube.Dimension);
        Assert.Equal(12, Rank3Int32Cube.ValueBufferSize);
    }

    #endregion

    #region Rank 1 double vector — Build + CreateTensor

    [Fact]
    public void Rank1DoubleVector_Build_CreateTensor()
    {
        using var workspace = JsonWorkspace.Create();

        Rank1DoubleVector.Source source = Rank1DoubleVector.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<double> values = [1.5, 2.5, 3.5];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<Rank1DoubleVector.Mutable> doc =
            Rank1DoubleVector.CreateBuilder(workspace, source);
        Rank1DoubleVector.Mutable root = doc.RootElement;

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1.5, (double)root[0]);
        Assert.Equal(2.5, (double)root[1]);
        Assert.Equal(3.5, (double)root[2]);
    }

    [Fact]
    public void Rank1DoubleVector_Build_CreateTensor_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        Rank1DoubleVector.Source source = Rank1DoubleVector.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<double> values = [1.5, 2.5, 3.5];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<Rank1DoubleVector.Mutable> doc =
            Rank1DoubleVector.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<Rank1DoubleVector>.Parse(json);
        Assert.Equal(3, reparsed.RootElement.GetArrayLength());
        Assert.Equal(1.5, (double)reparsed.RootElement[0]);
        Assert.Equal(2.5, (double)reparsed.RootElement[1]);
        Assert.Equal(3.5, (double)reparsed.RootElement[2]);
    }

    #endregion

    #region Rank 1 int32 vector — Build + CreateTensor

    [Fact]
    public void Rank1Int32Vector_Build_CreateTensor()
    {
        using var workspace = JsonWorkspace.Create();

        Rank1Int32Vector.Source source = Rank1Int32Vector.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<int> values = [10, 20, 30, 40];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<Rank1Int32Vector.Mutable> doc =
            Rank1Int32Vector.CreateBuilder(workspace, source);
        Rank1Int32Vector.Mutable root = doc.RootElement;

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(10, (int)root[0]);
        Assert.Equal(20, (int)root[1]);
        Assert.Equal(30, (int)root[2]);
        Assert.Equal(40, (int)root[3]);
    }

    [Fact]
    public void Rank1Int32Vector_Build_CreateTensor_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        Rank1Int32Vector.Source source = Rank1Int32Vector.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<int> values = [10, 20, 30, 40];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<Rank1Int32Vector.Mutable> doc =
            Rank1Int32Vector.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<Rank1Int32Vector>.Parse(json);
        Assert.Equal(4, reparsed.RootElement.GetArrayLength());
        Assert.Equal(10, (int)reparsed.RootElement[0]);
        Assert.Equal(20, (int)reparsed.RootElement[1]);
        Assert.Equal(30, (int)reparsed.RootElement[2]);
        Assert.Equal(40, (int)reparsed.RootElement[3]);
    }

    #endregion

    #region Rank 1 wrong-size tensor — ArgumentException

    [Fact]
    public void Rank1DoubleVector_CreateTensor_WrongSize_ThrowsArgumentException()
    {
        using var workspace = JsonWorkspace.Create();

        Assert.Throws<ArgumentException>(() =>
        {
            Rank1DoubleVector.Source source = Rank1DoubleVector.Build(
                static (ref builder) =>
                {
                    ReadOnlySpan<double> values = [1.0, 2.0];
                    builder.CreateTensor(values);
                });

            Rank1DoubleVector.CreateBuilder(workspace, source).Dispose();
        });
    }

    [Fact]
    public void Rank1Int32Vector_CreateTensor_WrongSize_ThrowsArgumentException()
    {
        using var workspace = JsonWorkspace.Create();

        Assert.Throws<ArgumentException>(() =>
        {
            Rank1Int32Vector.Source source = Rank1Int32Vector.Build(
                static (ref builder) =>
                {
                    ReadOnlySpan<int> values = [1, 2, 3, 4, 5];
                    builder.CreateTensor(values);
                });

            Rank1Int32Vector.CreateBuilder(workspace, source).Dispose();
        });
    }

    #endregion

    #region Rank 2 double matrix — Build + CreateTensor

    [Fact]
    public void Rank2DoubleMatrix_Build_CreateTensor()
    {
        using var workspace = JsonWorkspace.Create();

        // 2×3 matrix: [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]
        Rank2DoubleMatrix.Source source = Rank2DoubleMatrix.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<double> values = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<Rank2DoubleMatrix.Mutable> doc =
            Rank2DoubleMatrix.CreateBuilder(workspace, source);
        Rank2DoubleMatrix.Mutable root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());

        // First row: [1.0, 2.0, 3.0]
        Rank2DoubleMatrix.JsonDoubleArray.Mutable row0 = root[0];
        Assert.Equal(3, row0.GetArrayLength());
        Assert.Equal(1.0, (double)row0[0]);
        Assert.Equal(2.0, (double)row0[1]);
        Assert.Equal(3.0, (double)row0[2]);

        // Second row: [4.0, 5.0, 6.0]
        Rank2DoubleMatrix.JsonDoubleArray.Mutable row1 = root[1];
        Assert.Equal(3, row1.GetArrayLength());
        Assert.Equal(4.0, (double)row1[0]);
        Assert.Equal(5.0, (double)row1[1]);
        Assert.Equal(6.0, (double)row1[2]);
    }

    [Fact]
    public void Rank2DoubleMatrix_Build_CreateTensor_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        Rank2DoubleMatrix.Source source = Rank2DoubleMatrix.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<double> values = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<Rank2DoubleMatrix.Mutable> doc =
            Rank2DoubleMatrix.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<Rank2DoubleMatrix>.Parse(json);
        Assert.Equal(2, reparsed.RootElement.GetArrayLength());

        Rank2DoubleMatrix.JsonDoubleArray row0 = reparsed.RootElement[0];
        Assert.Equal(3, row0.GetArrayLength());
        Assert.Equal(1.0, (double)row0[0]);
        Assert.Equal(2.0, (double)row0[1]);
        Assert.Equal(3.0, (double)row0[2]);

        Rank2DoubleMatrix.JsonDoubleArray row1 = reparsed.RootElement[1];
        Assert.Equal(3, row1.GetArrayLength());
        Assert.Equal(4.0, (double)row1[0]);
        Assert.Equal(5.0, (double)row1[1]);
        Assert.Equal(6.0, (double)row1[2]);
    }

    [Fact]
    public void Rank2DoubleMatrix_CreateTensor_WrongSize_ThrowsArgumentException()
    {
        using var workspace = JsonWorkspace.Create();

        Assert.Throws<ArgumentException>(() =>
        {
            Rank2DoubleMatrix.Source source = Rank2DoubleMatrix.Build(
                static (ref builder) =>
                {
                    ReadOnlySpan<double> values = [1.0, 2.0, 3.0];
                    builder.CreateTensor(values);
                });

            Rank2DoubleMatrix.CreateBuilder(workspace, source).Dispose();
        });
    }

    #endregion

    #region Rank 2 double matrix — inner type static properties

    [Fact]
    public void Rank2DoubleMatrix_InnerType_StaticProperties()
    {
        Assert.Equal(1, Rank2DoubleMatrix.JsonDoubleArray.Rank);
        Assert.Equal(3, Rank2DoubleMatrix.JsonDoubleArray.Dimension);
        Assert.Equal(3, Rank2DoubleMatrix.JsonDoubleArray.ValueBufferSize);
    }

    #endregion

    #region Rank 3 int32 cube — Build + CreateTensor

    [Fact]
    public void Rank3Int32Cube_Build_CreateTensor()
    {
        using var workspace = JsonWorkspace.Create();

        // 2×2×3 cube: [[[1,2,3],[4,5,6]],[[7,8,9],[10,11,12]]]
        Rank3Int32Cube.Source source = Rank3Int32Cube.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<int> values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<Rank3Int32Cube.Mutable> doc =
            Rank3Int32Cube.CreateBuilder(workspace, source);
        Rank3Int32Cube.Mutable root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());

        // First plane: [[1,2,3],[4,5,6]]
        Rank3Int32Cube.ItemsArray2Array.Mutable plane0 = root[0];
        Assert.Equal(2, plane0.GetArrayLength());
        Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.Mutable row00 = plane0[0];
        Assert.Equal(3, row00.GetArrayLength());
        Assert.Equal(1, (int)row00[0]);
        Assert.Equal(2, (int)row00[1]);
        Assert.Equal(3, (int)row00[2]);
        Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.Mutable row01 = plane0[1];
        Assert.Equal(3, row01.GetArrayLength());
        Assert.Equal(4, (int)row01[0]);
        Assert.Equal(5, (int)row01[1]);
        Assert.Equal(6, (int)row01[2]);

        // Second plane: [[7,8,9],[10,11,12]]
        Rank3Int32Cube.ItemsArray2Array.Mutable plane1 = root[1];
        Assert.Equal(2, plane1.GetArrayLength());
        Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.Mutable row10 = plane1[0];
        Assert.Equal(3, row10.GetArrayLength());
        Assert.Equal(7, (int)row10[0]);
        Assert.Equal(8, (int)row10[1]);
        Assert.Equal(9, (int)row10[2]);
        Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.Mutable row11 = plane1[1];
        Assert.Equal(3, row11.GetArrayLength());
        Assert.Equal(10, (int)row11[0]);
        Assert.Equal(11, (int)row11[1]);
        Assert.Equal(12, (int)row11[2]);
    }

    [Fact]
    public void Rank3Int32Cube_Build_CreateTensor_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        Rank3Int32Cube.Source source = Rank3Int32Cube.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<int> values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<Rank3Int32Cube.Mutable> doc =
            Rank3Int32Cube.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<Rank3Int32Cube>.Parse(json);
        Assert.Equal(2, reparsed.RootElement.GetArrayLength());

        // Verify first plane, first row
        Rank3Int32Cube.ItemsArray2Array plane0 = reparsed.RootElement[0];
        Assert.Equal(2, plane0.GetArrayLength());
        Rank3Int32Cube.ItemsArray2Array.JsonInt32Array row00 = plane0[0];
        Assert.Equal(1, (int)row00[0]);
        Assert.Equal(2, (int)row00[1]);
        Assert.Equal(3, (int)row00[2]);

        // Verify last plane, last row
        Rank3Int32Cube.ItemsArray2Array plane1 = reparsed.RootElement[1];
        Rank3Int32Cube.ItemsArray2Array.JsonInt32Array row11 = plane1[1];
        Assert.Equal(10, (int)row11[0]);
        Assert.Equal(11, (int)row11[1]);
        Assert.Equal(12, (int)row11[2]);
    }

    [Fact]
    public void Rank3Int32Cube_CreateTensor_WrongSize_ThrowsArgumentException()
    {
        using var workspace = JsonWorkspace.Create();

        Assert.Throws<ArgumentException>(() =>
        {
            Rank3Int32Cube.Source source = Rank3Int32Cube.Build(
                static (ref builder) =>
                {
                    ReadOnlySpan<int> values = [1, 2, 3, 4, 5, 6];
                    builder.CreateTensor(values);
                });

            Rank3Int32Cube.CreateBuilder(workspace, source).Dispose();
        });
    }

    #endregion

    #region Rank 3 int32 cube — inner type static properties

    [Fact]
    public void Rank3Int32Cube_InnerTypes_StaticProperties()
    {
        // Middle rank: 2×3 matrix
        Assert.Equal(2, Rank3Int32Cube.ItemsArray2Array.Rank);
        Assert.Equal(2, Rank3Int32Cube.ItemsArray2Array.Dimension);
        Assert.Equal(6, Rank3Int32Cube.ItemsArray2Array.ValueBufferSize);

        // Innermost rank: 3-element vector
        Assert.Equal(1, Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.Rank);
        Assert.Equal(3, Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.Dimension);
        Assert.Equal(3, Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.ValueBufferSize);
    }

    #endregion

    #region TryGetNumericValues — immutable

    [Fact]
    public void Rank1DoubleVector_TryGetNumericValues_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<Rank1DoubleVector>.Parse("[1.1, 2.2, 3.3]");

        Span<double> buffer = stackalloc double[3];
        Assert.True(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.Equal(3, written);
        Assert.Equal(1.1, buffer[0]);
        Assert.Equal(2.2, buffer[1]);
        Assert.Equal(3.3, buffer[2]);
    }

    [Fact]
    public void Rank1DoubleVector_TryGetNumericValues_BufferTooSmall_ReturnsFalse()
    {
        using var doc =
            ParsedJsonDocument<Rank1DoubleVector>.Parse("[1.1, 2.2, 3.3]");

        Span<double> buffer = stackalloc double[2];
        Assert.False(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.Equal(2, written);
        Assert.Equal(1.1, buffer[0]);
        Assert.Equal(2.2, buffer[1]);
    }

    [Fact]
    public void Rank1Int32Vector_TryGetNumericValues_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<Rank1Int32Vector>.Parse("[10, 20, 30, 40]");

        Span<int> buffer = stackalloc int[4];
        Assert.True(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.Equal(4, written);
        Assert.Equal(10, buffer[0]);
        Assert.Equal(20, buffer[1]);
        Assert.Equal(30, buffer[2]);
        Assert.Equal(40, buffer[3]);
    }

    [Fact]
    public void Rank2DoubleMatrix_TryGetNumericValues_FlattensValues()
    {
        using var doc =
            ParsedJsonDocument<Rank2DoubleMatrix>.Parse("[[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]");

        Span<double> buffer = stackalloc double[6];
        Assert.True(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.Equal(6, written);
        Assert.Equal(1.0, buffer[0]);
        Assert.Equal(2.0, buffer[1]);
        Assert.Equal(3.0, buffer[2]);
        Assert.Equal(4.0, buffer[3]);
        Assert.Equal(5.0, buffer[4]);
        Assert.Equal(6.0, buffer[5]);
    }

    [Fact]
    public void Rank2DoubleMatrix_TryGetNumericValues_BufferTooSmall_ReturnsFalse()
    {
        using var doc =
            ParsedJsonDocument<Rank2DoubleMatrix>.Parse("[[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]");

        Span<double> buffer = stackalloc double[4];
        Assert.False(doc.RootElement.TryGetNumericValues(buffer, out int written));
        // First row (3 values) succeeds, second row fails partway
        Assert.True(written >= 3);
    }

    [Fact]
    public void Rank3Int32Cube_TryGetNumericValues_FlattensValues()
    {
        using var doc =
            ParsedJsonDocument<Rank3Int32Cube>.Parse("[[[1,2,3],[4,5,6]],[[7,8,9],[10,11,12]]]");

        Span<int> buffer = stackalloc int[12];
        Assert.True(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.Equal(12, written);
        for (int i = 0; i < 12; i++)
        {
            Assert.Equal(i + 1, buffer[i]);
        }
    }

    #endregion

    #region TryGetNumericValues — mutable

    [Fact]
    public void Rank1DoubleVector_Mutable_TryGetNumericValues_Succeeds()
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc =
            ParsedJsonDocument<Rank1DoubleVector>.Parse("[1.1, 2.2, 3.3]");
        using JsonDocumentBuilder<Rank1DoubleVector.Mutable> doc =
            sourceDoc.RootElement.CreateBuilder(workspace);

        Span<double> buffer = stackalloc double[3];
        Assert.True(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.Equal(3, written);
        Assert.Equal(1.1, buffer[0]);
        Assert.Equal(2.2, buffer[1]);
        Assert.Equal(3.3, buffer[2]);
    }

    [Fact]
    public void Rank2DoubleMatrix_Mutable_TryGetNumericValues_FlattensValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc =
            ParsedJsonDocument<Rank2DoubleMatrix>.Parse("[[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]");
        using JsonDocumentBuilder<Rank2DoubleMatrix.Mutable> doc =
            sourceDoc.RootElement.CreateBuilder(workspace);

        Span<double> buffer = stackalloc double[6];
        Assert.True(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.Equal(6, written);
        Assert.Equal(1.0, buffer[0]);
        Assert.Equal(6.0, buffer[5]);
    }

    [Fact]
    public void Rank3Int32Cube_Mutable_TryGetNumericValues_FlattensValues()
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc =
            ParsedJsonDocument<Rank3Int32Cube>.Parse("[[[1,2,3],[4,5,6]],[[7,8,9],[10,11,12]]]");
        using JsonDocumentBuilder<Rank3Int32Cube.Mutable> doc =
            sourceDoc.RootElement.CreateBuilder(workspace);

        Span<int> buffer = stackalloc int[12];
        Assert.True(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.Equal(12, written);
        for (int i = 0; i < 12; i++)
        {
            Assert.Equal(i + 1, buffer[i]);
        }
    }

    #endregion

    #region Build from span (tensor)

    [Fact]
    public void Rank1Int32Vector_BuildFromSpan()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<int> values = [1, 2, 3, 4];
        Rank1Int32Vector.Source source = Rank1Int32Vector.Build(values);

        using JsonDocumentBuilder<Rank1Int32Vector.Mutable> doc =
            Rank1Int32Vector.CreateBuilder(workspace, source);
        Rank1Int32Vector.Mutable root = doc.RootElement;

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, (int)root[0]);
        Assert.Equal(2, (int)root[1]);
        Assert.Equal(3, (int)root[2]);
        Assert.Equal(4, (int)root[3]);
    }

    [Fact]
    public void Rank1DoubleVector_BuildFromSpan()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<double> values = [1.5, 2.5, 3.5];
        Rank1DoubleVector.Source source = Rank1DoubleVector.Build(values);

        using JsonDocumentBuilder<Rank1DoubleVector.Mutable> doc =
            Rank1DoubleVector.CreateBuilder(workspace, source);
        Rank1DoubleVector.Mutable root = doc.RootElement;

        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1.5, (double)root[0]);
        Assert.Equal(2.5, (double)root[1]);
        Assert.Equal(3.5, (double)root[2]);
    }

    [Fact]
    public void Rank2DoubleMatrix_BuildFromSpan()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<double> values = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0];
        Rank2DoubleMatrix.Source source = Rank2DoubleMatrix.Build(values);

        using JsonDocumentBuilder<Rank2DoubleMatrix.Mutable> doc =
            Rank2DoubleMatrix.CreateBuilder(workspace, source);
        Rank2DoubleMatrix.Mutable root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());

        Rank2DoubleMatrix.JsonDoubleArray.Mutable row0 = root[0];
        Assert.Equal(3, row0.GetArrayLength());
        Assert.Equal(1.0, (double)row0[0]);
        Assert.Equal(2.0, (double)row0[1]);
        Assert.Equal(3.0, (double)row0[2]);

        Rank2DoubleMatrix.JsonDoubleArray.Mutable row1 = root[1];
        Assert.Equal(3, row1.GetArrayLength());
        Assert.Equal(4.0, (double)row1[0]);
        Assert.Equal(5.0, (double)row1[1]);
        Assert.Equal(6.0, (double)row1[2]);
    }

    [Fact]
    public void Rank3Int32Cube_BuildFromSpan()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<int> values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
        Rank3Int32Cube.Source source = Rank3Int32Cube.Build(values);

        using JsonDocumentBuilder<Rank3Int32Cube.Mutable> doc =
            Rank3Int32Cube.CreateBuilder(workspace, source);
        Rank3Int32Cube.Mutable root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());

        // First plane: [[1,2,3],[4,5,6]]
        Rank3Int32Cube.ItemsArray2Array.Mutable plane0 = root[0];
        Assert.Equal(2, plane0.GetArrayLength());
        Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.Mutable row00 = plane0[0];
        Assert.Equal(3, row00.GetArrayLength());
        Assert.Equal(1, (int)row00[0]);
        Assert.Equal(2, (int)row00[1]);
        Assert.Equal(3, (int)row00[2]);

        Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.Mutable row01 = plane0[1];
        Assert.Equal(4, (int)row01[0]);
        Assert.Equal(5, (int)row01[1]);
        Assert.Equal(6, (int)row01[2]);

        // Second plane: [[7,8,9],[10,11,12]]
        Rank3Int32Cube.ItemsArray2Array.Mutable plane1 = root[1];
        Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.Mutable row10 = plane1[0];
        Assert.Equal(7, (int)row10[0]);
        Assert.Equal(8, (int)row10[1]);
        Assert.Equal(9, (int)row10[2]);

        Rank3Int32Cube.ItemsArray2Array.JsonInt32Array.Mutable row11 = plane1[1];
        Assert.Equal(10, (int)row11[0]);
        Assert.Equal(11, (int)row11[1]);
        Assert.Equal(12, (int)row11[2]);
    }

    [Fact]
    public void Rank1Int32Vector_BuildFromSpan_RoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<int> values = [10, 20, 30, 40];
        Rank1Int32Vector.Source source = Rank1Int32Vector.Build(values);

        using JsonDocumentBuilder<Rank1Int32Vector.Mutable> doc =
            Rank1Int32Vector.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<Rank1Int32Vector>.Parse(json);
        Assert.Equal(4, reparsed.RootElement.GetArrayLength());
        Assert.Equal(10, (int)reparsed.RootElement[0]);
        Assert.Equal(20, (int)reparsed.RootElement[1]);
        Assert.Equal(30, (int)reparsed.RootElement[2]);
        Assert.Equal(40, (int)reparsed.RootElement[3]);
    }

    [Fact]
    public void Rank3Int32Cube_BuildFromSpan_RoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<int> values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
        Rank3Int32Cube.Source source = Rank3Int32Cube.Build(values);

        using JsonDocumentBuilder<Rank3Int32Cube.Mutable> doc =
            Rank3Int32Cube.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<Rank3Int32Cube>.Parse(json);
        Assert.Equal(2, reparsed.RootElement.GetArrayLength());
    }

    [Fact]
    public void Rank1Int32Vector_BuildFromSpan_WrongSize_ThrowsArgumentException()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<int> values = [1, 2, 3, 4, 5]; // Wrong size: 5 instead of 4
        Rank1Int32Vector.Source source = Rank1Int32Vector.Build(values);

        try
        {
            Rank1Int32Vector.CreateBuilder(workspace, source).Dispose();
            Assert.Fail("Expected ArgumentException was not thrown.");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    #endregion

    #region CreateBuilder from span (tensor convenience)

    [Fact]
    public void Rank1Int32Vector_CreateBuilderFromSpan()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<int> values = [1, 2, 3, 4];
        using JsonDocumentBuilder<Rank1Int32Vector.Mutable> doc =
            Rank1Int32Vector.CreateBuilder(workspace, values);
        Rank1Int32Vector.Mutable root = doc.RootElement;

        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, (int)root[0]);
        Assert.Equal(2, (int)root[1]);
        Assert.Equal(3, (int)root[2]);
        Assert.Equal(4, (int)root[3]);
    }

    [Fact]
    public void Rank3Int32Cube_CreateBuilderFromSpan_RoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<int> values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
        using JsonDocumentBuilder<Rank3Int32Cube.Mutable> doc =
            Rank3Int32Cube.CreateBuilder(workspace, values);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<Rank3Int32Cube>.Parse(json);
        Assert.Equal(2, reparsed.RootElement.GetArrayLength());

        // Verify first element of first row of first plane
        Span<int> roundTripped = stackalloc int[Rank3Int32Cube.ValueBufferSize];
        Assert.True(reparsed.RootElement.TryGetNumericValues(roundTripped, out int written));
        Assert.Equal(12, written);
        Assert.Equal(1, roundTripped[0]);
        Assert.Equal(12, roundTripped[11]);
    }

    #endregion

    #region Implicit conversion from ReadOnlySpan<T>

    [Fact]
    public void Rank1Int32Vector_ImplicitConversion_FromSpan()
    {
        ReadOnlySpan<int> values = [10, 20, 30, 40];
        Rank1Int32Vector.Source source = values;

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Rank1Int32Vector.Mutable> doc =
            Rank1Int32Vector.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();
        using var reparsed =
            ParsedJsonDocument<Rank1Int32Vector>.Parse(json);
        Span<int> roundTripped = stackalloc int[Rank1Int32Vector.ValueBufferSize];
        Assert.True(reparsed.RootElement.TryGetNumericValues(roundTripped, out int written));
        Assert.Equal(4, written);
        Assert.Equal(10, roundTripped[0]);
        Assert.Equal(40, roundTripped[3]);
    }

    [Fact]
    public void Rank1DoubleVector_ImplicitConversion_FromSpan()
    {
        ReadOnlySpan<double> values = [1.1, 2.2, 3.3];
        Rank1DoubleVector.Source source = values;

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Rank1DoubleVector.Mutable> doc =
            Rank1DoubleVector.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();
        using var reparsed =
            ParsedJsonDocument<Rank1DoubleVector>.Parse(json);
        Span<double> roundTripped = stackalloc double[Rank1DoubleVector.ValueBufferSize];
        Assert.True(reparsed.RootElement.TryGetNumericValues(roundTripped, out int written));
        Assert.Equal(3, written);
        Assert.Equal(1.1, roundTripped[0]);
        Assert.Equal(3.3, roundTripped[2]);
    }

    [Fact]
    public void Rank1Int32Vector_ImplicitConversion_InCreateBuilder()
    {
        // Verify the implicit conversion works inline in CreateBuilder
        using var workspace = JsonWorkspace.Create();
        ReadOnlySpan<int> values = [5, 6, 7, 8];
        using JsonDocumentBuilder<Rank1Int32Vector.Mutable> doc =
            Rank1Int32Vector.CreateBuilder(workspace, values);

        string json = doc.RootElement.ToString();
        Assert.Equal("[5,6,7,8]", json);
    }

    #endregion
}