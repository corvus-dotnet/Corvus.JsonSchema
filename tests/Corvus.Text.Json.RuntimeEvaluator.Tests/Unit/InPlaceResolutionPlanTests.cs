// <copyright file="InPlaceResolutionPlanTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// The composite plan (a node's own keywords through their plan, then its applicators), string consts tested in
/// place, single-pattern maps and prefix items on the array-items plan: each is chosen where it applies, rebuilt on
/// image load, and agrees with the general (collecting) path on matching, failing, escaped and non-container values.
/// </summary>
[TestClass]
public class InPlaceResolutionPlanTests
{
    private static void AssertAgree(JsonSchemaEvaluator evaluator, JsonSchemaEvaluator loaded, string instance, bool expected)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement, collector), "general " + instance);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement), "flag " + instance);
        Assert.AreEqual(expected, loaded.Evaluate(doc.RootElement), "image " + instance);
    }

    private static JsonSchemaEvaluator Load(JsonSchemaEvaluator evaluator) => JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());

    [TestMethod]
    public void AnObjectWithAnApplicatorTakesTheCompositePlan()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"type": "object", "properties": {"a": {"type": "integer"}}, "required": ["a"], "not": {"required": ["b"]}}""");
        SchemaNode root = evaluator.Program.Nodes[evaluator.RootNode];
        Assert.AreEqual(NodePlan.Composite, root.Plan);
        Assert.AreEqual(NodePlan.StrictObject, root.ConditionalOwnPlan);
        using JsonSchemaEvaluator loaded = Load(evaluator);
        Assert.AreEqual(NodePlan.Composite, loaded.Program.Nodes[loaded.RootNode].Plan, "The plan is rebuilt on load.");

        AssertAgree(evaluator, loaded, """{"a": 1}""", true);
        AssertAgree(evaluator, loaded, """{"a": 1, "c": "x"}""", true);
        AssertAgree(evaluator, loaded, """{"a": 1, "b": 2}""", false);
        AssertAgree(evaluator, loaded, """{"a": 1, "\u0062": 2}""", false);
        AssertAgree(evaluator, loaded, """{"a": "1"}""", false);
        AssertAgree(evaluator, loaded, """{}""", false);
        AssertAgree(evaluator, loaded, "[]", false);
    }

    [TestMethod]
    public void ALeafAndAnArrayWithApplicatorsTakeTheCompositePlan()
    {
        using JsonSchemaEvaluator leaf = JsonSchemaEvaluator.Compile("""{"type": "string", "minLength": 2, "anyOf": [{"pattern": "^a"}, {"pattern": "^b"}]}""");
        Assert.AreEqual(NodePlan.Composite, leaf.Program.Nodes[leaf.RootNode].Plan);
        Assert.AreEqual(NodePlan.Leaf, leaf.Program.Nodes[leaf.RootNode].ConditionalOwnPlan);
        using JsonSchemaEvaluator leafLoaded = Load(leaf);
        AssertAgree(leaf, leafLoaded, "\"ax\"", true);
        AssertAgree(leaf, leafLoaded, "\"bx\"", true);
        AssertAgree(leaf, leafLoaded, "\"cx\"", false);
        AssertAgree(leaf, leafLoaded, "\"a\"", false);
        AssertAgree(leaf, leafLoaded, "1", false);

        using JsonSchemaEvaluator array = JsonSchemaEvaluator.Compile("""{"type": "array", "items": {"type": "integer"}, "allOf": [{"not": {"maxItems": 0}}]}""");
        Assert.AreEqual(NodePlan.Composite, array.Program.Nodes[array.RootNode].Plan);
        Assert.AreEqual(NodePlan.ArrayItems, array.Program.Nodes[array.RootNode].ConditionalOwnPlan);
        using JsonSchemaEvaluator arrayLoaded = Load(array);
        AssertAgree(array, arrayLoaded, "[1, 2]", true);
        AssertAgree(array, arrayLoaded, "[]", false);
        AssertAgree(array, arrayLoaded, "[1, \"2\"]", false);
        AssertAgree(array, arrayLoaded, "{}", false);
    }

    [TestMethod]
    public void UnevaluatedKeywordsKeepTheGeneralPath()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"$schema": "https://json-schema.org/draft/2020-12/schema", "properties": {"a": {"type": "integer"}}, "not": {"required": ["b"]}, "unevaluatedProperties": false}""");
        Assert.AreNotEqual(NodePlan.Composite, evaluator.Program.Nodes[evaluator.RootNode].Plan);
        using JsonSchemaEvaluator loaded = Load(evaluator);
        AssertAgree(evaluator, loaded, """{"a": 1}""", true);
        AssertAgree(evaluator, loaded, """{"a": 1, "c": 1}""", false);
    }

    [TestMethod]
    public void AStringConstChildIsComparedInPlace()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"type": "object", "properties": {"kind": {"const": "a\"b"}, "n": {"type": "integer"}}, "required": ["kind"]}""");
        SchemaNode root = evaluator.Program.Nodes[evaluator.RootNode];
        Assert.AreEqual(NodePlan.StrictObject, root.Plan);
        Assert.AreEqual(1, root.StrictEntries!.Count(e => e.ConstBytes is not null), "The const child is tested in place.");
        using JsonSchemaEvaluator loaded = Load(evaluator);
        Assert.AreEqual(1, loaded.Program.Nodes[loaded.RootNode].StrictEntries!.Count(e => e.ConstBytes is not null), "Rebuilt on load.");

        AssertAgree(evaluator, loaded, """{"kind": "a\"b"}""", true);
        AssertAgree(evaluator, loaded, """{"kind": "a\u0022b", "n": 1}""", true);
        AssertAgree(evaluator, loaded, """{"kind": "a\"c"}""", false);
        AssertAgree(evaluator, loaded, """{"kind": "a\"bb"}""", false);
        AssertAgree(evaluator, loaded, """{"kind": 1}""", false);
        AssertAgree(evaluator, loaded, """{"n": 1}""", false);
    }

    [TestMethod]
    public void AStringConstBranchIsComparedInPlaceInAFusedObject()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """
            {
              "properties": {"type": {"type": "string"}},
              "if": {"properties": {"type": {"const": "a"}}, "required": ["type"]},
              "then": {"properties": {"v": {"const": "x"}}, "required": ["v"]},
              "else": {"properties": {"v": {"type": "integer"}}}
            }
            """);
        using JsonSchemaEvaluator loaded = Load(evaluator);
        AssertAgree(evaluator, loaded, """{"type": "a", "v": "x"}""", true);
        AssertAgree(evaluator, loaded, """{"type": "a", "v": "\u0078"}""", true);
        AssertAgree(evaluator, loaded, """{"type": "a", "v": "y"}""", false);
        AssertAgree(evaluator, loaded, """{"type": "a"}""", false);
        AssertAgree(evaluator, loaded, """{"type": "b", "v": 1}""", true);
        AssertAgree(evaluator, loaded, """{"type": "b", "v": "x"}""", false);
    }

    [TestMethod]
    public void ASinglePatternMapRunsItsOwnLoop()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"type": "object", "patternProperties": {"^x-": {"type": "string"}}, "additionalProperties": false, "maxProperties": 2}""");
        SchemaNode root = evaluator.Program.Nodes[evaluator.RootNode];
        Assert.AreEqual(NodePlan.Object, root.Plan);
        Assert.IsNotNull(root.PatternMap);
        using JsonSchemaEvaluator loaded = Load(evaluator);
        Assert.IsNotNull(loaded.Program.Nodes[loaded.RootNode].PatternMap, "Rebuilt on load.");

        AssertAgree(evaluator, loaded, """{}""", true);
        AssertAgree(evaluator, loaded, """{"x-a": "s", "x-b": "t"}""", true);
        AssertAgree(evaluator, loaded, """{"x\u002Da": "s"}""", true);
        AssertAgree(evaluator, loaded, """{"x-a": 1}""", false);
        AssertAgree(evaluator, loaded, """{"y": "s"}""", false);
        AssertAgree(evaluator, loaded, """{"x-a": "s", "x-b": "t", "x-c": "u"}""", false);
        AssertAgree(evaluator, loaded, "\"s\"", false);
    }

    [TestMethod]
    public void APatternThatMatchesEverythingSkipsTheNameAndOtherNamesTakeTheAdditionalSchema()
    {
        using JsonSchemaEvaluator all = JsonSchemaEvaluator.Compile("""{"patternProperties": {".*": {"const": "v"}}, "additionalProperties": false}""");
        Assert.IsNotNull(all.Program.Nodes[all.RootNode].PatternMap);
        using JsonSchemaEvaluator allLoaded = Load(all);
        AssertAgree(all, allLoaded, """{"a": "v", "": "v"}""", true);
        AssertAgree(all, allLoaded, """{"a": "w"}""", false);
        AssertAgree(all, allLoaded, "1", true);

        using JsonSchemaEvaluator typed = JsonSchemaEvaluator.Compile("""{"patternProperties": {"^n": {"type": "integer"}}, "additionalProperties": {"type": "string"}}""");
        Assert.IsNotNull(typed.Program.Nodes[typed.RootNode].PatternMap);
        using JsonSchemaEvaluator typedLoaded = Load(typed);
        AssertAgree(typed, typedLoaded, """{"n1": 1, "s": "a"}""", true);
        AssertAgree(typed, typedLoaded, """{"n1": "1"}""", false);
        AssertAgree(typed, typedLoaded, """{"s": 1}""", false);

        using JsonSchemaEvaluator required = JsonSchemaEvaluator.Compile("""{"patternProperties": {"^x-": {"type": "string"}}, "required": ["x-a"]}""");
        using JsonSchemaEvaluator requiredLoaded = Load(required);
        AssertAgree(required, requiredLoaded, """{"x-a": "s"}""", true);
        AssertAgree(required, requiredLoaded, """{"x-b": "s"}""", false);
        AssertAgree(required, requiredLoaded, """{"x-a": 1}""", false);
    }

    [TestMethod]
    public void PrefixItemsTakeTheArrayItemsPlan()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"$schema": "https://json-schema.org/draft/2020-12/schema", "prefixItems": [{"type": "integer"}, {"const": "b"}, {"$ref": "#/$defs/pair"}], "items": {"type": "boolean"}, "minItems": 1, "$defs": {"pair": {"type": "array", "maxItems": 2}}}""");
        SchemaNode root = evaluator.Program.Nodes[evaluator.RootNode];
        Assert.AreEqual(NodePlan.ArrayItems, root.Plan);
        Assert.AreEqual(4, root.PrefixEntries!.Length, "Three positions and the rest.");
        using JsonSchemaEvaluator loaded = Load(evaluator);
        Assert.AreEqual(4, loaded.Program.Nodes[loaded.RootNode].PrefixEntries!.Length, "Rebuilt on load.");

        AssertAgree(evaluator, loaded, "[1]", true);
        AssertAgree(evaluator, loaded, "[1, \"b\"]", true);
        AssertAgree(evaluator, loaded, "[1, \"b\", [1, 2], true, false]", true);
        AssertAgree(evaluator, loaded, "[1, \"\\u0062\"]", true);
        AssertAgree(evaluator, loaded, "[]", false);
        AssertAgree(evaluator, loaded, "[\"1\"]", false);
        AssertAgree(evaluator, loaded, "[1, \"c\"]", false);
        AssertAgree(evaluator, loaded, "[1, \"b\", [1, 2, 3]]", false);
        AssertAgree(evaluator, loaded, "[1, \"b\", [], 1]", false);
        AssertAgree(evaluator, loaded, "{}", true);
    }

    [TestMethod]
    public void ArrayFormItemsTakeTheArrayItemsPlan()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"$schema": "http://json-schema.org/draft-07/schema#", "type": "array", "items": [{"type": "string"}, {"enum": ["a", "b"]}], "additionalItems": false}""");
        Assert.AreEqual(NodePlan.ArrayItems, evaluator.Program.Nodes[evaluator.RootNode].Plan);
        using JsonSchemaEvaluator loaded = Load(evaluator);
        AssertAgree(evaluator, loaded, "[]", true);
        AssertAgree(evaluator, loaded, "[\"x\", \"a\"]", true);
        AssertAgree(evaluator, loaded, "[\"x\", \"c\"]", false);
        AssertAgree(evaluator, loaded, "[\"x\", \"a\", 1]", false);
        AssertAgree(evaluator, loaded, "[1]", false);
        AssertAgree(evaluator, loaded, "{}", false);

        using JsonSchemaEvaluator unique = JsonSchemaEvaluator.Compile(
            """{"$schema": "https://json-schema.org/draft/2020-12/schema", "prefixItems": [{"type": "integer"}], "uniqueItems": true}""");
        Assert.IsNull(unique.Program.Nodes[unique.RootNode].PrefixEntries, "uniqueItems keeps the general path.");
        using JsonSchemaEvaluator uniqueLoaded = Load(unique);
        AssertAgree(unique, uniqueLoaded, "[1, 2]", true);
        AssertAgree(unique, uniqueLoaded, "[1, 1]", false);
    }
}
