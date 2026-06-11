// <copyright file="WorkflowSchemaValidationBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Validator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Tests <see cref="WorkflowSchemaMetadataGenerator.TryBuildValidationSchema"/> by feeding the built schema to
/// the runtime <see cref="JsonSchema"/> validator and asserting real values pass/fail — including a response/
/// request body that is itself a <c>$ref</c> into <c>components</c> (so the carried definitions are exercised).
/// Relies on <c>PreserveCompilationContext</c> (set in this project) so the validator's dynamic compiler has the
/// compilation defines/references it needs — the same requirement a hosting service has.
/// </summary>
[TestClass]
public class WorkflowSchemaValidationBuilderTests
{
    private const string Workflow = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt-pet-v1",
              "inputs": {
                "type": "object",
                "properties": { "petId": { "type": "integer", "format": "int64", "minimum": 1 } },
                "required": [ "petId" ]
              },
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "outputs": {
                    "petName": "$response.body#/name",
                    "status": "$response.body#/status",
                    "code": "$statusCode",
                    "echoedId": "$inputs.petId"
                  }
                }
              ]
            }
          ]
        }
        """;

    private const string Petstore = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Petstore", "version": "1.0.0" },
          "paths": {
            "/pets/{petId}": {
              "get": {
                "operationId": "getPet",
                "requestBody": {
                  "content": { "application/json": { "schema": { "$ref": "#/components/schemas/PetCreate" } } }
                },
                "responses": {
                  "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } } } }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "Pet": {
                "type": "object",
                "properties": {
                  "name": { "type": "string", "maxLength": 40 },
                  "status": { "type": "string", "enum": [ "available", "pending", "sold" ] }
                },
                "required": [ "name" ]
              },
              "PetCreate": {
                "type": "object",
                "properties": { "name": { "type": "string" } },
                "required": [ "name" ]
              }
            }
          }
        }
        """;

    [TestMethod]
    public void Validates_workflow_inputs()
    {
        JsonSchema schema = Build(new WorkflowSchemaTarget(WorkflowSchemaTargetKind.Inputs, WorkflowId: "adopt-pet-v1"));

        schema.Validate("""{ "petId": 5 }""").ShouldBeTrue();
        schema.Validate("""{ }""").ShouldBeFalse("petId is required");
        schema.Validate("""{ "petId": 0 }""").ShouldBeFalse("petId minimum is 1");
    }

    [TestMethod]
    public void Validates_response_body_through_a_components_ref()
    {
        JsonSchema schema = Build(new WorkflowSchemaTarget(WorkflowSchemaTargetKind.ResponseBody, WorkflowId: "adopt-pet-v1", StepId: "getPet", Status: "200"));

        schema.Validate("""{ "name": "Rex", "status": "available" }""").ShouldBeTrue();
        schema.Validate("""{ "status": "available" }""").ShouldBeFalse("name is required");
        schema.Validate("""{ "name": "Rex", "status": "nope" }""").ShouldBeFalse("status is constrained by the enum");
    }

    [TestMethod]
    public void Validates_request_body_through_a_components_ref()
    {
        JsonSchema schema = Build(new WorkflowSchemaTarget(WorkflowSchemaTargetKind.RequestBody, WorkflowId: "adopt-pet-v1", StepId: "getPet"));

        schema.Validate("""{ "name": "Rex" }""").ShouldBeTrue();
        schema.Validate("""{ }""").ShouldBeFalse("name is required");
    }

    [TestMethod]
    public void Validates_step_outputs_object_against_resolved_output_schemas()
    {
        JsonSchema schema = Build(new WorkflowSchemaTarget(WorkflowSchemaTargetKind.StepOutputs, WorkflowId: "adopt-pet-v1", StepId: "getPet"));

        // petName (string), status (enum), code ($statusCode → integer), echoedId ($inputs.petId → integer ≥ 1).
        schema.Validate("""{ "code": 200, "echoedId": 5, "status": "available", "petName": "Rex" }""").ShouldBeTrue();
        schema.Validate("""{ "code": "not-an-int" }""").ShouldBeFalse("code resolves to an integer");
        schema.Validate("""{ "status": "nope" }""").ShouldBeFalse("status carries the response enum");
        schema.Validate("""{ "echoedId": 0 }""").ShouldBeFalse("echoedId inherits the inputs minimum");
    }

    private static JsonSchema Build(WorkflowSchemaTarget target)
    {
        WorkflowSchemaMetadataGenerator.TryBuildValidationSchema(
            Encoding.UTF8.GetBytes(Workflow),
            [new KeyValuePair<string, byte[]>("petstore", Encoding.UTF8.GetBytes(Petstore))],
            target,
            out byte[] document).ShouldBeTrue();

        // The validator caches by canonical URI only, so give each distinct schema a distinct URI.
        string uri = $"corvus:test/{target.Kind}/{target.WorkflowId}/{target.StepId}/{target.Status}";
        return JsonSchema.FromText(Encoding.UTF8.GetString(document), uri);
    }
}
