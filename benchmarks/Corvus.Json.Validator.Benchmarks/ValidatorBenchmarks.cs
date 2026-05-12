// <copyright file="ValidatorBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Corvus.Json.Validator;

namespace Corvus.Json.Validator.Benchmarks;

public abstract class ValidatorBenchmarksBase
{
    protected static readonly Dictionary<string, string> ValidJsonBySchema = new()
    {
        ["Person"] =
            """
            {
              "firstName": "Alice",
              "lastName": "Smith",
              "age": 30,
              "email": "alice@example.com",
              "address": {
                "street": "123 Main St",
                "city": "Springfield",
                "state": "IL",
                "zipCode": "62704"
              },
              "phoneNumbers": [
                { "type": "home", "number": "+1-555-1234" },
                { "type": "mobile", "number": "+1-555-5678" }
              ]
            }
            """,
        ["ProductCatalog"] =
            """
            {
              "catalogId": "550e8400-e29b-41d4-a716-446655440000",
              "catalogName": "Summer 2024",
              "lastUpdated": "2024-06-15T10:30:00Z",
              "products": [
                {
                  "sku": "AB-123456",
                  "name": "Widget",
                  "description": "A fine widget",
                  "price": 19.99,
                  "currency": "USD",
                  "inStock": true,
                  "categories": ["electronics", "gadgets"],
                  "dimensions": { "width": 10.0, "height": 5.0, "depth": 2.0, "unit": "cm" }
                },
                {
                  "sku": "CD-654321",
                  "name": "Gadget",
                  "price": 49.99,
                  "currency": "EUR",
                  "inStock": false,
                  "categories": ["electronics"]
                }
              ]
            }
            """,
    };

    protected static readonly Dictionary<string, string> InvalidJsonBySchema = new()
    {
        ["Person"] =
            """
            {
              "firstName": "Alice",
              "lastName": "Smith",
              "age": -5,
              "email": "not-an-email"
            }
            """,
        ["ProductCatalog"] =
            """
            {
              "catalogId": "not-a-uuid",
              "products": [
                {
                  "sku": "bad-sku",
                  "name": "",
                  "price": -1
                }
              ]
            }
            """,
    };

    protected JsonSchema schema;

    [Params("Person", "ProductCatalog")]
    public string Schema { get; set; } = null!;

    protected void SetupSchema()
    {
        string schemaFile = this.Schema switch
        {
            "Person" => "person.json",
            "ProductCatalog" => "product-catalog.json",
            _ => throw new InvalidOperationException($"Unknown schema: {this.Schema}"),
        };

        string schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", schemaFile);
        this.schema = JsonSchema.FromFile(schemaPath);
    }
}

[MemoryDiagnoser]
public class ValidDocumentBenchmarks : ValidatorBenchmarksBase
{
    private string json = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.SetupSchema();
        this.json = ValidJsonBySchema[this.Schema];
    }

    [Benchmark]
    public bool Validate()
    {
        using var doc = JsonDocument.Parse(this.json);
        return this.schema.Validate(doc.RootElement).IsValid;
    }
}

[MemoryDiagnoser]
public class InvalidDocumentBenchmarks : ValidatorBenchmarksBase
{
    private string json = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.SetupSchema();
        this.json = InvalidJsonBySchema[this.Schema];
    }

    [Benchmark]
    public bool Validate()
    {
        using var doc = JsonDocument.Parse(this.json);
        return this.schema.Validate(doc.RootElement).IsValid;
    }
}