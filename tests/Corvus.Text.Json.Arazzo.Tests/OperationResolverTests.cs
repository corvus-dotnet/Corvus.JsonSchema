// <copyright file="OperationResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class OperationResolverTests
{
    [TestMethod]
    public void Resolves_operation_by_id_carrying_generator_descriptor()
    {
        OperationResolver resolver = Create();

        resolver.TryResolveOperationId("getPet", out ResolvedOperation op).ShouldBeTrue();
        op.SourceName.ShouldBe("petstore");
        op.Operation.Path.ShouldBe("/pets/{petId}");
        op.Operation.Method.ShouldBe(OperationMethod.Get);
        op.Operation.RequestTypeName.ShouldBe("Acme.Pets.GetPetRequest");
        op.Operation.ResponseTypeName.ShouldBe("Acme.Pets.GetPetResponse");

        op.Operation.RequestParameters.Count.ShouldBe(1);
        op.Operation.RequestParameters[0].PropertyName.ShouldBe("PetId");
        op.Operation.RequestParameters[0].TypeName.ShouldBe("Acme.Pets.JsonString");
        op.Operation.RequestParameters[0].Location.ShouldBe(ParameterLocation.Path);
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
        op.Operation.Path.ShouldBe("/pets/{petId}");
        op.Operation.OperationId.ShouldBe("getPet");
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
        op.Operation.Path.ShouldBe("/pets/{petId}/~care");
        op.Operation.OperationId.ShouldBeNull();
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
        // Build the generator's descriptors directly — the resolver's contract is over descriptors,
        // not over a spec document.
        OperationDescriptor[] operations =
        [
            new(
                "/pets",
                OperationMethod.Get,
                "listPets",
                "ListPets",
                "Acme.Pets.ListPetsRequest",
                "Acme.Pets.ListPetsResponse",
                [],
                false),
            new(
                "/pets/{petId}",
                OperationMethod.Get,
                "getPet",
                "GetPet",
                "Acme.Pets.GetPetRequest",
                "Acme.Pets.GetPetResponse",
                [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Acme.Pets.JsonString", true)],
                false),
            new(
                "/pets/{petId}/~care",
                OperationMethod.Get,
                null,
                "GetPetsPetIdCare",
                "Acme.Pets.GetPetsPetIdCareRequest",
                "Acme.Pets.GetPetsPetIdCareResponse",
                [],
                false),
        ];

        return OperationResolver.Create("petstore", operations);
    }
}