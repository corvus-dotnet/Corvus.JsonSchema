// <copyright file="BooleanSchemaTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Tests for boolean schema support (true/false schemas).
/// </summary>
public class BooleanSchemaTests
{
    [Fact]
    public void TrueSchema_ValidatesAnything()
    {
        string schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "boolean-true.json");

        var schema = JsonSchema.FromFile(schemaPath);

        Assert.True(schema.Validate("\"hello\""));
        Assert.True(schema.Validate("42"));
        Assert.True(schema.Validate("true"));
        Assert.True(schema.Validate("null"));
        Assert.True(schema.Validate("[]"));
        Assert.True(schema.Validate("{}"));
    }

    [Fact]
    public void FalseSchema_RejectsEverything()
    {
        string schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "boolean-false.json");

        var schema = JsonSchema.FromFile(schemaPath);

        Assert.False(schema.Validate("\"hello\""));
        Assert.False(schema.Validate("42"));
        Assert.False(schema.Validate("true"));
        Assert.False(schema.Validate("null"));
        Assert.False(schema.Validate("[]"));
        Assert.False(schema.Validate("{}"));
    }

    [Fact]
    public void TrueSchema_FromText()
    {
        var schema = JsonSchema.FromText(
            "true",
            "https://example.com/test/boolean-true-text");

        Assert.True(schema.Validate("\"anything\""));
        Assert.True(schema.Validate("123"));
    }

    [Fact]
    public void FalseSchema_FromText()
    {
        var schema = JsonSchema.FromText(
            "false",
            "https://example.com/test/boolean-false-text");

        Assert.False(schema.Validate("\"anything\""));
        Assert.False(schema.Validate("123"));
    }

    [Fact]
    public void TrueSchema_WithResultsCollector_ReturnsTrue()
    {
        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);

        var schema = JsonSchema.FromText(
            "true",
            "https://example.com/test/boolean-true-collector");

        Assert.True(schema.Validate("\"hello\"", collector));
    }

    [Fact]
    public void FalseSchema_WithResultsCollector_ReturnsFalse()
    {
        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);

        var schema = JsonSchema.FromText(
            "false",
            "https://example.com/test/boolean-false-collector");

        Assert.False(schema.Validate("\"hello\"", collector));
    }
}