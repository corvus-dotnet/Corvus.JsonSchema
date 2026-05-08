// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 default property value handling produces equivalent results.
/// </summary>
/// <remarks>
/// <para>V4: <c>MyType.DefaultInstance</c> — available but may be empty</para>
/// <para>V5: <c>MyType.DefaultInstance</c> — contains schema-defined default values</para>
/// </remarks>
[TestClass]
public class DefaultValueEquivalenceTests
{
    private const string FullJson = """{"name":"Jo","status":"pending","count":5}""";
    private const string MinimalJson = """{"name":"Jo"}""";

    [TestMethod]
    public void V4_ReadExplicitValues()
    {
        var v4 = V4.MigrationWithDefaults.Parse(FullJson);
        Assert.AreEqual("Jo", (string)v4.Name);
        Assert.AreEqual("pending", (string)v4.Status);
        Assert.AreEqual(5, (int)v4.CountValue);
    }

    [TestMethod]
    public void V4_ReadExplicitValues_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationWithDefaults>.Parse(FullJson);
        V4.MigrationWithDefaults v4 = parsedV4.Instance;
        Assert.AreEqual("Jo", (string)v4.Name);
        Assert.AreEqual("pending", (string)v4.Status);
        Assert.AreEqual(5, (int)v4.CountValue);
    }

    [TestMethod]
    public void V5_ReadExplicitValues()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationWithDefaults>.Parse(FullJson);
        V5.MigrationWithDefaults v5 = parsedV5.RootElement;
        Assert.AreEqual("Jo", (string)v5.Name);
        Assert.AreEqual("pending", (string)v5.Status);
        Assert.AreEqual(5, (int)v5.Count);
    }

    [TestMethod]
    public void V4_OptionalPropertiesUndefinedWhenMissing()
    {
        var v4 = V4.MigrationWithDefaults.Parse(MinimalJson);
        Assert.AreEqual("Jo", (string)v4.Name);

        // Both V4 and V5 return schema-defined defaults for missing optional properties
        Assert.AreEqual("active", (string)v4.Status);
        Assert.AreEqual(0, (int)v4.CountValue);
    }

    [TestMethod]
    public void V4_OptionalPropertiesUndefinedWhenMissing_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationWithDefaults>.Parse(MinimalJson);
        V4.MigrationWithDefaults v4 = parsedV4.Instance;
        Assert.AreEqual("Jo", (string)v4.Name);

        // Both V4 and V5 return schema-defined defaults for missing optional properties
        Assert.AreEqual("active", (string)v4.Status);
        Assert.AreEqual(0, (int)v4.CountValue);
    }

    [TestMethod]
    public void V5_OptionalPropertiesUndefinedWhenMissing()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationWithDefaults>.Parse(MinimalJson);
        V5.MigrationWithDefaults v5 = parsedV5.RootElement;
        Assert.AreEqual("Jo", (string)v5.Name);

        // Both V4 and V5 return schema-defined defaults for missing optional properties
        Assert.AreEqual("active", (string)v5.Status);
        Assert.AreEqual(0, (int)v5.Count);
    }

    [TestMethod]
    public void V5_DefaultInstance_HasSchemaDefaults()
    {
        // The top-level DefaultInstance is a default struct with no backing document.
        // Schema defaults are carried by the individual property entity types.
        V5.MigrationWithDefaults.StatusEntity statusDefault = V5.MigrationWithDefaults.StatusEntity.DefaultInstance;
        Assert.IsTrue(statusDefault.TryGetValue(out string? status));
        Assert.AreEqual("active", status);

        V5.MigrationWithDefaults.CountEntity countDefault = V5.MigrationWithDefaults.CountEntity.DefaultInstance;
        Assert.AreEqual(0, (int)countDefault);
    }

    [TestMethod]
    public void V4_Validation_MinimalIsValid()
    {
        var v4 = V4.MigrationWithDefaults.Parse(MinimalJson);
        Corvus.Json.ValidationContext result = v4.Validate(Corvus.Json.ValidationContext.ValidContext, Corvus.Json.ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void V4_Validation_MinimalIsValid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationWithDefaults>.Parse(MinimalJson);
        V4.MigrationWithDefaults v4 = parsedV4.Instance;
        Corvus.Json.ValidationContext result = v4.Validate(Corvus.Json.ValidationContext.ValidContext, Corvus.Json.ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void V5_Validation_MinimalIsValid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationWithDefaults>.Parse(MinimalJson);
        V5.MigrationWithDefaults v5 = parsedV5.RootElement;
        Assert.IsTrue(v5.EvaluateSchema());
    }

    [TestMethod]
    public void V4_Validation_MissingRequiredIsInvalid()
    {
        var v4 = V4.MigrationWithDefaults.Parse("""{"status":"active"}""");
        Corvus.Json.ValidationContext result = v4.Validate(Corvus.Json.ValidationContext.ValidContext, Corvus.Json.ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void V4_Validation_MissingRequiredIsInvalid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationWithDefaults>.Parse("""{"status":"active"}""");
        V4.MigrationWithDefaults v4 = parsedV4.Instance;
        Corvus.Json.ValidationContext result = v4.Validate(Corvus.Json.ValidationContext.ValidContext, Corvus.Json.ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void V5_Validation_MissingRequiredIsInvalid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationWithDefaults>.Parse("""{"status":"active"}""");
        V5.MigrationWithDefaults v5 = parsedV5.RootElement;
        Assert.IsFalse(v5.EvaluateSchema());
    }

    [TestMethod]
    public void BothEngines_ExplicitValues_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationWithDefaults>.Parse(FullJson);
        V4.MigrationWithDefaults v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationWithDefaults>.Parse(FullJson);
        V5.MigrationWithDefaults v5 = parsedV5.RootElement;

        Assert.AreEqual((string)v4.Name, (string)v5.Name);
        Assert.AreEqual((string)v4.Status, (string)v5.Status);
        Assert.AreEqual((int)v4.CountValue, (int)v5.Count);
    }

    [TestMethod]
    public void BothEngines_MinimalJson_DefaultsApplied_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationWithDefaults>.Parse(MinimalJson);
        V4.MigrationWithDefaults v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationWithDefaults>.Parse(MinimalJson);
        V5.MigrationWithDefaults v5 = parsedV5.RootElement;

        Assert.AreEqual((string)v4.Name, (string)v5.Name);
        Assert.AreEqual((string)v4.Status, (string)v5.Status);
        Assert.AreEqual((int)v4.CountValue, (int)v5.Count);
    }
}