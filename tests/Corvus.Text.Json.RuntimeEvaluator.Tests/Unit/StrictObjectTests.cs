// <copyright file="StrictObjectTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// The strict object plan (every property child a type-only leaf or true, required as one mask, unknown names rejected
/// only by additionalProperties: false) must agree with the general path in every mode and through the image, and
/// the inline type tests and required mask must behave the same on the ordinary object plan and the fused plan.
/// </summary>
[TestClass]
public class StrictObjectTests
{
    private const string ChartLock = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["generated", "digest", "dependencies"],
          "properties": {
            "generated": {"type": "string", "format": "date-time"},
            "digest": {"type": "string"},
            "dependencies": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["name", "version", "repository"],
                "properties": {"name": {"type": "string"}, "version": {"type": "string"}, "repository": {"type": "string", "format": "uri"}}
              }
            }
          }
        }
        """;

    private static readonly string[] Instances =
    [
        """{"generated": "2023-08-10T01:36:55Z", "digest": "sha256:0ee0", "dependencies": [{"name": "minio", "repository": "https://helm.min.io/", "version": "8.0.10"}]}""",
        """{"generated": "2023-08-10T01:36:55Z", "digest": "sha256:0ee0", "dependencies": []}""",
        """{"generated": "2023-08-10T01:36:55Z", "dependencies": []}""",
        """{"generated": 1, "digest": "d", "dependencies": []}""",
        """{"generated": "g", "digest": "d", "dependencies": [], "extra": 1}""",
        """{"generated": "g", "digest": "d", "dependencies": [{"name": "n", "version": "v"}]}""",
        """{"generated": "g", "digest": "d", "dependencies": [{"name": "n", "version": "v", "repository": "r", "x": 1}]}""",
        """{"generated": "g", "digest": "d", "dependencies": [{"name": "n", "version": 1, "repository": "r"}]}""",
        """{"generated": "g", "digest": "d", "dependencies": []}""",
        """{"generated": "g", "digest": "d", "dependencies": [], "digest": "twice"}""",
        "\"not an object\"",
        "[]",
        "{}",
    ];

    private static void AssertAgree(JsonSchemaEvaluator evaluator, JsonSchemaEvaluator loaded, string instance, bool? expected = null)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        bool general = evaluator.Evaluate(doc.RootElement, collector);
        Assert.AreEqual(general, evaluator.Evaluate(doc.RootElement), $"flag mode differs for {instance}");
        Assert.AreEqual(general, loaded.Evaluate(doc.RootElement), $"image differs for {instance}");
        if (expected is bool e)
        {
            Assert.AreEqual(e, general, instance);
        }
    }

    [TestMethod]
    public void ChartLockTakesTheStrictPlanAtBothLevels()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(ChartLock);
        SchemaNode root = evaluator.Program.Nodes[evaluator.RootNode];
        Assert.AreEqual(NodePlan.StrictObject, root.Plan);
        Assert.AreEqual(0b111UL, root.RequiredMask, "Three required bits.");
        SchemaNode items = evaluator.Program.Nodes.First(n => n.SchemaLocation.AsSpan().EndsWith("/items"u8));
        Assert.AreEqual(NodePlan.StrictObject, items.Plan);
        Assert.AreEqual(2, root.Properties!.Values.ToArray().Count(e => e.InlineType != TypeMask.None), "The two string children are inlined; the array child dispatches on its plan.");

        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        Assert.AreEqual(NodePlan.StrictObject, loaded.Program.Nodes[loaded.RootNode].Plan, "The plan and its details are rebuilt on load.");
        Assert.AreEqual(0b111UL, loaded.Program.Nodes[loaded.RootNode].RequiredMask);

        bool[] expected = [true, true, false, false, false, false, false, false, true, true, false, false, false];
        for (int i = 0; i < Instances.Length; i++)
        {
            AssertAgree(evaluator, loaded, Instances[i], expected[i]);
        }
    }

    [TestMethod]
    public void UnknownNamesAreAllowedWithoutAdditionalPropertiesFalse()
    {
        using JsonSchemaEvaluator open = JsonSchemaEvaluator.Compile("""{"properties": {"a": {"type": "integer"}, "b": true}, "required": ["a"]}""");
        Assert.AreEqual(NodePlan.StrictObject, open.Program.Nodes[open.RootNode].Plan);
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(open.ToProgramImage());
        AssertAgree(open, loaded, """{"a": 1, "z": "anything"}""", true);
        AssertAgree(open, loaded, """{"a": 1, "b": [1]}""", true);
        AssertAgree(open, loaded, """{"a": 1.5}""", false);
        AssertAgree(open, loaded, """{"a": 1.0}""", true);
        AssertAgree(open, loaded, """{"b": 1}""", false);

        using JsonSchemaEvaluator anyAdditional = JsonSchemaEvaluator.Compile("""{"properties": {"a": {"type": ["string", "null"]}}, "additionalProperties": true}""");
        Assert.AreEqual(NodePlan.StrictObject, anyAdditional.Program.Nodes[anyAdditional.RootNode].Plan);
        Assert.IsTrue(anyAdditional.Evaluate("""{"a": null, "q": 1}"""));
        Assert.IsFalse(anyAdditional.Evaluate("""{"a": 1}"""));

        using JsonSchemaEvaluator draft4 = JsonSchemaEvaluator.Compile("""{"$schema": "http://json-schema.org/draft-04/schema#", "properties": {"a": {"type": "integer"}}}""");
        Assert.AreEqual(NodePlan.StrictObject, draft4.Program.Nodes[draft4.RootNode].Plan);
        Assert.IsTrue(draft4.Evaluate("""{"a": 1}"""));
        Assert.IsFalse(draft4.Evaluate("""{"a": 1.0}"""), "Draft 4 integers are lexical.");
    }

    [TestMethod]
    public void ShapesOutsideTheStrictPlanKeepTheObjectPlan()
    {
        // A child with its own keywords is dispatched on its plan from the strict loop.
        using JsonSchemaEvaluator schemaChild = JsonSchemaEvaluator.Compile("""{"properties": {"a": {"type": "string", "minLength": 1}, "b": {"type": "integer"}}, "required": ["a"]}""");
        Assert.AreEqual(NodePlan.StrictObject, schemaChild.Program.Nodes[schemaChild.RootNode].Plan);
        Assert.IsTrue(schemaChild.Evaluate("""{"a": "x", "b": 2}"""));
        Assert.IsFalse(schemaChild.Evaluate("""{"a": "", "b": 2}"""));
        Assert.IsFalse(schemaChild.Evaluate("""{"a": "x", "b": "2"}"""), "The inlined child still applies.");
        Assert.IsFalse(schemaChild.Evaluate("""{"b": 2}"""), "Required as a mask.");

        // A type-only additionalProperties is tested in place on the strict plan.
        using JsonSchemaEvaluator additionalSchema = JsonSchemaEvaluator.Compile("""{"properties": {"a": {"type": "string"}}, "additionalProperties": {"type": "integer"}}""");
        Assert.AreEqual(NodePlan.StrictObject, additionalSchema.Program.Nodes[additionalSchema.RootNode].Plan);
        Assert.AreEqual(TypeMask.Integer, additionalSchema.Program.Nodes[additionalSchema.RootNode].AdditionalInlineType);
        Assert.IsTrue(additionalSchema.Evaluate("""{"a": "x", "n": 1}"""));
        Assert.IsFalse(additionalSchema.Evaluate("""{"a": "x", "n": "1"}"""));

        using JsonSchemaEvaluator patterns = JsonSchemaEvaluator.Compile("""{"properties": {"a": {"type": "string"}}, "patternProperties": {"^x": {"type": "integer"}}}""");
        Assert.AreEqual(NodePlan.Object, patterns.Program.Nodes[patterns.RootNode].Plan);

        using JsonSchemaEvaluator dependent = JsonSchemaEvaluator.Compile("""{"properties": {"a": {"type": "string"}, "b": {"type": "string"}}, "dependentRequired": {"a": ["b"]}}""");
        Assert.AreEqual(NodePlan.Object, dependent.Program.Nodes[dependent.RootNode].Plan);
        Assert.IsFalse(dependent.Evaluate("""{"a": "x"}"""));
        Assert.IsTrue(dependent.Evaluate("""{"a": "x", "b": "y"}"""));

        // More than 64 named properties: the mask cannot hold them, so the bit loop stays.
        string many = "{\"properties\": {" + string.Join(", ", Enumerable.Range(0, 70).Select(i => $"\"p{i}\": {{\"type\": \"integer\"}}")) + "}, \"required\": [" + string.Join(", ", Enumerable.Range(0, 70).Select(i => $"\"p{i}\"")) + "]}";
        using JsonSchemaEvaluator wide = JsonSchemaEvaluator.Compile(many);
        Assert.AreEqual(NodePlan.Object, wide.Program.Nodes[wide.RootNode].Plan);
        string all = "{" + string.Join(", ", Enumerable.Range(0, 70).Select(i => $"\"p{i}\": {i}")) + "}";
        Assert.IsTrue(wide.Evaluate(all));
        Assert.IsFalse(wide.Evaluate(all.Replace("\"p69\": 69", "\"p69\": \"x\"")));
        Assert.IsFalse(wide.Evaluate("{" + string.Join(", ", Enumerable.Range(0, 69).Select(i => $"\"p{i}\": {i}")) + "}"), "One required name missing.");
    }

    [TestMethod]
    public void MapsAndTypedAdditionalPropertiesTakeTheStrictPlan()
    {
        const string importMap = """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "imports": {"type": "object", "additionalProperties": {"type": "string"}},
                "scopes": {"type": "object", "additionalProperties": {"type": "object", "additionalProperties": {"type": "string"}}}
              }
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(importMap);
        foreach (SchemaNode n in evaluator.Program.Nodes)
        {
            if (n.HasObjectKeywords)
            {
                Assert.AreEqual(NodePlan.StrictObject, n.Plan, System.Text.Encoding.UTF8.GetString(n.SchemaLocation));
            }
        }

        SchemaNode imports = evaluator.Program.Nodes.First(n => n.SchemaLocation.AsSpan().EndsWith("/imports"u8));
        Assert.AreEqual(TypeMask.String, imports.AdditionalInlineType, "A type-only additionalProperties is tested in place.");
        Assert.IsTrue(evaluator.Program.Nodes[evaluator.RootNode].AdditionalRejects);

        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        AssertAgree(evaluator, loaded, """{"imports": {"react": "https://esm.sh/react", "vue": "https://esm.sh/vue"}}""", true);
        AssertAgree(evaluator, loaded, """{"imports": {"react": 1}}""", false);
        AssertAgree(evaluator, loaded, """{"imports": {}, "scopes": {"/a/": {"x": "y"}, "/b/": {}}}""", true);
        AssertAgree(evaluator, loaded, """{"scopes": {"/a/": {"x": 1}}}""", false);
        AssertAgree(evaluator, loaded, """{"scopes": {"/a/": "not an object"}}""", false);
        AssertAgree(evaluator, loaded, """{"other": {}}""", false);
        AssertAgree(evaluator, loaded, """{"imports": {"\u0061": "escaped name is still a string value"}}""", true);

        // A schema additionalProperties that is neither true, false nor type-only is dispatched on its plan.
        using JsonSchemaEvaluator schemaAdditional = JsonSchemaEvaluator.Compile("""{"properties": {"a": {"type": "string"}}, "additionalProperties": {"type": "integer", "minimum": 0}}""");
        Assert.AreEqual(NodePlan.StrictObject, schemaAdditional.Program.Nodes[schemaAdditional.RootNode].Plan);
        Assert.IsTrue(schemaAdditional.Evaluate("""{"a": "x", "n": 1}"""));
        Assert.IsFalse(schemaAdditional.Evaluate("""{"a": "x", "n": -1}"""));
        Assert.IsFalse(schemaAdditional.Evaluate("""{"a": 1}"""));
    }

    [TestMethod]
    public void StringEnumChildrenAreTestedInPlace()
    {
        const string schema = """
            {
              "properties": {"level": {"enum": ["debug", "info", "warn"]}, "mode": {"type": "string", "enum": ["a", "b"]}, "count": {"enum": [1, 2]}},
              "required": ["level"]
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        SchemaNode root = evaluator.Program.Nodes[evaluator.RootNode];
        Assert.AreEqual(NodePlan.StrictObject, root.Plan);
        PropertyEntry[] entries = root.Properties!.Values.ToArray();
        Assert.AreEqual(2, entries.Count(e => e.InlineEnum is not null), "The two string enums are inlined; the integer enum dispatches to its leaf.");

        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        AssertAgree(evaluator, loaded, """{"level": "info", "mode": "b", "count": 2}""", true);
        AssertAgree(evaluator, loaded, """{"level": "verbose"}""", false);
        AssertAgree(evaluator, loaded, """{"level": 1}""", false);
        AssertAgree(evaluator, loaded, """{"level": "warn", "mode": "c"}""", false);
        AssertAgree(evaluator, loaded, """{"level": "warn", "count": 3}""", false);
        AssertAgree(evaluator, loaded, """{"level": "in\u0066o"}""", true);
        AssertAgree(evaluator, loaded, """{"mode": "a"}""", false);
    }

    [TestMethod]
    public void FusedPlanInlinesTypeOnlyChildren()
    {
        const string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "allOf": [
                {"properties": {"id": {"type": "integer"}, "name": {"type": "string"}}, "required": ["id"]},
                {"properties": {"tags": {"type": "array"}, "meta": true}}
              ],
              "unevaluatedProperties": false
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        Assert.AreEqual(NodePlan.FusedObject, evaluator.Program.Nodes[evaluator.RootNode].Plan);
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        AssertAgree(evaluator, loaded, """{"id": 1, "name": "n", "tags": [], "meta": {"x": 1}}""", true);
        AssertAgree(evaluator, loaded, """{"id": "1"}""", false);
        AssertAgree(evaluator, loaded, """{"id": 1, "tags": {}}""", false);
        AssertAgree(evaluator, loaded, """{"id": 1, "other": 1}""", false);
        AssertAgree(evaluator, loaded, """{"name": "n"}""", false);
    }
}
