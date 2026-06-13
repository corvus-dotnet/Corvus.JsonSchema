// <copyright file="WorkflowExecutorRequestBindingTests.cs" company="Endjin Limited">
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
/// Proves request-binding value locals are unique per step: two straight-line steps binding the same request
/// property (here both call <c>getPet</c>, whose request has a <c>petId</c> path parameter) compile into one
/// method body without colliding locals, and each call carries its own value.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string TwoStraightLineCallsDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "twoCalls",
              "steps": [
                {
                  "stepId": "first",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.a" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "firstName": "$response.body#/name" }
                },
                {
                  "stepId": "second",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.b" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "secondName": "$response.body#/name" }
                }
              ],
              "outputs": { "first": "$steps.first.outputs.firstName", "second": "$steps.second.outputs.secondName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Two_straight_line_steps_binding_the_same_request_property_do_not_collide()
    {
        OperationDescriptor getPet = new(
            "/pets/{petId}", OperationMethod.Get, "getPet", "GetPet",
            typeof(PetByIdRequest).FullName!, typeof(PetByIdResponse).FullName!,
            [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Corvus.Text.Json.JsonElement", true, "petId")],
            false, [new ResponseDescriptor("200", "Corvus.Text.Json.JsonElement", "OkBody")],
            typeof(PetByIdClient).FullName!, "GetPetAsync", null, null);

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", [getPet]))]);

        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(TwoStraightLineCallsDocument), binder, new ArazzoGenerationOptions("GeneratedWorkflows"));
        string[] executors = [.. files.Where(f => f.FileName.StartsWith("Workflows/", StringComparison.Ordinal)).Select(f => f.Content)];

        // Compilation is the regression guard: a shared `petIdValue` local in one method body was CS0128.
        Assembly assembly = CompileInMemory(executors);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.TwoCallsWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var workspace = JsonWorkspace.Create();
        using var inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"a":"42","b":"99"}"""));

        var pending = (ValueTask<JsonElement>)execute.Invoke(null, [transport, workspace, inputs.RootElement, default(CancellationToken), null])!;
        JsonElement outputs = await pending;

        // Both calls happened, each with its own petId value.
        transport.Requests.Count.ShouldBe(2);
        transport.Requests[0].Path.ShouldBe("/pets/42");
        transport.Requests[1].Path.ShouldBe("/pets/99");
        outputs.TryGetProperty("first"u8, out JsonElement first).ShouldBeTrue();
        first.GetString().ShouldBe("Fido");
        outputs.TryGetProperty("second"u8, out JsonElement second).ShouldBeTrue();
        second.GetString().ShouldBe("Fido");
    }
}