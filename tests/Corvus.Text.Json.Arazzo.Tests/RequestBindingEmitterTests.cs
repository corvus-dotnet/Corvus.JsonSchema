// <copyright file="RequestBindingEmitterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class RequestBindingEmitterTests
{
    private static readonly Dictionary<string, string> NoSteps = new(StringComparer.Ordinal);

    private static readonly ResolvedOperation GetPet = new(
        "petstore",
        new OperationDescriptor(
            "/pets/{petId}",
            OperationMethod.Get,
            "getPet",
            "GetPet",
            "Acme.Pets.GetPetRequest",
            "Acme.Pets.GetPetResponse",
            [
                new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Acme.Pets.JsonString", true, "petId"),
                new RequestParameterInfo("limit", ParameterLocation.Query, "Limit", "Acme.Pets.JsonInt32", false, "limit"),
            ],
            false,
            [new ResponseDescriptor("200", "Acme.Pets.Pet", "OkBody")],
            "Acme.Pets.PetsClient",
            "GetPetAsync",
            null));

    private static readonly ResolvedOperation CreatePet = new(
        "petstore",
        new OperationDescriptor(
            "/pets",
            OperationMethod.Post,
            "createPet",
            "CreatePet",
            "Acme.Pets.CreatePetRequest",
            "Acme.Pets.CreatePetResponse",
            [],
            true,
            [new ResponseDescriptor("201", "Acme.Pets.Pet", "CreatedBody")],
            "Acme.Pets.PetsClient",
            "CreatePetAsync",
            "Acme.Pets.NewPet"));

    [TestMethod]
    public void Binds_the_request_body_expression_to_the_body_parameter()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            CreatePet,
            [],
            "context",
            "CreatePet_",
            NoSteps,
            "$inputs.pet");

        code.Fields.ShouldContain("private static readonly ArazzoExpression CreatePet_Body = ArazzoExpression.Parse(\"$inputs.pet\");");
        code.Statements.ShouldContain("context.TryResolveValue(CreatePet_Body, out JsonElement bodyValue);");
        code.NamedArguments.ShouldContain("body: Acme.Pets.NewPet.From(bodyValue)");
    }

    [TestMethod]
    public void Does_not_bind_a_body_when_the_operation_has_no_body_type()
    {
        // GetPet has RequestBodyTypeName == null, so even a supplied body expression is not bound.
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId")],
            "context",
            "GetPet_",
            NoSteps,
            "$inputs.pet");

        code.NamedArguments.ShouldNotContain(a => a.StartsWith("body:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Does_not_bind_a_body_when_no_body_expression_is_supplied()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            CreatePet,
            [],
            "context",
            "CreatePet_",
            NoSteps);

        code.NamedArguments.ShouldNotContain(a => a.StartsWith("body:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emits_compiled_expression_fields_for_each_argument()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId"), new StepArgument("limit", "$inputs.limit")],
            "context",
            "GetPet_",
            NoSteps);

        code.Fields.ShouldContain("private static readonly ArazzoExpression GetPet_PetId = ArazzoExpression.Parse(\"$inputs.petId\");");
        code.Fields.ShouldContain("private static readonly ArazzoExpression GetPet_Limit = ArazzoExpression.Parse(\"$inputs.limit\");");
    }

    [TestMethod]
    public void Resolves_each_argument_and_binds_it_to_a_named_client_parameter()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId"), new StepArgument("limit", "$inputs.limit")],
            "context",
            "GetPet_",
            NoSteps);

        // Resolve each argument to a JsonElement, then bind it as a named argument with From().
        code.Statements.ShouldContain("context.TryResolveValue(GetPet_PetId, out JsonElement petIdValue);");
        code.NamedArguments.ShouldContain("petId: Acme.Pets.JsonString.From(petIdValue)");
        code.NamedArguments.ShouldContain("limit: Acme.Pets.JsonInt32.From(limitValue)");
    }

    [TestMethod]
    public void Omits_optional_parameter_when_no_argument_is_supplied()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId")],
            "context",
            "GetPet_",
            NoSteps);

        code.NamedArguments.ShouldContain("petId: Acme.Pets.JsonString.From(petIdValue)");
        code.NamedArguments.ShouldNotContain(a => a.StartsWith("limit:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Missing_required_argument_throws()
    {
        Should.Throw<InvalidOperationException>(() => RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("limit", "$inputs.limit")],
            "context",
            "GetPet_",
            NoSteps));
    }
}
