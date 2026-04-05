// <copyright file="BenchmarkComparison.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

using CorvusJsonElement = Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Benchmark for the "between" comparison pattern: {"&lt;":[1,{"var":"temp"},110]} with {"temp": 100}.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkComparison : JsonLogicBenchmarkBase
{
    private const string RuleJson = """{"<":[1,{"var":"temp"},110]}""";
    private const string DataJson = """{"temp":100}""";

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
    /// Evaluate using Corvus JsonLogic (bytecode VM).
    /// </summary>
    [Benchmark]
    public void CorvusJsonLogic()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonLogicEvaluator.Default.Evaluate(this.CorvusLogicRule, this.CorvusData, workspace);
    }

    /// <summary>
    /// Evaluate using Corvus JsonLogic (functional tree-walking).
    /// </summary>
    [Benchmark]
    public void CorvusFunctional()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonLogicEvaluator.Default.EvaluateFunctional(this.CorvusLogicRule, this.CorvusData, workspace);
    }
}
