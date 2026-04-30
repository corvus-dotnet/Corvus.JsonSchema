// <copyright file="CustomOperatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Tests for the <see cref="IOperatorCompiler"/> extensibility mechanism.
/// </summary>
public class CustomOperatorTests
{
    [Fact]
    public void CustomOperator_DoubleIt_EvaluatesCorrectly()
    {
        // Register a custom "double_it" operator that doubles a number
        var customOps = new Dictionary<string, IOperatorCompiler>
        {
            ["double_it"] = new DoubleItCompiler(),
        };

        JsonLogicEvaluator evaluator = new(customOps);

        using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse("""{"double_it":[{"var":"x"}]}""");
        using var dataDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x":5}""");

        JsonLogicRule rule = new(ruleDoc.RootElement);
        JsonElement result = evaluator.Evaluate(rule, dataDoc.RootElement);

        Assert.Equal(JsonValueKind.Number, result.ValueKind);
        Assert.Equal(10.0, result.GetDouble());
    }

    [Fact]
    public void CustomOperator_OverridesBuiltIn()
    {
        // Override the built-in "+" operator with one that always returns 42
        var customOps = new Dictionary<string, IOperatorCompiler>
        {
            ["+"] = new AlwaysFortyTwoCompiler(),
        };

        JsonLogicEvaluator evaluator = new(customOps);

        using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse("""{"+":[1,2,3]}""");
        using var dataDoc = ParsedJsonDocument<JsonElement>.Parse("{}");

        JsonLogicRule rule = new(ruleDoc.RootElement);
        JsonElement result = evaluator.Evaluate(rule, dataDoc.RootElement);

        Assert.Equal(42.0, result.GetDouble());
    }

    [Fact]
    public void CustomOperator_WorksInsideBuiltInOps()
    {
        // Use a custom operator as a sub-expression inside a standard operator
        var customOps = new Dictionary<string, IOperatorCompiler>
        {
            ["double_it"] = new DoubleItCompiler(),
        };

        JsonLogicEvaluator evaluator = new(customOps);

        // {"+":[{"double_it":[{"var":"x"}]}, {"var":"y"}]} = double(3) + 4 = 6 + 4 = 10
        using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse(
            """{"+":[{"double_it":[{"var":"x"}]}, {"var":"y"}]}""");
        using var dataDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x":3,"y":4}""");

        JsonLogicRule rule = new(ruleDoc.RootElement);
        JsonElement result = evaluator.Evaluate(rule, dataDoc.RootElement);

        Assert.Equal(10.0, result.GetDouble());
    }

    [Fact]
    public void CustomOperator_WithWorkspace()
    {
        var customOps = new Dictionary<string, IOperatorCompiler>
        {
            ["double_it"] = new DoubleItCompiler(),
        };

        JsonLogicEvaluator evaluator = new(customOps);

        using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse("""{"double_it":[{"var":"x"}]}""");
        using var dataDoc = ParsedJsonDocument<JsonElement>.Parse("""{"x":7}""");
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonLogicRule rule = new(ruleDoc.RootElement);
        JsonElement result = evaluator.Evaluate(rule, dataDoc.RootElement, workspace);

        Assert.Equal(14.0, result.GetDouble());
    }

    [Fact]
    public void CustomOperator_CachingWorks()
    {
        var customOps = new Dictionary<string, IOperatorCompiler>
        {
            ["double_it"] = new DoubleItCompiler(),
        };

        JsonLogicEvaluator evaluator = new(customOps);

        using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse("""{"double_it":[{"var":"x"}]}""");
        JsonLogicRule rule = new(ruleDoc.RootElement);

        // First evaluation
        using var data1 = ParsedJsonDocument<JsonElement>.Parse("""{"x":3}""");
        JsonElement result1 = evaluator.Evaluate(rule, data1.RootElement);
        Assert.Equal(6.0, result1.GetDouble());

        // Second evaluation with same rule, different data (should use cache)
        using var data2 = ParsedJsonDocument<JsonElement>.Parse("""{"x":10}""");
        JsonElement result2 = evaluator.Evaluate(rule, data2.RootElement);
        Assert.Equal(20.0, result2.GetDouble());
    }

    [Fact]
    public void CustomOperator_ZeroArguments()
    {
        var customOps = new Dictionary<string, IOperatorCompiler>
        {
            ["pi"] = new PiCompiler(),
        };

        JsonLogicEvaluator evaluator = new(customOps);

        using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse("""{"pi":[]}""");
        using var dataDoc = ParsedJsonDocument<JsonElement>.Parse("{}");

        JsonLogicRule rule = new(ruleDoc.RootElement);
        JsonElement result = evaluator.Evaluate(rule, dataDoc.RootElement);

        Assert.Equal(JsonValueKind.Number, result.ValueKind);
        Assert.Equal(3.14159, result.GetDouble(), 5);
    }

    [Fact]
    public void DefaultEvaluator_StillWorksWithoutCustomOps()
    {
        using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse("""{"+":[1,2,3]}""");
        using var dataDoc = ParsedJsonDocument<JsonElement>.Parse("{}");

        JsonLogicRule rule = new(ruleDoc.RootElement);
        JsonElement result = JsonLogicEvaluator.Default.Evaluate(rule, dataDoc.RootElement);

        Assert.Equal("6", result.GetRawText());
    }

    // ─── Test IOperatorCompiler implementations ─────────────────

    private sealed class DoubleItCompiler : IOperatorCompiler
    {
        public RuleEvaluator Compile(RuleEvaluator[] operands)
        {
            RuleEvaluator operand = operands[0];
            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                EvalResult val = operand(data, workspace);
                if (val.TryGetDouble(out double d))
                {
                    return EvalResult.FromDouble(d * 2);
                }

                return EvalResult.FromDouble(0);
            };
        }
    }

    private sealed class AlwaysFortyTwoCompiler : IOperatorCompiler
    {
        public RuleEvaluator Compile(RuleEvaluator[] operands)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromDouble(42);
        }
    }

    private sealed class PiCompiler : IOperatorCompiler
    {
        public RuleEvaluator Compile(RuleEvaluator[] operands)
        {
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromDouble(3.14159);
        }
    }
}