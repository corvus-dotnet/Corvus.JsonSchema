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
    public void Expression_resolves_to_a_JsonElement_passed_straight_as_the_source()
    {
        RequestBindingCode code = Emit([new StepArgument("petId", "$inputs.petId")]);

        code.Fields.ShouldContain("private static readonly ArazzoExpression GetPet_PetId = ArazzoExpression.Parse(\"$inputs.petId\");");
        code.Statements.ShouldContain("context.TryResolveValue(GetPet_PetId, out JsonElement petIdValue);");

        // No reification, no From — the JsonElement is the Source.
        code.NamedArguments.ShouldContain("petId: petIdValue");
        code.NamedArguments.ShouldNotContain(a => a.Contains(".From(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Literal_string_is_passed_as_a_utf8_content_span()
    {
        // The unescaped content of the JSON string "electronic".
        RequestBindingCode code = Emit([new StepArgument("petId", "electronic", ArgumentValueKind.LiteralString)]);

        code.NamedArguments.ShouldContain("petId: \"electronic\"u8");
        code.Fields.ShouldBeEmpty();
        code.Statements.ShouldBeEmpty();
    }

    [TestMethod]
    public void Literal_number_is_passed_as_a_numeric_literal()
    {
        RequestBindingCode code = Emit(
        [
            new StepArgument("petId", "$inputs.petId"),
            new StepArgument("limit", "10", ArgumentValueKind.LiteralNumber),
        ]);

        code.NamedArguments.ShouldContain("limit: 10");
    }

    [TestMethod]
    public void Literal_boolean_is_passed_as_a_bool_literal()
    {
        RequestBindingCode code = Emit(
        [
            new StepArgument("petId", "$inputs.petId"),
            new StepArgument("limit", "true", ArgumentValueKind.LiteralBoolean),
        ]);

        code.NamedArguments.ShouldContain("limit: true");
    }

    [TestMethod]
    public void Literal_object_is_a_parse_once_constant_passed_as_the_source()
    {
        RequestBindingCode code = Emit(
        [
            new StepArgument("petId", "$inputs.petId"),
            new StepArgument("limit", "{\"a\":1}", ArgumentValueKind.LiteralComposite),
        ]);

        code.Fields.ShouldContain("private static readonly ParsedJsonDocument<JsonElement> GetPet_Limit = ParsedJsonDocument<JsonElement>.Parse(");
        code.NamedArguments.ShouldContain("limit: GetPet_Limit.RootElement");
    }

    [TestMethod]
    public void Interpolated_value_is_built_and_passed_as_the_source()
    {
        RequestBindingCode code = Emit([new StepArgument("petId", "pet-{$inputs.id}", ArgumentValueKind.Interpolation)]);

        code.Fields.ShouldContain("private static readonly CompiledInterpolationTemplate GetPet_PetIdTemplate = CompiledInterpolationTemplate.Compile(\"pet-{$inputs.id}\");");
        code.NamedArguments.ShouldContain("petId: petIdValue");
    }

    [TestMethod]
    public void Omits_an_optional_parameter_with_no_argument()
    {
        RequestBindingCode code = Emit([new StepArgument("petId", "$inputs.petId")]);

        code.NamedArguments.ShouldContain("petId: petIdValue");
        code.NamedArguments.ShouldNotContain(a => a.StartsWith("limit:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Missing_required_argument_throws()
    {
        Should.Throw<InvalidOperationException>(() => Emit([new StepArgument("limit", "$inputs.limit")]));
    }

    [TestMethod]
    public void Expression_request_body_is_passed_straight_as_the_body_source()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            CreatePet, [], "context", "CreatePet_", NoSteps,
            new StepBody("$inputs.pet", ArgumentValueKind.Expression));

        code.Statements.ShouldContain("context.TryResolveValue(CreatePet_Body, out JsonElement bodyValue);");
        code.NamedArguments.ShouldContain("body: bodyValue");
        code.NamedArguments.ShouldNotContain(a => a.Contains(".From(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Literal_string_request_body_is_passed_as_a_utf8_content_span()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            CreatePet, [], "context", "CreatePet_", NoSteps,
            new StepBody("electronic", ArgumentValueKind.LiteralString));

        code.NamedArguments.ShouldContain("body: \"electronic\"u8");
    }

    [TestMethod]
    public void No_body_is_bound_when_the_operation_has_no_body_type()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet, [new StepArgument("petId", "$inputs.petId")], "context", "GetPet_", NoSteps,
            new StepBody("$inputs.pet", ArgumentValueKind.Expression));

        code.NamedArguments.ShouldNotContain(a => a.StartsWith("body:", StringComparison.Ordinal));
    }

    private static RequestBindingCode Emit(IReadOnlyList<StepArgument> arguments)
        => RequestBindingEmitter.Emit(GetPet, arguments, "context", "GetPet_", NoSteps);
}
