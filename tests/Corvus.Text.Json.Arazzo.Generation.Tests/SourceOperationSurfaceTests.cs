// <copyright file="SourceOperationSurfaceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Linq;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves <see cref="SourceOperationSurface"/> writes OpenAPI and AsyncAPI documents through to the
/// designer's operation surface in one pass: binding identities, summaries, merged parameters,
/// request/response content with RAW JSON Schema values (local $refs inlined, cycles bounded), and
/// the 2.x publish/subscribe → receive/send mapping the code generator uses. Assertions parse the
/// WRITTEN output — the wire shape is the contract.
/// </summary>
[TestClass]
public class SourceOperationSurfaceTests
{
    [TestMethod]
    public void An_openapi_document_projects_paths_methods_parameters_and_responses()
    {
        Stj.JsonElement surface = Project("""
        {
          "openapi": "3.1.0",
          "info": { "title": "Pets", "version": "1.0" },
          "paths": {
            "/pets/{petId}": {
              "parameters": [{ "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } }],
              "get": {
                "operationId": "getPet",
                "summary": "Fetch one pet",
                "parameters": [{ "name": "verbose", "in": "query", "schema": { "type": "boolean" } }],
                "responses": {
                  "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "name": { "type": "string" } } } } } },
                  "404": { "description": "missing" },
                  "default": { "description": "unexpected" }
                }
              },
              "delete": { "operationId": "deletePet", "deprecated": true, "responses": { "204": { "description": "gone" } } }
            }
          }
        }
        """);

        surface.GetArrayLength().ShouldBe(2);
        Stj.JsonElement get = surface[0];
        get.GetProperty("kind").GetString().ShouldBe("openapi");
        get.GetProperty("operationId").GetString().ShouldBe("getPet");
        get.GetProperty("path").GetString().ShouldBe("/pets/{petId}");
        get.GetProperty("method").GetString().ShouldBe("GET");
        get.GetProperty("summary").GetString().ShouldBe("Fetch one pet");
        get.TryGetProperty("deprecated", out _).ShouldBeFalse();

        // Path-level parameters come first, then the operation's own.
        Stj.JsonElement[] parameters = [.. get.GetProperty("parameters").EnumerateArray()];
        parameters.Select(p => p.GetProperty("name").GetString()).ShouldBe(["petId", "verbose"]);
        parameters[0].GetProperty("in").GetString().ShouldBe("path");
        parameters[0].GetProperty("required").GetBoolean().ShouldBeTrue();
        parameters[0].GetProperty("schema").GetProperty("type").GetString().ShouldBe("string");
        parameters[1].TryGetProperty("required", out _).ShouldBeFalse();

        Stj.JsonElement responses = get.GetProperty("responses");
        responses.EnumerateObject().Select(r => r.Name).ShouldBe(["200", "404", "default"]);
        responses.GetProperty("200").GetProperty("schema").GetProperty("properties").TryGetProperty("name", out _).ShouldBeTrue();
        responses.GetProperty("404").TryGetProperty("schema", out _).ShouldBeFalse();

        surface[1].GetProperty("deprecated").GetBoolean().ShouldBeTrue();
    }

    [TestMethod]
    public void The_request_body_prefers_json_content_and_inlines_local_refs()
    {
        Stj.JsonElement surface = Project("""
        {
          "openapi": "3.1.0",
          "components": {
            "schemas": {
              "Order": { "type": "object", "properties": { "card": { "$ref": "#/components/schemas/Card" } } },
              "Card": { "type": "object", "properties": { "number": { "type": "string" } } }
            }
          },
          "paths": {
            "/orders": {
              "post": {
                "operationId": "placeOrder",
                "requestBody": {
                  "content": {
                    "application/xml": { "schema": { "type": "string" } },
                    "application/json": { "schema": { "$ref": "#/components/schemas/Order" } }
                  }
                },
                "responses": { "201": { "description": "created" } }
              }
            }
          }
        }
        """);

        Stj.JsonElement post = surface.EnumerateArray().Single();
        Stj.JsonElement request = post.GetProperty("request");
        request.GetProperty("contentType").GetString().ShouldBe("application/json");
        string schema = request.GetProperty("schema").GetRawText();
        schema.ShouldContain("\"card\"");
        schema.ShouldContain("\"number\"", customMessage: "nested $refs inline too");
        schema.ShouldNotContain("$ref");
    }

    [TestMethod]
    public void Cyclic_refs_flatten_at_the_depth_bound_instead_of_hanging()
    {
        Stj.JsonElement surface = Project("""
        {
          "openapi": "3.1.0",
          "components": { "schemas": { "Node": { "type": "object", "properties": { "next": { "$ref": "#/components/schemas/Node" } } } } },
          "paths": {
            "/nodes": {
              "post": {
                "requestBody": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Node" } } } },
                "responses": { "201": { "description": "ok" } }
              }
            }
          }
        }
        """);

        string schema = surface.EnumerateArray().Single().GetProperty("request").GetProperty("schema").GetRawText();
        schema.ShouldContain("\"next\"");
        schema.ShouldContain("{}", customMessage: "the over-deep tail flattens to an empty schema");
    }

    [TestMethod]
    public void An_asyncapi_2x_document_maps_publish_to_receive_and_subscribe_to_send()
    {
        Stj.JsonElement surface = Project("""
        {
          "asyncapi": "2.6.0",
          "channels": {
            "order/confirmations": {
              "publish": {
                "operationId": "onConfirmation",
                "summary": "Confirmations arrive here",
                "message": { "payload": { "type": "object", "properties": { "confirmed": { "type": "boolean" } } } }
              },
              "subscribe": {
                "operationId": "emitReceipt",
                "message": { "oneOf": [ { "payload": { "type": "object", "properties": { "receiptId": { "type": "string" } } } }, { "payload": { "type": "string" } } ] }
              }
            }
          }
        }
        """);

        surface.GetArrayLength().ShouldBe(2);
        Stj.JsonElement receive = surface[0];
        receive.GetProperty("kind").GetString().ShouldBe("asyncapi");
        receive.GetProperty("channelPath").GetString().ShouldBe("order/confirmations");
        receive.GetProperty("action").GetString().ShouldBe("receive");
        receive.GetProperty("operationId").GetString().ShouldBe("onConfirmation");
        receive.GetProperty("request").GetProperty("schema").GetProperty("properties").TryGetProperty("confirmed", out _).ShouldBeTrue();

        Stj.JsonElement send = surface[1];
        send.GetProperty("action").GetString().ShouldBe("send");
        send.GetProperty("request").GetProperty("schema").GetProperty("properties").TryGetProperty("receiptId", out _)
            .ShouldBeTrue("a oneOf message projects its first alternative");
    }

    [TestMethod]
    public void An_asyncapi_30_document_projects_operations_over_channel_refs()
    {
        Stj.JsonElement surface = Project("""
        {
          "asyncapi": "3.0.0",
          "channels": {
            "confirmations": {
              "address": "order/confirmations",
              "messages": { "confirmation": { "payload": { "type": "object", "properties": { "at": { "type": "string" } } } } }
            }
          },
          "operations": {
            "receiveConfirmation": {
              "action": "receive",
              "summary": "Await the confirmation",
              "channel": { "$ref": "#/channels/confirmations" }
            }
          }
        }
        """);

        Stj.JsonElement op = surface.EnumerateArray().Single();
        op.GetProperty("kind").GetString().ShouldBe("asyncapi");
        op.GetProperty("operationId").GetString().ShouldBe("receiveConfirmation");
        op.GetProperty("channelPath").GetString().ShouldBe("order/confirmations");
        op.GetProperty("action").GetString().ShouldBe("receive");
        op.GetProperty("summary").GetString().ShouldBe("Await the confirmation");
        op.GetProperty("request").GetProperty("schema").GetProperty("properties").TryGetProperty("at", out _)
            .ShouldBeTrue("the channel's message payload projects when the operation names none");
    }

    [TestMethod]
    public void Malformed_documents_project_an_empty_surface_without_throwing()
    {
        Project("""{ "openapi": "3.1.0", "paths": "nope" }""").GetArrayLength().ShouldBe(0);
        Project("""{ "asyncapi": "2.6.0", "channels": [1, 2] }""").GetArrayLength().ShouldBe(0);
        Project("""{ "neither": true }""").GetArrayLength().ShouldBe(0);
        Project("""[]""").GetArrayLength().ShouldBe(0);
        Project("""{ "openapi": "3.1.0", "paths": { "/x": { "get": { "responses": null, "parameters": { "bad": true } } } } }""").GetArrayLength().ShouldBe(1);
    }

    // Drives the write-through projection and parses the WRITTEN output for assertions.
    private static Stj.JsonElement Project(string json)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
        var buffer = new ArrayBufferWriter<byte>(2048);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            SourceOperationSurface.WriteOperations(writer, document.RootElement);
        }

        using var parsed = Stj.JsonDocument.Parse(buffer.WrittenMemory.ToArray());
        return parsed.RootElement.Clone();
    }
}