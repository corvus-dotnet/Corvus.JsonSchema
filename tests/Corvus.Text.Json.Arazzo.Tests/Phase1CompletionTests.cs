// <copyright file="Phase1CompletionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class Phase1CompletionTests
{
    [TestMethod]
    public void Simple_not_operator()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(200);

        CompiledCriterion.Compile(CriterionType.Simple, "!($statusCode == 500)").Evaluate(context).ShouldBeTrue();
        CompiledCriterion.Compile(CriterionType.Simple, "!($statusCode == 200)").Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    public void Simple_not_on_lone_boolean()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "active": false }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "!$response.body#/active").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Simple_dot_property_navigation()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "count": 10 }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$response.body.count > 5").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Simple_index_navigation()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "items": ["a", "b", "c"] }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$response.body.items[1] == 'b'").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Simple_numeric_string_coercion()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "x": "3" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        // "3" (a JSON string) coerced to a number for the numeric operator.
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x < 5").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Resolve_workflow_output()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "token": "t" }""");
        var context = new WorkflowExecutionContext();
        context.SetWorkflowOutputs("auth", doc.RootElement);

        context.TryResolveValue(ArazzoExpression.Parse("$workflows.auth.outputs.token"), out JsonElement value).ShouldBeTrue();
        value.GetString().ShouldBe("t");
    }

    [TestMethod]
    public void Resolve_workflow_input()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "password": "p" }""");
        var context = new WorkflowExecutionContext();
        context.SetWorkflowInputs("auth", doc.RootElement);

        context.TryResolveValue(ArazzoExpression.Parse("$workflows.auth.inputs.password"), out JsonElement value).ShouldBeTrue();
        value.GetString().ShouldBe("p");
    }

    [TestMethod]
    public void Workflow_criterion_uses_workflow_output()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "token": "t" }""");
        var context = new WorkflowExecutionContext();
        context.SetWorkflowOutputs("auth", doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$workflows.auth.outputs.token == 't'").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Jsonpath_with_embedded_expression()
    {
        using ParsedJsonDocument<JsonElement> body = Parse("""[ { "status": "ok" } ]""");
        using ParsedJsonDocument<JsonElement> inputs = Parse("""{ "expected": "ok" }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(body.RootElement);
        context.SetInputs(inputs.RootElement);

        CompiledCriterion.Compile(
            CriterionType.JsonPath,
            "$[?@.status == \"{$inputs.expected}\"]",
            "$response.body").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Regex_with_embedded_expression()
    {
        using ParsedJsonDocument<JsonElement> body = Parse("""{ "status": "ok" }""");
        using ParsedJsonDocument<JsonElement> inputs = Parse("""{ "pat": "ok" }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(body.RootElement);
        context.SetInputs(inputs.RootElement);

        CompiledCriterion.Compile(
            CriterionType.Regex,
            "^({$inputs.pat})$",
            "$response.body#/status").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Regex_quantifier_braces_are_literal()
    {
        // a{2,3} must be a regex quantifier, not an embedded expression.
        var context = new WorkflowExecutionContext();
        context.SetResponseHeader("X", "aaa");

        CompiledCriterion.Compile(CriterionType.Regex, "^a{2,3}$", "$response.header.X")
            .Evaluate(context).ShouldBeTrue();
    }

    private static ParsedJsonDocument<JsonElement> Parse(string json)
        => ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
}