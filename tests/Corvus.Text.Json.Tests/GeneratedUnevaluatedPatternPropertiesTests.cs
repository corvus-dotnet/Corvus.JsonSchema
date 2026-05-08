// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
[TestClass]
public class GeneratedUnevaluatedPatternPropertiesTests
{
    // -------------------------------------------------------
    // Parsing and validation
    // -------------------------------------------------------

    [TestMethod]
    public void ValidInstance_WithRequiredNameOnly_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ValidInstance_WithStringPatternProperty_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_color":"blue"}""");

        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ValidInstance_WithIntegerPatternProperty_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","I_count":42}""");

        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ValidInstance_WithMixedPatternProperties_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_color":"blue","S_tag":"important","I_count":42,"I_level":7}""");

        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ValidInstance_AccessNameProperty_ReturnsExpectedValue()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Bob","S_label":"test"}""");

        Assert.IsTrue(instance.Name.ValueEquals("Bob"));
    }

    // -------------------------------------------------------
    // Invalid: missing required property
    // -------------------------------------------------------

    [TestMethod]
    public void InvalidInstance_MissingRequiredName_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"S_color":"blue","I_count":42}""");

        Assert.IsFalse(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Invalid: wrong value type for pattern
    // -------------------------------------------------------

    [TestMethod]
    public void InvalidInstance_StringPatternWithIntegerValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_color":123}""");

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void InvalidInstance_IntegerPatternWithStringValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","I_count":"not-a-number"}""");

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void InvalidInstance_IntegerPatternWithFloatValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","I_count":3.14}""");

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void InvalidInstance_StringPatternWithBooleanValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_flag":true}""");

        Assert.IsFalse(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Invalid: unevaluated properties (don't match any pattern)
    // -------------------------------------------------------

    [TestMethod]
    public void InvalidInstance_UnevaluatedPropertyNotMatchingPattern_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","unknown":"value"}""");

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void InvalidInstance_PropertyStartingWithLowercaseS_FailsValidation()
    {
        // "s_color" does NOT match "^S_" (case sensitive)
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","s_color":"blue"}""");

        Assert.IsFalse(instance.EvaluateSchema());
    }

    // -------------------------------------------------------
    // TryGetProperty (immutable)
    // -------------------------------------------------------

    [TestMethod]
    public void TryGetProperty_ExistingPatternProperty_ReturnsTrue()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_color":"blue","I_count":42}""");

        Assert.IsTrue(instance.TryGetProperty("S_color", out JsonElement value));
        Assert.AreEqual("blue", value.ToString());
    }

    [TestMethod]
    public void TryGetProperty_NonExistentPatternProperty_ReturnsFalse()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        Assert.IsFalse(instance.TryGetProperty("S_missing", out _));
    }

    [TestMethod]
    public void TryGetProperty_IntegerPatternProperty_ReturnsValue()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","I_score":99}""");

        Assert.IsTrue(instance.TryGetProperty("I_score", out JsonElement value));
        Assert.AreEqual("99", value.ToString());
    }

    // -------------------------------------------------------
    // Builder + SetProperty on Mutable
    // -------------------------------------------------------

    [TestMethod]
    public void Builder_ThenSetProperty_StringPattern_PassesValidation()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            ObjectWithUnevaluatedPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("Alice"));

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", JsonElement.ParseValue("\"red\""));
        Assert.IsTrue(root.EvaluateSchema());
        Assert.IsTrue(root.TryGetProperty("S_color", out _));
    }

    [TestMethod]
    public void Builder_ThenSetProperty_IntegerPattern_PassesValidation()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            ObjectWithUnevaluatedPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("Alice"));

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("I_count", JsonElement.ParseValue("42"));
        Assert.IsTrue(root.EvaluateSchema());
        Assert.IsTrue(root.TryGetProperty("I_count", out _));
    }

    [TestMethod]
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
        Assert.IsTrue(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Mutable: SetProperty for pattern properties
    // -------------------------------------------------------

    [TestMethod]
    public void Mutable_SetProperty_StringPattern_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", JsonElement.ParseValue("\"blue\""));
        Assert.IsTrue(root.EvaluateSchema());
    }

    [TestMethod]
    public void Mutable_SetProperty_IntegerPattern_PassesValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("I_count", JsonElement.ParseValue("42"));
        Assert.IsTrue(root.EvaluateSchema());
    }

    [TestMethod]
    public void Mutable_SetProperty_WrongTypeForPattern_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", JsonElement.ParseValue("123"));
        Assert.IsFalse(root.EvaluateSchema());
    }

    [TestMethod]
    public void Mutable_SetProperty_UnmatchedPattern_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("unknown", JsonElement.ParseValue("\"value\""));
        Assert.IsFalse(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Mutable: RemoveProperty for pattern properties
    // -------------------------------------------------------

    [TestMethod]
    public void Mutable_RemoveProperty_PatternProperty_Succeeds()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_color":"blue","I_count":42}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("S_color");
        Assert.IsTrue(removed);
        Assert.IsTrue(root.EvaluateSchema());
    }

    [TestMethod]
    public void Mutable_RemoveProperty_NonExistent_ReturnsFalse()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("S_missing");
        Assert.IsFalse(removed);
    }

    // -------------------------------------------------------
    // Mutable: SetName (typed property)
    // -------------------------------------------------------

    [TestMethod]
    public void Mutable_SetName_UpdatesValue()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """{"name":"Alice","S_label":"test"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        root.SetName("Bob");
        Assert.IsTrue(root.Name.ValueEquals("Bob"));
        Assert.IsTrue(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Build via Source + round-trip
    // -------------------------------------------------------

    [TestMethod]
    public void Build_ViaSourceBuild_RoundTrips()
    {
        ObjectWithUnevaluatedPatternProperties.Source source =
            ObjectWithUnevaluatedPatternProperties.Build(
                static (ref b) => b.Create("Carol"));

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithUnevaluatedPatternProperties.Mutable> builder =
            ObjectWithUnevaluatedPatternProperties.CreateBuilder(workspace, source);

        ObjectWithUnevaluatedPatternProperties.Mutable root = builder.RootElement;
        Assert.IsTrue(root.Name.ValueEquals("Carol"));
        Assert.IsTrue(root.EvaluateSchema());
    }

    // -------------------------------------------------------
    // Non-object values
    // -------------------------------------------------------

    [TestMethod]
    public void NonObjectValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """
            "not an object"
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ArrayValue_FailsValidation()
    {
        var instance = ObjectWithUnevaluatedPatternProperties.ParseValue(
            """[1, 2, 3]""");

        Assert.IsFalse(instance.EvaluateSchema());
    }
}
