// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for a source-generated type with patternProperties.
/// The schema defines:
///   - a required "name" property (string)
///   - patternProperties: "^S_" → string, "^I_" → integer (int32)
///   - additionalProperties: false
/// Exercises: schema validation for valid/invalid instances, pattern matching
/// for property names, and correct rejection of non-matching additional properties.
/// </summary>
[TestClass]
public class GeneratedPatternPropertiesTests
{
    #region Valid instances

    [TestMethod]
    public void ValidInstance_WithRequiredNameOnly_PassesValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice"}
            """);

        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ValidInstance_WithStringPatternProperties_PassesValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","S_color":"blue","S_label":"important"}
            """);

        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ValidInstance_WithIntegerPatternProperties_PassesValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","I_count":42,"I_score":100}
            """);

        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ValidInstance_WithMixedPatternProperties_PassesValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","S_color":"blue","I_count":42,"S_tag":"hello","I_level":7}
            """);

        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ValidInstance_AccessNameProperty_ReturnsExpectedValue()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Bob","S_label":"test"}
            """);

        Assert.IsTrue(instance.Name.ValueEquals("Bob"));
    }

    #endregion

    #region Invalid: missing required property

    [TestMethod]
    public void InvalidInstance_MissingRequiredName_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"S_color":"blue","I_count":42}
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    #endregion

    #region Invalid: wrong value type for pattern

    [TestMethod]
    public void InvalidInstance_StringPatternWithIntegerValue_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","S_color":123}
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void InvalidInstance_IntegerPatternWithStringValue_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","I_count":"not-a-number"}
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void InvalidInstance_IntegerPatternWithFloatValue_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","I_count":3.14}
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void InvalidInstance_StringPatternWithBooleanValue_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","S_flag":true}
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void InvalidInstance_IntegerPatternWithNullValue_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","I_count":null}
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    #endregion

    #region Invalid: additional properties not matching any pattern

    [TestMethod]
    public void InvalidInstance_AdditionalPropertyNotMatchingPattern_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","unknown":"value"}
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void InvalidInstance_PropertyStartingWithLowercaseS_FailsValidation()
    {
        // "s_color" does NOT match "^S_" (case sensitive)
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","s_color":"blue"}
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    #endregion

    #region Building via Source/Builder

    [TestMethod]
    public void Build_WithRequiredNameOnly_ProducesValidInstance()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            ObjectWithPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("Alice"));

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        Assert.IsTrue(root.EvaluateSchema());
    }

    [TestMethod]
    public void Build_ViaSourceBuild_RoundTrips()
    {
        ObjectWithPatternProperties.Source source =
            ObjectWithPatternProperties.Build(
                static (ref b) => b.Create("Carol"));

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            ObjectWithPatternProperties.CreateBuilder(workspace, source);

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        Assert.IsTrue(root.Name.ValueEquals("Carol"));
        Assert.IsTrue(root.EvaluateSchema());
    }

    #endregion

    #region Mutable: set name property

    [TestMethod]
    public void Mutable_SetName_UpdatesValue()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","S_label":"test"}
            """);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        root.SetName("Bob");
        Assert.IsTrue(root.Name.ValueEquals("Bob"));
        Assert.IsTrue(root.EvaluateSchema());
    }

    #endregion

    #region Mutable: SetProperty for pattern properties

    [TestMethod]
    public void Mutable_SetProperty_StringPattern_PassesValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice"}
            """);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", "blue");
        Assert.IsTrue(root.EvaluateSchema());
    }

    [TestMethod]
    public void Mutable_SetProperty_IntegerPattern_PassesValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice"}
            """);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("I_count", 42);
        Assert.IsTrue(root.EvaluateSchema());
    }

    [TestMethod]
    public void Mutable_SetProperty_WrongTypeForPattern_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice"}
            """);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", JsonElement.ParseValue("123"));
        Assert.IsFalse(root.EvaluateSchema());
    }

    [TestMethod]
    public void Mutable_SetProperty_UnmatchedPattern_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice"}
            """);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("unknown", JsonElement.ParseValue("\"value\""));
        Assert.IsFalse(root.EvaluateSchema());
    }

    #endregion

    #region Mutable: RemoveProperty for pattern properties

    [TestMethod]
    public void Mutable_RemoveProperty_PatternProperty_Succeeds()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","S_color":"blue","I_count":42}
            """);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("S_color");
        Assert.IsTrue(removed);
        Assert.IsTrue(root.EvaluateSchema());
    }

    [TestMethod]
    public void Mutable_RemoveProperty_NonExistent_ReturnsFalse()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice"}
            """);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            instance.CreateBuilder(workspace);

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        bool removed = root.RemoveProperty("S_missing");
        Assert.IsFalse(removed);
    }

    #endregion

    #region TryGetProperty for pattern properties

    [TestMethod]
    public void TryGetProperty_ExistingPatternProperty_ReturnsTrue()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","S_color":"blue","I_count":42}
            """);

        Assert.IsTrue(instance.TryGetProperty("S_color", out JsonElement value));
        Assert.AreEqual("blue", value.ToString());
    }

    [TestMethod]
    public void TryGetProperty_NonExistentPatternProperty_ReturnsFalse()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice"}
            """);

        Assert.IsFalse(instance.TryGetProperty("S_missing", out _));
    }

    [TestMethod]
    public void TryGetProperty_IntegerPatternProperty_ReturnsValue()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            {"name":"Alice","I_score":99}
            """);

        Assert.IsTrue(instance.TryGetProperty("I_score", out JsonElement value));
        Assert.AreEqual("99", value.ToString());
    }

    #endregion

    #region Builder: AddProperty for pattern properties

    [TestMethod]
    public void Builder_ThenSetProperty_StringPattern_PassesValidation()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            ObjectWithPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("Alice"));

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", JsonElement.ParseValue("\"red\""));
        Assert.IsTrue(root.EvaluateSchema());
        Assert.IsTrue(root.TryGetProperty("S_color", out _));
    }

    [TestMethod]
    public void Builder_ThenSetProperty_IntegerPattern_PassesValidation()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            ObjectWithPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("Alice"));

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("I_count", JsonElement.ParseValue("42"));
        Assert.IsTrue(root.EvaluateSchema());
        Assert.IsTrue(root.TryGetProperty("I_count", out _));
    }

    [TestMethod]
    public void Builder_ThenSetProperty_MixedPatterns_PassesValidation()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ObjectWithPatternProperties.Mutable> builder =
            ObjectWithPatternProperties.CreateBuilder(
                workspace,
                static (ref b) => b.Create("Alice"));

        ObjectWithPatternProperties.Mutable root = builder.RootElement;
        root.SetProperty("S_color", JsonElement.ParseValue("\"blue\""));
        root.SetProperty("S_tag", JsonElement.ParseValue("\"important\""));
        root.SetProperty("I_count", JsonElement.ParseValue("42"));
        root.SetProperty("I_level", JsonElement.ParseValue("7"));
        Assert.IsTrue(root.EvaluateSchema());
    }

    #endregion

    #region Non-object values

    [TestMethod]
    public void NonObjectValue_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            "not an object"
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ArrayValue_FailsValidation()
    {
        var instance = ObjectWithPatternProperties.ParseValue(
            """
            [1, 2, 3]
            """);

        Assert.IsFalse(instance.EvaluateSchema());
    }

    #endregion
}
