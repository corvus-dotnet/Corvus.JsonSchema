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

        code.Statements.ShouldContain("((JsonElement)inputs).TryGetProperty(\"petId\"u8, out JsonElement petIdValue);");

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
    public void Interpolated_value_is_inlined_into_a_pooled_buffer_and_passed_as_the_source()
    {
        RequestBindingCode code = Emit([new StepArgument("petId", "pet-{$inputs.id}", ArgumentValueKind.Interpolation)]);

        // The template is inlined directly: a workspace-pooled buffer, the literal segment as a UTF-8
        // write, and the $inputs.id fragment statically navigated and appended. No runtime interpreter,
        // no CompiledInterpolationTemplate field, and no WorkflowExecutionContext.
        code.Fields.ShouldNotContain("CompiledInterpolationTemplate");
        code.Statements.ShouldContain("workspace.RentWriterAndBuffer(256, out IByteBufferWriter GetPet_PetIdInterpBuffer)");
        code.Statements.ShouldContain("Interpolation.AppendUtf8(GetPet_PetIdInterpBuffer, \"pet-\"u8);");
        code.Statements.ShouldContain("Interpolation.AppendValue(GetPet_PetIdInterpBuffer,");
        code.Statements.ShouldNotContain("context");

        // The buffer's span is passed straight to the client as its Source (no reified document), and
        // the buffer is returned after the call.
        code.NamedArguments.ShouldContain("petId: GetPet_PetIdInterpBuffer.WrittenSpan");
        code.Cleanup.ShouldContain("workspace.ReturnWriterAndBuffer(GetPet_PetIdInterpWriter, GetPet_PetIdInterpBuffer);");
    }

    [TestMethod]
    public void Interpolated_value_with_an_unobservable_fragment_falls_back_to_the_interpreter()
    {
        // $url is not statically navigable, so the template cannot be inlined and the emitter falls
        // back to the context-based CompiledInterpolationTemplate path.
        RequestBindingCode code = Emit([new StepArgument("petId", "{$url}", ArgumentValueKind.Interpolation)]);

        code.Fields.ShouldContain("CompiledInterpolationTemplate GetPet_PetIdTemplate = CompiledInterpolationTemplate.Compile(\"{$url}\");");
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
            CreatePet, [], "context", "CreatePet_", NoSteps, "inputs", null,
            new StepBody("$inputs.pet", ArgumentValueKind.Expression));

        code.Statements.ShouldContain("((JsonElement)inputs).TryGetProperty(\"pet\"u8, out JsonElement bodyValue);");
        code.NamedArguments.ShouldContain("body: bodyValue");
        code.NamedArguments.ShouldNotContain(a => a.Contains(".From(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Literal_string_request_body_is_passed_as_a_utf8_content_span()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            CreatePet, [], "context", "CreatePet_", NoSteps, "inputs", null,
            new StepBody("electronic", ArgumentValueKind.LiteralString));

        code.NamedArguments.ShouldContain("body: \"electronic\"u8");
    }

    [TestMethod]
    public void No_body_is_bound_when_the_operation_has_no_body_type()
    {
        RequestBindingCode code = RequestBindingEmitter.Emit(
            GetPet, [new StepArgument("petId", "$inputs.petId")], "context", "GetPet_", NoSteps, "inputs", null,
            new StepBody("$inputs.pet", ArgumentValueKind.Expression));

        code.NamedArguments.ShouldNotContain(a => a.StartsWith("body:", StringComparison.Ordinal));
    }

    private static RequestBindingCode Emit(IReadOnlyList<StepArgument> arguments)
        => RequestBindingEmitter.Emit(GetPet, arguments, "context", "GetPet_", NoSteps, "inputs", null);
}
