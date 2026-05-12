// <copyright file="BooleanSchemaTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Tests for boolean schema support (true/false schemas).
/// </summary>
[TestClass]
public class BooleanSchemaTests
{
    [TestMethod]
    public void TrueSchema_ValidatesAnything()
    {
        string schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "boolean-true.json");

        var schema = JsonSchema.FromFile(schemaPath);

        Assert.IsTrue(schema.Validate("\"hello\""));
        Assert.IsTrue(schema.Validate("42"));
        Assert.IsTrue(schema.Validate("true"));
        Assert.IsTrue(schema.Validate("null"));
        Assert.IsTrue(schema.Validate("[]"));
        Assert.IsTrue(schema.Validate("{}"));
    }

    [TestMethod]
    public void FalseSchema_RejectsEverything()
    {
        string schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "boolean-false.json");

        var schema = JsonSchema.FromFile(schemaPath);

        Assert.IsFalse(schema.Validate("\"hello\""));
        Assert.IsFalse(schema.Validate("42"));
        Assert.IsFalse(schema.Validate("true"));
        Assert.IsFalse(schema.Validate("null"));
        Assert.IsFalse(schema.Validate("[]"));
        Assert.IsFalse(schema.Validate("{}"));
    }

    [TestMethod]
    public void TrueSchema_FromText()
    {
        var schema = JsonSchema.FromText(
            "true",
            "https://example.com/test/boolean-true-text");

        Assert.IsTrue(schema.Validate("\"anything\""));
        Assert.IsTrue(schema.Validate("123"));
    }

    [TestMethod]
    public void FalseSchema_FromText()
    {
        var schema = JsonSchema.FromText(
            "false",
            "https://example.com/test/boolean-false-text");

        Assert.IsFalse(schema.Validate("\"anything\""));
        Assert.IsFalse(schema.Validate("123"));
    }

    [TestMethod]
    public void TrueSchema_WithResultsCollector_ReturnsTrue()
    {
        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);

        var schema = JsonSchema.FromText(
            "true",
            "https://example.com/test/boolean-true-collector");

        Assert.IsTrue(schema.Validate("\"hello\"", collector));
    }

    [TestMethod]
    public void FalseSchema_WithResultsCollector_ReturnsFalse()
    {
        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);

        var schema = JsonSchema.FromText(
            "false",
            "https://example.com/test/boolean-false-collector");

        Assert.IsFalse(schema.Validate("\"hello\"", collector));
    }
}