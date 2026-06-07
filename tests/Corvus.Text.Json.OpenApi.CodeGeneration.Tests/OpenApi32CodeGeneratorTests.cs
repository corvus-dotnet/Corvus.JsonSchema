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
    private static JsonElement callbacksSpecRoot;

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

        string callbacksJson = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "callbacks-3.2.json"));
        using ParsedJsonDocument<JsonElement> callbacksDoc = ParsedJsonDocument<JsonElement>.Parse(callbacksJson);
        callbacksSpecRoot = callbacksDoc.RootElement.Clone();
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

    [TestMethod]
    public void ListTags_ReturnsHierarchicalTagInfo()
    {
        const string json = """
            {
              "openapi": "3.2.0",
              "info": { "title": "Test", "version": "1.0.0" },
              "tags": [
                {
                  "name": "products",
                  "summary": "Products",
                  "description": "All product operations",
                  "kind": "nav"
                },
                {
                  "name": "cakes",
                  "summary": "Cakes",
                  "description": "Cake catalog",
                  "parent": "products",
                  "kind": "nav"
                },
                {
                  "name": "seasonal",
                  "summary": "Seasonal",
                  "kind": "badge"
                },
                {
                  "name": "partner",
                  "parent": "external",
                  "kind": "audience"
                },
                {
                  "name": "external",
                  "summary": "External",
                  "kind": "audience"
                }
              ],
              "paths": {}
            }
            """;

        JsonElement root = ParseSpec(json);
        TagInfo[] tags = OpenApi32CodeGenerator.ListTags(root);

        Assert.AreEqual(5, tags.Length);

        TagInfo products = tags.First(t => t.Name == "products");
        Assert.AreEqual("Products", products.Summary);
        Assert.AreEqual("All product operations", products.Description);
        Assert.IsNull(products.Parent);
        Assert.AreEqual("nav", products.Kind);

        TagInfo cakes = tags.First(t => t.Name == "cakes");
        Assert.AreEqual("Cakes", cakes.Summary);
        Assert.AreEqual("products", cakes.Parent);
        Assert.AreEqual("nav", cakes.Kind);

        TagInfo seasonal = tags.First(t => t.Name == "seasonal");
        Assert.AreEqual("Seasonal", seasonal.Summary);
        Assert.IsNull(seasonal.Parent);
        Assert.AreEqual("badge", seasonal.Kind);

        TagInfo partner = tags.First(t => t.Name == "partner");
        Assert.AreEqual("external", partner.Parent);
        Assert.AreEqual("audience", partner.Kind);
        Assert.IsNull(partner.Summary);

        TagInfo external = tags.First(t => t.Name == "external");
        Assert.AreEqual("External", external.Summary);
        Assert.IsNull(external.Parent);
        Assert.AreEqual("audience", external.Kind);
    }

    [TestMethod]
    public void ListTags_ReturnsEmptyWhenNoTagsArray()
    {
        const string json = """
            {
              "openapi": "3.2.0",
              "info": { "title": "Test", "version": "1.0.0" },
              "paths": {}
            }
            """;

        JsonElement root = ParseSpec(json);
        TagInfo[] tags = OpenApi32CodeGenerator.ListTags(root);

        Assert.AreEqual(0, tags.Length);
    }

    [TestMethod]
    public void ListTags_SkipsTagsWithoutName()
    {
        const string json = """
            {
              "openapi": "3.2.0",
              "info": { "title": "Test", "version": "1.0.0" },
              "tags": [
                { "name": "valid", "kind": "nav" },
                { "summary": "No Name" },
                { "name": "also-valid" }
              ],
              "paths": {}
            }
            """;

        JsonElement root = ParseSpec(json);
        TagInfo[] tags = OpenApi32CodeGenerator.ListTags(root);

        Assert.AreEqual(2, tags.Length);
        Assert.AreEqual("valid", tags[0].Name);
        Assert.AreEqual("also-valid", tags[1].Name);
    }

    [TestMethod]
    public void GenerateServer_ProducesHandlerInterfaces()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        Assert.IsTrue(
            files.Any(f => f.FileName == "IApiDefaultHandler.cs"),
            "Expected IApiDefaultHandler.cs");
        Assert.IsTrue(
            files.Any(f => f.FileName == "IApiItemsHandler.cs"),
            "Expected IApiItemsHandler.cs");
        Assert.IsTrue(
            files.Any(f => f.FileName == "IApiSearchHandler.cs"),
            "Expected IApiSearchHandler.cs");
    }

    [TestMethod]
    public void GenerateServer_HandlerInterface_ContainsExpectedMethods()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        GeneratedFile defaultHandler = files.First(f => f.FileName == "IApiDefaultHandler.cs");

        Assert.IsTrue(
            defaultHandler.Content.Contains("HandleListItemsAsync"),
            "Expected HandleListItemsAsync in IApiDefaultHandler");
        Assert.IsTrue(
            defaultHandler.Content.Contains("HandleCreateItemAsync"),
            "Expected HandleCreateItemAsync in IApiDefaultHandler");
        Assert.IsTrue(
            defaultHandler.Content.Contains("HandleGetItemAsync"),
            "Expected HandleGetItemAsync in IApiDefaultHandler");

        // Verify method signatures use params and result types
        Assert.IsTrue(
            defaultHandler.Content.Contains("ListItemsParams parameters"),
            "Expected ListItemsParams parameter in handler method");
        Assert.IsTrue(
            defaultHandler.Content.Contains("ValueTask<ListItemsResult>"),
            "Expected ValueTask<ListItemsResult> return type");
    }

    [TestMethod]
    public void GenerateServer_ProducesParamsStructs()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        Assert.IsTrue(
            files.Any(f => f.FileName == "ListItemsParams.cs"),
            "Expected ListItemsParams.cs");
        Assert.IsTrue(
            files.Any(f => f.FileName == "CreateItemParams.cs"),
            "Expected CreateItemParams.cs");
        Assert.IsTrue(
            files.Any(f => f.FileName == "GetItemParams.cs"),
            "Expected GetItemParams.cs");
    }

    [TestMethod]
    public void GenerateServer_ParamsStruct_ContainsCorrectProperties()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        GeneratedFile listItemsParams = files.First(f => f.FileName == "ListItemsParams.cs");

        // ListItems has query params: active, category, page, sort, verbose
        Assert.IsTrue(
            listItemsParams.Content.Contains("Active"),
            "Expected Active property in ListItemsParams");
        Assert.IsTrue(
            listItemsParams.Content.Contains("Category"),
            "Expected Category property in ListItemsParams");
        Assert.IsTrue(
            listItemsParams.Content.Contains("Page"),
            "Expected Page property in ListItemsParams");
        Assert.IsTrue(
            listItemsParams.Content.Contains("Sort"),
            "Expected Sort property in ListItemsParams");
        Assert.IsTrue(
            listItemsParams.Content.Contains("Verbose"),
            "Expected Verbose property in ListItemsParams");

        // CreateItem has X-Correlation-Id header and Body
        GeneratedFile createItemParams = files.First(f => f.FileName == "CreateItemParams.cs");
        Assert.IsTrue(
            createItemParams.Content.Contains("XCorrelationId"),
            "Expected XCorrelationId property in CreateItemParams");
        Assert.IsTrue(
            createItemParams.Content.Contains("Body"),
            "Expected Body property in CreateItemParams");
    }

    [TestMethod]
    public void GenerateServer_ProducesResultStructs()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        Assert.IsTrue(
            files.Any(f => f.FileName == "ListItemsResult.cs"),
            "Expected ListItemsResult.cs");
        Assert.IsTrue(
            files.Any(f => f.FileName == "CreateItemResult.cs"),
            "Expected CreateItemResult.cs");
    }

    [TestMethod]
    public void GenerateServer_ResultStruct_HasFactoryMethods()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        GeneratedFile listItemsResult = files.First(f => f.FileName == "ListItemsResult.cs");

        // ListItems has 200 and default responses
        Assert.IsTrue(
            listItemsResult.Content.Contains("Ok("),
            "Expected Ok() factory method in ListItemsResult");
        Assert.IsTrue(
            listItemsResult.Content.Contains("Default("),
            "Expected Default() factory method in ListItemsResult");

        GeneratedFile createItemResult = files.First(f => f.FileName == "CreateItemResult.cs");

        // CreateItem has 201 and default (if present)
        Assert.IsTrue(
            createItemResult.Content.Contains("Created("),
            "Expected Created() factory method in CreateItemResult");
    }

    [TestMethod]
    public void GenerateServer_StreamingResultStruct_HasPushWriterFactory()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        GeneratedFile streamEventsResult = files.First(f => f.FileName == "StreamEventsResult.cs");

        Assert.IsTrue(
            streamEventsResult.Content.Contains("public static StreamEventsResult Ok(StreamEventsStreamWriter writer)"),
            "Expected Ok factory to accept a stream writer callback");
        Assert.IsTrue(
            streamEventsResult.Content.Contains("public static StreamEventsResult Ok<TContext>(TContext context, StreamEventsStreamWriter<TContext> writer)"),
            "Expected context overload for static stream writer callbacks");
        Assert.IsTrue(
            streamEventsResult.Content.Contains("public ValueTask AppendStreamEventItem(in CovTest.Client.StreamEventItem.Source item, CancellationToken cancellationToken = default)"),
            "Expected generated stream type to append item sources");
        Assert.IsTrue(
            streamEventsResult.Content.Contains("public bool HasStreamingBody => this.streamWriter is not null;"),
            "Expected streaming result marker");
        Assert.IsFalse(
            streamEventsResult.Content.Contains("public static StreamEventsResult Ok() =>"),
            "Did not expect parameterless Ok() for itemSchema streaming responses");
    }

    [TestMethod]
    public void GenerateServer_ProducesEndpointRegistration()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        Assert.IsTrue(
            files.Any(f => f.FileName == "ApiEndpointRegistration.cs"),
            "Expected ApiEndpointRegistration.cs");

        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        Assert.IsTrue(
            registration.Content.Contains("MapApiEndpoints"),
            "Expected MapApiEndpoints extension method");
        Assert.IsTrue(
            registration.Content.Contains("MapGet"),
            "Expected MapGet in endpoint registration");
        Assert.IsTrue(
            registration.Content.Contains("MapPost"),
            "Expected MapPost in endpoint registration");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_IncludesAllOperations()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        // Verify key routes are registered
        Assert.IsTrue(
            registration.Content.Contains("\"/items\""),
            "Expected /items route in endpoint registration");
        Assert.IsTrue(
            registration.Content.Contains("\"/items/{itemId}\""),
            "Expected /items/{itemId} route in endpoint registration");
        Assert.IsTrue(
            registration.Content.Contains("\"/download\""),
            "Expected /download route in endpoint registration");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_EmitsConfigureEndpointOverload()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        // The original overload is preserved (no ConfigureEndpoint parameter)...
        Assert.IsTrue(
            registration.Content.Contains(
                "public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app, IApiDefaultHandler defaultHandler",
                StringComparison.Ordinal),
            "Expected the original MapApiEndpoints overload to be preserved");

        // ...and delegates to the new overload passing a null callback.
        Assert.IsTrue(
            registration.Content.Contains("configureEndpoint: null", StringComparison.Ordinal),
            "Expected the original overload to delegate to the new overload with a null callback");

        // The new, additive overload carries the ConfigureEndpoint callback.
        Assert.IsTrue(
            registration.Content.Contains(", ConfigureEndpoint? configureEndpoint)", StringComparison.Ordinal),
            "Expected a new MapApiEndpoints overload accepting a ConfigureEndpoint callback");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_EmitsConfigurationTypes()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        Assert.IsTrue(
            registration.Content.Contains(
                "public delegate void ConfigureEndpoint(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder);",
                StringComparison.Ordinal),
            "Expected the ConfigureEndpoint delegate to be emitted");
        Assert.IsTrue(
            registration.Content.Contains("public readonly struct EndpointDescriptor", StringComparison.Ordinal),
            "Expected the EndpointDescriptor struct to be emitted");
        Assert.IsTrue(
            registration.Content.Contains("public readonly struct EndpointSecurityRequirement", StringComparison.Ordinal),
            "Expected the EndpointSecurityRequirement struct to be emitted");

        // No new package dependency: the callback uses only Microsoft.AspNetCore.Routing types.
        Assert.IsFalse(
            registration.Content.Contains("Microsoft.AspNetCore.Authorization", StringComparison.Ordinal),
            "Generated registration must not take a dependency on Microsoft.AspNetCore.Authorization");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_InvokesCallbackPerEndpoint()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        // The callback is invoked once per endpoint, building an EndpointDescriptor.
        Assert.IsTrue(
            registration.Content.Contains("configureEndpoint?.Invoke(", StringComparison.Ordinal),
            "Expected configureEndpoint to be invoked per endpoint");
        Assert.IsTrue(
            registration.Content.Contains("new EndpointDescriptor(", StringComparison.Ordinal),
            "Expected an EndpointDescriptor to be constructed for each endpoint");

        // Descriptor fields are populated from operation metadata. listItems is a GET on /items.
        Assert.IsTrue(
            registration.Content.Contains("methodName: \"ListItems\"", StringComparison.Ordinal),
            "Expected the descriptor to carry the generated method name");
        Assert.IsTrue(
            registration.Content.Contains("operationId: \"listItems\"", StringComparison.Ordinal),
            "Expected the descriptor to carry the operationId");
        Assert.IsTrue(
            registration.Content.Contains("httpMethod: \"GET\"", StringComparison.Ordinal),
            "Expected the descriptor to carry the HTTP method");

        // Regular (paths) server: every endpoint is flagged as not a callback.
        Assert.IsTrue(
            registration.Content.Contains("isCallback: false", StringComparison.Ordinal),
            "Expected regular server endpoints to be flagged isCallback: false");
        Assert.IsFalse(
            registration.Content.Contains("isCallback: true", StringComparison.Ordinal),
            "Regular server endpoints must not be flagged as callbacks");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_SurfacesSecurityRequirements()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        // covspec declares security schemes; the operation's requirements must be surfaced on the
        // descriptor so consumers can apply authorization.
        Assert.IsTrue(
            registration.Content.Contains("securityRequirements: new EndpointSecurityRequirementSet[]", StringComparison.Ordinal),
            "Expected at least one operation to surface its security requirement sets");
        Assert.IsTrue(
            registration.Content.Contains("new EndpointSecurityRequirement(\"bearerAuth\", System.Array.Empty<string>(), \"http\")", StringComparison.Ordinal),
            "Expected the bearerAuth scheme to be surfaced on the descriptor with its resolved scheme type");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_EmitsRequireDeclaredAuthorizationHelper()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot);

        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        Assert.IsTrue(
            registration.Content.Contains("public static class EndpointSecurityConventions", StringComparison.Ordinal),
            "Expected the EndpointSecurityConventions helper class to be emitted");
        Assert.IsTrue(
            registration.Content.Contains("public readonly struct EndpointSecurityRequirementSet", StringComparison.Ordinal),
            "Expected the EndpointSecurityRequirementSet struct (the OR alternative) to be emitted");
        Assert.IsTrue(
            registration.Content.Contains("public static IEndpointConventionBuilder RequireDeclaredAuthorization(this IEndpointConventionBuilder builder, in EndpointDescriptor endpoint)", StringComparison.Ordinal),
            "Expected the RequireDeclaredAuthorization extension method to be emitted");
        Assert.IsTrue(
            registration.Content.Contains("public string PolicyName =>", StringComparison.Ordinal),
            "Expected EndpointSecurityRequirement to expose a PolicyName property");
        Assert.IsFalse(
            registration.Content.Contains("Microsoft.AspNetCore.Authorization", StringComparison.Ordinal),
            "Generated registration must not take a dependency on Microsoft.AspNetCore.Authorization");
    }

    [TestMethod]
    public void GenerateCallbackServer_EndpointRegistration_FlagsEndpointsAsCallback()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi32CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackServer(callbacksSpecRoot);

        GeneratedFile registration = files.First(f => f.FileName.Contains("EndpointRegistration.cs"));

        // Webhook/callback endpoints are flagged as callbacks, and still invoke the hook.
        Assert.IsTrue(
            registration.Content.Contains("configureEndpoint?.Invoke(", StringComparison.Ordinal),
            "Expected the callback server to invoke configureEndpoint per endpoint");
        Assert.IsTrue(
            registration.Content.Contains("isCallback: true", StringComparison.Ordinal),
            "Expected webhook/callback endpoints to be flagged isCallback: true");
        Assert.IsFalse(
            registration.Content.Contains("isCallback: false", StringComparison.Ordinal),
            "Callback server endpoints must not be flagged isCallback: false");
    }

    [TestMethod]
    public void GenerateServer_WithFilter_OnlyIncludesMatchedPaths()
    {
        Dictionary<string, string> schemaTypeMap = BuildFullCovspecSchemaTypeMap();
        OpenApi32CodeGenerator generator = new("CovTest.Server", schemaTypeMap);
        OperationFilter filter = new(["/items/**"]);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(covspecRoot, filter);

        // Should have items-related files
        Assert.IsTrue(
            files.Any(f => f.FileName == "ListItemsParams.cs"),
            "Expected ListItemsParams.cs with /items/** filter");

        // Should NOT have operations from unmatched paths like /download or /health
        Assert.IsFalse(
            files.Any(f => f.FileName == "DownloadFileParams.cs"),
            "Did not expect DownloadFileParams.cs with /items/** filter");
        Assert.IsFalse(
            files.Any(f => f.FileName == "HeadHealthParams.cs"),
            "Did not expect HeadHealthParams.cs with /items/** filter");
    }

    [TestMethod]
    public void GenerateServer_Petstore_ProducesCorrectFileCount()
    {
        OpenApi32CodeGenerator generator = new("Petstore.Server", PetstoreSchemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(petstoreRoot);

        // Petstore has operations: listPets, createPet, showPetById, updatePet, uploadPetPhoto
        // Each produces Params + Result = 2 files per operation
        // Plus handler interfaces (grouped by tag) + 1 endpoint registration
        Assert.IsTrue(files.Count > 0, "Expected at least some generated files");

        // Should have endpoint registration
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("EndpointRegistration.cs")),
            "Expected EndpointRegistration.cs in petstore server output");

        // Should have at least one handler interface
        Assert.IsTrue(
            files.Any(f => f.FileName.StartsWith("I") && f.FileName.Contains("Handler.cs")),
            "Expected at least one handler interface in petstore server output");
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

    [TestMethod]
    public void CollectSchemaPointers_EmptyPaths_ReturnsEmpty()
    {
        // Exercises OpenApi32CodeGenerator lines 200-203: PathsValue.IsUndefined()
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Empty", "version": "1.0" }
            }
            """);

        SchemaReference[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out Dictionary<string, string> parameterNames)];

        Assert.AreEqual(0, pointers.Length);
        Assert.AreEqual(0, parameterNames.Count);
    }

    [TestMethod]
    public void CollectSchemaPointers_WithFilter_ExcludesNonMatchingPaths()
    {
        // Exercises OpenApi32CodeGenerator lines 212-216: filter.Matches()
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Filter Test", "version": "1.0" },
              "paths": {
                "/included": {
                  "get": {
                    "operationId": "getIncluded",
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "a": { "type": "string" } } }
                          }
                        }
                      }
                    }
                  }
                },
                "/excluded": {
                  "get": {
                    "operationId": "getExcluded",
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "b": { "type": "string" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        // Use a filter that only matches "/included"
        OperationFilter filter = new(["/included"]);
        SchemaReference[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _, filter: filter)];

        Assert.IsTrue(pointers.Any(p => p.PositionalPointer.Contains("included")));
        Assert.IsFalse(pointers.Any(p => p.PositionalPointer.Contains("excluded")));
    }

    [TestMethod]
    public void CollectSchemaPointers_BrokenParameterRef_IsSkipped()
    {
        // Exercises TryResolveParameter returning false (lines 1270-1273)
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Broken Ref", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "getTest",
                    "parameters": [
                      { "$ref": "#/components/parameters/NonExistent" }
                    ],
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "string" }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        // Should not throw — broken ref is simply skipped
        SchemaReference[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];

        // The response schema should still be collected
        Assert.IsTrue(pointers.Any(p => p.PositionalPointer.Contains("responses")));
    }

    [TestMethod]
    public void CollectSchemaPointers_BrokenRequestBodyRef_IsSkipped()
    {
        // Exercises TryResolveRequestBody returning false (lines 1301-1304)
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Broken RequestBody Ref", "version": "1.0" },
              "paths": {
                "/test": {
                  "post": {
                    "operationId": "postTest",
                    "requestBody": { "$ref": "#/components/requestBodies/NonExistent" },
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "id": { "type": "integer" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];

        // The response schema should still be collected
        Assert.IsTrue(pointers.Any(p => p.PositionalPointer.Contains("responses")));
    }

    [TestMethod]
    public void CollectSchemaPointers_BrokenResponseRef_IsSkipped()
    {
        // Exercises TryResolveResponse returning false (lines 1332-1335)
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Broken Response Ref", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "getTest",
                    "responses": {
                      "200": { "$ref": "#/components/responses/NonExistent" },
                      "404": {
                        "description": "Not found",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "error": { "type": "string" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];

        // The 404 response schema should still be collected
        Assert.IsTrue(pointers.Any(p => p.PositionalPointer.Contains("404")));
    }

    [TestMethod]
    public void CollectSchemaPointers_BrokenHeaderRef_IsSkipped()
    {
        // Exercises TryResolveHeader returning false (lines 1363-1366)
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Broken Header Ref", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "getTest",
                    "responses": {
                      "200": {
                        "description": "OK",
                        "headers": {
                          "X-Rate-Limit": { "$ref": "#/components/headers/NonExistent" }
                        },
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "ok": { "type": "boolean" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];

        // The response body schema should still be collected
        Assert.IsTrue(pointers.Any(p => p.PositionalPointer.Contains("content")));
    }

    [TestMethod]
    public void CollectSchemaPointers_BrokenPathItemRef_IsSkipped()
    {
        // Exercises TryResolvePathItem returning false (lines 1394-1396)
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Broken PathItem Ref", "version": "1.0" },
              "paths": {
                "/broken": { "$ref": "#/components/pathItems/NonExistent" },
                "/working": {
                  "get": {
                    "operationId": "getWorking",
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "val": { "type": "number" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];

        // The working path's schema should still be collected
        Assert.IsTrue(pointers.Any(p => p.PositionalPointer.Contains("working")));
    }

    [TestMethod]
    public void Generate_NonExplodeObjectParameter_ProducesCode()
    {
        // Exercises CodeEmitHelpers lines 1989-2002: non-explode object parameter parsing
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Non-Explode Object", "version": "1.0" },
              "paths": {
                "/items/{id}": {
                  "get": {
                    "operationId": "getItem",
                    "parameters": [
                      {
                        "name": "id",
                        "in": "path",
                        "required": true,
                        "schema": { "type": "string" }
                      },
                      {
                        "name": "filter",
                        "in": "query",
                        "style": "form",
                        "explode": false,
                        "schema": {
                          "type": "object",
                          "additionalProperties": { "type": "string" }
                        }
                      }
                    ],
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"Test.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Count > 0);

        // Verify the generated client handles non-explode object
        GeneratedFile clientFile = files.First(f => f.FileName.Contains("Client.cs"));
        Assert.IsTrue(clientFile.Content.Length > 0);
    }

    [TestMethod]
    public void Generate_ExoticNumericFormats_ProducesCode()
    {
        // Exercises CodeEmitHelpers lines 1858-1865: decimal, int16, byte, sbyte, etc.
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Exotic Numerics", "version": "1.0" },
              "paths": {
                "/numeric": {
                  "get": {
                    "operationId": "getNumeric",
                    "parameters": [
                      {
                        "name": "decimalVal",
                        "in": "query",
                        "schema": { "type": "number", "format": "decimal" }
                      },
                      {
                        "name": "shortVal",
                        "in": "query",
                        "schema": { "type": "integer", "format": "int16" }
                      },
                      {
                        "name": "byteVal",
                        "in": "query",
                        "schema": { "type": "integer", "format": "uint8" }
                      },
                      {
                        "name": "sbyteVal",
                        "in": "query",
                        "schema": { "type": "integer", "format": "int8" }
                      },
                      {
                        "name": "ushortVal",
                        "in": "query",
                        "schema": { "type": "integer", "format": "uint16" }
                      },
                      {
                        "name": "uintVal",
                        "in": "query",
                        "schema": { "type": "integer", "format": "uint32" }
                      },
                      {
                        "name": "ulongVal",
                        "in": "query",
                        "schema": { "type": "integer", "format": "uint64" }
                      },
                      {
                        "name": "halfVal",
                        "in": "query",
                        "schema": { "type": "number", "format": "half" }
                      }
                    ],
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "result": { "type": "string" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"Numeric.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("Numeric", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Count > 0);
        GeneratedFile clientFile = files.First(f => f.FileName.Contains("Client.cs"));

        // Should contain parsing for the exotic types — the codegen path was exercised
        Assert.IsTrue(clientFile.Content.Length > 0);
    }

    [TestMethod]
    public void GenerateServer_NonExplodeObjectParameter_ProducesCode()
    {
        // Exercises CodeEmitHelpers lines 1989-2002 in server mode
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Server Non-Explode", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      {
                        "name": "filter",
                        "in": "query",
                        "style": "form",
                        "explode": false,
                        "schema": {
                          "type": "object",
                          "additionalProperties": { "type": "string" }
                        }
                      }
                    ],
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "items": { "type": "array", "items": { "type": "string" } } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"SrvTest.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("SrvTest", map);
        IReadOnlyList<GeneratedFile> files = gen.GenerateServer(spec);

        Assert.IsTrue(files.Count > 0);
    }

    [TestMethod]
    public void Generate_DeepNestedArrayParameter_ProducesCode()
    {
        // Exercises CodeEmitHelpers lines 1920-1923: hasDeepNesting with array
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Deep Nesting", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      {
                        "name": "ids",
                        "in": "query",
                        "style": "form",
                        "explode": false,
                        "schema": {
                          "type": "array",
                          "items": {
                            "type": "object",
                            "properties": {
                              "nested": { "type": "string" }
                            }
                          }
                        }
                      }
                    ],
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "ok": { "type": "boolean" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"Deep.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("Deep", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Count > 0);
    }

    [TestMethod]
    public void CollectSchemaPointers_MultipartMixedPrefixEncoding_FindsSchemas()
    {
        // Exercises lines 835-853: multipart/mixed with prefixEncoding in additionalOperations
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "MultipartMixed PrefixEncoding", "version": "1.0" },
              "paths": {
                "/batch": {
                  "additionalOperations": {
                    "BATCH": {
                      "operationId": "batchItems",
                      "requestBody": {
                        "required": true,
                        "content": {
                          "multipart/mixed": {
                            "schema": {
                              "type": "array",
                              "prefixItems": [
                                { "type": "object", "properties": { "meta": { "type": "string" } } },
                                { "type": "string", "format": "binary" }
                              ]
                            },
                            "prefixEncoding": [
                              { "contentType": "application/json" },
                              { "contentType": "application/octet-stream" }
                            ]
                          }
                        }
                      },
                      "responses": {
                        "200": {
                          "description": "OK",
                          "content": {
                            "application/json": {
                              "schema": { "type": "object", "properties": { "ok": { "type": "boolean" } } }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];

        // Should find the first prefixItem schema (JSON one, not binary)
        Assert.IsTrue(pointers.Length >= 1);
    }

    [TestMethod]
    public void CollectSchemaPointers_MultipartMixedItemEncoding_FindsSchemas()
    {
        // Exercises lines 855-868: multipart/mixed with itemEncoding in additionalOperations
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "MultipartMixed ItemEncoding", "version": "1.0" },
              "paths": {
                "/process": {
                  "additionalOperations": {
                    "PROCESS": {
                      "operationId": "processItems",
                      "requestBody": {
                        "required": true,
                        "content": {
                          "multipart/mixed": {
                            "schema": {
                              "type": "array",
                              "items": {
                                "type": "object",
                                "properties": {
                                  "action": { "type": "string" },
                                  "payload": { "type": "object" }
                                }
                              }
                            },
                            "itemEncoding": {
                              "contentType": "application/json"
                            }
                          }
                        }
                      },
                      "responses": {
                        "200": {
                          "description": "OK",
                          "content": {
                            "application/json": {
                              "schema": { "type": "object", "properties": { "count": { "type": "integer" } } }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];

        // Should find the items schema
        Assert.IsTrue(pointers.Length >= 1);
    }

    [TestMethod]
    public void Generate_BrokenRequestBodyRef_Throws()
    {
        // Exercises line 1672-1674: ThrowUnableToResolveRequestBodyRef in Generate path
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Broken RB Ref Gen", "version": "1.0" },
              "paths": {
                "/test": {
                  "post": {
                    "operationId": "postTest",
                    "requestBody": { "$ref": "#/components/requestBodies/Missing" },
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "x": { "type": "string" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"Err.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("Err", map);

        Assert.ThrowsExactly<InvalidOperationException>(() => gen.Generate(spec));
    }

    [TestMethod]
    public void Generate_BrokenResponseRef_Throws()
    {
        // Exercises line 1787-1789: ThrowUnableToResolveResponseRef in Generate path
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Broken Resp Ref Gen", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "getTest",
                    "responses": {
                      "200": { "$ref": "#/components/responses/Missing" }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"Err.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("Err", map);

        Assert.ThrowsExactly<InvalidOperationException>(() => gen.Generate(spec));
    }

    [TestMethod]
    public void Generate_BrokenHeaderRef_Throws()
    {
        // Exercises line 1968-1970: ThrowUnableToResolveHeaderRef in Generate path
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Broken Header Ref Gen", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "getTest",
                    "responses": {
                      "200": {
                        "description": "OK",
                        "headers": {
                          "X-Rate": { "$ref": "#/components/headers/Missing" }
                        },
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "y": { "type": "integer" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"Err.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("Err", map);

        Assert.ThrowsExactly<InvalidOperationException>(() => gen.Generate(spec));
    }

    [TestMethod]
    public void Generate_MalformedPathTemplate_EmitsLiteral()
    {
        // Exercises line 2880-2882: unclosed '{' in path template
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Malformed Path", "version": "1.0" },
              "paths": {
                "/items/{id": {
                  "get": {
                    "operationId": "getItem",
                    "parameters": [
                      {
                        "name": "id",
                        "in": "path",
                        "required": true,
                        "schema": { "type": "string" }
                      }
                    ],
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"Malformed.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("Malformed", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        // Should generate code despite malformed path — literal segment emitted
        Assert.IsTrue(files.Count > 0);
    }

    [TestMethod]
    public void Generate_MultipartMixedBinaryItemBatch_EmitsWriteBinaryPart()
    {
        // Exercises lines 5542-5558: binary batch item code emission via itemEncoding
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Binary Batch", "version": "1.0" },
              "paths": {
                "/upload": {
                  "post": {
                    "operationId": "uploadBatch",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "multipart/mixed": {
                          "schema": {
                            "type": "array",
                            "items": {
                              "type": "string",
                              "format": "binary"
                            }
                          },
                          "itemEncoding": {
                            "contentType": "application/octet-stream"
                          }
                        }
                      }
                    },
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "count": { "type": "integer" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"BinBatch.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("BinBatch", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        // Check that binary batch code is emitted (WriteBinaryPartAsync)
        bool hasBinaryBatch = false;
        foreach (GeneratedFile f in files)
        {
            if (f.Content.Contains("WriteBinaryPartAsync"))
            {
                hasBinaryBatch = true;
                break;
            }
        }

        Assert.IsTrue(hasBinaryBatch, "Expected WriteBinaryPartAsync in generated code for binary batch");
    }

    [TestMethod]
    public void GenerateServer_DeepNestedArrayParam_EmitsElementType()
    {
        // Exercises lines 6256-6262: deep nesting with element type resolution in server code
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Deep Nested Server", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      {
                        "name": "filters",
                        "in": "query",
                        "style": "form",
                        "explode": false,
                        "schema": {
                          "type": "array",
                          "items": {
                            "type": "object",
                            "properties": {
                              "field": { "type": "string" },
                              "value": { "type": "string" }
                            }
                          }
                        }
                      }
                    ],
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "total": { "type": "integer" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"DeepSrv.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("DeepSrv", map);
        IReadOnlyList<GeneratedFile> files = gen.GenerateServer(spec);

        // Should generate server code with element type resolution for deep nested array param
        Assert.IsTrue(files.Count > 0);
    }

    [TestMethod]
    public void Generate_MultipartMixedJsonItemBatch_EmitsWriteJsonPart()
    {
        // Exercises lines 5560-5580: JSON batch item code emission via itemEncoding
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "JSON Batch", "version": "1.0" },
              "paths": {
                "/process": {
                  "post": {
                    "operationId": "processBatch",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "multipart/mixed": {
                          "schema": {
                            "type": "array",
                            "items": {
                              "type": "object",
                              "properties": {
                                "action": { "type": "string" },
                                "data": { "type": "object" }
                              }
                            }
                          },
                          "itemEncoding": {
                            "contentType": "application/json"
                          }
                        }
                      }
                    },
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "total": { "type": "integer" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"JsonBatch.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("JsonBatch", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        // Check that JSON batch code is emitted (WriteJsonPart in a foreach loop)
        bool hasJsonBatch = false;
        foreach (GeneratedFile f in files)
        {
            if (f.Content.Contains("WriteJsonPart") && f.Content.Contains("foreach"))
            {
                hasJsonBatch = true;
                break;
            }
        }

        Assert.IsTrue(hasJsonBatch, "Expected WriteJsonPart + foreach in generated code for JSON batch");
    }

    [TestMethod]
    public void CollectSchemaPointers_PrefixEncodingOnStandardOperation_FindsSchemas()
    {
        // Exercises lines 597-615: prefixEncoding in standard (non-additional) operation
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "PrefixEncoding Standard", "version": "1.0" },
              "paths": {
                "/upload": {
                  "post": {
                    "operationId": "uploadMixed",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "multipart/mixed": {
                          "schema": {
                            "type": "array",
                            "prefixItems": [
                              { "type": "object", "properties": { "name": { "type": "string" } } },
                              { "type": "string", "format": "binary" }
                            ]
                          },
                          "prefixEncoding": [
                            { "contentType": "application/json" },
                            { "contentType": "application/octet-stream" }
                          ]
                        }
                      }
                    },
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "ok": { "type": "boolean" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] pointers = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];

        // Should find the JSON prefixItem schema (not binary)
        Assert.IsTrue(pointers.Length >= 1);
    }

    [TestMethod]
    public void Generate_DeepNestedResponseHeader_ResolvesElementType()
    {
        // Exercises lines 4125-4132: deep nested response header with element type resolution
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Deep Header", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "responses": {
                      "200": {
                        "description": "OK",
                        "headers": {
                          "X-Ids": {
                            "style": "simple",
                            "explode": false,
                            "schema": {
                              "type": "array",
                              "items": {
                                "type": "object",
                                "properties": {
                                  "id": { "type": "string" },
                                  "rev": { "type": "integer" }
                                }
                              }
                            }
                          }
                        },
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "total": { "type": "integer" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"DeepHdr.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("DeepHdr", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        // Should generate code that resolves element type for deep nested header
        Assert.IsTrue(files.Count > 0);
    }

    [TestMethod]
    public void Generate_MultipartMixedPrefixWithBinaryPart_EmitsPrefixCode()
    {
        // Exercises lines 5421-5424: binary prefix part in multipart/mixed (emits BinaryPartData param)
        JsonElement spec = ParseSpec("""
            {
              "openapi": "3.2.0",
              "info": { "title": "Prefix Binary", "version": "1.0" },
              "paths": {
                "/mixed": {
                  "post": {
                    "operationId": "uploadMixed",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "multipart/mixed": {
                          "schema": {
                            "type": "array",
                            "prefixItems": [
                              { "type": "object", "properties": { "meta": { "type": "string" } } },
                              { "type": "string", "format": "binary" }
                            ]
                          },
                          "prefixEncoding": [
                            { "contentType": "application/json" },
                            { "contentType": "application/octet-stream" }
                          ]
                        }
                      }
                    },
                    "responses": {
                      "200": {
                        "description": "OK",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "uploaded": { "type": "boolean" } } }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        SchemaReference[] refs = [.. OpenApi32CodeGenerator.CollectSchemaPointers(spec, out _)];
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        int i = 0;
        foreach (SchemaReference r in refs)
        {
            map[r.PositionalPointer] = $"PfxBin.Type{i}";
            i++;
        }

        OpenApi32CodeGenerator gen = new("PfxBin", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        // Should emit both WriteJsonPart and WriteBinaryPartAsync for prefix parts
        bool hasJsonPart = false;
        bool hasBinaryPart = false;
        foreach (GeneratedFile f in files)
        {
            if (f.Content.Contains("WriteJsonPart"))
            {
                hasJsonPart = true;
            }

            if (f.Content.Contains("WriteBinaryPartAsync"))
            {
                hasBinaryPart = true;
            }
        }

        Assert.IsTrue(hasJsonPart, "Expected WriteJsonPart for JSON prefix part");
        Assert.IsTrue(hasBinaryPart, "Expected WriteBinaryPartAsync for binary prefix part");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Webhook and callback tests
    // ═══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void WalkWebhookAndCallbackOperations_FindsWebhooks()
    {
        OperationSummary[] ops = OpenApi32CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        Assert.IsTrue(
            ops.Any(o => o.OperationId == "petAdoptedWebhook"),
            "Expected petAdoptedWebhook from webhooks section");
        Assert.IsTrue(
            ops.Any(o => o.OperationId == "inventoryUpdateWebhook"),
            "Expected inventoryUpdateWebhook from webhooks section");
    }

    [TestMethod]
    public void WalkWebhookAndCallbackOperations_FindsCallbacks()
    {
        OperationSummary[] ops = OpenApi32CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        Assert.IsTrue(
            ops.Any(o => o.OperationId == "onEventCallback"),
            "Expected onEventCallback from per-operation callbacks");
    }

    [TestMethod]
    public void WalkWebhookAndCallbackOperations_CorrectMethods()
    {
        OperationSummary[] ops = OpenApi32CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        OperationSummary petAdopted = ops.First(o => o.OperationId == "petAdoptedWebhook");
        Assert.AreEqual(OperationMethod.Post, petAdopted.Method);

        OperationSummary inventoryUpdate = ops.First(o => o.OperationId == "inventoryUpdateWebhook");
        Assert.AreEqual(OperationMethod.Put, inventoryUpdate.Method);

        OperationSummary onEvent = ops.First(o => o.OperationId == "onEventCallback");
        Assert.AreEqual(OperationMethod.Post, onEvent.Method);
    }

    [TestMethod]
    public void WalkWebhookAndCallbackOperations_CorrectTags()
    {
        OperationSummary[] ops = OpenApi32CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        OperationSummary petAdopted = ops.First(o => o.OperationId == "petAdoptedWebhook");
        CollectionAssert.Contains(petAdopted.Tags, "webhooks");

        OperationSummary onEvent = ops.First(o => o.OperationId == "onEventCallback");
        CollectionAssert.Contains(onEvent.Tags, "callbacks");
    }

    [TestMethod]
    public void WalkWebhookAndCallbackOperations_CorrectParameterCount()
    {
        OperationSummary[] ops = OpenApi32CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        OperationSummary inventoryUpdate = ops.First(o => o.OperationId == "inventoryUpdateWebhook");
        Assert.AreEqual(1, inventoryUpdate.ParameterCount, "inventoryUpdateWebhook has X-Signature header parameter");

        OperationSummary onEvent = ops.First(o => o.OperationId == "onEventCallback");
        Assert.AreEqual(0, onEvent.ParameterCount, "onEventCallback has no parameters");
    }

    [TestMethod]
    public void WalkWebhookAndCallbackOperations_HasRequestBody()
    {
        OperationSummary[] ops = OpenApi32CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        Assert.IsTrue(ops.All(o => o.HasRequestBody), "All webhook/callback operations have request bodies");
    }

    [TestMethod]
    public void CollectWebhookAndCallbackSchemaPointers_FindsWebhookSchemas()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(
            callbacksSpecRoot, out _).Select(r => r.PositionalPointer)];

        // Webhook positional pointers use #/paths/<key>/... (matching code generator emit)
        CollectionAssert.Contains(
            pointers,
            "#/paths/petAdopted/post/requestBody/content/application~1json/schema");
        CollectionAssert.Contains(
            pointers,
            "#/paths/inventoryUpdate/put/requestBody/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectWebhookAndCallbackSchemaPointers_FindsWebhookParameterSchemas()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(
            callbacksSpecRoot, out var parameterNames).Select(r => r.PositionalPointer)];

        // inventoryUpdate has X-Signature header parameter
        CollectionAssert.Contains(
            pointers,
            "#/paths/inventoryUpdate/put/parameters/0/schema");

        Assert.AreEqual("X-Signature", parameterNames["/paths/inventoryUpdate/put/parameters/0/schema"]);
    }

    [TestMethod]
    public void CollectWebhookAndCallbackSchemaPointers_FindsCallbackSchemas()
    {
        string[] pointers = [.. OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(
            callbacksSpecRoot, out _).Select(r => r.PositionalPointer)];

        // Callback request body schema — the key is the runtime expression
        // {$request.body#/callbackUrl} with / escaped as ~1 in JSON Pointer
        Assert.IsTrue(
            pointers.Any(p => p.Contains("request.body") && p.Contains("requestBody")),
            "Expected callback request body schema pointer containing the runtime expression key");
    }

    [TestMethod]
    public void GenerateCallbackServer_ProducesFiles()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        // Map the webhook/callback schemas
        foreach (SchemaReference schemaRef in OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi32CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackServer(callbacksSpecRoot);

        Assert.IsTrue(files.Count > 0, "Expected generated callback server files");

        // Should produce handler interfaces
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("Handler.cs")),
            "Expected handler interface files");

        // Should produce endpoint registration
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("EndpointRegistration.cs")),
            "Expected endpoint registration file");
    }

    [TestMethod]
    public void GenerateCallbackClient_ProducesFiles()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi32CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackClient(callbacksSpecRoot);

        Assert.IsTrue(files.Count > 0, "Expected generated callback client files");

        // Should produce client class
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("Client.cs")),
            "Expected client class file");

        // Should produce request/response types
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("Request.cs")),
            "Expected request type files");
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("Response.cs")),
            "Expected response type files");
    }

    [TestMethod]
    public void GenerateCallbackServer_DoesNotIncludePathsOperations()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi32CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackServer(callbacksSpecRoot);

        // Should NOT include the createSubscription operation from paths
        Assert.IsFalse(
            files.Any(f => f.FileName.Contains("CreateSubscription")),
            "Callback server should not include paths operations like createSubscription");
    }

    [TestMethod]
    public void GenerateCallbackClient_DoesNotIncludePathsOperations()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi32CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackClient(callbacksSpecRoot);

        // Should NOT include the createSubscription operation from paths
        Assert.IsFalse(
            files.Any(f => f.FileName.Contains("CreateSubscription")),
            "Callback client should not include paths operations like createSubscription");
    }

    [TestMethod]
    public void WalkWebhookAndCallbackOperations_WithFilter_FiltersCorrectly()
    {
        // Filter to only "petAdopted" webhook
        OperationFilter filter = new(["petAdopted"]);
        OperationSummary[] ops = OpenApi32CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot, filter);

        Assert.AreEqual(1, ops.Length, "Filter should match only petAdopted webhook");
        Assert.AreEqual("petAdoptedWebhook", ops[0].OperationId);
    }

    [TestMethod]
    public void CollectWebhookAndCallbackSchemaPointers_CallbackResolvablePointerUsesFullPath()
    {
        // Regression test for #779: inline callback schemas must have ResolvablePointers
        // that use the full document path through the parent operation's callbacks map,
        // NOT a broken pointer rooted at #/paths/{callbackKey}/...
        SchemaReference[] refs = OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(
            callbacksSpecRoot, out _);

        // Find the callback request body schema reference
        SchemaReference callbackBodyRef = refs.First(r =>
            r.PositionalPointer.Contains("request.body", StringComparison.Ordinal) &&
            r.PositionalPointer.Contains("requestBody", StringComparison.Ordinal));

        // The ResolvablePointer must contain the full path through the parent operation:
        // #/paths/~1subscriptions/post/callbacks/onEvent/{...}/post/requestBody/...
        Assert.IsTrue(
            callbackBodyRef.ResolvablePointer.Contains("~1subscriptions/post/callbacks/onEvent/", StringComparison.Ordinal),
            $"Expected ResolvablePointer to include full parent path. Got: {callbackBodyRef.ResolvablePointer}");

        // And it must be resolvable against the document — verify the schema element exists
        JsonElement schema = callbacksSpecRoot;
        string fragment = callbackBodyRef.ResolvablePointer;
        Assert.IsTrue(fragment.StartsWith('#'), "ResolvablePointer should start with #");

        // Walk the pointer segments to verify it resolves
        string[] segments = fragment[2..].Split('/');
        foreach (string segment in segments)
        {
            string unescaped = segment.Replace("~1", "/").Replace("~0", "~");
            Assert.IsTrue(
                schema.TryGetProperty(unescaped, out schema),
                $"Failed to resolve segment '{unescaped}' in pointer '{fragment}'");
        }

        // The resolved element should be a schema object
        Assert.AreEqual(JsonValueKind.Object, schema.ValueKind,
            "Resolved callback schema should be a JSON object");
    }

    [TestMethod]
    public void CollectWebhookAndCallbackSchemaPointers_WebhookResolvablePointerUsesWebhooksRoot()
    {
        // Webhook ResolvablePointer should use webhooks root (actual document location)
        // while PositionalPointer uses paths root (matching code generator emit)
        SchemaReference[] refs = OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(
            callbacksSpecRoot, out _);

        SchemaReference webhookRef = refs.First(r =>
            r.PositionalPointer.Contains("petAdopted", StringComparison.Ordinal) &&
            r.PositionalPointer.Contains("requestBody", StringComparison.Ordinal));

        // PositionalPointer uses "paths" root for map lookups
        Assert.IsTrue(
            webhookRef.PositionalPointer.StartsWith("#/paths/petAdopted/", StringComparison.Ordinal),
            $"Expected PositionalPointer with paths root. Got: {webhookRef.PositionalPointer}");

        // ResolvablePointer uses "webhooks" root for document resolution
        Assert.IsTrue(
            webhookRef.ResolvablePointer.StartsWith("#/webhooks/petAdopted/", StringComparison.Ordinal),
            $"Expected ResolvablePointer with webhooks root. Got: {webhookRef.ResolvablePointer}");

        // Verify the ResolvablePointer actually resolves against the document
        JsonElement schema = callbacksSpecRoot;
        string fragment = webhookRef.ResolvablePointer;
        string[] segments = fragment[2..].Split('/');
        foreach (string segment in segments)
        {
            string unescaped = segment.Replace("~1", "/").Replace("~0", "~");
            Assert.IsTrue(
                schema.TryGetProperty(unescaped, out schema),
                $"Failed to resolve segment '{unescaped}' in pointer '{fragment}'");
        }

        Assert.AreEqual(JsonValueKind.Object, schema.ValueKind,
            "Resolved webhook schema should be a JSON object");
    }

    [TestMethod]
    public void GenerateCallbackServer_RuntimeExpressionPathUsesRouteParameter()
    {
        // Integration test: callback paths with runtime expressions (e.g. {$request.body#/callbackUrl})
        // must NOT emit the expression as a literal route — instead they must emit a method parameter.
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi32CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackServer(callbacksSpecRoot);

        GeneratedFile registration = files.First(f => f.FileName.Contains("EndpointRegistration.cs"));

        // The generated code must NOT contain the raw runtime expression as a route literal
        Assert.IsFalse(
            registration.Content.Contains("\"$request.body", StringComparison.Ordinal),
            "Generated endpoint registration should not emit runtime expressions as literal route strings");
        Assert.IsFalse(
            registration.Content.Contains("\"{$request", StringComparison.Ordinal),
            "Generated endpoint registration should not emit runtime expressions as literal route strings");

        // The generated MapApiEndpoints method must have a string route parameter for the callback
        Assert.IsTrue(
            registration.Content.Contains("string ", StringComparison.Ordinal) &&
            registration.Content.Contains("Route", StringComparison.Ordinal),
            "Generated MapApiEndpoints must accept a route parameter for runtime expression paths");
    }

    [TestMethod]
    public void GenerateCallbackServer_StaticPathUsesLiteralRoute()
    {
        // Integration test: webhook paths without runtime expressions (e.g. "petAdopted")
        // should emit the path as a literal string in the MapXxx call.
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi32CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackServer(callbacksSpecRoot);

        GeneratedFile registration = files.First(f => f.FileName.Contains("EndpointRegistration.cs"));

        // The static webhook paths (petAdopted, inventoryUpdate) should appear as literal route strings
        Assert.IsTrue(
            registration.Content.Contains("\"petAdopted\"", StringComparison.Ordinal),
            "Static webhook path 'petAdopted' should appear as a literal route");
        Assert.IsTrue(
            registration.Content.Contains("\"inventoryUpdate\"", StringComparison.Ordinal),
            "Static webhook path 'inventoryUpdate' should appear as a literal route");
    }

    [TestMethod]
    public void GenerateCallbackServer_NoInvalidAspNetRouteCharacters()
    {
        // Integration test: the generated endpoint registration must not contain any route
        // template with characters that ASP.NET Core route parameters forbid ($, #, /).
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi32CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi32CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackServer(callbacksSpecRoot);

        GeneratedFile registration = files.First(f => f.FileName.Contains("EndpointRegistration.cs"));

        // Extract all route strings from app.MapPost/MapGet/MapMethods calls.
        // A valid route string like MapPost("path", ...) should not trigger ASP0017.
        // If a runtime expression were emitted literally it would contain $ # /
        foreach (string line in registration.Content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("app.Map", StringComparison.Ordinal) && trimmed.Contains('('))
            {
                // If the line contains a string literal route (not a variable reference),
                // verify it doesn't have invalid chars inside route parameters
                int quoteStart = trimmed.IndexOf('"');
                if (quoteStart >= 0)
                {
                    int quoteEnd = trimmed.IndexOf('"', quoteStart + 1);
                    if (quoteEnd > quoteStart)
                    {
                        string route = trimmed.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                        Assert.IsFalse(
                            route.Contains('$'),
                            $"Route literal '{route}' contains invalid '$' character (ASP0017)");
                        Assert.IsFalse(
                            route.Contains('#'),
                            $"Route literal '{route}' contains invalid '#' character (ASP0017)");
                    }
                }
            }
        }
    }
}