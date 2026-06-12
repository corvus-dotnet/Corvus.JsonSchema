// <copyright file="WorkflowExecutorMultiSourceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Arazzo.Tests.Fakes;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Proves a workflow whose steps span more than one API source binds and runs against a per-source transport
/// map: each step calls through the transport for <em>its own</em> source, not a single shared transport.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string MultiSourceDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [
            { "name": "petstore", "url": "./p.yaml", "type": "openapi" },
            { "name": "billing", "url": "./b.yaml", "type": "openapi" }
          ],
          "workflows": [
            {
              "workflowId": "checkout",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" },
                  "onSuccess": [ { "name": "toInvoice", "type": "goto", "stepId": "getInvoice" } ]
                },
                {
                  "stepId": "getInvoice",
                  "operationId": "getInvoice",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.invoiceId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "amount": "$response.body#/amount" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName", "amount": "$steps.getInvoice.outputs.amount" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generated_executor_routes_each_step_to_its_own_source_transport()
    {
        // Two API sources, each owning one operation; both reuse the same fake client, so the only thing that
        // differs at runtime is which source's transport the step's client is constructed with.
        OperationDescriptor getPet = new(
            "/pets/{petId}", OperationMethod.Get, "getPet", "GetPet",
            typeof(PetByIdRequest).FullName!, typeof(PetByIdResponse).FullName!,
            [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Corvus.Text.Json.JsonElement", true, "petId")],
            false, [new ResponseDescriptor("200", "Corvus.Text.Json.JsonElement", "OkBody")],
            typeof(PetByIdClient).FullName!, "GetPetAsync", null, null);
        OperationDescriptor getInvoice = new(
            "/pets/{petId}", OperationMethod.Get, "getInvoice", "GetInvoice",
            typeof(PetByIdRequest).FullName!, typeof(PetByIdResponse).FullName!,
            [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Corvus.Text.Json.JsonElement", true, "petId")],
            false, [new ResponseDescriptor("200", "Corvus.Text.Json.JsonElement", "OkBody")],
            typeof(PetByIdClient).FullName!, "GetPetAsync", null, null);

        var binder = new WorkflowOperationBinder(
        [
            new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", [getPet])),
            new SourceDescriptionClient("billing", OperationResolver.Create("billing", [getInvoice])),
        ]);

        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(MultiSourceDocument), binder, new ArazzoGenerationOptions("GeneratedWorkflows"));
        string[] executors = [.. files.Where(f => f.FileName.StartsWith("Workflows/", StringComparison.Ordinal)).Select(f => f.Content)];

        // A multi-source executor takes a source→transport map and selects per source.
        executors[0].ShouldContain("ExecuteAsync(System.Collections.Generic.IReadOnlyDictionary<string, IApiTransport> transports");
        executors[0].ShouldContain("transports[\"petstore\"]");
        executors[0].ShouldContain("transports[\"billing\"]");

        Assembly assembly = CompileInMemory(executors);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.CheckoutWorkflow")!.GetMethod("ExecuteAsync")!;

        var petstore = new MockApiTransport();
        petstore.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        var billing = new MockApiTransport();
        billing.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"amount":42}""");
        var transports = new Dictionary<string, IApiTransport>(StringComparer.Ordinal) { ["petstore"] = petstore, ["billing"] = billing };

        using var workspace = JsonWorkspace.Create();
        using var inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42","invoiceId":"99"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transports, workspace, inputs.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        // The getPet step went to the petstore transport, getInvoice to the billing transport — not a shared one.
        petstore.Requests.Count.ShouldBe(1);
        petstore.Requests[0].Path.ShouldBe("/pets/42");
        billing.Requests.Count.ShouldBe(1);
        billing.Requests[0].Path.ShouldBe("/pets/99");

        outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
        name.GetString().ShouldBe("Fido");
        outputs.TryGetProperty("amount"u8, out JsonElement amount).ShouldBeTrue();
        amount.GetInt32().ShouldBe(42);
    }
}