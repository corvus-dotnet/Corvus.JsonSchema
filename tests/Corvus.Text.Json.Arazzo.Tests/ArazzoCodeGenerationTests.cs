// <copyright file="ArazzoCodeGenerationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Tests the document-level orchestrator <see cref="ArazzoCodeGeneration.GenerateAsync"/>, which composes
/// the inputs-model generator and executor emitter across every workflow in a document.
/// </summary>
[TestClass]
public class ArazzoCodeGenerationTests
{
    private const string Document = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "inputs": { "type": "object", "properties": { "petId": { "type": "string" } }, "required": [ "petId" ] },
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            },
            {
              "workflowId": "find-pet",
              "inputs": { "type": "object", "properties": { "query": { "type": "string" } } },
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.query" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generates_models_and_executors_for_every_workflow()
    {
        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(Document), Binder(), new ArazzoGenerationOptions("Acme.Pets"));

        // One executor per workflow, under Workflows/, in the workflows namespace, PascalCased (and the
        // kebab-cased id find-pet → FindPetWorkflow).
        GeneratedModelFile adopt = files.Single(f => f.FileName == "Workflows/AdoptWorkflow.cs");
        adopt.Content.ShouldContain("namespace Acme.Pets.Workflows;");
        adopt.Content.ShouldContain("public static partial class AdoptWorkflow");

        GeneratedModelFile find = files.Single(f => f.FileName == "Workflows/FindPetWorkflow.cs");
        find.Content.ShouldContain("public static partial class FindPetWorkflow");

        // Each workflow's inputs model lives in its own namespace, so the generated types never collide.
        adopt.Content.ShouldContain("Acme.Pets.Models.Adopt");
        find.Content.ShouldContain("Acme.Pets.Models.FindPet");

        // The typed accessors differ per workflow's inputs (petId vs query).
        adopt.Content.ShouldContain("((JsonElement)inputs.PetId)");
        find.Content.ShouldContain("((JsonElement)inputs.Query)");

        // Model files are emitted under Models/<Workflow>/ for each workflow.
        files.ShouldContain(f => f.FileName.StartsWith("Models/Adopt/", StringComparison.Ordinal));
        files.ShouldContain(f => f.FileName.StartsWith("Models/FindPet/", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Falls_back_to_JsonElement_when_a_workflow_has_no_inputs()
    {
        const string noInputs = """
            {
              "arazzo": "1.0.1",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
              "workflows": [
                {
                  "workflowId": "ping",
                  "steps": [
                    {
                      "stepId": "getPet",
                      "operationId": "getPet",
                      "parameters": [ { "name": "petId", "in": "path", "value": "42" } ],
                      "successCriteria": [ { "condition": "$statusCode == 200" } ]
                    }
                  ],
                  "outputs": {}
                }
              ]
            }
            """;

        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(noInputs), Binder(), new ArazzoGenerationOptions("Acme.Pets"));

        GeneratedModelFile ping = files.Single(f => f.FileName == "Workflows/PingWorkflow.cs");
        ping.Content.ShouldContain("Corvus.Text.Json.JsonElement inputs");
        files.ShouldNotContain(f => f.FileName.StartsWith("Models/Ping/", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Generates_an_inputs_model_resolving_an_external_schema_reference_by_its_root_id()
    {
        // The PREFERRED reference form: the external document declares an absolute root $id, and the inputs
        // schema references it by that $id — the document's canonical identity per JSON Schema 2020-12. The
        // generator registers the document under its $id (as well as the virtual fallback), so the reference
        // resolves exactly as the spec reads it.
        string document = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "inputs": {
                "type": "object",
                "properties": {
                  "petId": { "type": "string" },
                  "address": { "$ref": "https://schemas.acme.example/types#/$defs/Address" }
                },
                "required": [ "petId" ]
              },
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                }
              ]
            }
          ]
        }
        """;
        byte[] schemaDocument = Encoding.UTF8.GetBytes(
            """{"$id":"https://schemas.acme.example/types","$defs":{"Address":{"type":"object","properties":{"line1":{"type":"string"},"city":{"type":"string"}}}}}""");

        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(document),
            Binder(),
            new ArazzoGenerationOptions("Acme.Pets", SchemaDocuments: [new("acme-types", schemaDocument)]));

        files.ShouldContain(f => f.FileName.StartsWith("Models/Adopt/", StringComparison.Ordinal) && f.Content.Contains("Line1"));
        files.ShouldContain(f => f.FileName.StartsWith("Models/Adopt/", StringComparison.Ordinal) && f.Content.Contains("City"));
    }

    [TestMethod]
    public async Task Generates_an_inputs_model_resolving_an_external_schema_reference()
    {
        // The inputs schema references an attached external schema document (schemas/<name>#<pointer>, #94):
        // the generator registers the document as a sibling of the Arazzo document, so the relative reference
        // resolves with no rewriting and the generated model carries the referenced shape.
        string document = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "inputs": {
                "type": "object",
                "properties": {
                  "petId": { "type": "string" },
                  "address": { "$ref": "schemas/acme-types#/$defs/Address" }
                },
                "required": [ "petId" ]
              },
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                }
              ]
            }
          ]
        }
        """;
        byte[] schemaDocument = Encoding.UTF8.GetBytes(
            """{"$defs":{"Address":{"type":"object","properties":{"line1":{"type":"string"},"city":{"type":"string"}}}}}""");

        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(document),
            Binder(),
            new ArazzoGenerationOptions("Acme.Pets", SchemaDocuments: [new("acme-types", schemaDocument)]));

        // The model resolved the external document: the referenced Address shape's properties generate typed accessors.
        files.ShouldContain(f => f.FileName.StartsWith("Models/Adopt/", StringComparison.Ordinal) && f.Content.Contains("Line1"));
        files.ShouldContain(f => f.FileName.StartsWith("Models/Adopt/", StringComparison.Ordinal) && f.Content.Contains("City"));
    }

    private static WorkflowOperationBinder Binder()
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

        return new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);
    }
}
