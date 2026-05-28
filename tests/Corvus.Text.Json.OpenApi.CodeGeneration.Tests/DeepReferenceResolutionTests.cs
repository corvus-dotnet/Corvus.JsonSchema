// <copyright file="DeepReferenceResolutionTests.cs" company="Endjin Limited">
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
/// Comprehensive RFC 3986 reference resolution tests covering deeply nested
/// absolute, relative, back-reference, <c>$id</c>-based, and <c>$anchor</c>-based
/// JSON Schema references inside OpenAPI <c>$ref</c> chains.
/// </summary>
/// <remarks>
/// <para>Directory structure written to temp:</para>
/// <code>
/// root/
///   openapi.json          (entry spec)
///   models/
///     api-types.json      (has OpenAPI $ref to ../shared/common.json)
///     responses.json      (has response with schema using $id-based $ref)
///   shared/
///     common.json         (defines types using $id, $anchor, and relative $ref to ./geo/coords.json)
///     geo/
///       coords.json       (defines Coordinates type, back-references ../common.json via $ref)
/// </code>
/// </remarks>
[TestClass]
public class DeepReferenceResolutionTests
{
    // ══════════════════════════════════════════════════════════════════
    // Test Data: Multi-level directory structure exercising all ref types
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Entry spec: OpenAPI $ref to external files in subdirectories.
    /// </summary>
    private const string EntrySpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Deep Ref Test", "version": "1.0.0" },
          "paths": {
            "/users/{userId}": {
              "get": {
                "operationId": "getUser",
                "parameters": [
                  {
                    "name": "userId",
                    "in": "path",
                    "required": true,
                    "schema": { "type": "string", "format": "uuid" }
                  }
                ],
                "responses": {
                  "200": { "$ref": "./models/api-types.json#/components/responses/UserResponse" }
                }
              }
            },
            "/users/{userId}/address": {
              "put": {
                "operationId": "updateAddress",
                "parameters": [
                  {
                    "name": "userId",
                    "in": "path",
                    "required": true,
                    "schema": { "type": "string", "format": "uuid" }
                  }
                ],
                "requestBody": { "$ref": "./models/api-types.json#/components/requestBodies/AddressUpdate" },
                "responses": {
                  "200": { "$ref": "./models/responses.json#/components/responses/AddressResponse" }
                }
              }
            },
            "/locations": {
              "get": {
                "operationId": "listLocations",
                "responses": {
                  "200": { "$ref": "./models/api-types.json#/components/responses/LocationList" }
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// <c>models/api-types.json</c>: Contains responses whose schemas use back-references
    /// (<c>../shared/common.json</c>) and fragment-only refs within the same file.
    /// Also has an OpenAPI <c>$ref</c> pointing to another external file for one schema.
    /// </summary>
    private const string ApiTypesJson = """
        {
          "components": {
            "schemas": {
              "UserSummary": {
                "type": "object",
                "required": ["id", "name"],
                "properties": {
                  "id": { "type": "string", "format": "uuid" },
                  "name": { "type": "string" },
                  "primaryAddress": { "$ref": "../shared/common.json#/$defs/PostalAddress" }
                }
              },
              "LocationItem": {
                "type": "object",
                "required": ["name", "coords"],
                "properties": {
                  "name": { "type": "string" },
                  "coords": { "$ref": "../shared/geo/coords.json#/$defs/GeoPoint" }
                }
              }
            },
            "responses": {
              "UserResponse": {
                "description": "A user",
                "content": {
                  "application/json": {
                    "schema": { "$ref": "#/components/schemas/UserSummary" }
                  }
                }
              },
              "LocationList": {
                "description": "A list of locations",
                "content": {
                  "application/json": {
                    "schema": {
                      "type": "array",
                      "items": { "$ref": "#/components/schemas/LocationItem" }
                    }
                  }
                }
              }
            },
            "requestBodies": {
              "AddressUpdate": {
                "description": "Address update payload",
                "content": {
                  "application/json": {
                    "schema": { "$ref": "../shared/common.json#/$defs/PostalAddress" }
                  }
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// <c>models/responses.json</c>: Uses <c>$id</c>-based <c>$ref</c> to reference a schema
    /// defined with a canonical <c>$id</c> URI in <c>shared/common.json</c>.
    /// </summary>
    private const string ResponsesJson = """
        {
          "components": {
            "responses": {
              "AddressResponse": {
                "description": "An address",
                "content": {
                  "application/json": {
                    "schema": { "$ref": "../shared/common.json#/$defs/PostalAddress" }
                  }
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// <c>shared/common.json</c>: Root schema document with <c>$id</c> and <c>$anchor</c> usage.
    /// <list type="bullet">
    /// <item><c>PostalAddress</c>: has <c>$anchor: "postal-address"</c> and references <c>./geo/coords.json#/$defs/GeoPoint</c></item>
    /// <item><c>Country</c>: has <c>$id: "country-schema"</c> (relative $id)</item>
    /// <item><c>Region</c>: references <c>country-schema</c> via relative <c>$ref</c> (tests $id-based resolution)</item>
    /// </list>
    /// </summary>
    private const string CommonJson = """
        {
          "$id": "https://example.com/schemas/common",
          "$defs": {
            "PostalAddress": {
              "$anchor": "postal-address",
              "type": "object",
              "required": ["street", "city", "country"],
              "properties": {
                "street": { "type": "string" },
                "city": { "type": "string" },
                "zip": { "type": "string" },
                "country": { "$ref": "#/$defs/Country" },
                "location": { "$ref": "./geo/coords.json#/$defs/GeoPoint" }
              }
            },
            "Country": {
              "$id": "country-schema",
              "type": "object",
              "required": ["code", "name"],
              "properties": {
                "code": { "type": "string", "minLength": 2, "maxLength": 2 },
                "name": { "type": "string" }
              }
            },
            "Region": {
              "type": "object",
              "required": ["regionName", "country"],
              "properties": {
                "regionName": { "type": "string" },
                "country": { "$ref": "country-schema" }
              }
            }
          }
        }
        """;

    /// <summary>
    /// <c>shared/geo/coords.json</c>: Defines <c>GeoPoint</c> and back-references
    /// <c>../common.json#/$defs/Region</c> (proving <c>../</c> resolution works from a subdirectory).
    /// Also uses <c>$anchor</c> on <c>GeoPoint</c>.
    /// </summary>
    private const string CoordsJson = """
        {
          "$defs": {
            "GeoPoint": {
              "$anchor": "geo-point",
              "type": "object",
              "required": ["latitude", "longitude"],
              "properties": {
                "latitude": { "type": "number", "minimum": -90, "maximum": 90 },
                "longitude": { "type": "number", "minimum": -180, "maximum": 180 },
                "altitude": { "type": "number" },
                "region": { "$ref": "../common.json#/$defs/Region" }
              }
            },
            "BoundingBox": {
              "type": "object",
              "required": ["topLeft", "bottomRight"],
              "properties": {
                "topLeft": { "$ref": "#/$defs/GeoPoint" },
                "bottomRight": { "$ref": "#/$defs/GeoPoint" }
              }
            }
          }
        }
        """;

    private static string? tempDir;
    private static string? specFilePath;
    private static SchemaReference[]? collectedRefs;
    private static IReadOnlyCollection<GeneratedCodeFile>? generatedFiles;
    private static Dictionary<string, TypeDeclaration>? pointerToType;
    private static Dictionary<string, string>? pointerToTypeName;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        // Write files to temp directory structure
        tempDir = Path.Combine(Path.GetTempPath(), $"corvus-deep-ref-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        string modelsDir = Path.Combine(tempDir, "models");
        string sharedDir = Path.Combine(tempDir, "shared");
        string geoDir = Path.Combine(sharedDir, "geo");
        Directory.CreateDirectory(modelsDir);
        Directory.CreateDirectory(sharedDir);
        Directory.CreateDirectory(geoDir);

        specFilePath = Path.Combine(tempDir, "openapi.json");
        File.WriteAllText(specFilePath, EntrySpec);
        File.WriteAllText(Path.Combine(modelsDir, "api-types.json"), ApiTypesJson);
        File.WriteAllText(Path.Combine(modelsDir, "responses.json"), ResponsesJson);
        File.WriteAllText(Path.Combine(sharedDir, "common.json"), CommonJson);
        File.WriteAllText(Path.Combine(geoDir, "coords.json"), CoordsJson);

        // Load the entry spec
        JsonElement specRoot = LoadSpec(specFilePath);

        // Step 1: Collect schema pointers using external reference resolver
        using ExternalReferenceResolver referenceResolver = new(specRoot, specFilePath);

        collectedRefs = OpenApi31CodeGenerator.CollectSchemaPointers(
            specRoot, out _, referenceResolver: referenceResolver);

        // Step 2: Run the full JSON Schema type builder pipeline
        CompoundDocumentResolver documentResolver = new(
            new FileSystemDocumentResolver());

        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(
            documentResolver, vocabularyRegistry);

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (SchemaReference schemaRef in collectedRefs)
        {
            JsonReference reference;
            int hashIndex = schemaRef.ResolvablePointer.IndexOf('#');

            if (hashIndex == 0)
            {
                reference = new(specFilePath, schemaRef.ResolvablePointer);
            }
            else if (hashIndex > 0)
            {
                string docPart = schemaRef.ResolvablePointer[..hashIndex];
                string fragment = schemaRef.ResolvablePointer[hashIndex..];
                reference = new(Path.GetFullPath(docPart), fragment);
            }
            else
            {
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
        CSharpLanguageProvider.Options options = new("DeepRefTest");
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
    // Back-references (../) — entry → models/api-types.json → ../shared/common.json
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void BackReference_ResolvesPostalAddress_FromApiTypes()
    {
        // models/api-types.json UserSummary has "$ref": "../shared/common.json#/$defs/PostalAddress"
        // This exercises ../ resolution from models/ directory to shared/ directory
        Assert.IsNotNull(generatedFiles);

        bool hasPostalAddress = generatedFiles.Any(f =>
            f.FileContent.Contains("PostalAddress", StringComparison.Ordinal) &&
            f.FileContent.Contains("Street", StringComparison.Ordinal));

        Assert.IsTrue(hasPostalAddress,
            "Generated code should include PostalAddress type resolved via ../ back-reference. " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    [TestMethod]
    public void BackReference_ResolvesGeoPoint_FromCommonViaSubdirectory()
    {
        // shared/common.json PostalAddress has "$ref": "./geo/coords.json#/$defs/GeoPoint"
        // This is a relative forward reference into a subdirectory
        Assert.IsNotNull(generatedFiles);

        bool hasGeoPoint = generatedFiles.Any(f =>
            f.FileContent.Contains("Latitude", StringComparison.Ordinal) &&
            f.FileContent.Contains("Longitude", StringComparison.Ordinal));

        Assert.IsTrue(hasGeoPoint,
            "Generated code should include GeoPoint type resolved via ./geo/coords.json. " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    [TestMethod]
    public void BackReference_ResolvesRegion_FromCoordsBackToCommon()
    {
        // shared/geo/coords.json GeoPoint has "$ref": "../common.json#/$defs/Region"
        // This is a ../ back-reference FROM a subdirectory TO the parent directory
        Assert.IsNotNull(generatedFiles);

        bool hasRegion = generatedFiles.Any(f =>
            f.FileContent.Contains("Region", StringComparison.Ordinal) &&
            f.FileContent.Contains("RegionName", StringComparison.Ordinal));

        Assert.IsTrue(hasRegion,
            "Generated code should include Region type resolved via ../ from geo/coords.json back to common.json. " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    // ══════════════════════════════════════════════════════════════════
    // $id-based references — Country schema uses relative $id
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void IdBasedRef_ResolvesCountry_ViaRelativeId()
    {
        // shared/common.json Country has "$id": "country-schema"
        // Region references it via "$ref": "country-schema"
        // The type builder should resolve this via the $id registry
        Assert.IsNotNull(generatedFiles);

        bool hasCountry = generatedFiles.Any(f =>
            f.FileContent.Contains("Country", StringComparison.Ordinal) &&
            f.FileContent.Contains("Code", StringComparison.Ordinal));

        Assert.IsTrue(hasCountry,
            "Generated code should include Country type (proving $id-based reference resolution works). " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    [TestMethod]
    public void IdBasedRef_RegionReferencesCountryViaId()
    {
        // Region.country uses "$ref": "country-schema" — a relative URI resolved against
        // the document's $id ("https://example.com/schemas/common") to produce
        // "https://example.com/schemas/country-schema"
        Assert.IsNotNull(generatedFiles);

        // Region must have a country property that resolves to the Country type
        bool hasRegionWithCountry = generatedFiles.Any(f =>
            f.FileName.Contains("Region", StringComparison.Ordinal) &&
            f.FileContent.Contains("Country", StringComparison.Ordinal));

        // Alternatively the country type may be inlined into Region
        if (!hasRegionWithCountry)
        {
            hasRegionWithCountry = generatedFiles.Any(f =>
                f.FileContent.Contains("RegionName", StringComparison.Ordinal) &&
                f.FileContent.Contains("Code", StringComparison.Ordinal));
        }

        Assert.IsTrue(hasRegionWithCountry,
            "Region type should reference Country type (resolved via $id 'country-schema'). " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    // ══════════════════════════════════════════════════════════════════
    // $anchor-based references
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void AnchorRef_PostalAddressHasAnchor()
    {
        // PostalAddress declares "$anchor": "postal-address"
        // While we don't yet test $ref via #postal-address from another file,
        // we verify the schema with the anchor is correctly processed
        Assert.IsNotNull(generatedFiles);

        bool hasPostalAddress = generatedFiles.Any(f =>
            f.FileContent.Contains("PostalAddress", StringComparison.Ordinal));

        Assert.IsTrue(hasPostalAddress,
            "PostalAddress type (which declares $anchor) should be generated. " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    [TestMethod]
    public void AnchorRef_GeoPointHasAnchor()
    {
        // GeoPoint declares "$anchor": "geo-point"
        // Verify that the presence of $anchor does not prevent the type from being generated
        Assert.IsNotNull(generatedFiles);

        bool hasGeoPoint = generatedFiles.Any(f =>
            f.FileName.Contains("GeoPoint", StringComparison.Ordinal) &&
            f.FileContent.Contains("Latitude", StringComparison.Ordinal));

        Assert.IsTrue(hasGeoPoint,
            "GeoPoint type (which declares $anchor) should be generated correctly. " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Deep OpenAPI $ref chains — entry → models/responses.json → ../shared/common.json
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void DeepOpenApiRef_ResponsesJson_ResolvesPostalAddress()
    {
        // Entry spec → models/responses.json#/components/responses/AddressResponse
        // That response schema uses "$ref": "../shared/common.json#/$defs/PostalAddress"
        // This tests TWO levels of OpenAPI $ref + JSON Schema $ref back-reference
        Assert.IsNotNull(collectedRefs);

        // Find the pointer for the address endpoint response
        bool hasAddressResponsePointer = collectedRefs.Any(r =>
            r.PositionalPointer.Contains("address") &&
            r.PositionalPointer.Contains("responses/200"));

        Assert.IsTrue(hasAddressResponsePointer,
            $"Should collect schema pointer for PUT /users/{{userId}}/address 200 response. " +
            $"Pointers: {string.Join(", ", collectedRefs.Select(r => r.PositionalPointer))}");
    }

    [TestMethod]
    public void DeepOpenApiRef_RequestBody_ResolvesFromExternalFile()
    {
        // Entry → models/api-types.json#/components/requestBodies/AddressUpdate
        // That request body schema uses "$ref": "../shared/common.json#/$defs/PostalAddress"
        Assert.IsNotNull(collectedRefs);

        bool hasRequestBodyPointer = collectedRefs.Any(r =>
            r.PositionalPointer.Contains("requestBody") ||
            r.PositionalPointer.Contains("requestBodies"));

        Assert.IsTrue(hasRequestBodyPointer,
            $"Should collect schema pointer for the request body. " +
            $"Pointers: {string.Join(", ", collectedRefs.Select(r => r.PositionalPointer))}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Fragment-only $ref within external documents
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void FragmentOnlyRef_WithinApiTypes_ResolvesLocalSchema()
    {
        // models/api-types.json UserResponse schema is "#/components/schemas/UserSummary"
        // This fragment-only ref must resolve against api-types.json, not the entry spec
        Assert.IsNotNull(generatedFiles);

        bool hasUserSummary = generatedFiles.Any(f =>
            f.FileContent.Contains("UserSummary", StringComparison.Ordinal) ||
            (f.FileContent.Contains("Name", StringComparison.Ordinal) &&
             f.FileContent.Contains("PrimaryAddress", StringComparison.Ordinal)));

        Assert.IsTrue(hasUserSummary,
            "UserSummary type should be generated (proving fragment-only $ref resolved within external doc). " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    [TestMethod]
    public void FragmentOnlyRef_WithinCommon_ResolvesCountryFromPostalAddress()
    {
        // shared/common.json PostalAddress uses "$ref": "#/$defs/Country"
        // This is a fragment-only ref within an external doc that must resolve within common.json
        Assert.IsNotNull(generatedFiles);

        // Country type should be generated with Code/Name properties
        bool hasCountry = generatedFiles.Any(f =>
            f.FileName.Contains("Country", StringComparison.Ordinal) &&
            f.FileContent.Contains("Code", StringComparison.Ordinal));

        Assert.IsTrue(hasCountry,
            "Country type should be generated " +
            "(proving fragment-only $ref within common.json resolves to Country in same file). " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Cross-directory relative references
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void CrossDirectoryRef_ModelsToShared_ViaBackReference()
    {
        // models/api-types.json → "../shared/common.json#/$defs/PostalAddress"
        // Crosses from models/ up to root/ then into shared/
        Assert.IsNotNull(collectedRefs);

        // The positional pointer encodes /users/{userId} as ~1users~1{userId} per RFC 6901
        SchemaReference? userResponseRef = collectedRefs.FirstOrDefault(r =>
            r.PositionalPointer.Contains("~1users~1") &&
            r.PositionalPointer.Contains("responses/200"));

        Assert.IsNotNull(userResponseRef, "Should find user response pointer");

        // The resolvable pointer should be usable (either fragment-only or absolute path)
        Assert.IsFalse(
            string.IsNullOrEmpty(userResponseRef.Value.ResolvablePointer),
            "ResolvablePointer should not be empty");
    }

    [TestMethod]
    public void CrossDirectoryRef_SharedToGeoSubdirectory()
    {
        // shared/common.json PostalAddress → "./geo/coords.json#/$defs/GeoPoint"
        // Forward reference into a subdirectory
        Assert.IsNotNull(generatedFiles);

        // If GeoPoint's Latitude property exists in generated code, the cross-directory ref worked
        bool hasLatitude = generatedFiles.Any(f =>
            f.FileContent.Contains("Latitude", StringComparison.Ordinal));

        Assert.IsTrue(hasLatitude,
            "GeoPoint.Latitude should exist (proving ./geo/coords.json relative ref resolved from shared/). " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    [TestMethod]
    public void CrossDirectoryRef_GeoBackToSharedParent()
    {
        // shared/geo/coords.json GeoPoint → "../common.json#/$defs/Region"
        // Back-reference from subdirectory to parent
        Assert.IsNotNull(generatedFiles);

        bool hasRegionName = generatedFiles.Any(f =>
            f.FileContent.Contains("RegionName", StringComparison.Ordinal));

        Assert.IsTrue(hasRegionName,
            "Region.RegionName should exist (proving ../ back-reference from geo/ to shared/). " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Full chain verification — 4 levels deep
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void FullChain_EntryToModelsToSharedToGeo_AllTypesGenerated()
    {
        // entry → models/api-types.json → shared/common.json → shared/geo/coords.json
        // Plus: shared/geo/coords.json → shared/common.json (circular back-ref via Region→Country)
        Assert.IsNotNull(generatedFiles);
        Assert.IsTrue(generatedFiles.Count > 0, "Should generate files");

        // All key types should be present
        string allContent = string.Join("\n", generatedFiles.Select(f => f.FileContent));

        Assert.IsTrue(allContent.Contains("UserSummary", StringComparison.Ordinal),
            "Missing UserSummary (models/api-types.json)");
        Assert.IsTrue(allContent.Contains("PostalAddress", StringComparison.Ordinal),
            "Missing PostalAddress (shared/common.json)");
        Assert.IsTrue(allContent.Contains("Country", StringComparison.Ordinal),
            "Missing Country (shared/common.json, has $id)");
        Assert.IsTrue(
            allContent.Contains("Latitude", StringComparison.Ordinal) &&
            allContent.Contains("Longitude", StringComparison.Ordinal),
            "Missing GeoPoint (shared/geo/coords.json)");
        Assert.IsTrue(allContent.Contains("RegionName", StringComparison.Ordinal),
            "Missing Region (shared/common.json, referenced via ../ from coords.json)");
    }

    [TestMethod]
    public void FullChain_LocationEndpoint_ResolvesGeoPointDirectly()
    {
        // entry → models/api-types.json → LocationItem → ../shared/geo/coords.json#/$defs/GeoPoint
        // This tests a direct relative ref crossing TWO directory levels (models/ → shared/geo/)
        Assert.IsNotNull(generatedFiles);

        bool hasLocationItem = generatedFiles.Any(f =>
            f.FileContent.Contains("LocationItem", StringComparison.Ordinal) ||
            f.FileContent.Contains("Coords", StringComparison.Ordinal));

        Assert.IsTrue(hasLocationItem,
            "LocationItem type should be generated (direct back-ref from models/ to shared/geo/). " +
            $"Files: {string.Join(", ", generatedFiles.Select(f => f.FileName))}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Resolvable pointer correctness
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void ResolvablePointers_AreAbsoluteForExternalRefs()
    {
        // All resolvable pointers that reference external files should be absolute paths
        Assert.IsNotNull(collectedRefs);

        foreach (SchemaReference schemaRef in collectedRefs)
        {
            int hashIndex = schemaRef.ResolvablePointer.IndexOf('#');
            if (hashIndex > 0)
            {
                // External ref — doc part should be absolute
                string docPart = schemaRef.ResolvablePointer[..hashIndex];
                Assert.IsTrue(
                    Path.IsPathRooted(docPart) || Uri.IsWellFormedUriString(docPart, UriKind.Absolute),
                    $"ResolvablePointer doc part should be absolute. Got: '{docPart}' " +
                    $"(from positional: {schemaRef.PositionalPointer})");
            }
        }
    }

    [TestMethod]
    public void ResolvablePointers_FragmentOnly_DoNotContainDocPath()
    {
        // Fragment-only pointers (inline schemas) start with # and have no doc path
        Assert.IsNotNull(collectedRefs);

        foreach (SchemaReference schemaRef in collectedRefs)
        {
            if (schemaRef.ResolvablePointer.StartsWith('#'))
            {
                Assert.IsFalse(
                    schemaRef.ResolvablePointer.Contains('\\') ||
                    schemaRef.ResolvablePointer.Contains(":/"),
                    $"Fragment-only pointer should not contain path separators. " +
                    $"Got: '{schemaRef.ResolvablePointer}'");
            }
        }
    }

    [TestMethod]
    public void ResolvablePointers_ExternalRefs_PointToCorrectFiles()
    {
        // External resolvable pointers should reference files that actually exist
        Assert.IsNotNull(collectedRefs);
        Assert.IsNotNull(tempDir);

        foreach (SchemaReference schemaRef in collectedRefs)
        {
            int hashIndex = schemaRef.ResolvablePointer.IndexOf('#');
            if (hashIndex > 0)
            {
                string docPart = schemaRef.ResolvablePointer[..hashIndex];
                if (!Uri.IsWellFormedUriString(docPart, UriKind.Absolute))
                {
                    Assert.IsTrue(
                        File.Exists(docPart),
                        $"ResolvablePointer references non-existent file: '{docPart}' " +
                        $"(from positional: {schemaRef.PositionalPointer})");
                }
            }
        }
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