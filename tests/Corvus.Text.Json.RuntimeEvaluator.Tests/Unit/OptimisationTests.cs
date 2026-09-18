using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// Pins the behaviour of the flag-mode fast paths (discriminators, unrolled objects, elided refs)
/// by checking that flag mode and collecting mode always agree with the expected result.
/// </summary>
[TestClass]
public class OptimisationTests
{
    private static void AssertBoth(JsonSchemaEvaluator evaluator, string instance, bool expected)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement), "flag: " + instance);
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement, collector), "collecting: " + instance);
    }

    [TestMethod]
    public void DiscriminatorWithPositiveNegativeAndWildcardBranches()
    {
        const string schema = """
            {
              "oneOf": [
                {"$ref": "#/$defs/a"},
                {"$ref": "#/$defs/b"},
                {"$ref": "#/$defs/fn"},
                {"type": "boolean"}
              ],
              "$defs": {
                "a": {"type": "object", "required": ["op"], "properties": {"op": {"const": "a"}, "x": {"type": "integer"}}},
                "b": {"type": "object", "required": ["op"], "properties": {"op": {"enum": ["b", "bb"]}, "x": {"type": "string"}}},
                "fn": {"type": "object", "required": ["op", "args"], "properties": {"op": {"type": "string", "not": {"enum": ["a", "b", "bb"]}}, "args": {"type": "array"}}}
              }
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        AssertBoth(evaluator, """{"op": "a", "x": 1}""", true);
        AssertBoth(evaluator, """{"op": "a", "x": "s"}""", false);
        AssertBoth(evaluator, """{"op": "bb", "x": "s"}""", true);
        AssertBoth(evaluator, """{"op": "bb", "x": 1}""", false);
        AssertBoth(evaluator, """{"op": "custom", "args": []}""", true);
        AssertBoth(evaluator, """{"op": "custom"}""", false);
        AssertBoth(evaluator, """{"op": "a", "args": []}""", true);
        AssertBoth(evaluator, """{"op": 1, "args": []}""", false);
        AssertBoth(evaluator, """{"x": 1}""", false);
        AssertBoth(evaluator, "true", true);
        AssertBoth(evaluator, "\"a\"", false);
    }

    [TestMethod]
    public void DiscriminatorAnyOfWithOverlappingValues()
    {
        const string schema = """
            {
              "anyOf": [
                {"required": ["kind"], "properties": {"kind": {"enum": ["x", "y"]}, "v": {"type": "integer"}}},
                {"required": ["kind"], "properties": {"kind": {"enum": ["y", "z"]}, "v": {"type": "string"}}}
              ]
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        AssertBoth(evaluator, """{"kind": "y", "v": 1}""", true);
        AssertBoth(evaluator, """{"kind": "y", "v": "s"}""", true);
        AssertBoth(evaluator, """{"kind": "x", "v": "s"}""", false);
        AssertBoth(evaluator, """{"kind": "z", "v": 1}""", false);
        AssertBoth(evaluator, """{"kind": "q", "v": 1}""", false);
        AssertBoth(evaluator, """{"v": 1}""", false);
    }

    [TestMethod]
    public void DiscriminatorRespectsUnevaluatedTracking()
    {
        const string schema = """
            {
              "oneOf": [
                {"properties": {"kind": {"const": "a"}, "x": true}, "required": ["kind"]},
                {"properties": {"kind": {"const": "b"}, "y": true}, "required": ["kind"]}
              ],
              "unevaluatedProperties": false
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        AssertBoth(evaluator, """{"kind": "a", "x": 1}""", true);
        AssertBoth(evaluator, """{"kind": "a", "y": 1}""", false);
        AssertBoth(evaluator, """{"kind": "b", "y": 1}""", true);
    }

    [TestMethod]
    public void UnrolledObjectsHandleEscapedNamesAndCounts()
    {
        const string schema = """
            {
              "type": "object",
              "required": ["a\"b", "c/d"],
              "properties": {"a\"b": {"type": "integer"}, "c/d": {"type": "string"}, "e": {"type": "null"}},
              "minProperties": 2,
              "maxProperties": 3
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        AssertBoth(evaluator, """{"a\"b": 1, "c/d": "s"}""", true);
        AssertBoth(evaluator, """{"a\"b": 1, "c\/d": "s", "e": null}""", true);
        AssertBoth(evaluator, """{"a\"b": 1}""", false);
        AssertBoth(evaluator, """{"a\"b": "x", "c/d": "s"}""", false);
        AssertBoth(evaluator, """{"a\"b": 1, "c/d": "s", "e": 1}""", false);
        AssertBoth(evaluator, """{"a\"b": 1, "c/d": "s", "e": null, "f": 1}""", false);
    }

    [TestMethod]
    public void PureRefElisionKeepsDynamicScopeAcrossResources()
    {
        // The pure-$ref resource "outer" carries the $dynamicAnchor that must win once entered.
        const string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/root",
              "$defs": {
                "outer": {"$id": "outer", "$dynamicAnchor": "leaf", "$ref": "inner"},
                "inner": {"$id": "inner", "$dynamicAnchor": "leaf", "type": "array", "items": {"$dynamicRef": "#leaf"}},
                "outerLeaf": {"$id": "outer-leaf", "$dynamicAnchor": "leaf", "type": "integer"}
              },
              "$ref": "outer"
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        Assert.IsTrue(evaluator.UsesDynamicScope);

        // The outermost resource defining "leaf" in scope is "outer" whose anchor points to a pure $ref to inner,
        // so items must be arrays (recursively), never integers.
        AssertBoth(evaluator, "[[], [[]]]", true);
        AssertBoth(evaluator, "[1]", false);
    }

    [TestMethod]
    public void TypeOnlyLeavesInArraysAndProperties()
    {
        const string schema = """{"properties": {"a": {"type": ["integer", "null"]}}, "items": {"type": "number"}}""";
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        AssertBoth(evaluator, """{"a": 1}""", true);
        AssertBoth(evaluator, """{"a": null}""", true);
        AssertBoth(evaluator, """{"a": 1.5}""", false);
        AssertBoth(evaluator, "[1, 2.5, -3e2]", true);
        AssertBoth(evaluator, "[1, \"2\"]", false);
    }
}
