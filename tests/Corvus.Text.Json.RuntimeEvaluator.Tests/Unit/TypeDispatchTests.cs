// <copyright file="TypeDispatchTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// A <c>oneOf</c>/<c>anyOf</c> whose branches assert disjoint types is decided by the instance's token type: only
/// the branch for that type is evaluated, and no branch means no match. Branches may be any plan, and the
/// dispatch must agree with the general path in every mode, including under unevaluated tracking.
/// </summary>
[TestClass]
public class TypeDispatchTests
{
    private const string MixedOneOf = """
        {
          "oneOf": [
            {"type": "string", "minLength": 2},
            {"type": "object", "properties": {"name": {"type": "string"}}, "required": ["name"]},
            {"type": "array", "items": {"type": "integer"}},
            {"type": ["boolean", "null"]}
          ]
        }
        """;

    private const string NumberAnyOf = """
        {
          "anyOf": [
            {"type": "integer", "minimum": 0},
            {"type": "string", "pattern": "^[0-9]+$"}
          ]
        }
        """;

    private static readonly string[] Instances =
    [
        "\"ab\"", "\"a\"", """{"name": "x"}""", """{"name": 1}""", """{}""", "[1, 2]", "[1, \"2\"]", "[]", "true", "false", "null", "1", "1.0", "1.5", "-1", "\"12\"", "\"1a\"",
    ];

    private static void AssertAgree(JsonSchemaEvaluator evaluator, JsonSchemaEvaluator loaded, string instance)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        bool general = evaluator.Evaluate(doc.RootElement, collector);
        Assert.AreEqual(general, evaluator.Evaluate(doc.RootElement), $"flag mode differs for {instance}");
        Assert.AreEqual(general, loaded.Evaluate(doc.RootElement), $"image differs for {instance}");
    }

    [TestMethod]
    public void DisjointTypesDispatchOneOf()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(MixedOneOf);
        Assert.IsNotNull(evaluator.Program.Nodes[evaluator.RootNode].OneOfTypeDispatch, "Four branches with disjoint types dispatch on the token type.");
        Assert.AreEqual(NodePlan.TypeDispatch, evaluator.Program.Nodes[evaluator.RootNode].Plan, "A node whose only keyword is the oneOf takes the dispatch plan.");
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        Assert.IsNotNull(loaded.Program.Nodes[loaded.RootNode].OneOfTypeDispatch, "The dispatch is rebuilt on load.");

        Assert.IsTrue(evaluator.Evaluate("\"ab\""));
        Assert.IsFalse(evaluator.Evaluate("\"a\""), "The string branch's own keywords still apply.");
        Assert.IsTrue(evaluator.Evaluate("""{"name": "x"}"""));
        Assert.IsFalse(evaluator.Evaluate("""{}"""));
        Assert.IsTrue(evaluator.Evaluate("[1, 2]"));
        Assert.IsFalse(evaluator.Evaluate("[1, \"2\"]"));
        Assert.IsTrue(evaluator.Evaluate("null"));
        Assert.IsTrue(evaluator.Evaluate("false"));
        Assert.IsFalse(evaluator.Evaluate("1"), "No branch accepts a number.");
        foreach (string instance in Instances)
        {
            AssertAgree(evaluator, loaded, instance);
        }
    }

    [TestMethod]
    public void IntegerBranchKeepsItsOwnTypeTest()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(NumberAnyOf);
        Assert.IsNotNull(evaluator.Program.Nodes[evaluator.RootNode].AnyOfTypeDispatch);
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        Assert.IsTrue(evaluator.Evaluate("1"));
        Assert.IsTrue(evaluator.Evaluate("1.0"), "1.0 is an integer in 2020-12.");
        Assert.IsFalse(evaluator.Evaluate("1.5"), "The number token dispatches to the integer branch, which rejects a fraction.");
        Assert.IsFalse(evaluator.Evaluate("-1"));
        Assert.IsTrue(evaluator.Evaluate("\"12\""));
        Assert.IsFalse(evaluator.Evaluate("\"1a\""));
        foreach (string instance in Instances)
        {
            AssertAgree(evaluator, loaded, instance);
        }

        using JsonSchemaEvaluator draft4 = JsonSchemaEvaluator.Compile("""{"$schema": "http://json-schema.org/draft-04/schema#", "anyOf": [{"type": "integer", "minimum": 0}, {"type": "string", "minLength": 1}]}""");
        Assert.IsNotNull(draft4.Program.Nodes[draft4.RootNode].AnyOfTypeDispatch);
        Assert.IsTrue(draft4.Evaluate("1"));
        Assert.IsFalse(draft4.Evaluate("1.0"), "Draft 4 integers are lexical.");
    }

    [TestMethod]
    public void OverlappingOrUntypedBranchesDoNotDispatch()
    {
        // integer and number both accept a number token.
        using JsonSchemaEvaluator numeric = JsonSchemaEvaluator.Compile("""{"oneOf": [{"type": "integer"}, {"type": "number", "multipleOf": 0.5}]}""");
        Assert.IsNull(numeric.Program.Nodes[numeric.RootNode].OneOfTypeDispatch);
        Assert.IsFalse(numeric.Evaluate("1"), "Both branches match, so oneOf fails.");
        Assert.IsTrue(numeric.Evaluate("1.5"));

        // A branch without type could match anything.
        using JsonSchemaEvaluator untyped = JsonSchemaEvaluator.Compile("""{"anyOf": [{"type": "string"}, {"minimum": 1}]}""");
        Assert.IsNull(untyped.Program.Nodes[untyped.RootNode].AnyOfTypeDispatch);
        Assert.IsTrue(untyped.Evaluate("\"x\""));
        Assert.IsTrue(untyped.Evaluate("2"));
        Assert.IsTrue(untyped.Evaluate("true"), "The untyped branch accepts a boolean.");

        // Two branches sharing a type.
        using JsonSchemaEvaluator shared = JsonSchemaEvaluator.Compile("""{"anyOf": [{"type": "string", "maxLength": 1}, {"type": ["string", "null"], "minLength": 3}]}""");
        Assert.IsNull(shared.Program.Nodes[shared.RootNode].AnyOfTypeDispatch);
        Assert.IsTrue(shared.Evaluate("\"abc\""));
        Assert.IsTrue(shared.Evaluate("\"a\""));
        Assert.IsFalse(shared.Evaluate("\"ab\""));

        // Type-only branches keep the cheaper union test, as a plan of its own.
        using JsonSchemaEvaluator union = JsonSchemaEvaluator.Compile("""{"anyOf": [{"type": "string"}, {"type": "null"}]}""");
        Assert.AreNotEqual(TypeMask.None, union.Program.Nodes[union.RootNode].AnyOfTypeUnion);
        Assert.IsNull(union.Program.Nodes[union.RootNode].AnyOfTypeDispatch);
        Assert.AreEqual(NodePlan.TypeUnion, union.Program.Nodes[union.RootNode].Plan);
        using JsonSchemaEvaluator unionLoaded = JsonSchemaEvaluator.FromProgramImage(union.ToProgramImage());
        Assert.AreEqual(NodePlan.TypeUnion, unionLoaded.Program.Nodes[unionLoaded.RootNode].Plan);
        foreach (string instance in new[] { "\"s\"", "null", "1", "{}", "true" })
        {
            AssertAgree(union, unionLoaded, instance);
        }

        // With another keyword alongside, the node keeps the general plan (the keyword still uses the union test).
        using JsonSchemaEvaluator mixed = JsonSchemaEvaluator.Compile("""{"anyOf": [{"type": "string"}, {"type": "null"}], "minLength": 2}""");
        Assert.AreEqual(NodePlan.General, mixed.Program.Nodes[mixed.RootNode].Plan);
        Assert.IsFalse(mixed.Evaluate("\"s\""));
        Assert.IsTrue(mixed.Evaluate("\"ss\""));
        Assert.IsTrue(mixed.Evaluate("null"));

        // A branch on an in-place cycle keeps the guarded general edge.
        using JsonSchemaEvaluator cyclic = JsonSchemaEvaluator.Compile("""{"oneOf": [{"type": "string"}, {"type": "array", "items": {"$ref": "#"}}, {"type": "object", "allOf": [{"$ref": "#/$defs/loop"}]}], "$defs": {"loop": {"anyOf": [{"$ref": "#/$defs/loop"}, {"type": "object"}]}}}""");
        Assert.IsTrue(cyclic.Evaluate("[\"a\", [\"b\"]]"));
        Assert.IsFalse(cyclic.Evaluate("[1]"));
        Assert.ThrowsExactly<JsonSchemaEvaluationException>(() => cyclic.Evaluate("{}"), "The self-referential anyOf is detected as runaway recursion.");
    }

    [TestMethod]
    public void DispatchedBranchesMarkEvaluatedProperties()
    {
        const string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "oneOf": [
                {"type": "string"},
                {"type": "object", "properties": {"a": {"type": "integer"}}}
              ],
              "unevaluatedProperties": false
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        Assert.IsNotNull(evaluator.Program.Nodes[evaluator.RootNode].OneOfTypeDispatch);
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        Assert.IsTrue(evaluator.Evaluate("""{"a": 1}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"b": 1}"""), "b is unevaluated.");
        Assert.IsFalse(evaluator.Evaluate("""{"a": "x"}"""));
        Assert.IsTrue(evaluator.Evaluate("\"s\""));
        foreach (string instance in new[] { """{"a": 1}""", """{"b": 1}""", """{"a": "x"}""", "\"s\"", "1", "[]" })
        {
            AssertAgree(evaluator, loaded, instance);
        }
    }

    [TestMethod]
    public void RefBranchesDispatchThroughTheirTargets()
    {
        const string schema = """
            {
              "oneOf": [{"$ref": "#/$defs/s"}, {"$ref": "#/$defs/o"}],
              "$defs": {
                "s": {"type": "string", "enum": ["a", "b"]},
                "o": {"type": "object", "additionalProperties": {"type": "boolean"}}
              }
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        Assert.IsNotNull(evaluator.Program.Nodes[evaluator.RootNode].OneOfTypeDispatch, "Elided $ref branches expose their targets' types.");
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        foreach (string instance in new[] { "\"a\"", "\"c\"", """{"x": true}""", """{"x": 1}""", "1", "null" })
        {
            AssertAgree(evaluator, loaded, instance);
        }

        Assert.IsTrue(evaluator.Evaluate("\"a\""));
        Assert.IsFalse(evaluator.Evaluate("\"c\""));
        Assert.IsTrue(evaluator.Evaluate("""{"x": true}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"x": 1}"""));
        Assert.IsFalse(evaluator.Evaluate("null"));
    }
}
