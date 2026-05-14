// <copyright file="OpenApi31WalkerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi31;

namespace Corvus.Text.Json.OpenApi.Tests;

[TestClass]
public class OpenApi31WalkerTests
{
    private static JsonElement specRoot;
    private static ParsedJsonDocument<JsonElement> parsedDoc = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "petstore-3.1.json");
        byte[] bytes = File.ReadAllBytes(path);
        parsedDoc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        specRoot = parsedDoc.RootElement;
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        parsedDoc.Dispose();
    }

    [TestMethod]
    public void EnumerateOperations_FindsAllOperations()
    {
        var walker = new OpenApi31Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot).ToList();

        // Petstore: GET /pets, POST /pets, GET /pets/{petId}
        Assert.AreEqual(3, ops.Count);
    }

    [TestMethod]
    public void EnumerateOperations_CorrectMethods()
    {
        var walker = new OpenApi31Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot).ToList();

        Assert.AreEqual(2, ops.Count(o => o.Method == OperationMethod.Get));
        Assert.AreEqual(1, ops.Count(o => o.Method == OperationMethod.Post));
    }

    [TestMethod]
    public void EnumerateOperations_PathPropertyHasName()
    {
        var walker = new OpenApi31Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot).ToList();

        // Get the path names using UTF-16 (allocating for test assertions only)
        List<string> pathNames = ops.Select(o =>
        {
            using UnescapedUtf16JsonString name = o.Path.Utf16NameSpan;
            return name.Span.ToString();
        }).ToList();

        Assert.IsTrue(pathNames.Contains("/pets"));
        Assert.IsTrue(pathNames.Contains("/pets/{petId}"));
    }

    [TestMethod]
    public void EnumerateOperations_OperationHasOperationId()
    {
        var walker = new OpenApi31Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot).ToList();

        // Cast to typed Operation to get operationId
        List<string?> operationIds = ops.Select(o =>
        {
            OpenApiDocument.Operation typed = o.Operation;
            return typed.OperationId.ValueKind == JsonValueKind.String
                ? (string?)typed.OperationId
                : null;
        }).ToList();

        Assert.IsTrue(operationIds.Contains("listPets"));
        Assert.IsTrue(operationIds.Contains("createPet"));
        Assert.IsTrue(operationIds.Contains("showPetById"));
    }

    [TestMethod]
    public void EnumerateOperations_FilterIncludesOnlyMatchingPaths()
    {
        var walker = new OpenApi31Walker();
        var filter = new OperationFilter(includePaths: ["/pets"]);
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot, filter).ToList();

        // Only /pets (GET + POST), not /pets/{petId}
        Assert.AreEqual(2, ops.Count);
    }

    [TestMethod]
    public void EnumerateOperations_FilterExcludesPaths()
    {
        var walker = new OpenApi31Walker();
        var filter = new OperationFilter(excludePaths: ["/pets/{petId}"]);
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot, filter).ToList();

        // Only /pets (GET + POST)
        Assert.AreEqual(2, ops.Count);
    }

    [TestMethod]
    public void EnumerateOperations_GlobFilterMatchesSubpaths()
    {
        var walker = new OpenApi31Walker();
        var filter = new OperationFilter(includePaths: ["/pets/**"]);
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot, filter).ToList();

        // /pets/** matches /pets and /pets/{petId}
        Assert.AreEqual(3, ops.Count);
    }

    [TestMethod]
    public void ExtractSchemas_FindsResponseSchemas()
    {
        var walker = new OpenApi31Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(specRoot).ToList();

        Assert.IsTrue(schemas.Any(s => s.Role == SchemaRole.ResponseBody));
    }

    [TestMethod]
    public void ExtractSchemas_FindsRequestBodySchemas()
    {
        var walker = new OpenApi31Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(specRoot).ToList();

        Assert.IsTrue(schemas.Any(s => s.Role == SchemaRole.RequestBody));
    }

    [TestMethod]
    public void ExtractSchemas_FindsParameterSchemas()
    {
        var walker = new OpenApi31Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(specRoot).ToList();

        Assert.IsTrue(schemas.Any(s => s.Role == SchemaRole.Parameter));
    }

    [TestMethod]
    public void ExtractSchemas_FindsComponentSchemas()
    {
        var walker = new OpenApi31Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(specRoot).ToList();

        int componentCount = schemas.Count(s => s.Role == SchemaRole.ComponentSchema);

        // Petstore has 4 component schemas: Pet, NewPet, Pets, Error
        Assert.AreEqual(4, componentCount);
    }

    [TestMethod]
    public void ExtractSchemas_SchemaElementsAreAccessible()
    {
        var walker = new OpenApi31Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(specRoot).ToList();

        // Every extracted schema should be a valid JSON object
        foreach (ExtractedSchema schema in schemas)
        {
            Assert.AreEqual(JsonValueKind.Object, schema.Schema.ValueKind);
        }
    }

    [TestMethod]
    public void ExtractSchemas_FilterLimitsOperationSchemas()
    {
        var walker = new OpenApi31Walker();
        var filter = new OperationFilter(includePaths: ["/pets"]);
        List<ExtractedSchema> schemas = walker.ExtractSchemas(specRoot, filter).ToList();

        // Filtered to /pets only — should still have component schemas
        Assert.IsTrue(schemas.Any(s => s.Role == SchemaRole.ComponentSchema));

        // Should have fewer operation-derived schemas than unfiltered
        List<ExtractedSchema> allSchemas = walker.ExtractSchemas(specRoot).ToList();
        int filteredOpSchemas = schemas.Count(s => s.Role != SchemaRole.ComponentSchema);
        int allOpSchemas = allSchemas.Count(s => s.Role != SchemaRole.ComponentSchema);
        Assert.IsTrue(filteredOpSchemas < allOpSchemas);
    }

    [TestMethod]
    public void EnumerateOperations_EmptySpec_ReturnsEmpty()
    {
        var walker = new OpenApi31Walker();
        JsonElement emptyRoot = JsonElement.ParseValue(
            """{"openapi":"3.1.0","info":{"title":"Empty","version":"1.0"}}"""u8);

        List<OperationEntry> ops = walker.EnumerateOperations(emptyRoot).ToList();
        Assert.AreEqual(0, ops.Count);
    }

    [TestMethod]
    public void ExtractSchemas_EmptySpec_ReturnsEmpty()
    {
        var walker = new OpenApi31Walker();
        JsonElement emptyRoot = JsonElement.ParseValue(
            """{"openapi":"3.1.0","info":{"title":"Empty","version":"1.0"}}"""u8);

        List<ExtractedSchema> schemas = walker.ExtractSchemas(emptyRoot).ToList();
        Assert.AreEqual(0, schemas.Count);
    }
}