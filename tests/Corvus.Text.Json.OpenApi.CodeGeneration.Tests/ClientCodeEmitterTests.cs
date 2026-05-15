// <copyright file="ClientCodeEmitterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi31;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class ClientCodeEmitterTests
{
    private static ClientModel petstoreModel;
    private static Dictionary<string, string> petstoreSchemaTypeMap = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "petstore-3.1.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement.Clone();

        petstoreModel = ClientModelBuilder.Build(root, new OpenApi31Walker());

        // Complete type map: every schema pointer the walker discovers must be mapped.
        petstoreSchemaTypeMap = new(StringComparer.Ordinal)
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
        };
    }

    private static ClientCodeEmitter CreateEmitter(
        IReadOnlyDictionary<string, string>? schemaTypeMap = null,
        string? clientNamePrefix = null)
        => new("Petstore.Client", schemaTypeMap ?? petstoreSchemaTypeMap, clientNamePrefix);

    private static GeneratedFile GetFile(IReadOnlyList<GeneratedFile> files, string name)
        => files.First(f => f.FileName == name);

    [TestMethod]
    public void Emit_ProducesCorrectFileCount()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        // 3 operations × 2 (request + response) + 1 interface + 1 implementation = 8
        Assert.AreEqual(8, files.Count);
    }

    [TestMethod]
    public void Emit_ProducesRequestFiles()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        Assert.IsTrue(files.Any(f => f.FileName == "ListPetsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "CreatePetRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ShowPetByIdRequest.cs"));
    }

    [TestMethod]
    public void Emit_ProducesResponseFiles()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        Assert.IsTrue(files.Any(f => f.FileName == "ListPetsResponse.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "CreatePetResponse.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ShowPetByIdResponse.cs"));
    }

    [TestMethod]
    public void Emit_InterfaceFileName()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        Assert.IsTrue(files.Any(f => f.FileName == "IApiPetsClient.cs"));
    }

    [TestMethod]
    public void Emit_ImplementationFileName()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        Assert.IsTrue(files.Any(f => f.FileName == "ApiPetsClient.cs"));
    }

    [TestMethod]
    public void Emit_InterfaceContainsNamespace()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");
        Assert.IsTrue(iface.Content.Contains("namespace Petstore.Client;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceContainsAllOperationMethods()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(iface.Content.Contains("ListPetsAsync", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("CreatePetAsync", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("ShowPetByIdAsync", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceMethodsReturnTypedResponses()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("ValueTask<ListPetsResponse>", StringComparison.Ordinal));
        Assert.IsTrue(
            iface.Content.Contains("ValueTask<CreatePetResponse>", StringComparison.Ordinal));
        Assert.IsTrue(
            iface.Content.Contains("ValueTask<ShowPetByIdResponse>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ImplementationContainsTransportField()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");
        Assert.IsTrue(
            impl.Content.Contains(
                "private readonly IApiTransport transport;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ImplementationContainsConstructor()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");
        Assert.IsTrue(
            impl.Content.Contains(
                "public ApiPetsClient(IApiTransport transport)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_RequestStructImplementsIApiRequest()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");
        Assert.IsTrue(
            req.Content.Contains(
                ": IApiRequest<ListPetsRequest>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ResponseStructImplementsIApiResponse()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");
        Assert.IsTrue(
            resp.Content.Contains(
                ": IApiResponse<ListPetsResponse>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_RequestHasStaticPathTemplate()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");
        Assert.IsTrue(
            req.Content.Contains(
                "PathTemplateUtf8 => \"/pets\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_RequestHasStaticMethod()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");
        Assert.IsTrue(
            req.Content.Contains(
                "Method => OperationMethod.Get", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ListPetsRequestHasQueryParameters()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("HasQueryParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("HasPathParameters => false", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ListPetsRequestHasTypedLimitField()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        // limit is optional integer/int32, so the type should be nullable JsonInt32
        Assert.IsTrue(
            req.Content.Contains("Petstore.Client.JsonInt32? Limit { get; init; }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ListPetsRequestWriteQueryStringEmitsLimit()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        // Int32 query param uses TryFormat with an 11-byte buffer.
        Assert.IsTrue(
            req.Content.Contains("stackalloc byte[11]", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains(".TryFormat(buf", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("\"limit=\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ShowPetByIdRequestPathWritesUriEscapedString()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        // String path param uses GetUtf8String + TryEscapeDataString.
        Assert.IsTrue(
            req.Content.Contains("GetUtf8String()", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("Utf8Uri.TryEscapeDataString(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ShowPetByIdRequestHasTypedPathParameter()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("HasPathParameters => true", StringComparison.Ordinal));

        // petId is required string, so the type should be Petstore.Client.JsonString
        Assert.IsTrue(
            req.Content.Contains("Petstore.Client.JsonString PetId { get; init; }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ShowPetByIdRequestWriteResolvedPathEmitsSegments()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        // Should write the literal path prefix, then the parameter
        Assert.IsTrue(
            req.Content.Contains("WriteResolvedPath", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("\"/pets/\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ShowPetByIdRequestPathTemplateIncludesParameter()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "PathTemplateUtf8 => \"/pets/{petId}\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_CreatePetRequestHasPostMethod()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "CreatePetRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "Method => OperationMethod.Post", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ResponseHasStatusCodeProperty()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("StatusCode { get;", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("IsSuccess =>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ResponseHasCreateAsyncFactory()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "static async ValueTask<ListPetsResponse> CreateAsync",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ResponseHasDisposeAsync()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "public async ValueTask DisposeAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_CreatePetClientUsesTypedBodySendAsync()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("Petstore.Client.NewPet.Source body", StringComparison.Ordinal));
        Assert.IsTrue(
            impl.Content.Contains(
                "SendAsync<CreatePetRequest, Petstore.Client.NewPet, CreatePetResponse>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ListPetsClientUsesNoBodySendAsync()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "SendAsync<ListPetsRequest, ListPetsResponse>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_UsesTypedBody()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("Petstore.Client.NewPet.Source body", StringComparison.Ordinal));
        Assert.IsTrue(
            impl.Content.Contains(
                "SendAsync<CreatePetRequest, Petstore.Client.NewPet, CreatePetResponse>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceUsesTypedBody()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("Petstore.Client.NewPet.Source body", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_UnmappedPointerFallsBackToJsonElement()
    {
        // A map that doesn't contain the createPet request body pointer
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            ["#/components/schemas/SomethingElse"] = "Petstore.Client.SomethingElse",
        };

        ClientCodeEmitter emitter = CreateEmitter(schemaTypeMap: schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("JsonElement.Source body", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ContainsAutoGeneratedHeader()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        foreach (GeneratedFile file in files)
        {
            Assert.IsTrue(
                file.Content.Contains("<auto-generated>", StringComparison.Ordinal),
                $"File {file.FileName} missing auto-generated header");
        }
    }

    [TestMethod]
    public void Emit_WithCustomClientNamePrefix()
    {
        ClientCodeEmitter emitter = CreateEmitter(clientNamePrefix: "Petstore");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        Assert.IsTrue(files.Any(f => f.FileName == "IPetstorePetsClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "PetstorePetsClient.cs"));
    }

    [TestMethod]
    public void Emit_ImplementationImplementsInterface()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");
        Assert.IsTrue(
            impl.Content.Contains(": IApiPetsClient", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ImplementsDisposePattern()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");
        Assert.IsTrue(
            impl.Content.Contains(
                "public ValueTask DisposeAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceExtendsIAsyncDisposable()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");
        Assert.IsTrue(
            iface.Content.Contains("IAsyncDisposable", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_MethodsHaveXmlDocs()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("List all pets", StringComparison.Ordinal));
        Assert.IsTrue(
            iface.Content.Contains("Create a pet", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_RequestHasWriteHeadersMethod()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");
        Assert.IsTrue(
            req.Content.Contains("WriteHeaders", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_AllFilesContainNamespace()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        foreach (GeneratedFile file in files)
        {
            Assert.IsTrue(
                file.Content.Contains("namespace Petstore.Client;", StringComparison.Ordinal),
                $"File {file.FileName} missing namespace");
        }
    }

    [TestMethod]
    public void Emit_WithSchemaTypeMap_ResponseUsesTypedBody()
    {
        const string pointer = "#/paths/~1pets/get/responses/200/content/application~1json/schema";

        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            [pointer] = "Petstore.Client.Pets",
        };

        ClientCodeEmitter emitter = new("Petstore.Client", schemaTypeMap: schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        // The 200 response body should use the resolved type
        Assert.IsTrue(
            resp.Content.Contains("Petstore.Client.Pets OkBody", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("TryGetOk", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ResponseHasDefaultErrorAccessor()
    {
        const string pointer =
            "#/paths/~1pets/get/responses/default/content/application~1json/schema";

        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            [pointer] = "Petstore.Client.Error",
        };

        ClientCodeEmitter emitter = new("Petstore.Client", schemaTypeMap: schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains("Petstore.Client.Error DefaultBody", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("TryGetDefault", StringComparison.Ordinal));
    }

    // ---- Source-based client API signature tests ----
    [TestMethod]
    public void Emit_InterfaceUsesSourceTypeForOptionalParam()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        // ListPetsAsync has optional limit: Source type with default
        Assert.IsTrue(
            iface.Content.Contains(
                "Petstore.Client.JsonInt32.Source limit = default",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceUsesSourceTypeForRequiredParam()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        // ShowPetByIdAsync has required petId: Source type, no default
        Assert.IsTrue(
            iface.Content.Contains(
                "Petstore.Client.JsonString.Source petId",
                StringComparison.Ordinal));
        Assert.IsFalse(
            iface.Content.Contains(
                "Petstore.Client.JsonString.Source petId =",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ClientMethodCreatesWorkspaceForParams()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        // Methods with parameters should create an unrented workspace
        Assert.IsTrue(
            impl.Content.Contains(
                "JsonWorkspace.CreateUnrented()",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ClientMethodDisposesWorkspace()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("workspace.Dispose()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ClientMethodMaterialisesRequiredParamViaCreateBuilder()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        // ShowPetById has required petId — should materialise directly
        Assert.IsTrue(
            impl.Content.Contains(
                "Petstore.Client.JsonString.CreateBuilder(workspace, petId).RootElement",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ClientMethodGuardsOptionalParamWithIsUndefined()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        // ListPets has optional limit — should guard with IsUndefined
        Assert.IsTrue(
            impl.Content.Contains(
                "limit.IsUndefined",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_BodyMaterialisedViaCreateBuilder()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        // CreatePet body is materialised via CreateBuilder, same as parameters
        Assert.IsTrue(
            impl.Content.Contains(
                "Petstore.Client.NewPet.CreateBuilder(workspace, body).RootElement",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_BodyMethodPassesMaterialisedValueToSendAsync()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        // The materialised bodyValue (not the Source) is passed to SendAsync
        Assert.IsTrue(
            impl.Content.Contains("bodyValue, cancellationToken", StringComparison.Ordinal));
    }

    // ---- Response TryGetDefault and MatchResult tests ----
    private static ClientModel BuildModelFromJson(string json)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement.Clone();
        return ClientModelBuilder.Build(root, new OpenApi31Walker());
    }

    // Spec: multiple non-default responses (200 + 201) plus a default
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

    // Spec: no non-default responses, only a default
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

    // Spec: no default response at all, only specific codes (200 + 404)
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
    public void Emit_TryGetDefault_MultipleNonDefault_ChecksAllSpecificCodes()
    {
        ClientModel model = BuildModelFromJson(MultiStatusSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/post/responses/200/content/application~1json/schema"] = "Test.OkBody",
            ["#/paths/~1items/post/responses/201/content/application~1json/schema"] = "Test.CreatedBody",
            ["#/paths/~1items/post/responses/default/content/application~1json/schema"] = "Test.ErrorBody",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        // TryGetDefault must check that StatusCode is not any of the specific codes
        Assert.IsTrue(
            resp.Content.Contains(
                "this.StatusCode != 200 && this.StatusCode != 201",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_TryGetDefault_DefaultOnly_AlwaysReturnsTrue()
    {
        ClientModel model = BuildModelFromJson(DefaultOnlySpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/responses/default/content/application~1json/schema"] = "Test.AnyBody",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        // With no specific status codes, TryGetDefault should unconditionally return true
        Assert.IsTrue(
            resp.Content.Contains("result = this.DefaultBody;", StringComparison.Ordinal));

        // Should NOT contain any StatusCode != check
        Assert.IsFalse(
            resp.Content.Contains("this.StatusCode !=", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_TryGetDefault_NoDefault_NotEmitted()
    {
        ClientModel model = BuildModelFromJson(NoDefaultSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.ItemBody",
            ["#/paths/~1items~1{id}/get/responses/404/content/application~1json/schema"] = "Test.NotFoundBody",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "GetItemByIdResponse.cs");

        // No TryGetDefault method when there is no default response
        Assert.IsFalse(
            resp.Content.Contains("TryGetDefault", StringComparison.Ordinal));

        // But specific TryGet methods should exist
        Assert.IsTrue(
            resp.Content.Contains("TryGetOk", StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains("TryGetNotFound", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_MatchResult_MultipleNonDefault_HasAllHandlers()
    {
        ClientModel model = BuildModelFromJson(MultiStatusSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/post/responses/200/content/application~1json/schema"] = "Test.OkBody",
            ["#/paths/~1items/post/responses/201/content/application~1json/schema"] = "Test.CreatedBody",
            ["#/paths/~1items/post/responses/default/content/application~1json/schema"] = "Test.ErrorBody",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        // Non-context overload: one handler per specific code, plus default
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
    public void Emit_MatchResult_MultipleNonDefault_ContextOverloadHasAllowsRefStruct()
    {
        ClientModel model = BuildModelFromJson(MultiStatusSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/post/responses/200/content/application~1json/schema"] = "Test.OkBody",
            ["#/paths/~1items/post/responses/201/content/application~1json/schema"] = "Test.CreatedBody",
            ["#/paths/~1items/post/responses/default/content/application~1json/schema"] = "Test.ErrorBody",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        // Context overload must have allows ref struct constraint
        Assert.IsTrue(
            resp.Content.Contains(
                "where TContext : allows ref struct",
                StringComparison.Ordinal));

        // Context overload uses ResponseMatcher with TContext
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.OkBody, TContext, TResult> matchOk",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_MatchResult_MultipleNonDefault_BranchesOnStatusCode()
    {
        ClientModel model = BuildModelFromJson(MultiStatusSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/post/responses/200/content/application~1json/schema"] = "Test.OkBody",
            ["#/paths/~1items/post/responses/201/content/application~1json/schema"] = "Test.CreatedBody",
            ["#/paths/~1items/post/responses/default/content/application~1json/schema"] = "Test.ErrorBody",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        // Body dispatches to the correct handler
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
    public void Emit_MatchResult_DefaultOnly_SingleHandler()
    {
        ClientModel model = BuildModelFromJson(DefaultOnlySpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/responses/default/content/application~1json/schema"] = "Test.AnyBody",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        // Only one handler: matchDefault
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.AnyBody, TResult> matchDefault)",
                StringComparison.Ordinal));

        // No matchOk or matchCreated etc.
        Assert.IsFalse(
            resp.Content.Contains("matchOk", StringComparison.Ordinal));

        // Body immediately returns matchDefault
        Assert.IsTrue(
            resp.Content.Contains(
                "return matchDefault(this.DefaultBody);",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_MatchResult_NoDefault_UsesStatusCodeFallback()
    {
        ClientModel model = BuildModelFromJson(NoDefaultSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.ItemBody",
            ["#/paths/~1items~1{id}/get/responses/404/content/application~1json/schema"] = "Test.NotFoundBody",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "GetItemByIdResponse.cs");

        // Specific handlers exist
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.ItemBody, TResult> matchOk",
                StringComparison.Ordinal));
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<Test.NotFoundBody, TResult> matchNotFound",
                StringComparison.Ordinal));

        // Fallback handler takes int (status code), not a typed body
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<int, TResult> matchDefault)",
                StringComparison.Ordinal));

        // Fallback dispatches with StatusCode
        Assert.IsTrue(
            resp.Content.Contains(
                "return matchDefault(this.StatusCode);",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_MatchResult_NoDefault_ContextOverloadUsesStatusCodeFallback()
    {
        ClientModel model = BuildModelFromJson(NoDefaultSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/get/responses/200/content/application~1json/schema"] = "Test.ItemBody",
            ["#/paths/~1items~1{id}/get/responses/404/content/application~1json/schema"] = "Test.NotFoundBody",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "GetItemByIdResponse.cs");

        // Context overload fallback handler takes int + TContext
        Assert.IsTrue(
            resp.Content.Contains(
                "ResponseMatcher<int, TContext, TResult> matchDefault)",
                StringComparison.Ordinal));

        // And has the allows ref struct constraint
        Assert.IsTrue(
            resp.Content.Contains(
                "where TContext : allows ref struct",
                StringComparison.Ordinal));
    }
}