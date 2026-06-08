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

        // No CompiledCriterion: the operand is navigated from the live body to a JsonElement and read as
        // a Comparand, with the string literal baked once into a static readonly byte[].
        code.Fields.ShouldNotContain("CompiledCriterion");
        code.Fields.ShouldContain("\"ok\"u8.ToArray();");
        code.Statements.ShouldContain("JsonElement getPet_C0o0 = default;");
        code.Statements.ShouldContain("getPetResponseBody.TryResolvePointer(\"/status\"u8, out JsonElement getPet_C0o0n0)");
        code.Statements.ShouldContain("getPet_C0o0 = getPet_C0o0n0;");
        code.Statements.ShouldContain("if (!(Comparand.FromJsonElement(getPet_C0o0).ValueEquals(Comparand.FromUtf8String(getPet_C0o1Lit))))");
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
        code.Statements.ShouldContain("Comparand.FromJsonElement(getPet_C0o0).GreaterThan(Comparand.FromNumber(5))");
        // Lone operand → IsTrue.
        code.Statements.ShouldContain("Comparand.FromJsonElement(getPet_C1o0).IsTrue");
        code.Statements.ShouldContain("if (!(Comparand.FromJsonElement(getPet_C0o0).GreaterThan(Comparand.FromNumber(5)) && Comparand.FromJsonElement(getPet_C1o0).IsTrue))");
    }

    [TestMethod]
    public void Inlines_a_compound_simple_condition_with_logical_operators()
    {
        // The full grammar (||, &&, !, grouping) is inlined — operands within one criterion share its
        // index and the logical structure is emitted as a C# boolean expression.
        StepBodyCode code = Emit([new StepCriterion("simple", "($response.body#/count > 5 || $response.body#/count == 0) && !$response.body#/error", null)]);

        code.Fields.ShouldNotContain("CompiledCriterion");

        // Operand bases are numbered per operand encountered (literals consume a slot too), so the
        // navigated body operands here are o0 (count), o2 (count), o4 (error).
        code.Statements.ShouldContain(
            "if (!(((Comparand.FromJsonElement(getPet_C0o0).GreaterThan(Comparand.FromNumber(5)) || Comparand.FromJsonElement(getPet_C0o2).ValueEquals(Comparand.FromNumber(0))) && !(Comparand.FromJsonElement(getPet_C0o4).IsTrue))))");
    }

    [TestMethod]
    public void Inlines_a_regex_criterion_as_a_generated_regex()
    {
        StepBodyCode code = Emit([new StepCriterion("regex", "^Fido$", "$response.body#/name")]);

        // The pattern is compiled ahead-of-time via [GeneratedRegex]; the context is resolved
        // statically and matched through the RegexCriterion runtime helper — no CompiledCriterion.
        code.Fields.ShouldNotContain("CompiledCriterion");
        code.Fields.ShouldContain("[GeneratedRegex(\"^Fido$\", RegexOptions.CultureInvariant, 1000)]");
        code.Fields.ShouldContain("private static partial Regex getPet_C0Regex();");
        code.Statements.ShouldContain("getPetResponseBody.TryResolvePointer(\"/name\"u8, out JsonElement getPet_C0ctxn0)");
        code.Statements.ShouldContain("if (!(RegexCriterion.IsMatch(getPet_C0Regex(), getPet_C0ctx)))");
    }

    [TestMethod]
    public void Inlines_a_regex_criterion_against_the_status_code()
    {
        StepBodyCode code = Emit([new StepCriterion("regex", "^4", "$statusCode")]);

        code.Fields.ShouldContain("[GeneratedRegex(\"^4\", RegexOptions.CultureInvariant, 1000)]");
        code.Statements.ShouldContain("if (!(RegexCriterion.IsMatch(getPet_C0Regex(), getPetResponse.StatusCode)))");
    }

    [TestMethod]
    public void Falls_back_to_a_compiled_criterion_for_a_dynamic_regex_pattern()
    {
        // An embedded {expression} makes the pattern dynamic — [GeneratedRegex] needs a constant, so it
        // compiles to a CompiledCriterion instead.
        StepBodyCode code = Emit([new StepCriterion("regex", "^{$inputs.prefix}-", "$response.body#/name")]);

        code.Fields.ShouldNotContain("GeneratedRegex");
        code.Fields.ShouldContain("CompiledCriterion.Compile(CriterionType.Regex,");
        code.Statements.ShouldContain("getPet_SuccessCriterion0.Evaluate(context)");
    }

    [TestMethod]
    public void Falls_back_to_a_compiled_criterion_for_an_unsupported_source()
    {
        // $response.header is not statically navigable yet — the criterion compiles to a CompiledCriterion.
        StepBodyCode code = Emit([new StepCriterion("simple", "$response.header.foo == 'bar'", null)]);

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
