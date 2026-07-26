// <copyright file="OpenApi20CodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi20;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class OpenApi20CodeGeneratorTests
{
    private static JsonElement petstoreRoot;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "petstore-2.0.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        petstoreRoot = doc.RootElement.Clone();
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsBodyParameterSchemas()
    {
        string[] pointers = [.. OpenApi20CodeGenerator.CollectSchemaPointers(petstoreRoot, out var parameterNames).Select(r => r.PositionalPointer)];

        CollectionAssert.Contains(pointers, "#/paths/~1pet/post/parameters/0/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1store~1order/post/parameters/0/schema");

        Assert.AreEqual("body", parameterNames["/paths/~1pet/post/parameters/0/schema"]);
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsParameterObjectPointers()
    {
        string[] pointers = [.. OpenApi20CodeGenerator.CollectSchemaPointers(petstoreRoot, out var parameterNames).Select(r => r.PositionalPointer)];

        // Non-body parameters are addressed as Parameter Objects with no /schema tail.
        CollectionAssert.Contains(pointers, "#/paths/~1pet~1findByStatus/get/parameters/0");
        CollectionAssert.Contains(pointers, "#/paths/~1pet~1{petId}/get/parameters/0");

        Assert.AreEqual("status", parameterNames["/paths/~1pet~1findByStatus/get/parameters/0"]);
        Assert.AreEqual("petId", parameterNames["/paths/~1pet~1{petId}/get/parameters/0"]);
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsResponseSchemas()
    {
        string[] pointers = [.. OpenApi20CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];

        // 2.0 responses carry a direct schema; there is no content media-type map.
        CollectionAssert.Contains(pointers, "#/paths/~1pet~1findByStatus/get/responses/200/schema");
        CollectionAssert.Contains(pointers, "#/paths/~1store~1inventory/get/responses/200/schema");
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsResponseHeaderObjects()
    {
        string[] pointers = [.. OpenApi20CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];

        // Response headers are Header Objects with no /schema tail.
        CollectionAssert.Contains(pointers, "#/paths/~1user~1login/get/responses/200/headers/X-Rate-Limit");
        CollectionAssert.Contains(pointers, "#/paths/~1user~1login/get/responses/200/headers/X-Expires-After");
    }

    [TestMethod]
    public void CollectSchemaPointers_SynthesizesFormBodies()
    {
        SchemaReference[] refs = OpenApi20CodeGenerator.CollectSchemaPointers(
            petstoreRoot, out _, out IReadOnlyDictionary<string, string> syntheticDocuments);

        SchemaReference formBody = refs.Single(r => r.PositionalPointer == "#/paths/~1pet~1{petId}/post/x-corvus-form-body");
        Assert.AreEqual(
            "https://corvus-openapi20.invalid/form-bodies.json#/schemas/UpdatePetWithFormFormBody",
            formBody.ResolvablePointer);

        SchemaReference uploadBody = refs.Single(r => r.PositionalPointer == "#/paths/~1pet~1{petId}~1uploadImage/post/x-corvus-form-body");
        Assert.AreEqual(
            "https://corvus-openapi20.invalid/form-bodies.json#/schemas/UploadFileFormBody",
            uploadBody.ResolvablePointer);

        // The synthetic document holds both aggregated schemas; file fields become
        // type: string, format: binary properties.
        string syntheticJson = syntheticDocuments[OpenApi20CodeGenerator.FormBodyDocumentUri];
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(syntheticJson);
        JsonElement schemas = doc.RootElement.GetProperty("schemas"u8);

        JsonElement updateForm = schemas.GetProperty("UpdatePetWithFormFormBody"u8);
        Assert.AreEqual(JsonValueKind.Object, updateForm.GetProperty("properties"u8).GetProperty("name"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Object, updateForm.GetProperty("properties"u8).GetProperty("status"u8).ValueKind);

        JsonElement uploadForm = schemas.GetProperty("UploadFileFormBody"u8);
        JsonElement fileProp = uploadForm.GetProperty("properties"u8).GetProperty("file"u8);
        Assert.IsTrue(fileProp.GetProperty("format"u8).ValueEquals("binary"u8));
    }

    [TestMethod]
    public void CollectSchemaPointers_DoesNotEmitFormDataParameterPointers()
    {
        string[] pointers = [.. OpenApi20CodeGenerator.CollectSchemaPointers(petstoreRoot, out _).Select(r => r.PositionalPointer)];

        // The formData parameters at indices 1 and 2 of updatePetWithForm fold into the
        // synthesized form body; only the path parameter (index 0) is a schema position.
        CollectionAssert.Contains(pointers, "#/paths/~1pet~1{petId}/post/parameters/0");
        CollectionAssert.DoesNotContain(pointers, "#/paths/~1pet~1{petId}/post/parameters/1");
        CollectionAssert.DoesNotContain(pointers, "#/paths/~1pet~1{petId}/post/parameters/2");
    }

    [TestMethod]
    public void ListOperations_ReportsBodiesForBodyAndFormDataOperations()
    {
        OperationSummary[] operations = OpenApi20CodeGenerator.ListOperations(petstoreRoot);

        Assert.AreEqual(20, operations.Length);

        Assert.IsTrue(operations.Single(o => o.OperationId == "addPet").HasRequestBody);
        Assert.IsTrue(operations.Single(o => o.OperationId == "updatePetWithForm").HasRequestBody);
        Assert.IsTrue(operations.Single(o => o.OperationId == "uploadFile").HasRequestBody);
        Assert.IsFalse(operations.Single(o => o.OperationId == "findPetsByStatus").HasRequestBody);
        Assert.IsFalse(operations.Single(o => o.OperationId == "deletePet").HasRequestBody);
    }

    [TestMethod]
    public void ListTags_ReadsTopLevelTags()
    {
        TagInfo[] tags = OpenApi20CodeGenerator.ListTags(petstoreRoot);

        CollectionAssert.AreEquivalent(
            new[] { "pet", "store", "user" },
            tags.Select(t => t.Name).ToArray());
    }

    [TestMethod]
    public void Generate_EmitsClientFilesForEveryTag()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);
        OpenApi20CodeGenerator generator = new("Petstore.Client", schemaTypeMap);

        IReadOnlyList<GeneratedFile> files = generator.Generate(petstoreRoot);

        Assert.IsTrue(files.Count > 0);

        string[] fileNames = [.. files.Select(f => f.FileName)];
        CollectionAssert.Contains(fileNames, "IApiPetClient.cs");
        CollectionAssert.Contains(fileNames, "ApiPetClient.cs");
        CollectionAssert.Contains(fileNames, "IApiStoreClient.cs");
        CollectionAssert.Contains(fileNames, "IApiUserClient.cs");
    }

    [TestMethod]
    public void Generate_FormDataOperationSerializesAsFormUrlEncoded()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);
        OpenApi20CodeGenerator generator = new("Petstore.Client", schemaTypeMap);

        IReadOnlyList<GeneratedFile> files = generator.Generate(petstoreRoot);

        // The media-type literals are emitted in the client implementation's send path.
        // updatePetWithForm consumes application/x-www-form-urlencoded via formData
        // params; uploadFile carries a file parameter, so its body is multipart with
        // binary parts. Both operations are tagged pet.
        GeneratedFile client = files.First(f => f.FileName == "ApiPetClient.cs");
        StringAssert.Contains(client.Content, "application/x-www-form-urlencoded");
        StringAssert.Contains(client.Content, "multipart/form-data");

        // The file field is hoisted to a BinaryPartData parameter on the client method.
        GeneratedFile clientInterface = files.First(f => f.FileName == "IApiPetClient.cs");
        StringAssert.Contains(clientInterface.Content, "BinaryPartData file");
    }

    [TestMethod]
    public void Generate_QueryParameterWithCollectionFormatMultiExplodes()
    {
        Dictionary<string, string> schemaTypeMap = new(StringComparer.Ordinal);
        OpenApi20CodeGenerator generator = new("Petstore.Client", schemaTypeMap);

        IReadOnlyList<GeneratedFile> files = generator.Generate(petstoreRoot);

        // findPetsByStatus declares collectionFormat: multi → repeated name=value pairs.
        GeneratedFile request = files.First(f => f.FileName == "FindPetsByStatusRequest.cs");
        StringAssert.Contains(request.Content, "status=");
    }
}