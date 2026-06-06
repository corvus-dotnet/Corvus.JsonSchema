// <copyright file="OpenApi30CodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi30;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class OpenApi30CodeGeneratorTests
{
    private static JsonElement petstoreRoot;
    private static JsonElement callbacksSpecRoot;

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

        string callbacksJson = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "callbacks-3.0.json"));
        using ParsedJsonDocument<JsonElement> callbacksDoc = ParsedJsonDocument<JsonElement>.Parse(callbacksJson);
        callbacksSpecRoot = callbacksDoc.RootElement.Clone();
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out var parameterNames).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(pointers, "#/paths/~1pets/get/parameters/0/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1pets~1{petId}/get/parameters/0/schema");

        // Parameter names are recorded for naming heuristics (keyed by fragment without '#')
        Assert.AreEqual("limit", parameterNames["/paths/~1pets/get/parameters/0/schema"]);
        Assert.AreEqual("petId", parameterNames["/paths/~1pets~1{petId}/get/parameters/0/schema"]);
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsRequestBodySchemas()
    {
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/post/requestBody/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsResponseSchemas()
    {
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];

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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers, "#/paths/~1pets/get/responses/200/headers/x-next/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_WithFilter_OnlyIncludesMatchingPaths()
    {
        OperationFilter filter = new(["/pets"]);
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _, filter).Select(r => r.PositionalPointer)];

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

        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(emptySpec, out var parameterNames).Select(r => r.PositionalPointer)];

        Assert.AreEqual(0, pointers.Length);
        Assert.AreEqual(0, parameterNames.Count);
    }

    [TestMethod]
    public void CollectSchemaPointers_BrokenRefs_SkipsGracefully()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Broken", "version": "1.0" },
              "paths": {
                "/a": {
                  "get": {
                    "operationId": "getA",
                    "parameters": [
                      { "$ref": "#/components/parameters/DoesNotExist" }
                    ],
                    "requestBody": { "$ref": "#/components/requestBodies/DoesNotExist" },
                    "responses": {
                      "200": { "$ref": "#/components/responses/DoesNotExist" }
                    }
                  }
                },
                "/b": { "$ref": "#/components/pathItems/DoesNotExist" }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        SchemaReference[] refs = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _);

        Assert.AreEqual(0, refs.Length);
    }

    [TestMethod]
    public void CollectSchemaPointers_BrokenHeaderRef_SkipsGracefully()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "BrokenHeader", "version": "1.0" },
              "paths": {
                "/x": {
                  "get": {
                    "operationId": "getX",
                    "responses": {
                      "200": {
                        "description": "ok",
                        "headers": {
                          "X-Broken": { "$ref": "#/components/headers/DoesNotExist" }
                        },
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
            """;

        JsonElement root = ParseSpec(spec);
        SchemaReference[] refs = OpenApi30CodeGenerator.CollectSchemaPointers(root, out _);

        Assert.AreEqual(1, refs.Length);
        Assert.IsTrue(refs[0].PositionalPointer.Contains("content", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_ProducesCorrectFileCount()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        // 4 operations × 2 (request + response) + 1 interface + 1 implementation = 10
        Assert.AreEqual(10, files.Count);
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
                "Petstore.Client.JsonString.CreateBuilder(workspace, petId, 30).RootElement",
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
                "Petstore.Client.NewPet.CreateBuilder(workspace, body, 30).RootElement",
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
                "Petstore.Client.JsonInt32 Limit { get; init; }", StringComparison.Ordinal));
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _).Select(r => r.PositionalPointer)];

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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];

        // 3 param schemas + 1 request body + 7 response bodies + 1 response header = 12
        Assert.AreEqual(12, pointers.Length);
    }

    [TestMethod]
    public void CollectSchemaPointers_Petstore_AllPointersAreUnique()
    {
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];
        HashSet<string> unique = new(pointers, StringComparer.Ordinal);

        Assert.AreEqual(pointers.Length, unique.Count, "Duplicate schema pointers found");
    }

    [TestMethod]
    public void Generate_InterfaceHasCreateServerUri()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains(
                "CreateServerUri",
                StringComparison.Ordinal));
        Assert.IsTrue(
            iface.Content.Contains(
                "\"https://petstore.example.com/v1\"",
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(spec, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/parameters/0/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_RefRequestBody_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(spec, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/requestBody/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_RefResponse_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(spec, out _).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(pointers, "#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_RefHeader_ResolvesAndCollects()
    {
        JsonElement spec = ParseSpec(RefSpec30);
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(spec, out _).Select(r => r.PositionalPointer)];

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
    public void Generate_OptionalCookieParam_EmitsIsNotUndefinedCheck()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec30);
        OpenApi30CodeGenerator gen = new("Test", RequiredParamsMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("Test.JsonString Pref { get; init; }", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("this.Pref.IsNotUndefined()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_OptionalHeaderParam_EmitsIsNotUndefinedCheck()
    {
        JsonElement spec = ParseSpec(RequiredParamsSpec30);
        OpenApi30CodeGenerator gen = new("Test", RequiredParamsMap30);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec);

        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");
        Assert.IsTrue(req.Content.Contains("Test.JsonString XTrace { get; init; }", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("this.XTrace.IsNotUndefined()", StringComparison.Ordinal));
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(spec, out _).Select(r => r.PositionalPointer)];

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
        Assert.IsFalse(iface.Content.Contains("CreateServerUri", StringComparison.Ordinal));
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
        Assert.IsFalse(iface.Content.Contains("CreateServerUri", StringComparison.Ordinal));
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

        // A parameter without a schema is still a valid query parameter — it defaults
        // to JsonElement as its type and is included in the request struct.
        Assert.IsTrue(
            req.Content.Contains("HasQueryParameters => true", StringComparison.Ordinal),
            "Schema-less query parameter should still be a query parameter");
        Assert.IsTrue(
            req.Content.Contains("JsonElement", StringComparison.Ordinal),
            "Schema-less parameter should default to JsonElement type");
    }

    [TestMethod]
    public void Generate_RefParameter_ResolvesCorrectly()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "RefParam", "version": "1.0" },
              "paths": {
                "/items": {
                  "parameters": [
                    { "$ref": "#/components/parameters/PageSize" }
                  ],
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "parameters": [
                      { "$ref": "#/components/parameters/PageToken" }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              },
              "components": {
                "parameters": {
                  "PageSize": {
                    "name": "pageSize",
                    "in": "query",
                    "schema": { "type": "integer", "format": "int32" }
                  },
                  "PageToken": {
                    "name": "pageToken",
                    "in": "query",
                    "schema": { "type": "string" }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> typeMap = new(StringComparer.Ordinal)
        {
            ["#/components/parameters/PageSize/schema"] = "Test.JsonInt32",
            ["#/components/parameters/PageToken/schema"] = "Test.JsonString",
        };
        OpenApi30CodeGenerator gen = new("Test", typeMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");

        // Both $ref parameters should be resolved and included
        Assert.IsTrue(
            req.Content.Contains("HasQueryParameters => true", StringComparison.Ordinal),
            "Request should have query parameters from $ref resolution");
        Assert.IsTrue(
            req.Content.Contains("pageSize", StringComparison.Ordinal),
            "Path-level $ref parameter 'pageSize' should be resolved");
        Assert.IsTrue(
            req.Content.Contains("pageToken", StringComparison.Ordinal),
            "Operation-level $ref parameter 'pageToken' should be resolved");
    }

    [TestMethod]
    public void Generate_RefResponse_ResolvesCorrectly()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "RefResp", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "responses": {
                      "200": { "$ref": "#/components/responses/ItemList" }
                    }
                  }
                }
              },
              "components": {
                "responses": {
                  "ItemList": {
                    "description": "A list of items",
                    "content": {
                      "application/json": {
                        "schema": { "type": "array", "items": { "type": "string" } }
                      }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> typeMap = new(StringComparer.Ordinal)
        {
            ["#/components/responses/ItemList/content/application~1json/schema"] = "Test.Items",
        };
        OpenApi30CodeGenerator gen = new("Test", typeMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile resp = GetFile(files, "ListItemsResponse.cs");

        // The $ref response should be resolved and generate a typed accessor
        Assert.IsTrue(
            resp.Content.Contains("TryGetOk", StringComparison.Ordinal),
            "Response should have a TryGetOk method for the $ref response");
    }

    [TestMethod]
    public void Generate_RefRequestBody_ResolvesCorrectly()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "RefBody", "version": "1.0" },
              "paths": {
                "/items": {
                  "post": {
                    "operationId": "createItem",
                    "tags": ["items"],
                    "requestBody": { "$ref": "#/components/requestBodies/NewItem" },
                    "responses": { "201": { "description": "Created" } }
                  }
                }
              },
              "components": {
                "requestBodies": {
                  "NewItem": {
                    "description": "The item to create",
                    "required": true,
                    "content": {
                      "application/json": {
                        "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                      }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> typeMap = new(StringComparer.Ordinal)
        {
            ["#/components/requestBodies/NewItem/content/application~1json/schema"] = "Test.NewItem",
        };
        OpenApi30CodeGenerator gen = new("Test", typeMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile client = GetFile(files, "ApiItemsClient.cs");

        // The $ref request body should be resolved and the client method should accept a body
        Assert.IsTrue(
            client.Content.Contains("SendWithBodyAsyncCore", StringComparison.Ordinal),
            "Client should call SendWithBodyAsyncCore for the $ref request body");
    }

    [TestMethod]
    public void Generate_RefHeader_ResolvesCorrectly()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "RefHeader", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "responses": {
                      "200": {
                        "description": "Ok",
                        "headers": {
                          "X-Rate-Limit": { "$ref": "#/components/headers/RateLimit" }
                        }
                      }
                    }
                  }
                }
              },
              "components": {
                "headers": {
                  "RateLimit": {
                    "description": "Rate limit remaining",
                    "schema": { "type": "integer", "format": "int32" }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> typeMap = new(StringComparer.Ordinal)
        {
            ["#/components/headers/RateLimit/schema"] = "Test.JsonInt32",
        };
        OpenApi30CodeGenerator gen = new("Test", typeMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile resp = GetFile(files, "ListItemsResponse.cs");

        // The $ref header should be resolved and exposed as a typed property
        Assert.IsTrue(
            resp.Content.Contains("X-Rate-Limit", StringComparison.Ordinal),
            "Response should include the $ref header X-Rate-Limit");
    }

    [TestMethod]
    public void Generate_FormUrlEncoded_NoEncodings_EmitsSerializeWithoutEncodings()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "FormNoEnc", "version": "1.0" },
              "paths": {
                "/submit": {
                  "post": {
                    "operationId": "submitForm",
                    "tags": ["forms"],
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/x-www-form-urlencoded": {
                          "schema": {
                            "type": "object",
                            "properties": {
                              "name": { "type": "string" },
                              "age": { "type": "integer" }
                            }
                          }
                        }
                      }
                    },
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> typeMap = new(StringComparer.Ordinal)
        {
            ["#/paths/~1submit/post/requestBody/content/application~1x-www-form-urlencoded/schema"] = "Test.FormBody",
        };
        OpenApi30CodeGenerator gen = new("Test", typeMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile client = GetFile(files, "ApiFormsClient.cs");

        // Without encoding objects, should call FormUrlEncodedSerializer.Serialize without encodings param
        Assert.IsTrue(
            client.Content.Contains("FormUrlEncodedSerializer.Serialize(bodyValue, stream)", StringComparison.Ordinal),
            "Should call Serialize without encodings parameter");
    }

    [TestMethod]
    public void Generate_Multipart_NoEncodings_EmitsSerializeWithoutEncodings()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "MultiNoEnc", "version": "1.0" },
              "paths": {
                "/upload": {
                  "post": {
                    "operationId": "uploadFile",
                    "tags": ["uploads"],
                    "requestBody": {
                      "required": true,
                      "content": {
                        "multipart/form-data": {
                          "schema": {
                            "type": "object",
                            "properties": {
                              "file": { "type": "string", "format": "binary" },
                              "description": { "type": "string" }
                            }
                          }
                        }
                      }
                    },
                    "responses": { "201": { "description": "Created" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> typeMap = new(StringComparer.Ordinal)
        {
            ["#/paths/~1upload/post/requestBody/content/multipart~1form-data/schema"] = "Test.UploadBody",
        };
        OpenApi30CodeGenerator gen = new("Test", typeMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile client = GetFile(files, "ApiUploadsClient.cs");

        // With format: binary detected, the file property is hoisted to a BinaryPartData
        // parameter and the Serialize call includes the binaryParts dictionary.
        Assert.IsTrue(
            client.Content.Contains("BinaryPartData file", StringComparison.Ordinal),
            "Should generate BinaryPartData parameter for format: binary property");
        Assert.IsTrue(
            client.Content.Contains("MultipartFormDataSerializer.SerializeAsync(bodyValue, stream, boundary, null, binaryParts, ct)", StringComparison.Ordinal),
            "Should call SerializeAsync with binaryParts parameter");
    }

    [TestMethod]
    public void Generate_TextPlainResponse_EmitsTextAccessor()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "TextResp", "version": "1.0" },
              "paths": {
                "/echo": {
                  "get": {
                    "operationId": "echo",
                    "tags": ["misc"],
                    "responses": {
                      "200": {
                        "description": "Echo response",
                        "content": {
                          "text/plain": {
                            "schema": { "type": "string" }
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
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile resp = GetFile(files, "EchoResponse.cs");

        // text/plain response should generate a text accessor
        Assert.IsTrue(
            resp.Content.Contains("OkText", StringComparison.Ordinal),
            "Response should have a text accessor for text/plain response");
    }

    [TestMethod]
    public void Generate_OctetStreamResponse_EmitsStreamAccessor()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "StreamResp", "version": "1.0" },
              "paths": {
                "/download": {
                  "get": {
                    "operationId": "downloadFile",
                    "tags": ["files"],
                    "responses": {
                      "200": {
                        "description": "File download",
                        "content": {
                          "application/octet-stream": {}
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile resp = GetFile(files, "DownloadFileResponse.cs");

        // octet-stream response should generate a stream accessor
        Assert.IsTrue(
            resp.Content.Contains("OkStream", StringComparison.Ordinal),
            "Response should have a stream accessor for octet-stream response");
    }

    [TestMethod]
    public void Generate_NoResponseContent_HasNoBodyAccessor()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "NoContent", "version": "1.0" },
              "paths": {
                "/items/{id}": {
                  "delete": {
                    "operationId": "deleteItem",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": {
                      "204": { "description": "No Content" }
                    }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        Dictionary<string, string> typeMap = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/delete/parameters/0/schema"] = "Test.JsonString",
        };
        OpenApi30CodeGenerator gen = new("Test", typeMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile resp = GetFile(files, "DeleteItemResponse.cs");

        // No-content response should not have a body accessor
        Assert.IsFalse(
            resp.Content.Contains("TryGetNoContent", StringComparison.Ordinal) &&
            resp.Content.Contains("Body", StringComparison.Ordinal),
            "No-content response should not have a body accessor");
    }

    [TestMethod]
    public void Generate_MultiContentResponse_EmitsContentTypeBranching()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Multi", "version": "1.0" },
              "paths": {
                "/data": {
                  "get": {
                    "operationId": "getData",
                    "tags": ["data"],
                    "responses": {
                      "200": {
                        "description": "Data in multiple formats",
                        "content": {
                          "application/json": {
                            "schema": {
                              "type": "object",
                              "properties": {
                                "value": { "type": "string" }
                              }
                            }
                          },
                          "text/plain": {
                            "schema": { "type": "string" }
                          }
                        }
                      },
                      "default": {
                        "description": "Error in multiple formats",
                        "content": {
                          "application/json": {
                            "schema": {
                              "type": "object",
                              "properties": {
                                "error": { "type": "string" }
                              }
                            }
                          },
                          "text/plain": {
                            "schema": { "type": "string" }
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
        Dictionary<string, string> typeMap = new(StringComparer.Ordinal)
        {
            ["#/paths/~1data/get/responses/200/content/application~1json/schema"] = "Test.DataResult",
            ["#/paths/~1data/get/responses/default/content/application~1json/schema"] = "Test.ErrorResult",
        };
        OpenApi30CodeGenerator gen = new("Test", typeMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        // Multi-content response should have Content-Type branching
        Assert.IsTrue(
            resp.Content.Contains("contentType", StringComparison.Ordinal),
            "Multi-content response should branch on Content-Type");

        // Should have both JSON and text accessors
        Assert.IsTrue(
            resp.Content.Contains("OkText", StringComparison.Ordinal),
            "Should have text accessor for 200");
        Assert.IsTrue(
            resp.Content.Contains("OkBody", StringComparison.Ordinal) ||
            resp.Content.Contains("TryGetOk", StringComparison.Ordinal),
            "Should have JSON accessor for 200");

        // Client should generate Match method with multi-category support
        GeneratedFile client = GetFile(files, "ApiDataClient.cs");
        Assert.IsTrue(
            client.Content.Contains("getData", StringComparison.OrdinalIgnoreCase),
            "Client should have the getData operation method");
    }

    [TestMethod]
    public void Generate_OctetStreamRequestBody_EmitsStreamSend()
    {
        const string spec = """
            {
              "openapi": "3.0.3",
              "info": { "title": "StreamReq", "version": "1.0" },
              "paths": {
                "/upload-raw": {
                  "post": {
                    "operationId": "uploadRaw",
                    "tags": ["uploads"],
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/octet-stream": {
                          "schema": { "type": "string", "format": "binary" }
                        }
                      }
                    },
                    "responses": { "201": { "description": "Created" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>(StringComparer.Ordinal));
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);

        GeneratedFile client = GetFile(files, "ApiUploadsClient.cs");

        // octet-stream request body should use stream sending
        Assert.IsTrue(
            client.Content.Contains("Stream", StringComparison.Ordinal),
            "Client should use Stream for octet-stream request body");
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null).Select(r => r.PositionalPointer)];

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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null).Select(r => r.PositionalPointer)];
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null).Select(r => r.PositionalPointer)];
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null).Select(r => r.PositionalPointer)];
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null).Select(r => r.PositionalPointer)];
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null).Select(r => r.PositionalPointer)];
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _, null).Select(r => r.PositionalPointer)];
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

    // ── Non-JSON body content is treated as raw stream ────────────────
    [TestMethod]
    public void Generate_NonJsonBodyContent_FallsBackToStreamBody()
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
        Assert.IsTrue(client.Content.Contains("Stream body", StringComparison.Ordinal));
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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _).Select(r => r.PositionalPointer)];

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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _).Select(r => r.PositionalPointer)];

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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _).Select(r => r.PositionalPointer)];

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
        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(root, out _).Select(r => r.PositionalPointer)];

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

    // --- Accept header tests ---
    [TestMethod]
    public void AcceptHeader_JsonOnlyResponse_EmitsAcceptApplicationJson()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);
        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("""callback("Accept"u8, "application/json"u8, state)""", StringComparison.Ordinal),
            "JSON-only response should emit Accept: application/json");
    }

    [TestMethod]
    public void AcceptHeader_JsonOnlyResponse_HasHeaderParametersIsTrue()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);
        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("HasHeaderParameters => true", StringComparison.Ordinal),
            "Request with Accept header should have HasHeaderParameters => true");
    }

    [TestMethod]
    public void AcceptHeader_TextPlainResponse_EmitsAcceptTextPlain()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile req = GetFile(files, "EchoTextRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("""callback("Accept"u8, "text/plain"u8, state)""", StringComparison.Ordinal),
            "text/plain response should emit Accept: text/plain");
    }

    [TestMethod]
    public void AcceptHeader_MixedTextAndJson_EmitsJsonFirst()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile req = GetFile(files, "GetTextOrJsonRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("""callback("Accept"u8, "application/json, text/plain"u8, state)""", StringComparison.Ordinal),
            "Mixed response should emit Accept with JSON first: application/json, text/plain");
    }

    [TestMethod]
    public void AcceptHeader_MixedTextAndJson_HasHeaderParametersIsTrue()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile req = GetFile(files, "GetTextOrJsonRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("HasHeaderParameters => true", StringComparison.Ordinal),
            "Request with Accept header should have HasHeaderParameters => true");
    }

    [TestMethod]
    public void AcceptHeader_TextWildcard_EmitsAcceptTextStar()
    {
        IReadOnlyList<GeneratedFile> files = GenerateTextPlainSpec();
        GeneratedFile req = GetFile(files, "GetWildcardRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("""callback("Accept"u8, "text/*"u8, state)""", StringComparison.Ordinal),
            "text/* wildcard response should emit Accept: text/*");
    }

    [TestMethod]
    public void AcceptHeader_NoResponseContent_HasHeaderParametersFalse()
    {
        JsonElement root = ParseSpec("""
            {
              "openapi": "3.0.3",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/fire": {
                  "post": {
                    "operationId": "fireAndForget",
                    "tags": ["test"],
                    "responses": {
                      "204": { "description": "No content" }
                    }
                  }
                }
              }
            }
            """);

        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "FireAndForgetRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("HasHeaderParameters => false", StringComparison.Ordinal),
            "Operation with no response content should have HasHeaderParameters => false");
        Assert.IsFalse(
            req.Content.Contains("Accept", StringComparison.Ordinal),
            "Operation with no response content should not emit Accept header");
    }

    [TestMethod]
    public void AcceptHeader_WithExistingHeaderParams_EmitsAcceptAndHeaderParams()
    {
        JsonElement root = ParseSpec("""
            {
              "openapi": "3.0.3",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "searchItems",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "X-Api-Key", "in": "header", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": {
                      "200": {
                        "description": "Items",
                        "content": { "application/json": { "schema": { "type": "array", "items": { "type": "object" } } } }
                      }
                    }
                  }
                }
              }
            }
            """);

        OpenApi30CodeGenerator gen = new("Test", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = gen.Generate(root);
        GeneratedFile req = GetFile(files, "SearchItemsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("""callback("Accept"u8, "application/json"u8, state)""", StringComparison.Ordinal),
            "Should emit Accept header when response has content");
        Assert.IsTrue(
            req.Content.Contains("\"X-Api-Key\"u8", StringComparison.Ordinal),
            "Should still emit declared header parameters");
    }

    // ── Content-Type branching tests ────────────────────────────────────
    private const string MultiContentSpec = """
        {
          "openapi": "3.0.0",
          "info": { "title": "MultiContent", "version": "1.0" },
          "paths": {
            "/data": {
              "get": {
                "operationId": "getData",
                "tags": ["data"],
                "responses": {
                  "200": {
                    "description": "Success",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": { "id": { "type": "integer" } }
                        }
                      },
                      "text/plain": {
                        "schema": { "type": "string" }
                      }
                    }
                  },
                  "default": {
                    "description": "Error",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": { "error": { "type": "string" } }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private static IReadOnlyList<GeneratedFile> GenerateMultiContentSpec()
    {
        JsonElement root = ParseSpec(MultiContentSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1data/get/responses/200/content/application~1json/schema"] = "Test.DataResponse",
            ["#/paths/~1data/get/responses/default/content/application~1json/schema"] = "Test.ErrorResponse",
        };

        OpenApi30CodeGenerator gen = new("Test", map);
        return gen.Generate(root);
    }

    [TestMethod]
    public void MultiContent_CreateAsync_HasContentTypeParameter()
    {
        IReadOnlyList<GeneratedFile> files = GenerateMultiContentSpec();
        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("string? contentType = null,", StringComparison.Ordinal),
            "CreateAsync should include contentType parameter for multi-content response");
    }

    [TestMethod]
    public void MultiContent_CreateAsync_BranchesOnContentType()
    {
        IReadOnlyList<GeneratedFile> files = GenerateMultiContentSpec();
        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "contentType.StartsWith(\"text/\", StringComparison.OrdinalIgnoreCase)",
                StringComparison.Ordinal),
            "CreateAsync should branch on text/ content type");
        Assert.IsTrue(
            resp.Content.Contains(
                "contentType.EndsWith(\"+json\", StringComparison.OrdinalIgnoreCase)",
                StringComparison.Ordinal),
            "CreateAsync should branch on JSON content type");
    }

    [TestMethod]
    public void MultiContent_ResponseStruct_HasTextFields()
    {
        IReadOnlyList<GeneratedFile> files = GenerateMultiContentSpec();
        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("private byte[]? okTextBuffer;", StringComparison.Ordinal),
            "Response struct should have okTextBuffer field for text/plain");
        Assert.IsTrue(
            resp.Content.Contains("public string? OkText", StringComparison.Ordinal),
            "Response struct should have OkText property for text/plain");
    }

    [TestMethod]
    public void MultiContent_ResponseStruct_HasJsonBody()
    {
        IReadOnlyList<GeneratedFile> files = GenerateMultiContentSpec();
        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("public Test.DataResponse OkBody", StringComparison.Ordinal),
            "Response struct should have OkBody property for JSON");
    }

    [TestMethod]
    public void MultiContent_ResponseStruct_HasBothTryGetMethods()
    {
        IReadOnlyList<GeneratedFile> files = GenerateMultiContentSpec();
        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("public bool TryGetOk(", StringComparison.Ordinal),
            "Response struct should have TryGetOk for JSON");
        Assert.IsTrue(
            resp.Content.Contains("public bool TryGetOkString(", StringComparison.Ordinal),
            "Response struct should have TryGetOkString for text/plain");
    }

    [TestMethod]
    public void MultiContent_MatchResult_HasSeparateHandlers()
    {
        IReadOnlyList<GeneratedFile> files = GenerateMultiContentSpec();
        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("matchOk,", StringComparison.Ordinal),
            "MatchResult should have matchOk parameter for JSON");
        Assert.IsTrue(
            resp.Content.Contains("matchOkString,", StringComparison.Ordinal),
            "MatchResult should have matchOkString parameter for text/plain");
    }

    [TestMethod]
    public void MultiContent_MatchResult_DispatchesOnTextBuffer()
    {
        IReadOnlyList<GeneratedFile> files = GenerateMultiContentSpec();
        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("this.okTextBuffer is not null", StringComparison.Ordinal),
            "MatchResult should detect text content by checking okTextBuffer");
        Assert.IsTrue(
            resp.Content.Contains("matchOkString(this.OkText", StringComparison.Ordinal),
            "MatchResult should call matchOkString when text buffer is populated");
        Assert.IsTrue(
            resp.Content.Contains("matchOk(this.OkBody", StringComparison.Ordinal),
            "MatchResult should fall back to matchOk for JSON");
    }

    [TestMethod]
    public void MultiContent_MatchResult_DefaultIsSingleCategory()
    {
        IReadOnlyList<GeneratedFile> files = GenerateMultiContentSpec();
        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("matchDefault(this.DefaultBody", StringComparison.Ordinal),
            "Default (single-category) should dispatch directly to matchDefault");
    }

    [TestMethod]
    public void MultiContent_DisposeAsync_ReturnsTextBuffer()
    {
        IReadOnlyList<GeneratedFile> files = GenerateMultiContentSpec();
        GeneratedFile resp = GetFile(files, "GetDataResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("ArrayPool<byte>.Shared.Return(this.okTextBuffer)", StringComparison.Ordinal),
            "DisposeAsync should return the text buffer to the pool");
    }

    // ── Response Validate() codegen tests ────────────────────────────────
    [TestMethod]
    public void Generate_ResponseWithJsonBodies_EmitsValidate()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "CreatePetResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("public void Validate(ValidationMode mode = ValidationMode.Basic)", StringComparison.Ordinal),
            "Response should contain the Validate method");
    }

    [TestMethod]
    public void Generate_ResponseValidate_ChecksNoneMode()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "CreatePetResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("if (mode == ValidationMode.None)", StringComparison.Ordinal),
            "Response Validate should check for None mode");
    }

    [TestMethod]
    public void Generate_ResponseValidate_BasicMode_CallsEvaluateSchema()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "CreatePetResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("!this.CreatedBody.EvaluateSchema()", StringComparison.Ordinal),
            "Basic mode should call EvaluateSchema on the body");
        Assert.IsTrue(
            resp.Content.Contains("ThrowHelper.ThrowResponseBodyValidationFailed(201)", StringComparison.Ordinal),
            "Basic mode should throw with the status code");
    }

    [TestMethod]
    public void Generate_ResponseValidate_DetailedMode_CreatesCollector()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "CreatePetResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed)",
                StringComparison.Ordinal),
            "Detailed mode should create a results collector");
        Assert.IsTrue(
            resp.Content.Contains(
                "SchemaValidationDetail.FormatResults(collector)",
                StringComparison.Ordinal),
            "Detailed mode should format results");
    }

    [TestMethod]
    public void Generate_ResponseValidate_DefaultResponse_UsesStatusCodeProperty()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "CreatePetResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "ThrowHelper.ThrowResponseBodyValidationFailed(this.StatusCode",
                StringComparison.Ordinal),
            "Default response should use this.StatusCode as the status code expression");
    }

    [TestMethod]
    public void Generate_ResponseValidate_UsesIfElseIfChain()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile resp = GetFile(files, "CreatePetResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("if (this.StatusCode == 201)", StringComparison.Ordinal),
            "Should branch on the named status code");
        Assert.IsTrue(
            resp.Content.Contains("else", StringComparison.Ordinal),
            "Default response should be in the else branch");
    }

    [TestMethod]
    public void Generate_ClientMethod_HasResponseValidationModeParam()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile client = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            client.Content.Contains(
                "ValidationMode responseValidationMode = ValidationMode.None",
                StringComparison.Ordinal),
            "Client method should have responseValidationMode parameter defaulting to None");
    }

    [TestMethod]
    public void Generate_SendAsyncCore_CallsResponseValidate()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile client = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            client.Content.Contains("response.Validate(responseValidationMode)", StringComparison.Ordinal),
            "SendAsyncCore should call response.Validate with the mode");
    }

    [TestMethod]
    public void Generate_DeprecatedOperation_EmitsObsoleteOnInterface()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("[Obsolete(\"This operation is deprecated.\")]", StringComparison.Ordinal),
            "Interface should contain [Obsolete] for deprecated operation");
    }

    [TestMethod]
    public void Generate_DeprecatedOperation_EmitsObsoleteOnImplementation()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile client = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            client.Content.Contains("[Obsolete(\"This operation is deprecated.\")]", StringComparison.Ordinal),
            "Implementation should contain [Obsolete] for deprecated operation");
    }

    [TestMethod]
    public void Generate_NonDeprecatedOperation_DoesNotEmitObsolete()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        // Count [Obsolete] occurrences — should be exactly 1 (only for deletePet)
        int count = 0;
        int idx = 0;
        while ((idx = iface.Content.IndexOf("[Obsolete(", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx++;
        }

        Assert.AreEqual(1, count, "Only the deprecated operation should have [Obsolete]");
    }

    [TestMethod]
    public void Generate_DefaultParameterValue_EmitsNumberConstant()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile client = GetFile(files, "ApiPetsClient.cs");

        // The limit param has "default": 100, so the fallback should use NumberConstant
        Assert.IsTrue(
            client.Content.Contains("NumberConstant", StringComparison.Ordinal),
            "Client should use NumberConstant for integer default value");
    }

    [TestMethod]
    public void Generate_DefaultParameterValue_DocumentsDefault()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("Default: 100.", StringComparison.Ordinal),
            "Interface doc should mention the default value");
    }

    [TestMethod]
    public void Generate_DefaultParameterValue_DoesNotUseParseValue()
    {
        OpenApi30CodeGenerator gen = CreateGenerator();
        IReadOnlyList<GeneratedFile> files = gen.Generate(petstoreRoot);

        GeneratedFile client = GetFile(files, "ApiPetsClient.cs");

        Assert.IsFalse(
            client.Content.Contains("ParseValue", StringComparison.Ordinal),
            "Client should not use ParseValue for scalar defaults");
    }

    // ── Coverage spec helpers ─────────────────────────────────────────────
    private static readonly Dictionary<string, string> CoverageSchemaTypeMap = new(StringComparer.Ordinal)
    {
        ["#/paths/~1items/get/parameters/0/schema"] = "CovTest.JsonBoolean",
        ["#/paths/~1items/get/parameters/1/schema"] = "CovTest.JsonString",
        ["#/paths/~1items/get/parameters/2/schema"] = "CovTest.JsonInt32",
        ["#/paths/~1items/get/parameters/3/schema"] = "CovTest.JsonString",
        ["#/paths/~1items/get/parameters/4/schema"] = "CovTest.JsonBoolean",
        ["#/paths/~1items/get/responses/200/content/application~1json/schema"] = "CovTest.Items",
        ["#/paths/~1items/get/responses/default/content/application~1json/schema"] = "CovTest.Error",
        ["#/paths/~1items/post/requestBody/content/application~1json/schema"] = "CovTest.NewItem",
        ["#/paths/~1items/post/responses/201/content/application~1json/schema"] = "CovTest.CreatedItem",
        ["#/paths/~1items~1{itemId}/get/parameters/0/schema"] = "CovTest.JsonString",
        ["#/paths/~1items~1{itemId}/get/responses/200/content/application~1json/schema"] = "CovTest.Item",
        ["#/paths/~1items~1{itemId}/get/responses/200/headers/X-Rate-Limit/schema"] = "CovTest.JsonInt32",
        ["#/paths/~1items~1{itemId}/get/responses/200/headers/X-Active/schema"] = "CovTest.JsonBoolean",
        ["#/paths/~1items~1{itemId}/get/responses/200/headers/X-Tags/schema"] = "CovTest.Tags",
        ["#/paths/~1items~1{itemId}/get/responses/200/headers/X-Page-Sizes/schema"] = "CovTest.PageSizes",
        ["#/paths/~1items~1{itemId}/get/responses/200/headers/X-Flags/schema"] = "CovTest.Flags",
        ["#/paths/~1items~1{itemId}~1form/post/parameters/0/schema"] = "CovTest.JsonString",
        ["#/paths/~1items~1{itemId}~1form/post/requestBody/content/application~1x-www-form-urlencoded/schema"] = "CovTest.UpdateItemFormBody",
        ["#/paths/~1items~1{itemId}~1form/post/responses/200/content/application~1json/schema"] = "CovTest.Item",
        ["#/paths/~1items~1{itemId}~1upload/post/parameters/0/schema"] = "CovTest.JsonString",
        ["#/paths/~1items~1{itemId}~1upload/post/responses/201/content/application~1json/schema"] = "CovTest.Item",
        ["#/paths/~1quirky~1{qid}/get/parameters/0/schema"] = "CovTest.JsonString",
        ["#/paths/~1quirky~1{qid}/get/parameters/1/schema"] = "CovTest.JsonString",
        ["#/paths/~1quirky~1{qid}/get/responses/200/content/application~1json/schema"] = "CovTest.Item",
        ["#/paths/~1quirky~1{sid}~1styled/get/parameters/0/schema"] = "CovTest.JsonString",
        ["#/paths/~1quirky~1{sid}~1styled/get/parameters/1/schema"] = "CovTest.JsonString",
        ["#/paths/~1quirky~1{sid}~1styled/get/responses/200/content/application~1json/schema"] = "CovTest.Item",
        ["#/paths/~1empty-servers/get/responses/200/content/application~1json/schema"] = "CovTest.GetEmptyServersOk",
    };

    private static JsonElement coverageRoot;

    private static JsonElement GetCoverageRoot()
    {
        if (coverageRoot.ValueKind != JsonValueKind.Undefined)
        {
            return coverageRoot;
        }

        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "covspec-3.0.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        coverageRoot = doc.RootElement.Clone();
        return coverageRoot;
    }

    private static IReadOnlyList<GeneratedFile> GenerateCoverageSpec()
    {
        JsonElement root = GetCoverageRoot();
        OpenApi30CodeGenerator gen = new("CovTest", CoverageSchemaTypeMap);
        return gen.Generate(root);
    }

    // ── Server URL templating tests ───────────────────────────────────────
    [TestMethod]
    public void CovSpec_Interface_HasCreateServerUri()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile iface = files.First(f => f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            iface.Content.Contains("static Uri CreateServerUri(", StringComparison.Ordinal),
            "Interface should contain CreateServerUri method");
    }

    [TestMethod]
    public void CovSpec_Interface_CreateServerUri_HasEnvironmentParam()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile iface = files.First(f => f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            iface.Content.Contains("string environment = \"api\"", StringComparison.Ordinal),
            "CreateServerUri should have environment param with default 'api'");
    }

    [TestMethod]
    public void CovSpec_Interface_CreateServerUri_HasAllowedValuesDoc()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile iface = files.First(f => f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            iface.Content.Contains("Allowed: api, staging, sandbox", StringComparison.Ordinal),
            "CreateServerUri doc should list allowed values for environment");
    }

    // ── Form-urlencoded with Encoding Object tests ────────────────────────
    [TestMethod]
    public void CovSpec_FormEncoded_ClientEmitsEncodings()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => !f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("private static readonly Dictionary<string, PropertyEncoding>", StringComparison.Ordinal),
            "Client should emit static readonly encodings field for form-urlencoded body with encoding object");
    }

    [TestMethod]
    public void CovSpec_FormEncoded_EncodingHasStyle()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => !f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("Style: \"form\"", StringComparison.Ordinal),
            "Encoding for tags should have Style: form");
    }

    [TestMethod]
    public void CovSpec_FormEncoded_EncodingHasAllowReserved()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => !f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("AllowReserved: true", StringComparison.Ordinal),
            "Encoding for metadata should have AllowReserved: true");
    }

    [TestMethod]
    public void CovSpec_FormEncoded_UsesFormUrlEncodedSerializer()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => !f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("FormUrlEncodedSerializer.Serialize", StringComparison.Ordinal),
            "Client should use FormUrlEncodedSerializer for form-urlencoded body");
    }

    // ── Multipart with Encoding Object tests ──────────────────────────────
    [TestMethod]
    public void CovSpec_Multipart_UsesMultipartSerializer()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => !f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("MultipartFormDataSerializer.Serialize", StringComparison.Ordinal),
            "Client should use MultipartFormDataSerializer for multipart body");
    }

    [TestMethod]
    public void CovSpec_Multipart_HasFileContentTypeEncoding()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => !f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("ContentType: \"application/octet-stream\"", StringComparison.Ordinal),
            "Encoding for file part should have ContentType: application/octet-stream");
    }

    // ── Wildcard media type tests ─────────────────────────────────────────
    [TestMethod]
    public void CovSpec_WildcardResponse_TreatedAsStream()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "DownloadFileResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("OkStream", StringComparison.Ordinal),
            "Wildcard */* response should be treated as stream");
    }

    // ── Response header type branches ─────────────────────────────────────
    [TestMethod]
    public void CovSpec_ResponseHeader_BooleanHeader()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("bool.Parse(rawValue)", StringComparison.Ordinal),
            "Boolean header should use bool.Parse for scalar parsing");
    }

    // ── Default parameter value branches ──────────────────────────────────
    [TestMethod]
    public void CovSpec_DefaultParam_BooleanTrue()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => !f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("ParsedJsonDocument<CovTest.JsonBoolean>.True", StringComparison.Ordinal),
            "Boolean true default should use ParsedJsonDocument<T>.True");
    }

    [TestMethod]
    public void CovSpec_DefaultParam_NullDefault()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => !f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("ParsedJsonDocument<CovTest.JsonString>.Null", StringComparison.Ordinal),
            "Null default should use ParsedJsonDocument<T>.Null");
    }

    // ── Operation-level server override ───────────────────────────────────
    [TestMethod]
    public void CovSpec_OperationServer_Override()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile req = GetFile(files, "GetItemRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("detail.example.com", StringComparison.Ordinal),
            "Request struct should contain the operation-level server URL for getItem");
    }

    // ── Path-level server ─────────────────────────────────────────────────
    [TestMethod]
    public void CovSpec_PathServer_Override()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("items.example.com", StringComparison.Ordinal),
            "Request struct should contain the path-level server URL for listItems");
    }

    // ── Boolean false default parameter ───────────────────────────────────
    [TestMethod]
    public void CovSpec_DefaultParam_BooleanFalse()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => !f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("ParsedJsonDocument<CovTest.JsonBoolean>.False", StringComparison.Ordinal),
            "Boolean false default should use ParsedJsonDocument<T>.False");
    }

    // ── Array response header ────────────────────────────────────────────
    [TestMethod]
    public void CovSpec_ResponseHeader_ArrayHeader_HasProperty()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("XTagsHeader", StringComparison.Ordinal),
            "Response should have XTagsHeader property for array header");
    }

    [TestMethod]
    public void CovSpec_ResponseHeader_ArrayHeader_UsesCreateBuilder()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("CreateBuilder<string>", StringComparison.Ordinal),
            "Array header should use CreateBuilder to parse comma-separated elements");
    }

    // ── Non-conformant parameter edge cases ───────────────────────────────
    [TestMethod]
    public void CovSpec_QuirkyPath_GeneratesWithoutError()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();

        GeneratedFile iface = files.First(f => f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));
        Assert.IsTrue(
            iface.Content.Contains("GetQuirkyAsync", StringComparison.Ordinal),
            "Interface should have GetQuirkyAsync for path param without required:true and unknown 'in' param");
    }

    [TestMethod]
    public void CovSpec_StyledQuirkyPath_GeneratesWithoutError()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();

        GeneratedFile iface = files.First(f => f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));
        Assert.IsTrue(
            iface.Content.Contains("GetStyledQuirkyAsync", StringComparison.Ordinal),
            "Interface should have GetStyledQuirkyAsync for path param with non-standard style");
    }

    [TestMethod]
    public void CovSpec_EmptyServers_GeneratesWithoutError()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();

        GeneratedFile iface = files.First(f => f.FileName.StartsWith("IApi", StringComparison.Ordinal)
            && f.FileName.EndsWith("Client.cs", StringComparison.Ordinal));
        Assert.IsTrue(
            iface.Content.Contains("GetEmptyServersAsync", StringComparison.Ordinal),
            "Interface should have GetEmptyServersAsync even with empty servers arrays");
    }

    // ── Raw string header (no schema) ─────────────────────────────────────
    [TestMethod]
    public void CovSpec_ResponseHeader_NoSchema_RawString()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("XRequestIdHeader", StringComparison.Ordinal),
            "Response should have XRequestIdHeader property for header with no schema");
    }

    [TestMethod]
    public void CovSpec_ResponseHeader_NoSchema_IsStringProperty()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("string? XRequestIdHeader", StringComparison.Ordinal),
            "Header with no schema should be a raw string? property");
    }

    // ── Typed array response headers ─────────────────────────────────────
    [TestMethod]
    public void CovSpec_ResponseHeader_IntArrayEmitsIntParse()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("int.Parse(", StringComparison.Ordinal),
            "Integer array header should emit int.Parse for each element");
    }

    [TestMethod]
    public void CovSpec_ResponseHeader_BoolArrayEmitsBoolParse()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("bool.Parse(", StringComparison.Ordinal),
            "Boolean array header should emit bool.Parse for each element");
    }

    // ── ListOperations tests ──────────────────────────────────────────────
    [TestMethod]
    public void ListOperations_ReturnsPetstoreOperations()
    {
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(petstoreRoot);

        Assert.IsTrue(ops.Length > 0, "Should return at least one operation");
    }

    [TestMethod]
    public void ListOperations_IncludesPathAndMethod()
    {
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(petstoreRoot);

        OperationSummary listPets = ops.First(o => o.OperationId == "listPets");
        Assert.AreEqual("/pets", listPets.Path);
        Assert.AreEqual(OperationMethod.Get, listPets.Method);
    }

    [TestMethod]
    public void ListOperations_IncludesOperationId()
    {
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(petstoreRoot);

        Assert.IsTrue(ops.Any(o => o.OperationId == "createPet"));
        Assert.IsTrue(ops.Any(o => o.OperationId == "showPetById"));
    }

    [TestMethod]
    public void ListOperations_IncludesParameterCount()
    {
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(petstoreRoot);

        OperationSummary listPets = ops.First(o => o.OperationId == "listPets");
        Assert.AreEqual(1, listPets.ParameterCount, "listPets has 1 parameter (limit)");

        OperationSummary showPet = ops.First(o => o.OperationId == "showPetById");
        Assert.AreEqual(1, showPet.ParameterCount, "showPetById has 1 parameter (petId)");
    }

    [TestMethod]
    public void ListOperations_IncludesHasRequestBody()
    {
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(petstoreRoot);

        OperationSummary createPet = ops.First(o => o.OperationId == "createPet");
        Assert.IsTrue(createPet.HasRequestBody, "createPet has a request body");

        OperationSummary listPets = ops.First(o => o.OperationId == "listPets");
        Assert.IsFalse(listPets.HasRequestBody, "listPets has no request body");
    }

    [TestMethod]
    public void ListOperations_WithFilter_OnlyIncludesMatchingPaths()
    {
        OperationFilter filter = new(["/pets"]);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(petstoreRoot, filter);

        Assert.IsTrue(ops.All(o => o.Path == "/pets"), "All operations should be on /pets");
        Assert.IsTrue(ops.Length >= 2, "Should include GET and POST on /pets");
        Assert.IsFalse(ops.Any(o => o.Path.Contains("{petId}", StringComparison.Ordinal)), "Should not include /pets/{petId}");
    }

    [TestMethod]
    public void ListOperations_WithExcludeFilter_ExcludesMatchingPaths()
    {
        OperationFilter filter = new(null, ["/pets/{petId}*"]);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(petstoreRoot, filter);

        Assert.IsFalse(ops.Any(o => o.Path.Contains("{petId}", StringComparison.Ordinal)), "Should exclude /pets/{petId} paths");
        Assert.IsTrue(ops.Any(o => o.Path == "/pets"), "Should include /pets");
    }

    [TestMethod]
    public void ListOperations_NoOperations_ReturnsEmpty()
    {
        const string spec = """
            {
              "openapi": "3.0.0",
              "info": { "title": "Empty", "version": "1.0" },
              "paths": {}
            }
            """;

        JsonElement root = ParseSpec(spec);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(root);

        Assert.AreEqual(0, ops.Length);
    }

    [TestMethod]
    public void ListOperations_DeprecatedOperation()
    {
        const string spec = """
            {
              "openapi": "3.0.0",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/old": {
                  "get": {
                    "operationId": "oldEndpoint",
                    "deprecated": true,
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(root);

        Assert.AreEqual(1, ops.Length);
        Assert.IsTrue(ops[0].IsDeprecated);
    }

    [TestMethod]
    public void ListOperations_OperationWithTags()
    {
        const string spec = """
            {
              "openapi": "3.0.0",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items", "read"],
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(root);

        Assert.AreEqual(1, ops.Length);
        CollectionAssert.AreEqual(new[] { "items", "read" }, ops[0].Tags);
    }

    [TestMethod]
    public void ListOperations_OperationWithSummary()
    {
        const string spec = """
            {
              "openapi": "3.0.0",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "summary": "List all items",
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(root);

        Assert.AreEqual(1, ops.Length);
        Assert.AreEqual("List all items", ops[0].Summary);
    }

    [TestMethod]
    public void ListOperations_NoOperationId_ReturnsNull()
    {
        const string spec = """
            {
              "openapi": "3.0.0",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get": {
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(root);

        Assert.AreEqual(1, ops.Length);
        Assert.IsNull(ops[0].OperationId);
    }

    [TestMethod]
    public void ListOperations_AllHttpMethods()
    {
        const string spec = """
            {
              "openapi": "3.0.0",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items": {
                  "get":     { "operationId": "getItems",     "responses": { "200": { "description": "ok" } } },
                  "post":    { "operationId": "createItem",   "responses": { "200": { "description": "ok" } } },
                  "put":     { "operationId": "replaceItem",  "responses": { "200": { "description": "ok" } } },
                  "delete":  { "operationId": "deleteItem",   "responses": { "200": { "description": "ok" } } },
                  "patch":   { "operationId": "patchItem",    "responses": { "200": { "description": "ok" } } },
                  "options": { "operationId": "optionsItem",  "responses": { "200": { "description": "ok" } } },
                  "head":    { "operationId": "headItem",     "responses": { "200": { "description": "ok" } } },
                  "trace":   { "operationId": "traceItem",    "responses": { "200": { "description": "ok" } } }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(root);

        Assert.AreEqual(8, ops.Length);
        Assert.IsTrue(ops.Any(o => o.Method == OperationMethod.Get));
        Assert.IsTrue(ops.Any(o => o.Method == OperationMethod.Post));
        Assert.IsTrue(ops.Any(o => o.Method == OperationMethod.Put));
        Assert.IsTrue(ops.Any(o => o.Method == OperationMethod.Delete));
        Assert.IsTrue(ops.Any(o => o.Method == OperationMethod.Patch));
        Assert.IsTrue(ops.Any(o => o.Method == OperationMethod.Options));
        Assert.IsTrue(ops.Any(o => o.Method == OperationMethod.Head));
        Assert.IsTrue(ops.Any(o => o.Method == OperationMethod.Trace));
    }

    [TestMethod]
    public void ListOperations_CountsPathLevelParameters()
    {
        const string spec = """
            {
              "openapi": "3.0.0",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/items/{itemId}": {
                  "parameters": [
                    { "name": "itemId", "in": "path", "required": true, "schema": { "type": "string" } }
                  ],
                  "get": {
                    "operationId": "getItem",
                    "parameters": [
                      { "name": "expand", "in": "query", "schema": { "type": "boolean" } }
                    ],
                    "responses": { "200": { "description": "ok" } }
                  },
                  "delete": {
                    "operationId": "deleteItem",
                    "responses": { "204": { "description": "deleted" } }
                  }
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(root);

        // GET: 1 path-level param (itemId) + 1 operation-level param (expand) = 2
        OperationSummary getItem = ops.First(o => o.OperationId == "getItem");
        Assert.AreEqual(2, getItem.ParameterCount, "getItem should count path + operation params");

        // DELETE: 1 path-level param (itemId) + 0 operation-level params = 1
        OperationSummary deleteItem = ops.First(o => o.OperationId == "deleteItem");
        Assert.AreEqual(1, deleteItem.ParameterCount, "deleteItem should count path-level params");
    }

    [TestMethod]
    public void ListOperations_MissingPaths_ReturnsEmpty()
    {
        const string spec = """
            {
              "openapi": "3.0.0",
              "info": { "title": "No paths key", "version": "1.0" }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(root);

        Assert.AreEqual(0, ops.Length);
    }

    [TestMethod]
    public void ListOperations_BrokenPathItemRef_SkipsPath()
    {
        const string spec = """
            {
              "openapi": "3.0.0",
              "info": { "title": "T", "version": "1" },
              "paths": {
                "/ok": {
                  "get": {
                    "operationId": "okOp",
                    "responses": { "200": { "description": "ok" } }
                  }
                },
                "/broken": {
                  "$ref": "#/components/pathItems/doesNotExist"
                }
              }
            }
            """;

        JsonElement root = ParseSpec(spec);
        OperationSummary[] ops = OpenApi30CodeGenerator.ListOperations(root);

        Assert.AreEqual(1, ops.Length);
        Assert.AreEqual("okOp", ops[0].OperationId);
    }

    // ══════════════════════════════════════════════════════════════════
    // External file $ref integration tests
    // ══════════════════════════════════════════════════════════════════

    // Main spec — all $refs point to an external "common.json" file
    private const string ExternalRefSpec30 = """
        {
          "openapi": "3.0.0",
          "info": { "title": "ExternalRefTest", "version": "1.0" },
          "paths": {
            "/items/{id}": {
              "get": {
                "operationId": "getItem",
                "tags": ["items"],
                "parameters": [
                  { "$ref": "./common.json#/components/parameters/ItemId" }
                ],
                "responses": {
                  "200": { "$ref": "./common.json#/components/responses/ItemResponse" }
                }
              },
              "post": {
                "operationId": "createItem",
                "tags": ["items"],
                "requestBody": { "$ref": "./common.json#/components/requestBodies/NewItem" },
                "responses": {
                  "201": {
                    "description": "Created",
                    "content": {
                      "application/json": {
                        "schema": { "type": "object", "properties": { "id": { "type": "string" } } }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    // External document — contains the shared components
    private const string ExternalCommonDoc30 = """
        {
          "components": {
            "parameters": {
              "ItemId": {
                "name": "id",
                "in": "path",
                "required": true,
                "schema": { "type": "string", "format": "uuid" }
              }
            },
            "requestBodies": {
              "NewItem": {
                "required": true,
                "content": {
                  "application/json": {
                    "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                  }
                }
              }
            },
            "responses": {
              "ItemResponse": {
                "description": "An item",
                "content": {
                  "application/json": {
                    "schema": { "type": "object", "properties": { "id": { "type": "string" }, "name": { "type": "string" } } }
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

    private static ExternalReferenceResolver CreateExternalResolver30(
        JsonElement specRoot)
    {
        string fakePath = Path.Combine(Path.GetTempPath(), "test-api", "openapi.json");
        ExternalReferenceResolver resolver = new(specRoot, fakePath);

        using ParsedJsonDocument<JsonElement> commonDoc =
            ParsedJsonDocument<JsonElement>.Parse(ExternalCommonDoc30);
        resolver.AddDocument("./common.json", commonDoc.RootElement.Clone());

        return resolver;
    }

    [TestMethod]
    public void ExternalRef_CollectSchemaPointers_ResolvesParameterFromExternalFile()
    {
        JsonElement spec = ParseSpec(ExternalRefSpec30);
        using ExternalReferenceResolver resolver = CreateExternalResolver30(spec);

        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(
            spec, out _, referenceResolver: resolver).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers,
            "#/paths/~1items~1{id}/get/parameters/0/schema",
            "Parameter schema from external file should be collected at the main spec's pointer location");
    }

    [TestMethod]
    public void ExternalRef_CollectSchemaPointers_ResolvesResponseFromExternalFile()
    {
        JsonElement spec = ParseSpec(ExternalRefSpec30);
        using ExternalReferenceResolver resolver = CreateExternalResolver30(spec);

        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(
            spec, out _, referenceResolver: resolver).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers,
            "#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema",
            "Response schema from external file should be collected");
    }

    [TestMethod]
    public void ExternalRef_CollectSchemaPointers_ResolvesRequestBodyFromExternalFile()
    {
        JsonElement spec = ParseSpec(ExternalRefSpec30);
        using ExternalReferenceResolver resolver = CreateExternalResolver30(spec);

        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(
            spec, out _, referenceResolver: resolver).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers,
            "#/paths/~1items~1{id}/post/requestBody/content/application~1json/schema",
            "RequestBody schema from external file should be collected");
    }

    [TestMethod]
    public void ExternalRef_CollectSchemaPointers_ResolvesHeaderViaChainedRef()
    {
        JsonElement spec = ParseSpec(ExternalRefSpec30);
        using ExternalReferenceResolver resolver = CreateExternalResolver30(spec);

        string[] pointers = [.. OpenApi30CodeGenerator.CollectSchemaPointers(
            spec, out _, referenceResolver: resolver).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(
            pointers,
            "#/paths/~1items~1{id}/get/responses/200/headers/X-Request-Id/schema",
            "Header schema from chained $ref in external file should be collected");
    }

    [TestMethod]
    public void ExternalRef_CollectSchemaPointers_ExtractsParameterNames()
    {
        JsonElement spec = ParseSpec(ExternalRefSpec30);
        using ExternalReferenceResolver resolver = CreateExternalResolver30(spec);

        OpenApi30CodeGenerator.CollectSchemaPointers(
            spec, out Dictionary<string, string> parameterNames, referenceResolver: resolver);

        Assert.IsTrue(
            parameterNames.ContainsKey("/paths/~1items~1{id}/get/parameters/0/schema"),
            "Parameter name should be extracted from externally-resolved parameter");
        Assert.AreEqual(
            "id",
            parameterNames["/paths/~1items~1{id}/get/parameters/0/schema"],
            "Parameter name from external file should be 'id'");
    }

    [TestMethod]
    public void ExternalRef_Generate_ProducesClientCode()
    {
        JsonElement spec = ParseSpec(ExternalRefSpec30);
        using ExternalReferenceResolver resolver = CreateExternalResolver30(spec);

        Dictionary<string, string> schemaMap = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.ItemEntity",
            ["#/paths/~1items~1{id}/get/responses/200/headers/X-Request-Id/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/post/requestBody/content/application~1json/schema"] = "Test.NewItemEntity",
            ["#/paths/~1items~1{id}/post/responses/201/content/application~1json/schema"] = "Test.CreatedItemEntity",
        };

        OpenApi30CodeGenerator gen = new("Test", schemaMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec, referenceResolver: resolver);

        Assert.IsTrue(files.Count > 0, "Should generate at least one file");

        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("GetItemRequest")),
            "Should generate GetItemRequest from externally-resolved parameter");
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("GetItemResponse")),
            "Should generate GetItemResponse from externally-resolved response");
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("CreateItemRequest")),
            "Should generate CreateItemRequest from externally-resolved request body");
    }

    [TestMethod]
    public void ExternalRef_Generate_RequestContainsResolvedParameter()
    {
        JsonElement spec = ParseSpec(ExternalRefSpec30);
        using ExternalReferenceResolver resolver = CreateExternalResolver30(spec);

        Dictionary<string, string> schemaMap = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.ItemEntity",
            ["#/paths/~1items~1{id}/get/responses/200/headers/X-Request-Id/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/post/requestBody/content/application~1json/schema"] = "Test.NewItemEntity",
            ["#/paths/~1items~1{id}/post/responses/201/content/application~1json/schema"] = "Test.CreatedItemEntity",
        };

        OpenApi30CodeGenerator gen = new("Test", schemaMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec, referenceResolver: resolver);

        GeneratedFile requestFile = files.First(f => f.FileName.Contains("GetItemRequest"));
        string code = requestFile.Content;

        Assert.IsTrue(
            code.Contains("id"),
            "Generated request should contain the 'id' parameter from the external file");
    }

    [TestMethod]
    public void ExternalRef_Generate_ResponseContainsResolvedHeader()
    {
        JsonElement spec = ParseSpec(ExternalRefSpec30);
        using ExternalReferenceResolver resolver = CreateExternalResolver30(spec);

        Dictionary<string, string> schemaMap = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.ItemEntity",
            ["#/paths/~1items~1{id}/get/responses/200/headers/X-Request-Id/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/post/requestBody/content/application~1json/schema"] = "Test.NewItemEntity",
            ["#/paths/~1items~1{id}/post/responses/201/content/application~1json/schema"] = "Test.CreatedItemEntity",
        };

        OpenApi30CodeGenerator gen = new("Test", schemaMap);
        IReadOnlyList<GeneratedFile> files = gen.Generate(spec, referenceResolver: resolver);

        GeneratedFile responseFile = files.First(f => f.FileName.Contains("GetItemResponse"));
        string code = responseFile.Content;

        Assert.IsTrue(
            code.Contains("X-Request-Id"),
            "Generated response should contain the header from the external file's chained $ref");
    }

    // ── Response Links tests ──────────────────────────────────────────────
    [TestMethod]
    public void CovSpec_ResponseLinks_CreateItemResponse_HasLinksProperty()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("public CreateItemResponseLinksAccessor Links =>", StringComparison.Ordinal),
            "CreateItemResponse should have a Links property");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_LinksAccessor_HasGetCreatedItemMethod()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("GetCreatedItemAsync(", StringComparison.Ordinal),
            "Links accessor should have GetCreatedItemAsync method");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_LinksAccessor_ReturnsGetItemResponse()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("ValueTask<GetItemResponse>", StringComparison.Ordinal),
            "GetCreatedItemAsync should return ValueTask<GetItemResponse>");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_LinksAccessor_ExtractsBodyProperty()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("this.response.CreatedBody.Id", StringComparison.Ordinal),
            "Link method should extract 'id' from response body via the typed property accessor");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_CreateAsync_HasTransportParam()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("IApiTransport? transport = null,", StringComparison.Ordinal),
            "CreateAsync should accept optional transport parameter");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_CreateAsync_StoresTransport()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("response.transport = transport;", StringComparison.Ordinal),
            "CreateAsync should store the transport on the response");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_HasTransportField()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("private IApiTransport? transport;", StringComparison.Ordinal),
            "Response struct should have private transport field");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_GetItemResponse_HasRefreshItemLink()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("RefreshItemAsync(", StringComparison.Ordinal),
            "GetItemResponse should have RefreshItemAsync link method");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_GetItemResponse_HasSourceRequestField()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("internal GetItemRequest sourceRequest;", StringComparison.Ordinal),
            "GetItemResponse should have sourceRequest field for $request.path expressions");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_RefreshItem_UsesSourceRequest()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("this.response.sourceRequest.ItemId", StringComparison.Ordinal),
            "RefreshItem link should access sourceRequest.ItemId for $request.path.itemId");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_ListItemsResponse_NoLinksProperty()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile resp = GetFile(files, "ListItemsResponse.cs");

        Assert.IsFalse(
            resp.Content.Contains("LinksAccessor", StringComparison.Ordinal),
            "ListItemsResponse should NOT have a Links accessor (no links defined)");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_ClientMethod_UsesLocalAsyncCaptureWhenRequestExpr()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => f.FileName.EndsWith("Client.cs", StringComparison.Ordinal)
            && !f.FileName.StartsWith("IApi", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("return CaptureRequestAsync(", StringComparison.Ordinal),
            "GetItemAsync should use CaptureRequestAsync local function when response has $request.* link expressions");
    }

    [TestMethod]
    public void CovSpec_ResponseLinks_ClientMethod_SetsSourceRequest()
    {
        IReadOnlyList<GeneratedFile> files = GenerateCoverageSpec();
        GeneratedFile client = files.First(f => f.FileName.EndsWith("Client.cs", StringComparison.Ordinal)
            && !f.FileName.StartsWith("IApi", StringComparison.Ordinal));

        Assert.IsTrue(
            client.Content.Contains("response.sourceRequest = request;", StringComparison.Ordinal),
            "CaptureRequestAsync local function should set response.sourceRequest");
    }

    // ── Server generation tests ───────────────────────────────────────────
    private static IReadOnlyList<GeneratedFile> GenerateServerCoverageSpec()
    {
        JsonElement root = GetCoverageRoot();
        OpenApi30CodeGenerator gen = new("CovTest.Server", CoverageSchemaTypeMap);
        return gen.GenerateServer(root);
    }

    [TestMethod]
    public void GenerateServer_ProducesHandlerInterfaces()
    {
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();

        Assert.IsTrue(
            files.Any(f => f.FileName == "IApiDefaultHandler.cs"),
            "Expected IApiDefaultHandler.cs");
    }

    [TestMethod]
    public void GenerateServer_HandlerInterface_ContainsExpectedMethods()
    {
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();
        GeneratedFile defaultHandler = files.First(f => f.FileName == "IApiDefaultHandler.cs");

        Assert.IsTrue(
            defaultHandler.Content.Contains("HandleListItemsAsync"),
            "Expected HandleListItemsAsync in IApiDefaultHandler");
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
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();

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
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();
        GeneratedFile listItemsParams = files.First(f => f.FileName == "ListItemsParams.cs");

        Assert.IsTrue(listItemsParams.Content.Contains("Active"), "Expected Active property");
        Assert.IsTrue(listItemsParams.Content.Contains("Category"), "Expected Category property");
        Assert.IsTrue(listItemsParams.Content.Contains("Page"), "Expected Page property");
        Assert.IsTrue(listItemsParams.Content.Contains("Sort"), "Expected Sort property");
        Assert.IsTrue(listItemsParams.Content.Contains("Verbose"), "Expected Verbose property");

        GeneratedFile createItemParams = files.First(f => f.FileName == "CreateItemParams.cs");
        Assert.IsTrue(createItemParams.Content.Contains("Body"), "Expected Body property");
    }

    [TestMethod]
    public void GenerateServer_ProducesResultStructs()
    {
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();

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
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();
        GeneratedFile listItemsResult = files.First(f => f.FileName == "ListItemsResult.cs");

        Assert.IsTrue(listItemsResult.Content.Contains("Ok("), "Expected Ok() factory method");
        Assert.IsTrue(listItemsResult.Content.Contains("Default("), "Expected Default() factory method");

        GeneratedFile createItemResult = files.First(f => f.FileName == "CreateItemResult.cs");
        Assert.IsTrue(createItemResult.Content.Contains("Created("), "Expected Created() factory method");
    }

    [TestMethod]
    public void GenerateServer_ProducesEndpointRegistration()
    {
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();

        Assert.IsTrue(
            files.Any(f => f.FileName == "ApiEndpointRegistration.cs"),
            "Expected ApiEndpointRegistration.cs");

        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");
        Assert.IsTrue(registration.Content.Contains("MapApiEndpoints"), "Expected MapApiEndpoints");
        Assert.IsTrue(registration.Content.Contains("MapGet"), "Expected MapGet");
        Assert.IsTrue(registration.Content.Contains("MapPost"), "Expected MapPost");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_IncludesAllOperations()
    {
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();
        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        Assert.IsTrue(registration.Content.Contains("\"/items\""), "Expected /items route");
        Assert.IsTrue(registration.Content.Contains("\"/items/{itemId}\""), "Expected /items/{itemId} route");
        Assert.IsTrue(registration.Content.Contains("\"/download\""), "Expected /download route");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_EmitsConfigureEndpointOverload()
    {
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();
        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        Assert.IsTrue(
            registration.Content.Contains("configureEndpoint: null", StringComparison.Ordinal),
            "Expected the original overload to delegate with a null callback");
        Assert.IsTrue(
            registration.Content.Contains(", ConfigureEndpoint? configureEndpoint)", StringComparison.Ordinal),
            "Expected a new MapApiEndpoints overload accepting a ConfigureEndpoint callback");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_EmitsConfigurationTypes()
    {
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();
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
        Assert.IsFalse(
            registration.Content.Contains("Microsoft.AspNetCore.Authorization", StringComparison.Ordinal),
            "Generated registration must not take a dependency on Microsoft.AspNetCore.Authorization");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_InvokesCallbackPerEndpoint()
    {
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();
        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        Assert.IsTrue(
            registration.Content.Contains("configureEndpoint?.Invoke(", StringComparison.Ordinal),
            "Expected configureEndpoint to be invoked per endpoint");
        Assert.IsTrue(
            registration.Content.Contains("methodName: \"ListItems\"", StringComparison.Ordinal),
            "Expected the descriptor to carry the generated method name");
        Assert.IsTrue(
            registration.Content.Contains("httpMethod: \"GET\"", StringComparison.Ordinal),
            "Expected the descriptor to carry the HTTP method");
        Assert.IsTrue(
            registration.Content.Contains("isCallback: false", StringComparison.Ordinal),
            "Expected regular server endpoints to be flagged isCallback: false");

        // OpenAPI 3.0 extracts security: operations without declared security surface an empty list,
        // while the secured listItems operation surfaces its requirements with the resolved scheme type.
        Assert.IsTrue(
            registration.Content.Contains("securityRequirements: System.Array.Empty<EndpointSecurityRequirement>()", StringComparison.Ordinal),
            "Expected unsecured 3.0 endpoints to surface an empty security requirements list");
        Assert.IsTrue(
            registration.Content.Contains("new EndpointSecurityRequirement(\"bearerAuth\", System.Array.Empty<string>(), \"http\")", StringComparison.Ordinal),
            "Expected 3.0 to surface the bearerAuth requirement with its resolved scheme type");
    }

    [TestMethod]
    public void GenerateServer_EndpointRegistration_EmitsRequireDeclaredAuthorizationHelper()
    {
        IReadOnlyList<GeneratedFile> files = GenerateServerCoverageSpec();
        GeneratedFile registration = files.First(f => f.FileName == "ApiEndpointRegistration.cs");

        Assert.IsTrue(
            registration.Content.Contains("public static class EndpointSecurityConventions", StringComparison.Ordinal),
            "Expected the EndpointSecurityConventions helper class to be emitted");
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
        foreach (SchemaReference schemaRef in OpenApi30CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi30CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackServer(callbacksSpecRoot);
        GeneratedFile registration = files.First(f => f.FileName.Contains("EndpointRegistration.cs"));

        Assert.IsTrue(
            registration.Content.Contains("configureEndpoint?.Invoke(", StringComparison.Ordinal),
            "Expected the callback server to invoke configureEndpoint per endpoint");
        Assert.IsTrue(
            registration.Content.Contains("isCallback: true", StringComparison.Ordinal),
            "Expected callback endpoints to be flagged isCallback: true");
        Assert.IsFalse(
            registration.Content.Contains("isCallback: false", StringComparison.Ordinal),
            "Callback server endpoints must not be flagged isCallback: false");
    }

    [TestMethod]
    public void GenerateServer_WithFilter_OnlyIncludesMatchedPaths()
    {
        JsonElement root = GetCoverageRoot();
        OpenApi30CodeGenerator gen = new("CovTest.Server", CoverageSchemaTypeMap);
        OperationFilter filter = new(["/items/**"]);
        IReadOnlyList<GeneratedFile> files = gen.GenerateServer(root, filter);

        Assert.IsTrue(files.Any(f => f.FileName == "ListItemsParams.cs"), "Expected ListItemsParams.cs");
        Assert.IsFalse(files.Any(f => f.FileName == "DownloadFileParams.cs"), "Did not expect DownloadFileParams.cs");
        Assert.IsFalse(files.Any(f => f.FileName == "HeadHealthParams.cs"), "Did not expect HeadHealthParams.cs");
    }

    [TestMethod]
    public void GenerateServer_Petstore_ProducesCorrectFileCount()
    {
        OpenApi30CodeGenerator generator = new("Petstore.Server", PetstoreSchemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateServer(petstoreRoot);

        Assert.IsTrue(files.Count > 0, "Expected at least some generated files");
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("EndpointRegistration.cs")),
            "Expected EndpointRegistration.cs");
        Assert.IsTrue(
            files.Any(f => f.FileName.StartsWith("I") && f.FileName.Contains("Handler.cs")),
            "Expected at least one handler interface");
    }

    [TestMethod]
    public void ListTags_ReturnsTagInfo()
    {
        const string json = """
            {
              "openapi": "3.0.3",
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
                }
              ],
              "paths": {}
            }
            """;

        JsonElement root = ParseSpec(json);
        TagInfo[] tags = OpenApi30CodeGenerator.ListTags(root);

        Assert.AreEqual(3, tags.Length);

        TagInfo products = tags.First(t => t.Name == "products");
        Assert.AreEqual("Products", products.Summary);
        Assert.AreEqual("All product operations", products.Description);
        Assert.IsNull(products.Parent);
        Assert.AreEqual("nav", products.Kind);

        TagInfo cakes = tags.First(t => t.Name == "cakes");
        Assert.AreEqual("Cakes", cakes.Summary);
        Assert.AreEqual("products", cakes.Parent);
        Assert.AreEqual("nav", cakes.Kind);
    }

    [TestMethod]
    public void ListTags_ReturnsEmptyWhenNoTagsArray()
    {
        const string json = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Test", "version": "1.0.0" },
              "paths": {}
            }
            """;

        JsonElement root = ParseSpec(json);
        TagInfo[] tags = OpenApi30CodeGenerator.ListTags(root);

        Assert.AreEqual(0, tags.Length);
    }

    [TestMethod]
    public void ListTags_SkipsTagsWithoutName()
    {
        const string json = """
            {
              "openapi": "3.0.3",
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
        TagInfo[] tags = OpenApi30CodeGenerator.ListTags(root);

        Assert.AreEqual(2, tags.Length);
        Assert.AreEqual("valid", tags[0].Name);
        Assert.AreEqual("also-valid", tags[1].Name);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Callback tests
    // ═══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void WalkWebhookAndCallbackOperations_FindsCallbacks()
    {
        OperationSummary[] ops = OpenApi30CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        Assert.IsTrue(
            ops.Any(o => o.OperationId == "onEventCallback"),
            "Expected onEventCallback from per-operation callbacks");
        Assert.IsTrue(
            ops.Any(o => o.OperationId == "pushNotificationCallback"),
            "Expected pushNotificationCallback from per-operation callbacks");
    }

    [TestMethod]
    public void WalkWebhookAndCallbackOperations_CorrectMethods()
    {
        OperationSummary[] ops = OpenApi30CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        OperationSummary onEvent = ops.First(o => o.OperationId == "onEventCallback");
        Assert.AreEqual(OperationMethod.Post, onEvent.Method);

        OperationSummary pushNotification = ops.First(o => o.OperationId == "pushNotificationCallback");
        Assert.AreEqual(OperationMethod.Post, pushNotification.Method);
    }

    [TestMethod]
    public void WalkWebhookAndCallbackOperations_CorrectTags()
    {
        OperationSummary[] ops = OpenApi30CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        OperationSummary onEvent = ops.First(o => o.OperationId == "onEventCallback");
        CollectionAssert.Contains(onEvent.Tags, "callbacks");

        OperationSummary pushNotification = ops.First(o => o.OperationId == "pushNotificationCallback");
        CollectionAssert.Contains(pushNotification.Tags, "callbacks");
    }

    [TestMethod]
    public void WalkWebhookAndCallbackOperations_CorrectParameterCount()
    {
        OperationSummary[] ops = OpenApi30CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        OperationSummary onEvent = ops.First(o => o.OperationId == "onEventCallback");
        Assert.AreEqual(0, onEvent.ParameterCount, "onEventCallback has no parameters");

        OperationSummary pushNotification = ops.First(o => o.OperationId == "pushNotificationCallback");
        Assert.AreEqual(1, pushNotification.ParameterCount, "pushNotificationCallback has X-Webhook-Secret header parameter");
    }

    [TestMethod]
    public void WalkWebhookAndCallbackOperations_HasRequestBody()
    {
        OperationSummary[] ops = OpenApi30CodeGenerator.ListWebhookAndCallbackOperations(callbacksSpecRoot);

        Assert.IsTrue(ops.All(o => o.HasRequestBody), "All callback operations have request bodies");
    }

    [TestMethod]
    public void CollectWebhookAndCallbackSchemaPointers_FindsCallbackSchemas()
    {
        string[] pointers = [.. OpenApi30CodeGenerator.CollectWebhookAndCallbackSchemaPointers(
            callbacksSpecRoot, out _).Select(r => r.PositionalPointer)];

        Assert.IsTrue(
            pointers.Any(p => p.Contains("request.body", StringComparison.Ordinal) && p.Contains("requestBody", StringComparison.Ordinal)),
            "Expected callback request body schema pointer containing the runtime expression key");
    }

    [TestMethod]
    public void CollectWebhookAndCallbackSchemaPointers_FindsCallbackParameterSchemas()
    {
        string[] pointers = [.. OpenApi30CodeGenerator.CollectWebhookAndCallbackSchemaPointers(
            callbacksSpecRoot, out var parameterNames).Select(r => r.PositionalPointer)];

        string parameterPointer = pointers.Single(p => p.Contains("parameters/0/schema", StringComparison.Ordinal));
        Assert.IsTrue(
            parameterPointer.Contains("request.body", StringComparison.Ordinal),
            "Expected callback parameter schema pointer containing the runtime expression key");
        Assert.AreEqual("X-Webhook-Secret", parameterNames[parameterPointer[1..]]);
    }

    [TestMethod]
    public void GenerateCallbackServer_ProducesFiles()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi30CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi30CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackServer(callbacksSpecRoot);

        Assert.IsTrue(files.Count > 0, "Expected generated callback server files");
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("Handler.cs", StringComparison.Ordinal)),
            "Expected handler interface files");
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("EndpointRegistration.cs", StringComparison.Ordinal)),
            "Expected endpoint registration file");
    }

    [TestMethod]
    public void GenerateCallbackClient_ProducesFiles()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi30CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi30CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackClient(callbacksSpecRoot);

        Assert.IsTrue(files.Count > 0, "Expected generated callback client files");
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("Client.cs", StringComparison.Ordinal)),
            "Expected client class file");
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("Request.cs", StringComparison.Ordinal)),
            "Expected request type files");
        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("Response.cs", StringComparison.Ordinal)),
            "Expected response type files");
    }

    [TestMethod]
    public void GenerateCallbackServer_DoesNotIncludePathsOperations()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi30CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi30CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackServer(callbacksSpecRoot);

        Assert.IsFalse(
            files.Any(f => f.FileName.Contains("CreateSubscription", StringComparison.Ordinal)),
            "Callback server should not include paths operations like createSubscription");
    }

    [TestMethod]
    public void GenerateCallbackClient_DoesNotIncludePathsOperations()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi30CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi30CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackClient(callbacksSpecRoot);

        Assert.IsFalse(
            files.Any(f => f.FileName.Contains("CreateSubscription", StringComparison.Ordinal)),
            "Callback client should not include paths operations like createSubscription");
    }

    [TestMethod]
    public void CollectWebhookAndCallbackSchemaPointers_CallbackResolvablePointerUsesFullPath()
    {
        // Regression test for #779: inline callback schemas must have ResolvablePointers
        // that use the full document path through the parent operation's callbacks map,
        // NOT a broken pointer rooted at #/paths/{callbackKey}/...
        SchemaReference[] refs = OpenApi30CodeGenerator.CollectWebhookAndCallbackSchemaPointers(
            callbacksSpecRoot, out _);

        // Find the callback request body schema reference from /subscriptions
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
    public void GenerateCallbackServer_RuntimeExpressionPathUsesRouteParameter()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);

        foreach (SchemaReference schemaRef in OpenApi30CodeGenerator.CollectWebhookAndCallbackSchemaPointers(callbacksSpecRoot, out _))
        {
            schemaTypeMap[schemaRef.PositionalPointer] = $"Callbacks.Test.{schemaRef.PositionalPointer.GetHashCode():X8}";
        }

        OpenApi30CodeGenerator generator = new("Callbacks.Test", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.GenerateCallbackServer(callbacksSpecRoot);

        GeneratedFile registration = files.First(f => f.FileName.Contains("EndpointRegistration.cs"));

        // Must NOT emit runtime expressions as literal route strings
        Assert.IsFalse(
            registration.Content.Contains("\"{$request", StringComparison.Ordinal),
            "Generated endpoint registration should not emit runtime expressions as literal route strings");

        // Must have a string route parameter for runtime expression paths
        Assert.IsTrue(
            registration.Content.Contains("string ", StringComparison.Ordinal) &&
            registration.Content.Contains("Route", StringComparison.Ordinal),
            "Generated MapApiEndpoints must accept a route parameter for runtime expression paths");
    }
}