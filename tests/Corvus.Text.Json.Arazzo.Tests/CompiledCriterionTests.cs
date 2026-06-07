// <copyright file="CompiledCriterionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class CompiledCriterionTests
{
    [TestMethod]
    [DataRow(200, true)]
    [DataRow(404, false)]
    public void Simple_status_code_equality(int statusCode, bool expected)
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(statusCode);
        CompiledCriterion criterion = CompiledCriterion.Compile(CriterionType.Simple, "$statusCode == 200");

        criterion.Evaluate(context).ShouldBe(expected);
    }

    [TestMethod]
    public void Simple_numeric_ordering()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "x": 3 }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x < 5").Evaluate(context).ShouldBeTrue();
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x >= 5").Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    public void Simple_string_equality()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "status": "ok" }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$response.body#/status == 'ok'").Evaluate(context).ShouldBeTrue();
        CompiledCriterion.Compile(CriterionType.Simple, "$response.body#/status != 'ok'").Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    public void Simple_string_equality_is_case_insensitive()
    {
        // Arazzo §Condition Evaluation: string comparisons MUST be case-insensitive.
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "status": "OK" }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$response.body#/status == 'ok'").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Simple_string_literal_escapes_doubled_single_quote()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "v": "it's ok" }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$response.body#/v == 'it''s ok'").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Simple_boolean_and_null()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "active": true, "maybe": null }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$response.body#/active == true").Evaluate(context).ShouldBeTrue();
        CompiledCriterion.Compile(CriterionType.Simple, "$response.body#/maybe == null").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Simple_lone_boolean_operand()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "active": true }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$response.body#/active").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Simple_and_or_with_parentheses()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(201);

        CompiledCriterion criterion = CompiledCriterion.Compile(
            CriterionType.Simple,
            "($statusCode == 200 || $statusCode == 201) && $statusCode != 500");

        criterion.Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Simple_undefined_operand_is_false()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "x": 1 }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.missing == 1").Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    [DataRow("Bearer xyz", true)]
    [DataRow("Basic abc", false)]
    public void Regex_against_response_header(string headerValue, bool expected)
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseHeader("Authorization", headerValue);

        CompiledCriterion criterion = CompiledCriterion.Compile(
            CriterionType.Regex,
            "^Bearer ",
            "$response.header.Authorization");

        criterion.Evaluate(context).ShouldBe(expected);
    }

    [TestMethod]
    public void Regex_missing_context_is_false()
    {
        var context = new WorkflowExecutionContext();

        CompiledCriterion criterion = CompiledCriterion.Compile(
            CriterionType.Regex,
            "^Bearer ",
            "$response.header.Authorization");

        criterion.Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    public void JsonPath_non_empty_passes()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "items": [1, 2, 3] }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.JsonPath, "$.items[*]", "$response.body")
            .Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void JsonPath_empty_fails()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "items": [] }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.JsonPath, "$.items[*]", "$response.body")
            .Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    public void Regex_requires_context_expression()
    {
        Should.Throw<ArgumentException>(() => CompiledCriterion.Compile(CriterionType.Regex, "^x$"));
    }

    private static ParsedJsonDocument<JsonElement> Parse(string json)
        => ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
}