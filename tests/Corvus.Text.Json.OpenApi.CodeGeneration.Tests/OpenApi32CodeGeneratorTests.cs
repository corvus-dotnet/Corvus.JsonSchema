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
}