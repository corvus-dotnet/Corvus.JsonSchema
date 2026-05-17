// <copyright file="OpenApi30CodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi30;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class OpenApi30CodeGeneratorTests
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
            Path.Combine(AppContext.BaseDirectory, "TestData", "petstore-3.0.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        petstoreRoot = doc.RootElement.Clone();
    }

    private static OpenApi30CodeGenerator CreateGenerator(
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
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out var parameterNames);

        CollectionAssert.Contains(pointers, "#/paths/~1pets/get/parameters/0/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1pets~1{petId}/get/parameters/0/schema");

        // Parameter names are recorded for naming heuristics (keyed by fragment without '#')
        Assert.AreEqual("limit", parameterNames["/paths/~1pets/get/parameters/0/schema"]);
        Assert.AreEqual("petId", parameterNames["/paths/~1pets~1{petId}/get/parameters/0/schema"]);
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsRequestBodySchemas()
    {
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _);

        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/post/requestBody/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsResponseSchemas()
    {
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _);

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
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _);

        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/get/responses/200/headers/x-next/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_WithFilter_OnlyIncludesMatchingPaths()
    {
        OperationFilter filter = new(["/pets"]);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _, filter);

        // /pets operations only — no /pets/{petId}
        Assert.IsTrue(pointers.Any(p => p.StartsWith("#/paths/~1pets/", StringComparison.Ordinal)));
        Assert.IsFalse(pointers.Any(p => p.Contains("~1{petId}", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CollectSchemaPointers_EmptyPaths_ReturnsEmpty()
    {
        JsonElement emptySpec = ParseSpec("""
            {
              "openapi": "3.0.3",
              "info": { "title": "Empty", "version": "1.0" }
            }
            """);

        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(emptySpec, out var parameterNames);

        Assert.AreEqual(0, pointers.Length);
        Assert.AreEqual(0, parameterNames.Count);
    }

    [TestMethod]
    public void Generate_ProducesCorrectFileCount()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        // 3 operations × 2 (request + response) + 1 interface + 1 implementation = 8
        Assert.AreEqual(8, files.Count);
    }

    [TestMethod]
    public void Generate_ProducesRequestFiles()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        Assert.IsTrue(files.Any(f => f.FileName == "ListPetsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "CreatePetRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ShowPetByIdRequest.cs"));
    }

    [TestMethod]
    public void Generate_ProducesResponseFiles()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        Assert.IsTrue(files.Any(f => f.FileName == "ListPetsResponse.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "CreatePetResponse.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ShowPetByIdResponse.cs"));
    }

    [TestMethod]
    public void Generate_ProducesInterfaceFile()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        Assert.IsTrue(files.Any(f => f.FileName == "IApiPetsClient.cs"));
    }

    [TestMethod]
    public void Generate_ProducesImplementationFile()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        Assert.IsTrue(files.Any(f => f.FileName == "ApiPetsClient.cs"));
    }

    [TestMethod]
    public void Generate_AllFilesContainNamespace()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(iface.Content.Contains("ListPetsAsync", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("CreatePetAsync", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("ShowPetByIdAsync", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_InterfaceMethodsReturnTypedResponses()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("IAsyncDisposable", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_InterfaceUsesSourceTypeForOptionalParam()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("Petstore.Client.NewPet.Source body", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_InterfaceHasXmlDocs()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "private readonly IApiTransport transport;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ImplementationContainsConstructor()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "public ApiPetsClient(IApiTransport transport)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ImplementationImplementsInterface()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(": IApiPetsClient", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ImplementationImplementsDisposeAsync()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "public ValueTask DisposeAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsUsesNoBodySendAsync()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("JsonWorkspace.CreateUnrented()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_MethodDisposesWorkspace()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("workspace.Dispose()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequiredParamMaterialisedViaCreateBuilder()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("limit.IsUndefined", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_BodyMaterialisedViaCreateBuilder()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                ": IApiRequest<ListPetsRequest>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsRequestHasPathTemplate()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "PathTemplateUtf8 => \"/pets\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsRequestHasGetMethod()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "Method => OperationMethod.Get", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsRequestHasQueryParameterFlags()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "Petstore.Client.JsonInt32? Limit { get; init; }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ListPetsRequestWriteQueryStringEmitsLimit()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("WriteHeaders", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ShowPetByIdRequestHasPathParameterFlags()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("HasPathParameters => true", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ShowPetByIdRequestHasTypedPathParameter()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "Petstore.Client.JsonString PetId { get; init; }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ShowPetByIdRequestPathTemplateIncludesParameter()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "PathTemplateUtf8 => \"/pets/{petId}\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ShowPetByIdRequestWriteResolvedPathEmitsSegments()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile req = GetFile(files, "CreatePetRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "Method => OperationMethod.Post", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseImplementsIApiResponse()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                ": IApiResponse<ListPetsResponse>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseHasStatusCodeProperty()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "public async ValueTask DisposeAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseUsesTypedBody()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("XNext", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_WithCustomClientNamePrefix()
    {
        OpenApi30CodeGenerator gen = CreateGenerator(clientNamePrefix: "Petstore");
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        Assert.IsTrue(files.Any(f => f.FileName == "IPetstorePetsClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "PetstorePetsClient.cs"));
    }

    [TestMethod]
    public void Generate_WithFilter_OnlyIncludesMatchingPaths()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
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
        OpenApi30CodeGenerator gen = new("Petstore.Client", emptyMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("JsonElement.Source body", StringComparison.Ordinal));
    }

    private const string MultiStatusSpec = """
        {
          "openapi": "3.0.3",
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
          "openapi": "3.0.3",
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
          "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
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

        OpenApi30CodeGenerator gen = new("Test", map);
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

        OpenApi30CodeGenerator gen = new("Test", map);
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

        OpenApi30CodeGenerator gen = new("Test", map);
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

        OpenApi30CodeGenerator gen = new("Test", map);
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

        OpenApi30CodeGenerator gen = new("Test", map);
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

        OpenApi30CodeGenerator gen = new("Test", map);
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

        OpenApi30CodeGenerator gen = new("Test", map);
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
          "openapi": "3.0.3",
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
        OpenApi30CodeGenerator gen = new("Test", MultiMethodMap);
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
        OpenApi30CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "UpdateUserRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Put", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_DeleteMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi30CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "DeleteUserRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Delete", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PatchMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi30CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "PatchOrderRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Patch", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_HeadMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi30CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "HeadOrderRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Head", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_OptionsMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi30CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "OptionsHealthRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Options", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_TraceMethod_EmitsCorrectOperationMethodExpression()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi30CodeGenerator gen = new("Test", MultiMethodMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TraceHealthRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Trace", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_OperationWithSummary_EmitsXmlRemarks()
    {
        JsonElement spec = ParseSpec(MultiMethodSpec);
        OpenApi30CodeGenerator gen = new("Test", MultiMethodMap);
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
        OpenApi30CodeGenerator gen = new("Test", MultiMethodMap, "MyService");
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "IMyServiceUsersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "MyServiceUsersClient.cs"));
    }

    private const string NoOperationIdSpec = """
        {
          "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "GetItemsItemIdDetailsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "GetItemsItemIdDetailsResponse.cs"));
    }

    [TestMethod]
    public void Generate_NoOperationId_SynthesizedNameIncludesHttpMethod()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        Assert.IsTrue(files.Any(f => f.FileName == "DeleteItemsIdRequest.cs"));
    }

    [TestMethod]
    public void Generate_CSharpKeywordParameterNames_AreEscaped()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
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
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
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
              "openapi": "3.0.3",
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
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _);

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
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _);

        // 2 param schemas + 1 request body + 6 response bodies + 1 response header = 10
        Assert.AreEqual(10, pointers.Length);
    }

    [TestMethod]
    public void CollectSchemaPointers_Petstore_AllPointersAreUnique()
    {
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _);
        HashSet<string> unique = new(pointers, StringComparer.Ordinal);

        Assert.AreEqual(pointers.Length, unique.Count, "Duplicate schema pointers found");
    }

    [TestMethod]
    public void Generate_InterfaceHasDefaultServerUrl()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains(
                "\"https://petstore.example.com/v1\"u8",
                StringComparison.Ordinal));
    }

    // ── $ref resolution tests (also covers LocalReferenceResolver) ──────
    private const string RefSpec30 = """
        {
          "openapi": "3.0.3",
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

    private static readonly Dictionary<string, string> RefSpecMap30 = new(StringComparer.Ordinal)
    {
        ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
        ["#/paths/~1items~1{id}/get/requestBody/content/application~1json/schema"] = "Test.ItemBody",
        ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.Item",
        ["#/paths/~1items~1{id}/get/responses/200/headers/X-Request-Id/schema"] = "Test.JsonString",
    };

    [TestMethod]
    public void CollectSchemaPointers_RefParameter_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(spec, out _);

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/parameters/0/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_RefRequestBody_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(spec, out _);

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/requestBody/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_RefResponse_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(spec, out _);

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_RefHeader_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(spec, out _);

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/responses/200/headers/X-Request-Id/schema");
    }

    [TestMethod]
    public void Generate_RefParameter_EmitsParameter()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        OpenApi30CodeGenerator gen = new("Test", RefSpecMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "GetItemRequest.cs");
        Assert.IsTrue(req.Content.Contains("Id { get; init; }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RefRequestBody_EmitsRequestBody()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        OpenApi30CodeGenerator gen = new("Test", RefSpecMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile iface = GetFile(files, "IApiItemsClient.cs");
        Assert.IsTrue(iface.Content.Contains("Test.ItemBody", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RefResponse_EmitsResponseBody()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        OpenApi30CodeGenerator gen = new("Test", RefSpecMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");
        Assert.IsTrue(resp.Content.Contains("Test.Item", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RefResponseHeader_EmitsHeaderProperty()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        OpenApi30CodeGenerator gen = new("Test", RefSpecMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");
        Assert.IsTrue(resp.Content.Contains("XRequestIdHeader", StringComparison.Ordinal));
    }

    // ── Schema serialization kind tests ─────────────────────────────────
    private const string SerializationKindSpec30 = """
        {
          "openapi": "3.0.3",
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

    private static readonly Dictionary<string, string> SerializationKindMap30 = new(StringComparer.Ordinal)
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
        JsonElement spec = ParseSpec(SerializationKindSpec30);
        OpenApi30CodeGenerator gen = new("Test", SerializationKindMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");
        Assert.IsTrue(req.Content.Contains("\"true\"u8 : \"false\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_NumberParam_EmitsUnboundedNumber()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec30);
        OpenApi30CodeGenerator gen = new("Test", SerializationKindMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");
        Assert.IsTrue(req.Content.Contains("JsonMarshal.GetRawUtf8Value", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_FloatParam_EmitsTryFormat()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec30);
        OpenApi30CodeGenerator gen = new("Test", SerializationKindMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");
        Assert.IsTrue(req.Content.Contains("stackalloc byte[32]", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("TryFormat", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ArrayParam_EmitsEnumerateArray()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec30);
        OpenApi30CodeGenerator gen = new("Test", SerializationKindMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateArray", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_Int64Param_EmitsCorrectBufferSize()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec30);
        OpenApi30CodeGenerator gen = new("Test", SerializationKindMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");
        Assert.IsTrue(req.Content.Contains("stackalloc byte[20]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_Int16Param_EmitsCorrectBufferSize()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec30);
        OpenApi30CodeGenerator gen = new("Test", SerializationKindMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");
        Assert.IsTrue(req.Content.Contains("stackalloc byte[6]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ByteParam_EmitsCorrectBufferSize()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec30);
        OpenApi30CodeGenerator gen = new("Test", SerializationKindMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");
        Assert.IsTrue(req.Content.Contains("stackalloc byte[3]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_Int32PathParam_EmitsTryFormat()
    {
        JsonElement spec = ParseSpec(SerializationKindSpec30);
        OpenApi30CodeGenerator gen = new("Test", SerializationKindMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "TestKindsRequest.cs");
        Assert.IsTrue(req.Content.Contains("stackalloc byte[11]", StringComparison.Ordinal));
    }

    // ── Required parameter tests ────────────────────────────────────────
    private const string RequiredParamsSpec30 = """
        {
          "openapi": "3.0.3",
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

    private static readonly Dictionary<string, string> RequiredParamsMap30 = new(StringComparer.Ordinal)
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
        JsonElement spec = ParseSpec(RequiredParamsSpec30);
        OpenApi30CodeGenerator gen = new("Test", RequiredParamsMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("HasQueryParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"q=\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequiredHeaderParam_EmitsDirectWrite()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec30);
        OpenApi30CodeGenerator gen = new("Test", RequiredParamsMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("HasHeaderParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"X-Api-Key\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequiredCookieParam_EmitsDirectWrite()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec30);
        OpenApi30CodeGenerator gen = new("Test", RequiredParamsMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("HasCookieParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"session=\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_OptionalCookieParam_EmitsNullableCheck()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec30);
        OpenApi30CodeGenerator gen = new("Test", RequiredParamsMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("Test.JsonString? Pref { get; init; }", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("this.Pref is { } PrefValue", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_OptionalHeaderParam_EmitsNullableCheck()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec30);
        OpenApi30CodeGenerator gen = new("Test", RequiredParamsMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("Test.JsonString? XTrace { get; init; }", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("this.XTrace is { } XTraceHeaderValue", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequiredParams_EmitsConstructorWithRequiredOnly()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec30);
        OpenApi30CodeGenerator gen = new("Test", RequiredParamsMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("public SearchItemsRequest(", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("Test.JsonString q,", StringComparison.Ordinal));
    }

    // ── Synthesized method names for all HTTP methods ────────────────────
    private const string AllMethodsSynthesizedSpec30 = """
        {
          "openapi": "3.0.3",
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
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec30);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "GetResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Put()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec30);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "PutResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Post()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec30);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "PostResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Delete()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec30);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "DeleteResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Options()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec30);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "OptionsResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Head()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec30);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "HeadResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Patch()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec30);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "PatchResourcesRequest.cs"));
    }

    [TestMethod]
    public void Generate_SynthesizedName_Trace()
    {
        JsonElement spec = ParseSpec(AllMethodsSynthesizedSpec30);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        Assert.IsTrue(files.Any(f => f.FileName == "TraceResourcesRequest.cs"));
    }

    // ── Response header tests ───────────────────────────────────────────
    private const string ResponseHeaderSpec30 = """
        {
          "openapi": "3.0.3",
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

    private static readonly Dictionary<string, string> ResponseHeaderMap30 = new(StringComparer.Ordinal)
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
        JsonElement spec = ParseSpec(ResponseHeaderSpec30);
        OpenApi30CodeGenerator gen = new("Test", ResponseHeaderMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");
        Assert.IsTrue(resp.Content.Contains("XRateLimitHeader", StringComparison.Ordinal));
        Assert.IsTrue(resp.Content.Contains("XRequestIdHeader", StringComparison.Ordinal));
        Assert.IsTrue(resp.Content.Contains("RetryAfterHeader", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ResponseHeaders_DuplicateHeaderDeduped()
    {
        JsonElement spec = ParseSpec(ResponseHeaderSpec30);
        OpenApi30CodeGenerator gen = new("Test", ResponseHeaderMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");
        int count = 0;
        int idx = 0;
        while ((idx = resp.Content.IndexOf("XRateLimitHeader", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += "XRateLimitHeader".Length;
        }

        Assert.AreEqual(1, count, "XRateLimitHeader property should appear exactly once");
    }

    [TestMethod]
    public void CollectSchemaPointers_ResponseHeaders_CollectsAll()
    {
        JsonElement spec = ParseSpec(ResponseHeaderSpec30);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(spec, out _);

        CollectionAssert.Contains(pointers, "#/paths/~1data/get/responses/200/headers/X-Rate-Limit/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1data/get/responses/200/headers/X-Request-Id/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1data/get/responses/429/headers/X-Rate-Limit/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1data/get/responses/429/headers/Retry-After/schema");
    }

    // ── Edge case tests ─────────────────────────────────────────────────
    [TestMethod]
    public void Generate_NoServersArray_NoDefaultServerUrl()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile iface = GetFile(files, "IApiItemsClient.cs");
        Assert.IsFalse(iface.Content.Contains("DefaultServerUrlUtf8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_EmptyServersArray_NoDefaultServerUrl()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile iface = GetFile(files, "IApiItemsClient.cs");
        Assert.IsFalse(iface.Content.Contains("DefaultServerUrlUtf8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_SchemaLessParam_DefaultsToString()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "SearchRequest.cs");

        // Parameters without a schema are skipped — the request struct has no query params
        Assert.IsTrue(req.Content.Contains("HasQueryParameters => false", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_StaticPath_EmitsLiteralWrite()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "HealthCheckRequest.cs");
        Assert.IsTrue(req.Content.Contains("HasPathParameters => false", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"/health/live\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_HeaderParam_BooleanKind_EmitsCallback()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
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
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
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
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByFlagRequest.cs");
        Assert.IsTrue(req.Content.Contains("\"true\"u8 : \"false\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PathParam_ObjectKind_EmitsEnumerateObject()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByDataRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateObject", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_QueryParam_ObjectKind_EmitsEnumerateObject()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "SearchObjRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateObject", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CookieParam_BooleanKind_EmitsBooleanWrite()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
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
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestCookieStrRequest.cs");
        Assert.IsTrue(req.Content.Contains("GetUtf8String", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"token=\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CookieParam_ObjectKind_EmitsEnumerateObject()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestCookieObjRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateObject", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_CookieParam_NumberKind_EmitsRawUtf8()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestCookieNumRequest.cs");
        Assert.IsTrue(req.Content.Contains("JsonMarshal.GetRawUtf8Value", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_HeaderParam_Int32Kind_EmitsTryFormat()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
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
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestHeaderNumRequest.cs");
        Assert.IsTrue(req.Content.Contains("JsonMarshal.GetRawUtf8Value", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("callback(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_HeaderParam_ObjectKind_EmitsEnumerateObject()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "TestHeaderObjRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateObject", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PathParam_UnboundedNumber_EmitsRawUtf8()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByAmountRequest.cs");
        Assert.IsTrue(req.Content.Contains("JsonMarshal.GetRawUtf8Value", StringComparison.Ordinal));
    }

    // ── CollectSchemaPointers coverage: all HTTP methods ────────────────
    [TestMethod]
    public void CollectSchemaPointers_AllHttpMethods()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/things": {
                  "get":     { "operationId": "getThings",     "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } } },
                  "put":     { "operationId": "putThings",     "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } } },
                  "post":    { "operationId": "postThings",    "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } } },
                  "delete":  { "operationId": "deleteThings",  "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } } },
                  "options": { "operationId": "optionsThings", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } } },
                  "head":    { "operationId": "headThings",    "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } } },
                  "patch":   { "operationId": "patchThings",   "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } } },
                  "trace":   { "operationId": "traceThings",   "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } } }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null);

        Assert.AreEqual(8, pointers.Length);
        Assert.IsTrue(pointers.Any(p => p.Contains("/get/", StringComparison.Ordinal)));
        Assert.IsTrue(pointers.Any(p => p.Contains("/put/", StringComparison.Ordinal)));
        Assert.IsTrue(pointers.Any(p => p.Contains("/post/", StringComparison.Ordinal)));
        Assert.IsTrue(pointers.Any(p => p.Contains("/delete/", StringComparison.Ordinal)));
        Assert.IsTrue(pointers.Any(p => p.Contains("/options/", StringComparison.Ordinal)));
        Assert.IsTrue(pointers.Any(p => p.Contains("/head/", StringComparison.Ordinal)));
        Assert.IsTrue(pointers.Any(p => p.Contains("/patch/", StringComparison.Ordinal)));
        Assert.IsTrue(pointers.Any(p => p.Contains("/trace/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CollectSchemaPointers_NoPaths_ReturnsEmpty()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" }
            }
            """;

        JsonElement root = ParseSpec(spec);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null);
        Assert.AreEqual(0, pointers.Length);
    }

    [TestMethod]
    public void CollectSchemaPointers_ResponseHeaders()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "responses": {
                      "200": {
                        "description": "ok",
                        "content": { "application/json": { "schema": { "type": "string" } } },
                        "headers": {
                          "X-Total": { "schema": { "type": "integer", "format": "int32" } }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null);
        Assert.AreEqual(2, pointers.Length);
        Assert.IsTrue(pointers.Any(p => p.Contains("headers/X-Total", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CollectSchemaPointers_WithRefParameter()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items/{id}": {
                  "get": {
                    "operationId": "getItem",
                    "parameters": [
                      { "$ref": "#/components/parameters/ItemId" }
                    ],
                    "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } }
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
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null);
        Assert.AreEqual(2, pointers.Length);
    }

    [TestMethod]
    public void CollectSchemaPointers_WithRefResponse()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "responses": {
                      "200": { "$ref": "#/components/responses/ItemList" }
                    }
                  }
                }
              },
              "components": {
                "responses": {
                  "ItemList": {
                    "description": "OK",
                    "content": { "application/json": { "schema": { "type": "array" } } },
                    "headers": {
                      "X-Count": { "schema": { "type": "integer", "format": "int32" } }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null);
        Assert.AreEqual(2, pointers.Length);
    }

    [TestMethod]
    public void CollectSchemaPointers_WithRefRequestBody()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "post": {
                    "operationId": "createItem",
                    "requestBody": { "$ref": "#/components/requestBodies/NewItem" },
                    "responses": { "201": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } }
                  }
                }
              },
              "components": {
                "requestBodies": {
                  "NewItem": {
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null);
        Assert.AreEqual(2, pointers.Length);
    }

    // ── Generate coverage: all HTTP methods ─────────────────────────────
    [TestMethod]
    public void Generate_AllHttpMethods_ProducesCorrectMethodNames()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/things": {
                  "get":     { "operationId": "getThings",     "responses": { "200": { "description": "ok" } } },
                  "put":     { "operationId": "putThings",     "responses": { "200": { "description": "ok" } } },
                  "post":    { "operationId": "postThings",    "responses": { "201": { "description": "ok" } } },
                  "delete":  { "operationId": "deleteThings",  "responses": { "200": { "description": "ok" } } },
                  "options": { "operationId": "optionsThings", "responses": { "200": { "description": "ok" } } },
                  "head":    { "operationId": "headThings",    "responses": { "200": { "description": "ok" } } },
                  "patch":   { "operationId": "patchThings",   "responses": { "200": { "description": "ok" } } },
                  "trace":   { "operationId": "traceThings",   "responses": { "200": { "description": "ok" } } }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        Assert.IsTrue(files.Any(f => f.FileName == "GetThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "PutThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "PostThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "DeleteThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "OptionsThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "HeadThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "PatchThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "TraceThingsRequest.cs"));
    }

    // ── Generate coverage: $ref resolution ──────────────────────────────
    [TestMethod]
    public void Generate_RefParameter_ResolvesAndEmitsCorrectly()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items/{id}": {
                  "get": {
                    "operationId": "getItem",
                    "parameters": [
                      { "$ref": "#/components/parameters/ItemId" }
                    ],
                    "responses": { "200": { "description": "ok" } }
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
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetItemRequest.cs");
        Assert.IsTrue(req.Content.Contains("Test.JsonString", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("Id", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RefRequestBody_ResolvesAndEmitsCorrectly()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "post": {
                    "operationId": "createItem",
                    "requestBody": { "$ref": "#/components/requestBodies/NewItem" },
                    "responses": { "201": { "description": "created" } }
                  }
                }
              },
              "components": {
                "requestBodies": {
                  "NewItem": {
                    "required": true,
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);

        // Verify that CollectSchemaPointers finds the $ref'd requestBody schema
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null);
        string? requestBodyPointer = pointers.FirstOrDefault(
            p => p.Contains("requestBody", StringComparison.Ordinal));
        Assert.IsNotNull(requestBodyPointer, "Expected requestBody schema pointer");

        // Use the actual pointer in the map
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            [requestBodyPointer] = "Test.NewItemBody",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        // The body type name appears in the client implementation, not the request struct
        GeneratedFile impl = GetFile(files, "ApiDefaultClient.cs");
        Assert.IsTrue(
            impl.Content.Contains("Test.NewItemBody", StringComparison.Ordinal),
            "Expected body type name in client implementation");
    }

    [TestMethod]
    public void Generate_RefResponse_ResolvesAndEmitsCorrectly()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "responses": {
                      "200": { "$ref": "#/components/responses/ItemList" }
                    }
                  }
                }
              },
              "components": {
                "responses": {
                  "ItemList": {
                    "description": "OK",
                    "content": { "application/json": { "schema": { "type": "array" } } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/responses/200/content/application~1json/schema"] = "Test.Items",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile resp = GetFile(files, "ListItemsResponse.cs");
        Assert.IsTrue(resp.Content.Contains("Test.Items", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RefHeader_ResolvesAndEmitsCorrectly()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "responses": {
                      "200": {
                        "description": "ok",
                        "headers": {
                          "X-Total": { "$ref": "#/components/headers/TotalCount" }
                        }
                      }
                    }
                  }
                }
              },
              "components": {
                "headers": {
                  "TotalCount": {
                    "schema": { "type": "integer", "format": "int32" }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile resp = GetFile(files, "ListItemsResponse.cs");
        Assert.IsTrue(resp.Content.Contains("XTotalHeader", StringComparison.Ordinal));
    }

    // ── Path style coverage (matrix, label, explicit simple) ───────────
    [TestMethod]
    public void Generate_PathParameter_MatrixStyle()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items/{id}": {
                  "get": {
                    "operationId": "getItem",
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "style": "matrix", "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "GetItemRequest.cs");
        Assert.IsTrue(req.Content.Contains("Id", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PathParameter_LabelStyle()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items/{id}": {
                  "get": {
                    "operationId": "getItem",
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "style": "label", "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "GetItemRequest.cs");
        Assert.IsTrue(req.Content.Contains("Id", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PathParameter_SimpleExplicit()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items/{id}": {
                  "get": {
                    "operationId": "getItem",
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "style": "simple", "explode": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "GetItemRequest.cs");
        Assert.IsTrue(req.Content.Contains("Id", StringComparison.Ordinal));
    }

    // ── Query style coverage ────────────────────────────────────────────
    [TestMethod]
    public void Generate_QueryParameter_FormExplicit()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "q", "in": "query", "style": "form", "explode": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("Q", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_QueryParameter_SpaceDelimited()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "tags", "in": "query", "style": "spaceDelimited", "schema": { "type": "array" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("Tags", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_QueryParameter_PipeDelimited()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "ids", "in": "query", "style": "pipeDelimited", "schema": { "type": "array" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("Ids", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_QueryParameter_DeepObject()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "filter", "in": "query", "style": "deepObject", "schema": { "type": "object" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("Filter", StringComparison.Ordinal));
    }

    // ── Synthesized method names ────────────────────────────────────────
    [TestMethod]
    public void Generate_NoOperationId_SynthesizesMethodNames()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/things": {
                  "put":     { "responses": { "200": { "description": "ok" } } },
                  "delete":  { "responses": { "200": { "description": "ok" } } },
                  "options": { "responses": { "200": { "description": "ok" } } },
                  "head":    { "responses": { "200": { "description": "ok" } } },
                  "patch":   { "responses": { "200": { "description": "ok" } } },
                  "trace":   { "responses": { "200": { "description": "ok" } } }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        Assert.IsTrue(files.Any(f => f.FileName == "PutThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "DeleteThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "OptionsThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "HeadThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "PatchThingsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "TraceThingsRequest.cs"));
    }

    // ── Required params ─────────────────────────────────────────────────
    [TestMethod]
    public void Generate_RequiredQueryParam_NotNullable()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "page", "in": "query", "required": true, "schema": { "type": "integer", "format": "int32" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/parameters/0/schema"] = "Test.JsonInt32",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("Test.JsonInt32 Page", StringComparison.Ordinal));
        Assert.IsFalse(req.Content.Contains("Test.JsonInt32? Page", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequiredHeaderParam()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "X-Api-Key", "in": "header", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("WriteHeaders", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("XApiKey", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_RequiredCookieParam()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "session", "in": "cookie", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("WriteCookies", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("Session", StringComparison.Ordinal));
    }

    // ── SchemaClassifier numeric formats ────────────────────────────────
    [TestMethod]
    public void Generate_IntegerFormats_AllVariants()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "a", "in": "query", "schema": { "type": "integer", "format": "uint16" } },
                      { "name": "b", "in": "query", "schema": { "type": "integer", "format": "uint32" } },
                      { "name": "c", "in": "query", "schema": { "type": "integer", "format": "uint64" } },
                      { "name": "d", "in": "query", "schema": { "type": "integer", "format": "uint128" } },
                      { "name": "e", "in": "query", "schema": { "type": "integer", "format": "sbyte" } },
                      { "name": "f", "in": "query", "schema": { "type": "integer", "format": "int128" } },
                      { "name": "g", "in": "query", "schema": { "type": "integer", "format": "byte" } },
                      { "name": "h", "in": "query", "schema": { "type": "integer", "format": "int16" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("A", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("H", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_NumberFormats_AllVariants()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "a", "in": "query", "schema": { "type": "number", "format": "half" } },
                      { "name": "b", "in": "query", "schema": { "type": "number", "format": "single" } },
                      { "name": "c", "in": "query", "schema": { "type": "number", "format": "double" } },
                      { "name": "d", "in": "query", "schema": { "type": "number", "format": "decimal" } },
                      { "name": "e", "in": "query", "schema": { "type": "number", "format": "float" } },
                      { "name": "f", "in": "query", "schema": { "type": "number" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("A", StringComparison.Ordinal));
    }

    // ── Edge cases ──────────────────────────────────────────────────────
    [TestMethod]
    public void Generate_ParameterWithoutSchema_TreatedAsString()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "token", "in": "query", "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");

        // Even with just a minimal schema, the parameter should appear with PascalCase name
        Assert.IsTrue(req.Content.Contains("Token", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_NoResponses_ProducesEmptyResponseStruct()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems"
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile resp = GetFile(files, "ListItemsResponse.cs");
        Assert.IsTrue(resp.Content.Contains("ListItemsResponse", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_StaticPath_NoPlaceholders()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/health/check": {
                  "get": {
                    "operationId": "healthCheck",
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "HealthCheckRequest.cs");
        Assert.IsTrue(req.Content.Contains("\"/health/check\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_DuplicateHeadersAcrossResponses_DedupedInStruct()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "responses": {
                      "200": {
                        "description": "ok",
                        "headers": {
                          "X-Request-Id": { "schema": { "type": "string" } }
                        }
                      },
                      "404": {
                        "description": "not found",
                        "headers": {
                          "X-Request-Id": { "schema": { "type": "string" } }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile resp = GetFile(files, "ListItemsResponse.cs");

        int count = 0;
        int idx = 0;
        while ((idx = resp.Content.IndexOf("XRequestIdHeader", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += "XRequestIdHeader".Length;
        }

        Assert.AreEqual(1, count, "Header property should appear exactly once (deduped)");
    }

    [TestMethod]
    public void Generate_IntegerNoFormat_UnboundedNumber()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "count", "in": "query", "schema": { "type": "integer" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("GetRawUtf8Value", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_UnknownSchemaType_TreatedAsString()
    {
        // In OpenAPI 3.0, "null" is not a valid type value. Use an unrecognised
        // type extension — SchemaClassifier falls through to String.
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "x", "in": "query", "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("X", StringComparison.Ordinal));
    }

    // ── Non-JSON body content falls back to JsonElement ────────────────
    [TestMethod]
    public void Generate_NonJsonBodyContent_FallsBackToJsonElement()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/upload": {
                  "post": {
                    "operationId": "uploadFile",
                    "tags": ["files"],
                    "requestBody": {
                      "content": {
                        "text/plain": {
                          "schema": { "type": "string" }
                        }
                      }
                    },
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile client = GetFile(files, "ApiFilesClient.cs");
        Assert.IsTrue(client.Content.Contains("JsonElement", StringComparison.Ordinal));
    }

    // ── Malformed path template (unclosed brace) ──────────────────────
    [TestMethod]
    public void Generate_MalformedTemplate_UnclosedBrace_EmitsLiteralRemainder()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items/{id}/{bad": {
                  "get": {
                    "operationId": "getItem",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}~1{bad}/get/parameters/0/schema"] = "Test.JsonString",
        };
        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "GetItemRequest.cs");
        Assert.IsTrue(req.Content.Contains("\"{bad\"u8", StringComparison.Ordinal));
    }

    // ── Response with non-JSON content returns null type ───────────────
    [TestMethod]
    public void Generate_NonJsonResponse_NoTypedAccessor()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/download": {
                  "get": {
                    "operationId": "downloadFile",
                    "tags": ["files"],
                    "responses": {
                      "200": {
                        "description": "binary",
                        "content": {
                          "application/octet-stream": {
                            "schema": { "type": "string", "format": "binary" }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile resp = GetFile(files, "DownloadFileResponse.cs");
        Assert.IsFalse(resp.Content.Contains("GetOk()", StringComparison.Ordinal));
    }

    // ── Empty response (no content) ───────────────────────────────────
    [TestMethod]
    public void Generate_EmptyResponse_NoContentAccessor()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items/{id}": {
                  "delete": {
                    "operationId": "deleteItem",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": {
                      "204": { "description": "deleted" }
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
        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile resp = GetFile(files, "DeleteItemResponse.cs");
        Assert.IsTrue(resp.Content.Contains("StatusCode", StringComparison.Ordinal));
        Assert.IsFalse(resp.Content.Contains("Body { get;", StringComparison.Ordinal));
    }

    // ── Cookie parameter ──────────────────────────────────────────────
    [TestMethod]
    public void Generate_CookieParam_EmitsCookieWrite()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "session_id", "in": "cookie", "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/parameters/0/schema"] = "Test.JsonString",
        };
        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("WriteCookies", StringComparison.Ordinal));
    }

    // ── Tilde in path segment triggers ~0 encoding ────────────────────
    [TestMethod]
    public void Generate_TildeInPath_SchemaPointerUsesTildeEncoding()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items~special": {
                  "get": {
                    "operationId": "getSpecial",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "q", "in": "query", "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~0special/get/parameters/0/schema"] = "Test.JsonString",
        };
        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        Assert.IsTrue(files.Count > 0);
    }

    // ── Server with no URL defined ────────────────────────────────────
    [TestMethod]
    public void Generate_ServerWithNoUrl_NoDefaultServerUrl()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "servers": [{}],
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile iface = GetFile(files, "IApiItemsClient.cs");
        Assert.IsFalse(iface.Content.Contains("DefaultServerUrl", StringComparison.Ordinal));
    }

    // ── Path template with trailing literal after last parameter ──────
    [TestMethod]
    public void Generate_PathTrailingLiteral_EmitsTrailingSegment()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items/{id}/details": {
                  "get": {
                    "operationId": "getItemDetails",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}~1details/get/parameters/0/schema"] = "Test.JsonString",
        };
        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "GetItemDetailsRequest.cs");
        Assert.IsTrue(req.Content.Contains("\"/details\"u8", StringComparison.Ordinal));
    }

    // ── Response with undefined content map ───────────────────────────
    [TestMethod]
    public void Generate_ResponseWithDescriptionOnly_NoBodyProperty()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "post": {
                    "operationId": "createItem",
                    "tags": ["items"],
                    "requestBody": {
                      "content": {
                        "application/json": {
                          "schema": { "type": "object" }
                        }
                      }
                    },
                    "responses": {
                      "201": { "description": "created" },
                      "500": { "description": "server error" }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");
        Assert.IsFalse(resp.Content.Contains("InternalServerErrorBody", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_PathParam_LabelStyle_EmitsDotPrefix()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "LabelPath", "version": "1.0" },
              "paths": {
                "/items/{color}": {
                  "get": {
                    "operationId": "getByColor",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "color", "in": "path", "required": true, "style": "label", "schema": { "type": "string" } }
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
            ["#/paths/~1items~1{color}/get/parameters/0/schema"] = "Test.JsonString",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByColorRequest.cs");
        Assert.IsTrue(req.Content.Contains("\".\"u8", StringComparison.Ordinal), "Label style should emit dot prefix");
    }

    [TestMethod]
    public void Generate_PathParam_MatrixStyle_EmitsSemicolonNamePrefix()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "MatrixPath", "version": "1.0" },
              "paths": {
                "/items/{color}": {
                  "get": {
                    "operationId": "getByColor",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "color", "in": "path", "required": true, "style": "matrix", "schema": { "type": "string" } }
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
            ["#/paths/~1items~1{color}/get/parameters/0/schema"] = "Test.JsonString",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByColorRequest.cs");
        Assert.IsTrue(req.Content.Contains("\";color=\"u8", StringComparison.Ordinal), "Matrix style should emit ;name= prefix");
    }

    [TestMethod]
    public void Generate_PathParam_SimpleStyle_NoPrefix()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "SimplePath", "version": "1.0" },
              "paths": {
                "/items/{color}": {
                  "get": {
                    "operationId": "getByColor",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "color", "in": "path", "required": true, "style": "simple", "schema": { "type": "string" } }
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
            ["#/paths/~1items~1{color}/get/parameters/0/schema"] = "Test.JsonString",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByColorRequest.cs");
        Assert.IsFalse(req.Content.Contains("\".\"u8", StringComparison.Ordinal), "Simple style should not emit dot prefix");
        Assert.IsFalse(req.Content.Contains("\";color=\"u8", StringComparison.Ordinal), "Simple style should not emit matrix prefix");
    }

    [TestMethod]
    public void Generate_PathParam_LabelArray_EmitsEnumerateArrayWithDotSeparator()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "LabelArray", "version": "1.0" },
              "paths": {
                "/items/{colors}": {
                  "get": {
                    "operationId": "getByColors",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "colors", "in": "path", "required": true, "style": "label", "explode": true, "schema": { "type": "array", "items": { "type": "string" } } }
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
            ["#/paths/~1items~1{colors}/get/parameters/0/schema"] = "Test.JsonArray",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByColorsRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateArray", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\".\"u8", StringComparison.Ordinal), "Label+explode array should use dot separator");
    }

    [TestMethod]
    public void Generate_PathParam_MatrixExplodeArray_EmitsRepeatedNameSeparator()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "MatrixExplodeArray", "version": "1.0" },
              "paths": {
                "/items/{colors}": {
                  "get": {
                    "operationId": "getByColors",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "colors", "in": "path", "required": true, "style": "matrix", "explode": true, "schema": { "type": "array", "items": { "type": "string" } } }
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
            ["#/paths/~1items~1{colors}/get/parameters/0/schema"] = "Test.JsonArray",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByColorsRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateArray", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\";colors=\"u8", StringComparison.Ordinal), "Matrix+explode array should use ;name= separator");
    }

    [TestMethod]
    public void Generate_PathParam_SimpleExplodeObject_EmitsEqualsKvSeparator()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "SimpleExplodeObj", "version": "1.0" },
              "paths": {
                "/items/{color}": {
                  "get": {
                    "operationId": "getByColor",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "color", "in": "path", "required": true, "style": "simple", "explode": true, "schema": { "type": "object" } }
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
            ["#/paths/~1items~1{color}/get/parameters/0/schema"] = "Test.JsonObject",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "GetByColorRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateObject", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"=\"u8", StringComparison.Ordinal), "Simple+explode object should use = as key-value separator");
    }

    [TestMethod]
    public void Generate_QueryParam_FormExplodeArray_EmitsRepeatedNameValue()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "FormExplode", "version": "1.0" },
              "paths": {
                "/search": {
                  "get": {
                    "operationId": "search",
                    "tags": ["search"],
                    "parameters": [
                      { "name": "tags", "in": "query", "style": "form", "explode": true, "schema": { "type": "array", "items": { "type": "string" } } }
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
            ["#/paths/~1search/get/parameters/0/schema"] = "Test.JsonArray",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "SearchRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateArray", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"tags=\"u8", StringComparison.Ordinal), "Form+explode array should repeat name= for each element");
    }

    [TestMethod]
    public void Generate_QueryParam_SpaceDelimitedArray_EmitsSpaceSeparator()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "SpaceDelim", "version": "1.0" },
              "paths": {
                "/search": {
                  "get": {
                    "operationId": "search",
                    "tags": ["search"],
                    "parameters": [
                      { "name": "tags", "in": "query", "style": "spaceDelimited", "schema": { "type": "array", "items": { "type": "string" } } }
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
            ["#/paths/~1search/get/parameters/0/schema"] = "Test.JsonArray",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "SearchRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateArray", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"%20\"u8", StringComparison.Ordinal), "SpaceDelimited array should use %20 separator");
    }

    [TestMethod]
    public void Generate_QueryParam_PipeDelimitedArray_EmitsPipeSeparator()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "PipeDelim", "version": "1.0" },
              "paths": {
                "/search": {
                  "get": {
                    "operationId": "search",
                    "tags": ["search"],
                    "parameters": [
                      { "name": "tags", "in": "query", "style": "pipeDelimited", "schema": { "type": "array", "items": { "type": "string" } } }
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
            ["#/paths/~1search/get/parameters/0/schema"] = "Test.JsonArray",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "SearchRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateArray", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"%7C\"u8", StringComparison.Ordinal), "PipeDelimited array should use %7C separator");
    }

    [TestMethod]
    public void Generate_QueryParam_DeepObjectStyle_EmitsSquareBracketEncoding()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "DeepObj", "version": "1.0" },
              "paths": {
                "/search": {
                  "get": {
                    "operationId": "search",
                    "tags": ["search"],
                    "parameters": [
                      { "name": "color", "in": "query", "style": "deepObject", "explode": true, "schema": { "type": "object" } }
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "SearchRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateObject", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("\"color%5B\"u8", StringComparison.Ordinal), "DeepObject should emit name%5B prefix");
        Assert.IsTrue(req.Content.Contains("\"%5D=\"u8", StringComparison.Ordinal), "DeepObject should emit %5D= suffix");
    }

    [TestMethod]
    public void Generate_QueryParam_FormExplodeObject_EmitsKeyEqualsValuePairs()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "FormExplodeObj", "version": "1.0" },
              "paths": {
                "/search": {
                  "get": {
                    "operationId": "search",
                    "tags": ["search"],
                    "parameters": [
                      { "name": "color", "in": "query", "style": "form", "explode": true, "schema": { "type": "object" } }
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

        OpenApi30CodeGenerator gen = new("Test", map);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "SearchRequest.cs");
        Assert.IsTrue(req.Content.Contains("EnumerateObject", StringComparison.Ordinal));
        Assert.IsFalse(req.Content.Contains("\"color=\"u8", StringComparison.Ordinal), "Form+explode object should not prefix with name=");
        Assert.IsTrue(req.Content.Contains("\"=\"u8", StringComparison.Ordinal), "Form+explode object should emit = between key and value");
    }

    // ── Octet-stream / vendor +json code generation ──────────────────────
    private const string StreamSpec = """
        {
          "openapi": "3.0.3",
          "info": { "title": "StreamTest", "version": "1.0" },
          "paths": {
            "/download": {
              "get": {
                "operationId": "downloadFile",
                "tags": ["files"],
                "responses": {
                  "200": {
                    "description": "Binary file",
                    "content": { "application/octet-stream": { "schema": { "type": "string", "format": "binary" } } }
                  }
                }
              }
            },
            "/upload": {
              "post": {
                "operationId": "uploadFile",
                "tags": ["files"],
                "requestBody": {
                  "required": true,
                  "content": { "application/octet-stream": { "schema": { "type": "string", "format": "binary" } } }
                },
                "responses": {
                  "201": {
                    "description": "Created",
                    "content": { "application/json": { "schema": { "type": "object", "properties": { "id": { "type": "string" } } } } }
                  }
                }
              }
            },
            "/mixed": {
              "get": {
                "operationId": "downloadMixed",
                "tags": ["files"],
                "responses": {
                  "200": {
                    "description": "Binary file",
                    "content": { "application/octet-stream": { "schema": { "type": "string", "format": "binary" } } }
                  },
                  "404": {
                    "description": "Not found",
                    "content": { "application/json": { "schema": { "type": "object", "properties": { "error": { "type": "string" } } } } }
                  }
                }
              }
            },
            "/vendor": {
              "get": {
                "operationId": "getVendorData",
                "tags": ["files"],
                "responses": {
                  "200": {
                    "description": "Vendor JSON",
                    "content": { "application/vnd.api+json": { "schema": { "type": "object", "properties": { "data": { "type": "string" } } } } }
                  }
                }
              }
            }
          }
        }
        """;

    private static IReadOnlyList<GeneratedFile> GenerateStreamSpec()
    {
        JsonElement root = ParseSpec(StreamSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1upload/post/responses/201/content/application~1json/schema"] = "Test.UploadResult",
            ["#/paths/~1mixed/get/responses/404/content/application~1json/schema"] = "Test.ErrorResponse",
            ["#/paths/~1vendor/get/responses/200/content/application~1vnd.api+json/schema"] = "Test.VendorData",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        return gen.Generate(root);
    }

    [TestMethod]
    public void CollectSchemaPointers_ExcludesOctetStreamSchemas()
    {
        JsonElement root = ParseSpec(StreamSpec);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _);

        Assert.IsFalse(
            pointers.Any(p => p.Contains("octet-stream", StringComparison.Ordinal)),
            "Octet-stream schemas should be excluded from schema pointer collection");
        Assert.IsTrue(
            pointers.Any(p => p.Contains("application~1json", StringComparison.Ordinal)),
            "JSON schemas should still be collected");
    }

    [TestMethod]
    public void CollectSchemaPointers_IncludesVendorJsonSchemas()
    {
        JsonElement root = ParseSpec(StreamSpec);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _);

        Assert.IsTrue(
            pointers.Any(p => p.Contains("vnd.api+json", StringComparison.Ordinal)),
            "Vendor +json schemas should be collected like regular JSON");
    }

    [TestMethod]
    public void StreamResponse_NoParsedDocumentField()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "DownloadFileResponse.cs");

        Assert.IsFalse(
            resp.Content.Contains("parsedDocument", StringComparison.Ordinal),
            "Stream-only response should not have a parsedDocument field");
    }

    [TestMethod]
    public void StreamResponse_HasStreamProperty()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "DownloadFileResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("public Stream? OkStream", StringComparison.Ordinal),
            "Stream response should have a Stream? property");
    }

    [TestMethod]
    public void StreamResponse_HasTryGetOkStreamWithNotNullWhen()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "DownloadFileResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("[NotNullWhen(true)] out Stream? result", StringComparison.Ordinal),
            "Stream TryGet should use [NotNullWhen(true)]");
    }

    [TestMethod]
    public void StreamResponse_MatchResultUsesStreamType()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "DownloadFileResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("ResponseMatcher<Stream?, TResult>", StringComparison.Ordinal),
            "Stream response MatchResult should use Stream? as body type");
    }

    [TestMethod]
    public void StreamResponse_IsNotAsync()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "DownloadFileResponse.cs");

        Assert.IsFalse(
            resp.Content.Contains("async ValueTask<DownloadFileResponse>", StringComparison.Ordinal),
            "Stream-only CreateAsync should not be async");
        Assert.IsTrue(
            resp.Content.Contains("ValueTask.FromResult(response)", StringComparison.Ordinal),
            "Non-async CreateAsync should use ValueTask.FromResult");
    }

    [TestMethod]
    public void StreamRequestBody_ClientMethodAcceptsStream()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile client = GetFile(files, "ApiFilesClient.cs");

        Assert.IsTrue(
            client.Content.Contains("Stream body", StringComparison.Ordinal),
            "Upload operation should accept a Stream body parameter");
        Assert.IsTrue(
            client.Content.Contains("string contentType", StringComparison.Ordinal),
            "Upload operation should accept a contentType parameter");
    }

    [TestMethod]
    public void StreamRequestBody_InterfaceMethodAcceptsStream()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile iface = GetFile(files, "IApiFilesClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("Stream body", StringComparison.Ordinal),
            "Interface upload method should accept Stream body");
    }

    [TestMethod]
    public void StreamRequestBody_ResponseHasParsedDocumentForJsonBody()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "UploadFileResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("parsedDocument", StringComparison.Ordinal),
            "Upload response has JSON body (201) so should have parsedDocument");
        Assert.IsFalse(
            resp.Content.Contains("OkStream", StringComparison.Ordinal),
            "Upload response should not have stream properties (only JSON response)");
    }

    [TestMethod]
    public void MixedResponse_HasBothStreamAndJsonAccessors()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "DownloadMixedResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("TryGetOkStream", StringComparison.Ordinal),
            "Mixed response should have TryGetOkStream for 200");
        Assert.IsTrue(
            resp.Content.Contains("TryGetNotFound", StringComparison.Ordinal),
            "Mixed response should have TryGetNotFound for 404 JSON body");
    }

    [TestMethod]
    public void MixedResponse_HasParsedDocumentField()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "DownloadMixedResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("parsedDocument", StringComparison.Ordinal),
            "Mixed response with JSON body should have parsedDocument");
    }

    [TestMethod]
    public void MixedResponse_StreamAccessorHasNotNullWhen()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "DownloadMixedResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("[NotNullWhen(true)] out Stream? result", StringComparison.Ordinal),
            "Mixed stream TryGet should use [NotNullWhen(true)]");
    }

    [TestMethod]
    public void VendorJson_TreatedAsRegularJson()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "GetVendorDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("TryGetOk", StringComparison.Ordinal),
            "Vendor +json response should have standard TryGetOk accessor");
        Assert.IsFalse(
            resp.Content.Contains("OkStream", StringComparison.Ordinal),
            "Vendor +json should not generate stream accessors");
        Assert.IsTrue(
            resp.Content.Contains("parsedDocument", StringComparison.Ordinal),
            "Vendor +json should parse as JSON (parsedDocument field present)");
    }

    [TestMethod]
    public void VendorJson_UsesCorrectSchemaType()
    {
        IReadOnlyList<GeneratedFile> files = GenerateStreamSpec();
        GeneratedFile resp = GetFile(files, "GetVendorDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("Test.VendorData", StringComparison.Ordinal),
            "Vendor +json response should use the mapped schema type");
    }

    // ── text/plain codegen tests ────────────────────────────────────────
    private const string TextPlainSpec = """
        {
          "openapi": "3.0.3",
          "info": { "title": "TextTest", "version": "1.0" },
          "paths": {
            "/echo": {
              "post": {
                "operationId": "echoText",
                "tags": ["text"],
                "requestBody": {
                  "required": true,
                  "content": { "text/plain": { "schema": { "type": "string" } } }
                },
                "responses": {
                  "200": {
                    "description": "Echo",
                    "content": { "text/plain": { "schema": { "type": "string" } } }
                  }
                }
              }
            },
            "/textmixed": {
              "get": {
                "operationId": "getTextOrJson",
                "tags": ["text"],
                "responses": {
                  "200": {
                    "description": "Text response",
                    "content": { "text/plain": { "schema": { "type": "string" } } }
                  },
                  "400": {
                    "description": "Error",
                    "content": { "application/json": { "schema": { "type": "object", "properties": { "error": { "type": "string" } } } } }
                  }
                }
              }
            },
            "/wildcard": {
              "get": {
                "operationId": "getWildcard",
                "tags": ["text"],
                "responses": {
                  "200": {
                    "description": "Any text",
                    "content": { "text/*": { "schema": { "type": "string" } } }
                  }
                }
              }
            }
          }
        }
        """;

    private static IReadOnlyList<GeneratedFile> GenerateTextPlainSpec()
    {
        JsonElement root = ParseSpec(TextPlainSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1textmixed/get/responses/400/content/application~1json/schema"] = "Test.ErrorResponse",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        return gen.Generate(root);
    }

    [TestMethod]
    public void CollectSchemaPointers_ExcludesTextPlainSchemas()
    {
        JsonElement root = ParseSpec(TextPlainSpec);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _);

        Assert.IsFalse(
            pointers.Any(p => p.Contains("text~1plain", StringComparison.Ordinal)),
            "text/plain schemas should be excluded from schema pointer collection");

        Assert.IsTrue(
            pointers.Any(p => p.Contains("application~1json", StringComparison.Ordinal)),
            "JSON schemas should still be collected");
    }

    [TestMethod]
    public void CollectSchemaPointers_ExcludesTextWildcardSchemas()
    {
        JsonElement root = ParseSpec(TextPlainSpec);
        string[] pointers = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _);

        Assert.IsFalse(
            pointers.Any(p => p.Contains("text~1*", StringComparison.Ordinal)),
            "text/* wildcard schemas should be excluded from schema pointer collection");
    }

    [TestMethod]
    public void TextResponse_NoParsedDocumentField()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsFalse(
            resp.Content.Contains("parsedDocument", StringComparison.Ordinal),
            "Text-only response should not have a parsedDocument field");
    }

    [TestMethod]
    public void TextResponse_HasLazyTextProperty()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("OkText", StringComparison.Ordinal),
            "Text response should have an OkText property");
    }

    [TestMethod]
    public void TextResponse_HasUtf8BytesProperty()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("OkUtf8Bytes", StringComparison.Ordinal),
            "Text response should have an OkUtf8Bytes span property");
    }

    [TestMethod]
    public void TextResponse_HasTryGetOkString()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("TryGetOkString", StringComparison.Ordinal),
            "Text response should have a TryGetOkString method");
    }

    [TestMethod]
    public void TextResponse_NoTryGetOkStream()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsFalse(
            resp.Content.Contains("TryGetOkStream", StringComparison.Ordinal),
            "Text response should NOT have a TryGetOkStream method");
    }

    [TestMethod]
    public void TextResponse_NoStreamProperty()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsFalse(
            resp.Content.Contains("OkStream", StringComparison.Ordinal),
            "Text response should NOT have a Stream property");
    }

    [TestMethod]
    public void TextResponse_HasArrayPoolReturn()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("ArrayPool<byte>.Shared.Return", StringComparison.Ordinal),
            "Text response DisposeAsync should return rented buffer to ArrayPool");
    }

    [TestMethod]
    public void TextResponse_HasReadStreamToRentedBufferHelper()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("ReadStreamToRentedBuffer", StringComparison.Ordinal),
            "Text response should contain the ReadStreamToRentedBuffer helper method");
    }

    [TestMethod]
    public void TextResponse_HasBackingFields()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("okTextBuffer", StringComparison.Ordinal),
            "Text response should have a buffer backing field");
        Assert.IsTrue(
            resp.Content.Contains("okTextLength", StringComparison.Ordinal),
            "Text response should have a length backing field");
        Assert.IsTrue(
            resp.Content.Contains("okTextCached", StringComparison.Ordinal),
            "Text response should have a cached string backing field");
    }

    [TestMethod]
    public void TextRequest_UsesStreamBody()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile client = GetFile(files, "IApiTextClient.cs");

        Assert.IsTrue(
            client.Content.Contains("Stream body", StringComparison.Ordinal),
            "Text/plain request body should use Stream parameter");
    }

    [TestMethod]
    public void TextRequest_UsesTextPlainContentType()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile client = GetFile(files, "ApiTextClient.cs");

        Assert.IsTrue(
            client.Content.Contains("text/plain", StringComparison.Ordinal),
            "Text/plain request should pass text/plain content type");
    }

    [TestMethod]
    public void TextMixedResponse_HasBothParsedDocAndTextFields()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "GetTextOrJsonResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("parsedDocument", StringComparison.Ordinal),
            "Mixed text+JSON response should have a parsedDocument field for the JSON path");
        Assert.IsTrue(
            resp.Content.Contains("OkText", StringComparison.Ordinal),
            "Mixed text+JSON response should have an OkText property for the text path");
    }

    [TestMethod]
    public void TextMixedResponse_HasBothTryGetMethods()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "GetTextOrJsonResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("TryGetOkString", StringComparison.Ordinal),
            "Mixed response should have TryGetOkString");
        Assert.IsTrue(
            resp.Content.Contains("TryGetBadRequest", StringComparison.Ordinal),
            "Mixed response should have TryGetBadRequest for the JSON status");
    }

    [TestMethod]
    public void TextWildcard_TreatedAsTextPlain()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "GetWildcardResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("OkText", StringComparison.Ordinal),
            "text/* wildcard response should be treated as text/plain with OkText property");
        Assert.IsFalse(
            resp.Content.Contains("OkStream", StringComparison.Ordinal),
            "text/* wildcard should NOT produce a stream property");
    }

    [TestMethod]
    public void TextResponse_MatchResult_UsesStringType()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("string?", StringComparison.Ordinal),
            "Text response MatchResult should use string? as the type");
    }

    [TestMethod]
    public void TextResponse_CreateAsync_CallsReadStreamToRentedBuffer()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile resp = GetFile(files, "EchoTextResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("ReadStreamToRentedBuffer(contentStream", StringComparison.Ordinal),
            "CreateAsync should call ReadStreamToRentedBuffer for text/plain responses");
    }
}