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
    public void Maps_operation_id_to_pascal_cased_request_response_types()
    {
        var operation = new ResolvedOperation("petstore", "/pets/{petId}", OperationMethod.Get, "getPet");

        GeneratedOperationTypes types = OperationTypeNameMapper.Map(operation, "Acme.Pets");

        types.MethodName.ShouldBe("GetPet");
        types.RequestTypeName.ShouldBe("Acme.Pets.GetPetRequest");
        types.ResponseTypeName.ShouldBe("Acme.Pets.GetPetResponse");
    }

    [TestMethod]
    public void Pascal_cases_snake_and_kebab_operation_ids()
    {
        var operation = new ResolvedOperation("petstore", "/pets", OperationMethod.Post, "create-new_pet");

        GeneratedOperationTypes types = OperationTypeNameMapper.Map(operation, "Acme.Pets");

        types.MethodName.ShouldBe("CreateNewPet");
    }

    [TestMethod]
    public void Derives_method_name_from_method_and_path_when_no_operation_id()
    {
        var operation = new ResolvedOperation("petstore", "/pets/{petId}", OperationMethod.Delete, null);

        GeneratedOperationTypes types = OperationTypeNameMapper.Map(operation, "Acme.Pets");

        types.MethodName.ShouldBe("DeletePetsPetId");
        types.RequestTypeName.ShouldBe("Acme.Pets.DeletePetsPetIdRequest");
        types.ResponseTypeName.ShouldBe("Acme.Pets.DeletePetsPetIdResponse");
    }

    [TestMethod]
    public void Empty_namespace_is_rejected()
    {
        var operation = new ResolvedOperation("petstore", "/pets", OperationMethod.Get, "listPets");

        Should.Throw<ArgumentException>(() => OperationTypeNameMapper.Map(operation, string.Empty));
    }
}