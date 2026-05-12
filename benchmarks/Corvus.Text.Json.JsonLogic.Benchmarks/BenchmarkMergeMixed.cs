// <copyright file="BenchmarkMergeMixed.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Benchmark for merging mixed constant and dynamic arrays:
/// {"merge":[[1,2],{"var":"arr1"},[5,6],{"var":"arr2"}]}.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkMergeMixed : JsonLogicBenchmarkBase
{
    private const string RuleJson = """{"merge":[[1,2],{"var":"arr1"},[5,6],{"var":"arr2"}]}""";
    private const string DataJson = """{"arr1":[3,4],"arr2":[7,8,9,10]}""";

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
        MergeMixedCodeGen.Evaluate(this.CorvusData, workspace);
    }
}