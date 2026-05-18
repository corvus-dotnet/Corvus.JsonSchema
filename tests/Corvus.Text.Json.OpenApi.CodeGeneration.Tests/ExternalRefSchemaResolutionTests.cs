// <copyright file="ExternalRefSchemaResolutionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi31;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

/// <summary>
/// End-to-end tests verifying that JSON Schema <c>$ref</c> values inside external
/// OpenAPI documents resolve correctly through the full code generation pipeline.
/// </summary>
/// <remarks>
/// <para>
/// These tests exercise the complete chain:
/// <list type="number">
/// <item><description>Entry spec has OpenAPI <c>$ref</c> to external files</description></item>
/// <item><description>External files contain schemas with JSON Schema <c>$ref</c> (fragment-only and relative)</description></item>
/// <item><description><see cref="OpenApi31CodeGenerator.CollectSchemaPointers"/> collects pointers from resolved elements</description></item>
/// <item><description><see cref="JsonSchemaTypeBuilder"/> follows <c>$ref</c> chains via <see cref="FileSystemDocumentResolver"/></description></item>
/// <item><description>Generated types correctly represent the resolved schemas</description></item>
/// </list>
/// </para>
/// <para>
/// Each test writes real files to a temp directory so the <see cref="FileSystemDocumentResolver"/>
/// can resolve relative references naturally (matching the CLI tool's behavior).
/// </para>
/// </remarks>
[TestClass]
public class ExternalRefSchemaResolutionTests
{
    // ══════════════════════════════════════════════════════════════════
    // Test data: Entry spec references ./common/types.json for schemas
    // The external file has schemas with $ref to other schemas in the same file
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Entry OpenAPI 3.1 spec — references external file for response schemas.
    /// The response content schema lives in <c>./common/types.json</c> and itself
    /// contains a JSON Schema <c>$ref</c> to another type in that same file.
    /// </summary>
    private const string EntrySpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "External Schema Ref Test", "version": "1.0.0" },
          "paths": {
            "/orders/{orderId}": {
              "get": {
                "operationId": "getOrder",
                "parameters": [
                  { "$ref": "./common/types.json#/components/parameters/OrderId" }
                ],
                "responses": {
                  "200": { "$ref": "./common/types.json#/components/responses/OrderResponse" }
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// External file at <c>./common/types.json</c> — contains schemas with JSON Schema <c>$ref</c>.
    /// <list type="bullet">
    /// <item><c>Order</c> schema references <c>#/components/schemas/Address</c> (fragment-only, same file)</item>
    /// <item><c>Address</c> schema references <c>./geo.json#/definitions/Coordinates</c> (relative to this file)</item>
    /// </list>
    /// </summary>
    private const string CommonTypesJson = """
        {
          "components": {
            "parameters": {
              "OrderId": {
                "name": "orderId",
                "in": "path",
                "required": true,
                "schema": { "type": "string", "format": "uuid" }
              }
            },
            "schemas": {
              "Order": {
                "type": "object",
                "required": ["id", "shippingAddress"],
                "properties": {
                  "id": { "type": "string", "format": "uuid" },
                  "total": { "type": "number" },
                  "shippingAddress": { "$ref": "#/components/schemas/Address" }
                }
              },
              "Address": {
                "type": "object",
                "required": ["street", "city"],
                "properties": {
                  "street": { "type": "string" },
                  "city": { "type": "string" },
                  "zip": { "type": "string" },
                  "location": { "$ref": "./geo.json#/definitions/Coordinates" }
                }
              }
            },
            "responses": {
              "OrderResponse": {
                "description": "A single order",
                "content": {
                  "application/json": {
                    "schema": { "$ref": "#/components/schemas/Order" }
                  }
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// Second-level external file at <c>./common/geo.json</c> — referenced by Address schema
    /// via relative <c>$ref</c> from within <c>common/types.json</c>.
    /// </summary>
    private const string GeoJson = """
        {
          "definitions": {
            "Coordinates": {
              "type": "object",
              "required": ["lat", "lon"],
              "properties": {
                "lat": { "type": "number", "minimum": -90, "maximum": 90 },
                "lon": { "type": "number", "minimum": -180, "maximum": 180 }
              }
            }
          }
        }
        """;

    private static string? tempDir;
    private static string? specFilePath;
    private static SchemaReference[]? collectedRefs;
    private static string[]? collectedPointers;
    private static Dictionary<string, string>? pointerToTypeName;
    private static IReadOnlyCollection<GeneratedCodeFile>? generatedFiles;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        // Write files to temp directory
        tempDir = Path.Combine(Path.GetTempPath(), $"corvus-ext-schema-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string commonDir = Path.Combine(tempDir, "common");
        Directory.CreateDirectory(commonDir);

        specFilePath = Path.Combine(tempDir, "openapi.json");
        string commonTypesPath = Path.Combine(commonDir, "types.json");
        string geoPath = Path.Combine(commonDir, "geo.json");

        File.WriteAllText(specFilePath, EntrySpec);
        File.WriteAllText(commonTypesPath, CommonTypesJson);
        File.WriteAllText(geoPath, GeoJson);

        // Load the entry spec for pointer collection
        JsonElement specRoot = LoadSpec(specFilePath);

        // Create the OpenAPI reference resolver (for CollectSchemaPointers)
        using ExternalReferenceResolver referenceResolver = new(specRoot, specFilePath);

        // Step 1: Collect schema pointers (exercises PushResolvedBase across external docs)
        collectedRefs = OpenApi31CodeGenerator.CollectSchemaPointers(
            specRoot, out _, referenceResolver: referenceResolver);
        collectedPointers = [.. collectedRefs.Select(r => r.PositionalPointer)];

        // Step 2: Run the full JSON Schema type builder pipeline
        CompoundDocumentResolver documentResolver = new(
            new FileSystemDocumentResolver());

        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(
            documentResolver, vocabularyRegistry);

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        Dictionary<string, TypeDeclaration> pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (SchemaReference schemaRef in collectedRefs)
        {
            // Build the JsonReference from the resolvable pointer (same logic as OpenApiGenerateCommand).
            // The ResolvablePointer's doc part is already absolute (ResolveToAbsolute ran at scan time).
            JsonReference reference;
            int hashIndex = schemaRef.ResolvablePointer.IndexOf('#');

            if (hashIndex == 0)
            {
                // Fragment-only — resolve against entry document
                reference = new(specFilePath, schemaRef.ResolvablePointer);
            }
            else if (hashIndex > 0)
            {
                // External document + fragment (doc part already absolute)
                string docPart = schemaRef.ResolvablePointer[..hashIndex];
                string fragment = schemaRef.ResolvablePointer[hashIndex..];
                reference = new(Path.GetFullPath(docPart), fragment);
            }
            else
            {
                // No fragment — entire external doc is the schema
                reference = new(Path.GetFullPath(schemaRef.ResolvablePointer), "#");
            }

            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference,
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
                rebaseAsRoot: false);

            pointerToType[schemaRef.PositionalPointer] = rootType;
            typesToGenerate.Add(rootType);
        }

        // Step 3: Generate code
        CSharpLanguageProvider.Options options = new("TestApi");
        CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        generatedFiles = typeBuilder.GenerateCodeUsing(
            languageProvider, typesToGenerate, CancellationToken.None);

        // Build pointer → type name map
        pointerToTypeName = new(StringComparer.Ordinal);
        foreach ((string pointer, TypeDeclaration td) in pointerToType)
        {
            TypeDeclaration reduced = td.ReducedTypeDeclaration().ReducedType;
            if (reduced.HasDotnetTypeName())
            {
                pointerToTypeName[pointer] = reduced.DotnetTypeName()?.ToString() ?? string.Empty;
            }
        }
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        if (tempDir is not null && Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Schema pointer collection
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void CollectSchemaPointers_FindsResponseSchemaInExternalDoc()
    {
        Assert.IsNotNull(collectedPointers);
        Assert.IsTrue(
            collectedPointers.Any(p => p.Contains("responses/200/content")),
            $"Should find response schema pointer. Got: {string.Join(", ", collectedPointers)}");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsParameterSchemaInExternalDoc()
    {
        Assert.IsNotNull(collectedPointers);
        Assert.IsTrue(
            collectedPointers.Any(p => p.Contains("parameters/0/schema")),
            $"Should find parameter schema pointer. Got: {string.Join(", ", collectedPointers)}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Type generation — verifies JSON Schema $ref chains resolve
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void TypeBuilder_GeneratesTypesSuccessfully()
    {
        Assert.IsNotNull(generatedFiles);
        Assert.IsTrue(generatedFiles.Count > 0,
            "Type builder should generate at least one code file");
    }

    [TestMethod]
    public void TypeBuilder_ResolvesResponseSchema_FromExternalDoc()
    {
        // The response content schema points to common/types.json's Order schema
        // via $ref chain: entry→common/types.json#/components/responses/OrderResponse→schema→$ref:#/components/schemas/Order
        Assert.IsNotNull(pointerToTypeName);

        string? responsePointer = collectedPointers?.FirstOrDefault(p => p.Contains("responses/200/content"));
        Assert.IsNotNull(responsePointer, "Response pointer should exist");
        Assert.IsTrue(
            pointerToTypeName.ContainsKey(responsePointer),
            $"Type builder should resolve the response schema. Available: {string.Join(", ", pointerToTypeName.Keys)}");
    }

    [TestMethod]
    public void TypeBuilder_ResolvesFragmentRef_WithinExternalDoc()
    {
        // Order schema has "shippingAddress": { "$ref": "#/components/schemas/Address" }
        // This fragment-only $ref must resolve against common/types.json (not the entry doc)
        Assert.IsNotNull(generatedFiles);

        // Look for generated code that contains "Address" as a type (proving the $ref was followed)
        bool hasAddressType = generatedFiles.Any(f =>
            f.FileContent.Contains("Address", StringComparison.Ordinal) &&
            f.FileContent.Contains("street", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(hasAddressType,
            "Generated code should include Address type from fragment-only $ref within external doc. " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    [TestMethod]
    public void TypeBuilder_ResolvesRelativeRef_FromExternalToDeepExternalDoc()
    {
        // Address schema has "location": { "$ref": "./geo.json#/definitions/Coordinates" }
        // This relative $ref resolves from common/types.json's directory → common/geo.json
        Assert.IsNotNull(generatedFiles);

        // Look for generated code containing "Coordinates" type (lat/lon properties)
        bool hasCoordinatesType = generatedFiles.Any(f =>
            f.FileContent.Contains("Coordinates", StringComparison.Ordinal) ||
            (f.FileContent.Contains("lat", StringComparison.OrdinalIgnoreCase) &&
             f.FileContent.Contains("lon", StringComparison.OrdinalIgnoreCase)));

        Assert.IsTrue(hasCoordinatesType,
            "Generated code should include Coordinates type from relative $ref (geo.json) " +
            "resolved relative to the external doc's directory. " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    [TestMethod]
    public void TypeBuilder_GeneratesProperties_ForOrder()
    {
        // Order must have id, total, shippingAddress properties
        Assert.IsNotNull(generatedFiles);

        bool hasOrderProperties = generatedFiles.Any(f =>
            f.FileContent.Contains("ShippingAddress", StringComparison.Ordinal));

        Assert.IsTrue(hasOrderProperties,
            "Generated code should include Order type with ShippingAddress property " +
            "(proving the external schema was resolved and codegen ran)");
    }

    [TestMethod]
    public void TypeBuilder_GeneratesProperties_ForCoordinates()
    {
        // Coordinates must have lat, lon properties with numeric constraints
        Assert.IsNotNull(generatedFiles);

        // The generated code for Coordinates should reference both lat and lon
        bool hasLatLon = generatedFiles.Any(f =>
            f.FileContent.Contains("Lat", StringComparison.Ordinal) &&
            f.FileContent.Contains("Lon", StringComparison.Ordinal));

        Assert.IsTrue(hasLatLon,
            "Generated code should include Coordinates type with Lat/Lon properties " +
            "(proving the two-level external $ref chain was fully resolved)");
    }

    // ══════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════
    private static JsonElement LoadSpec(string path)
    {
        string json = File.ReadAllText(path);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        return doc.RootElement.Clone();
    }
}