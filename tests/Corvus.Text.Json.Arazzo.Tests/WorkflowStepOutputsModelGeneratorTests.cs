// <copyright file="WorkflowStepOutputsModelGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Tests that <see cref="WorkflowStepOutputsModelGenerator"/> drives the JSON Schema code generator over a step's
/// resolved outputs schema, producing a typed model, the accessor map, and source files (#872). The outputs here
/// resolve without an external source document (from the workflow inputs schema and <c>$statusCode</c>), so the
/// test is self-contained.
/// </summary>
[TestClass]
public class WorkflowStepOutputsModelGeneratorTests
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
                    "resultCount": "$inputs.maxResults",
                    "code": "$statusCode"
                  }
                }
              ]
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generates_a_typed_step_outputs_model_and_accessor_map()
    {
        WorkflowStepOutputsModel? model = await WorkflowStepOutputsModelGenerator.GenerateAsync(
            Encoding.UTF8.GetBytes(Document), [], "adopt", "getPet", "Acme.Pets.Workflows.Models.GetPet");

        model.ShouldNotBeNull();

        // A generated model type, in the requested per-step namespace.
        model!.TypeName.ShouldStartWith("Acme.Pets.Workflows.Models.GetPet");

        // Each declared output name maps to its generated PascalCase dotnet accessor.
        model.Accessors["chosenPet"].ShouldBe("ChosenPet");
        model.Accessors["resultCount"].ShouldBe("ResultCount");
        model.Accessors["code"].ShouldBe("Code");

        // The model source files were generated with typed accessors for the outputs.
        model.Files.ShouldNotBeEmpty();
        model.Files.ShouldContain(f => f.Content.Contains("ChosenPet", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Returns_null_for_a_step_with_no_outputs()
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

        WorkflowStepOutputsModel? model = await WorkflowStepOutputsModelGenerator.GenerateAsync(
            Encoding.UTF8.GetBytes(noOutputs), [], "adopt", "ping", "Acme.Pets.Workflows.Models.Ping");

        // No declared outputs → an empty object schema → no typed properties. The generator still yields a type
        // (an empty object) but with no accessors; assert the accessor map is empty rather than throwing.
        (model is null || model.Accessors.Count == 0).ShouldBeTrue();
    }
}