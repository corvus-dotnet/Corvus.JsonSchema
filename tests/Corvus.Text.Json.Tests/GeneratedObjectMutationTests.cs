// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated mutable object types with mixed required/optional properties.
/// Exercises: named setters, IsUndefined guards (throw for required, remove for optional),
/// RemoveXxx for optional properties, generic SetProperty/RemoveProperty overloads,
/// and round-trip serialization.
/// </summary>
public class GeneratedObjectMutationTests
{
    private const string SampleJson =
        """
        {"name":"Alice","age":30,"email":"alice@example.com","isActive":true}
        """;

    #region Named property setters

    [Fact]
    public void SetName_WithValidSource_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetName("Bob");
        Assert.Equal("Bob", root.Name.ToString());
    }

    [Fact]
    public void SetAge_WithValidSource_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetAge(42);
        Assert.Equal("42", root.Age.ToString());
    }

    [Fact]
    public void SetEmail_WithValidSource_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetEmail("bob@example.com");
        Assert.Equal("bob@example.com", root.Email.ToString());
    }

    [Fact]
    public void SetIsActive_WithValidSource_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetIsActive(false);
        Assert.False((bool)root.IsActive);
    }

    #endregion

    #region IsUndefined guards — required properties throw

    [Fact]
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

        Assert.True(threw);
    }

    [Fact]
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

        Assert.True(threw);
    }

    #endregion

    #region IsUndefined guards — optional properties remove

    [Fact]
    public void SetEmail_WithUndefinedSource_RemovesProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetEmail(default);
        Assert.True(root.Email.IsUndefined());
    }

    [Fact]
    public void SetIsActive_WithUndefinedSource_RemovesProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetIsActive(default);
        Assert.True(root.IsActive.IsUndefined());
    }

    #endregion

    #region RemoveXxx for optional properties

    [Fact]
    public void RemoveEmail_WhenPresent_ReturnsTrue()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveEmail();

        Assert.True(removed);
        Assert.True(root.Email.IsUndefined());
    }

    [Fact]
    public void RemoveEmail_WhenAbsent_ReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30}""");
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveEmail();

        Assert.False(removed);
    }

    [Fact]
    public void RemoveIsActive_WhenPresent_ReturnsTrue()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveIsActive();

        Assert.True(removed);
        Assert.True(root.IsActive.IsUndefined());
    }

    #endregion

    #region Generic SetProperty/RemoveProperty

    [Fact]
    public void SetProperty_ByString_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetProperty("name", "Charlie");
        Assert.Equal("Charlie", root.Name.ToString());
    }

    [Fact]
    public void SetProperty_ByCharSpan_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetProperty("name".AsSpan(), "Diana");
        Assert.Equal("Diana", root.Name.ToString());
    }

    [Fact]
    public void SetProperty_ByUtf8Span_SetsProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetProperty("name"u8, "Eve");
        Assert.Equal("Eve", root.Name.ToString());
    }

    [Fact]
    public void RemoveProperty_ByString_RemovesProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("email");

        Assert.True(removed);
        Assert.True(root.Email.IsUndefined());
    }

    [Fact]
    public void RemoveProperty_ByString_WhenAbsent_ReturnsFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30}""");
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("email");

        Assert.False(removed);
    }

    [Fact]
    public void SetProperty_WithUndefinedSource_RemovesProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(SampleJson);
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetProperty("email", default);
        Assert.True(root.Email.IsUndefined());
    }

    #endregion

    #region Round-trip

    [Fact]
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
        Assert.Equal("Frank", roundTrip.RootElement.Name.ToString());
        Assert.Equal("55", roundTrip.RootElement.Age.ToString());
        Assert.Equal("frank@test.com", roundTrip.RootElement.Email.ToString());
        Assert.True((bool)roundTrip.RootElement.IsActive);
    }

    #endregion
}