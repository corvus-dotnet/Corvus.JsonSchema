// <copyright file="BenchmarkSimpleVar.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

using CorvusJsonElement = Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Benchmark for simple variable access: {"var":"x"} with {"x": 1}.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkSimpleVar : JsonLogicBenchmarkBase
{
    private const string RuleJson = """{"var":"x"}""";
    private const string DataJson = """{"x":1}""";

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
