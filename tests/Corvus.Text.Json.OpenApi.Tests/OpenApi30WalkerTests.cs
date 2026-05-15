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

    [TestMethod]
    public void PathLevelParams_GetOperationInheritsPathParameters()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsePathLevelParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry getOp = ops.First(o => o.Method == OperationMethod.Get);

        // GET inherits orderId (path) + X-Trace-Id (header) from path, plus own fields (query) = 3
        Assert.AreEqual(3, getOp.Parameters.Length);
    }

    [TestMethod]
    public void PathLevelParams_PutOperationOverridesPathParameter()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsePathLevelParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry putOp = ops.First(o => o.Method == OperationMethod.Put);

        // PUT inherits orderId (path) from path, overrides X-Trace-Id (header) = 2
        Assert.AreEqual(2, putOp.Parameters.Length);
    }

    [TestMethod]
    public void PathLevelParams_InheritedParamIsMarkedPathLevel()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsePathLevelParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry getOp = ops.First(o => o.Method == OperationMethod.Get);

        WalkedParameter orderId = FindParam(getOp.Parameters, "orderId");
        Assert.IsTrue(orderId.IsPathLevel);
    }

    [TestMethod]
    public void PathLevelParams_OperationLevelParamIsNotMarkedPathLevel()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsePathLevelParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry getOp = ops.First(o => o.Method == OperationMethod.Get);

        WalkedParameter fields = FindParam(getOp.Parameters, "fields");
        Assert.IsFalse(fields.IsPathLevel);
    }

    [TestMethod]
    public void PathLevelParams_OverriddenParamUsesOperationLevelDefinition()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsePathLevelParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry putOp = ops.First(o => o.Method == OperationMethod.Put);

        WalkedParameter traceId = FindParam(putOp.Parameters, "X-Trace-Id");
        Assert.IsFalse(traceId.IsPathLevel);
        Assert.IsTrue(traceId.IsRequired);
    }

    [TestMethod]
    public void PathLevelParams_SourceIndexPreservesOriginalArrayPosition()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsePathLevelParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry getOp = ops.First(o => o.Method == OperationMethod.Get);

        WalkedParameter orderId = FindParam(getOp.Parameters, "orderId");
        Assert.AreEqual(0, orderId.SourceIndex);

        WalkedParameter traceId = FindParam(getOp.Parameters, "X-Trace-Id");
        Assert.AreEqual(1, traceId.SourceIndex);

        WalkedParameter fields = FindParam(getOp.Parameters, "fields");
        Assert.AreEqual(0, fields.SourceIndex);
    }

    [TestMethod]
    public void PathLevelParams_ExtractSchemasIncludesPathLevelParameterSchemas()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsePathLevelParamsSpec();
        var walker = new OpenApi30Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(doc.RootElement).ToList();

        int paramSchemas = schemas.Count(s => s.Role == SchemaRole.Parameter);
        Assert.IsTrue(paramSchemas >= 3, $"Expected at least 3 parameter schemas, found {paramSchemas}");
    }

    private static WalkedParameter FindParam(ReadOnlyMemory<WalkedParameter> parameters, string name)
    {
        ReadOnlyMemory<byte> nameUtf8 = "name"u8.ToArray();
        ReadOnlySpan<WalkedParameter> span = parameters.Span;

        for (int i = 0; i < span.Length; i++)
        {
            if (span[i].Element.TryGetProperty(nameUtf8.Span, out JsonElement n)
                && n.ValueKind == JsonValueKind.String
                && n.GetString() == name)
            {
                return span[i];
            }
        }

        Assert.Fail($"Parameter '{name}' not found");
        return default; // unreachable
    }

    private static ParsedJsonDocument<JsonElement> ParsePathLevelParamsSpec()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "path-level-params-3.0.json");
        byte[] bytes = File.ReadAllBytes(path);
        return ParsedJsonDocument<JsonElement>.Parse(bytes);
    }

    private static ParsedJsonDocument<JsonElement> ParseRefParamsSpec()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "ref-params-3.0.json");
        byte[] bytes = File.ReadAllBytes(path);
        return ParsedJsonDocument<JsonElement>.Parse(bytes);
    }

    [TestMethod]
    public void Ref_ParameterRefIsResolved()
    {
        using ParsedJsonDocument<JsonElement> doc = ParseRefParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry getOp = ops.First(o => o.Method == OperationMethod.Get);

        WalkedParameter petId = FindParam(getOp.Parameters, "petId");
        Assert.AreEqual(ParameterLocation.Path, petId.Location);
        Assert.IsTrue(petId.IsRequired);
        Assert.IsTrue(petId.HasSchema);
    }

    [TestMethod]
    public void Ref_OperationLevelParameterRefIsResolved()
    {
        using ParsedJsonDocument<JsonElement> doc = ParseRefParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry getOp = ops.First(o => o.Method == OperationMethod.Get);

        WalkedParameter limit = FindParam(getOp.Parameters, "limit");
        Assert.AreEqual(ParameterLocation.Query, limit.Location);
        Assert.IsFalse(limit.IsRequired);
        Assert.IsTrue(limit.HasSchema);
    }

    [TestMethod]
    public void Ref_ResponseRefIsResolved()
    {
        using ParsedJsonDocument<JsonElement> doc = ParseRefParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry getOp = ops.First(o => o.Method == OperationMethod.Get);

        WalkedResponse okResponse = getOp.Responses.First(
            r => r.Property.NameEquals("200"u8));
        Assert.IsTrue(okResponse.Content.Length > 0, "Resolved response should have content");
    }

    [TestMethod]
    public void Ref_RequestBodyRefIsResolved()
    {
        using ParsedJsonDocument<JsonElement> doc = ParseRefParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry postOp = ops.First(o => o.Method == OperationMethod.Post);

        Assert.IsNotNull(postOp.RequestBody, "Resolved request body should not be null");
        Assert.IsTrue(postOp.RequestBody!.Value.IsRequired, "PetBody is required");
        Assert.IsTrue(postOp.RequestBody!.Value.Content.Length > 0, "PetBody has content");
    }

    [TestMethod]
    public void Ref_HeaderRefIsResolved()
    {
        using ParsedJsonDocument<JsonElement> doc = ParseRefParamsSpec();
        var walker = new OpenApi30Walker();
        List<OperationEntry> ops = walker.EnumerateOperations(doc.RootElement).ToList();

        OperationEntry getOp = ops.First(o => o.Method == OperationMethod.Get);

        WalkedResponse defaultResponse = getOp.Responses.First(
            r => r.Property.NameEquals("default"u8));
        Assert.IsTrue(defaultResponse.Headers.Length > 0, "Should have resolved headers");
        Assert.IsTrue(defaultResponse.Headers[0].HasSchema, "Resolved header should have schema");
    }

    [TestMethod]
    public void Ref_ExtractSchemasFindsAllSchemas()
    {
        using ParsedJsonDocument<JsonElement> doc = ParseRefParamsSpec();
        var walker = new OpenApi30Walker();
        List<ExtractedSchema> schemas = walker.ExtractSchemas(doc.RootElement).ToList();

        Assert.IsTrue(schemas.Count >= 4, $"Expected at least 4 schemas, found {schemas.Count}");

        int paramSchemas = schemas.Count(s => s.Role == SchemaRole.Parameter);
        Assert.IsTrue(paramSchemas >= 2, $"Expected at least 2 parameter schemas, found {paramSchemas}");

        int responseSchemas = schemas.Count(s => s.Role == SchemaRole.ResponseBody);
        Assert.IsTrue(responseSchemas >= 1, $"Expected at least 1 response body schema, found {responseSchemas}");

        int requestSchemas = schemas.Count(s => s.Role == SchemaRole.RequestBody);
        Assert.IsTrue(requestSchemas >= 1, $"Expected at least 1 request body schema, found {requestSchemas}");
    }
}