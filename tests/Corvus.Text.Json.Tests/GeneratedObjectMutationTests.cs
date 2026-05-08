// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated mutable object types with mixed required/optional properties.
/// Exercises: named setters, IsUndefined guards (throw for required, remove for optional),
/// RemoveXxx for optional properties, generic SetProperty/RemoveProperty overloads,
/// and round-trip serialization.
/// </summary>
[TestClass]
public class GeneratedObjectMutationTests
{
    private const string SampleJson =
        """
        {"name":"Alice","age":30,"email":"alice@example.com","isActive":true}
        """;

    #region Named property setters

    [TestMethod]
    public void SetName_WithValidSource_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetName("Bob");
        Assert.AreEqual("Bob", root.Name.ToString());
    }

    [TestMethod]
    public void SetAge_WithValidSource_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetAge(42);
        Assert.AreEqual("42", root.Age.ToString());
    }

    [TestMethod]
    public void SetEmail_WithValidSource_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetEmail("bob@example.com");
        Assert.AreEqual("bob@example.com", root.Email.ToString());
    }

    [TestMethod]
    public void SetIsActive_WithValidSource_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetIsActive(false);
        Assert.IsFalse((bool)root.IsActive);
    }

    #endregion

    #region IsUndefined guards — required properties throw

    [TestMethod]
    public void SetName_WithUndefinedSource_ThrowsInvalidOperationException()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;

        bool threw = false;
        try
        {
            root.SetName(default);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }

    [TestMethod]
    public void SetAge_WithUndefinedSource_ThrowsInvalidOperationException()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;

        bool threw = false;
        try
        {
            root.SetAge(default);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }

    #endregion

    #region IsUndefined guards — optional properties remove

    [TestMethod]
    public void SetEmail_WithUndefinedSource_RemovesProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetEmail(default);
        Assert.IsTrue(root.Email.IsUndefined());
    }

    [TestMethod]
    public void SetIsActive_WithUndefinedSource_RemovesProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetIsActive(default);
        Assert.IsTrue(root.IsActive.IsUndefined());
    }

    #endregion

    #region RemoveXxx for optional properties

    [TestMethod]
    public void RemoveEmail_WhenPresent_ReturnsTrue()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveEmail();

        Assert.IsTrue(removed);
        Assert.IsTrue(root.Email.IsUndefined());
    }

    [TestMethod]
    public void RemoveEmail_WhenAbsent_ReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30}""");
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveEmail();

        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void RemoveIsActive_WhenPresent_ReturnsTrue()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveIsActive();

        Assert.IsTrue(removed);
        Assert.IsTrue(root.IsActive.IsUndefined());
    }

    #endregion

    #region Generic SetProperty/RemoveProperty

    [TestMethod]
    public void SetProperty_ByString_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetProperty("name", "Charlie");
        Assert.AreEqual("Charlie", root.Name.ToString());
    }

    [TestMethod]
    public void SetProperty_ByCharSpan_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetProperty("name".AsSpan(), "Diana");
        Assert.AreEqual("Diana", root.Name.ToString());
    }

    [TestMethod]
    public void SetProperty_ByUtf8Span_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetProperty("name"u8, "Eve");
        Assert.AreEqual("Eve", root.Name.ToString());
    }

    [TestMethod]
    public void RemoveProperty_ByString_RemovesProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("email");

        Assert.IsTrue(removed);
        Assert.IsTrue(root.Email.IsUndefined());
    }

    [TestMethod]
    public void RemoveProperty_ByString_WhenAbsent_ReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30}""");
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("email");

        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void SetProperty_WithUndefinedSource_RemovesProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetProperty("email", default);
        Assert.IsTrue(root.Email.IsUndefined());
    }

    #endregion

    #region Round-trip

    [TestMethod]
    public void RoundTrip_SetAllProperties_SerializesCorrectly()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"X","age":0}""");
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetName("Frank");
        root.SetAge(55);
        root.SetEmail("frank@test.com");
        root.SetIsActive(true);

        string json = root.ToString();

        using var roundTrip = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(json);
        Assert.AreEqual("Frank", roundTrip.RootElement.Name.ToString());
        Assert.AreEqual("55", roundTrip.RootElement.Age.ToString());
        Assert.AreEqual("frank@test.com", roundTrip.RootElement.Email.ToString());
        Assert.IsTrue((bool)roundTrip.RootElement.IsActive);
    }

    #endregion
}
