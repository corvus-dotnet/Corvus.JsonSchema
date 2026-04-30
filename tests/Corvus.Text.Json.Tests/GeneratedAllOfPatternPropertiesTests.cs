// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for pattern properties inherited via allOf/$ref composition with
/// additionalProperties: false on the root schema.
/// The pattern properties (^T_ → string, ^N_ → number) are defined
/// on a $defs/textAndNumericPatterns type and referenced via allOf/$ref.
/// Because additionalProperties uses LocalEvaluatedProperties, the composed
/// pattern properties are NOT visible at the root — mutation methods
/// (SetProperty, RemoveProperty, TryGetProperty) are correctly NOT emitted.
/// Pattern property values fail validation because they are treated as
/// additional properties by the root schema.
/// </summary>
public class GeneratedAllOfPatternPropertiesTests
{
    // -------------------------------------------------------
    // Valid instances (required properties only)
    // -------------------------------------------------------

    [Fact]
    public void ValidInstance_WithRequiredIdOnly_PassesValidation()
    {
        var instance =
            ObjectWithAllOfPatternProperties.ParseValue("""{"id":"abc-123"}""");

        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void ValidInstance_AccessIdProperty_ReturnsExpectedValue()
    {
        var instance =
            ObjectWithAllOfPatternProperties.ParseValue("""{"id":"abc-123"}""");

        Assert.True(instance.Id.ValueEquals("abc-123"));
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
            ObjectWithAllOfPatternProperties.ParseValue("""{"id":"abc","T_label":"hello"}""");

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidInstance_NumberPatternPropertyTreatedAsAdditional_FailsValidation()
    {
        var instance =
            ObjectWithAllOfPatternProperties.ParseValue("""{"id":"abc","N_score":3.14}""");

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidInstance_MixedPatternPropertiesTreatedAsAdditional_FailsValidation()
    {
        var instance =
            ObjectWithAllOfPatternProperties.ParseValue(
                """{"id":"abc","T_label":"hello","N_score":3.14}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Invalid: missing required property
    // -------------------------------------------------------

    [Fact]
    public void InvalidInstance_MissingRequiredId_FailsValidation()
    {
        var instance =
            ObjectWithAllOfPatternProperties.ParseValue("""{"T_label":"hello"}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Invalid: additional properties that don't match any pattern
    // -------------------------------------------------------

    [Fact]
    public void InvalidInstance_UnknownAdditionalProperty_FailsValidation()
    {
        var instance =
            ObjectWithAllOfPatternProperties.ParseValue("""{"id":"abc","unknown":"value"}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Invalid: wrong type for pattern (also additional at root)
    // -------------------------------------------------------

    [Fact]
    public void InvalidInstance_StringPatternWithNumberValue_FailsValidation()
    {
        var instance =
            ObjectWithAllOfPatternProperties.ParseValue("""{"id":"abc","T_label":99}""");

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidInstance_NumberPatternWithStringValue_FailsValidation()
    {
        var instance =
            ObjectWithAllOfPatternProperties.ParseValue("""{"id":"abc","N_score":"not a number"}""");

        Assert.False(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Builder: create instances with required properties only
    // -------------------------------------------------------

    [Fact]
    public void Builder_WithRequiredIdOnly_ProducesValidInstance()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithAllOfPatternProperties.Mutable> builder =
            ObjectWithAllOfPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("abc-123"));

        ObjectWithAllOfPatternProperties.Mutable root = builder.RootElement;
        Assert.True(root.EvaluateSchema());
        Assert.True(root.Id.ValueEquals("abc-123"));
    }

    [Fact]
    public void Build_ViaSourceBuild_RoundTrips()
    {
        ObjectWithAllOfPatternProperties.Source source =
            ObjectWithAllOfPatternProperties.Build(
                static (ref b) => b.Create("xyz-789"));

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithAllOfPatternProperties.Mutable> builder =
            ObjectWithAllOfPatternProperties.CreateBuilder(workspace, source);

        ObjectWithAllOfPatternProperties.Mutable root = builder.RootElement;
        Assert.True(root.Id.ValueEquals("xyz-789"));
        Assert.True(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Mutable: SetId (typed property) still works
    // -------------------------------------------------------

    [Fact]
    public void Mutable_SetId_UpdatesValue()
    {
        var instance = ObjectWithAllOfPatternProperties.ParseValue("""{"id":"abc"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithAllOfPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithAllOfPatternProperties.Mutable root = builder.RootElement;
        root.SetId("xyz-999");
        Assert.True(root.Id.ValueEquals("xyz-999"));
        Assert.True(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Non-object values
    // -------------------------------------------------------

    [Fact]
    public void NonObjectValue_FailsValidation()
    {
        var instance = ObjectWithAllOfPatternProperties.ParseValue(
            """
            "not an object"
            """);

        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void ArrayValue_FailsValidation()
    {
        var instance = ObjectWithAllOfPatternProperties.ParseValue("""[1, 2, 3]""");

        Assert.False(instance.EvaluateSchema());
    }
}