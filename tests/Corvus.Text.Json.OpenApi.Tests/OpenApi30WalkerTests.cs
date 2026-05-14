// <copyright file="OpenApi30WalkerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi30;

namespace Corvus.Text.Json.OpenApi.Tests;

[TestClass]
public class OpenApi30WalkerTests
{
    private static JsonElement specRoot;
    private static ParsedJsonDocument<JsonElement> parsedDoc = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "petstore-3.0.json");
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
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot).ToList();

        // Petstore 3.0: GET /pets, POST /pets, GET /pets/{petId}
        Assert.AreEqual(3, ops.Count);
    }

    [TestMethod]
    public void EnumerateOperations_CorrectMethods()
    {
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot).ToList();

        Assert.AreEqual(2, ops.Count(o => o.Method == OperationMethod.Get));
        Assert.AreEqual(1, ops.Count(o => o.Method == OperationMethod.Post));
    }

    [TestMethod]
    public void EnumerateOperations_PathPropertyHasName()
    {
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot).ToList();

        List<string> pathNames = ops.Select(o =>
        {
            using UnescapedUtf16JsonString name = o.Path.Utf16NameSpan;
            return name.Span.ToString();
        }).ToList();

        Assert.IsTrue(pathNames.Contains("/pets"));
        Assert.IsTrue(pathNames.Contains("/pets/{petId}"));
    }

    [TestMethod]
    public void EnumerateOperations_FilterIncludesOnlyMatchingPaths()
    {
        var walker = new OpenApi30Walker();
        var filter = new OperationFilter(includePaths: ["/pets"]);
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot, filter).ToList();

        Assert.AreEqual(2, ops.Count);
    }

    [TestMethod]
    public void EnumerateOperations_FilterExcludesPaths()
    {
        var walker = new OpenApi30Walker();
        var filter = new OperationFilter(excludePaths: ["/pets/{petId}"]);
        List<OperationEntry> ops = walker.EnumerateOperations(specRoot, filter).ToList();

        Assert.AreEqual(2, ops.Count);
    }

    [TestMethod]
    public void ExtractSchemas_FindsResponseSchemas()
    {
        var walker = new OpenApi30Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(specRoot).ToList();

        Assert.IsTrue(schemas.Any(s => s.Role == SchemaRole.ResponseBody));
    }

    [TestMethod]
    public void ExtractSchemas_FindsParameterSchemas()
    {
        var walker = new OpenApi30Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(specRoot).ToList();

        Assert.IsTrue(schemas.Any(s => s.Role == SchemaRole.Parameter));
    }

    [TestMethod]
    public void ExtractSchemas_FindsComponentSchemas()
    {
        var walker = new OpenApi30Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(specRoot).ToList();

        int componentCount = schemas.Count(s => s.Role == SchemaRole.ComponentSchema);

        // Petstore 3.0 has component schemas: Pet, Pets, Error
        Assert.IsTrue(componentCount >= 3);
    }

    [TestMethod]
    public void ExtractSchemas_SchemaElementsAreAccessible()
    {
        var walker = new OpenApi30Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(specRoot).ToList();

        foreach (ExtractedSchema schema in schemas)
        {
            Assert.AreEqual(JsonValueKind.Object, schema.Schema.ValueKind);
        }
    }

    [TestMethod]
    public void EnumerateOperations_EmptySpec_ReturnsEmpty()
    {
        var walker = new OpenApi30Walker();
        JsonElement emptyRoot = JsonElement.ParseValue(
            """{"openapi":"3.0.0","info":{"title":"Empty","version":"1.0"},"paths":{}}"""u8);

        List<OperationEntry> ops = walker.EnumerateOperations(emptyRoot).ToList();
        Assert.AreEqual(0, ops.Count);
    }

    [TestMethod]
    public void ExtractSchemas_EmptySpec_ReturnsEmpty()
    {
        var walker = new OpenApi30Walker();
        JsonElement emptyRoot = JsonElement.ParseValue(
            """{"openapi":"3.0.0","info":{"title":"Empty","version":"1.0"},"paths":{}}"""u8);

        List<ExtractedSchema> schemas = walker.ExtractSchemas(emptyRoot).ToList();
        Assert.AreEqual(0, schemas.Count);
    }
}