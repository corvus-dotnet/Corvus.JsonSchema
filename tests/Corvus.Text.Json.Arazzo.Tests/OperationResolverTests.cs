// <copyright file="OperationResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class OperationResolverTests
{
    private const string Spec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets": {
              "get": { "operationId": "listPets", "responses": { "200": { "description": "ok" } } },
              "post": { "operationId": "createPet", "responses": { "201": { "description": "made" } } }
            },
            "/pets/{petId}": {
              "get": { "operationId": "getPet", "responses": { "200": { "description": "ok" } } }
            },
            "/pets/{petId}/~care": {
              "get": { "responses": { "200": { "description": "ok" } } }
            }
          }
        }
        """;

    private ParsedJsonDocument<JsonElement>? document;

    [TestMethod]
    public void Resolves_operation_by_id()
    {
        OperationResolver resolver = Create();

        resolver.TryResolveOperationId("getPet", out ResolvedOperation op).ShouldBeTrue();
        op.SourceName.ShouldBe("petstore");
        op.Path.ShouldBe("/pets/{petId}");
        op.Method.ShouldBe(OperationMethod.Get);
        op.OperationId.ShouldBe("getPet");
    }

    [TestMethod]
    public void Resolves_post_operation_by_id()
    {
        OperationResolver resolver = Create();

        resolver.TryResolveOperationId("createPet", out ResolvedOperation op).ShouldBeTrue();
        op.Path.ShouldBe("/pets");
        op.Method.ShouldBe(OperationMethod.Post);
    }

    [TestMethod]
    public void Unknown_operation_id_does_not_resolve()
    {
        OperationResolver resolver = Create();

        resolver.TryResolveOperationId("nope", out ResolvedOperation op).ShouldBeFalse();
        op.ShouldBe(default);
    }

    [TestMethod]
    public void Resolves_operation_by_path_pointer()
    {
        OperationResolver resolver = Create();

        bool resolved = resolver.TryResolveOperationPath(
            "{$sourceDescriptions.petstore.url}#/paths/~1pets~1{petId}/get",
            out ResolvedOperation op);

        resolved.ShouldBeTrue();
        op.SourceName.ShouldBe("petstore");
        op.Path.ShouldBe("/pets/{petId}");
        op.Method.ShouldBe(OperationMethod.Get);
        op.OperationId.ShouldBe("getPet");
    }

    [TestMethod]
    public void Resolves_operation_by_path_pointer_unescaping_tilde()
    {
        OperationResolver resolver = Create();

        // The path key "/pets/{petId}/~care" pointer-escapes to "~1pets~1{petId}~1~0care"
        // ('/' -> ~1, '~' -> ~0); the resolver must unescape it back.
        bool resolved = resolver.TryResolveOperationPath(
            "#/paths/~1pets~1{petId}~1~0care/get",
            out ResolvedOperation op);

        resolved.ShouldBeTrue();
        op.Path.ShouldBe("/pets/{petId}/~care");
        op.OperationId.ShouldBeNull();
    }

    [TestMethod]
    public void Operation_path_without_fragment_does_not_resolve()
    {
        OperationResolver resolver = Create();

        resolver.TryResolveOperationPath("https://example.com/openapi.json", out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Operation_path_to_missing_node_does_not_resolve()
    {
        OperationResolver resolver = Create();

        resolver.TryResolveOperationPath("#/paths/~1missing/get", out _).ShouldBeFalse();
    }

    // The resolver holds a JsonElement into the document's memory, so the document must stay
    // alive for the resolver's lifetime; dispose it after each test rather than inside Create.
    [TestCleanup]
    public void Cleanup() => this.document?.Dispose();

    private OperationResolver Create()
    {
        this.document = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(Spec));
        return OperationResolver.Create("petstore", this.document.RootElement);
    }
}