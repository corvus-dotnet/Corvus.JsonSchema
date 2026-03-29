// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for pattern properties inherited via $ref composition with
/// additionalProperties: false on the root schema.
/// The pattern properties (^S_ → string, ^I_ → int32) are defined
/// on a $defs/patternBase type and referenced via allOf/$ref.
/// Because additionalProperties uses LocalEvaluatedProperties, the composed
/// pattern properties are NOT visible at the root — mutation methods
/// (SetProperty, RemoveProperty, TryGetProperty) are correctly NOT emitted.
/// Pattern property values fail validation because they are treated as
/// additional properties by the root schema.
/// </summary>
public class GeneratedRefPatternPropertiesTests
{
    // -------------------------------------------------------
    // Valid instances (required properties only)
    // -------------------------------------------------------

    [Fact]
    public void ValidInstance_WithRequiredNameOnly_PassesValidation()
    {
        var instance =
            ObjectWithRefPatternProperties.ParseValue("""{"name":"Alice"}""");

        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void ValidInstance_AccessNameProperty_ReturnsExpectedValue()
    {
        var instance =
            ObjectWithRefPatternProperties.ParseValue("""{"name":"Alice"}""");

        Assert.True(instance.Name.ValueEquals("Alice"));
    }

    // -------------------------------------------------------
    // Invalid: pattern property values are treated as additional
    // (additionalProperties: false + composed patternProperties
    //  means the root schema does not see them as evaluated)
    // -------------------------------------------------------

    [Fact]
    public void InvalidInstance_StringPatternPropertyTreatedAsAdditional_FailsValidation()
    {
        var instance =
            ObjectWithRefPatternProperties.ParseValue("""{"name":"Alice","S_color":"blue"}""");

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidInstance_IntegerPatternPropertyTreatedAsAdditional_FailsValidation()
    {
        var instance =
            ObjectWithRefPatternProperties.ParseValue("""{"name":"Alice","I_count":42}""");

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidInstance_MixedPatternPropertiesTreatedAsAdditional_FailsValidation()
    {
        var instance =
            ObjectWithRefPatternProperties.ParseValue(
                """{"name":"Alice","S_color":"blue","I_count":42}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Invalid: missing required property
    // -------------------------------------------------------

    [Fact]
    public void InvalidInstance_MissingRequiredName_FailsValidation()
    {
        var instance =
            ObjectWithRefPatternProperties.ParseValue("""{"S_color":"blue"}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Invalid: additional properties that don't match any pattern
    // -------------------------------------------------------

    [Fact]
    public void InvalidInstance_UnknownAdditionalProperty_FailsValidation()
    {
        var instance =
            ObjectWithRefPatternProperties.ParseValue("""{"name":"Alice","unknown":"value"}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Invalid: wrong type for pattern (also additional at root)
    // -------------------------------------------------------

    [Fact]
    public void InvalidInstance_StringPatternWithIntegerValue_FailsValidation()
    {
        var instance =
            ObjectWithRefPatternProperties.ParseValue("""{"name":"Alice","S_color":99}""");

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidInstance_IntegerPatternWithStringValue_FailsValidation()
    {
        var instance =
            ObjectWithRefPatternProperties.ParseValue("""{"name":"Alice","I_count":"not a number"}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Builder: create instances with required properties only
    // -------------------------------------------------------

    [Fact]
    public void Builder_WithRequiredNameOnly_ProducesValidInstance()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithRefPatternProperties.Mutable> builder =
            ObjectWithRefPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("Alice"));

        ObjectWithRefPatternProperties.Mutable root = builder.RootElement;
        Assert.True(root.EvaluateSchema());
        Assert.True(root.Name.ValueEquals("Alice"));
    }

    [Fact]
    public void Build_ViaSourceBuild_RoundTrips()
    {
        ObjectWithRefPatternProperties.Source source =
            ObjectWithRefPatternProperties.Build(
                static (ref b) => b.Create("Carol"));

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithRefPatternProperties.Mutable> builder =
            ObjectWithRefPatternProperties.CreateBuilder(workspace, source);

        ObjectWithRefPatternProperties.Mutable root = builder.RootElement;
        Assert.True(root.Name.ValueEquals("Carol"));
        Assert.True(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Mutable: SetName (typed property) still works
    // -------------------------------------------------------

    [Fact]
    public void Mutable_SetName_UpdatesValue()
    {
        var instance = ObjectWithRefPatternProperties.ParseValue("""{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithRefPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithRefPatternProperties.Mutable root = builder.RootElement;
        root.SetName("Bob");
        Assert.True(root.Name.ValueEquals("Bob"));
        Assert.True(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Non-object values
    // -------------------------------------------------------

    [Fact]
    public void NonObjectValue_FailsValidation()
    {
        var instance = ObjectWithRefPatternProperties.ParseValue(
            """
            "not an object"
            """);

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void ArrayValue_FailsValidation()
    {
        var instance = ObjectWithRefPatternProperties.ParseValue("""[1, 2, 3]""");

        Assert.False(instance.EvaluateSchema());
    }
}