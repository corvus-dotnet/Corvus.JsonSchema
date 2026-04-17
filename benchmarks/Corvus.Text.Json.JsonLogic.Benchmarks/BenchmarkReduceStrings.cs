// <copyright file="BenchmarkReduceStrings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Benchmark for reduce accumulating strings (join names with commas).
/// </summary>
[MemoryDiagnoser]
public class BenchmarkReduceStrings : JsonLogicBenchmarkBase
{
    private const string RuleJson = """{"reduce":[{"var":"people"},{"cat":[{"var":"accumulator"},{"if":[{"var":"accumulator"},", ",""]},{"var":"current.name"}]},""]}""";
    private const string DataJson = """{"people":[{"name":"Alice","age":30},{"name":"Bob","age":25},{"name":"Charlie","age":35},{"name":"Diana","age":28},{"name":"Eve","age":32}]}""";

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
        ReduceStringsCodeGen.Evaluate(this.CorvusData, workspace);
    }
}