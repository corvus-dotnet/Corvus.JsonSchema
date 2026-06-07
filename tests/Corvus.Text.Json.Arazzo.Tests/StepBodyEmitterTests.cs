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
            [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Acme.Pets.JsonString", true)],
            false,
            [new ResponseDescriptor("200", "Acme.Pets.Pet", "OkBody")]));

    [TestMethod]
    public void Emits_send_telemetry_and_response_feed()
    {
        StepBodyCode code = Emit([]);

        code.Statements.ShouldContain("var getPetResponse = await transport.SendAsync<Acme.Pets.GetPetRequest, Acme.Pets.GetPetResponse>(getPetRequest, cancellationToken).ConfigureAwait(false);");
        code.Statements.ShouldContain("ArazzoTelemetry.StepsExecuted.Add(1);");
        code.Statements.ShouldContain("context.SetResponseStatusCode(getPetResponse.StatusCode);");
        code.Statements.ShouldContain("if (getPetResponse.StatusCode == 200) { context.SetResponseBody(getPetResponse.OkBody); }");
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
        code.Statements.ShouldContain("did not satisfy its success criteria");
    }

    [TestMethod]
    public void Defaults_to_http_success_when_no_criteria()
    {
        StepBodyCode code = Emit([]);

        code.Statements.ShouldContain("if (!getPetResponse.IsSuccess)");
        code.Statements.ShouldContain("returned an unsuccessful status");
        code.Fields.ShouldNotContain("CompiledCriterion");
    }

    private static StepBodyCode Emit(IReadOnlyList<StepCriterion> criteria)
        => StepBodyEmitter.Emit(
            "getPet",
            GetPet,
            [new StepArgument("petId", "$inputs.petId")],
            criteria,
            "transport",
            "context",
            "cancellationToken",
            NoSteps);
}