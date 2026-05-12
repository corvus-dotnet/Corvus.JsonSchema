// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated mutable nested object types.
/// Exercises: nested object property setting, deep mutation,
/// required/optional guards on both outer and inner objects.
/// </summary>
[TestClass]
public class GeneratedNestedObjectMutationTests
{
    private const string SampleJson =
        """
        {"address":{"street":"123 Main St","city":"Springfield","zip":"62704"},"notes":"Test notes"}
        """;

    #region Set nested object property

    [TestMethod]
    public void SetNotes_WithValidSource_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<NestedObject>.Parse(SampleJson);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        NestedObject.Mutable root = builder.RootElement;
        root.SetNotes("Updated notes");
        Assert.AreEqual("Updated notes", root.Notes.ToString());
    }

    #endregion

    #region IsUndefined guards on outer object

    [TestMethod]
    public void SetAddress_WithUndefinedSource_ThrowsForRequired()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<NestedObject>.Parse(SampleJson);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        NestedObject.Mutable root = builder.RootElement;

        bool threw = false;
        try
        {
            root.SetAddress(default);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }

    [TestMethod]
    public void SetNotes_WithUndefinedSource_RemovesOptional()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<NestedObject>.Parse(SampleJson);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        NestedObject.Mutable root = builder.RootElement;
        root.SetNotes(default);
        Assert.IsTrue(root.Notes.IsUndefined());
    }

    #endregion

    #region Remove optional properties

    [TestMethod]
    public void RemoveNotes_WhenPresent_ReturnsTrue()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<NestedObject>.Parse(SampleJson);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        NestedObject.Mutable root = builder.RootElement;
        bool removed = root.RemoveNotes();

        Assert.IsTrue(removed);
        Assert.IsTrue(root.Notes.IsUndefined());
    }

    #endregion

    #region Deep mutationon nested object

    [TestMethod]
    public void MutateNestedObject_SetStreetOnAddress_UpdatesProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<NestedObject>.Parse(SampleJson);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        NestedObject.Mutable root = builder.RootElement;
        NestedObject.RequiredStreet.Mutable address = root.Address;
        address.SetStreet("456 Oak Ave");
        Assert.AreEqual("456 Oak Ave", address.Street.ToString());
    }

    [TestMethod]
    public void MutateNestedObject_RemoveOptionalCity_RemovesProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<NestedObject>.Parse(SampleJson);
        using JsonDocumentBuilder<NestedObject.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        NestedObject.Mutable root = builder.RootElement;
        NestedObject.RequiredStreet.Mutable address = root.Address;
        bool removed = address.RemoveCity();

        Assert.IsTrue(removed);
        Assert.IsTrue(address.City.IsUndefined());
    }

    #endregion
}
