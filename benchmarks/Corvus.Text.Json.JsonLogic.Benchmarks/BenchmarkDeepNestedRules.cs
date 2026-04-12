// <copyright file="BenchmarkDeepNestedRules.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

using BenchmarkDotNet.Attributes;

using CorvusJsonElement = Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Benchmark for deeply nested rule evaluation. Builds an if/else chain of
/// configurable depth to measure the impact of rule tree depth on performance.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkDeepNestedRules : JsonLogicBenchmarkBase
{
    private const int Depth = 50;

    private static readonly string RuleJson = BuildDeepIfElse(Depth);
    private const string DataJson = """{"x":25}""";

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => this.Setup(RuleJson, DataJson);

    /// <summary>
    /// Evaluate using JsonEverything.
    /// </summary>
    [Benchmark(Baseline = true)]
    public System.Text.Json.Nodes.JsonNode? JsonEverything()
    {
        return global::Json.Logic.JsonLogic.Apply(this.JeRule, this.JeData);
    }

    /// <summary>
    /// Evaluate using Corvus JsonLogic.
    /// </summary>
    [Benchmark]
    public void CorvusJsonLogic()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonLogicEvaluator.Default.Evaluate(this.CorvusLogicRule, this.CorvusData, workspace);
    }

    /// <summary>
    /// Evaluate using hand-written code-generated evaluator for the 50-deep if/else chain.
    /// </summary>
    [Benchmark]
    public void CorvusCodeGen()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        DeepNestedRulesCodeGen.Evaluate(this.CorvusData, workspace);
    }

    /// <summary>
    /// Builds a deeply nested if/else chain:
    /// if x > (depth-1) then depth, else if x > (depth-2) then (depth-1), ... else 0.
    /// The "x":25 data will match near the middle of the chain.
    /// </summary>
    private static string BuildDeepIfElse(int depth)
    {
        StringBuilder sb = new();
        for (int i = depth; i > 0; i--)
        {
            sb.Append("{\"if\":[{\">\": [{\"var\": \"x\"}, ");
            sb.Append(i - 1);
            sb.Append("]}, ");
            sb.Append(i);
            sb.Append(", ");
        }

        sb.Append('0');

        for (int i = 0; i < depth; i++)
        {
            sb.Append("]}");
        }

        return sb.ToString();
    }
}
