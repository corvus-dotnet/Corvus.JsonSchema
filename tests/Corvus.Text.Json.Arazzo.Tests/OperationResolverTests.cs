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
              "get": { "operationId": "list-pets", "responses": { "200": { "description": "ok" } } },
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

    [TestMethod]
    public void Resolves_operation_by_id_with_authoritative_generated_names()
    {
        OperationResolver resolver = Create();

        resolver.TryResolveOperationId("getPet", out ResolvedOperation op).ShouldBeTrue();
        op.SourceName.ShouldBe("petstore");
        op.Path.ShouldBe("/pets/{petId}");
        op.Method.ShouldBe(OperationMethod.Get);
        op.OperationId.ShouldBe("getPet");

        // The names come straight from the OpenAPI generator — no Arazzo-side heuristic.
        op.MethodName.ShouldBe("GetPet");
        op.RequestTypeName.ShouldBe("GetPetRequest");
        op.ResponseTypeName.ShouldBe("GetPetResponse");
    }

    [TestMethod]
    public void Generated_method_name_pascal_cases_a_kebab_operation_id()
    {
        OperationResolver resolver = Create();

        resolver.TryResolveOperationId("list-pets", out ResolvedOperation op).ShouldBeTrue();
        op.MethodName.ShouldBe("ListPets");
        op.RequestTypeName.ShouldBe("ListPetsRequest");
    }

    [TestMethod]
    public void Resolves_post_operation_by_id()
    {
        OperationResolver resolver = Create();

        resolver.TryResolveOperationId("createPet", out ResolvedOperation op).ShouldBeTrue();
        op.Path.ShouldBe("/pets");
        op.Method.ShouldBe(OperationMethod.Post);
        op.ResponseTypeName.ShouldBe("CreatePetResponse");
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
        op.MethodName.ShouldBe("GetPet");
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
    public void Operation_path_to_unknown_operation_does_not_resolve()
    {
        OperationResolver resolver = Create();

        resolver.TryResolveOperationPath("#/paths/~1missing/get", out _).ShouldBeFalse();
    }

    private static OperationResolver Create()
    {
        // The resolver retains only strings from the generator's operation list, not the document,
        // so it is safe to dispose the parsed document immediately after creation.
        using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(Spec));
        return OperationResolver.Create("petstore", doc.RootElement);
    }
}