// <copyright file="OutputExtractionEmitterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class OutputExtractionEmitterTests
{
    private static readonly Dictionary<string, string> NoSteps = new(StringComparer.Ordinal);

    [TestMethod]
    public void Projects_response_body_values_from_the_live_body_copying_only_those_values()
    {
        OutputExtractionCode code = OutputExtractionEmitter.Emit(
            "getPet",
            [new OutputMapping("id", "$response.body#/id"), new OutputMapping("name", "$response.body#/name")],
            "workspace",
            "context",
            NoSteps,
            "inputs",
            null,
            "getPetResponseBody");

        // $response.body values are navigated from the live body and ONLY those values are copied
        // (CloneAsBuilder) into the workspace — no whole-body clone, no context resolution.
        code.Statements.ShouldContain("getPetResponseBody.TryResolvePointer(\"/id\"u8, out JsonElement getPetOutput0Nav)");
        code.Statements.ShouldContain("getPetOutput0 = getPetOutput0Nav.CloneAsBuilder(workspace).RootElement;");
        code.Statements.ShouldNotContain("context.TryResolveValue");

        // Built via a closure-free ReadOnlySpan<JsonElement> context, assigning the pre-declared element.
        code.Statements.ShouldContain("Span<JsonElement> getPetOutputsValues = [getPetOutput0, getPetOutput1];");
        code.Statements.ShouldContain("builder.AddProperty(\"id\"u8, values[0]);");
        code.Statements.ShouldContain("getPetOutputsElement = getPetOutputs.RootElement;");
        code.Statements.ShouldNotContain("JsonElement getPetOutputsElement =");
        code.Statements.ShouldNotContain("SetStepOutputs");
    }

    [TestMethod]
    public void Resolves_a_steps_reference_statically_against_the_step_local()
    {
        var stepLocals = new Dictionary<string, string>(StringComparer.Ordinal) { ["login"] = "loginOutputsElement" };

        OutputExtractionCode code = OutputExtractionEmitter.Emit(
            "use",
            [new OutputMapping("token", "$steps.login.outputs.token")],
            "workspace",
            "context",
            stepLocals,
            "inputs",
            null,
            null);

        // Direct navigation of the prior step's outputs local — no context, no dictionary, no field.
        code.Statements.ShouldContain("loginOutputsElement.TryGetProperty(\"token\"u8, out JsonElement useOutput0);");
        code.Fields.ShouldBeEmpty();
        code.Statements.ShouldNotContain("SetStepOutputs");
    }

    [TestMethod]
    public void Emits_nothing_when_the_step_has_no_outputs()
    {
        OutputExtractionCode code = OutputExtractionEmitter.Emit("getPet", [], "workspace", "context", NoSteps, "inputs", null, null);

        code.Fields.ShouldBeEmpty();
        code.Statements.ShouldBeEmpty();
    }
}