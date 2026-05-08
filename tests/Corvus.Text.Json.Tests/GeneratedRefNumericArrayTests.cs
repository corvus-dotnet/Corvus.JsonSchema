// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated fixed-size numeric array (tensor) types where items and/or
/// inner arrays are referenced via $ref or composed via allOf, rather than inlined.
/// Verifies that the generated APIs are consistent with fully-inline tensor schemas.
/// </summary>
[TestClass]
public class GeneratedRefNumericArrayTests
{
    #region RefItemsDoubleVector — $ref items type, rank 1

    [TestMethod]
    public void RefItemsDoubleVector_StaticProperties()
    {
        Assert.AreEqual(1, RefItemsDoubleVector.Rank);
        Assert.AreEqual(3, RefItemsDoubleVector.Dimension);
        Assert.AreEqual(3, RefItemsDoubleVector.ValueBufferSize);
    }

    [TestMethod]
    public void RefItemsDoubleVector_Build_CreateTensor()
    {
        using var workspace = JsonWorkspace.Create();

        RefItemsDoubleVector.Source source = RefItemsDoubleVector.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<double> values = [1.5, 2.5, 3.5];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<RefItemsDoubleVector.Mutable> doc =
            RefItemsDoubleVector.CreateBuilder(workspace, source);
        RefItemsDoubleVector.Mutable root = doc.RootElement;

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1.5, (double)root[0]);
        Assert.AreEqual(2.5, (double)root[1]);
        Assert.AreEqual(3.5, (double)root[2]);
    }

    [TestMethod]
    public void RefItemsDoubleVector_Build_CreateTensor_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        RefItemsDoubleVector.Source source = RefItemsDoubleVector.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<double> values = [1.5, 2.5, 3.5];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<RefItemsDoubleVector.Mutable> doc =
            RefItemsDoubleVector.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<RefItemsDoubleVector>.Parse(json);
        Assert.AreEqual(3, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual(1.5, (double)reparsed.RootElement[0]);
        Assert.AreEqual(2.5, (double)reparsed.RootElement[1]);
        Assert.AreEqual(3.5, (double)reparsed.RootElement[2]);
    }

    [TestMethod]
    public void RefItemsDoubleVector_CreateTensor_WrongSize_ThrowsArgumentException()
    {
        using var workspace = JsonWorkspace.Create();

        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            RefItemsDoubleVector.Source source = RefItemsDoubleVector.Build(
                static (ref builder) =>
                {
                    ReadOnlySpan<double> values = [1.0, 2.0];
                    builder.CreateTensor(values);
                });

            RefItemsDoubleVector.CreateBuilder(workspace, source).Dispose();
        });
    }

    #endregion

    #region RefItemsInt32Vector — $ref items type, rank 1

    [TestMethod]
    public void RefItemsInt32Vector_StaticProperties()
    {
        Assert.AreEqual(1, RefItemsInt32Vector.Rank);
        Assert.AreEqual(4, RefItemsInt32Vector.Dimension);
        Assert.AreEqual(4, RefItemsInt32Vector.ValueBufferSize);
    }

    [TestMethod]
    public void RefItemsInt32Vector_Build_CreateTensor()
    {
        using var workspace = JsonWorkspace.Create();

        RefItemsInt32Vector.Source source = RefItemsInt32Vector.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<int> values = [10, 20, 30, 40];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<RefItemsInt32Vector.Mutable> doc =
            RefItemsInt32Vector.CreateBuilder(workspace, source);
        RefItemsInt32Vector.Mutable root = doc.RootElement;

        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(10, (int)root[0]);
        Assert.AreEqual(20, (int)root[1]);
        Assert.AreEqual(30, (int)root[2]);
        Assert.AreEqual(40, (int)root[3]);
    }

    [TestMethod]
    public void RefItemsInt32Vector_Build_CreateTensor_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        RefItemsInt32Vector.Source source = RefItemsInt32Vector.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<int> values = [10, 20, 30, 40];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<RefItemsInt32Vector.Mutable> doc =
            RefItemsInt32Vector.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<RefItemsInt32Vector>.Parse(json);
        Assert.AreEqual(4, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual(10, (int)reparsed.RootElement[0]);
        Assert.AreEqual(20, (int)reparsed.RootElement[1]);
        Assert.AreEqual(30, (int)reparsed.RootElement[2]);
        Assert.AreEqual(40, (int)reparsed.RootElement[3]);
    }

    #endregion

    #region RefInnerArrayDoubleMatrix — $ref inner array, rank 2

    [TestMethod]
    public void RefInnerArrayDoubleMatrix_StaticProperties()
    {
        Assert.AreEqual(2, RefInnerArrayDoubleMatrix.Rank);
        Assert.AreEqual(2, RefInnerArrayDoubleMatrix.Dimension);
        Assert.AreEqual(6, RefInnerArrayDoubleMatrix.ValueBufferSize);
    }

    [TestMethod]
    public void RefInnerArrayDoubleMatrix_InnerType_StaticProperties()
    {
        Assert.AreEqual(1, RefInnerArrayDoubleMatrix.DoubleRow.Rank);
        Assert.AreEqual(3, RefInnerArrayDoubleMatrix.DoubleRow.Dimension);
        Assert.AreEqual(3, RefInnerArrayDoubleMatrix.DoubleRow.ValueBufferSize);
    }

    [TestMethod]
    public void RefInnerArrayDoubleMatrix_Build_CreateTensor()
    {
        using var workspace = JsonWorkspace.Create();

        // 2×3 matrix: [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]
        RefInnerArrayDoubleMatrix.Source source = RefInnerArrayDoubleMatrix.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<double> values = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<RefInnerArrayDoubleMatrix.Mutable> doc =
            RefInnerArrayDoubleMatrix.CreateBuilder(workspace, source);
        RefInnerArrayDoubleMatrix.Mutable root = doc.RootElement;

        Assert.AreEqual(2, root.GetArrayLength());

        RefInnerArrayDoubleMatrix.DoubleRow.Mutable row0 = root[0];
        Assert.AreEqual(3, row0.GetArrayLength());
        Assert.AreEqual(1.0, (double)row0[0]);
        Assert.AreEqual(2.0, (double)row0[1]);
        Assert.AreEqual(3.0, (double)row0[2]);

        RefInnerArrayDoubleMatrix.DoubleRow.Mutable row1 = root[1];
        Assert.AreEqual(3, row1.GetArrayLength());
        Assert.AreEqual(4.0, (double)row1[0]);
        Assert.AreEqual(5.0, (double)row1[1]);
        Assert.AreEqual(6.0, (double)row1[2]);
    }

    [TestMethod]
    public void RefInnerArrayDoubleMatrix_Build_CreateTensor_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        RefInnerArrayDoubleMatrix.Source source = RefInnerArrayDoubleMatrix.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<double> values = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<RefInnerArrayDoubleMatrix.Mutable> doc =
            RefInnerArrayDoubleMatrix.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<RefInnerArrayDoubleMatrix>.Parse(json);
        Assert.AreEqual(2, reparsed.RootElement.GetArrayLength());

        RefInnerArrayDoubleMatrix.DoubleRow row0 = reparsed.RootElement[0];
        Assert.AreEqual(3, row0.GetArrayLength());
        Assert.AreEqual(1.0, (double)row0[0]);
        Assert.AreEqual(2.0, (double)row0[1]);
        Assert.AreEqual(3.0, (double)row0[2]);

        RefInnerArrayDoubleMatrix.DoubleRow row1 = reparsed.RootElement[1];
        Assert.AreEqual(3, row1.GetArrayLength());
        Assert.AreEqual(4.0, (double)row1[0]);
        Assert.AreEqual(5.0, (double)row1[1]);
        Assert.AreEqual(6.0, (double)row1[2]);
    }

    [TestMethod]
    public void RefInnerArrayDoubleMatrix_CreateTensor_WrongSize_ThrowsArgumentException()
    {
        using var workspace = JsonWorkspace.Create();

        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            RefInnerArrayDoubleMatrix.Source source = RefInnerArrayDoubleMatrix.Build(
                static (ref builder) =>
                {
                    ReadOnlySpan<double> values = [1.0, 2.0, 3.0];
                    builder.CreateTensor(values);
                });

            RefInnerArrayDoubleMatrix.CreateBuilder(workspace, source).Dispose();
        });
    }

    #endregion

    #region AllOfDoubleVector — allOf-composed rank-1 vector

    [TestMethod]
    public void AllOfDoubleVector_StaticProperties()
    {
        Assert.AreEqual(1, AllOfDoubleVector.Rank);
        Assert.AreEqual(3, AllOfDoubleVector.Dimension);
        Assert.AreEqual(3, AllOfDoubleVector.ValueBufferSize);
    }

    [TestMethod]
    public void AllOfDoubleVector_BaseVector_StaticProperties()
    {
        Assert.AreEqual(1, AllOfDoubleVector.BaseVector.Rank);
        Assert.AreEqual(3, AllOfDoubleVector.BaseVector.Dimension);
        Assert.AreEqual(3, AllOfDoubleVector.BaseVector.ValueBufferSize);
    }

    [TestMethod]
    public void AllOfDoubleVector_Build_CreateTensor()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfDoubleVector.Source source = AllOfDoubleVector.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<double> values = [1.5, 2.5, 3.5];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<AllOfDoubleVector.Mutable> doc =
            AllOfDoubleVector.CreateBuilder(workspace, source);
        AllOfDoubleVector.Mutable root = doc.RootElement;

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1.5, (double)root[0]);
        Assert.AreEqual(2.5, (double)root[1]);
        Assert.AreEqual(3.5, (double)root[2]);
    }

    [TestMethod]
    public void AllOfDoubleVector_Build_CreateTensor_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfDoubleVector.Source source = AllOfDoubleVector.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<double> values = [1.5, 2.5, 3.5];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<AllOfDoubleVector.Mutable> doc =
            AllOfDoubleVector.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<AllOfDoubleVector>.Parse(json);
        Assert.AreEqual(3, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual(1.5, (double)reparsed.RootElement[0]);
        Assert.AreEqual(2.5, (double)reparsed.RootElement[1]);
        Assert.AreEqual(3.5, (double)reparsed.RootElement[2]);
    }

    [TestMethod]
    public void AllOfDoubleVector_Build_Add_Items()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfDoubleVector.Source source = AllOfDoubleVector.Build(
            static (ref builder) =>
            {
                builder.AddItem(1.5);
                builder.AddItem(2.5);
                builder.AddItem(3.5);
            });

        using JsonDocumentBuilder<AllOfDoubleVector.Mutable> doc =
            AllOfDoubleVector.CreateBuilder(workspace, source);
        AllOfDoubleVector.Mutable root = doc.RootElement;

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1.5, (double)root[0]);
        Assert.AreEqual(2.5, (double)root[1]);
        Assert.AreEqual(3.5, (double)root[2]);
    }

    [TestMethod]
    public void AllOfDoubleVector_Build_Add_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfDoubleVector.Source source = AllOfDoubleVector.Build(
            static (ref builder) =>
            {
                builder.AddItem(1.5);
                builder.AddItem(2.5);
                builder.AddItem(3.5);
            });

        using JsonDocumentBuilder<AllOfDoubleVector.Mutable> doc =
            AllOfDoubleVector.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<AllOfDoubleVector>.Parse(json);
        Assert.AreEqual(3, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual(1.5, (double)reparsed.RootElement[0]);
        Assert.AreEqual(2.5, (double)reparsed.RootElement[1]);
        Assert.AreEqual(3.5, (double)reparsed.RootElement[2]);
    }

    [TestMethod]
    public void AllOfDoubleVector_CreateTensor_WrongSize_ThrowsArgumentException()
    {
        using var workspace = JsonWorkspace.Create();

        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            AllOfDoubleVector.Source source = AllOfDoubleVector.Build(
                static (ref builder) =>
                {
                    ReadOnlySpan<double> values = [1.0, 2.0];
                    builder.CreateTensor(values);
                });

            AllOfDoubleVector.CreateBuilder(workspace, source).Dispose();
        });
    }

    [TestMethod]
    public void AllOfDoubleVector_TryGetAsBaseVector_AccessesComposedType()
    {
        using var doc =
            ParsedJsonDocument<AllOfDoubleVector>.Parse("[1.0, 2.0, 3.0]");

        Assert.IsTrue(doc.RootElement.TryGetAsBaseVector(out AllOfDoubleVector.BaseVector baseVector));
        Assert.AreEqual(3, baseVector.GetArrayLength());
        Assert.AreEqual(1.0, (double)baseVector[0]);
        Assert.AreEqual(2.0, (double)baseVector[1]);
        Assert.AreEqual(3.0, (double)baseVector[2]);
    }

    #endregion

    #region AllOfInt32Matrix — allOf-composed rank-2 matrix

    [TestMethod]
    public void AllOfInt32Matrix_StaticProperties()
    {
        Assert.AreEqual(2, AllOfInt32Matrix.Rank);
        Assert.AreEqual(2, AllOfInt32Matrix.Dimension);
        Assert.AreEqual(6, AllOfInt32Matrix.ValueBufferSize);
    }

    [TestMethod]
    public void AllOfInt32Matrix_BaseMatrix_StaticProperties()
    {
        Assert.AreEqual(2, AllOfInt32Matrix.BaseMatrix.Rank);
        Assert.AreEqual(2, AllOfInt32Matrix.BaseMatrix.Dimension);
        Assert.AreEqual(6, AllOfInt32Matrix.BaseMatrix.ValueBufferSize);
    }

    [TestMethod]
    public void AllOfInt32Matrix_Build_CreateTensor()
    {
        using var workspace = JsonWorkspace.Create();

        // 2×3 matrix: [[1, 2, 3], [4, 5, 6]]
        AllOfInt32Matrix.Source source = AllOfInt32Matrix.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<int> values = [1, 2, 3, 4, 5, 6];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<AllOfInt32Matrix.Mutable> doc =
            AllOfInt32Matrix.CreateBuilder(workspace, source);
        AllOfInt32Matrix.Mutable root = doc.RootElement;

        Assert.AreEqual(2, root.GetArrayLength());

        AllOfInt32Matrix.BaseMatrix.JsonInt32Array.Mutable row0 = root[0];
        Assert.AreEqual(3, row0.GetArrayLength());
        Assert.AreEqual(1, (int)row0[0]);
        Assert.AreEqual(2, (int)row0[1]);
        Assert.AreEqual(3, (int)row0[2]);

        AllOfInt32Matrix.BaseMatrix.JsonInt32Array.Mutable row1 = root[1];
        Assert.AreEqual(3, row1.GetArrayLength());
        Assert.AreEqual(4, (int)row1[0]);
        Assert.AreEqual(5, (int)row1[1]);
        Assert.AreEqual(6, (int)row1[2]);
    }

    [TestMethod]
    public void AllOfInt32Matrix_Build_CreateTensor_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfInt32Matrix.Source source = AllOfInt32Matrix.Build(
            static (ref builder) =>
            {
                ReadOnlySpan<int> values = [1, 2, 3, 4, 5, 6];
                builder.CreateTensor(values);
            });

        using JsonDocumentBuilder<AllOfInt32Matrix.Mutable> doc =
            AllOfInt32Matrix.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<AllOfInt32Matrix>.Parse(json);
        Assert.AreEqual(2, reparsed.RootElement.GetArrayLength());

        AllOfInt32Matrix.BaseMatrix.JsonInt32Array row0 = reparsed.RootElement[0];
        Assert.AreEqual(3, row0.GetArrayLength());
        Assert.AreEqual(1, (int)row0[0]);
        Assert.AreEqual(2, (int)row0[1]);
        Assert.AreEqual(3, (int)row0[2]);

        AllOfInt32Matrix.BaseMatrix.JsonInt32Array row1 = reparsed.RootElement[1];
        Assert.AreEqual(3, row1.GetArrayLength());
        Assert.AreEqual(4, (int)row1[0]);
        Assert.AreEqual(5, (int)row1[1]);
        Assert.AreEqual(6, (int)row1[2]);
    }

    [TestMethod]
    public void AllOfInt32Matrix_Build_Add_Rows()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfInt32Matrix.Source source = AllOfInt32Matrix.Build(
            static (ref builder) =>
            {
                builder.AddItem(AllOfInt32Matrix.BaseMatrix.JsonInt32Array.Build(
                    static (ref inner) =>
                    {
                        inner.AddItem(1);
                        inner.AddItem(2);
                        inner.AddItem(3);
                    }));
                builder.AddItem(AllOfInt32Matrix.BaseMatrix.JsonInt32Array.Build(
                    static (ref inner) =>
                    {
                        inner.AddItem(4);
                        inner.AddItem(5);
                        inner.AddItem(6);
                    }));
            });

        using JsonDocumentBuilder<AllOfInt32Matrix.Mutable> doc =
            AllOfInt32Matrix.CreateBuilder(workspace, source);
        AllOfInt32Matrix.Mutable root = doc.RootElement;

        Assert.AreEqual(2, root.GetArrayLength());

        AllOfInt32Matrix.BaseMatrix.JsonInt32Array.Mutable row0 = root[0];
        Assert.AreEqual(3, row0.GetArrayLength());
        Assert.AreEqual(1, (int)row0[0]);
        Assert.AreEqual(2, (int)row0[1]);
        Assert.AreEqual(3, (int)row0[2]);

        AllOfInt32Matrix.BaseMatrix.JsonInt32Array.Mutable row1 = root[1];
        Assert.AreEqual(3, row1.GetArrayLength());
        Assert.AreEqual(4, (int)row1[0]);
        Assert.AreEqual(5, (int)row1[1]);
        Assert.AreEqual(6, (int)row1[2]);
    }

    [TestMethod]
    public void AllOfInt32Matrix_Build_Add_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfInt32Matrix.Source source = AllOfInt32Matrix.Build(
            static (ref builder) =>
            {
                builder.AddItem(AllOfInt32Matrix.BaseMatrix.JsonInt32Array.Build(
                    static (ref inner) =>
                    {
                        inner.AddItem(1);
                        inner.AddItem(2);
                        inner.AddItem(3);
                    }));
                builder.AddItem(AllOfInt32Matrix.BaseMatrix.JsonInt32Array.Build(
                    static (ref inner) =>
                    {
                        inner.AddItem(4);
                        inner.AddItem(5);
                        inner.AddItem(6);
                    }));
            });

        using JsonDocumentBuilder<AllOfInt32Matrix.Mutable> doc =
            AllOfInt32Matrix.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<AllOfInt32Matrix>.Parse(json);
        Assert.AreEqual(2, reparsed.RootElement.GetArrayLength());

        AllOfInt32Matrix.BaseMatrix.JsonInt32Array row0 = reparsed.RootElement[0];
        Assert.AreEqual(3, row0.GetArrayLength());
        Assert.AreEqual(1, (int)row0[0]);
        Assert.AreEqual(2, (int)row0[1]);
        Assert.AreEqual(3, (int)row0[2]);

        AllOfInt32Matrix.BaseMatrix.JsonInt32Array row1 = reparsed.RootElement[1];
        Assert.AreEqual(3, row1.GetArrayLength());
        Assert.AreEqual(4, (int)row1[0]);
        Assert.AreEqual(5, (int)row1[1]);
        Assert.AreEqual(6, (int)row1[2]);
    }

    [TestMethod]
    public void AllOfInt32Matrix_CreateTensor_WrongSize_ThrowsArgumentException()
    {
        using var workspace = JsonWorkspace.Create();

        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            AllOfInt32Matrix.Source source = AllOfInt32Matrix.Build(
                static (ref builder) =>
                {
                    ReadOnlySpan<int> values = [1, 2, 3];
                    builder.CreateTensor(values);
                });

            AllOfInt32Matrix.CreateBuilder(workspace, source).Dispose();
        });
    }

    [TestMethod]
    public void AllOfInt32Matrix_TryGetAsBaseMatrix_AccessesComposedType()
    {
        using var doc =
            ParsedJsonDocument<AllOfInt32Matrix>.Parse("[[1,2,3],[4,5,6]]");

        Assert.IsTrue(doc.RootElement.TryGetAsBaseMatrix(out AllOfInt32Matrix.BaseMatrix baseMatrix));
        Assert.AreEqual(2, baseMatrix.GetArrayLength());
        Assert.AreEqual(1, (int)baseMatrix[0][0]);
        Assert.AreEqual(6, (int)baseMatrix[1][2]);
    }

    #endregion

    #region TryGetNumericValues — ref and allOf types

    [TestMethod]
    public void RefItemsDoubleVector_TryGetNumericValues_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<RefItemsDoubleVector>.Parse("[1.1, 2.2, 3.3]");

        Span<double> buffer = stackalloc double[3];
        Assert.IsTrue(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.AreEqual(3, written);
        Assert.AreEqual(1.1, buffer[0]);
        Assert.AreEqual(2.2, buffer[1]);
        Assert.AreEqual(3.3, buffer[2]);
    }

    [TestMethod]
    public void RefItemsInt32Vector_TryGetNumericValues_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<RefItemsInt32Vector>.Parse("[10, 20, 30, 40]");

        Span<int> buffer = stackalloc int[4];
        Assert.IsTrue(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.AreEqual(4, written);
        Assert.AreEqual(10, buffer[0]);
        Assert.AreEqual(20, buffer[1]);
        Assert.AreEqual(30, buffer[2]);
        Assert.AreEqual(40, buffer[3]);
    }

    [TestMethod]
    public void RefInnerArrayDoubleMatrix_TryGetNumericValues_FlattensValues()
    {
        using var doc =
            ParsedJsonDocument<RefInnerArrayDoubleMatrix>.Parse("[[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]");

        Span<double> buffer = stackalloc double[6];
        Assert.IsTrue(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.AreEqual(6, written);
        Assert.AreEqual(1.0, buffer[0]);
        Assert.AreEqual(6.0, buffer[5]);
    }

    [TestMethod]
    public void AllOfDoubleVector_TryGetNumericValues_Succeeds()
    {
        using var doc =
            ParsedJsonDocument<AllOfDoubleVector>.Parse("[1.1, 2.2, 3.3]");

        Span<double> buffer = stackalloc double[3];
        Assert.IsTrue(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.AreEqual(3, written);
        Assert.AreEqual(1.1, buffer[0]);
        Assert.AreEqual(2.2, buffer[1]);
        Assert.AreEqual(3.3, buffer[2]);
    }

    [TestMethod]
    public void AllOfInt32Matrix_TryGetNumericValues_FlattensValues()
    {
        using var doc =
            ParsedJsonDocument<AllOfInt32Matrix>.Parse("[[1, 2, 3], [4, 5, 6]]");

        Span<int> buffer = stackalloc int[6];
        Assert.IsTrue(doc.RootElement.TryGetNumericValues(buffer, out int written));
        Assert.AreEqual(6, written);
        Assert.AreEqual(1, buffer[0]);
        Assert.AreEqual(2, buffer[1]);
        Assert.AreEqual(3, buffer[2]);
        Assert.AreEqual(4, buffer[3]);
        Assert.AreEqual(5, buffer[4]);
        Assert.AreEqual(6, buffer[5]);
    }

    #endregion
}
