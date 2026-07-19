// <copyright file="WorkflowOutputsModelGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Tests that <see cref="WorkflowOutputsModelGenerator"/> drives the JSON Schema code generator over a workflow's
/// resolved outputs schema — its result type — producing a typed model, the accessor map, and source files (#872).
/// The outputs here resolve without an external source document (a workflow output reads a step output, which reads
/// the workflow inputs / <c>$statusCode</c>), so the test is self-contained and also exercises the transitive
/// <c>$steps.&lt;id&gt;.outputs.&lt;name&gt;</c> resolution (seam A) through the model layer.
/// </summary>
[TestClass]
public class WorkflowOutputsModelGeneratorTests
{
    private const string Document = """
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
                  "maxResults": { "type": "integer" }
                },
                "required": [ "petId" ]
              },
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "outputs": {
                    "chosenPet": "$inputs.petId",
                    "code": "$statusCode"
                  }
                }
              ],
              "outputs": {
                "result": "$steps.getPet.outputs.chosenPet",
                "status": "$steps.getPet.outputs.code",
                "echoed": "$inputs.maxResults"
              }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generates_a_typed_workflow_result_model_and_accessor_map()
    {
        WorkflowOutputsModel? model = await WorkflowOutputsModelGenerator.GenerateAsync(
            Encoding.UTF8.GetBytes(Document), [], "adopt", "Acme.Pets.Workflows.Models.Adopt.Result");

        model.ShouldNotBeNull();

        // A generated model type, in the requested per-workflow result namespace.
        model!.TypeName.ShouldStartWith("Acme.Pets.Workflows.Models.Adopt.Result");

        // Each declared workflow output maps to its generated PascalCase dotnet accessor.
        model.Accessors["result"].ShouldBe("Result");
        model.Accessors["status"].ShouldBe("Status");
        model.Accessors["echoed"].ShouldBe("Echoed");

        model.Files.ShouldNotBeEmpty();
        model.Files.ShouldContain(f => f.Content.Contains("Result", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Returns_null_for_a_workflow_with_no_outputs()
    {
        const string noOutputs = """
            {
              "arazzo": "1.0.1",
              "info": { "title": "t", "version": "1.0.0" },
              "workflows": [
                { "workflowId": "adopt", "steps": [ { "stepId": "ping", "operationId": "ping" } ] }
              ]
            }
            """;

        WorkflowOutputsModel? model = await WorkflowOutputsModelGenerator.GenerateAsync(
            Encoding.UTF8.GetBytes(noOutputs), [], "adopt", "Acme.Pets.Workflows.Models.Adopt.Result");

        // No declared outputs → an empty object schema → no typed properties. The generator still yields a type
        // (an empty object) but with no accessors; assert the accessor map is empty rather than throwing.
        (model is null || model.Accessors.Count == 0).ShouldBeTrue();
    }
}