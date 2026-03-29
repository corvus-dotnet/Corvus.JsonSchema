// <copyright file="ResultsCollectorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Tests for results collector integration with <see cref="JsonSchema"/>.
/// </summary>
public class ResultsCollectorTests
{
    private static readonly JsonSchema Schema = CreateSchema();

    [Fact]
    public void Validate_WithCollector_ValidDocument_ReturnsTrue()
    {
        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);

        bool result = Schema.Validate("""{"name":"Alice","age":30}""", collector);

        Assert.True(result);
    }

    [Fact]
    public void Validate_WithCollector_InvalidDocument_ReturnsFalse()
    {
        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);

        bool result = Schema.Validate("""{"name":"Alice"}""", collector);

        Assert.False(result);
    }

    [Fact]
    public void Validate_WithCollector_InvalidDocument_HasResults()
    {
        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);

        Schema.Validate("""{"name":"Alice"}""", collector);

        Assert.True(collector.GetResultCount() > 0);
    }

    [Fact]
    public void Validate_WithCollector_ValidDocument_HasNoFailureResults()
    {
        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);

        Schema.Validate("""{"name":"Alice","age":30}""", collector);

        bool hasFailure = false;
        foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
        {
            if (!r.IsMatch)
            {
                hasFailure = true;
                break;
            }
        }

        Assert.False(hasFailure);
    }

    [Fact]
    public void Validate_WithNullCollector_StillReturnsCorrectResult()
    {
        Assert.True(Schema.Validate("""{"name":"Alice","age":30}""", null));
        Assert.False(Schema.Validate("""{"name":"Alice"}""", null));
    }

    [Fact]
    public void Validate_WithDetailedCollector_InvalidDocument_HasResults()
    {
        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);

        Schema.Validate("""{"name":"Alice"}""", collector);

        Assert.True(collector.GetResultCount() > 0);
    }

    private static JsonSchema CreateSchema()
    {
        string schemaJson =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/results-collector",
              "type": "object",
              "required": ["name", "age"],
              "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer", "minimum": 0 }
              },
              "additionalProperties": false
            }
            """;

        return JsonSchema.FromText(schemaJson);
    }
}