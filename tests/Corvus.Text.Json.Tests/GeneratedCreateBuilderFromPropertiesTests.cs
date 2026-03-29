// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the generated CreateBuilder overload that takes per-property Source parameters.
/// </summary>
public class GeneratedCreateBuilderFromPropertiesTests
{
    [Fact]
    public void CreateBuilder_WithRequiredPropertyOnly_CreatesExpectedJson()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder =
            AllOfObjectWithProperties.CreateBuilder(workspace, "Alice");

        Assert.Equal("""{"name":"Alice"}""", builder.RootElement.ToString());
    }

    [Fact]
    public void CreateBuilder_WithRequiredAndOptionalProperties_CreatesExpectedJson()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder =
            AllOfObjectWithProperties.CreateBuilder(workspace, "Alice", 30, "alice@example.com");

        AllOfObjectWithProperties.Mutable root = builder.RootElement;
        Assert.True(root.Name.ValueEquals("Alice"));
        Assert.Equal("30", root.Age.ToString());
        Assert.True(root.Email.ValueEquals("alice@example.com"));
    }

    [Fact]
    public void CreateBuilder_WithRequiredAndPartialOptionalProperties_CreatesExpectedJson()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder =
            AllOfObjectWithProperties.CreateBuilder(workspace, "Bob", 25);

        AllOfObjectWithProperties.Mutable root = builder.RootElement;
        Assert.True(root.Name.ValueEquals("Bob"));
        Assert.Equal("25", root.Age.ToString());
        Assert.True(root.Email.IsUndefined());
    }

    [Fact]
    public void CreateBuilder_ResultCanBeMutated()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder =
            AllOfObjectWithProperties.CreateBuilder(workspace, "Alice", 30);

        AllOfObjectWithProperties.Mutable root = builder.RootElement;
        root.SetName("Bob");
        Assert.True(root.Name.ValueEquals("Bob"));
    }

    [Fact]
    public void CreateBuilder_ProducesValidRoundTrippableJson()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder =
            AllOfObjectWithProperties.CreateBuilder(workspace, "Alice", 30, "alice@example.com");

        string json = builder.RootElement.ToString();

        // Parse back and verify
        using var parsed =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse(json);

        Assert.True(parsed.RootElement.Name.ValueEquals("Alice"));
        Assert.Equal("30", parsed.RootElement.Age.ToString());
        Assert.True(parsed.RootElement.Email.ValueEquals("alice@example.com"));
    }

    [Fact]
    public void CreateBuilder_WithMixedProperties_CreatesExpectedJson()
    {
        // ObjectWithMixedProperties has: age (required int32), name (required string),
        // email (optional string), isActive (optional boolean)
        // Parameters are ordered alphabetically: required first, then optional
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder =
            ObjectWithMixedProperties.CreateBuilder(workspace, 40, "Charlie", "charlie@example.com", true);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        Assert.True(root.Name.ValueEquals("Charlie"));
        Assert.Equal("40", root.Age.ToString());
        Assert.True(root.Email.ValueEquals("charlie@example.com"));
        Assert.True((bool)root.IsActive);
    }

    [Fact]
    public void CreateBuilder_WithDefaultOptionalProperties_OmitsThem()
    {
        // Pass default for optional properties — they should be undefined
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder =
            ObjectWithMixedProperties.CreateBuilder(workspace, 30, "Dave");

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        Assert.True(root.Name.ValueEquals("Dave"));
        Assert.Equal("30", root.Age.ToString());
        Assert.True(root.Email.IsUndefined());
        Assert.True(root.IsActive.IsUndefined());
    }
}