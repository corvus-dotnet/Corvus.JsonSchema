// <copyright file="FusedObjectTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// The fused object plan (flag mode) must agree with the general path (which collecting mode always takes) on
/// schemas whose object semantics are spread over allOf, $ref and if/then/else, with per-branch additional and
/// pattern properties, required lists, and unevaluatedProperties.
/// </summary>
[TestClass]
public class FusedObjectTests
{
    private const string AllOfWithUnevaluated = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "allOf": [
            {"properties": {"id": {"type": "integer"}, "name": {"type": "string"}}},
            {"properties": {"email": {"type": "string"}, "tags": {"type": "array"}}},
            {"if": {"required": ["active"]}, "then": {"properties": {"active": {"type": "boolean"}, "score": {"type": "number"}}}}
          ],
          "properties": {"address": {"type": "object"}},
          "unevaluatedProperties": false
        }
        """;

    private const string PerBranchAdditional = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "allOf": [
            {"properties": {"a": {"type": "integer"}}, "additionalProperties": {"type": "string"}},
            {"properties": {"b": {"type": "string"}}, "patternProperties": {"^x-": {"type": "number"}}, "additionalProperties": false, "required": ["b"]},
            {"minProperties": 1, "maxProperties": 4}
          ],
          "unevaluatedProperties": {"type": "string"}
        }
        """;

    private const string RefChainWithElse = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$ref": "#/$defs/base",
          "if": {"required": ["kind"]},
          "then": {"properties": {"kind": {"enum": ["a", "b"]}, "payload": {"type": "object"}}, "required": ["payload"]},
          "else": {"properties": {"legacy": {"type": "string"}}, "required": ["legacy"]},
          "unevaluatedProperties": {"type": "boolean"},
          "$defs": {
            "base": {"$ref": "#/$defs/core", "properties": {"version": {"type": "integer", "minimum": 1}}},
            "core": {"properties": {"id": {"type": "string"}}, "required": ["id"]}
          }
        }
        """;

    private const string ValueConditions = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "allOf": [
            {"properties": {"in": {"enum": ["query", "path", "header"]}, "name": {"type": "string"}}, "required": ["in", "name"]},
            {"if": {"properties": {"in": {"const": "path"}}, "required": ["in"]}, "then": {"properties": {"required": {"const": true}}, "required": ["required"]}},
            {"if": {"properties": {"in": {"const": "query"}}}, "then": {"properties": {"allowEmptyValue": {"type": "boolean"}}}, "else": {"properties": {"style": {"type": "string"}}}},
            {"if": {"properties": {"version": {"enum": [1, 2]}}, "required": ["version"]}, "then": {"properties": {"legacy": {"type": "boolean"}}}}
          ],
          "unevaluatedProperties": false
        }
        """;

    private const string OpenApiLikeParameter = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "name": {"type": "string"},
            "in": {"enum": ["query", "header", "path", "cookie"]},
            "required": {"type": "boolean"},
            "schema": {"type": "object"},
            "content": {"type": "object"}
          },
          "required": ["in", "name"],
          "oneOf": [{"required": ["schema"]}, {"required": ["content"]}],
          "if": {"properties": {"in": {"const": "query"}}, "required": ["in"]},
          "then": {"properties": {"allowEmptyValue": {"type": "boolean"}}},
          "dependentSchemas": {
            "schema": {
              "properties": {"style": {"type": "string"}, "explode": {"type": "boolean"}},
              "allOf": [
                {"if": {"properties": {"in": {"const": "path"}}, "required": ["in"]}, "then": {"properties": {"required": {"const": true}}, "required": ["required"]}},
                {"if": {"properties": {"style": {"const": "form"}}, "required": ["style"]}, "then": {"properties": {"explode": {"default": true}}}, "else": {"properties": {"explode": {"default": false}}}},
                {"if": {"properties": {"in": {"const": "header"}, "scheme": {"type": "string", "pattern": "^[Bb]earer$"}}, "required": ["in", "scheme"]}, "then": {"properties": {"bearerFormat": {"type": "string"}}}}
              ]
            }
          },
          "dependentRequired": {"content": ["name"]},
          "unevaluatedProperties": false
        }
        """;

    private static readonly string[] Instances =
    [
        """{"id": 1, "name": "n", "email": "e", "tags": [], "address": {}}""",
        """{"id": 1, "active": true, "score": 2}""",
        """{"id": 1, "score": 2}""",
        """{"id": 1, "active": "no"}""",
        """{"id": "x"}""",
        """{"other": 1}""",
        """{}""",
        """{"a": 1, "b": "s"}""",
        """{"a": 1, "b": "s", "x-1": 2}""",
        """{"a": 1, "b": "s", "x-1": "no"}""",
        """{"a": 1, "b": "s", "c": "str"}""",
        """{"a": "no", "b": "s"}""",
        """{"a": 1}""",
        """{"a": 1, "b": "s", "x-1": 1, "x-2": 2, "x-3": 3}""",
        """{"id": "i", "version": 2, "kind": "a", "payload": {}}""",
        """{"id": "i", "version": 2, "kind": "c", "payload": {}}""",
        """{"id": "i", "version": 2, "kind": "a"}""",
        """{"id": "i", "legacy": "old"}""",
        """{"id": "i"}""",
        """{"id": "i", "legacy": "old", "flag": true}""",
        """{"id": "i", "legacy": "old", "flag": "no"}""",
        """{"version": 0, "legacy": "old"}""",
        """[1, 2]""",
        "\"string\"",
        """{"in": "query", "name": "q", "allowEmptyValue": true}""",
        """{"in": "query", "name": "q", "style": "form"}""",
        """{"in": "path", "name": "id", "required": true}""",
        """{"in": "path", "name": "id"}""",
        """{"in": "path", "name": "id", "required": false}""",
        """{"in": "header", "name": "h", "style": "simple"}""",
        """{"in": "header", "name": "h", "allowEmptyValue": true}""",
        """{"in": "cookie", "name": "c"}""",
        """{"in": "query", "name": "q", "version": 2, "legacy": true}""",
        """{"in": "query", "name": "q", "version": 2.0, "legacy": true}""",
        """{"in": "query", "name": "q", "version": 3, "legacy": true}""",
        """{"in": "query", "name": "q", "version": "2", "legacy": true}""",
        """{"in": 5, "name": "q"}""",
        """{"in": "query", "name": "q", "schema": {}}""",
        """{"in": "query", "name": "q", "content": {}}""",
        """{"in": "query", "name": "q", "schema": {}, "content": {}}""",
        """{"in": "query", "name": "q"}""",
        """{"in": "query", "name": "q", "schema": {}, "style": "form", "explode": true}""",
        """{"in": "query", "name": "q", "schema": {}, "style": "form", "explode": "yes"}""",
        """{"in": "query", "name": "q", "content": {}, "style": "form"}""",
        """{"in": "path", "name": "id", "schema": {}, "required": true}""",
        """{"in": "path", "name": "id", "schema": {}}""",
        """{"in": "path", "name": "id", "content": {}}""",
        """{"in": "header", "name": "h", "schema": {}, "scheme": "Bearer", "bearerFormat": "JWT"}""",
        """{"in": "header", "name": "h", "schema": {}, "scheme": "Basic", "bearerFormat": "JWT"}""",
        """{"in": "header", "name": "h", "schema": {}, "scheme": 7, "bearerFormat": "JWT"}""",
        """{"in": "query", "name": "q", "schema": {}, "allowEmptyValue": true}""",
        """{"in": "header", "name": "h", "schema": {}, "allowEmptyValue": true}""",
    ];

    [TestMethod]
    [DataRow(AllOfWithUnevaluated, DisplayName = "allOf with unevaluatedProperties")]
    [DataRow(PerBranchAdditional, DisplayName = "per-branch additional and pattern properties")]
    [DataRow(ValueConditions, DisplayName = "conditions on property values")]
    [DataRow(OpenApiLikeParameter, DisplayName = "dependent schemas, required-only oneOf, nested and pattern conditions")]
    [DataRow(RefChainWithElse, DisplayName = "$ref chain with if/then/else")]
    public void FusedPlanAgreesWithTheGeneralPath(string schema)
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        Assert.AreEqual(NodePlan.FusedObject, evaluator.Program.Nodes[evaluator.RootNode].Plan, "The root is expected to fuse.");

        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        Assert.AreEqual(NodePlan.FusedObject, loaded.Program.Nodes[loaded.RootNode].Plan, "The plan is rebuilt on load.");

        foreach (string instance in Instances)
        {
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
            using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
            bool general = evaluator.Evaluate(doc.RootElement, collector);
            Assert.AreEqual(general, evaluator.Evaluate(doc.RootElement), $"flag mode differs for {instance}");
            Assert.AreEqual(general, loaded.Evaluate(doc.RootElement), $"image differs for {instance}");
        }
    }

    [TestMethod]
    public void ShapesThatCannotFuseKeepTheGeneralPlan()
    {
        // anyOf is not an in-place applicator the plan fuses.
        using JsonSchemaEvaluator anyOf = JsonSchemaEvaluator.Compile("""{"anyOf": [{"properties": {"a": {"type": "integer"}}}, {"required": ["b"]}], "unevaluatedProperties": false}""");
        Assert.AreNotEqual(NodePlan.FusedObject, anyOf.Program.Nodes[anyOf.RootNode].Plan);

        // An if that tests more than presence is not fused.
        using JsonSchemaEvaluator ifValue = JsonSchemaEvaluator.Compile("""{"if": {"properties": {"kind": {"const": "a"}}}, "then": {"required": ["x"]}}""");
        Assert.AreNotEqual(NodePlan.FusedObject, ifValue.Program.Nodes[ifValue.RootNode].Plan);

        // A live dynamic scope reachable from the node disables fusion for that node.
        using JsonSchemaEvaluator dynamic = JsonSchemaEvaluator.Compile("""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/outer",
              "$ref": "a",
              "allOf": [{"properties": {"p": {"type": "string"}}}],
              "$defs": {
                "a": {"$id": "a", "$dynamicAnchor": "items", "$ref": "b"},
                "b": {"$id": "b", "$dynamicAnchor": "items", "type": "array", "items": {"$dynamicRef": "#items"}}
              }
            }
            """);
        Assert.IsTrue(dynamic.UsesDynamicScope);
        Assert.AreNotEqual(NodePlan.FusedObject, dynamic.Program.Nodes[dynamic.RootNode].Plan);
    }

    [TestMethod]
    public void ADynamicReferenceElsewhereInTheProgramDoesNotBlockFusion()
    {
        // A generated program compiles every schema of a compilation as one program with an entry point per type. A
        // live $dynamicRef in one schema must not cost the fused plan in an unrelated one.
        const string program = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/program",
              "$defs": {
                "fusable": {
                  "allOf": [
                    {"properties": {"id": {"type": "integer"}, "name": {"type": "string"}}},
                    {"if": {"required": ["active"]}, "then": {"properties": {"active": {"type": "boolean"}}}}
                  ],
                  "unevaluatedProperties": false
                },
                "dynamic": {
                  "$id": "dynamic",
                  "$ref": "a",
                  "$defs": {
                    "a": {"$id": "a", "$dynamicAnchor": "items", "$ref": "b"},
                    "b": {"$id": "b", "$dynamicAnchor": "items", "type": "array", "items": {"$dynamicRef": "#items"}}
                  }
                }
              }
            }
            """;
        using JsonSchemaEvaluator root = JsonSchemaEvaluator.Compile(System.Text.Encoding.UTF8.GetBytes(program), "#/$defs/fusable");
        root.RegisterEntryPoints(["#/$defs/dynamic"]);
        Assert.IsTrue(root.UsesDynamicScope, "The dynamic schema keeps its dynamic scope.");
        Assert.AreEqual(NodePlan.FusedObject, root.Program.Nodes[root.RootNode].Plan, "The unrelated schema still fuses.");

        JsonSchemaEvaluator fusable = root.ForEntryPoint("#/$defs/fusable");
        Assert.IsTrue(fusable.Evaluate("""{"id": 1, "name": "n", "active": true}"""));
        Assert.IsFalse(fusable.Evaluate("""{"id": 1, "other": true}"""));
        Assert.IsFalse(fusable.Evaluate("""{"active": "no"}"""));

        JsonSchemaEvaluator dynamic = root.ForEntryPoint("#/$defs/dynamic");
        Assert.IsTrue(dynamic.Evaluate("[[], [[]]]"));
        Assert.IsFalse(dynamic.Evaluate("[1]"));
    }
}
