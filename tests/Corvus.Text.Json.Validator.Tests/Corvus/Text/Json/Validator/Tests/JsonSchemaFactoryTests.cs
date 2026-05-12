// <copyright file="JsonSchemaFactoryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Tests for the factory methods on <see cref="JsonSchema"/>.
/// </summary>
[TestClass]
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

    [TestMethod]
    public void FromText_WithCanonicalUri_CreatesSchema()
    {
        var schema = JsonSchema.FromText(
            PersonSchemaJson,
            "https://example.com/test/person");

        Assert.IsTrue(schema.Validate("""{"name":"Alice","age":30}"""));
    }

    [TestMethod]
    public void FromText_WithoutCanonicalUri_UsesSchemaId()
    {
        var schema = JsonSchema.FromText(PersonSchemaJson);

        Assert.IsTrue(schema.Validate("""{"name":"Alice","age":30}"""));
    }

    [TestMethod]
    public void FromText_WithoutCanonicalUriOrSchemaId_Throws()
    {
        string schemaWithoutId =
            """
            {
              "type": "string"
            }
            """;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            JsonSchema.FromText(schemaWithoutId));
    }

    [TestMethod]
    public void FromText_ValidatesInvalidDocument()
    {
        var schema = JsonSchema.FromText(
            PersonSchemaJson,
            "https://example.com/test/person");

        Assert.IsFalse(schema.Validate("""{"name":"Alice"}"""));
    }

    [TestMethod]
    public void FromStream_CreatesSchema()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(PersonSchemaJson));

        var schema = JsonSchema.FromStream(
            stream,
            "https://example.com/test/person-from-stream");

        Assert.IsTrue(schema.Validate("""{"name":"Bob","age":25}"""));
    }

    [TestMethod]
    public void FromFile_CreatesSchema()
    {
        string schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "person.json");

        var schema = JsonSchema.FromFile(schemaPath);

        Assert.IsTrue(schema.Validate("""{"name":"Charlie","age":40}"""));
    }

    [TestMethod]
    public void FromFile_ValidatesInvalidDocument()
    {
        string schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "person.json");

        var schema = JsonSchema.FromFile(schemaPath);

        Assert.IsFalse(schema.Validate("""{"name":"Charlie","age":-1}"""));
    }

    [TestMethod]
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

        Assert.IsTrue(jsonSchema.Validate("\"hello\""));
        Assert.IsFalse(jsonSchema.Validate("\"\""));
        Assert.IsFalse(jsonSchema.Validate("\"this string is way too long\""));
    }

    [TestMethod]
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

        Assert.IsTrue(jsonSchema.Validate("50"));
        Assert.IsFalse(jsonSchema.Validate("101"));
        Assert.IsFalse(jsonSchema.Validate("-1"));
    }

    [TestMethod]
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

        Assert.IsTrue(jsonSchema.Validate("""["a","b"]"""));
        Assert.IsFalse(jsonSchema.Validate("[]"));
        Assert.IsFalse(jsonSchema.Validate("""["a","b","c","d"]"""));
    }
}