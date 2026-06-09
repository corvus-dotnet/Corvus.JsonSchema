// <copyright file="Coverage_Runtime2Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Branch-coverage tests for the interpreted criterion evaluator — the dynamic (embedded-expression)
/// regex/JSONPath paths, the explicit-timeout and long-context regex paths, the full simple-condition
/// operator/boolean matrix, and the <see cref="CriteriaSet"/> aggregation. These exercise the runtime
/// interpreter that a non-inlined <see cref="CompiledCriterion"/> uses.
/// </summary>
[TestClass]
public class Coverage_Runtime2Tests
{
    private static ParsedJsonDocument<JsonElement> Parse(string json)
        => ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));

    [TestMethod]
    public void Compile_unknown_type_throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => CompiledCriterion.Compile((CriterionType)99, "x"));
    }

    [TestMethod]
    public void Regex_with_explicit_timeout_evaluates()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseHeader("X-Tag", "alpha");

        CompiledCriterion criterion = CompiledCriterion.Compile(
            CriterionType.Regex, "^al", "$response.header.X-Tag", TimeSpan.FromSeconds(2));
        criterion.Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Regex_against_long_context_uses_pooled_buffer()
    {
        // > 256 chars forces the pooled (rented) transcode buffer in the regex match path.
        using ParsedJsonDocument<JsonElement> doc = Parse($$"""{ "s": "{{new string('a', 300)}}needle" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Regex, "needle", "$inputs.s").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Dynamic_regex_substitutes_embedded_expression()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "prefix": "id", "value": "id-42" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        // The pattern embeds {$inputs.prefix}, interpolated to "^id-" before matching $inputs.value.
        CompiledCriterion.Compile(CriterionType.Regex, "^{$inputs.prefix}-", "$inputs.value")
            .Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Dynamic_regex_is_translated_from_ecma_262()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "prefix": "x", "value": "x5", "unicode": "x٥" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        // The interpolated pattern "x\d" is still ECMAScript: \d is ASCII, so an ASCII digit matches...
        CompiledCriterion.Compile(CriterionType.Regex, @"{$inputs.prefix}\d", "$inputs.value")
            .Evaluate(context).ShouldBeTrue();

        // ...but an Arabic-Indic digit does not (it would under a raw .NET \d), proving the dynamic
        // pattern is translated after interpolation.
        CompiledCriterion.Compile(CriterionType.Regex, @"{$inputs.prefix}\d", "$inputs.unicode")
            .Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    public void Dynamic_regex_with_unresolved_embedded_expression_is_false()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "value": "x" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        // The embedded expression cannot be resolved, so the pattern cannot be built → false.
        CompiledCriterion.Compile(CriterionType.Regex, "^{$inputs.missing}$", "$inputs.value")
            .Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    public void Dynamic_regex_against_status_code_and_method()
    {
        var context = new WorkflowExecutionContext();
        context.SetRequest("https://h", "GET");
        context.SetResponseStatusCode(200);

        // Embeds $method into the pattern and matches the literal context.
        CompiledCriterion.Compile(CriterionType.Regex, "^{$method}$", "GET")
            .Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Dynamic_jsonpath_substitutes_embedded_expression()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "items": [1, 2] }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        // Builds the query "$.items" from the embedded {$inputs.key}.
        using ParsedJsonDocument<JsonElement> inputs = Parse("""{ "key": "items" }""");
        context.SetInputs(inputs.RootElement);

        CompiledCriterion.Compile(CriterionType.JsonPath, "$.{$inputs.key}[*]", "$response.body")
            .Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Dynamic_jsonpath_with_unresolved_embedded_expression_is_false()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "items": [1] }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.JsonPath, "$.{$inputs.missing}[*]", "$response.body")
            .Evaluate(context).ShouldBeFalse();
    }

    [TestMethod]
    public void Jsonpath_unresolved_context_is_false()
    {
        var context = new WorkflowExecutionContext();
        CompiledCriterion.Compile(CriterionType.JsonPath, "$.items[*]", "$response.body")
            .Evaluate(context).ShouldBeFalse();
    }

    // ---- SimpleConditionEvaluator: operator + boolean matrix ----

    [TestMethod]
    public void Simple_all_numeric_operators()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "x": 5 }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x <= 5").Evaluate(context).ShouldBeTrue();
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x <= 4").Evaluate(context).ShouldBeFalse();
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x > 4").Evaluate(context).ShouldBeTrue();
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x > 5").Evaluate(context).ShouldBeFalse();
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x < 6").Evaluate(context).ShouldBeTrue();
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x >= 5").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Simple_not_operator_and_not_equal()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "x": 5 }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        CompiledCriterion.Compile(CriterionType.Simple, "!($inputs.x == 99)").Evaluate(context).ShouldBeTrue();
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x != 99").Evaluate(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Simple_and_or_short_circuit_both_sides()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "t": true, "f": false }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        // OR: left-true short-circuits; left-false evaluates the right.
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.t || $inputs.f").Evaluate(context).ShouldBeTrue();
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.f || $inputs.t").Evaluate(context).ShouldBeTrue();
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.f || $inputs.f").Evaluate(context).ShouldBeFalse();

        // AND: left-false short-circuits; left-true evaluates the right.
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.f && $inputs.t").Evaluate(context).ShouldBeFalse();
        CompiledCriterion.Compile(CriterionType.Simple, "$inputs.t && $inputs.t").Evaluate(context).ShouldBeTrue();
    }

    // ---- CriteriaSet ----

    [TestMethod]
    public void CriteriaSet_default_and_empty_always_match()
    {
        var context = new WorkflowExecutionContext();

        CriteriaSet defaultSet = default;
        defaultSet.Count.ShouldBe(0);
        defaultSet.AllMatch(context).ShouldBeTrue();

        CriteriaSet.Empty.Count.ShouldBe(0);
        CriteriaSet.Empty.AllMatch(context).ShouldBeTrue();
    }

    [TestMethod]
    public void CriteriaSet_all_match_semantics()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "x": 5 }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        var set = new CriteriaSet(
        [
            CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x == 5"),
            CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x < 10"),
        ]);
        set.Count.ShouldBe(2);
        set.AllMatch(context).ShouldBeTrue();

        var failing = new CriteriaSet(
        [
            CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x == 5"),
            CompiledCriterion.Compile(CriterionType.Simple, "$inputs.x == 6"),
        ]);
        failing.AllMatch(context).ShouldBeFalse();
    }
}
