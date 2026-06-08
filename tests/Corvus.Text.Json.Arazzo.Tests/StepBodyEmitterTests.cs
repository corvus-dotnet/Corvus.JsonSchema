// <copyright file="StepBodyEmitterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class StepBodyEmitterTests
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
            [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Acme.Pets.JsonString", true, "petId")],
            false,
            [new ResponseDescriptor("200", "Acme.Pets.Pet", "OkBody")],
            "Acme.Pets.PetsClient",
            "GetPetAsync",
            null));

    [TestMethod]
    public void Invokes_the_generated_client_method_with_named_arguments()
    {
        StepBodyCode code = Emit([]);

        code.Statements.ShouldContain("var getPetClient = new Acme.Pets.PetsClient(transport);");
        code.Statements.ShouldContain("var getPetResponse = await getPetClient.GetPetAsync(petId: petIdValue, cancellationToken: cancellationToken).ConfigureAwait(false);");
        code.Statements.ShouldContain("ArazzoTelemetry.StepsExecuted.Add(1);");
        code.Statements.ShouldContain("context.SetResponseStatusCode(getPetResponse.StatusCode);");
    }

    [TestMethod]
    public void Binds_the_response_body_as_a_live_reference_without_cloning()
    {
        StepBodyCode code = Emit([]);

        // The matched-status body is a live reference (used by criteria while the response is alive);
        // the whole-body CloneAsBuilder is gone.
        code.Statements.ShouldContain("if (getPetResponse.StatusCode == 200) { getPetResponseBody = (JsonElement)getPetResponse.OkBody; }");
        code.Statements.ShouldContain("context.SetResponseBody(getPetResponseBody);");
        code.Statements.ShouldNotContain("CloneAsBuilder");
    }

    [TestMethod]
    public void Disposes_the_response_in_a_finally()
    {
        StepBodyCode code = Emit([]);

        code.Statements.ShouldContain("finally");
        code.Statements.ShouldContain("await getPetResponse.DisposeAsync().ConfigureAwait(false);");
    }

    [TestMethod]
    public void Skips_the_response_body_clone_when_the_body_is_not_referenced()
    {
        StepBodyCode code = StepBodyEmitter.Emit(
            "getPet",
            GetPet,
            [new StepArgument("petId", "$inputs.petId")],
            [],
            [],
            "transport",
            "workspace",
            "context",
            "cancellationToken",
            NoSteps,
            "inputs",
            requestBody: null,
            bindResponseBody: false);

        // No body clone is emitted at all …
        code.Statements.ShouldNotContain("SetResponseBody");
        code.Statements.ShouldNotContain("CloneAsBuilder");

        // … but the status is still recorded and the response is still disposed.
        code.Statements.ShouldContain("context.SetResponseStatusCode(getPetResponse.StatusCode);");
        code.Statements.ShouldContain("await getPetResponse.DisposeAsync().ConfigureAwait(false);");
    }

    [TestMethod]
    public void Compiles_success_criteria_into_static_fields_and_gates_on_them()
    {
        StepBodyCode code = Emit(
        [
            new StepCriterion("simple", "$statusCode == 200", null),
            new StepCriterion("jsonpath", "$.id", "$response.body"),
        ]);

        // The bare $statusCode comparison is emitted inline (no field); the jsonpath criterion still
        // compiles to a CompiledCriterion field, keeping its per-criterion index.
        code.Fields.ShouldNotContain("$statusCode == 200");
        code.Fields.ShouldContain("CompiledCriterion.Compile(CriterionType.JsonPath, \"$.id\", \"$response.body\");");
        code.Statements.ShouldContain("if (!(getPetResponse.StatusCode == 200 && getPet_SuccessCriterion1.Evaluate(context)))");
        code.Statements.ShouldContain("throw new WorkflowStepFailedException(\"getPet\", \"Step 'getPet' did not satisfy its success criteria.\");");
    }

    [TestMethod]
    public void Emits_a_bare_status_code_criterion_inline_without_a_compiled_field()
    {
        StepBodyCode code = Emit([new StepCriterion("simple", "$statusCode != 500", null)]);

        // A pure $statusCode comparison needs neither a CompiledCriterion field nor the context.
        code.Fields.ShouldNotContain("CompiledCriterion");
        code.Statements.ShouldContain("if (!(getPetResponse.StatusCode != 500))");
        code.Statements.ShouldContain("throw new WorkflowStepFailedException(\"getPet\", \"Step 'getPet' did not satisfy its success criteria.\");");
    }

    [TestMethod]
    public void Inlines_a_body_comparison_simple_criterion_against_the_live_body()
    {
        StepBodyCode code = Emit([new StepCriterion("simple", "$response.body#/status == 'ok'", null)]);

        // No CompiledCriterion: the operand is navigated from the live body and compared via Comparand,
        // with the string literal baked once into a static readonly byte[].
        code.Fields.ShouldNotContain("CompiledCriterion");
        code.Fields.ShouldContain("\"ok\"u8.ToArray();");
        code.Statements.ShouldContain("getPetResponseBody.TryResolvePointer(\"/status\"u8, out JsonElement getPet_C0L0)");
        code.Statements.ShouldContain("getPet_C0L = Comparand.FromJsonElement(getPet_C0L0);");
        code.Statements.ShouldContain("if (!(getPet_C0L.ValueEquals(Comparand.FromUtf8String(getPet_C0RLit))))");
    }

    [TestMethod]
    public void Inlines_a_numeric_body_comparison_and_a_lone_truthy_operand()
    {
        StepBodyCode code = Emit(
        [
            new StepCriterion("simple", "$response.body#/count > 5", null),
            new StepCriterion("simple", "$response.body#/active", null),
        ]);

        code.Fields.ShouldNotContain("CompiledCriterion");
        // Numeric comparison → GreaterThan against a Comparand.FromNumber literal.
        code.Statements.ShouldContain("getPet_C0L.GreaterThan(Comparand.FromNumber(5))");
        // Lone operand → IsTrue.
        code.Statements.ShouldContain("getPet_C1L.IsTrue");
        code.Statements.ShouldContain("if (!(getPet_C0L.GreaterThan(Comparand.FromNumber(5)) && getPet_C1L.IsTrue))");
    }

    [TestMethod]
    public void Falls_back_to_a_compiled_criterion_for_a_compound_simple_condition()
    {
        // A condition using the logical grammar is not inlined yet — it compiles to a CompiledCriterion.
        StepBodyCode code = Emit([new StepCriterion("simple", "$response.body#/count > 5 && $response.body#/active", null)]);

        code.Fields.ShouldContain("CompiledCriterion.Compile(CriterionType.Simple,");
        code.Statements.ShouldContain("getPet_SuccessCriterion0.Evaluate(context)");
    }

    [TestMethod]
    public void Defaults_to_http_success_when_no_criteria()
    {
        StepBodyCode code = Emit([]);

        code.Statements.ShouldContain("if (!getPetResponse.IsSuccess)");
        code.Statements.ShouldContain("throw new WorkflowStepFailedException(\"getPet\", \"Step 'getPet' returned an unsuccessful status.\");");
        code.Fields.ShouldNotContain("CompiledCriterion");
    }

    private static StepBodyCode Emit(IReadOnlyList<StepCriterion> criteria)
        => StepBodyEmitter.Emit(
            "getPet",
            GetPet,
            [new StepArgument("petId", "$inputs.petId")],
            criteria,
            [],
            "transport",
            "workspace",
            "context",
            "cancellationToken",
            NoSteps,
            "inputs");
}
