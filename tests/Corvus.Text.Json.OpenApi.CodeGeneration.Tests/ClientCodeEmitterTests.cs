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

    [TestMethod]
    public void Emit_ProducesInterfaceAndImplementation()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        // One tag group ("pets") → interface + implementation
        Assert.AreEqual(2, files.Count);
    }

    [TestMethod]
    public void Emit_InterfaceFileName()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = files.First(f => f.FileName.StartsWith("I", StringComparison.Ordinal));
        Assert.AreEqual("IApiPetsClient.cs", iface.FileName);
    }

    [TestMethod]
    public void Emit_ImplementationFileName()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));
        Assert.AreEqual("ApiPetsClient.cs", impl.FileName);
    }

    [TestMethod]
    public void Emit_InterfaceContainsNamespace()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = files.First(f => f.FileName.StartsWith("I", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("namespace Petstore.Client;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceContainsAllOperationMethods()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = files.First(f => f.FileName.StartsWith("I", StringComparison.Ordinal));

        Assert.IsTrue(iface.Content.Contains("ListPetsAsync", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("CreatePetAsync", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("ShowPetByIdAsync", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ImplementationContainsTransportField()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains("private readonly IApiTransport transport;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ImplementationContainsConstructor()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains("public ApiPetsClient(IApiTransport transport)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ListPetsHasLimitQueryParameter()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        // The limit parameter should be optional (not required in the spec)
        Assert.IsTrue(impl.Content.Contains("string? limit", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains("AddQueryParameter(\"limit\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ShowPetByIdHasPathParameter()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        Assert.IsTrue(impl.Content.Contains("string petId", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains("request.AddPathParameter(\"petId\", petId", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_CreatePetHasRequestBody()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        // Without schema type map, body is a JsonElement
        Assert.IsTrue(impl.Content.Contains("JsonElement body", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains("SendAsync(in request, in body", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_WithSchemaTypeMap_UsesTypedBody()
    {
        // The createPet request body schema pointer for /pets POST application/json
        string pointer = "#/paths/~1pets/post/requestBody/content/application~1json/schema";

        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            [pointer] = "Petstore.Client.NewPet",
        };

        ClientCodeEmitter emitter = new("Petstore.Client", schemaTypeMap: schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        // With schema type map, body should use the resolved type name
        Assert.IsTrue(impl.Content.Contains("Petstore.Client.NewPet body", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains("SendAsync(in request, in body", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_WithSchemaTypeMap_InterfaceAlsoUsesTypedBody()
    {
        string pointer = "#/paths/~1pets/post/requestBody/content/application~1json/schema";

        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            [pointer] = "Petstore.Client.NewPet",
        };

        ClientCodeEmitter emitter = new("Petstore.Client", schemaTypeMap: schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = files.First(f => f.FileName.StartsWith("I", StringComparison.Ordinal));

        // Interface signature should also use the resolved type
        Assert.IsTrue(iface.Content.Contains("Petstore.Client.NewPet body", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_WithSchemaTypeMap_UnmappedPointerFallsBackToJsonElement()
    {
        // Provide a map that does NOT contain the createPet pointer
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal)
        {
            ["#/components/schemas/SomethingElse"] = "Petstore.Client.SomethingElse",
        };

        ClientCodeEmitter emitter = new("Petstore.Client", schemaTypeMap: schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        // Should fall back to JsonElement since the pointer isn't in the map
        Assert.IsTrue(impl.Content.Contains("JsonElement body", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ContainsAutoGeneratedHeader()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        foreach (GeneratedFile file in files)
        {
            Assert.IsTrue(file.Content.Contains("<auto-generated>", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Emit_UsesCorrectHttpMethods()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        // GET for listPets and showPetById — path template is now passed directly
        Assert.IsTrue(impl.Content.Contains("new(\"/pets\", OperationMethod.Get)", StringComparison.Ordinal)
            || impl.Content.Contains("new(\"/pets/{petId}\", OperationMethod.Get)", StringComparison.Ordinal));
        // POST for createPet
        Assert.IsTrue(impl.Content.Contains("new(\"/pets\", OperationMethod.Post)", StringComparison.Ordinal));
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

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains(": IApiPetsClient", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ImplementsDisposePattern()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains("public ValueTask DisposeAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceExtendsIAsyncDisposable()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = files.First(f => f.FileName.StartsWith("I", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("IAsyncDisposable", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_MethodsHaveXmlDocs()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = files.First(f => f.FileName.StartsWith("I", StringComparison.Ordinal));

        // Should have summary from the spec
        Assert.IsTrue(iface.Content.Contains("List all pets", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("Create a pet", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_QueryParameterIncludesStyleAndExplode()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        // The limit query parameter should be emitted with form style, explode true (defaults)
        Assert.IsTrue(impl.Content.Contains(
            "AddQueryParameter(\"limit\", limitValue, ParameterStyle.Form, true)",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_PathParameterIncludesStyleAndExplode()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        // The petId path parameter should be emitted with simple style, explode false (defaults)
        Assert.IsTrue(impl.Content.Contains(
            "AddPathParameter(\"petId\", petId, ParameterStyle.Simple, false)",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_PathTemplatePassedDirectlyToApiRequest()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        // The path template should be passed directly to ApiRequest, not pre-resolved
        Assert.IsTrue(impl.Content.Contains(
            "new(\"/pets/{petId}\", OperationMethod.Get)",
            StringComparison.Ordinal));
    }
}