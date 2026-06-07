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
                new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Acme.Pets.JsonString", true),
                new RequestParameterInfo("limit", ParameterLocation.Query, "Limit", "Acme.Pets.JsonInt32", false),
            ],
            false,
            [new ResponseDescriptor("200", "Acme.Pets.Pet", "OkBody")]));

    [TestMethod]
    public void Emits_compiled_expression_fields_for_each_argument()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId"), new StepArgument("limit", "$inputs.limit")],
            "context",
            "request",
            "GetPet_",
            NoSteps);

        code.Fields.ShouldContain("private static readonly ArazzoExpression GetPet_PetId = ArazzoExpression.Parse(\"$inputs.petId\");");
        code.Fields.ShouldContain("private static readonly ArazzoExpression GetPet_Limit = ArazzoExpression.Parse(\"$inputs.limit\");");
    }

    [TestMethod]
    public void Binds_required_param_via_constructor_and_optional_via_initializer()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId"), new StepArgument("limit", "$inputs.limit")],
            "context",
            "request",
            "GetPet_",
            NoSteps);

        // Resolve each argument to a JsonElement, then From()-bind to the generated property type.
        code.Statements.ShouldContain("context.TryResolveValue(GetPet_PetId, out JsonElement petIdValue);");
        code.Statements.ShouldContain("var request = new Acme.Pets.GetPetRequest(Acme.Pets.JsonString.From(petIdValue))");
        code.Statements.ShouldContain("Limit = Acme.Pets.JsonInt32.From(limitValue),");
    }

    [TestMethod]
    public void Omits_optional_parameter_when_no_argument_is_supplied()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId")],
            "context",
            "request",
            "GetPet_",
            NoSteps);

        code.Statements.ShouldContain("var request = new Acme.Pets.GetPetRequest(Acme.Pets.JsonString.From(petIdValue));");
        code.Statements.ShouldNotContain("Limit =");
    }

    [TestMethod]
    public void Missing_required_argument_throws()
    {
        Should.Throw<InvalidOperationException>(() => RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("limit", "$inputs.limit")],
            "context",
            "request",
            "GetPet_",
            NoSteps));
    }
}