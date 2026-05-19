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

    [TestMethod]
    public void CollectSchemaPointers_FindsRefBasedParameters()
    {
        // /documents/{documentId} uses $ref for the documentId parameter
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1documents~1{documentId}/get/parameters/0/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsRefBasedRequestBody()
    {
        // PUT /documents/{documentId} uses $ref for the request body
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1documents~1{documentId}/put/requestBody/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsRefBasedResponse()
    {
        // GET /documents/{documentId} uses $ref for the response
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1documents~1{documentId}/get/responses/200/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsRefBasedResponseHeader()
    {
        // GET /documents/{documentId} response uses $ref for X-Document-Version header
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1documents~1{documentId}/get/responses/200/headers/X-Document-Version/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsStreamingItemSchema()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1events~1stream/get/responses/200/content/application~1x-ndjson/itemSchema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsQuerystringContentSchema()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1search-qs/get/parameters/0/content/application~1x-www-form-urlencoded/schema");
    }

    [TestMethod]
    public void Generate_SecuritySchemesEmitsMetadata()
    {
        // Build a minimal schema type map covering the full spec
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        // Should produce a SecuritySchemes file or contain security metadata in the client
        GeneratedFile clientFile = files.First(f => f.FileName.Contains("Client.cs"));
        Assert.IsTrue(
            clientFile.Content.Contains("SecuritySchemes"),
            "Expected SecuritySchemes class in client output");
        Assert.IsTrue(
            clientFile.Content.Contains("BearerAuth"),
            "Expected BearerAuth property in security metadata");
        Assert.IsTrue(
            clientFile.Content.Contains("ApiKeyAuth"),
            "Expected ApiKeyAuth property in security metadata");
        Assert.IsTrue(
            clientFile.Content.Contains("[Obsolete"),
            "Expected [Obsolete] attribute on deprecated security scheme");
    }

    [TestMethod]
    public void Generate_SecuritySchemesEmitsOAuth2Metadata()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile clientFile = files.First(f => f.FileName.Contains("Client.cs"));
        Assert.IsTrue(
            clientFile.Content.Contains("Oauth2MetadataUrl"),
            "Expected Oauth2MetadataUrl property for oauth2 scheme");
        Assert.IsTrue(
            clientFile.Content.Contains("DeviceAuthorizationUrl"),
            "Expected DeviceAuthorizationUrl property for device auth flow");
        Assert.IsTrue(
            clientFile.Content.Contains("OpenIdConnectUrl"),
            "Expected OpenIdConnectUrl property for openIdConnect scheme");
    }

    [TestMethod]
    public void Generate_StreamingEndpointProducesEnumerateMethod()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile responseFile = files.First(f => f.FileName == "StreamEventsResponse.cs");
        Assert.IsTrue(
            responseFile.Content.Contains("EnumerateOkItems"),
            "Expected EnumerateOkItems method in streaming response");
        Assert.IsTrue(
            responseFile.Content.Contains("EnumerateOkSseItems"),
            "Expected EnumerateOkSseItems method in streaming response");
        Assert.IsTrue(
            responseFile.Content.Contains("IAsyncEnumerable"),
            "Expected IAsyncEnumerable return type in streaming response");
        Assert.IsTrue(
            responseFile.Content.Contains("SseEvent<"),
            "Expected SseEvent<T> return type for SSE method");
    }

    [TestMethod]
    public void Generate_QuerystringContentProducesFormUrlEncodedWrite()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile requestFile = files.First(f => f.FileName == "SearchWithQuerystringRequest.cs");
        Assert.IsTrue(
            requestFile.Content.Contains("FormUrlEncodedQueryStringWriter"),
            "Expected FormUrlEncodedQueryStringWriter usage for querystring content param");
    }

    [TestMethod]
    public void Generate_RefRequestBodyProducesBodyOnClient()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        // The client file should contain the updateDocument method with a body parameter
        GeneratedFile clientFile = files.First(f => f.FileName == "ApiDefaultClient.cs");

        Assert.IsTrue(
            clientFile.Content.Contains("UpdateDocumentAsync"),
            "Expected UpdateDocumentAsync method in client file");

        // The method should take a body parameter (resolved from $ref requestBody)
        Assert.IsTrue(
            clientFile.Content.Contains(".Source body"),
            "Expected body parameter in UpdateDocument method from $ref request body");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsAdditionalOpRequestBody()
    {
        // COPY now has a requestBody — exercises lines 692-718
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1resources~1{resourceId}/additionalOperations/COPY/requestBody/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsAdditionalOpQuerystringContent()
    {
        // MOVE has a querystring content parameter — exercises lines 660-687
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1resources~1{resourceId}/additionalOperations/MOVE/parameters/1/content/application~1x-www-form-urlencoded/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsAdditionalOpStreamingItemSchema()
    {
        // MOVE response has itemSchema — exercises lines 877-883
        string[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1resources~1{resourceId}/additionalOperations/MOVE/responses/200/content/application~1x-ndjson/itemSchema");
    }

    [TestMethod]
    public void Generate_OperationWithoutOperationIdUsesFallbackName()
    {
        // /health HEAD has no operationId — exercises GetMethodName fallback (lines 1907-1928)
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        // The fallback name for HEAD /health should be "HeadHealth"
        Assert.IsTrue(
            files.Any(f => f.FileName == "HeadHealthRequest.cs"),
            "Expected HeadHealthRequest.cs file (synthesized from method + path)");
    }

    [TestMethod]
    public void Generate_AdditionalOpWithRequestBodyProducesBodyParam()
    {
        // COPY now has a requestBody — the generated client method should accept body
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile clientFile = files.First(f => f.FileName == "ApiDefaultClient.cs");

        // Should have CopyResource method with body param
        Assert.IsTrue(
            clientFile.Content.Contains("CopyResourceAsync"),
            "Expected CopyResourceAsync method");
        Assert.IsTrue(
            clientFile.Content.Contains("SendWithBodyAsyncCore"),
            "Expected SendWithBodyAsyncCore call for COPY request body");
    }

    [TestMethod]
    public void Generate_AdditionalOpStreamingProducesEnumerateMethod()
    {
        // MOVE response has itemSchema → should produce streaming enumerate method
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        Assert.IsTrue(
            files.Any(f => f.FileName == "MoveResourceResponse.cs"),
            "Expected MoveResourceResponse.cs file");

        GeneratedFile responseFile = files.First(f => f.FileName == "MoveResourceResponse.cs");
        Assert.IsTrue(
            responseFile.Content.Contains("EnumerateOkItems"),
            "Expected EnumerateOkItems method in MOVE streaming response");
    }

    [TestMethod]
    public void Generate_DocumentSelfEmitsIdentityUri()
    {
        // $self at root should produce DocumentIdentityUri in the interface
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile interfaceFile = files.First(f => f.FileName == "IApiDefaultClient.cs");

        Assert.IsTrue(
            interfaceFile.Content.Contains("DocumentIdentityUri"),
            "Expected DocumentIdentityUri property in interface");
        Assert.IsTrue(
            interfaceFile.Content.Contains("covspec-3.2.json"),
            "Expected the $self URI value in the interface");
    }

    [TestMethod]
    public void Generate_OptionsOperationProducesMethod()
    {
        // OPTIONS on /items (no operationId) should produce OptionsItems method
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile clientFile = files.First(f => f.FileName == "ApiDefaultClient.cs");

        Assert.IsTrue(
            clientFile.Content.Contains("OptionsItemsAsync"),
            "Expected OptionsItemsAsync method for OPTIONS /items");
    }

    [TestMethod]
    public void Generate_PatchOperationProducesMethod()
    {
        // PATCH on /items/{itemId} (no operationId) should produce PatchItemsItemId method
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile clientFile = files.First(f => f.FileName == "ApiDefaultClient.cs");

        Assert.IsTrue(
            clientFile.Content.Contains("PatchItemsItemIdAsync"),
            "Expected PatchItemsItemIdAsync method for PATCH /items/{itemId}");
    }

    [TestMethod]
    public void Generate_TraceOperationProducesMethod()
    {
        // TRACE on /health (no operationId) should produce TraceHealth method
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile clientFile = files.First(f => f.FileName == "ApiDefaultClient.cs");

        Assert.IsTrue(
            clientFile.Content.Contains("TraceHealthAsync"),
            "Expected TraceHealthAsync method for TRACE /health");
    }

    [TestMethod]
    public void Generate_StreamBodyProducesSendWithStreamBody()
    {
        // POST /upload-raw with application/octet-stream body should use SendWithStreamBodyAsyncCore
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile clientFile = files.First(f => f.FileName == "ApiDefaultClient.cs");

        Assert.IsTrue(
            clientFile.Content.Contains("UploadRawFileAsync"),
            "Expected UploadRawFileAsync method");
        Assert.IsTrue(
            clientFile.Content.Contains("Stream body"),
            "Expected Stream body parameter for octet-stream upload");
        Assert.IsTrue(
            clientFile.Content.Contains("SendWithStreamBodyAsyncCore"),
            "Expected SendWithStreamBodyAsyncCore call for stream upload");
    }

    [TestMethod]
    public void Generate_OperationDescriptionEmitsRemarks()
    {
        // /items GET has a description — should emit <remarks> in generated docs
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile interfaceFile = files.First(f => f.FileName == "IApiDefaultClient.cs");

        Assert.IsTrue(
            interfaceFile.Content.Contains("<remarks>"),
            "Expected <remarks> XML doc for operation with description");
        Assert.IsTrue(
            interfaceFile.Content.Contains("Returns all items matching optional filters"),
            "Expected description text in remarks");
    }

    [TestMethod]
    public void Generate_PathItemRefResolvesOperations()
    {
        // /versions/{versionId} uses $ref to components/pathItems/VersionedResource
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile clientFile = files.First(f => f.FileName == "ApiDefaultClient.cs");

        Assert.IsTrue(
            clientFile.Content.Contains("GetResourceVersionAsync"),
            "Expected GetResourceVersionAsync method from pathItem $ref");
    }

    [TestMethod]
    public void Generate_CookieParameterIncludedInRequest()
    {
        // /search-qs GET has a cookie parameter session_id
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile requestFile = files.First(f => f.FileName == "SearchWithQuerystringRequest.cs");

        Assert.IsTrue(
            requestFile.Content.Contains("sessionId") || requestFile.Content.Contains("session_id"),
            "Expected session_id cookie parameter in search request struct");
    }

    [TestMethod]
    public void Generate_OperationRefLinkSkipsGracefully()
    {
        // /items/{itemId} GET 200 has a link with operationRef — should be skipped (deferred)
        // The generation should still succeed without errors
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        // Just verify generation succeeds and the response file exists
        Assert.IsTrue(
            files.Any(f => f.FileName == "GetItemResponse.cs"),
            "Expected GetItemResponse.cs to be generated despite operationRef link");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsPathItemRefSchemas()
    {
        // The pathItem $ref should have its schemas collected
        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _)];

        Assert.IsTrue(
            refs.Any(r => r.PositionalPointer.Contains("versions")),
            "Expected schema pointer from pathItem $ref path");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsPatchRequestBody()
    {
        // PATCH on /items/{itemId} has a requestBody
        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _)];

        Assert.IsTrue(
            refs.Any(r => r.PositionalPointer.Contains("items~1{itemId}") && r.PositionalPointer.Contains("patch") && r.PositionalPointer.Contains("requestBody")),
            "Expected PATCH requestBody schema pointer");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsStreamBodyResponse()
    {
        // POST /upload-raw has a response schema
        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _)];

        Assert.IsTrue(
            refs.Any(r => r.PositionalPointer.Contains("upload-raw") && r.PositionalPointer.Contains("responses")),
            "Expected upload-raw response schema pointer");
    }

    [TestMethod]
    public void Generate_FallbackNamingForGetPutPostDelete()
    {
        // /monitoring/status has GET, PUT, POST, DELETE without operationId
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile clientFile = files.First(f => f.FileName == "ApiDefaultClient.cs");

        Assert.IsTrue(
            clientFile.Content.Contains("GetMonitoringStatusAsync"),
            "Expected GetMonitoringStatusAsync from fallback naming");
        Assert.IsTrue(
            clientFile.Content.Contains("PutMonitoringStatusAsync"),
            "Expected PutMonitoringStatusAsync from fallback naming");
        Assert.IsTrue(
            clientFile.Content.Contains("PostMonitoringStatusAsync"),
            "Expected PostMonitoringStatusAsync from fallback naming");
        Assert.IsTrue(
            clientFile.Content.Contains("DeleteMonitoringStatusAsync"),
            "Expected DeleteMonitoringStatusAsync from fallback naming");
    }

    [TestMethod]
    public void Generate_FallbackNamingForQueryMethod()
    {
        // /monitoring/status has QUERY without operationId
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        GeneratedFile clientFile = files.First(f => f.FileName == "ApiDefaultClient.cs");

        Assert.IsTrue(
            clientFile.Content.Contains("QueryMonitoringStatusAsync"),
            "Expected QueryMonitoringStatusAsync from fallback naming");
    }

    [TestMethod]
    public void Generate_LinkWithRequestBodyExpression()
    {
        // /items/{itemId} GET 200 has a RefreshItem link with operationId and requestBody
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();

        OpenApi32CodeGenerator generator = new("CovTest.Client", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(covspecRoot);

        // The response file should have a links accessor
        GeneratedFile responseFile = files.First(f => f.FileName == "GetItemResponse.cs");

        Assert.IsTrue(
            responseFile.Content.Contains("RefreshItem"),
            "Expected RefreshItem link accessor in GetItemResponse");
    }

    private static Dictionary<string, string> BuildFullCovspecSchemaTypeMap()
    {
        // Collect all schema pointers and assign type names
        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(covspecRoot, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            string typeName = r.PositionalPointer.Contains("itemSchema")
                ? "CovTest.Client.StreamEventItem"
                : $"CovTest.Client.Type{i}";
            map[r.PositionalPointer] = typeName;
            i++;
        }

        return map;
    }
}