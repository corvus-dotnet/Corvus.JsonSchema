// <copyright file="ForwardPlanTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// A node whose only assertion is a single in-place child (a lone <c>allOf</c> branch, or a <c>$ref</c> the pure-ref
/// elision keeps for its resource boundary) forwards flag-mode evaluation straight to that child's plan. Collecting
/// mode keeps the general path so the reported keywords and locations are unchanged.
/// </summary>
[TestClass]
public class ForwardPlanTests
{
    [TestMethod]
    public void SingleBranchAllOfForwardsToTheBranch()
    {
        const string schema = """
            {
              "allOf": [{"$ref": "#/definitions/ignore"}],
              "definitions": {
                "ignore": {"properties": {"ignore": {"type": "string"}}}
              }
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        SchemaNode root = evaluator.Program.Nodes[evaluator.RootNode];
        Assert.AreEqual(NodePlan.Forward, root.Plan);
        Assert.AreEqual(NodePlan.Object, evaluator.Program.Nodes[root.ForwardNode].Plan, "The forward target is the referenced object node.");

        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        Assert.AreEqual(NodePlan.Forward, loaded.Program.Nodes[loaded.RootNode].Plan, "The plan is rebuilt on load.");

        foreach ((string instance, bool expected) in new[] { ("""{"ignore": "x"}""", true), ("""{"ignore": 1}""", false), ("""{"rules": {}}""", true), ("\"s\"", true), ("[1]", true) })
        {
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
            Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement), instance);
            Assert.AreEqual(expected, loaded.Evaluate(doc.RootElement), instance);
            using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
            Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement, collector), "collecting " + instance);
        }

        // Collecting mode still reports the allOf keyword.
        using ParsedJsonDocument<JsonElement> bad = ParsedJsonDocument<JsonElement>.Parse("""{"ignore": 1}""");
        using JsonSchemaResultsCollector verbose = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        Assert.IsFalse(evaluator.Evaluate(bad.RootElement, verbose));
        bool sawAllOf = false;
        foreach (JsonSchemaResultsCollector.Result r in verbose.EnumerateResults())
        {
            sawAllOf |= r.GetEvaluationLocationText().Contains("allOf");
        }

        Assert.IsTrue(sawAllOf, "The allOf keyword is reported when collecting.");
    }

    [TestMethod]
    public void NodesWithOtherAssertionsDoNotForward()
    {
        using JsonSchemaEvaluator typed = JsonSchemaEvaluator.Compile("""{"type": "object", "allOf": [{"properties": {"a": {"type": "string"}}}]}""");
        Assert.AreNotEqual(NodePlan.Forward, typed.Program.Nodes[typed.RootNode].Plan);
        Assert.IsFalse(typed.Evaluate("\"s\""));

        using JsonSchemaEvaluator two = JsonSchemaEvaluator.Compile("""{"allOf": [{"properties": {"a": {"type": "string"}}}, {"required": ["a"]}]}""");
        Assert.AreNotEqual(NodePlan.Forward, two.Program.Nodes[two.RootNode].Plan);
        Assert.IsFalse(two.Evaluate("{}"));
        Assert.IsTrue(two.Evaluate("""{"a": "x"}"""));

        // A self-referential branch stays guarded on the general path.
        using JsonSchemaEvaluator cyclic = JsonSchemaEvaluator.Compile("""{"allOf": [{"$ref": "#"}]}""");
        Assert.AreNotEqual(NodePlan.Forward, cyclic.Program.Nodes[cyclic.RootNode].Plan);
        Assert.ThrowsExactly<JsonSchemaEvaluationException>(() => cyclic.Evaluate("1"));
    }

    [TestMethod]
    public void ForwardingKeepsTheDynamicScope()
    {
        // The property schema forwards through its lone allOf branch to a $ref that crosses into the "outer"
        // resource; the dynamic anchor there must still win, exactly as on the general path.
        const string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/root",
              "properties": {"x": {"allOf": [{"$ref": "outer"}]}},
              "$defs": {
                "outer": {"$id": "outer", "$dynamicAnchor": "leaf", "$ref": "inner"},
                "inner": {"$id": "inner", "$dynamicAnchor": "leaf", "type": "array", "items": {"$dynamicRef": "#leaf"}},
                "outerLeaf": {"$id": "outer-leaf", "$dynamicAnchor": "leaf", "type": "integer"}
              }
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        Assert.IsTrue(evaluator.UsesDynamicScope);
        foreach ((string instance, bool expected) in new[] { ("""{"x": [[], [[]]]}""", true), ("""{"x": [1]}""", false), ("""{"x": []}""", true), ("""{"x": 1}""", false) })
        {
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
            Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement), "flag " + instance);
            using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
            Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement, collector), "collecting " + instance);
        }
    }
}
