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
                "parameters": [
                  { "name": "petId", "in": "path", "required": true, "schema": { "type": "integer", "format": "int64" } }
                ],
                "responses": {
                  "200": {
                    "headers": { "X-Rate-Limit": { "schema": { "type": "integer" } } },
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

        // Editor metadata: the operation reference, typed request parameters, and typed responses + headers.
        JsonElement step = Prop(Prop(wf, "steps"), "getPet");
        JsonElement operation = Prop(step, "operation");
        Prop(operation, "kind").GetString().ShouldBe("openapi");
        Prop(operation, "method").GetString().ShouldBe("get");
        Prop(operation, "path").GetString().ShouldBe("/pets/{petId}");
        Prop(operation, "source").GetString().ShouldBe("petstore");

        JsonElement petIdParam = Prop(Prop(Prop(step, "request"), "parameters"), "petId");
        Prop(petIdParam, "in").GetString().ShouldBe("path");
        Prop(petIdParam, "required").GetBoolean().ShouldBeTrue();
        Prop(Prop(petIdParam, "schema"), "type").GetString().ShouldBe("integer");

        JsonElement ok = Prop(Prop(step, "responses"), "200");
        Prop(Prop(ok, "body"), "type").GetString().ShouldBe("object");
        Prop(Prop(Prop(ok, "headers"), "X-Rate-Limit"), "type").GetString().ShouldBe("integer");
    }

    [TestMethod]
    public void Resolves_asyncapi_message_payload_for_a_channel_step()
    {
        const string AsyncWorkflow = """
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "events", "url": "./events.json", "type": "asyncapi" } ],
              "workflows": [
                {
                  "workflowId": "watch-lights-v1",
                  "inputs": { "type": "object", "properties": {} },
                  "steps": [
                    {
                      "stepId": "awaitMeasurement",
                      "operationId": "receiveLightMeasurement",
                      "outputs": { "lumens": "$message.payload#/lumens" }
                    }
                  ]
                }
              ]
            }
            """;
        const string Events = """
            {
              "asyncapi": "3.0.0",
              "info": { "title": "Streetlights", "version": "1.0.0" },
              "operations": {
                "receiveLightMeasurement": {
                  "action": "receive",
                  "channel": { "$ref": "#/channels/lightingMeasured" },
                  "messages": [ { "$ref": "#/channels/lightingMeasured/messages/lightMeasured" } ]
                }
              },
              "channels": {
                "lightingMeasured": {
                  "address": "light/measured",
                  "messages": { "lightMeasured": { "$ref": "#/components/messages/lightMeasured" } }
                }
              },
              "components": {
                "messages": {
                  "lightMeasured": {
                    "payload": { "type": "object", "properties": { "lumens": { "type": "integer" } } }
                  }
                }
              }
            }
            """;

        byte[] metadata = WorkflowSchemaMetadataGenerator.Generate(
            Encoding.UTF8.GetBytes(AsyncWorkflow),
            [new KeyValuePair<string, byte[]>("events", Encoding.UTF8.GetBytes(Events))]);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(metadata);
        JsonElement step = Prop(Prop(Prop(Prop(doc.RootElement, "workflows"), "watch-lights-v1"), "steps"), "awaitMeasurement");

        JsonElement operation = Prop(step, "operation");
        Prop(operation, "kind").GetString().ShouldBe("asyncapi");
        Prop(operation, "action").GetString().ShouldBe("receive");

        // The message payload schema resolves through the operation -> channel -> components message $ref chain.
        Prop(Prop(Prop(step, "message"), "payload"), "type").GetString().ShouldBe("object");

        // And $message.payload#/lumens resolves to the typed output.
        Prop(Prop(Prop(step, "outputs"), "lumens"), "type").GetString().ShouldBe("integer");
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

    [TestMethod]
    public void Normalises_unions_tuples_and_maps()
    {
        const string Shapes = """
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [],
              "workflows": [
                {
                  "workflowId": "shapes-v1",
                  "inputs": {
                    "type": "object",
                    "properties": {
                      "payment": {
                        "discriminator": { "propertyName": "kind" },
                        "oneOf": [
                          { "type": "object", "title": "Card", "properties": { "kind": { "const": "card" }, "pan": { "type": "string" } } },
                          { "type": "object", "title": "Bank", "properties": { "kind": { "const": "bank" }, "iban": { "type": "string" } } }
                        ]
                      },
                      "maybeName": { "oneOf": [ { "type": "string", "maxLength": 5 }, { "type": "null" } ] },
                      "point": { "type": "array", "prefixItems": [ { "type": "number" }, { "type": "number" } ], "items": { "type": "string" } },
                      "labels": { "type": "object", "additionalProperties": { "type": "string" } }
                    }
                  },
                  "steps": []
                }
              ]
            }
            """;

        byte[] metadata = WorkflowSchemaMetadataGenerator.Generate(Encoding.UTF8.GetBytes(Shapes), []);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(metadata);
        JsonElement props = Prop(Prop(Prop(Prop(doc.RootElement, "workflows"), "shapes-v1"), "inputs"), "properties");

        // oneOf with a discriminator becomes a typed variant picker, variants carry their titles + const props.
        JsonElement payment = Prop(props, "payment");
        Prop(payment, "type").GetString().ShouldBe("union");
        Prop(payment, "discriminator").GetString().ShouldBe("kind");
        Prop(payment, "variants").GetArrayLength().ShouldBe(2);
        Prop(Prop(payment, "variants")[0], "title").GetString().ShouldBe("Card");
        Prop(Prop(Prop(Prop(payment, "variants")[0], "properties"), "kind"), "const").GetString().ShouldBe("card");

        // "X | null" collapses to a nullable X (the constraints of X survive).
        JsonElement maybe = Prop(props, "maybeName");
        Prop(maybe, "type").GetString().ShouldBe("string");
        Prop(maybe, "nullable").GetBoolean().ShouldBeTrue();
        Prop(maybe, "maxLength").GetInt32().ShouldBe(5);

        // A tuple: positional prefixItems plus a trailing schema for additional items.
        JsonElement point = Prop(props, "point");
        Prop(point, "type").GetString().ShouldBe("array");
        Prop(point, "prefixItems").GetArrayLength().ShouldBe(2);
        Prop(Prop(point, "prefixItems")[0], "type").GetString().ShouldBe("number");
        Prop(Prop(point, "items"), "type").GetString().ShouldBe("string");

        // A free-form map: additionalProperties carries the value schema.
        JsonElement labels = Prop(props, "labels");
        Prop(labels, "type").GetString().ShouldBe("object");
        Prop(Prop(labels, "additionalProperties"), "type").GetString().ShouldBe("string");
    }

    private static JsonElement Prop(JsonElement element, string name)
    {
        element.TryGetProperty(name, out JsonElement value).ShouldBeTrue($"expected property '{name}'");
        return value;
    }
}
