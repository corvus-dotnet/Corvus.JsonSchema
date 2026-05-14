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

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "petstore-3.1.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement.Clone();

        petstoreModel = ClientModelBuilder.Build(root, new OpenApi31Walker());
    }

    private static GeneratedFile GetFile(IReadOnlyList<GeneratedFile> files, string name)
        => files.First(f => f.FileName == name);

    [TestMethod]
    public void Emit_ProducesCorrectFileCount()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        // 3 operations × 2 (request + response) + 1 interface + 1 implementation = 8
        Assert.AreEqual(8, files.Count);
    }

    [TestMethod]
    public void Emit_ProducesRequestFiles()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        Assert.IsTrue(files.Any(f => f.FileName == "ListPetsRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "CreatePetRequest.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ShowPetByIdRequest.cs"));
    }

    [TestMethod]
    public void Emit_ProducesResponseFiles()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        Assert.IsTrue(files.Any(f => f.FileName == "ListPetsResponse.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "CreatePetResponse.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "ShowPetByIdResponse.cs"));
    }

    [TestMethod]
    public void Emit_InterfaceFileName()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        Assert.IsTrue(files.Any(f => f.FileName == "IApiPetsClient.cs"));
    }

    [TestMethod]
    public void Emit_ImplementationFileName()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        Assert.IsTrue(files.Any(f => f.FileName == "ApiPetsClient.cs"));
    }

    [TestMethod]
    public void Emit_InterfaceContainsNamespace()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");
        Assert.IsTrue(iface.Content.Contains("namespace Petstore.Client;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceContainsAllOperationMethods()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(iface.Content.Contains("ListPetsAsync", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("CreatePetAsync", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("ShowPetByIdAsync", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceMethodsReturnTypedResponses()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
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
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");
        Assert.IsTrue(
            impl.Content.Contains(
                "private readonly IApiTransport transport;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ImplementationContainsConstructor()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");
        Assert.IsTrue(
            impl.Content.Contains(
                "public ApiPetsClient(IApiTransport transport)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_RequestStructImplementsIApiRequest()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");
        Assert.IsTrue(
            req.Content.Contains(
                ": IApiRequest<ListPetsRequest>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ResponseStructImplementsIApiResponse()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");
        Assert.IsTrue(
            resp.Content.Contains(
                ": IApiResponse<ListPetsResponse>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_RequestHasStaticPathTemplate()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");
        Assert.IsTrue(
            req.Content.Contains(
                "PathTemplateUtf8 => \"/pets\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_RequestHasStaticMethod()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");
        Assert.IsTrue(
            req.Content.Contains(
                "Method => OperationMethod.Get", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ListPetsRequestHasQueryParameters()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("HasQueryParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("HasPathParameters => false", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ListPetsRequestHasLimitField()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        // limit is optional, so the type should be nullable
        Assert.IsTrue(req.Content.Contains("Limit { get; init; }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ListPetsRequestWriteQueryStringEmitsLimit()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");

        // The query string writer should reference the limit parameter name
        Assert.IsTrue(
            req.Content.Contains("WriteQueryString", StringComparison.Ordinal));
        Assert.IsTrue(
            req.Content.Contains("\"limit=\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ShowPetByIdRequestHasPathParameter()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains("HasPathParameters => true", StringComparison.Ordinal));
        Assert.IsTrue(req.Content.Contains("PetId { get; init; }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ShowPetByIdRequestWriteResolvedPathEmitsSegments()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
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
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ShowPetByIdRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "PathTemplateUtf8 => \"/pets/{petId}\"u8", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_CreatePetRequestHasPostMethod()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "CreatePetRequest.cs");

        Assert.IsTrue(
            req.Content.Contains(
                "Method => OperationMethod.Post", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ResponseHasStatusCodeProperty()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
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
        ClientCodeEmitter emitter = new("Petstore.Client");
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
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile resp = GetFile(files, "ListPetsResponse.cs");

        Assert.IsTrue(
            resp.Content.Contains(
                "public async ValueTask DisposeAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_CreatePetClientUsesBodySendAsync()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        // Without schema type map, body is a JsonElement
        Assert.IsTrue(
            impl.Content.Contains("JsonElement body", StringComparison.Ordinal));
        Assert.IsTrue(
            impl.Content.Contains(
                "SendAsync<CreatePetRequest, JsonElement, CreatePetResponse>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ListPetsClientUsesNoBodySendAsync()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains(
                "SendAsync<ListPetsRequest, ListPetsResponse>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_WithSchemaTypeMap_UsesTypedBody()
    {
        const string pointer = "#/paths/~1pets/post/requestBody/content/application~1json/schema";

        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            [pointer] = "Petstore.Client.NewPet",
        };

        ClientCodeEmitter emitter = new("Petstore.Client", schemaTypeMap: schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("Petstore.Client.NewPet body", StringComparison.Ordinal));
        Assert.IsTrue(
            impl.Content.Contains(
                "SendAsync<CreatePetRequest, Petstore.Client.NewPet, CreatePetResponse>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_WithSchemaTypeMap_InterfaceAlsoUsesTypedBody()
    {
        const string pointer = "#/paths/~1pets/post/requestBody/content/application~1json/schema";

        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            [pointer] = "Petstore.Client.NewPet",
        };

        ClientCodeEmitter emitter = new("Petstore.Client", schemaTypeMap: schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");

        Assert.IsTrue(
            iface.Content.Contains("Petstore.Client.NewPet body", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_WithSchemaTypeMap_UnmappedPointerFallsBackToJsonElement()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            ["#/components/schemas/SomethingElse"] = "Petstore.Client.SomethingElse",
        };

        ClientCodeEmitter emitter = new("Petstore.Client", schemaTypeMap: schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");

        Assert.IsTrue(
            impl.Content.Contains("JsonElement body", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ContainsAutoGeneratedHeader()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
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
        ClientCodeEmitter emitter = new("Petstore.Client", clientNamePrefix: "Petstore");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        Assert.IsTrue(files.Any(f => f.FileName == "IPetstorePetsClient.cs"));
        Assert.IsTrue(files.Any(f => f.FileName == "PetstorePetsClient.cs"));
    }

    [TestMethod]
    public void Emit_ImplementationImplementsInterface()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");
        Assert.IsTrue(
            impl.Content.Contains(": IApiPetsClient", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ImplementsDisposePattern()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = GetFile(files, "ApiPetsClient.cs");
        Assert.IsTrue(
            impl.Content.Contains(
                "public ValueTask DisposeAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceExtendsIAsyncDisposable()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = GetFile(files, "IApiPetsClient.cs");
        Assert.IsTrue(
            iface.Content.Contains("IAsyncDisposable", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_MethodsHaveXmlDocs()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
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
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile req = GetFile(files, "ListPetsRequest.cs");
        Assert.IsTrue(
            req.Content.Contains("WriteHeaders", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_AllFilesContainNamespace()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
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
}