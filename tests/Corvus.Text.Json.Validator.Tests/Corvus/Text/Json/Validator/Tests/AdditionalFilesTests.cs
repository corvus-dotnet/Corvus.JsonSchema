// <copyright file="AdditionalFilesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Tests for additional schema file support in <see cref="JsonSchema"/>.
/// </summary>
public class AdditionalFilesTests
{
    [Fact]
    public void FromText_WithAdditionalFiles_ResolvesRef()
    {
        string addressSchemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "address.json");

        JsonSchema.Options options = new(
            additionalSchemaFiles:
            [
                new AdditionalSchemaFile("https://example.com/schemas/address", addressSchemaPath),
            ]);

        string personWithAddressSchema =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/additional-files-ref",
              "type": "object",
              "required": ["name", "address"],
              "properties": {
                "name": { "type": "string" },
                "address": { "$ref": "https://example.com/schemas/address" }
              }
            }
            """;

        var schema = JsonSchema.FromText(personWithAddressSchema, options: options);

        string validJson =
            """
            {
              "name": "Alice",
              "address": {
                "street": "123 Main St",
                "city": "Springfield",
                "zipCode": "12345"
              }
            }
            """;

        string invalidJson =
            """
            {
              "name": "Alice",
              "address": {
                "city": "Springfield"
              }
            }
            """;

        Assert.True(schema.Validate(validJson));
        Assert.False(schema.Validate(invalidJson));
    }

    [Fact]
    public void FromText_WithAdditionalFiles_ResolvesRefById()
    {
        // The address.json schema has $id = "https://example.com/schemas/address"
        // Register it with a different canonical URI — it should still be resolvable by its $id
        string addressSchemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "address.json");

        JsonSchema.Options options = new(
            additionalSchemaFiles:
            [
                new AdditionalSchemaFile("file:///some/other/path/address.json", addressSchemaPath),
            ]);

        string personWithAddressSchema =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/additional-files-ref-by-id",
              "type": "object",
              "required": ["address"],
              "properties": {
                "address": { "$ref": "https://example.com/schemas/address" }
              }
            }
            """;

        var schema = JsonSchema.FromText(personWithAddressSchema, options: options);

        string validJson =
            """
            {
              "address": {
                "street": "456 Oak Ave",
                "city": "Shelbyville"
              }
            }
            """;

        Assert.True(schema.Validate(validJson));
    }

    [Fact]
    public void FromFile_WithAdditionalFiles_ResolvesRef()
    {
        string addressSchemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "address.json");

        string personWithAddressPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "person-with-address.json");

        JsonSchema.Options options = new(
            additionalSchemaFiles:
            [
                new AdditionalSchemaFile("https://example.com/schemas/address", addressSchemaPath),
            ]);

        var schema = JsonSchema.FromFile(personWithAddressPath, options: options);

        string validJson =
            """
            {
              "name": "Charlie",
              "address": {
                "street": "789 Elm St",
                "city": "Capital City"
              }
            }
            """;

        Assert.True(schema.Validate(validJson));
    }
}