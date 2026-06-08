// <copyright file="Coverage_GeneratorNavTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Branch-coverage tests for the criterion operand navigation (<see cref="CriterionExpressionParsing"/>
/// via the simple-criterion inliner): the supported operand sources (inputs untyped/typed, prior step
/// outputs, response body) combined with each navigation form (<c>.property</c>, <c>[index]</c>, and
/// <c>#/json/pointer</c>). These static-navigation forms are not all exercised by the happy-path tests.
/// </summary>
[TestClass]
public class Coverage_GeneratorNavTests
{
    [TestMethod]
    public void Inliner_navigates_inputs_steps_and_response_body_operands()
    {
        // The 'check' step compares operands drawn from every statically-navigable source and form.
        string criteria = string.Join(", ", new[]
        {
            """{ "condition": "$inputs.tag.kind == 'cat'" }""",        // $inputs + .property
            """{ "condition": "$inputs.scores[0] == 1" }""",           // $inputs + [index]
            """{ "condition": "$inputs.profile#/nested == 'v'" }""",   // $inputs + #/pointer
            """{ "condition": "$steps.prev.outputs.token == 'Rex'" }""", // $steps.*.outputs
            """{ "condition": "$steps.prev.outputs.obj#/name == 'Rex'" }""", // $steps + #/pointer
            """{ "condition": "$response.body#/name == 'Rex'" }""",    // $response.body + #/pointer
            """{ "condition": "$inputs.a == $inputs.b" }""",           // operand vs operand
        });

        string source = Emit(BuildDocument(criteria), inputAccessors: null);

        source.ShouldContain("TryGetProperty");
        source.ShouldContain("TryResolvePointer");
    }

    [TestMethod]
    public void Inliner_navigates_typed_inputs_accessor()
    {
        // With an inputs accessor map, $inputs.petId compiles to the strongly-typed property.
        string source = Emit(
            BuildDocument("""{ "condition": "$inputs.petId == '42'" }"""),
            inputAccessors: new Dictionary<string, string> { ["petId"] = "PetId" });

        source.ShouldContain("((JsonElement)inputs.PetId)");
    }

    private static string BuildDocument(string checkCriteria) => $$"""
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "prev",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "outputs": { "token": "$response.body#/name", "obj": "$response.body" }
                },
                {
                  "stepId": "check",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ {{checkCriteria}} ],
                  "outputs": { "ok": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.check.outputs.ok" }
            }
          ]
        }
        """;

    private static string Emit(string document, IReadOnlyDictionary<string, string>? inputAccessors)
    {
        OperationDescriptor[] operations =
        [
            new(
                "/pets/{petId}",
                OperationMethod.Get,
                "getPet",
                "GetPet",
                "Acme.Pets.GetPetRequest",
                "Acme.Pets.GetPetResponse",
                [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Acme.Pets.JsonString", true, "petId")],
                false,
                [new ResponseDescriptor("200", "Acme.Pets.Pet", "OkBody")],
                "Acme.Pets.PetsClient",
                "GetPetAsync",
                null),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);

        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(document));
        ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
        return WorkflowExecutorEmitter.Emit(
            workflow,
            binder,
            new WorkflowExecutorOptions("Acme.Pets.Workflows", "AdoptWorkflow", "Acme.Pets.AdoptInputs", "Acme.Pets.AdoptOutputs", inputAccessors));
    }
}
