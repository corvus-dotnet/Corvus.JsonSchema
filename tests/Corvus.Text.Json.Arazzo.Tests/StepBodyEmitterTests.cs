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
        code.Statements.ShouldContain("var getPetResponse = await getPetClient.GetPetAsync(petId: Acme.Pets.JsonString.From(petIdValue), cancellationToken: cancellationToken).ConfigureAwait(false);");
        code.Statements.ShouldContain("ArazzoTelemetry.StepsExecuted.Add(1);");
        code.Statements.ShouldContain("context.SetResponseStatusCode(getPetResponse.StatusCode);");
    }

    [TestMethod]
    public void Clones_the_response_body_into_the_workspace_before_feeding_the_context()
    {
        StepBodyCode code = Emit([]);

        code.Statements.ShouldContain("if (getPetResponse.StatusCode == 200) { context.SetResponseBody(((JsonElement)getPetResponse.OkBody).CloneAsBuilder(workspace).RootElement); }");
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
            "transport",
            "workspace",
            "context",
            "cancellationToken",
            NoSteps,
            requestBodyExpression: null,
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

        code.Fields.ShouldContain("CompiledCriterion.Compile(CriterionType.Simple, \"$statusCode == 200\");");
        code.Fields.ShouldContain("CompiledCriterion.Compile(CriterionType.JsonPath, \"$.id\", \"$response.body\");");
        code.Statements.ShouldContain("if (!(getPet_SuccessCriterion0.Evaluate(context) && getPet_SuccessCriterion1.Evaluate(context)))");
        code.Statements.ShouldContain("throw new WorkflowStepFailedException(\"getPet\", \"Step 'getPet' did not satisfy its success criteria.\");");
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
            "transport",
            "workspace",
            "context",
            "cancellationToken",
            NoSteps);
}
