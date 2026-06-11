// <copyright file="WorkflowSchemaMetadataGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Tests <see cref="WorkflowSchemaMetadataGenerator"/> — the precomputed typed-shape metadata for a workflow
/// package (workflow inputs + each step's resolved output types).
/// </summary>
[TestClass]
public class WorkflowSchemaMetadataGeneratorTests
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
                "responses": {
                  "200": {
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "name": { "type": "string", "maxLength": 40 },
                            "status": { "type": "string", "enum": [ "available", "pending", "sold" ] }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    [TestMethod]
    public void Generates_typed_inputs_and_resolved_step_outputs()
    {
        byte[] metadata = WorkflowSchemaMetadataGenerator.Generate(
            Encoding.UTF8.GetBytes(Workflow),
            [new KeyValuePair<string, byte[]>("petstore", Encoding.UTF8.GetBytes(Petstore))]);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(metadata);
        JsonElement root = doc.RootElement;
        Prop(root, "formatVersion").GetInt32().ShouldBe(1);

        JsonElement wf = Prop(Prop(root, "workflows"), "adopt-pet-v1");

        // Inputs: the inline schema is normalised, including format + the recognised numeric constraint.
        JsonElement inputs = Prop(wf, "inputs");
        Prop(inputs, "type").GetString().ShouldBe("object");
        JsonElement petId = Prop(Prop(inputs, "properties"), "petId");
        Prop(petId, "type").GetString().ShouldBe("integer");
        Prop(petId, "format").GetString().ShouldBe("int64");
        Prop(petId, "minimum").GetInt32().ShouldBe(1);
        Prop(inputs, "required")[0].GetString().ShouldBe("petId");

        // Step outputs resolve to concrete types from the OpenAPI response schema / inputs / $statusCode.
        JsonElement outputs = Prop(Prop(Prop(wf, "steps"), "getPet"), "outputs");
        Prop(Prop(outputs, "petName"), "type").GetString().ShouldBe("string");
        Prop(Prop(outputs, "petName"), "maxLength").GetInt32().ShouldBe(40);
        Prop(Prop(outputs, "code"), "type").GetString().ShouldBe("integer");
        Prop(Prop(outputs, "echoedId"), "type").GetString().ShouldBe("integer");

        // The enum carries through for a suitable dropdown control.
        JsonElement status = Prop(outputs, "status");
        Prop(status, "type").GetString().ShouldBe("string");
        Prop(status, "enum").GetArrayLength().ShouldBe(3);
        Prop(status, "enum")[0].GetString().ShouldBe("available");
    }

    [TestMethod]
    public void Degrades_to_unknown_when_outputs_cannot_be_resolved()
    {
        // No sources → the $response.body output can't be typed, but generation still succeeds.
        byte[] metadata = WorkflowSchemaMetadataGenerator.Generate(Encoding.UTF8.GetBytes(Workflow), []);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(metadata);
        JsonElement outputs = Prop(Prop(Prop(Prop(Prop(doc.RootElement, "workflows"), "adopt-pet-v1"), "steps"), "getPet"), "outputs");
        Prop(Prop(outputs, "petName"), "type").GetString().ShouldBe("unknown");

        // $statusCode + $inputs still resolve without sources.
        Prop(Prop(outputs, "code"), "type").GetString().ShouldBe("integer");
        Prop(Prop(outputs, "echoedId"), "type").GetString().ShouldBe("integer");
    }

    private static JsonElement Prop(JsonElement element, string name)
    {
        element.TryGetProperty(name, out JsonElement value).ShouldBeTrue($"expected property '{name}'");
        return value;
    }
}
