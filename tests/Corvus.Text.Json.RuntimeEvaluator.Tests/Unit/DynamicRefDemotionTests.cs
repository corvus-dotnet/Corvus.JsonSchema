// <copyright file="DynamicRefDemotionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// A <c>$dynamicRef</c> whose anchor is defined, with one target, in every resource evaluation can start in resolves
/// the same way on every path, so the compiler makes it a static reference and the program needs no dynamic scope.
/// </summary>
[TestClass]
public class DynamicRefDemotionTests
{
    private const string StrictTree = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://example.com/strict-tree",
          "$dynamicAnchor": "node",
          "$ref": "tree",
          "unevaluatedProperties": false,
          "$defs": {
            "tree": {
              "$id": "tree",
              "$dynamicAnchor": "node",
              "type": "object",
              "properties": { "data": true, "children": { "type": "array", "items": { "$dynamicRef": "#node" } } }
            }
          }
        }
        """;

    [TestMethod]
    public void StrictTreeNeedsNoDynamicScope()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(StrictTree);
        Assert.IsFalse(evaluator.UsesDynamicScope, "The root resource defines the anchor, so every $dynamicRef resolves to it.");
        Assert.IsTrue(evaluator.Evaluate("""{"data": 1, "children": [{"data": 2, "children": [{"data": 3}]}]}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"data": 1, "children": [{"data": 2, "extra": true}]}"""), "The strict extension applies at every level.");
        Assert.IsFalse(evaluator.Evaluate("""{"data": 1, "children": [1]}"""));
    }

    [TestMethod]
    public void StrictTreeThroughAnImageNeedsNoDynamicScope()
    {
        using JsonSchemaEvaluator compiled = JsonSchemaEvaluator.Compile(StrictTree);
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(compiled.ToProgramImage());
        Assert.IsFalse(loaded.UsesDynamicScope);
        Assert.IsFalse(loaded.Evaluate("""{"data": 1, "children": [{"data": 2, "extra": true}]}"""));
    }

    [TestMethod]
    public void EntryInTheSubresourceKeepsItsOwnBookend()
    {
        // Entering at the tree resource, only its own (permissive) anchor is in scope.
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(StrictTree, new JsonSchemaEvaluatorOptions { EntryPoint = "#/$defs/tree" });
        Assert.IsFalse(evaluator.UsesDynamicScope);
        Assert.IsTrue(evaluator.Evaluate("""{"data": 1, "children": [{"data": 2, "extra": true}]}"""), "Without the root in scope the extension is not strict.");
    }

    [TestMethod]
    public void TwoEntriesWithDifferentTargetsStayDynamic()
    {
        using JsonSchemaEvaluator root = JsonSchemaEvaluator.Compile(StrictTree);
        using JsonSchemaEvaluator tree = root.ForEntryPoint("#/$defs/tree");
        Assert.IsTrue(root.UsesDynamicScope, "Two entry resources resolve the anchor to different nodes, so the reference must stay dynamic.");
        Assert.IsFalse(root.Evaluate("""{"data": 1, "children": [{"data": 2, "extra": true}]}"""));
        Assert.IsTrue(tree.Evaluate("""{"data": 1, "children": [{"data": 2, "extra": true}]}"""));
    }

    [TestMethod]
    public void AnchorMissingFromTheEntryResourceStaysDynamic()
    {
        const string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/outer",
              "$ref": "a",
              "$defs": {
                "a": { "$id": "a", "$dynamicAnchor": "items", "$ref": "b" },
                "b": { "$id": "b", "$dynamicAnchor": "items", "type": "array", "items": { "$dynamicRef": "#items" } }
              }
            }
            """;
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        Assert.IsTrue(evaluator.UsesDynamicScope, "The entry resource has no anchor, so the scope decides at evaluation time.");
    }
}
