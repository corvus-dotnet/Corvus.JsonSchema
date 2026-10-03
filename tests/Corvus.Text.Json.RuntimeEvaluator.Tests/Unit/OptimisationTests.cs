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

    [TestMethod]
    public void IntegerMultipleOfOnTheIntegerFastPath()
    {
        // Integer instances against an integer divisor are decided exactly on longs, beyond double precision.
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"minimum": -100, "multipleOf": 3}""");
        AssertBoth(evaluator, "9007199254740993", true);
        AssertBoth(evaluator, "9007199254740994", false);
        AssertBoth(evaluator, "999999999999999999", true);
        AssertBoth(evaluator, "-99", true);
        AssertBoth(evaluator, "-98", false);
        AssertBoth(evaluator, "-102", false);
        AssertBoth(evaluator, "0", true);
        AssertBoth(evaluator, "1000000000000000002", true);
        AssertBoth(evaluator, "1000000000000000001", false);
        AssertBoth(evaluator, "4.5", false);
        AssertBoth(evaluator, "6.0", true);

        using JsonSchemaEvaluator odd = JsonSchemaEvaluator.Compile("""{"multipleOf": 2}""");
        AssertBoth(odd, "9007199254740993", false);
        AssertBoth(odd, "9007199254740992", true);

        // Divisors that are not plain integers stay on the decimal path.
        using JsonSchemaEvaluator half = JsonSchemaEvaluator.Compile("""{"maximum": 10, "multipleOf": 0.5}""");
        AssertBoth(half, "3", true);
        AssertBoth(half, "12", false);
        using JsonSchemaEvaluator exponent = JsonSchemaEvaluator.Compile("""{"multipleOf": 2e0}""");
        AssertBoth(exponent, "4", true);
        AssertBoth(exponent, "5", false);
        using JsonSchemaEvaluator big = JsonSchemaEvaluator.Compile("""{"multipleOf": 10000000000000000000}""");
        AssertBoth(big, "20000000000000000000", true);
        AssertBoth(big, "100", false);
    }

    [TestMethod]
    public void ContainsStopsOnceSettled()
    {
        using JsonSchemaEvaluator min = JsonSchemaEvaluator.Compile("""{"contains": {"type": "string"}, "minContains": 2}""");
        AssertBoth(min, """[1, "a", "b", 1, "c"]""", true);
        AssertBoth(min, """["a", 1]""", false);
        AssertBoth(min, "[]", false);

        using JsonSchemaEvaluator max = JsonSchemaEvaluator.Compile("""{"contains": {"type": "string"}, "maxContains": 1}""");
        AssertBoth(max, """["a", 1]""", true);
        AssertBoth(max, """["a", "b", 1]""", false);

        using JsonSchemaEvaluator zero = JsonSchemaEvaluator.Compile("""{"contains": {"type": "string"}, "minContains": 0}""");
        AssertBoth(zero, "[]", true);
        AssertBoth(zero, "[1, 2]", true);

        // Other array keywords still see every item after contains is settled.
        using JsonSchemaEvaluator items = JsonSchemaEvaluator.Compile("""{"items": {"type": ["string", "integer"]}, "contains": {"type": "string"}}""");
        AssertBoth(items, """["a", 1, 2]""", true);
        AssertBoth(items, """["a", 1, true]""", false);
        using JsonSchemaEvaluator prefix = JsonSchemaEvaluator.Compile("""{"prefixItems": [true, {"type": "integer"}], "contains": {"type": "string"}}""");
        AssertBoth(prefix, """["a", 1]""", true);
        AssertBoth(prefix, """["a", "b"]""", false);
        using JsonSchemaEvaluator unique = JsonSchemaEvaluator.Compile("""{"uniqueItems": true, "contains": {"type": "string"}}""");
        AssertBoth(unique, """["a", 1, 2]""", true);
        AssertBoth(unique, """["a", 1, 1]""", false);

        // Matches that mark items evaluated are all needed by unevaluatedItems.
        using JsonSchemaEvaluator marks = JsonSchemaEvaluator.Compile("""{"contains": {"type": "string"}, "unevaluatedItems": false}""");
        AssertBoth(marks, """["a", "b"]""", true);
        AssertBoth(marks, """["a", 1]""", false);
    }

    [TestMethod]
    public void StringLengthLeavesTestedInPlace()
    {
        // Leaves with only type and length bounds, under the strict loop, the map loop, the flat fused loop and prefix items.
        const string schema = """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "name": {"type": "string", "minLength": 2, "maxLength": 4},
                "any": {"maxLength": 2},
                "either": {"type": ["string", "integer"], "minLength": 3},
                "map": {"type": "object", "additionalProperties": {"type": "string", "maxLength": 3}},
                "tuple": {"type": "array", "prefixItems": [{"minLength": 1, "maxLength": 1}], "items": false},
                "fused": {"allOf": [{"properties": {"a": {"type": "string", "maxLength": 2}}}, {"properties": {"b": {"minLength": 2}}}]}
              }
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        AssertBoth(evaluator, """{"name": "abcd"}""", true);
        AssertBoth(evaluator, """{"name": "abcde"}""", false);
        AssertBoth(evaluator, """{"name": "a"}""", false);
        AssertBoth(evaluator, """{"name": 1}""", false);

        // Non-ASCII: 4 runes in 8 bytes passes maxLength 4; 5 runes fails. 2 runes in 8 bytes passes minLength 2.
        AssertBoth(evaluator, """{"name": "éééé"}""", true);
        AssertBoth(evaluator, """{"name": "éééé"}""", true);
        AssertBoth(evaluator, """{"name": "ééééé"}""", false);
        AssertBoth(evaluator, """{"name": "😀😀"}""", true);
        AssertBoth(evaluator, """{"name": "😀"}""", false);

        // Escaped values are measured unescaped.
        AssertBoth(evaluator, """{"name": "\u0041\u0042\u0043\u0044"}""", true);
        AssertBoth(evaluator, """{"name": "\u0041"}""", false);
        AssertBoth(evaluator, """{"name": "\u0041\u0042\u0043\u0044\u0045"}""", false);

        // No type: only strings are measured.
        AssertBoth(evaluator, """{"any": "ab"}""", true);
        AssertBoth(evaluator, """{"any": "abc"}""", false);
        AssertBoth(evaluator, """{"any": 12345}""", true);
        AssertBoth(evaluator, """{"any": [1, 2, 3]}""", true);

        // A type union: integers pass without a length, strings are measured, other types fail.
        AssertBoth(evaluator, """{"either": 1}""", true);
        AssertBoth(evaluator, """{"either": "abc"}""", true);
        AssertBoth(evaluator, """{"either": "ab"}""", false);
        AssertBoth(evaluator, """{"either": true}""", false);

        AssertBoth(evaluator, """{"map": {"x": "abc", "y": "z"}}""", true);
        AssertBoth(evaluator, """{"map": {"x": "abcd"}}""", false);
        AssertBoth(evaluator, """{"tuple": ["a"]}""", true);
        AssertBoth(evaluator, """{"tuple": ["ab"]}""", false);
        AssertBoth(evaluator, """{"fused": {"a": "ab", "b": "bc"}}""", true);
        AssertBoth(evaluator, """{"fused": {"a": "abc"}}""", false);
        AssertBoth(evaluator, """{"fused": {"b": "b"}}""", false);
    }

    [TestMethod]
    public void ChildrenDecidedByTypeAlone()
    {
        // Children whose keywords apply only to some kinds of value: the other kinds are decided by the type in place.
        const string schema = """
            {
              "type": "object",
              "properties": {
                "list": {"type": "array", "items": {"type": ["string", "array"], "items": {"type": "integer"}}},
                "either": {"type": ["string", "object"], "required": ["k"]},
                "count": {"type": "integer", "minimum": 1},
                "untyped": {"properties": {"a": {"type": "string"}}},
                "bounded": {"type": ["number", "boolean"], "maximum": 10}
              },
              "additionalProperties": {"type": ["null", "array"], "minItems": 1}
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        AssertBoth(evaluator, """{"list": ["a", [1, 2], "b"]}""", true);
        AssertBoth(evaluator, """{"list": ["a", [1, "x"]]}""", false);
        AssertBoth(evaluator, """{"list": ["a", 1]}""", false);
        AssertBoth(evaluator, """{"list": [null]}""", false);
        AssertBoth(evaluator, """{"either": "s"}""", true);
        AssertBoth(evaluator, """{"either": {"k": 1}}""", true);
        AssertBoth(evaluator, """{"either": {}}""", false);
        AssertBoth(evaluator, """{"either": 1}""", false);
        AssertBoth(evaluator, """{"count": 2}""", true);
        AssertBoth(evaluator, """{"count": 0}""", false);
        AssertBoth(evaluator, """{"count": 2.5}""", false);
        AssertBoth(evaluator, """{"count": "2"}""", false);
        AssertBoth(evaluator, """{"untyped": 1}""", true);
        AssertBoth(evaluator, """{"untyped": {"a": 1}}""", false);
        AssertBoth(evaluator, """{"bounded": true}""", true);
        AssertBoth(evaluator, """{"bounded": 11}""", false);
        AssertBoth(evaluator, """{"bounded": "x"}""", false);
        AssertBoth(evaluator, """{"extra": null}""", true);
        AssertBoth(evaluator, """{"extra": []}""", false);
        AssertBoth(evaluator, """{"extra": "x"}""", false);

        // Draft 4: an integer type with other keywords still tests numbers lexically.
        using JsonSchemaEvaluator draft4 = JsonSchemaEvaluator.Compile("""
            {
              "$schema": "http://json-schema.org/draft-04/schema#",
              "properties": {"n": {"type": ["integer", "string"], "maxLength": 1}},
              "items": {"type": ["integer", "string"], "maxLength": 1}
            }
            """);
        AssertBoth(draft4, """{"n": 1}""", true);
        AssertBoth(draft4, """{"n": 1.0}""", false);
        AssertBoth(draft4, """{"n": "ab"}""", false);
        AssertBoth(draft4, "[1, \"a\"]", true);
        AssertBoth(draft4, "[1.0]", false);
    }

    [TestMethod]
    public void BranchesNarrowedByTheKindsTheyAdmit()
    {
        // Branches reached through $ref, nested oneOf/anyOf and allOf admit only some kinds of value; only those that
        // admit the instance's kind are evaluated, and the result is the same.
        const string schema = """
            {
              "$defs": {
                "text": {"oneOf": [{"type": "string", "maxLength": 3}, {"$ref": "#/$defs/wrapped"}]},
                "wrapped": {"type": "object", "required": ["t"], "properties": {"t": {"$ref": "#/$defs/text"}}},
                "num": {"anyOf": [{"type": "integer"}, {"allOf": [{"type": ["number", "string"]}, {"type": "number", "maximum": 0}]}]},
                "flag": {"enum": ["on", "off"]}
              },
              "type": "array",
              "items": {"oneOf": [{"$ref": "#/$defs/text"}, {"$ref": "#/$defs/num"}, {"$ref": "#/$defs/flag"}, {"type": "boolean"}]}
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        AssertBoth(evaluator, """["ab", {"t": "x"}, {"t": {"t": "y"}}, 3, -1.5, true]""", true);
        AssertBoth(evaluator, """["abcd"]""", false);
        AssertBoth(evaluator, """[{"t": "abcd"}]""", false);
        AssertBoth(evaluator, """[{"u": 1}]""", false);
        AssertBoth(evaluator, """[1.5]""", false);
        AssertBoth(evaluator, """[null]""", false);
        AssertBoth(evaluator, """[[1]]""", false);

        // "on" matches both text and flag: more than one branch, so oneOf fails.
        AssertBoth(evaluator, """["on"]""", false);

        using JsonSchemaEvaluator anyOf = JsonSchemaEvaluator.Compile("""
            {"anyOf": [{"$ref": "#/$defs/o"}, {"type": "string", "minLength": 2}], "$defs": {"o": {"type": "object", "required": ["k"]}}}
            """);
        AssertBoth(anyOf, "\"ab\"", true);
        AssertBoth(anyOf, "\"a\"", false);
        AssertBoth(anyOf, """{"k": 1}""", true);
        AssertBoth(anyOf, "{}", false);
        AssertBoth(anyOf, "1", false);

        // With unevaluatedProperties, only the candidate branches mark properties evaluated.
        using JsonSchemaEvaluator tracked = JsonSchemaEvaluator.Compile("""
            {
              "oneOf": [{"type": "object", "properties": {"a": true}, "required": ["a"]}, {"type": "array"}, {"type": "object", "properties": {"b": true}, "required": ["b"]}],
              "unevaluatedProperties": false
            }
            """);
        AssertBoth(tracked, """{"a": 1}""", true);
        AssertBoth(tracked, """{"b": 1}""", true);
        AssertBoth(tracked, """{"a": 1, "c": 1}""", false);
        AssertBoth(tracked, "[]", true);
        AssertBoth(tracked, "1", false);
    }

    [TestMethod]
    public void TypeUnionAndDispatchChildrenDecidedInPlace()
    {
        // Children that are an anyOf/oneOf of type-only branches (one mask) or of branches with disjoint types (a
        // dispatch), under the strict loop and as array items.
        const string schema = """
            {
              "type": "object",
              "properties": {
                "union": {"anyOf": [{"type": "string"}, {"type": "boolean"}]},
                "whole": {"oneOf": [{"type": "integer"}, {"type": "null"}]},
                "dispatch": {"oneOf": [{"type": "string", "maxLength": 2}, {"type": "array", "items": {"type": "integer"}}, {"type": "null"}]},
                "list": {"type": "array", "items": {"anyOf": [{"type": "number"}, {"type": "object", "required": ["k"]}]}}
              }
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        AssertBoth(evaluator, """{"union": "s"}""", true);
        AssertBoth(evaluator, """{"union": false}""", true);
        AssertBoth(evaluator, """{"union": 1}""", false);
        AssertBoth(evaluator, """{"whole": 2}""", true);
        AssertBoth(evaluator, """{"whole": 2.5}""", false);
        AssertBoth(evaluator, """{"whole": null}""", true);
        AssertBoth(evaluator, """{"whole": "x"}""", false);
        AssertBoth(evaluator, """{"dispatch": "ab"}""", true);
        AssertBoth(evaluator, """{"dispatch": "abc"}""", false);
        AssertBoth(evaluator, """{"dispatch": [1, 2]}""", true);
        AssertBoth(evaluator, """{"dispatch": [1, "x"]}""", false);
        AssertBoth(evaluator, """{"dispatch": null}""", true);
        AssertBoth(evaluator, """{"dispatch": true}""", false);
        AssertBoth(evaluator, """{"list": [1, 2.5, {"k": 1}]}""", true);
        AssertBoth(evaluator, """{"list": [{"j": 1}]}""", false);
        AssertBoth(evaluator, """{"list": ["x"]}""", false);
    }
}
