// <copyright file="OperationTypeNameMapperTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class OperationTypeNameMapperTests
{
    [TestMethod]
    public void Qualifies_generated_type_names_with_the_client_namespace()
    {
        var operation = new ResolvedOperation(
            "petstore",
            "/pets/{petId}",
            OperationMethod.Get,
            "getPet",
            "GetPet",
            "GetPetRequest",
            "GetPetResponse");

        GeneratedOperationTypes types = OperationTypeNameMapper.Map(operation, "Acme.Pets");

        types.MethodName.ShouldBe("GetPet");
        types.RequestTypeName.ShouldBe("Acme.Pets.GetPetRequest");
        types.ResponseTypeName.ShouldBe("Acme.Pets.GetPetResponse");
    }

    [TestMethod]
    public void Passes_generator_supplied_names_through_verbatim()
    {
        // The mapper applies no heuristic — whatever simple names the generator assigned are used.
        var operation = new ResolvedOperation(
            "petstore",
            "/pets",
            OperationMethod.Post,
            null,
            "PostPets",
            "PostPetsRequest",
            "PostPetsResponse");

        GeneratedOperationTypes types = OperationTypeNameMapper.Map(operation, "Acme.Pets");

        types.RequestTypeName.ShouldBe("Acme.Pets.PostPetsRequest");
        types.ResponseTypeName.ShouldBe("Acme.Pets.PostPetsResponse");
    }

    [TestMethod]
    public void Empty_namespace_is_rejected()
    {
        var operation = new ResolvedOperation(
            "petstore",
            "/pets",
            OperationMethod.Get,
            "listPets",
            "ListPets",
            "ListPetsRequest",
            "ListPetsResponse");

        Should.Throw<ArgumentException>(() => OperationTypeNameMapper.Map(operation, string.Empty));
    }
}