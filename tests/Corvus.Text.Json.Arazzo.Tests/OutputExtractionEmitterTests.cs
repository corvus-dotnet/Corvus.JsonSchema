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
    [TestMethod]
    public void Emits_compiled_fields_and_a_closure_free_object_build()
    {
        OutputExtractionCode code = OutputExtractionEmitter.Emit(
            "getPet",
            [new OutputMapping("id", "$response.body#/id"), new OutputMapping("name", "$response.body#/name")],
            "workspace",
            "context");

        code.Fields.ShouldContain("private static readonly ArazzoExpression getPet_Output_id = ArazzoExpression.Parse(\"$response.body#/id\");");
        code.Fields.ShouldContain("private static readonly ArazzoExpression getPet_Output_name = ArazzoExpression.Parse(\"$response.body#/name\");");

        // Closure-free, context-carrying build delegate; values added directly with no intermediate buffers.
        code.Statements.ShouldContain("var getPetOutputs = JsonElement.CreateBuilder(");
        code.Statements.ShouldContain("static (in WorkflowExecutionContext ctx, ref JsonElement.ObjectBuilder builder) =>");
        code.Statements.ShouldContain("ctx.TryResolveValue(getPet_Output_id, out JsonElement idValue);");
        code.Statements.ShouldContain("builder.AddProperty(\"id\"u8, idValue);");
        code.Statements.ShouldContain("context.SetStepOutputs(\"getPet\", getPetOutputs.RootElement);");
    }

    [TestMethod]
    public void Emits_nothing_when_the_step_has_no_outputs()
    {
        OutputExtractionCode code = OutputExtractionEmitter.Emit("getPet", [], "workspace", "context");

        code.Fields.ShouldBeEmpty();
        code.Statements.ShouldBeEmpty();
    }
}