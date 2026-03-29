// <copyright file="JsonSchemaFactoryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Xunit;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Tests for the factory methods on <see cref="JsonSchema"/>.
/// </summary>
public class JsonSchemaFactoryTests
{
    private const string PersonSchemaJson =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://example.com/test/person",
          "type": "object",
          "required": ["name", "age"],
          "properties": {
            "name": { "type": "string" },
            "age": { "type": "integer", "minimum": 0 }
          },
          "additionalProperties": false
        }
        """;

    [Fact]
    public void FromText_WithCanonicalUri_CreatesSchema()
    {
        var schema = JsonSchema.FromText(
            PersonSchemaJson,
            "https://example.com/test/person");

        Assert.True(schema.Validate("""{"name":"Alice","age":30}"""));
    }

    [Fact]
    public void FromText_WithoutCanonicalUri_UsesSchemaId()
    {
        var schema = JsonSchema.FromText(PersonSchemaJson);

        Assert.True(schema.Validate("""{"name":"Alice","age":30}"""));
    }

    [Fact]
    public void FromText_WithoutCanonicalUriOrSchemaId_Throws()
    {
        string schemaWithoutId =
            """
            {
              "type": "string"
            }
            """;

        Assert.Throws<InvalidOperationException>(() =>
            JsonSchema.FromText(schemaWithoutId));
    }

    [Fact]
    public void FromText_ValidatesInvalidDocument()
    {
        var schema = JsonSchema.FromText(
            PersonSchemaJson,
            "https://example.com/test/person");

        Assert.False(schema.Validate("""{"name":"Alice"}"""));
    }

    [Fact]
    public void FromStream_CreatesSchema()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(PersonSchemaJson));

        var schema = JsonSchema.FromStream(
            stream,
            "https://example.com/test/person-from-stream");

        Assert.True(schema.Validate("""{"name":"Bob","age":25}"""));
    }

    [Fact]
    public void FromFile_CreatesSchema()
    {
        string schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "person.json");

        var schema = JsonSchema.FromFile(schemaPath);

        Assert.True(schema.Validate("""{"name":"Charlie","age":40}"""));
    }

    [Fact]
    public void FromFile_ValidatesInvalidDocument()
    {
        string schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "person.json");

        var schema = JsonSchema.FromFile(schemaPath);

        Assert.False(schema.Validate("""{"name":"Charlie","age":-1}"""));
    }

    [Fact]
    public void FromText_SimpleStringSchema()
    {
        string schema =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/simple-string",
              "type": "string",
              "minLength": 1,
              "maxLength": 10
            }
            """;

        var jsonSchema = JsonSchema.FromText(schema);

        Assert.True(jsonSchema.Validate("\"hello\""));
        Assert.False(jsonSchema.Validate("\"\""));
        Assert.False(jsonSchema.Validate("\"this string is way too long\""));
    }

    [Fact]
    public void FromText_SimpleIntegerSchema()
    {
        string schema =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/simple-integer",
              "type": "integer",
              "minimum": 0,
              "maximum": 100
            }
            """;

        var jsonSchema = JsonSchema.FromText(schema);

        Assert.True(jsonSchema.Validate("50"));
        Assert.False(jsonSchema.Validate("101"));
        Assert.False(jsonSchema.Validate("-1"));
    }

    [Fact]
    public void FromText_ArraySchema()
    {
        string schema =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/string-array",
              "type": "array",
              "items": { "type": "string" },
              "minItems": 1,
              "maxItems": 3
            }
            """;

        var jsonSchema = JsonSchema.FromText(schema);

        Assert.True(jsonSchema.Validate("""["a","b"]"""));
        Assert.False(jsonSchema.Validate("[]"));
        Assert.False(jsonSchema.Validate("""["a","b","c","d"]"""));
    }
}