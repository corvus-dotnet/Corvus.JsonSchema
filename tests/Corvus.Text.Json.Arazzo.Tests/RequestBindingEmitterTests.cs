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
            new StepBody("$inputs.pet", IsLiteral: false));

        code.Fields.ShouldContain("private static readonly ArazzoExpression CreatePet_Body = ArazzoExpression.Parse(\"$inputs.pet\");");
        code.Statements.ShouldContain("context.TryResolveValue(CreatePet_Body, out JsonElement bodyValue);");
        code.NamedArguments.ShouldContain("body: Acme.Pets.NewPet.From(bodyValue)");
    }

    [TestMethod]
    public void Binds_a_literal_request_body_via_a_fixed_document()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            CreatePet,
            [],
            "context",
            "CreatePet_",
            NoSteps,
            new StepBody("\"electronic\"", IsLiteral: true));

        code.Fields.ShouldContain("private static readonly Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement> CreatePet_BodyLiteral = Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement>.ForString(");
        code.Statements.ShouldContain("JsonElement bodyValue = CreatePet_BodyLiteral.RootElement;");
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
            new StepBody("$inputs.pet", IsLiteral: false));

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
    public void Binds_a_literal_string_value_via_the_specialised_fixed_document()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "\"42\"", IsLiteral: true)],
            "context",
            "GetPet_",
            NoSteps);

        // A scalar string wraps the raw UTF-8 directly (no parse), held as a never-disposed static.
        code.Fields.ShouldContain("private static readonly Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement> GetPet_PetIdLiteral = Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement>.ForString(");
        code.Statements.ShouldContain("JsonElement petIdValue = GetPet_PetIdLiteral.RootElement;");
        code.Statements.ShouldNotContain("context.TryResolveValue");
        code.NamedArguments.ShouldContain("petId: Acme.Pets.JsonString.From(petIdValue)");
    }

    [TestMethod]
    public void Binds_a_literal_number_value_via_the_specialised_fixed_document()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId"), new StepArgument("limit", "10", IsLiteral: true)],
            "context",
            "GetPet_",
            NoSteps);

        code.Fields.ShouldContain("private static readonly Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement> GetPet_LimitLiteral = Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement>.ForNumber(");
        code.NamedArguments.ShouldContain("limit: Acme.Pets.JsonInt32.From(limitValue)");
    }

    [TestMethod]
    public void Binds_a_literal_boolean_via_the_shared_singleton_with_no_field()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId"), new StepArgument("limit", "true", IsLiteral: true)],
            "context",
            "GetPet_",
            NoSteps);

        code.Statements.ShouldContain("JsonElement limitValue = Corvus.Text.Json.Internal.ValuelessJsonDocument<JsonElement>.BooleanTrue.RootElement;");
        code.Fields.ShouldNotContain("GetPet_LimitLiteral");
    }

    [TestMethod]
    public void Binds_a_literal_null_via_the_shared_singleton_with_no_field()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId"), new StepArgument("limit", "null", IsLiteral: true)],
            "context",
            "GetPet_",
            NoSteps);

        code.Statements.ShouldContain("JsonElement limitValue = Corvus.Text.Json.Internal.ValuelessJsonDocument<JsonElement>.Null.RootElement;");
        code.Fields.ShouldNotContain("GetPet_LimitLiteral");
    }

    [TestMethod]
    public void Binds_an_interpolated_parameter_value_via_a_compiled_template()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "pet-{$inputs.id}")],
            "context",
            "GetPet_",
            NoSteps);

        code.Fields.ShouldContain("private static readonly CompiledInterpolationTemplate GetPet_PetIdTemplate = CompiledInterpolationTemplate.Compile(\"pet-{$inputs.id}\");");
        code.Statements.ShouldContain("_ = context.TryInterpolate(GetPet_PetIdTemplate,");
        code.Statements.ShouldContain("JsonElement petIdValue = Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement>.ForUnescapedString(");
        code.NamedArguments.ShouldContain("petId: Acme.Pets.JsonString.From(petIdValue)");
    }

    [TestMethod]
    public void Binds_a_literal_object_value_via_a_parsed_document()
    {
        // Object/array/bool/null literals are not scalar — they fall back to ParsedJsonDocument.
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet,
            [new StepArgument("petId", "$inputs.petId"), new StepArgument("limit", "{\"a\":1}", IsLiteral: true)],
            "context",
            "GetPet_",
            NoSteps);

        code.Fields.ShouldContain("private static readonly ParsedJsonDocument<JsonElement> GetPet_LimitLiteral = ParsedJsonDocument<JsonElement>.Parse(");
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
