// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated mutable objects with array-typed properties.
/// Exercises: setting array properties, required/optional IsUndefined guards,
/// and RemoveXxx for optional array properties.
/// </summary>
[TestClass]
public class GeneratedObjectWithArrayPropertyTests
{
    private const string SampleJson =
        """
        {"tags":["a","b","c"],"scores":[1.5,2.5,3.5]}
        """;

    #region Set array properties

    [TestMethod]
    public void SetTags_WithValidSource_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithArrayProperty>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithArrayProperty.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithArrayProperty.Mutable root = builder.RootElement;

        using var tagsDoc =
            ParsedJsonDocument<ObjectWithArrayProperty.JsonStringArray>.Parse("""["x","y"]""");
        root.SetTags(tagsDoc.RootElement);
        Assert.AreEqual(2, root.Tags.GetArrayLength());
    }

    [TestMethod]
    public void BuilderAddProperty_WithContextSource_SetsArrayProperty()
    {
        using var workspace = JsonWorkspace.Create();

        ObjectWithArrayProperty.JsonStringArray.Source<int> tags =
            ObjectWithArrayProperty.JsonStringArray.Build(
                3,
                static (in int itemCount, ref ObjectWithArrayProperty.JsonStringArray.Builder builder) =>
                {
                    for (int i = 0; i < itemCount; ++i)
                    {
                        builder.AddItem($"tag-{i}");
                    }
                });

        using JsonDocumentBuilder<ObjectWithArrayProperty.Mutable> doc =
            ObjectWithArrayProperty.CreateBuilder(workspace, 3, tags);

        ObjectWithArrayProperty.Mutable root = doc.RootElement;

        Assert.AreEqual(3, root.Tags.GetArrayLength());
        Assert.AreEqual("tag-0", (string)root.Tags[0]);
        Assert.AreEqual("tag-2", (string)root.Tags[2]);
    }

    #endregion

    #region IsUndefined guards

    [TestMethod]
    public void SetTags_WithUndefinedSource_ThrowsForRequired()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithArrayProperty>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithArrayProperty.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithArrayProperty.Mutable root = builder.RootElement;

        bool threw = false;
        try
        {
            root.SetTags(default);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }

    [TestMethod]
    public void SetScores_WithUndefinedSource_RemovesOptional()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithArrayProperty>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithArrayProperty.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithArrayProperty.Mutable root = builder.RootElement;
        root.SetScores(default);
        Assert.IsTrue(root.Scores.IsUndefined());
    }

    #endregion

    #region Remove optional array property

    [TestMethod]
    public void RemoveScores_WhenPresent_ReturnsTrue()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithArrayProperty>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithArrayProperty.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithArrayProperty.Mutable root = builder.RootElement;
        bool removed = root.RemoveScores();

        Assert.IsTrue(removed);
        Assert.IsTrue(root.Scores.IsUndefined());
    }

    #endregion

    #region Build and CreateBuilder from span (variable-length numeric array)

    [TestMethod]
    public void ScoresArray_BuildFromSpan()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<double> values = [1.1, 2.2, 3.3];
        ObjectWithArrayProperty.JsonDoubleArray.Source source =
            ObjectWithArrayProperty.JsonDoubleArray.Build(values);

        using JsonDocumentBuilder<ObjectWithArrayProperty.JsonDoubleArray.Mutable> doc =
            ObjectWithArrayProperty.JsonDoubleArray.CreateBuilder(workspace, source);
        ObjectWithArrayProperty.JsonDoubleArray.Mutable root = doc.RootElement;

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1.1, (double)root[0]);
        Assert.AreEqual(2.2, (double)root[1]);
        Assert.AreEqual(3.3, (double)root[2]);
    }

    [TestMethod]
    public void ScoresArray_CreateBuilderFromSpan()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<double> values = [10.0, 20.0, 30.0, 40.0, 50.0];
        using JsonDocumentBuilder<ObjectWithArrayProperty.JsonDoubleArray.Mutable> doc =
            ObjectWithArrayProperty.JsonDoubleArray.CreateBuilder(workspace, values);
        ObjectWithArrayProperty.JsonDoubleArray.Mutable root = doc.RootElement;

        Assert.AreEqual(5, root.GetArrayLength());
        Assert.AreEqual(10.0, (double)root[0]);
        Assert.AreEqual(50.0, (double)root[4]);
    }

    [TestMethod]
    public void ScoresArray_BuildFromSpan_RoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<double> values = [1.5, 2.5, 3.5];
        using JsonDocumentBuilder<ObjectWithArrayProperty.JsonDoubleArray.Mutable> doc =
            ObjectWithArrayProperty.JsonDoubleArray.CreateBuilder(workspace, values);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<ObjectWithArrayProperty.JsonDoubleArray>.Parse(json);
        Assert.AreEqual(3, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual(1.5, (double)reparsed.RootElement[0]);
        Assert.AreEqual(2.5, (double)reparsed.RootElement[1]);
        Assert.AreEqual(3.5, (double)reparsed.RootElement[2]);
    }

    [TestMethod]
    public void ScoresArray_BuildFromEmptySpan()
    {
        using var workspace = JsonWorkspace.Create();

        ReadOnlySpan<double> values = [];
        using JsonDocumentBuilder<ObjectWithArrayProperty.JsonDoubleArray.Mutable> doc =
            ObjectWithArrayProperty.JsonDoubleArray.CreateBuilder(workspace, values);

        string json = doc.RootElement.ToString();
        Assert.AreEqual("[]", json);
    }

    [TestMethod]
    public void ScoresArray_ImplicitConversion_FromSpan()
    {
        ReadOnlySpan<double> values = [9.0, 8.5, 7.0];
        ObjectWithArrayProperty.JsonDoubleArray.Source source = values;

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithArrayProperty.JsonDoubleArray.Mutable> doc =
            ObjectWithArrayProperty.JsonDoubleArray.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();
        Assert.AreEqual("[9,8.5,7]", json);
    }

    #endregion
}
