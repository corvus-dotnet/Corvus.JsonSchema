// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for pattern properties inherited via allOf/$ref composition with
/// unevaluatedProperties: false on the root schema.
/// The pattern properties (^S_ → string, ^I_ → int32) are defined
/// on a $defs/patternBase type and referenced via allOf/$ref.
/// Because unevaluatedProperties uses LocalAndAppliedEvaluatedProperties,
/// the composed pattern properties ARE visible at the root — mutation methods
/// (SetProperty, RemoveProperty, TryGetProperty) are correctly emitted.
/// Pattern property values pass validation because they are seen as evaluated
/// by the composed subschema.
/// </summary>
public class GeneratedUnevaluatedPatternPropertiesTests
{
    // -------------------------------------------------------
    // Parsing and validation
    // -------------------------------------------------------

    [Fact]
    public void ValidInstance_WithRequiredNameOnly_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void ValidInstance_WithStringPatternProperty_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_color":"blue"}""");

        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void ValidInstance_WithIntegerPatternProperty_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","I_count":42}""");

        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void ValidInstance_WithMixedPatternProperties_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_color":"blue","S_tag":"important","I_count":42,"I_level":7}""");

        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void ValidInstance_AccessNameProperty_ReturnsExpectedValue()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Bob","S_label":"test"}""");

        Assert.True(instance.Name.ValueEquals("Bob"));
    }

    // -------------------------------------------------------
    // Invalid: missing required property
    // -------------------------------------------------------

    [Fact]
    public void InvalidInstance_MissingRequiredName_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"S_color":"blue","I_count":42}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Invalid: wrong value type for pattern
    // -------------------------------------------------------

    [Fact]
    public void InvalidInstance_StringPatternWithIntegerValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_color":123}""");

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidInstance_IntegerPatternWithStringValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","I_count":"not-a-number"}""");

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidInstance_IntegerPatternWithFloatValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","I_count":3.14}""");

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidInstance_StringPatternWithBooleanValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_flag":true}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Invalid: unevaluated properties (don't match any pattern)
    // -------------------------------------------------------

    [Fact]
    public void InvalidInstance_UnevaluatedPropertyNotMatchingPattern_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","unknown":"value"}""");

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidInstance_PropertyStartingWithLowercaseS_FailsValidation()
    {
        // "s_color" does NOT match "^S_" (case sensitive)
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","s_color":"blue"}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // TryGetProperty (immutable)
    // -------------------------------------------------------

    [Fact]
    public void TryGetProperty_ExistingPatternProperty_ReturnsTrue()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_color":"blue","I_count":42}""");

        Assert.True(instance.TryGetProperty("S_color", out JsonElement value));
        Assert.Equal("blue", value.ToString());
    }

    [Fact]
    public void TryGetProperty_NonExistentPatternProperty_ReturnsFalse()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        Assert.False(instance.TryGetProperty("S_missing", out _));
    }

    [Fact]
    public void TryGetProperty_IntegerPatternProperty_ReturnsValue()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","I_score":99}""");

        Assert.True(instance.TryGetProperty("I_score", out JsonElement value));
        Assert.Equal("99", value.ToString());
    }

    // -------------------------------------------------------
    // Builder + SetProperty on Mutable
    // -------------------------------------------------------

    [Fact]
    public void Builder_ThenSetProperty_StringPattern_PassesValidation()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            ObjectWithUnevaluatedPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("Alice"));

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", JsonElement.ParseValue("\"red\""));
        Assert.True(root.EvaluateSchema());
        Assert.True(root.TryGetProperty("S_color", out _));
    }

    [Fact]
    public void Builder_ThenSetProperty_IntegerPattern_PassesValidation()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            ObjectWithUnevaluatedPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("Alice"));

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("I_count", JsonElement.ParseValue("42"));
        Assert.True(root.EvaluateSchema());
        Assert.True(root.TryGetProperty("I_count", out _));
    }

    [Fact]
    public void Builder_ThenSetProperty_MixedPatterns_PassesValidation()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            ObjectWithUnevaluatedPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("Alice"));

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", JsonElement.ParseValue("\"blue\""));
        root.SetProperty("S_tag", JsonElement.ParseValue("\"important\""));
        root.SetProperty("I_count", JsonElement.ParseValue("42"));
        root.SetProperty("I_level", JsonElement.ParseValue("7"));
        Assert.True(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Mutable: SetProperty for pattern properties
    // -------------------------------------------------------

    [Fact]
    public void Mutable_SetProperty_StringPattern_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", JsonElement.ParseValue("\"blue\""));
        Assert.True(root.EvaluateSchema());
    }

    [Fact]
    public void Mutable_SetProperty_IntegerPattern_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("I_count", JsonElement.ParseValue("42"));
        Assert.True(root.EvaluateSchema());
    }

    [Fact]
    public void Mutable_SetProperty_WrongTypeForPattern_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", JsonElement.ParseValue("123"));
        Assert.False(root.EvaluateSchema());
    }

    [Fact]
    public void Mutable_SetProperty_UnmatchedPattern_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("unknown", JsonElement.ParseValue("\"value\""));
        Assert.False(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Mutable: RemoveProperty for pattern properties
    // -------------------------------------------------------

    [Fact]
    public void Mutable_RemoveProperty_PatternProperty_Succeeds()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_color":"blue","I_count":42}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("S_color");
        Assert.True(removed);
        Assert.True(root.EvaluateSchema());
    }

    [Fact]
    public void Mutable_RemoveProperty_NonExistent_ReturnsFalse()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("S_missing");
        Assert.False(removed);
    }

    // -------------------------------------------------------
    // Mutable: SetName (typed property)
    // -------------------------------------------------------

    [Fact]
    public void Mutable_SetName_UpdatesValue()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_label":"test"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetName("Bob");
        Assert.True(root.Name.ValueEquals("Bob"));
        Assert.True(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Build via Source + round-trip
    // -------------------------------------------------------

    [Fact]
    public void Build_ViaSourceBuild_RoundTrips()
    {
        ObjectWithUnevaluatedPatternProperties.Source source =
            ObjectWithUnevaluatedPatternProperties.Build(
                static (ref b) => b.Create("Carol"));

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            ObjectWithUnevaluatedPatternProperties.CreateBuilder(workspace, source);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        Assert.True(root.Name.ValueEquals("Carol"));
        Assert.True(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Non-object values
    // -------------------------------------------------------

    [Fact]
    public void NonObjectValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """
            "not an object"
            """);

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void ArrayValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """[1, 2, 3]""");

        Assert.False(instance.EvaluateSchema());
    }
}