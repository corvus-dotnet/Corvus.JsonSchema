// <copyright file="BenchmarkLogicShortCircuit.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Benchmark for short-circuit logic with nested comparisons.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkLogicShortCircuit : JsonLogicBenchmarkBase
{
    private const string RuleJson =
        """{"and":[{">":[{"var":"temp"},0]},{"<":[{"var":"temp"},110]},{"==":[{"var":"pie.filling"},"apple"]}]}""";

    private const string DataJson =
        """{"temp":100,"pie":{"filling":"apple"}}""";

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
    /// Evaluate using Corvus code-generated evaluator.
    /// </summary>
    [Benchmark]
    public void CorvusCodeGen()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        LogicShortCircuitCodeGen.Evaluate(this.CorvusData, workspace);
    }
}