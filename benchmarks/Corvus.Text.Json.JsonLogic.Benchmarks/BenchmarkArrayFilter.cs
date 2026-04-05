// <copyright file="BenchmarkArrayFilter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

using CorvusJsonElement = Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Benchmark for array filtering: {"filter":[{"var":"items"},{">":[{"var":""},5]}]}.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkArrayFilter : JsonLogicBenchmarkBase
{
    private const string RuleJson = """{"filter":[{"var":"items"},{">":[{"var":""},5]}]}""";
    private const string DataJson = """{"items":[1,2,3,4,5,6,7,8,9,10]}""";

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
