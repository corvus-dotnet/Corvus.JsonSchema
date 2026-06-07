// <copyright file="CoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class CoverageTests
{
    // ---- $outputs resolution + interpolation ----
    [TestMethod]
    public void Resolve_workflow_outputs_current()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "result": "done" }""");
        var context = new WorkflowExecutionContext();
        context.SetWorkflowOutputs(doc.RootElement);

        context.TryResolveValue(ArazzoExpression.Parse("$outputs.result"), out JsonElement value).ShouldBeTrue();
        value.GetString().ShouldBe("done");
        context.TryInterpolate("r={$outputs.result}", out string s).ShouldBeTrue();
        s.ShouldBe("r=done");
    }

    // ---- TryResolveString across scalar + JSON + literal + not-found ----
    [TestMethod]
    public void TryResolveString_scalar_and_json_sources()
    {
        using ParsedJsonDocument<JsonElement> body = Parse("""{"n":42,"obj":{"a":1}}""");
        var context = new WorkflowExecutionContext();
        context.SetRequest("https://h/x", "GET");
        context.SetResponseStatusCode(204);
        context.SetRequestQueryParameter("q", "qv");
        context.SetRequestPathParameter("p", "pv");
        context.SetRequestHeader("rh", "rhv");
        context.SetMessageHeader("mh", "mhv");
        context.SetResponseBody(body.RootElement);

        Resolve(context, "$url").ShouldBe("https://h/x");
        Resolve(context, "$method").ShouldBe("GET");
        Resolve(context, "$statusCode").ShouldBe("204");
        Resolve(context, "$request.query.q").ShouldBe("qv");
        Resolve(context, "$request.path.p").ShouldBe("pv");
        Resolve(context, "$request.header.rh").ShouldBe("rhv");
        Resolve(context, "$message.header.mh").ShouldBe("mhv");
        Resolve(context, "literalValue").ShouldBe("literalValue");      // literal
        Resolve(context, "$response.body#/n").ShouldBe("42");           // JSON number -> raw text
        Resolve(context, "$response.body#/obj").ShouldBe("""{"a":1}""");// JSON object -> raw text

        context.TryResolveString(ArazzoExpression.Parse("$url"), out _).ShouldBeTrue();
        context.TryResolveString(ArazzoExpression.Parse("$method"), out _).ShouldBeTrue();
    }

    [TestMethod]
    public void TryResolveString_missing_returns_false()
    {
        var context = new WorkflowExecutionContext();
        context.TryResolveString(ArazzoExpression.Parse("$statusCode"), out _).ShouldBeFalse();
        context.TryResolveString(ArazzoExpression.Parse("$url"), out _).ShouldBeFalse();
        context.TryResolveString(ArazzoExpression.Parse("$request.header.x"), out _).ShouldBeFalse();
        context.TryResolveString(ArazzoExpression.Parse("$response.body#/x"), out _).ShouldBeFalse();
    }

    // ---- Comparand branches via simple criteria ----
    [TestMethod]
    public void Simple_object_equality_by_canonical_text()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "a": { "x": 1 }, "b": { "x": 1 }, "c": { "x": 2 } }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        Eval(context, "$response.body#/a == $response.body#/b").ShouldBeTrue();
        Eval(context, "$response.body#/a == $response.body#/c").ShouldBeFalse();
    }

    [TestMethod]
    public void Simple_mismatched_kinds_not_equal()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(200);

        Eval(context, "$statusCode == '200'").ShouldBeFalse();   // number vs string
    }

    [TestMethod]
    public void Simple_ordering_on_non_numeric_is_false()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "s": "abc" }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        Eval(context, "$response.body#/s < 5").ShouldBeFalse();
        Eval(context, "$response.body#/s >= 5").ShouldBeFalse();
    }

    [TestMethod]
    public void Simple_navigation_to_missing_member_is_false()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "a": 1 }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        Eval(context, "$response.body.missing == 1").ShouldBeFalse();
    }

    [TestMethod]
    public void Simple_double_quoted_literal_accepted()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "s": "hi" }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        Eval(context, "$response.body#/s == \"hi\"").ShouldBeTrue();
    }

    // ---- unhandled JSON-valued sources resolve to false ----
    [TestMethod]
    [DataRow("$self")]
    [DataRow("$sourceDescriptions.petStore.url")]
    [DataRow("$components.parameters.apiKey")]
    public void Unhandled_sources_resolve_false(string expression)
    {
        var context = new WorkflowExecutionContext();
        context.TryResolveValue(ArazzoExpression.Parse(expression), out _).ShouldBeFalse();
    }

    // ---- ArazzoExpression literal fallbacks ----
    [TestMethod]
    [DataRow("$response.unknown")]
    [DataRow("$message.unknown")]
    [DataRow("$request.unknown")]
    [DataRow("$workflows.onlyId")]
    public void Unrecognized_subforms_are_literal(string expression)
    {
        ArazzoExpression.Parse(expression).Source.ShouldBe(ArazzoExpressionSource.Literal);
    }

    // ---- Parser error paths ----
    [TestMethod]
    [DataRow("($statusCode == 200")]      // missing ')'
    [DataRow("$statusCode == 200 extra")] // trailing content
    [DataRow("$statusCode ==")]            // missing operand
    [DataRow("$statusCode == abc")]        // unrecognized literal
    public void Simple_malformed_conditions_throw(string condition)
    {
        Should.Throw<FormatException>(() => SimpleConditionEvaluator.Compile(condition));
    }

    // ---- argument guards ----
    [TestMethod]
    public void Argument_guards()
    {
        var context = new WorkflowExecutionContext();
        Should.Throw<ArgumentNullException>(() => SimpleConditionEvaluator.Compile(null!));
        Should.Throw<ArgumentNullException>(() => CompiledCriterion.Compile(CriterionType.Simple, null!));
        Should.Throw<ArgumentNullException>(() => SimpleConditionEvaluator.Compile("$statusCode == 200").Evaluate(null!));
        Should.Throw<ArgumentNullException>(() => context.SetStepOutputs(null!, default));
        Should.Throw<ArgumentNullException>(() => context.SetWorkflowInputs(null!, default));
        Should.Throw<ArgumentNullException>(() => context.SetWorkflowOutputs(null!, default));
        Should.Throw<ArgumentNullException>(() => context.TryInterpolate(null!, out _));
    }

    [TestMethod]
    public void JsonPath_unresolved_context_is_false()
    {
        var context = new WorkflowExecutionContext();
        CompiledCriterion.Compile(CriterionType.JsonPath, "$.items[*]", "$response.body")
            .Evaluate(context).ShouldBeFalse();
    }

    // ---- telemetry smoke (names + instruments execute) ----
    [TestMethod]
    public void Telemetry_instruments_are_usable()
    {
        ArazzoTelemetry.ActivitySourceName.ShouldBe("Corvus.Arazzo");
        ArazzoTelemetry.MeterName.ShouldBe("Corvus.Arazzo");
        ArazzoTelemetry.ActivitySource.ShouldNotBeNull();
        ArazzoTelemetry.Meter.ShouldNotBeNull();

        // No listener attached: these are no-ops but must not throw and exercise the instruments.
        ArazzoTelemetry.WorkflowsStarted.Add(1);
        ArazzoTelemetry.WorkflowsCompleted.Add(1);
        ArazzoTelemetry.WorkflowsFaulted.Add(1);
        ArazzoTelemetry.StepsExecuted.Add(1);
        ArazzoTelemetry.StepRetries.Add(1);
        ArazzoTelemetry.Gotos.Add(1);
        ArazzoTelemetry.WorkflowDuration.Record(0.1);
        ArazzoTelemetry.StepDuration.Record(0.1);
    }

    [TestMethod]
    public void Interpolate_all_scalar_sources()
    {
        var context = new WorkflowExecutionContext();
        context.SetRequest("https://h", "POST");
        context.SetResponseStatusCode(201);
        context.SetRequestQueryParameter("q", "qv");
        context.SetRequestPathParameter("p", "pv");
        context.SetRequestHeader("rh", "rhv");
        context.SetMessageHeader("mh", "mhv");

        context.TryInterpolate("{$url}|{$method}|{$statusCode}|{$request.query.q}|{$request.path.p}|{$request.header.rh}|{$message.header.mh}", out string s).ShouldBeTrue();
        s.ShouldBe("https://h|POST|201|qv|pv|rhv|mhv");
    }

    [TestMethod]
    public void Interpolate_embedded_array_bool_null()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{"arr":[1,2],"flag":true,"none":null}""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        context.TryInterpolate("{$response.body#/arr}|{$response.body#/flag}|{$response.body#/none}", out string s).ShouldBeTrue();
        s.ShouldBe("[1,2]|true|null");
    }

    [TestMethod]
    public void Interpolate_unrecognized_dollar_embedded_is_literal_text()
    {
        var context = new WorkflowExecutionContext();
        context.TryInterpolate("x{$bogus}y", out string s).ShouldBeTrue();
        s.ShouldBe("x$bogusy");
    }

    [TestMethod]
    public void Interpolate_unresolved_scalar_returns_false()
    {
        var context = new WorkflowExecutionContext();
        context.TryInterpolate("{$url}", out _).ShouldBeFalse();
        context.TryInterpolate("{$request.header.x}", out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Simple_dollar_token_with_no_navigation_unresolved_is_false()
    {
        var context = new WorkflowExecutionContext();

        // "$bogus" parses to a literal that resolves to nothing -> comparison false.
        Eval(context, "$bogus == 1").ShouldBeFalse();
    }

    [TestMethod]
    public void Simple_numeric_ge_le_true()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(200);

        Eval(context, "$statusCode >= 200 && $statusCode <= 200").ShouldBeTrue();
    }

    [TestMethod]
    public void Regex_dynamic_unresolved_expression_is_false()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseHeader("X", "abc");

        CompiledCriterion.Compile(CriterionType.Regex, "^({$inputs.missing})$", "$response.header.X")
            .Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    public void JsonPath_dynamic_unresolved_expression_is_false()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""[{"status":"ok"}]""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.JsonPath, "$[?@.status == \"{$inputs.missing}\"]", "$response.body")
            .Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    public void Interpolate_to_buffer_unresolved_returns_false()
    {
        var context = new WorkflowExecutionContext();
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        context.TryInterpolate("{$inputs.missing}", output).ShouldBeFalse();
        output.WrittenCount.ShouldBe(0);
    }

    [TestMethod]
    public void Simple_navigation_on_scalar_source_is_false()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(200);

        // Navigating into a scalar ($statusCode.x) yields an undefined operand -> comparison false.
        Eval(context, "$statusCode.x == 1").ShouldBeFalse();
    }

    [TestMethod]
    public void Simple_conditions_over_scalar_sources()
    {
        var context = new WorkflowExecutionContext();
        context.SetRequest("https://h", "GET");
        context.SetRequestHeader("rh", "rhv");
        context.SetRequestQueryParameter("q", "qv");
        context.SetRequestPathParameter("p", "pv");
        context.SetResponseHeader("sh", "shv");
        context.SetMessageHeader("mh", "mhv");

        Eval(context, "$url == 'https://h'").ShouldBeTrue();
        Eval(context, "$method == 'GET'").ShouldBeTrue();
        Eval(context, "$request.header.rh == 'rhv'").ShouldBeTrue();
        Eval(context, "$request.query.q == 'qv'").ShouldBeTrue();
        Eval(context, "$request.path.p == 'pv'").ShouldBeTrue();
        Eval(context, "$response.header.sh == 'shv'").ShouldBeTrue();
        Eval(context, "$message.header.mh == 'mhv'").ShouldBeTrue();

        // Unset scalar -> undefined operand -> false.
        Eval(context, "$request.header.absent == 'x'").ShouldBeFalse();
    }

    [TestMethod]
    public void Interpolate_unterminated_embedded_is_literal()
    {
        var context = new WorkflowExecutionContext();
        context.TryInterpolate("a {$inputs.x", out string s).ShouldBeTrue();
        s.ShouldBe("a {$inputs.x");
    }

    private static string Resolve(WorkflowExecutionContext context, string expression)
    {
        context.TryResolveString(ArazzoExpression.Parse(expression), out string value).ShouldBeTrue();
        return value;
    }

    private static bool Eval(WorkflowExecutionContext context, string condition)
        => CompiledCriterion.Compile(CriterionType.Simple, condition).Evaluate(context);

    private static ParsedJsonDocument<JsonElement> Parse(string json)
        => ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
}