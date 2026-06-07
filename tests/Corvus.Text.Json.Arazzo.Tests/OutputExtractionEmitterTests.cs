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
    public void Pre_resolves_values_then_builds_via_a_span_context()
    {
        OutputExtractionCode code = OutputExtractionEmitter.Emit(
            "getPet",
            [new OutputMapping("id", "$response.body#/id"), new OutputMapping("name", "$response.body#/name")],
            "workspace",
            "context",
            NoSteps);

        code.Fields.ShouldContain("private static readonly ArazzoExpression getPet_Output_id = ArazzoExpression.Parse(\"$response.body#/id\");");

        // Values are resolved in method scope (current-step sources via the field-based context)...
        code.Statements.ShouldContain("context.TryResolveValue(getPet_Output_id, out JsonElement getPetOutput0);");
        code.Statements.ShouldContain("context.TryResolveValue(getPet_Output_name, out JsonElement getPetOutput1);");

        // ...then built via a closure-free ReadOnlySpan<JsonElement> context — no dictionary, no SetStepOutputs.
        code.Statements.ShouldContain("Span<JsonElement> getPetOutputsValues = [getPetOutput0, getPetOutput1];");
        code.Statements.ShouldContain("static (in ReadOnlySpan<JsonElement> values, ref JsonElement.ObjectBuilder builder) =>");
        code.Statements.ShouldContain("builder.AddProperty(\"id\"u8, values[0]);");
        code.Statements.ShouldContain("JsonElement getPetOutputsElement = getPetOutputs.RootElement;");
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
            stepLocals);

        // Direct navigation of the prior step's outputs local — no context, no dictionary, no field.
        code.Statements.ShouldContain("loginOutputsElement.TryGetProperty(\"token\"u8, out JsonElement useOutput0);");
        code.Fields.ShouldBeEmpty();
        code.Statements.ShouldNotContain("SetStepOutputs");
    }

    [TestMethod]
    public void Emits_nothing_when_the_step_has_no_outputs()
    {
        OutputExtractionCode code = OutputExtractionEmitter.Emit("getPet", [], "workspace", "context", NoSteps);

        code.Fields.ShouldBeEmpty();
        code.Statements.ShouldBeEmpty();
    }
}