// <copyright file="WorkflowInputsModelGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Tests that <see cref="WorkflowInputsModelGenerator"/> drives the JSON Schema code generator over a
/// workflow's inline inputs schema, producing a model type, the accessor map, and source files.
/// </summary>
[TestClass]
public class WorkflowInputsModelGeneratorTests
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
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ]
                }
              ]
            }
          ]
        }
        """;

    [TestMethod]
    public async Task Generates_a_typed_inputs_model_and_accessor_map()
    {
        WorkflowInputsModel? model = await WorkflowInputsModelGenerator.GenerateAsync(
            Encoding.UTF8.GetBytes(Document), 0, "Acme.Pets.Workflows.Models");

        model.ShouldNotBeNull();

        // A generated model type, in the requested namespace.
        model!.TypeName.ShouldStartWith("Acme.Pets.Workflows.Models");

        // The accessor map maps each JSON input name to its generated PascalCase dotnet property.
        model.Accessors["petId"].ShouldBe("PetId");
        model.Accessors["maxResults"].ShouldBe("MaxResults");

        // The model source files were generated.
        model.Files.ShouldNotBeEmpty();
        model.Files.ShouldContain(f => f.Content.Contains("PetId", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Returns_a_model_whose_files_compile_shape()
    {
        WorkflowInputsModel? model = await WorkflowInputsModelGenerator.GenerateAsync(
            Encoding.UTF8.GetBytes(Document), 0, "Acme.Pets.Workflows.Models");

        // Every generated file is non-empty C# with the model namespace.
        model.ShouldNotBeNull();
        foreach (GeneratedModelFile file in model!.Files)
        {
            file.FileName.ShouldNotBeNullOrEmpty();
            file.Content.ShouldContain("namespace Acme.Pets.Workflows.Models");
        }
    }
}
