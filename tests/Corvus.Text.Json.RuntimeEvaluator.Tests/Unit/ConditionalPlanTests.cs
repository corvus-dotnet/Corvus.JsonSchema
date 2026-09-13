// <copyright file="ConditionalPlanTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// A node whose keywords are type and object keywords plus if/then/else, and which does not fuse (the branches here
/// carry a <c>not</c>, which the fused plan refuses), takes the conditional plan: its own keywords through their
/// plan, then the if and the selected branch as children, agreeing with the general path on every value kind, on the
/// absent property, on non-objects, on escaped names and values, on duplicate names and on non-canonical numbers.
/// </summary>
[TestClass]
public class ConditionalPlanTests
{
    private const string TypeChain = """
        {
          "if": {"properties": {"type": {"const": "application"}}},
          "then": {"required": ["app"], "not": {"required": ["lib"]}},
          "else": {
            "if": {"properties": {"type": {"enum": ["library", "theme-library"]}}},
            "then": {"required": ["lib"], "not": {"required": ["app"]}},
            "else": {
              "if": {"properties": {"type": {"const": "module"}}},
              "then": {"required": ["mod"], "not": {"required": ["app"]}}
            }
          }
        }
        """;

    private const string NumberChain = """
        {
          "if": {"properties": {"v": {"enum": [1, 2, true, null]}}},
          "then": {"required": ["a"], "not": {"required": ["b"]}},
          "else": {"required": ["b"], "not": {"required": ["a"]}}
        }
        """;

    private const string ObjectWithCondition = """
        {
          "type": "object",
          "required": ["v"],
          "properties": {"v": {"type": "string"}, "n": {"type": "integer"}},
          "if": {"properties": {"v": {"const": "a"}}},
          "then": {"not": {"required": ["b"]}},
          "else": {"required": ["b"]}
        }
        """;

    private static void AssertAgree(JsonSchemaEvaluator evaluator, JsonSchemaEvaluator loaded, string instance, bool expected)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement, collector), "general " + instance);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement), "conditional " + instance);
        Assert.AreEqual(expected, loaded.Evaluate(doc.RootElement), "image " + instance);
    }

    [TestMethod]
    public void APureChainTakesTheConditionalPlanAtEveryLevel()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(TypeChain);
        SchemaNode root = evaluator.Program.Nodes[evaluator.RootNode];
        Assert.AreEqual(NodePlan.Conditional, root.Plan);
        Assert.AreEqual(NodePlan.AlwaysTrue, root.ConditionalOwnPlan, "No keywords of its own.");
        Assert.AreEqual(NodePlan.Conditional, evaluator.Program.Nodes[root.Else.FastNode].Plan);
        Assert.AreEqual(NodePlan.StrictObject, evaluator.Program.Nodes[root.If.FastNode].Plan, "The if is a one-property strict object.");
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        Assert.AreEqual(NodePlan.Conditional, loaded.Program.Nodes[loaded.RootNode].Plan, "The plan is rebuilt on load.");

        foreach ((string instance, bool expected) in new[]
        {
            ("""{"type": "application", "app": 1}""", true),
            ("""{"type": "application"}""", false),
            ("""{"type": "library", "lib": 1}""", true),
            ("""{"type": "theme-library", "lib": 1}""", true),
            ("""{"type": "theme-library", "app": 1}""", false),
            ("""{"type": "module", "mod": 1}""", true),
            ("""{"type": "module"}""", false),
            ("""{"type": "other"}""", true),
            ("""{"type": 1}""", true),
            ("""{"type": null}""", true),
            ("""{"type": {"x": "application"}}""", true),
            ("""{"app": 1}""", true),
            ("""{}""", false),
            ("""{"type": "module", "mod": 1}""", true),
            ("""{"type": "application", "app": 1}""", true),
            ("""{"type": "application", "type": "library", "lib": 1}""", true),
            ("""{"type": "application", "type": "library", "app": 1}""", true),
            ("\"application\"", false),
            ("[]", false),
            ("null", false),
        })
        {
            AssertAgree(evaluator, loaded, instance, expected);
        }
    }

    [TestMethod]
    public void IntegerBooleanAndNullConditionsAgree()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(NumberChain);
        Assert.AreEqual(NodePlan.Conditional, evaluator.Program.Nodes[evaluator.RootNode].Plan);
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());

        foreach ((string instance, bool expected) in new[]
        {
            ("""{"v": 1, "a": 1}""", true),
            ("""{"v": 1, "b": 1}""", false),
            ("""{"v": 2.0, "a": 1}""", true),
            ("""{"v": 1e0, "b": 1}""", false),
            ("""{"v": 1.5, "b": 1}""", true),
            ("""{"v": 3, "b": 1}""", true),
            ("""{"v": "1", "b": 1}""", true),
            ("""{"v": true, "a": 1}""", true),
            ("""{"v": false, "b": 1}""", true),
            ("""{"v": null, "a": 1}""", true),
            ("""{"v": null, "b": 1}""", false),
            ("""{"b": 1}""", false),
            ("""{"a": 1}""", true),
        })
        {
            AssertAgree(evaluator, loaded, instance, expected);
        }
    }

    [TestMethod]
    public void OwnObjectKeywordsGoThroughTheStrictPlanFirst()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(ObjectWithCondition);
        SchemaNode root = evaluator.Program.Nodes[evaluator.RootNode];
        Assert.AreEqual(NodePlan.Conditional, root.Plan);
        Assert.AreEqual(NodePlan.StrictObject, root.ConditionalOwnPlan);
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        Assert.AreEqual(NodePlan.StrictObject, loaded.Program.Nodes[loaded.RootNode].ConditionalOwnPlan);

        foreach ((string instance, bool expected) in new[]
        {
            ("""{"v": "a"}""", true),
            ("""{"v": "a", "n": 2}""", true),
            ("""{"v": "a", "n": "x"}""", false),
            ("""{"v": "a", "b": 1}""", false),
            ("""{"v": "c", "b": 1}""", true),
            ("""{"v": "c"}""", false),
            ("""{"v": 1, "b": 1}""", false),
            ("""{}""", false),
            ("""{"b": 1}""", false),
            ("\"a\"", false),
            ("[]", false),
        })
        {
            AssertAgree(evaluator, loaded, instance, expected);
        }

        // Type alone as the own keywords.
        using JsonSchemaEvaluator typed = JsonSchemaEvaluator.Compile("""{"type": ["object", "null"], "if": {"properties": {"v": {"const": "a"}}}, "then": {"not": {"required": ["b"]}}}""");
        Assert.AreEqual(NodePlan.Conditional, typed.Program.Nodes[typed.RootNode].Plan);
        Assert.AreEqual(NodePlan.Leaf, typed.Program.Nodes[typed.RootNode].ConditionalOwnPlan);
        Assert.IsTrue(typed.Evaluate("""{"v": "a"}"""));
        Assert.IsFalse(typed.Evaluate("""{"v": "a", "b": 1}"""));
        Assert.IsTrue(typed.Evaluate("""{"v": "c", "b": 1}"""));
        Assert.IsFalse(typed.Evaluate("null"), "The then applies to null too, and its not fails on a non-object.");
        Assert.IsFalse(typed.Evaluate("1"));
    }
}
