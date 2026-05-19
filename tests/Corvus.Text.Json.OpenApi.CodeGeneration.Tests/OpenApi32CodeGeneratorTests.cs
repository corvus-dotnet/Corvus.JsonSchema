// <copyright file="OpenApi32CodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi32;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class OpenApi32CodeGeneratorTests
{
    private static JsonElement petstoreRoot;
    private static JsonElement covspecRoot;

    // Complete schema-type map for the petstore spec
    private static readonly Dictionary<string, string> PetstoreSchemaTypeMap = new(StringComparer.Ordinal)
    {
        // Parameter schemas
        ["#/paths/~1pets/get/parameters/0/schema"] = "Petstore.Client.JsonInt32",
        ["#/paths/~1pets~1{petId}/get/parameters/0/schema"] = "Petstore.Client.JsonString",
        ["#/paths/~1pets~1{petId}/put/parameters/0/schema"] = "Petstore.Client.JsonString",
        ["#/paths/~1pets~1{petId}~1photo/post/parameters/0/schema"] = "Petstore.Client.JsonString",

        // Request body schemas
        ["#/paths/~1pets/post/requestBody/content/application~1json/schema"] = "Petstore.Client.NewPet",
        ["#/paths/~1pets~1{petId}/put/requestBody/content/application~1x-www-form-urlencoded/schema"] = "Petstore.Client.NewPet",
        ["#/paths/~1pets~1{petId}~1photo/post/requestBody/content/multipart~1form-data/schema"] = "Petstore.Client.PostPetsPetIdPhotoBody",

        // Response body schemas
        ["#/paths/~1pets/get/responses/200/content/application~1json/schema"] = "Petstore.Client.Pets",
        ["#/paths/~1pets/get/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",
        ["#/paths/~1pets/post/responses/201/content/application~1json/schema"] = "Petstore.Client.Pet",
        ["#/paths/~1pets/post/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",
        ["#/paths/~1pets~1{petId}/get/responses/200/content/application~1json/schema"] = "Petstore.Client.Pet",
        ["#/paths/~1pets~1{petId}/get/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",
        ["#/paths/~1pets~1{petId}/put/responses/200/content/application~1json/schema"] = "Petstore.Client.Pet",
        ["#/paths/~1pets~1{petId}/put/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",
        ["#/paths/~1pets~1{petId}~1photo/post/responses/201/content/application~1json/schema"] = "Petstore.Client.Pet",
        ["#/paths/~1pets~1{petId}~1photo/post/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",

        // Response header schemas
        ["#/paths/~1pets/get/responses/200/headers/x-next/schema"] = "Petstore.Client.JsonString",
    };

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "petstore-3.2.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        petstoreRoot = doc.RootElement.Clone();

        string covJson = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "covspec-3.2.json"));
        using ParsedJsonDocument<JsonElement> covDoc = ParsedJsonDocument<JsonElement>.Parse(covJson);
        covspecRoot = covDoc.RootElement.Clone();
    }

    private static OpenApi32CodeGenerator CreateGenerator(
        IReadOnlyDictionary<string, string>? schemaTypeMap = null,
        string? clientNamePrefix = null)
        => new("Petstore.Client", schemaTypeMap ?? PetstoreSchemaTypeMap, clientNamePrefix);

    private static GeneratedFile GetFile(IReadOnlyList<GeneratedFile> files, string name)
        => files.First(f => f.FileName == name);

    private static JsonElement ParseSpec(string json)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        return doc.RootElement.Clone();
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsParameterSchemas()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(petstoreRoot, out var parameterNames).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(pointers, "#/paths/~1pets/get/parameters/0/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1pets~1{petId}/get/parameters/0/schema");

        // Parameter names are recorded for naming heuristics (keyed by fragment without '#')
        Assert.AreEqual("limit", parameterNames["/paths/~1pets/get/parameters/0/schema"]);
        Assert.AreEqual("petId", parameterNames["/paths/~1pets~1{petId}/get/parameters/0/schema"]);
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsRequestBodySchemas()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/post/requestBody/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsResponseSchemas()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/get/responses/200/content/application~1json/schema");
        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/get/responses/default/content/application~1json/schema");
        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/post/responses/201/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsResponseHeaderSchemas()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/get/responses/200/headers/x-next/schema");
    }

    [TestMethod]
    public void Generate_ProducesClientAndRequestFiles()
    {
        OpenApi32CodeGenerator generator = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = generator.Generate(petstoreRoot);

        Assert.IsTrue(files.Count > 0);

        // Must produce a client file
        Assert.IsTrue(files.Any(f => f.FileName.Contains("Client.cs")));
    }

    [TestMethod]
    public void ListOperations_ReturnsExpectedOperations()
    {
        OperationSummary[] ops = OpenApi32CodeGenerator.ListOperations(petstoreRoot);

        Assert.IsTrue(ops.Length >= 4, $"Expected at least 4 operations, got {ops.Length}");
        Assert.IsTrue(ops.Any(o => o.OperationId == "listPets"));
        Assert.IsTrue(ops.Any(o => o.OperationId == "createPet"));
        Assert.IsTrue(ops.Any(o => o.OperationId == "showPetById"));
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsQueryMethodSchemas()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        // Query method parameter
        CollectionAssert.Contains(
            pointers, "#/paths/~1query-endpoint/query/parameters/0/schema");

        // Query method request body
        CollectionAssert.Contains(
            pointers, "#/paths/~1query-endpoint/query/requestBody/content/application~1json/schema");

        // Query method response
        CollectionAssert.Contains(
            pointers, "#/paths/~1query-endpoint/query/responses/200/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsAdditionalOperationsSchemas()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        // COPY operation parameter
        CollectionAssert.Contains(
            pointers, "#/paths/~1resources~1{resourceId}/additionalOperations/COPY/parameters/0/schema");

        // COPY operation header parameter
        CollectionAssert.Contains(
            pointers, "#/paths/~1resources~1{resourceId}/additionalOperations/COPY/parameters/1/schema");

        // COPY operation response body
        CollectionAssert.Contains(
            pointers, "#/paths/~1resources~1{resourceId}/additionalOperations/COPY/responses/201/content/application~1json/schema");

        // COPY operation response header
        CollectionAssert.Contains(
            pointers, "#/paths/~1resources~1{resourceId}/additionalOperations/COPY/responses/201/headers/Location/schema");

        // PURGE operation parameter
        CollectionAssert.Contains(
            pointers, "#/paths/~1resources~1{resourceId}/additionalOperations/PURGE/parameters/0/schema");
    }

    [TestMethod]
    public void ListOperations_IncludesQueryMethod()
    {
        OperationSummary[] ops = OpenApi32CodeGenerator.ListOperations(covspecRoot);

        Assert.IsTrue(
            ops.Any(o => o.OperationId == "queryItems" && o.Method == OperationMethod.Query),
            "Expected queryItems operation with Query method");
    }

    [TestMethod]
    public void ListOperations_IncludesAdditionalOperations()
    {
        OperationSummary[] ops = OpenApi32CodeGenerator.ListOperations(covspecRoot);

        Assert.IsTrue(
            ops.Any(o => o.OperationId == "copyResource" && o.Method == OperationMethod.Custom),
            "Expected copyResource operation with Custom method");
        Assert.IsTrue(
            ops.Any(o => o.OperationId == "purgeResource" && o.Method == OperationMethod.Custom),
            "Expected purgeResource operation with Custom method");
    }

    [TestMethod]
    public void Generate_QueryMethodProducesRequestAndResponse()
    {
        // Build a minimal schema type map for the covspec's query endpoint
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            ["#/paths/~1query-endpoint/query/parameters/0/schema"] = "CovTest.Client.JsonString",
            ["#/paths/~1query-endpoint/query/requestBody/content/application~1json/schema"] = "CovTest.Client.QueryItemsBody",
            ["#/paths/~1query-endpoint/query/responses/200/content/application~1json/schema"] = "CovTest.Client.QueryItemsItemArray",
        };

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        Assert.IsTrue(
            files.Any(f => f.FileName == "QueryItemsRequest.cs"),
            "Expected QueryItemsRequest.cs file");
        Assert.IsTrue(
            files.Any(f => f.FileName == "QueryItemsResponse.cs"),
            "Expected QueryItemsResponse.cs file");

        // Verify the request uses Query method
        GeneratedFile requestFile = files.First(f => f.FileName == "QueryItemsRequest.cs");
        Assert.IsTrue(
            requestFile.Content.Contains("OperationMethod.Query"),
            "Expected OperationMethod.Query in generated request");
    }

    [TestMethod]
    public void Generate_AdditionalOperationsProducesCustomMethodRequest()
    {
        // Build a schema type map for the COPY operation
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            ["#/paths/~1resources~1{resourceId}/get/parameters/0/schema"] = "CovTest.Client.JsonString",
            ["#/paths/~1resources~1{resourceId}/get/responses/200/content/application~1json/schema"] = "CovTest.Client.Item",
            ["#/paths/~1resources~1{resourceId}/additionalOperations/COPY/parameters/0/schema"] = "CovTest.Client.JsonString",
            ["#/paths/~1resources~1{resourceId}/additionalOperations/COPY/parameters/1/schema"] = "CovTest.Client.JsonUri",
            ["#/paths/~1resources~1{resourceId}/additionalOperations/COPY/responses/201/content/application~1json/schema"] = "CovTest.Client.Item",
            ["#/paths/~1resources~1{resourceId}/additionalOperations/COPY/responses/201/headers/Location/schema"] = "CovTest.Client.JsonUri",
            ["#/paths/~1resources~1{resourceId}/additionalOperations/PURGE/parameters/0/schema"] = "CovTest.Client.JsonString",
        };

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        Assert.IsTrue(
            files.Any(f => f.FileName == "CopyResourceRequest.cs"),
            "Expected CopyResourceRequest.cs file");

        // Verify the request uses Custom method and has the custom method name
        GeneratedFile requestFile = files.First(f => f.FileName == "CopyResourceRequest.cs");
        Assert.IsTrue(
            requestFile.Content.Contains("OperationMethod.Custom"),
            "Expected OperationMethod.Custom in generated request");
        Assert.IsTrue(
            requestFile.Content.Contains("\"COPY\"u8"),
            "Expected CustomMethodNameUtf8 with \"COPY\"u8 in generated request");

        Assert.IsTrue(
            files.Any(f => f.FileName == "PurgeResourceRequest.cs"),
            "Expected PurgeResourceRequest.cs file");
    }
}