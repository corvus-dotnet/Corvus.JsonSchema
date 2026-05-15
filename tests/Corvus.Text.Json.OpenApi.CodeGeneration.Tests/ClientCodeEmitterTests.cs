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
                "SendWithBodyAsyncCore<CreatePetRequest, Petstore.Client.NewPet, CreatePetResponse>",
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
                "SendAsyncCore<ListPetsRequest, ListPetsResponse>",
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
                "SendWithBodyAsyncCore<CreatePetRequest, Petstore.Client.NewPet, CreatePetResponse>",
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

    // Spec with two tags and PUT/DELETE methods to cover multi-tag grouping and
    // OperationMethodExpression for Put/Delete, plus operation summary emission.
    private const string MultiTagSpec = """
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

    [TestMethod]
    public void Emit_MultiTagSpec_ProducesMultipleClientInterfaces()
    {
        ClientModel model = BuildModelFromJson(MultiTagSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1users/put/requestBody/content/application~1json/schema"] = "Test.UserBody",
            ["#/paths/~1users/put/responses/200/content/application~1json/schema"] = "Test.UserResult",
            ["#/paths/~1orders/patch/responses/200/content/application~1json/schema"] = "Test.OrderResult",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        // 3 tags → 3 interface files + 3 implementation files
        Assert.IsTrue(files.Any(f => f.FileName == "IApiUsersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ApiUsersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "IApiOrdersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ApiOrdersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "IApiAdminClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ApiAdminClient.cs"));
    }

    [TestMethod]
    public void Emit_PutMethod_EmitsCorrectOperationMethodExpression()
    {
        ClientModel model = BuildModelFromJson(MultiTagSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1users/put/requestBody/content/application~1json/schema"] = "Test.UserBody",
            ["#/paths/~1users/put/responses/200/content/application~1json/schema"] = "Test.UserResult",
            ["#/paths/~1orders/patch/responses/200/content/application~1json/schema"] = "Test.OrderResult",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile req = GetFile(files, "UpdateUserRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Put", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_DeleteMethod_EmitsCorrectOperationMethodExpression()
    {
        ClientModel model = BuildModelFromJson(MultiTagSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1users/put/requestBody/content/application~1json/schema"] = "Test.UserBody",
            ["#/paths/~1users/put/responses/200/content/application~1json/schema"] = "Test.UserResult",
            ["#/paths/~1orders/patch/responses/200/content/application~1json/schema"] = "Test.OrderResult",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile req = GetFile(files, "DeleteUserRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Delete", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_PatchMethod_EmitsCorrectOperationMethodExpression()
    {
        ClientModel model = BuildModelFromJson(MultiTagSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1users/put/requestBody/content/application~1json/schema"] = "Test.UserBody",
            ["#/paths/~1users/put/responses/200/content/application~1json/schema"] = "Test.UserResult",
            ["#/paths/~1orders/patch/responses/200/content/application~1json/schema"] = "Test.OrderResult",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile req = GetFile(files, "PatchOrderRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Patch", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_HeadMethod_EmitsCorrectOperationMethodExpression()
    {
        ClientModel model = BuildModelFromJson(MultiTagSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1users/put/requestBody/content/application~1json/schema"] = "Test.UserBody",
            ["#/paths/~1users/put/responses/200/content/application~1json/schema"] = "Test.UserResult",
            ["#/paths/~1orders/patch/responses/200/content/application~1json/schema"] = "Test.OrderResult",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile req = GetFile(files, "HeadOrderRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Head", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_OptionsMethod_EmitsCorrectOperationMethodExpression()
    {
        ClientModel model = BuildModelFromJson(MultiTagSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1users/put/requestBody/content/application~1json/schema"] = "Test.UserBody",
            ["#/paths/~1users/put/responses/200/content/application~1json/schema"] = "Test.UserResult",
            ["#/paths/~1orders/patch/responses/200/content/application~1json/schema"] = "Test.OrderResult",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile req = GetFile(files, "OptionsHealthRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Options", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_TraceMethod_EmitsCorrectOperationMethodExpression()
    {
        ClientModel model = BuildModelFromJson(MultiTagSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1users/put/requestBody/content/application~1json/schema"] = "Test.UserBody",
            ["#/paths/~1users/put/responses/200/content/application~1json/schema"] = "Test.UserResult",
            ["#/paths/~1orders/patch/responses/200/content/application~1json/schema"] = "Test.OrderResult",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile req = GetFile(files, "TraceHealthRequest.cs");
        Assert.IsTrue(req.Content.Contains("OperationMethod.Trace", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_OperationWithSummary_EmitsXmlRemarks()
    {
        ClientModel model = BuildModelFromJson(MultiTagSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1users/put/requestBody/content/application~1json/schema"] = "Test.UserBody",
            ["#/paths/~1users/put/responses/200/content/application~1json/schema"] = "Test.UserResult",
            ["#/paths/~1orders/patch/responses/200/content/application~1json/schema"] = "Test.OrderResult",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile req = GetFile(files, "UpdateUserRequest.cs");

        // Summary with XML-escaped content
        Assert.IsTrue(
            req.Content.Contains(
                "/// <remarks>Update a &lt;user&gt; &amp; their profile</remarks>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_CustomClientNamePrefix()
    {
        ClientModel model = BuildModelFromJson(MultiTagSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1users/put/requestBody/content/application~1json/schema"] = "Test.UserBody",
            ["#/paths/~1users/put/responses/200/content/application~1json/schema"] = "Test.UserResult",
            ["#/paths/~1orders/patch/responses/200/content/application~1json/schema"] = "Test.OrderResult",
        };

        ClientCodeEmitter emitter = new("Test", map, "MyService");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        Assert.IsTrue(files.Any(f => f.FileName == "IMyServiceUsersClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "MyServiceUsersClient.cs"));
    }

    [TestMethod]
    public void Emit_SchemaPointerNotInMap_FallsBackToJsonElement()
    {
        // Use empty schema map — no pointers resolve
        Dictionary<string, string> map = new(StringComparer.Ordinal);

        ClientModel model = BuildModelFromJson(DefaultOnlySpec);
        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        // Fallback type is JsonElement
        Assert.IsTrue(
            resp.Content.Contains("JsonElement DefaultBody", StringComparison.Ordinal));
    }

    // Spec with no operationId — forces synthesized method name path
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
    public void Emit_NoOperationId_SynthesizesMethodName()
    {
        ClientModel model = BuildModelFromJson(NoOperationIdSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{itemId}~1details/get/responses/200/content/application~1json/schema"] = "Test.ItemDetail",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        // Synthesized from "get /items/{itemId}/details" → GetItemsItemIdDetails
        Assert.IsTrue(files.Any(f => f.FileName == "GetItemsItemIdDetailsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "GetItemsItemIdDetailsResponse.cs"));
    }

    [TestMethod]
    public void Emit_NoOperationId_SynthesizedNameIncludesHttpMethod()
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

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/delete/parameters/0/schema"] = "Test.JsonString",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        // Synthesized from "delete /items/{id}" → DeleteItemsId
        Assert.IsTrue(files.Any(f => f.FileName == "DeleteItemsIdRequest.cs"));
    }

    [TestMethod]
    public void Emit_CSharpKeywordParameterNames_AreEscapedInConstructor()
    {
        // Keywords used as required path params will appear as constructor parameters
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

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{ref}~1{string}/get/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{ref}~1{string}/get/parameters/1/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{ref}~1{string}/get/responses/200/content/application~1json/schema"] = "Test.Result",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile req = GetFile(files, "SearchRequest.cs");

        // Constructor parameters should be @-escaped for C# keywords
        Assert.IsTrue(req.Content.Contains("@ref", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("@string", StringComparison.Ordinal));

        // Field names should be PascalCase (SanitizeIdentifier)
        Assert.IsTrue(req.Content.Contains("this.Ref = @ref;", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("this.String = @string;", StringComparison.Ordinal));
    }

    // Spec with wildcard status code (2XX)
    private const string WildcardStatusSpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Wildcard", "version": "1.0" },
          "paths": {
            "/items": {
              "get": {
                "operationId": "listItems",
                "tags": ["items"],
                "responses": {
                  "2XX": {
                    "description": "Success",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
          }
        }
        """;

    [TestMethod]
    public void Emit_WildcardStatusCode_UsesStatusNxxNaming()
    {
        ClientModel model = BuildModelFromJson(WildcardStatusSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/responses/2XX/content/application~1json/schema"] = "Test.Items",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile resp = GetFile(files, "ListItemsResponse.cs");

        // 2XX → Status2xx naming
        Assert.IsTrue(resp.Content.Contains("Status2xxBody", StringComparison.Ordinal));
        Assert.IsTrue(resp.Content.Contains("TryGetStatus2xx", StringComparison.Ordinal));
    }

    // Spec with unusual numeric status code (422)
    private const string NumericStatusSpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Numeric", "version": "1.0" },
          "paths": {
            "/items": {
              "post": {
                "operationId": "createItem",
                "tags": ["items"],
                "requestBody": {
                  "required": true,
                  "content": { "application/json": { "schema": { "type": "object" } } }
                },
                "responses": {
                  "201": {
                    "description": "Created",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  },
                  "422": {
                    "description": "Unprocessable",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
          }
        }
        """;

    [TestMethod]
    public void Emit_KnownStatusCode422_UsesSemanticName()
    {
        ClientModel model = BuildModelFromJson(NumericStatusSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/post/requestBody/content/application~1json/schema"] = "Test.NewItem",
            ["#/paths/~1items/post/responses/201/content/application~1json/schema"] = "Test.CreatedItem",
            ["#/paths/~1items/post/responses/422/content/application~1json/schema"] = "Test.ValidationError",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile resp = GetFile(files, "CreateItemResponse.cs");

        // 422 has a known semantic name
        Assert.IsTrue(resp.Content.Contains("UnprocessableEntityBody", StringComparison.Ordinal));
        Assert.IsTrue(resp.Content.Contains("TryGetUnprocessableEntity", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ClientOperation_GetDescription_ReturnsDescription()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "description": "Returns all available items",
                    "tags": ["items"],
                    "responses": {
                      "200": { "description": "Ok" }
                    }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        ClientOperation op = model.Operations[0];

        Assert.AreEqual("Returns all available items", op.GetDescription());
    }

    [TestMethod]
    public void ClientOperation_GetDescription_ReturnsNullWhenMissing()
    {
        ClientModel model = BuildModelFromJson(DefaultOnlySpec);
        ClientOperation op = model.Operations[0];

        Assert.IsNull(op.GetDescription());
    }

    [TestMethod]
    public void ClientOperation_GetTags_ReturnsEmptyWhenNoTags()
    {
        // DefaultOnlySpec has no tags
        ClientModel model = BuildModelFromJson(DefaultOnlySpec);
        ClientOperation op = model.Operations[0];

        Assert.AreEqual(0, op.GetTags().Length);
    }

    [TestMethod]
    public void ClientOperation_GetTags_ReturnsTagStrings()
    {
        ClientModel model = BuildModelFromJson(MultiTagSpec);

        // Find an operation that has tags
        ClientOperation op = model.Operations.First(
            o => o.GetOperationId() == "updateUser");

        string[] tags = op.GetTags();
        Assert.AreEqual(1, tags.Length);
        Assert.AreEqual("users", tags[0]);
    }

    [TestMethod]
    public void ClientModel_GetDefaultServerUrl_ReturnsPetstoreUrl()
    {
        Assert.AreEqual("https://petstore.example.com/v1", petstoreModel.GetDefaultServerUrl());
    }

    [TestMethod]
    public void ClientModel_GetDefaultServerUrl_ReturnsNullWhenNoServers()
    {
        ClientModel model = BuildModelFromJson(DefaultOnlySpec);
        Assert.IsNull(model.GetDefaultServerUrl());
    }

    [TestMethod]
    public void EmitInterface_IncludesDefaultServerUrlUtf8_WhenServerPresent()
    {
        ClientCodeEmitter emitter = CreateEmitter();
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");
        string content = iface.Content;

        StringAssert.Contains(
            content,
            """static ReadOnlySpan<byte> DefaultServerUrlUtf8 => "https://petstore.example.com/v1"u8;""");
        StringAssert.Contains(
            content,
            "/// The default server URL from the OpenAPI specification.");
    }

    [TestMethod]
    public void EmitInterface_OmitsDefaultServerUrlUtf8_WhenNoServers()
    {
        ClientModel model = BuildModelFromJson(DefaultOnlySpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        ClientCodeEmitter emitter = new("Test", map);

        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile iface = GetFile(files, "IApiDefaultClient.cs");

        Assert.IsFalse(
            iface.Content.Contains("DefaultServerUrlUtf8", StringComparison.Ordinal),
            "Should not contain DefaultServerUrlUtf8 when no servers in spec");
    }

    private const string ServerVariablesSpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "VarApi", "version": "1.0" },
          "servers": [
            {
              "url": "https://{host}.example.com/{basePath}",
              "variables": {
                "host": { "default": "api" },
                "basePath": { "default": "v2" }
              }
            }
          ],
          "paths": {
            "/items": {
              "get": {
                "operationId": "listItems",
                "responses": {
                  "200": {
                    "description": "OK"
                  }
                }
              }
            }
          }
        }
        """;

    // ---- Parameter type classification and serialization tests ----
    // This spec exercises all ParameterSerializationKind branches:
    // boolean, int64, float, double, unbounded number (integer no format, number no format),
    // object, array — in path, query, and header locations.
    private const string AllParamTypesSpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "ParamTypes", "version": "1.0" },
          "paths": {
            "/data/{boolId}/{longId}/{floatId}/{doubleId}/{numId}/{objId}/{arrId}": {
              "get": {
                "operationId": "getByAllTypes",
                "tags": ["data"],
                "parameters": [
                  { "name": "boolId", "in": "path", "required": true, "schema": { "type": "boolean" } },
                  { "name": "longId", "in": "path", "required": true, "schema": { "type": "integer", "format": "int64" } },
                  { "name": "floatId", "in": "path", "required": true, "schema": { "type": "number", "format": "float" } },
                  { "name": "doubleId", "in": "path", "required": true, "schema": { "type": "number", "format": "double" } },
                  { "name": "numId", "in": "path", "required": true, "schema": { "type": "integer" } },
                  { "name": "objId", "in": "path", "required": true, "schema": { "type": "object" } },
                  { "name": "arrId", "in": "path", "required": true, "schema": { "type": "array" } },
                  { "name": "qBool", "in": "query", "required": true, "schema": { "type": "boolean" } },
                  { "name": "qLong", "in": "query", "required": true, "schema": { "type": "integer", "format": "int64" } },
                  { "name": "qFloat", "in": "query", "required": true, "schema": { "type": "number", "format": "float" } },
                  { "name": "qDouble", "in": "query", "required": true, "schema": { "type": "number", "format": "double" } },
                  { "name": "qNum", "in": "query", "required": true, "schema": { "type": "number" } },
                  { "name": "qObj", "in": "query", "required": true, "schema": { "type": "object" } },
                  { "name": "qArr", "in": "query", "required": true, "schema": { "type": "array" } },
                  { "name": "qStr", "in": "query", "schema": { "type": "string" } },
                  { "name": "hBool", "in": "header", "required": true, "schema": { "type": "boolean" } },
                  { "name": "hLong", "in": "header", "required": true, "schema": { "type": "integer", "format": "int64" } },
                  { "name": "hFloat", "in": "header", "required": true, "schema": { "type": "number", "format": "float" } },
                  { "name": "hDouble", "in": "header", "required": true, "schema": { "type": "number", "format": "double" } },
                  { "name": "hNum", "in": "header", "required": true, "schema": { "type": "number" } },
                  { "name": "hObj", "in": "header", "required": true, "schema": { "type": "object" } },
                  { "name": "hArr", "in": "header", "required": true, "schema": { "type": "array" } },
                  { "name": "hStr", "in": "header", "schema": { "type": "string" } }
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

    private static IReadOnlyList<GeneratedFile> EmitAllParamTypesFiles()
    {
        ClientModel model = BuildModelFromJson(AllParamTypesSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal);

        const string basePath = "#/paths/~1data~1{boolId}~1{longId}~1{floatId}~1{doubleId}~1{numId}~1{objId}~1{arrId}/get";
        for (int i = 0; i <= 23; i++)
        {
            map[$"{basePath}/parameters/{i}/schema"] = "Test.JsonElement";
        }

        map[$"{basePath}/responses/200/content/application~1json/schema"] = "Test.Result";

        ClientCodeEmitter emitter = new("Test", map);
        return emitter.Emit(model);
    }

    [TestMethod]
    public void Emit_BooleanPathParam_EmitsTernaryTrueFalse()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Boolean path param uses ternary "true"u8 / "false"u8
        Assert.IsTrue(
            req.Content.Contains(
                """writer.Write((bool)this.BoolId ? "true"u8 : "false"u8);""",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_Int64PathParam_EmitsTryFormatWithCorrectBufferSize()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // int64 path param gets 20-byte buffer
        Assert.IsTrue(
            req.Content.Contains("Span<byte> bufLongId = stackalloc byte[20];", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("this.LongId.TryFormat(bufLongId,", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_FloatPathParam_EmitsTryFormatWithCorrectBufferSize()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // float path param gets 32-byte buffer
        Assert.IsTrue(
            req.Content.Contains("Span<byte> bufFloatId = stackalloc byte[32];", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("this.FloatId.TryFormat(bufFloatId,", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_DoublePathParam_EmitsTryFormatWithCorrectBufferSize()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // double path param gets 32-byte buffer
        Assert.IsTrue(
            req.Content.Contains("Span<byte> bufDoubleId = stackalloc byte[32];", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("this.DoubleId.TryFormat(bufDoubleId,", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_UnboundedIntegerPathParam_EmitsGetRawUtf8Value()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Unbounded number (integer with no format) uses GetRawUtf8Value
        Assert.IsTrue(
            req.Content.Contains("JsonMarshal.GetRawUtf8Value(this.NumId)", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("writer.Write(rawNumId.Span);", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ObjectPathParam_EmitsJsonWriterFallback()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Object param uses JSON writer fallback
        Assert.IsTrue(
            req.Content.Contains("using Utf8JsonWriter jwObjId = new(writer);", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("((JsonElement)this.ObjId).WriteTo(jwObjId);", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ArrayPathParam_EmitsJsonWriterFallback()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Array param uses JSON writer fallback
        Assert.IsTrue(
            req.Content.Contains("using Utf8JsonWriter jwArrId = new(writer);", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("((JsonElement)this.ArrId).WriteTo(jwArrId);", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_BooleanQueryParam_EmitsBooleanWriteCounted()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Boolean query param uses counted boolean write
        Assert.IsTrue(
            req.Content.Contains("""bool bv = (bool)this.QBool;""", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("""writer.Write(bv ? "true"u8 : "false"u8);""", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("totalWritten += bv ? 4 : 5;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_Int64QueryParam_EmitsTryFormatCounted()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // int64 query param has buffer and counted write
        Assert.IsTrue(
            req.Content.Contains("Span<byte> bufQLong = stackalloc byte[20];", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("totalWritten += bwQLong;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_FloatQueryParam_EmitsTryFormatCounted()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("Span<byte> bufQFloat = stackalloc byte[32];", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("totalWritten += bwQFloat;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_DoubleQueryParam_EmitsTryFormatCounted()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("Span<byte> bufQDouble = stackalloc byte[32];", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("totalWritten += bwQDouble;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_UnboundedNumberQueryParam_EmitsRawCounted()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Unbounded number query param: GetRawUtf8Value with counted write
        Assert.IsTrue(
            req.Content.Contains("JsonMarshal.GetRawUtf8Value(this.QNum)", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("totalWritten += rawQNum.Span.Length;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_StringQueryParam_EmitsUriEscapeCounted()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Optional string query param uses GetUtf8String with URI escaping
        Assert.IsTrue(
            req.Content.Contains("((JsonElement)QStrValue).GetUtf8String();", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("totalWritten += ewQStr;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ObjectQueryParam_EmitsJsonWriterFallbackCounted()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Object query param uses counted JSON writer fallback
        Assert.IsTrue(
            req.Content.Contains("using Utf8JsonWriter jwQObj = new(writer);", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("totalWritten += (int)jwQObj.BytesCommitted;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ArrayQueryParam_EmitsJsonWriterFallbackCounted()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("using Utf8JsonWriter jwQArr = new(writer);", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("totalWritten += (int)jwQArr.BytesCommitted;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_BooleanHeaderParam_EmitsBooleanHeaderWrite()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Boolean header param uses ternary with callback
        Assert.IsTrue(
            req.Content.Contains(
                """callback(nameUtf8HBool, (bool)this.HBool ? "true"u8 : "false"u8, state);""",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_Int64HeaderParam_EmitsTryFormatWithCallback()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("Span<byte> bufHLong = stackalloc byte[20];", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("callback(nameUtf8HLong, bufHLong[..bwHLong], state);", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_FloatHeaderParam_EmitsTryFormatWithCallback()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("Span<byte> bufHFloat = stackalloc byte[32];", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("callback(nameUtf8HFloat, bufHFloat[..bwHFloat], state);", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_DoubleHeaderParam_EmitsTryFormatWithCallback()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("Span<byte> bufHDouble = stackalloc byte[32];", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("callback(nameUtf8HDouble, bufHDouble[..bwHDouble], state);", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_UnboundedNumberHeaderParam_EmitsRawWithCallback()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("JsonMarshal.GetRawUtf8Value(this.HNum)", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("callback(nameUtf8HNum, rawHNum.Span, state);", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_StringHeaderParam_EmitsStringWriteWithCallback()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // String header emits GetUtf8String + callback with span
        Assert.IsTrue(
            req.Content.Contains("((JsonElement)HStrHeaderValue).GetUtf8String();", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("callback(nameUtf8HStr, utf8HStr.Span, state);", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ObjectHeaderParam_EmitsJsonWriterFallback()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("using Utf8JsonWriter jwHObj = new(writer);", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_RequiredQueryParams_NotWrappedInNullCheck()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("""writer.Write("qBool="u8);""", StringComparison.Ordinal));

        // But optional string param should be wrapped
        Assert.IsTrue(
            req.Content.Contains("if (this.QStr is { } QStrValue)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_OptionalHeaderParams_WrappedInNullCheck()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Optional header param should be null-checked
        Assert.IsTrue(
            req.Content.Contains("if (this.HStr is { } HStrHeaderValue)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_RequiredHeaderParams_EmitNameUtf8Constant()
    {
        IReadOnlyList<GeneratedFile> files = EmitAllParamTypesFiles();
        GeneratedFile req = GetFile(files, "GetByAllTypesRequest.cs");

        // Required header params emit ReadOnlySpan<byte> nameUtf8 constants
        Assert.IsTrue(
            req.Content.Contains("""ReadOnlySpan<byte> nameUtf8HBool = "hBool"u8;""", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("""ReadOnlySpan<byte> nameUtf8HLong = "hLong"u8;""", StringComparison.Ordinal));
    }

    // ---- Response with no content body tests ----
    private const string NoContentResponseSpec = """
        {
          "openapi": "3.1.0",
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
                  "204": { "description": "Deleted" },
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
    public void Emit_ResponseWithNoContent_SkipsBodyProperty()
    {
        ClientModel model = BuildModelFromJson(NoContentResponseSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/delete/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/delete/responses/404/content/application~1json/schema"] = "Test.Error",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "DeleteItemResponse.cs");

        Assert.IsFalse(
            resp.Content.Contains("NoContentBody", StringComparison.Ordinal));

        // 404 does have content
        Assert.IsTrue(
            resp.Content.Contains("NotFoundBody", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ResponseWithNoContent_DoesNotParseBodyInCreateAsync()
    {
        ClientModel model = BuildModelFromJson(NoContentResponseSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/delete/parameters/0/schema"] = "Test.JsonString",
            ["#/paths/~1items~1{id}/delete/responses/404/content/application~1json/schema"] = "Test.Error",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "DeleteItemResponse.cs");

        Assert.IsFalse(
            resp.Content.Contains("statusCode == 204", StringComparison.Ordinal));

        // 404 does have content and should be parsed
        Assert.IsTrue(
            resp.Content.Contains("statusCode == 404", StringComparison.Ordinal));
    }

    // ---- Additional StatusCodeToName coverage ----
    [TestMethod]
    public void Emit_StatusCode301_MapsToMovedPermanently()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "getItem",
                    "tags": ["items"],
                    "responses": {
                      "301": {
                        "description": "Moved",
                        "content": { "application/json": { "schema": { "type": "object" } } }
                      }
                    }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/responses/301/content/application~1json/schema"] = "Test.Redirect",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(resp.Content.Contains("MovedPermanentlyBody", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_UnknownStatusCode_FallsBackToStatusNNN()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "getItem",
                    "tags": ["items"],
                    "responses": {
                      "418": {
                        "description": "I'm a teapot",
                        "content": { "application/json": { "schema": { "type": "object" } } }
                      }
                    }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/responses/418/content/application~1json/schema"] = "Test.Teapot",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(resp.Content.Contains("Status418Body", StringComparison.Ordinal));
        Assert.IsTrue(resp.Content.Contains("TryGetStatus418", StringComparison.Ordinal));
    }

    // ---- ClientModel edge cases ----
    [TestMethod]
    public void ClientModel_TryGetInfoString_ReturnsNullForMissingInfo()
    {
        // Spec with no info block at all — should return null for title/description/version
        const string spec = """
            {
              "openapi": "3.1.0",
              "paths": {}
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Assert.IsNull(model.GetTitle());
        Assert.IsNull(model.GetVersion());
        Assert.IsNull(model.GetDescription());
    }

    // ---- Param with no schema or no type ----
    [TestMethod]
    public void Emit_ParamWithNoSchema_ClassifiesAsString()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "filter", "in": "query", "required": true }
                    ],
                    "responses": {
                      "200": { "description": "Ok" }
                    }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");

        // No schema → classified as string → uses GetUtf8String
        Assert.IsTrue(
            req.Content.Contains("GetUtf8String()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ParamWithNoType_ClassifiesAsString()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "filter", "in": "query", "required": true, "schema": {} }
                    ],
                    "responses": {
                      "200": { "description": "Ok" }
                    }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");

        // Schema with no type → classified as string → uses GetUtf8String
        Assert.IsTrue(
            req.Content.Contains("GetUtf8String()", StringComparison.Ordinal));
    }

    // ---- RequestBody with non-JSON content type ----
    [TestMethod]
    public void Emit_RequestBodyWithNonJsonContent_FallsBackToJsonElement()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/upload": {
                  "post": {
                    "operationId": "upload",
                    "tags": ["files"],
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/octet-stream": {
                          "schema": { "type": "string", "format": "binary" }
                        }
                      }
                    },
                    "responses": {
                      "200": { "description": "Ok" }
                    }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile impl = GetFile(files, "ApiFilesClient.cs");

        // Non-JSON request body falls back to JsonElement
        Assert.IsTrue(
            impl.Content.Contains("JsonElement", StringComparison.Ordinal));
    }

    // ---- Response with non-JSON content type ----
    [TestMethod]
    public void Emit_ResponseWithNonJsonContent_SkipsBodyParsing()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/download": {
                  "get": {
                    "operationId": "download",
                    "tags": ["files"],
                    "responses": {
                      "200": {
                        "description": "Ok",
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

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "DownloadResponse.cs");

        // Non-JSON response content should not produce a typed body property
        Assert.IsFalse(resp.Content.Contains("OkBody", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ClientModel_GetDefaultServerUrl_ReturnsTemplateWithVariables()
    {
        ClientModel model = BuildModelFromJson(ServerVariablesSpec);
        Assert.AreEqual("https://{host}.example.com/{basePath}", model.GetDefaultServerUrl());
    }

    [TestMethod]
    public void EmitInterface_EmitsServerUrlTemplate_WithVariables()
    {
        ClientModel model = BuildModelFromJson(ServerVariablesSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        ClientCodeEmitter emitter = new("Test", map);

        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile iface = GetFile(files, "IApiDefaultClient.cs");

        StringAssert.Contains(
            iface.Content,
            """DefaultServerUrlUtf8 => "https://{host}.example.com/{basePath}"u8;""");
    }

    // ── SanitizeIdentifier special characters ──────────────────────────
    [TestMethod]
    public void Emit_ParamNameWithDash_ConvertsToSanitizedPascalCase()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "x-rate-limit", "in": "header", "required": true, "schema": { "type": "integer", "format": "int32" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/parameters/0/schema"] = "Test.JsonInt32",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");

        // "x-rate-limit" → "XRateLimit" (dashes treated as word separators)
        Assert.IsTrue(req.Content.Contains("XRateLimit", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ParamNameWithDot_ConvertsToSanitizedPascalCase()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "user.name", "in": "query", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/parameters/0/schema"] = "Test.JsonString",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");

        // "user.name" → "UserName" (dots treated as word separators)
        Assert.IsTrue(req.Content.Contains("UserName", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ParamNameWithSpecialChars_StripsNonAlphanumeric()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "item@id#", "in": "query", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/parameters/0/schema"] = "Test.JsonString",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");

        // "item@id#" → "Itemid" (@ and # stripped without word boundary)
        Assert.IsTrue(req.Content.Contains("Itemid", StringComparison.Ordinal));
    }

    // ── Additional status code coverage ────────────────────────────────
    [TestMethod]
    [DataRow("202", "AcceptedBody")]
    [DataRow("304", "NotModifiedBody")]
    [DataRow("400", "BadRequestBody")]
    [DataRow("401", "UnauthorizedBody")]
    [DataRow("403", "ForbiddenBody")]
    [DataRow("405", "MethodNotAllowedBody")]
    [DataRow("409", "ConflictBody")]
    [DataRow("422", "UnprocessableEntityBody")]
    [DataRow("429", "TooManyRequestsBody")]
    [DataRow("500", "InternalServerErrorBody")]
    [DataRow("502", "BadGatewayBody")]
    [DataRow("503", "ServiceUnavailableBody")]
    public void Emit_StatusCode_MapsToExpectedName(string statusCode, string expectedFieldName)
    {
        string spec = $$"""
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "getItem",
                    "tags": ["items"],
                    "responses": {
                      "{{statusCode}}": {
                        "description": "Response",
                        "content": { "application/json": { "schema": { "type": "object" } } }
                      }
                    }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            [$"#/paths/~1items/get/responses/{statusCode}/content/application~1json/schema"] = "Test.ResponseBody",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile resp = GetFile(files, "GetItemResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(expectedFieldName, StringComparison.Ordinal),
            $"Expected response to contain '{expectedFieldName}' for status code {statusCode}");
    }

    // ── ClassifyParameter edge cases ───────────────────────────────────
    [TestMethod]
    public void Emit_IntegerWithUnknownFormat_ClassifiesAsUnboundedNumber()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items/{id}": {
                  "get": {
                    "operationId": "getItem",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "schema": { "type": "integer", "format": "uint32" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}/get/parameters/0/schema"] = "Test.JsonElement",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "GetItemRequest.cs");

        // Unbounded number uses JsonMarshal.GetRawUtf8Value
        Assert.IsTrue(
            req.Content.Contains("JsonMarshal.GetRawUtf8Value", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_NumberWithUnknownFormat_ClassifiesAsUnboundedNumber()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "price", "in": "query", "required": true, "schema": { "type": "number", "format": "decimal" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/parameters/0/schema"] = "Test.JsonElement",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");

        // Unbounded number uses JsonMarshal.GetRawUtf8Value
        Assert.IsTrue(
            req.Content.Contains("JsonMarshal.GetRawUtf8Value", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_UnknownType_ClassifiesAsString()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "weird", "in": "query", "required": true, "schema": { "type": "null" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items/get/parameters/0/schema"] = "Test.JsonElement",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "ListItemsRequest.cs");

        // Unknown type falls back to string classification → GetUtf8String
        Assert.IsTrue(
            req.Content.Contains("GetUtf8String()", StringComparison.Ordinal));
    }

    // ── Path template edge cases ───────────────────────────────────────
    [TestMethod]
    public void Emit_LiteralPathWithNoParams_EmitsStaticWrite()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/health": {
                  "get": {
                    "operationId": "healthCheck",
                    "tags": ["health"],
                    "responses": { "200": { "description": "Healthy" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "HealthCheckRequest.cs");

        // Path with no parameters uses static write of the literal template
        Assert.IsTrue(
            req.Content.Contains("writer.Write(\"/health\"u8);", StringComparison.Ordinal));
    }

    // ── Operations with various HTTP methods (ModelBuilder + Operation coverage) ──
    private const string MultiMethodSpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Multi", "version": "1.0" },
          "paths": {
            "/resources": {
              "options": {
                "operationId": "optionsResources",
                "tags": ["resources"],
                "responses": { "200": { "description": "Ok", "content": { "application/json": { "schema": { "type": "object" } } } } }
              },
              "head": {
                "operationId": "headResources",
                "tags": ["resources"],
                "responses": { "200": { "description": "Ok", "content": { "application/json": { "schema": { "type": "object" } } } } }
              },
              "patch": {
                "operationId": "patchResources",
                "tags": ["resources"],
                "parameters": [
                  { "name": "id", "in": "query", "required": true, "schema": { "type": "string" } }
                ],
                "responses": { "200": { "description": "Ok", "content": { "application/json": { "schema": { "type": "object" } } } } }
              },
              "trace": {
                "operationId": "traceResources",
                "tags": ["resources"],
                "responses": { "200": { "description": "Ok", "content": { "application/json": { "schema": { "type": "object" } } } } }
              },
              "put": {
                "operationId": "putResources",
                "tags": ["resources"],
                "responses": { "200": { "description": "Ok", "content": { "application/json": { "schema": { "type": "object" } } } } }
              },
              "delete": {
                "operationId": "deleteResources",
                "tags": ["resources"],
                "responses": { "200": { "description": "Ok", "content": { "application/json": { "schema": { "type": "object" } } } } }
              }
            }
          }
        }
        """;

    private static Dictionary<string, string> MultiMethodSchemaMap() => new(StringComparer.Ordinal)
    {
        ["#/paths/~1resources/patch/parameters/0/schema"] = "Test.JsonString",
        ["#/paths/~1resources/options/responses/200/content/application~1json/schema"] = "Test.Result",
        ["#/paths/~1resources/head/responses/200/content/application~1json/schema"] = "Test.Result",
        ["#/paths/~1resources/patch/responses/200/content/application~1json/schema"] = "Test.Result",
        ["#/paths/~1resources/trace/responses/200/content/application~1json/schema"] = "Test.Result",
        ["#/paths/~1resources/put/responses/200/content/application~1json/schema"] = "Test.Result",
        ["#/paths/~1resources/delete/responses/200/content/application~1json/schema"] = "Test.Result",
    };

    [TestMethod]
    public void Emit_OptionsMethod_EmitsCorrectOperationMethod()
    {
        ClientModel model = BuildModelFromJson(MultiMethodSpec);
        ClientCodeEmitter emitter = new("Test", MultiMethodSchemaMap());
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "OptionsResourcesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("OperationMethod.Options", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_HeadMethod_EmitsCorrectOperationMethod()
    {
        ClientModel model = BuildModelFromJson(MultiMethodSpec);
        ClientCodeEmitter emitter = new("Test", MultiMethodSchemaMap());
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "HeadResourcesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("OperationMethod.Head", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_PatchMethod_EmitsCorrectOperationMethod()
    {
        ClientModel model = BuildModelFromJson(MultiMethodSpec);
        ClientCodeEmitter emitter = new("Test", MultiMethodSchemaMap());
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "PatchResourcesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("OperationMethod.Patch", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_TraceMethod_EmitsCorrectOperationMethod()
    {
        ClientModel model = BuildModelFromJson(MultiMethodSpec);
        ClientCodeEmitter emitter = new("Test", MultiMethodSchemaMap());
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "TraceResourcesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("OperationMethod.Trace", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_PutMethod_EmitsCorrectOperationMethod()
    {
        ClientModel model = BuildModelFromJson(MultiMethodSpec);
        ClientCodeEmitter emitter = new("Test", MultiMethodSchemaMap());
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "PutResourcesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("OperationMethod.Put", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_DeleteMethod_EmitsCorrectOperationMethod()
    {
        ClientModel model = BuildModelFromJson(MultiMethodSpec);
        ClientCodeEmitter emitter = new("Test", MultiMethodSchemaMap());
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "DeleteResourcesRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("OperationMethod.Delete", StringComparison.Ordinal));
    }

    // ── ClientModel edge cases ─────────────────────────────────────────
    [TestMethod]
    public void ClientModel_GetDefaultServerUrl_ReturnsNullForNoServers()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {}
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Assert.IsNull(model.GetDefaultServerUrl());
    }

    [TestMethod]
    public void ClientModel_GetDefaultServerUrl_ReturnsNullForEmptyServers()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "servers": [],
              "paths": {}
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Assert.IsNull(model.GetDefaultServerUrl());
    }

    // ── ClientRequestBody.GetDescription ───────────────────────────────
    [TestMethod]
    public void Emit_RequestBodyWithDescription_EmitsXmlDoc()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "post": {
                    "operationId": "createItem",
                    "tags": ["items"],
                    "requestBody": {
                      "description": "The item to create",
                      "content": {
                        "application/json": {
                          "schema": { "type": "object" }
                        }
                      }
                    },
                    "responses": { "201": { "description": "Created" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);

        // Verify the model builder captures the requestBody description
        Assert.AreEqual(1, model.Operations.Length);
        Assert.IsNotNull(model.Operations[0].RequestBody);
        Assert.AreEqual("The item to create", model.Operations[0].RequestBody!.Value.GetDescription());
    }

    // ── ClientResponse.GetDescription ──────────────────────────────────
    [TestMethod]
    public void ClientResponse_GetDescription_ReturnsDescriptionWhenPresent()
    {
        ClientModel model = BuildModelFromJson(NoContentResponseSpec);

        // The 204 response has description "Deleted"
        ClientResponse resp204 = model.Operations[0].Responses.First(
            r => r.GetStatusCode() == "204");
        Assert.AreEqual("Deleted", resp204.GetDescription());
    }

    [TestMethod]
    public void ClientResponse_GetDescription_ReturnsNullWhenMissing()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "responses": {
                      "200": {}
                    }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        ClientResponse resp200 = model.Operations[0].Responses.First(
            r => r.GetStatusCode() == "200");
        Assert.IsNull(resp200.GetDescription());
    }

    // ── ClientParameter.GetName fallback ───────────────────────────────
    [TestMethod]
    public void ClientParameter_GetName_ReturnsUnknownWhenNoName()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"],
                    "parameters": [
                      { "in": "query", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Assert.AreEqual("unknown", model.Operations[0].Parameters[0].GetName());
    }

    // ── ModelBuilder: empty responses ───────────────────────────────────
    [TestMethod]
    public void ModelBuilder_EmptyResponses_ProducesEmptyArray()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "tags": ["items"]
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Assert.AreEqual(0, model.Operations[0].Responses.Length);
    }

    // ── TryFormatBufferSize Boolean path (exercised via path param) ────
    [TestMethod]
    public void Emit_BooleanPathParam_UsesBooleanTernary()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items/{active}": {
                  "get": {
                    "operationId": "getByActive",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "active", "in": "path", "required": true, "schema": { "type": "boolean" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{active}/get/parameters/0/schema"] = "Test.JsonBoolean",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "GetByActiveRequest.cs");

        // Boolean path param uses ternary true/false write (not TryFormat)
        Assert.IsTrue(
            req.Content.Contains("\"true\"u8 : \"false\"u8", StringComparison.Ordinal));
    }

    // ── Path template with trailing literal after param ────────────────
    [TestMethod]
    public void Emit_PathWithTrailingLiteral_EmitsTrailingSegment()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items/{id}/details": {
                  "get": {
                    "operationId": "getItemDetails",
                    "tags": ["items"],
                    "parameters": [
                      { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1items~1{id}~1details/get/parameters/0/schema"] = "Test.JsonString",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);
        GeneratedFile req = GetFile(files, "GetItemDetailsRequest.cs");

        // After the {id} param write, "/details" is written as a literal trailing segment
        Assert.IsTrue(
            req.Content.Contains("writer.Write(\"/details\"u8);", StringComparison.Ordinal));
    }

    // ── ClientModel.GetDefaultServerUrl with malformed first server entry ──
    [TestMethod]
    public void ClientModel_GetDefaultServerUrl_ReturnsNullForServerWithNoUrl()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "servers": [ { "description": "A server with no url" } ],
              "paths": {}
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Assert.IsNull(model.GetDefaultServerUrl());
    }

    // ── Synthesized operation name (no operationId) ────────────────────
    [TestMethod]
    public void ClientOperation_SynthesizedName_WhenNoOperationId()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/pets/{petId}": {
                  "get": {
                    "tags": ["pets"],
                    "parameters": [
                      { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } }
                    ],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Assert.AreEqual(1, model.Operations.Length);

        // No operationId → synthesized from method + path: "get /pets/{petId}" → "GetPetsPetid"
        string methodName = model.Operations[0].GetMethodName();
        StringAssert.StartsWith(methodName, "Get");
        Assert.IsTrue(methodName.Contains("Pets", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ClientOperation_SynthesizedName_VariousMethods()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "post": {
                    "tags": ["items"],
                    "responses": { "200": { "description": "Ok" } }
                  },
                  "put": {
                    "tags": ["items"],
                    "responses": { "200": { "description": "Ok" } }
                  },
                  "delete": {
                    "tags": ["items"],
                    "responses": { "200": { "description": "Ok" } }
                  },
                  "options": {
                    "tags": ["items"],
                    "responses": { "200": { "description": "Ok" } }
                  },
                  "head": {
                    "tags": ["items"],
                    "responses": { "200": { "description": "Ok" } }
                  },
                  "patch": {
                    "tags": ["items"],
                    "responses": { "200": { "description": "Ok" } }
                  },
                  "trace": {
                    "tags": ["items"],
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);

        // All operations should have synthesized names from method + path
        foreach (ClientOperation op in model.Operations)
        {
            string name = op.GetMethodName();
            Assert.IsTrue(name.Contains("Items", StringComparison.Ordinal),
                $"Expected method name '{name}' to contain 'Items'");
        }
    }

    // ── Empty requestBody content ──────────────────────────────────────
    [TestMethod]
    public void ModelBuilder_RequestBodyWithEmptyContent_ProducesEmptyContentArray()
    {
        const string spec = """
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "post": {
                    "operationId": "createItem",
                    "tags": ["items"],
                    "requestBody": {
                      "description": "Empty body",
                      "content": {}
                    },
                    "responses": { "200": { "description": "Ok" } }
                  }
                }
              }
            }
            """;

        ClientModel model = BuildModelFromJson(spec);
        Assert.IsNotNull(model.Operations[0].RequestBody);
        Assert.AreEqual(0, model.Operations[0].RequestBody!.Value.Content.Length);
    }

    // ── Path-level parameters ──────────────────────────────────────────
    private const string PathLevelParamsSpec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "PathLevel", "version": "1.0" },
          "paths": {
            "/orders/{orderId}": {
              "parameters": [
                {
                  "name": "orderId",
                  "in": "path",
                  "required": true,
                  "schema": { "type": "string", "format": "uuid" }
                },
                {
                  "name": "X-Trace-Id",
                  "in": "header",
                  "schema": { "type": "string" }
                }
              ],
              "get": {
                "operationId": "getOrder",
                "tags": ["orders"],
                "parameters": [
                  {
                    "name": "fields",
                    "in": "query",
                    "schema": { "type": "string" }
                  }
                ],
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              },
              "put": {
                "operationId": "updateOrder",
                "tags": ["orders"],
                "parameters": [
                  {
                    "name": "X-Trace-Id",
                    "in": "header",
                    "required": true,
                    "schema": { "type": "string", "format": "uuid" }
                  }
                ],
                "requestBody": {
                  "required": true,
                  "content": { "application/json": { "schema": { "type": "object" } } }
                },
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": { "application/json": { "schema": { "type": "object" } } }
                  }
                }
              }
            }
          }
        }
        """;

    [TestMethod]
    public void PathLevel_GetOperationInheritsPathParameters()
    {
        ClientModel model = BuildModelFromJson(PathLevelParamsSpec);

        ClientOperation getOp = model.Operations.First(o => o.GetMethodName() == "GetOrder");
        Assert.AreEqual(3, getOp.Parameters.Length);
    }

    [TestMethod]
    public void PathLevel_PutOperationOverridesPathParameter()
    {
        ClientModel model = BuildModelFromJson(PathLevelParamsSpec);

        ClientOperation putOp = model.Operations.First(o => o.GetMethodName() == "UpdateOrder");

        // PUT inherits orderId (path), overrides X-Trace-Id (header) = 2
        Assert.AreEqual(2, putOp.Parameters.Length);
    }

    [TestMethod]
    public void PathLevel_PathLevelParamUsesPathLevelPointer()
    {
        ClientModel model = BuildModelFromJson(PathLevelParamsSpec);

        ClientOperation getOp = model.Operations.First(o => o.GetMethodName() == "GetOrder");

        // orderId is path-level — pointer omits HTTP method
        ClientParameter orderId = getOp.Parameters.First(p => p.GetName() == "orderId");
        Assert.IsTrue(
            orderId.SchemaPointer!.Contains("/parameters/0/schema", StringComparison.Ordinal),
            $"Expected path-level pointer, got: {orderId.SchemaPointer}");
        Assert.IsFalse(
            orderId.SchemaPointer!.Contains("/get/", StringComparison.Ordinal),
            $"Path-level pointer should not contain method segment: {orderId.SchemaPointer}");
    }

    [TestMethod]
    public void PathLevel_OperationLevelParamUsesOperationPointer()
    {
        ClientModel model = BuildModelFromJson(PathLevelParamsSpec);

        ClientOperation getOp = model.Operations.First(o => o.GetMethodName() == "GetOrder");

        // fields is operation-level — pointer includes HTTP method
        ClientParameter fields = getOp.Parameters.First(p => p.GetName() == "fields");
        Assert.IsTrue(
            fields.SchemaPointer!.Contains("/get/parameters/", StringComparison.Ordinal),
            $"Expected operation-level pointer with method, got: {fields.SchemaPointer}");
    }

    [TestMethod]
    public void PathLevel_OverriddenParamUsesOperationPointer()
    {
        ClientModel model = BuildModelFromJson(PathLevelParamsSpec);

        ClientOperation putOp = model.Operations.First(o => o.GetMethodName() == "UpdateOrder");

        // X-Trace-Id is overridden — should use operation-level pointer
        ClientParameter traceId = putOp.Parameters.First(p => p.GetName() == "X-Trace-Id");
        Assert.IsTrue(
            traceId.SchemaPointer!.Contains("/put/parameters/", StringComparison.Ordinal),
            $"Expected operation-level pointer for override, got: {traceId.SchemaPointer}");
    }

    [TestMethod]
    public void PathLevel_EmittedRequestIncludesInheritedParams()
    {
        ClientModel model = BuildModelFromJson(PathLevelParamsSpec);
        Dictionary<string, string> map = new(StringComparer.Ordinal)
        {
            ["#/paths/~1orders~1{orderId}/parameters/0/schema"] = "Test.OrderId",
            ["#/paths/~1orders~1{orderId}/parameters/1/schema"] = "Test.TraceId",
            ["#/paths/~1orders~1{orderId}/get/parameters/0/schema"] = "Test.Fields",
            ["#/paths/~1orders~1{orderId}/put/parameters/0/schema"] = "Test.UuidTraceId",
            ["#/paths/~1orders~1{orderId}/put/requestBody/content/application~1json/schema"] = "Test.OrderUpdate",
            ["#/paths/~1orders~1{orderId}/get/responses/200/content/application~1json/schema"] = "Test.Order",
            ["#/paths/~1orders~1{orderId}/put/responses/200/content/application~1json/schema"] = "Test.Order",
        };

        ClientCodeEmitter emitter = new("Test", map);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(model);

        GeneratedFile getReq = GetFile(files, "GetOrderRequest.cs");

        // Should have all 3 params: orderId (path), X-Trace-Id (header), fields (query)
        Assert.IsTrue(
            getReq.Content.Contains("Test.OrderId OrderId", StringComparison.Ordinal),
            "Missing inherited path param orderId");
        Assert.IsTrue(
            getReq.Content.Contains("Test.TraceId XTraceId", StringComparison.Ordinal),
            "Missing inherited header param X-Trace-Id");
        Assert.IsTrue(
            getReq.Content.Contains("Test.Fields Fields", StringComparison.Ordinal),
            "Missing operation-level query param fields");
    }
}