// <copyright file="DiscriminatorKeyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// Branch selection keyed on a property whose constraint is an integer or boolean constant, or a mixed enum, and the
/// fallbacks for values that cannot be keyed.
/// </summary>
[TestClass]
public class DiscriminatorKeyTests
{
    private const string VersionedOneOf = """
        {
          "oneOf": [
            {"type": "object", "required": ["version"], "properties": {"version": {"const": 1}, "a": {"type": "string"}}, "additionalProperties": false},
            {"type": "object", "required": ["version"], "properties": {"version": {"const": 2}, "b": {"type": "string"}}, "additionalProperties": false},
            {"type": "object", "required": ["version"], "properties": {"version": {"enum": [3, 4]}, "c": {"type": "string"}}, "additionalProperties": false},
            {"type": "object", "required": ["version"], "properties": {"version": {"const": "legacy"}, "d": {"type": "string"}}, "additionalProperties": false}
          ]
        }
        """;

    private const string BooleanOneOf = """
        {
          "oneOf": [
            {"type": "object", "required": ["strict"], "properties": {"strict": {"const": true}, "level": {"type": "integer"}}, "additionalProperties": false},
            {"type": "object", "required": ["strict"], "properties": {"strict": {"const": false}, "level": {"type": "string"}}, "additionalProperties": false}
          ]
        }
        """;

    [TestMethod]
    public void IntegerConstantsKeyTheDiscriminator()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(VersionedOneOf);
        Discriminator? d = evaluator.Program.Nodes[evaluator.RootNode].OneOfDiscriminator;
        Assert.IsNotNull(d, "A oneOf keyed on integer, enum and string constants gets a discriminator.");
        Assert.AreEqual(5, d.KnownValues.Count, "1, 2, 3, 4 and \"legacy\".");
        Assert.AreEqual(4, d.AllBranches.Length);

        Assert.IsTrue(evaluator.Evaluate("""{"version": 1, "a": "x"}"""));
        Assert.IsTrue(evaluator.Evaluate("""{"version": 2, "b": "x"}"""));
        Assert.IsTrue(evaluator.Evaluate("""{"version": 4, "c": "x"}"""));
        Assert.IsTrue(evaluator.Evaluate("""{"version": "legacy", "d": "x"}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"version": 1, "b": "x"}"""), "Branch 1 rejects b.");
        Assert.IsFalse(evaluator.Evaluate("""{"version": 5, "a": "x"}"""), "No branch keys 5.");
        Assert.IsFalse(evaluator.Evaluate("""{"version": "1", "a": "x"}"""), "The string \"1\" is not the integer 1.");
        Assert.IsFalse(evaluator.Evaluate("""{"a": "x"}"""), "Every branch requires the property.");
    }

    [TestMethod]
    public void NonCanonicalNumbersFallBackToEveryBranch()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(VersionedOneOf);
        Assert.IsTrue(evaluator.Evaluate("""{"version": 2.0, "b": "x"}"""), "2.0 equals the integer constant 2.");
        Assert.IsTrue(evaluator.Evaluate("""{"version": 1e0, "a": "x"}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"version": 2.5, "b": "x"}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"version": [2], "b": "x"}"""), "A structured value matches no constant.");
    }

    [TestMethod]
    public void BooleanConstantsKeyTheDiscriminator()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(BooleanOneOf);
        Assert.IsNotNull(evaluator.Program.Nodes[evaluator.RootNode].OneOfDiscriminator);
        Assert.IsTrue(evaluator.Evaluate("""{"strict": true, "level": 3}"""));
        Assert.IsTrue(evaluator.Evaluate("""{"strict": false, "level": "low"}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"strict": true, "level": "low"}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"strict": "true", "level": 3}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"strict": null, "level": 3}"""));
    }

    [TestMethod]
    public void KeysSurviveTheImage()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(VersionedOneOf);
        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(evaluator.ToProgramImage());
        Assert.IsNotNull(loaded.Program.Nodes[loaded.RootNode].OneOfDiscriminator);
        Assert.IsTrue(loaded.Evaluate("""{"version": 3, "c": "x"}"""));
        Assert.IsTrue(loaded.Evaluate("""{"version": 3.0, "c": "x"}"""));
        Assert.IsFalse(loaded.Evaluate("""{"version": 3, "a": "x"}"""));
    }
}