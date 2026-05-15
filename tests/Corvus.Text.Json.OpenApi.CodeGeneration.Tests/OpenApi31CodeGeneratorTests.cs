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
}