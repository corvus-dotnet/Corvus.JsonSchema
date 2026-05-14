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
    private static ClientModel petstoreModel = null!;

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
        Assert.IsTrue(impl.Content.Contains("WithQueryParameter(\"limit\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_ShowPetByIdHasPathParameter()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        Assert.IsTrue(impl.Content.Contains("string petId", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains("path.Replace(\"{petId}\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_CreatePetHasRequestBody()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile impl = files.First(f => !f.FileName.StartsWith("I", StringComparison.Ordinal));

        Assert.IsTrue(impl.Content.Contains("ReadOnlyMemory<byte> body", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains("WithBody(body", StringComparison.Ordinal));
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

        // GET for listPets and showPetById
        Assert.IsTrue(impl.Content.Contains("new(path, \"GET\")", StringComparison.Ordinal));
        // POST for createPet
        Assert.IsTrue(impl.Content.Contains("new(path, \"POST\")", StringComparison.Ordinal));
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
        Assert.IsTrue(impl.Content.Contains("public void Dispose()", StringComparison.Ordinal));
        Assert.IsTrue(impl.Content.Contains("public ValueTask DisposeAsync()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_InterfaceExtendsIDisposable()
    {
        ClientCodeEmitter emitter = new("Petstore.Client");
        IReadOnlyList<GeneratedFile> files = emitter.Emit(petstoreModel);

        GeneratedFile iface = files.First(f => f.FileName.StartsWith("I", StringComparison.Ordinal));
        Assert.IsTrue(iface.Content.Contains("IDisposable", StringComparison.Ordinal));
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
}