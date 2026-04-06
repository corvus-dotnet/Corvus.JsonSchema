// <copyright file="BenchmarkMissingData.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

using CorvusJsonElement = Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Benchmark for the missing/missing_some operators with default values.
/// Tests data access patterns with partial data.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkMissingData : JsonLogicBenchmarkBase
{
    private const string RuleJson =
        """{"if":[{"missing":["a","b"]},{"cat":["Missing: ",{"missing":["a","b"]}]},{"+":[{"var":"a"},{"var":"b"}]}]}""";

    private const string DataJson = """{"a":10,"b":20,"c":30}""";

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
        MissingDataCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
