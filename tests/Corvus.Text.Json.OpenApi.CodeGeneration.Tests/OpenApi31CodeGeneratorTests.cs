// <copyright file="OpenApi31CodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi31;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class OpenApi31CodeGeneratorTests
{
    private static JsonElement petstoreRoot;

    // Complete schema-type map for the petstore spec
    private static readonly Dictionary<string, string> PetstoreSchemaTypeMap = new(StringComparer.Ordinal)
    {
        // Parameter schemas
        ["#/paths/~1pets/get/parameters/0/schema"] = "Petstore.Client.JsonInt32",
        ["#/paths/~1pets~1{petId}/get/parameters/0/schema"] = "Petstore.Client.JsonString",

        // Request body schemas
        ["#/paths/~1pets/post/requestBody/content/application~1json/schema"] = "Petstore.Client.NewPet",

        // Response body schemas
        ["#/paths/~1pets/get/responses/200/content/application~1json/schema"] = "Petstore.Client.Pets",
        ["#/paths/~1pets/get/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",
        ["#/paths/~1pets/post/responses/201/content/application~1json/schema"] = "Petstore.Client.Pet",
        ["#/paths/~1pets/post/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",
        ["#/paths/~1pets~1{petId}/get/responses/200/content/application~1json/schema"] = "Petstore.Client.Pet",
        ["#/paths/~1pets~1{petId}/get/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",

        // Response header schemas
        ["#/paths/~1pets/get/responses/200/headers/x-next/schema"] = "Petstore.Client.JsonString",
    };

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "petstore-3.1.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        petstoreRoot = doc.RootElement.Clone();
    }

    private static OpenApi31CodeGenerator CreateGenerator(
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
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(petstoreRoot);

        CollectionAssert.Contains(pointers, "#/paths/~1pets/get/parameters/0/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1pets~1{petId}/get/parameters/0/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsRequestBodySchemas()
    {
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(petstoreRoot);

        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/post/requestBody/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsResponseSchemas()
    {
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(petstoreRoot);

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
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(petstoreRoot);

        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/get/responses/200/headers/x-next/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_WithFilter_OnlyIncludesMatchingPaths()
    {
        OperationFilter filter = new(["/pets"]);
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(petstoreRoot, filter);

        // /pets operations only — no /pets/{petId}
        Assert.IsTrue(pointers.Any(p => p.StartsWith("#/paths/~1pets/", StringComparison.Ordinal)));
        Assert.IsFalse(pointers.Any(p => p.Contains("~1{petId}", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CollectSchemaPointers_EmptyPaths_ReturnsEmpty()
    {
        JsonElement emptySpec = ParseSpec("""
            {
              "openapi": "3.1.0",
              "info": { "title": "Empty", "version": "1.0" }
            }
            """);

        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(emptySpec);

        Assert.AreEqual(0, pointers.Length);
    }

    [TestMethod]
    public void Generate_ProducesCorrectFileCount()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        // 3 operations × 2 (request + response) + 1 interface + 1 implementation = 8
        Assert.AreEqual(8, files.Count);
    }

    [TestMethod]
    public void Generate_ProducesRequestFiles()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        Assert.IsTrue(files.Any(f => f.FileName == "ListPetsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "CreatePetRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ShowPetByIdRequest.cs"));
    }

    [TestMethod]
    public void Generate_ProducesResponseFiles()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        Assert.IsTrue(files.Any(f => f.FileName == "ListPetsResponse.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "CreatePetResponse.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ShowPetByIdResponse.cs"));
    }

    [TestMethod]
    public void Generate_ProducesInterfaceFile()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        Assert.IsTrue(files.Any(f => f.FileName == "IApiPetsClient.cs"));
    }

    [TestMethod]
    public void Generate_ProducesImplementationFile()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        Assert.IsTrue(files.Any(f => f.FileName == "ApiPetsClient.cs"));
    }

    [TestMethod]
    public void Generate_AllFilesContainNamespace()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        foreach (GeneratedFile file in files)
        {
            Assert.IsTrue(
                file.Content.Contains("namespace Petstore.Client;", StringComparison.Ordinal),
                $"File {file.FileName} missing namespace");
        }
    }

    [TestMethod]
    public void Generate_AllFilesContainAutoGeneratedHeader()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        foreach (GeneratedFile file in files)
        {
            Assert.IsTrue(
                file.Content.Contains("<auto-generated>", StringComparison.Ordinal),
                $"File {file.FileName} missing auto-generated header");
        }
    }

    [TestMethod]
    public void Generate_InterfaceContainsAllOperationMethods()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(iface.Content.Contains("ListPetsAsync", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("CreatePetAsync", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("ShowPetByIdAsync", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_InterfaceMethodsReturnTypedResponses()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("ValueTask<ListPetsResponse>", StringComparison.Ordinal));
        Assert.IsTrue(
            iface.Content.Contains("ValueTask<CreatePetResponse>", StringComparison.Ordinal));
        Assert.IsTrue(
            iface.Content.Contains("ValueTask<ShowPetByIdResponse>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_InterfaceExtendsIAsyncDisposable()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("IAsyncDisposable", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_InterfaceUsesSourceTypeForOptionalParam()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains(
                "Petstore.Client.JsonInt32.Source limit = default",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_InterfaceUsesSourceTypeForRequiredParam()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains(
                "Petstore.Client.JsonString.Source petId",
                StringComparison.Ordinal));

        // Required param should NOT have a default value
        Assert.IsFalse(
            iface.Content.Contains(
                "Petstore.Client.JsonString.Source petId =",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_InterfaceUsesSourceTypeForBody()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("Petstore.Client.NewPet.Source body", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_InterfaceHasXmlDocs()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("List all pets", StringComparison.Ordinal));
        Assert.IsTrue(
            iface.Content.Contains("Create a pet", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ImplementationContainsTransportField()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "private readonly IApiTransport transport;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ImplementationContainsConstructor()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "public ApiPetsClient(IApiTransport transport)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ImplementationImplementsInterface()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(": IApiPetsClient", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ImplementationImplementsDisposeAsync()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "public ValueTask DisposeAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsUsesNoBodySendAsync()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "SendAsyncCore<ListPetsRequest, ListPetsResponse>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CreatePetUsesTypedBodySendAsync()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("Petstore.Client.NewPet.Source body", StringComparison.Ordinal));
        Assert.IsTrue(
            impl.Content.Contains(
                "SendWithBodyAsyncCore<CreatePetRequest, Petstore.Client.NewPet, CreatePetResponse>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_MethodCreatesWorkspaceForParams()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("JsonWorkspace.CreateUnrented()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_MethodDisposesWorkspace()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("workspace.Dispose()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequiredParamMaterialisedViaCreateBuilder()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "Petstore.Client.JsonString.CreateBuilder(workspace, petId).RootElement",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_OptionalParamGuardedWithIsUndefined()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("limit.IsUndefined", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_BodyMaterialisedViaCreateBuilder()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "Petstore.Client.NewPet.CreateBuilder(workspace, body).RootElement",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequestImplementsIApiRequest()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                ": IApiRequest<ListPetsRequest>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsRequestHasPathTemplate()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "PathTemplateUtf8 => \"/pets\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsRequestHasGetMethod()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "Method => OperationMethod.Get", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsRequestHasQueryParameterFlags()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("HasQueryParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("HasPathParameters => false", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsRequestHasTypedLimitField()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "Petstore.Client.JsonInt32? Limit { get; init; }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsRequestWriteQueryStringEmitsLimit()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("stackalloc byte[11]", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains(".TryFormat(buf", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("\"limit=\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsRequestHasWriteHeaders()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("WriteHeaders", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ShowPetByIdRequestHasPathParameterFlags()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("HasPathParameters => true", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ShowPetByIdRequestHasTypedPathParameter()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "Petstore.Client.JsonString PetId { get; init; }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ShowPetByIdRequestPathTemplateIncludesParameter()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "PathTemplateUtf8 => \"/pets/{petId}\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ShowPetByIdRequestWriteResolvedPathEmitsSegments()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("WriteResolvedPath", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("\"/pets/\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ShowPetByIdRequestPathWritesUriEscapedString()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("GetUtf8String()", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("Utf8Uri.TryEscapeDataString(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CreatePetRequestHasPostMethod()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "CreatePetRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "Method => OperationMethod.Post", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseImplementsIApiResponse()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                ": IApiResponse<ListPetsResponse>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseHasStatusCodeProperty()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("StatusCode { get;", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("IsSuccess =>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseHasCreateAsyncFactory()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "static async ValueTask<ListPetsResponse> CreateAsync",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseHasDisposeAsync()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "public async ValueTask DisposeAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseUsesTypedBody()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("Petstore.Client.Pets OkBody", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("TryGetOk", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseHasDefaultErrorAccessor()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("Petstore.Client.Error DefaultBody", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("TryGetDefault", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseHasResponseHeaders()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("XNext", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_WithCustomClientNamePrefix()
    {
        OpenApi31CodeGenerator gen = CreateGenerator(clientNamePrefix: "Petstore");
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        Assert.IsTrue(files.Any(f => f.FileName == "IPetstorePetsClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "PetstorePetsClient.cs"));
    }

    [TestMethod]
    public void Generate_WithFilter_OnlyIncludesMatchingPaths()
    {
        OpenApi31CodeGenerator gen = CreateGenerator();
        OperationFilter filter = new(["/pets"]);
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot, filter);

        // /pets has GET + POST = 2 operations × 2 files = 4 + 1 interface + 1 impl = 6
        Assert.AreEqual(6, files.Count);
        Assert.IsTrue(files.Any(f => f.FileName == "ListPetsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "CreatePetRequest.cs"));
        Assert.IsFalse(files.Any(f => f.FileName == "ShowPetByIdRequest.cs"));
    }

    [TestMethod]
    public void Generate_UnmappedPointerFallsBackToJsonElement()
    {
        Dictionary<string, string> emptyMap = new(StringComparer.Ordinal);
        OpenApi31CodeGenerator gen = new("Petstore.Client", emptyMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("JsonElement.Source body", StringComparison.Ordinal));
    }

    private const string MultiStatusSpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Multi", "version": "1.0" },
          "paths": {
            "/items": {
              "post": {
                "operationId": "createItem",
                "responses": {
                  "200": {
                    "description": "Ok",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  },
                  "201": {
                    "description": "Created",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  },
                  "default": {
                    "description": "Error",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
          }
        }
        """;

    private const string DefaultOnlySpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "DefaultOnly", "version": "1.0" },
          "paths": {
            "/items": {
              "get": {
                "operationId": "getItem",
                "responses": {
                  "default": {
                    "description": "Any response",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
          }
        }
        """;

    private const string NoDefaultSpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "NoDefault", "version": "1.0" },
          "paths": {
            "/items/{id}": {
              "get": {
                "operationId": "getItemById",
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": {
                    "description": "Found",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  },
                  "404": {
                    "description": "Not found",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
          }
        }
        """;

    [TestMethod]
    public void Generate_TryGetDefault_MultipleNonDefault_ChecksAllSpecificCodes()
    {
        JsonElement spec = ParseSpec(MultiStatusSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/post/responses/200/content/application~1json/schema"] = "Test.OkBody",
            ["#/paths/~1items/post/responses/201/content/application~1json/schema"] = "Test.CreatedBody",
            ["#/paths/~1items/post/responses/default/content/application~1json/schema"] = "Test.ErrorBody",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "this.StatusCode != 200 && this.StatusCode != 201",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_TryGetDefault_DefaultOnly_AlwaysReturnsTrue()
    {
        JsonElement spec = ParseSpec(DefaultOnlySpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/responses/default/content/application~1json/schema"] = "Test.AnyBody",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("result = this.DefaultBody;", StringComparison.Ordinal));
        Assert.IsFalse(
            resp.Content.Contains("this.StatusCode !=", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_TryGetDefault_NoDefault_NotEmitted()
    {
        JsonElement spec = ParseSpec(NoDefaultSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.ItemBody",
            ["#/paths/~1items~1{id}/get/responses/404/content/application~1json/schema"] = "Test.NotFoundBody",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);
        GeneratedFile resp = GetFile(files, "GetItemByIdResponse.cs");

        Assert.IsFalse(
            resp.Content.Contains("TryGetDefault", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("TryGetOk", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("TryGetNotFound", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_MatchResult_MultipleNonDefault_HasAllHandlers()
    {
        JsonElement spec = ParseSpec(MultiStatusSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/post/responses/200/content/application~1json/schema"] = "Test.OkBody",
            ["#/paths/~1items/post/responses/201/content/application~1json/schema"] = "Test.CreatedBody",
            ["#/paths/~1items/post/responses/default/content/application~1json/schema"] = "Test.ErrorBody",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.OkBody, TResult> matchOk",
                StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.CreatedBody, TResult> matchCreated",
                StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.ErrorBody, TResult> matchDefault",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_MatchResult_MultipleNonDefault_BranchesOnStatusCode()
    {
        JsonElement spec = ParseSpec(MultiStatusSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/post/responses/200/content/application~1json/schema"] = "Test.OkBody",
            ["#/paths/~1items/post/responses/201/content/application~1json/schema"] = "Test.CreatedBody",
            ["#/paths/~1items/post/responses/default/content/application~1json/schema"] = "Test.ErrorBody",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("if (this.StatusCode == 200)", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("return matchOk(this.OkBody)", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("if (this.StatusCode == 201)", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("return matchCreated(this.CreatedBody)", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("return matchDefault(this.DefaultBody)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_MatchResult_ContextOverloadHasAllowsRefStruct()
    {
        JsonElement spec = ParseSpec(MultiStatusSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/post/responses/200/content/application~1json/schema"] = "Test.OkBody",
            ["#/paths/~1items/post/responses/201/content/application~1json/schema"] = "Test.CreatedBody",
            ["#/paths/~1items/post/responses/default/content/application~1json/schema"] = "Test.ErrorBody",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "where TContext : allows ref struct",
                StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.OkBody, TContext, TResult> matchOk",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_MatchResult_DefaultOnly_SingleHandler()
    {
        JsonElement spec = ParseSpec(DefaultOnlySpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/responses/default/content/application~1json/schema"] = "Test.AnyBody",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.AnyBody, TResult> matchDefault)",
                StringComparison.Ordinal));
        Assert.IsFalse(
            resp.Content.Contains("matchOk", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains(
                "return matchDefault(this.DefaultBody);",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_MatchResult_NoDefault_UsesStatusCodeFallback()
    {
        JsonElement spec = ParseSpec(NoDefaultSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.ItemBody",
            ["#/paths/~1items~1{id}/get/responses/404/content/application~1json/schema"] = "Test.NotFoundBody",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);
        GeneratedFile resp = GetFile(files, "GetItemByIdResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.ItemBody, TResult> matchOk",
                StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.NotFoundBody, TResult> matchNotFound",
                StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<int, TResult> matchDefault)",
                StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains(
                "return matchDefault(this.StatusCode);",
                StringComparison.Ordinal));
    }

    private const string MultiMethodSpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Multi", "version": "1.0" },
          "paths": {
            "/users": {
              "put": {
                "operationId": "updateUser",
                "tags": ["users"],
                "summary": "Update a <user> & their profile",
                "requestBody": {
                  "required": true,
                  "content": { "application/json": { "schema": { "type": "object" } } }
                },
                "responses": {
                  "200": {
                    "description": "Ok",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              },
              "delete": {
                "operationId": "deleteUser",
                "tags": ["users"],
                "responses": {
                  "204": { "description": "Deleted" }
                }
              }
            },
            "/orders": {
              "patch": {
                "operationId": "patchOrder",
                "tags": ["orders"],
                "responses": {
                  "200": {
                    "description": "Ok",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              },
              "head": {
                "operationId": "headOrder",
                "tags": ["orders"],
                "responses": {
                  "200": { "description": "Ok" }
                }
              }
            },
            "/health": {
              "options": {
                "operationId": "optionsHealth",
                "tags": ["admin"],
                "responses": {
                  "200": { "description": "Ok" }
                }
              },
              "trace": {
                "operationId": "traceHealth",
                "tags": ["admin"],
                "responses": {
                  "200": { "description": "Ok" }
                }
              }
            }
          }
        }
        """;

    private static readonly Dictionary<string, string> MultiMethodMap = new(StringComparer.Ordinal)
    {
        ["#/paths/~1users/put/requestBody/content/application~1json/schema"] = "Test.UserBody",
        ["#/paths/~1users/put/responses/200/content/application~1json/schema"] = "Test.UserResult",
        ["#/paths/~1orders/patch/responses/200/content/application~1json/schema"] = "Test.OrderResult",
    };

    [TestMethod]
    public void Generate_MultiTag_ProducesMultipleClientInterfaces()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi31CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "IApiUsersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ApiUsersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "IApiOrdersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ApiOrdersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "IApiAdminClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ApiAdminClient.cs"));
    }

    [TestMethod]
    public void Generate_PutMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi31CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "UpdateUserRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Put", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_DeleteMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi31CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "DeleteUserRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Delete", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PatchMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi31CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "PatchOrderRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Patch", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_HeadMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi31CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "HeadOrderRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Head", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_OptionsMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi31CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "OptionsHealthRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Options", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_TraceMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi31CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TraceHealthRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Trace", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_OperationWithSummary_EmitsXmlRemarks()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi31CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "UpdateUserRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "/// <remarks>Update a &lt;user&gt; &amp; their profile</remarks>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CustomClientNamePrefix_MultiTag()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi31CodeGenerator gen = new("Test", MultiMethodMap, "MyService");
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "IMyServiceUsersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "MyServiceUsersClient.cs"));
    }

    private const string NoOperationIdSpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "NoOpId", "version": "1.0" },
          "paths": {
            "/items/{itemId}/details": {
              "get": {
                "tags": ["items"],
                "responses": {
                  "200": {
                    "description": "Ok",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
          }
        }
        """;

    [TestMethod]
    public void Generate_NoOperationId_SynthesizesMethodName()
    {
        JsonElement spec = ParseSpec(NoOperationIdSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{itemId}~1details/get/responses/200/content/application~1json/schema"] = "Test.ItemDetail",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "GetItemsItemIdDetailsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "GetItemsItemIdDetailsResponse.cs"));
    }

    [TestMethod]
    public void Generate_NoOperationId_SynthesizedNameIncludesHttpMethod()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items/{id}": {
                  "delete": {
                    "tags": ["items"],
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": {
                      "204": { "description": "Deleted" }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/delete/parameters/0/schema"] = "Test.JsonString",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        Assert.IsTrue(files.Any(f => f.FileName == "DeleteItemsIdRequest.cs"));
    }

    [TestMethod]
    public void Generate_CSharpKeywordParameterNames_AreEscaped()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Keywords", "version": "1.0" },
              "paths": {
                "/items/{ref}/{string}": {
                  "get": {
                    "operationId": "search",
                    "tags": ["search"],
                    "parameters": [
                      { "name": "ref", "in": "path", "required": true, "schema": { "type": "string" } },
                      { "name": "string", "in": "path", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": {
                      "200": {
                        "description": "Ok",
                        "content": { "application/json": { "schema": { "type": "object" } } }
                      }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{ref}~1{string}/get/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{ref}~1{string}/get/parameters/1/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{ref}~1{string}/get/responses/200/content/application~1json/schema"] = "Test.Result",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "SearchRequest.cs");

        // C# keywords should be prefixed with @
        Assert.IsTrue(req.Content.Contains("@ref", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("@string", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PathLevelParameters_MergedWithOperationParameters()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "PathParams", "version": "1.0" },
              "paths": {
                "/items/{id}": {
                  "parameters": [
                    { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                  ],
                  "get": {
                    "operationId": "getItem",
                    "tags": ["items"],
                    "responses": {
                      "200": {
                        "description": "Ok",
                        "content": { "application/json": { "schema": { "type": "object" } } }
                      }
                    }
                  },
                  "delete": {
                    "operationId": "deleteItem",
                    "tags": ["items"],
                    "responses": {
                      "204": { "description": "Deleted" }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            // Path-level parameter
            ["#/paths/~1items~1{id}/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.Item",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        // Both operations should have the path-level id parameter
        GeneratedFile getReq = GetFile(files, "GetItemRequest.cs");
        Assert.IsTrue(getReq.Content.Contains("Id { get; init; }", StringComparison.Ordinal));
        Assert.IsTrue(getReq.Content.Contains("HasPathParameters => true", StringComparison.Ordinal));

        GeneratedFile deleteReq = GetFile(files, "DeleteItemRequest.cs");
        Assert.IsTrue(deleteReq.Content.Contains("Id { get; init; }", StringComparison.Ordinal));
        Assert.IsTrue(deleteReq.Content.Contains("HasPathParameters => true", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PathLevelParameters_OperationOverridesPath()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Override", "version": "1.0" },
              "paths": {
                "/items/{id}": {
                  "parameters": [
                    { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                  ],
                  "get": {
                    "operationId": "getItem",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "schema": { "type": "integer", "format": "int32" } }
                    ],
                    "responses": {
                      "200": {
                        "description": "Ok",
                        "content": { "application/json": { "schema": { "type": "object" } } }
                      }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);

        // Collect pointers — should see the operation-level parameter, not the path-level one
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(root);

        // The operation-level parameter should take precedence
        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/parameters/0/schema");

        // The path-level parameter should NOT appear (overridden by same name+location)
        Assert.IsFalse(
            pointers.Any(p => p == "#/paths/~1items~1{id}/parameters/0/schema"),
            "Path-level parameter should be overridden by operation-level parameter");
    }

    [TestMethod]
    public void CollectSchemaPointers_Petstore_ReturnsCorrectCount()
    {
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(petstoreRoot);

        // 2 param schemas + 1 request body + 6 response bodies + 1 response header = 10
        Assert.AreEqual(10, pointers.Length);
    }

    [TestMethod]
    public void CollectSchemaPointers_Petstore_AllPointersAreUnique()
    {
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(petstoreRoot);
        HashSet<string> unique = new(pointers, StringComparer.Ordinal);

        Assert.AreEqual(pointers.Length, unique.Count, "Duplicate schema pointers found");
    }

    // ── $ref resolution tests (also covers LocalReferenceResolver) ──────
    private const string RefSpec31 = """
        {
          "openapi": "3.1.0",
          "info": { "title": "RefTest", "version": "1.0" },
          "paths": {
            "/items/{id}": {
              "get": {
                "operationId": "getItem",
                "tags": ["items"],
                "parameters": [
                  { "$ref": "#/components/parameters/ItemId" }
                ],
                "requestBody": { "$ref": "#/components/requestBodies/CreateItem" },
                "responses": {
                  "200": { "$ref": "#/components/responses/ItemResponse" }
                }
              }
            }
          },
          "components": {
            "parameters": {
              "ItemId": {
                "name": "id",
                "in": "path",
                "required": true,
                "schema": { "type": "string" }
              }
            },
            "requestBodies": {
              "CreateItem": {
                "required": true,
                "content": {
                  "application/json": {
                    "schema": { "type": "object" }
                  }
                }
              }
            },
            "responses": {
              "ItemResponse": {
                "description": "Item response",
                "content": {
                  "application/json": {
                    "schema": { "type": "object" }
                  }
                },
                "headers": {
                  "X-Request-Id": { "$ref": "#/components/headers/RequestId" }
                }
              }
            },
            "headers": {
              "RequestId": {
                "schema": { "type": "string" }
              }
            }
          }
        }
        """;

    private static readonly Dictionary<string, string> RefSpecMap31 = new(StringComparer.Ordinal)
    {
        ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
        ["#/paths/~1items~1{id}/get/requestBody/content/application~1json/schema"] = "Test.ItemBody",
        ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.Item",
        ["#/paths/~1items~1{id}/get/responses/200/headers/X-Request-Id/schema"] = "Test.JsonString",
    };

    [TestMethod]
    public void CollectSchemaPointers_RefParameter_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec31);
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(spec);

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/parameters/0/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_RefRequestBody_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec31);
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(spec);

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/requestBody/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_RefResponse_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec31);
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(spec);

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_RefHeader_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec31);
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(spec);

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/responses/200/headers/X-Request-Id/schema");
    }

    [TestMethod]
    public void Generate_RefParameter_EmitsParameter()
    {
        JsonElement spec = ParseSpec(RefSpec31);
        OpenApi31CodeGenerator gen = new("Test", RefSpecMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "GetItemRequest.cs");
        Assert.IsTrue(req.Content.Contains("Id { get; init; }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RefRequestBody_EmitsRequestBody()
    {
        JsonElement spec = ParseSpec(RefSpec31);
        OpenApi31CodeGenerator gen = new("Test", RefSpecMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile iface = GetFile(files, "IApiItemsClient.cs");
        Assert.IsTrue(iface.Content.Contains("Test.ItemBody", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RefResponse_EmitsResponseBody()
    {
        JsonElement spec = ParseSpec(RefSpec31);
        OpenApi31CodeGenerator gen = new("Test", RefSpecMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");
        Assert.IsTrue(resp.Content.Contains("Test.Item", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RefResponseHeader_EmitsHeaderProperty()
    {
        JsonElement spec = ParseSpec(RefSpec31);
        OpenApi31CodeGenerator gen = new("Test", RefSpecMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");
        Assert.IsTrue(resp.Content.Contains("XRequestIdHeader", StringComparison.Ordinal));
    }

    // ── Schema serialization kind tests ─────────────────────────────────
    private const string SerializationKindSpec31 = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Kinds", "version": "1.0" },
          "paths": {
            "/test/{id}": {
              "get": {
                "operationId": "testKinds",
                "tags": ["test"],
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "integer", "format": "int32" } },
                  { "name": "enabled", "in": "query", "schema": { "type": "boolean" } },
                  { "name": "score", "in": "query", "schema": { "type": "number" } },
                  { "name": "rating", "in": "query", "schema": { "type": "number", "format": "float" } },
                  { "name": "precise", "in": "query", "schema": { "type": "number", "format": "double" } },
                  { "name": "big", "in": "query", "schema": { "type": "integer", "format": "int64" } },
                  { "name": "small", "in": "query", "schema": { "type": "integer", "format": "int16" } },
                  { "name": "tiny", "in": "query", "schema": { "type": "integer", "format": "byte" } },
                  { "name": "tags", "in": "query", "schema": { "type": "array", "items": { "type": "string" } } },
                  { "name": "meta", "in": "query", "schema": { "type": "object" } }
                ],
                "responses": {
                  "200": {
                    "description": "Ok",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
          }
        }
        """;

    private static readonly Dictionary<string, string> SerializationKindMap31 = new(StringComparer.Ordinal)
    {
        ["#/paths/~1test~1{id}/get/parameters/0/schema"] = "Test.JsonInt32",
        ["#/paths/~1test~1{id}/get/parameters/1/schema"] = "Test.JsonBoolean",
        ["#/paths/~1test~1{id}/get/parameters/2/schema"] = "Test.JsonNumber",
        ["#/paths/~1test~1{id}/get/parameters/3/schema"] = "Test.JsonSingle",
        ["#/paths/~1test~1{id}/get/parameters/4/schema"] = "Test.JsonDouble",
        ["#/paths/~1test~1{id}/get/parameters/5/schema"] = "Test.JsonInt64",
        ["#/paths/~1test~1{id}/get/parameters/6/schema"] = "Test.JsonInt16",
        ["#/paths/~1test~1{id}/get/parameters/7/schema"] = "Test.JsonByte",
        ["#/paths/~1test~1{id}/get/parameters/8/schema"] = "Test.JsonArray",
        ["#/paths/~1test~1{id}/get/parameters/9/schema"] = "Test.JsonObject",
        ["#/paths/~1test~1{id}/get/responses/200/content/application~1json/schema"] = "Test.Result",
    };

    [TestMethod]
    public void Generate_BooleanParam_EmitsBooleanWrite()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec31);
        OpenApi31CodeGenerator gen = new("Test", SerializationKindMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");

        // Boolean query param emits bv variable for ternary
        Assert.IsTrue(req.Content.Contains("\"true\"u8 : \"false\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_NumberParam_EmitsUnboundedNumber()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec31);
        OpenApi31CodeGenerator gen = new("Test", SerializationKindMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");

        // Unbounded number emits GetRawUtf8Value
        Assert.IsTrue(req.Content.Contains("JsonMarshal.GetRawUtf8Value", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_FloatParam_EmitsTryFormat()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec31);
        OpenApi31CodeGenerator gen = new("Test", SerializationKindMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");

        // Float/single format with stackalloc 32
        Assert.IsTrue(req.Content.Contains("stackalloc byte[32]", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("TryFormat", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ArrayParam_EmitsJsonWriterFallback()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec31);
        OpenApi31CodeGenerator gen = new("Test", SerializationKindMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");

        // Array/Object emits Utf8JsonWriter fallback
        Assert.IsTrue(req.Content.Contains("Utf8JsonWriter", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_Int64Param_EmitsCorrectBufferSize()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec31);
        OpenApi31CodeGenerator gen = new("Test", SerializationKindMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");

        // int64 buffer size is 20
        Assert.IsTrue(req.Content.Contains("stackalloc byte[20]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_Int16Param_EmitsCorrectBufferSize()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec31);
        OpenApi31CodeGenerator gen = new("Test", SerializationKindMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");

        // int16 buffer size is 6
        Assert.IsTrue(req.Content.Contains("stackalloc byte[6]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ByteParam_EmitsCorrectBufferSize()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec31);
        OpenApi31CodeGenerator gen = new("Test", SerializationKindMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");

        // byte buffer size is 3
        Assert.IsTrue(req.Content.Contains("stackalloc byte[3]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_Int32PathParam_EmitsTryFormat()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec31);
        OpenApi31CodeGenerator gen = new("Test", SerializationKindMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");

        // int32 buffer size is 11
        Assert.IsTrue(req.Content.Contains("stackalloc byte[11]", StringComparison.Ordinal));
    }

    // ── Required parameter tests ────────────────────────────────────────
    private const string RequiredParamsSpec31 = """
        {
          "openapi": "3.1.0",
          "info": { "title": "RequiredParams", "version": "1.0" },
          "paths": {
            "/items": {
              "get": {
                "operationId": "searchItems",
                "tags": ["items"],
                "parameters": [
                  { "name": "q", "in": "query", "required": true, "schema": { "type": "string" } },
                  { "name": "X-Api-Key", "in": "header", "required": true, "schema": { "type": "string" } },
                  { "name": "session", "in": "cookie", "required": true, "schema": { "type": "string" } },
                  { "name": "pref", "in": "cookie", "schema": { "type": "string" } },
                  { "name": "page", "in": "query", "schema": { "type": "integer", "format": "int32" } },
                  { "name": "X-Trace", "in": "header", "schema": { "type": "string" } }
                ],
                "responses": {
                  "200": {
                    "description": "Ok",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
          }
        }
        """;

    private static readonly Dictionary<string, string> RequiredParamsMap31 = new(StringComparer.Ordinal)
    {
        ["#/paths/~1items/get/parameters/0/schema"] = "Test.JsonString",
        ["#/paths/~1items/get/parameters/1/schema"] = "Test.JsonString",
        ["#/paths/~1items/get/parameters/2/schema"] = "Test.JsonString",
        ["#/paths/~1items/get/parameters/3/schema"] = "Test.JsonString",
        ["#/paths/~1items/get/parameters/4/schema"] = "Test.JsonInt32",
        ["#/paths/~1items/get/parameters/5/schema"] = "Test.JsonString",
        ["#/paths/~1items/get/responses/200/content/application~1json/schema"] = "Test.Result",
    };

    [TestMethod]
    public void Generate_RequiredQueryParam_EmitsDirectWrite()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec31);
        OpenApi31CodeGenerator gen = new("Test", RequiredParamsMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");

        // Required query param emits direct write (no nullable "is { }" check)
        Assert.IsTrue(req.Content.Contains("HasQueryParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"q=\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequiredHeaderParam_EmitsDirectWrite()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec31);
        OpenApi31CodeGenerator gen = new("Test", RequiredParamsMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("HasHeaderParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"X-Api-Key\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequiredCookieParam_EmitsDirectWrite()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec31);
        OpenApi31CodeGenerator gen = new("Test", RequiredParamsMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("HasCookieParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"session=\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_OptionalCookieParam_EmitsNullableCheck()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec31);
        OpenApi31CodeGenerator gen = new("Test", RequiredParamsMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");

        // Optional cookie: nullable type and "is { }" check
        Assert.IsTrue(req.Content.Contains("Test.JsonString? Pref { get; init; }", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("this.Pref is { } PrefValue", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_OptionalHeaderParam_EmitsNullableCheck()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec31);
        OpenApi31CodeGenerator gen = new("Test", RequiredParamsMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("Test.JsonString? XTrace { get; init; }", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("this.XTrace is { } XTraceHeaderValue", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequiredParams_EmitsConstructorWithRequiredOnly()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec31);
        OpenApi31CodeGenerator gen = new("Test", RequiredParamsMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");

        // Constructor has required params q, X-Api-Key, session
        Assert.IsTrue(req.Content.Contains("public SearchItemsRequest(", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("Test.JsonString q,", StringComparison.Ordinal));
    }

    // ── Synthesized method names for all HTTP methods ────────────────────
    private const string AllMethodsSynthesizedSpec31 = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Synth", "version": "1.0" },
          "paths": {
            "/resources": {
              "get": {
                "tags": ["res"],
                "responses": { "200": { "description": "Ok" } }
              },
              "put": {
                "tags": ["res"],
                "responses": { "200": { "description": "Ok" } }
              },
              "post": {
                "tags": ["res"],
                "responses": { "201": { "description": "Created" } }
              },
              "delete": {
                "tags": ["res"],
                "responses": { "204": { "description": "Deleted" } }
              },
              "options": {
                "tags": ["res"],
                "responses": { "200": { "description": "Ok" } }
              },
              "head": {
                "tags": ["res"],
                "responses": { "200": { "description": "Ok" } }
              },
              "patch": {
                "tags": ["res"],
                "responses": { "200": { "description": "Ok" } }
              },
              "trace": {
                "tags": ["res"],
                "responses": { "200": { "description": "Ok" } }
              }
            }
          }
        }
        """;

    [TestMethod]
    public void Generate_SynthesizedName_Get()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec31);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "GetResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Put()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec31);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "PutResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Post()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec31);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "PostResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Delete()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec31);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "DeleteResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Options()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec31);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "OptionsResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Head()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec31);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "HeadResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Patch()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec31);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "PatchResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Trace()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec31);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "TraceResourcesRequest.cs"));
    }

    // ── Response header tests ───────────────────────────────────────────
    private const string ResponseHeaderSpec31 = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Headers", "version": "1.0" },
          "paths": {
            "/data": {
              "get": {
                "operationId": "getData",
                "tags": ["data"],
                "responses": {
                  "200": {
                    "description": "Ok",
                    "content": { "application/json": { "schema": { "type": "object" } } },
                    "headers": {
                      "X-Rate-Limit": { "schema": { "type": "integer", "format": "int32" } },
                      "X-Request-Id": { "schema": { "type": "string" } }
                    }
                  },
                  "429": {
                    "description": "Rate limited",
                    "headers": {
                      "X-Rate-Limit": { "schema": { "type": "integer", "format": "int32" } },
                      "Retry-After": { "schema": { "type": "integer", "format": "int32" } }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private static readonly Dictionary<string, string> ResponseHeaderMap31 = new(StringComparer.Ordinal)
    {
        ["#/paths/~1data/get/responses/200/content/application~1json/schema"] = "Test.Data",
        ["#/paths/~1data/get/responses/200/headers/X-Rate-Limit/schema"] = "Test.JsonInt32",
        ["#/paths/~1data/get/responses/200/headers/X-Request-Id/schema"] = "Test.JsonString",
        ["#/paths/~1data/get/responses/429/headers/X-Rate-Limit/schema"] = "Test.JsonInt32",
        ["#/paths/~1data/get/responses/429/headers/Retry-After/schema"] = "Test.JsonInt32",
    };

    [TestMethod]
    public void Generate_ResponseHeaders_EmitsHeaderProperties()
    {
        JsonElement spec = ParseSpec(ResponseHeaderSpec31);
        OpenApi31CodeGenerator gen = new("Test", ResponseHeaderMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");
        Assert.IsTrue(resp.Content.Contains("XRateLimitHeader", StringComparison.Ordinal));
        Assert.IsTrue(resp.Content.Contains("XRequestIdHeader", StringComparison.Ordinal));
        Assert.IsTrue(resp.Content.Contains("RetryAfterHeader", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseHeaders_DuplicateHeaderDeduped()
    {
        JsonElement spec = ParseSpec(ResponseHeaderSpec31);
        OpenApi31CodeGenerator gen = new("Test", ResponseHeaderMap31);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        // X-Rate-Limit appears in both 200 and 429 but should only generate one property
        int count = 0;
        int idx = 0;
        while ((idx = resp.Content.IndexOf("XRateLimitHeader { get;", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += "XRateLimitHeader { get;".Length;
        }

        Assert.AreEqual(1, count, "XRateLimitHeader property should appear exactly once");
    }

    [TestMethod]
    public void CollectSchemaPointers_ResponseHeaders_CollectsAll()
    {
        JsonElement spec = ParseSpec(ResponseHeaderSpec31);
        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(spec);

        CollectionAssert.Contains(pointers, "#/paths/~1data/get/responses/200/headers/X-Rate-Limit/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1data/get/responses/200/headers/X-Request-Id/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1data/get/responses/429/headers/X-Rate-Limit/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1data/get/responses/429/headers/Retry-After/schema");
    }

    // ── Edge case tests ─────────────────────────────────────────────────
    [TestMethod]
    public void Generate_NoServersArray_DefaultsToSlash()
    {
        // OpenAPI 3.1 schema defines a default servers value of [{"url": "/"}],
        // so even without a "servers" property, the generator emits the default URL.
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "NoServers", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "getItems",
                    "tags": ["items"],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile iface = GetFile(files, "IApiItemsClient.cs");
        Assert.IsTrue(iface.Content.Contains("""DefaultServerUrlUtf8 => "/"u8""", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_EmptyServersArray_NoDefaultServerUrl()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "EmptyServers", "version": "1.0" },
              "servers": [],
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "getItems",
                    "tags": ["items"],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile iface = GetFile(files, "IApiItemsClient.cs");
        Assert.IsFalse(iface.Content.Contains("DefaultServerUrlUtf8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_SchemaLessParam_DefaultsToString()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "NoSchema", "version": "1.0" },
              "paths": {
                "/search": {
                  "get": {
                    "operationId": "search",
                    "tags": ["search"],
                    "parameters": [
                      { "name": "q", "in": "query", "required": true }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "SearchRequest.cs");

        // Schema-less parameter defaults to String kind, which uses GetUtf8String
        Assert.IsTrue(req.Content.Contains("HasQueryParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("GetUtf8String", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_StaticPath_EmitsLiteralWrite()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Static", "version": "1.0" },
              "paths": {
                "/health/live": {
                  "get": {
                    "operationId": "healthCheck",
                    "tags": ["health"],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi31CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "HealthCheckRequest.cs");

        // No path params → literal path write
        Assert.IsTrue(req.Content.Contains("HasPathParameters => false", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"/health/live\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_HeaderParam_BooleanKind_EmitsCallback()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "HeaderBool", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "testHeaderBool",
                    "tags": ["test"],
                    "parameters": [
                      { "name": "X-Dry-Run", "in": "header", "required": true, "schema": { "type": "boolean" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1test/get/parameters/0/schema"] = "Test.JsonBoolean",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestHeaderBoolRequest.cs");
        Assert.IsTrue(req.Content.Contains("HasHeaderParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("callback(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CookieParam_IntKind_EmitsTryFormat()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "CookieInt", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "testCookieInt",
                    "tags": ["test"],
                    "parameters": [
                      { "name": "counter", "in": "cookie", "required": true, "schema": { "type": "integer", "format": "int32" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1test/get/parameters/0/schema"] = "Test.JsonInt32",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestCookieIntRequest.cs");
        Assert.IsTrue(req.Content.Contains("HasCookieParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("TryFormat", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PathParam_BooleanKind_EmitsTernary()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "PathBool", "version": "1.0" },
              "paths": {
                "/items/{flag}": {
                  "get": {
                    "operationId": "getByFlag",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "flag", "in": "path", "required": true, "schema": { "type": "boolean" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{flag}/get/parameters/0/schema"] = "Test.JsonBoolean",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByFlagRequest.cs");
        Assert.IsTrue(req.Content.Contains("\"true\"u8 : \"false\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PathParam_ObjectKind_EmitsJsonWriter()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "PathObj", "version": "1.0" },
              "paths": {
                "/items/{data}": {
                  "get": {
                    "operationId": "getByData",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "data", "in": "path", "required": true, "schema": { "type": "object" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{data}/get/parameters/0/schema"] = "Test.JsonObject",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByDataRequest.cs");
        Assert.IsTrue(req.Content.Contains("Utf8JsonWriter", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_QueryParam_ObjectKind_EmitsJsonWriterCounted()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "QueryObj", "version": "1.0" },
              "paths": {
                "/search": {
                  "get": {
                    "operationId": "searchObj",
                    "tags": ["search"],
                    "parameters": [
                      { "name": "filter", "in": "query", "required": true, "schema": { "type": "object" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1search/get/parameters/0/schema"] = "Test.JsonObject",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "SearchObjRequest.cs");
        Assert.IsTrue(req.Content.Contains("Utf8JsonWriter", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("BytesCommitted", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CookieParam_BooleanKind_EmitsBooleanWrite()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "CookieBool", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "testCookieBool",
                    "tags": ["test"],
                    "parameters": [
                      { "name": "opt_in", "in": "cookie", "required": true, "schema": { "type": "boolean" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1test/get/parameters/0/schema"] = "Test.JsonBoolean",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestCookieBoolRequest.cs");
        Assert.IsTrue(req.Content.Contains("HasCookieParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"true\"u8 : \"false\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CookieParam_StringKind_EmitsUtf8Write()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "CookieStr", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "testCookieStr",
                    "tags": ["test"],
                    "parameters": [
                      { "name": "token", "in": "cookie", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1test/get/parameters/0/schema"] = "Test.JsonString",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestCookieStrRequest.cs");
        Assert.IsTrue(req.Content.Contains("GetUtf8String", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"token=\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CookieParam_ObjectKind_EmitsJsonWriterCounted()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "CookieObj", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "testCookieObj",
                    "tags": ["test"],
                    "parameters": [
                      { "name": "prefs", "in": "cookie", "required": true, "schema": { "type": "object" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1test/get/parameters/0/schema"] = "Test.JsonObject",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestCookieObjRequest.cs");
        Assert.IsTrue(req.Content.Contains("Utf8JsonWriter", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("BytesCommitted", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CookieParam_NumberKind_EmitsRawUtf8()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "CookieNum", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "testCookieNum",
                    "tags": ["test"],
                    "parameters": [
                      { "name": "amount", "in": "cookie", "required": true, "schema": { "type": "number" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1test/get/parameters/0/schema"] = "Test.JsonNumber",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestCookieNumRequest.cs");
        Assert.IsTrue(req.Content.Contains("JsonMarshal.GetRawUtf8Value", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_HeaderParam_Int32Kind_EmitsTryFormat()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "HeaderInt", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "testHeaderInt",
                    "tags": ["test"],
                    "parameters": [
                      { "name": "X-Page", "in": "header", "required": true, "schema": { "type": "integer", "format": "int32" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1test/get/parameters/0/schema"] = "Test.JsonInt32",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestHeaderIntRequest.cs");
        Assert.IsTrue(req.Content.Contains("TryFormat", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("callback(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_HeaderParam_UnboundedNumber_EmitsRawUtf8()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "HeaderNum", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "testHeaderNum",
                    "tags": ["test"],
                    "parameters": [
                      { "name": "X-Amount", "in": "header", "required": true, "schema": { "type": "number" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1test/get/parameters/0/schema"] = "Test.JsonNumber",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestHeaderNumRequest.cs");
        Assert.IsTrue(req.Content.Contains("JsonMarshal.GetRawUtf8Value", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("callback(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_HeaderParam_ObjectKind_EmitsJsonWriter()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "HeaderObj", "version": "1.0" },
              "paths": {
                "/test": {
                  "get": {
                    "operationId": "testHeaderObj",
                    "tags": ["test"],
                    "parameters": [
                      { "name": "X-Meta", "in": "header", "required": true, "schema": { "type": "object" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1test/get/parameters/0/schema"] = "Test.JsonObject",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestHeaderObjRequest.cs");
        Assert.IsTrue(req.Content.Contains("Utf8JsonWriter", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PathParam_UnboundedNumber_EmitsRawUtf8()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "PathNum", "version": "1.0" },
              "paths": {
                "/items/{amount}": {
                  "get": {
                    "operationId": "getByAmount",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "amount", "in": "path", "required": true, "schema": { "type": "number" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{amount}/get/parameters/0/schema"] = "Test.JsonNumber",
        };

        OpenApi31CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByAmountRequest.cs");
        Assert.IsTrue(req.Content.Contains("JsonMarshal.GetRawUtf8Value", StringComparison.Ordinal));
    }
}