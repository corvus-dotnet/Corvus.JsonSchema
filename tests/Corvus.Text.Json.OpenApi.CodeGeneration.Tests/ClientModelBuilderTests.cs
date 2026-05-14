// <copyright file="ClientModelBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi31;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class ClientModelBuilderTests
{
    private static JsonElement petstoreRoot;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "petstore-3.1.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        petstoreRoot = doc.RootElement.Clone();
    }

    [TestMethod]
    public void Build_ExtractsApiTitle()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());
        Assert.AreEqual("Petstore", model.Title);
    }

    [TestMethod]
    public void Build_ExtractsApiVersion()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());
        Assert.AreEqual("1.0.0", model.Version);
    }

    [TestMethod]
    public void Build_ExtractsApiDescription()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());
        Assert.AreEqual("A sample Petstore API", model.Description);
    }

    [TestMethod]
    public void Build_FindsAllOperations()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        // listPets, createPet, showPetById
        Assert.AreEqual(3, model.Operations.Count);
    }

    [TestMethod]
    public void Build_ExtractsOperationIds()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());
        List<string?> ids = model.Operations.Select(o => o.OperationId).ToList();

        CollectionAssert.Contains(ids, "listPets");
        CollectionAssert.Contains(ids, "createPet");
        CollectionAssert.Contains(ids, "showPetById");
    }

    [TestMethod]
    public void Build_ExtractsHttpMethods()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        ClientOperation listPets = model.Operations.First(o => o.OperationId == "listPets");
        Assert.AreEqual(OperationMethod.Get, listPets.Method);

        ClientOperation createPet = model.Operations.First(o => o.OperationId == "createPet");
        Assert.AreEqual(OperationMethod.Post, createPet.Method);
    }

    [TestMethod]
    public void Build_ExtractsPaths()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        ClientOperation listPets = model.Operations.First(o => o.OperationId == "listPets");
        Assert.AreEqual("/pets", listPets.Path);

        ClientOperation showPet = model.Operations.First(o => o.OperationId == "showPetById");
        Assert.AreEqual("/pets/{petId}", showPet.Path);
    }

    [TestMethod]
    public void Build_ExtractsTags()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        ClientOperation listPets = model.Operations.First(o => o.OperationId == "listPets");
        Assert.AreEqual(1, listPets.Tags.Count);
        Assert.AreEqual("pets", listPets.Tags[0]);
    }

    [TestMethod]
    public void Build_ExtractsSummary()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        ClientOperation listPets = model.Operations.First(o => o.OperationId == "listPets");
        Assert.AreEqual("List all pets", listPets.Summary);
    }

    [TestMethod]
    public void Build_ExtractsQueryParameter()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        ClientOperation listPets = model.Operations.First(o => o.OperationId == "listPets");
        Assert.AreEqual(1, listPets.Parameters.Count);

        ClientParameter limit = listPets.Parameters[0];
        Assert.AreEqual("limit", limit.Name);
        Assert.AreEqual(ParameterLocation.Query, limit.Location);
        Assert.IsFalse(limit.IsRequired);
        Assert.IsNotNull(limit.SchemaPointer);
    }

    [TestMethod]
    public void Build_ExtractsPathParameter()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        ClientOperation showPet = model.Operations.First(o => o.OperationId == "showPetById");
        Assert.AreEqual(1, showPet.Parameters.Count);

        ClientParameter petId = showPet.Parameters[0];
        Assert.AreEqual("petId", petId.Name);
        Assert.AreEqual(ParameterLocation.Path, petId.Location);
        Assert.IsTrue(petId.IsRequired);
    }

    [TestMethod]
    public void Build_ExtractsRequestBody()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        ClientOperation createPet = model.Operations.First(o => o.OperationId == "createPet");
        Assert.IsNotNull(createPet.RequestBody);
        Assert.IsTrue(createPet.RequestBody.IsRequired);
        Assert.AreEqual(1, createPet.RequestBody.Content.Count);
        Assert.AreEqual("application/json", createPet.RequestBody.Content[0].MediaType);
        Assert.IsNotNull(createPet.RequestBody.Content[0].SchemaPointer);
    }

    [TestMethod]
    public void Build_ExtractsResponses()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        ClientOperation listPets = model.Operations.First(o => o.OperationId == "listPets");

        // 200 and default
        Assert.AreEqual(2, listPets.Responses.Count);

        ClientResponse ok = listPets.Responses.First(r => r.StatusCodePattern == "200");
        Assert.AreEqual("A list of pets", ok.Description);
        Assert.AreEqual(1, ok.Content.Count);
        Assert.AreEqual("application/json", ok.Content[0].MediaType);

        ClientResponse defaultResp = listPets.Responses.First(r => r.StatusCodePattern == "default");
        Assert.AreEqual("unexpected error", defaultResp.Description);
    }

    [TestMethod]
    public void Build_NoRequestBodyForGetOperation()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        ClientOperation listPets = model.Operations.First(o => o.OperationId == "listPets");
        Assert.IsNull(listPets.RequestBody);
    }

    [TestMethod]
    public void Build_CollectsSchemaPointers()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        // Should have pointers for: inline parameter schemas, request body schemas,
        // response body schemas, and component schemas
        Assert.IsTrue(model.SchemaPointers.Count > 0);
    }

    [TestMethod]
    public void Build_WithFilter_OnlyIncludesMatchingOperations()
    {
        OperationFilter filter = new(["/pets"]);

        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker(), filter);

        // Only /pets (GET, POST) — not /pets/{petId}
        Assert.AreEqual(2, model.Operations.Count);
        Assert.IsTrue(model.Operations.All(o => o.Path == "/pets"));
    }

    [TestMethod]
    public void GetOperationsByTag_GroupsCorrectly()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        IReadOnlyDictionary<string, IReadOnlyList<ClientOperation>> groups =
            model.GetOperationsByTag();

        // All petstore operations are tagged "pets"
        Assert.AreEqual(1, groups.Count);
        Assert.IsTrue(groups.ContainsKey("pets"));
        Assert.AreEqual(3, groups["pets"].Count);
    }

    [TestMethod]
    public void GetMethodName_UsesOperationId()
    {
        ClientModel model = ClientModelBuilder.Build(petstoreRoot, new OpenApi31Walker());

        ClientOperation listPets = model.Operations.First(o => o.OperationId == "listPets");
        Assert.AreEqual("ListPets", listPets.GetMethodName());
    }

    [TestMethod]
    public void GetMethodName_SynthesizesFromPathAndMethod_WhenNoOperationId()
    {
        // Build an operation with no operationId
        ClientOperation op = new(
            operationId: null,
            path: "/pets/{petId}",
            method: OperationMethod.Get,
            summary: null,
            description: null,
            tags: [],
            parameters: [],
            requestBody: null,
            responses: []);

        Assert.AreEqual("GetPetsPetId", op.GetMethodName());
    }
}
