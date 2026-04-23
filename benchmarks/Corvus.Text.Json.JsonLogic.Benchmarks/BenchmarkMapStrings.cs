// <copyright file="BenchmarkMapStrings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Benchmark for mapping objects to string fields:
/// {"map":[{"var":"people"},{"var":"name"}]}.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkMapStrings : JsonLogicBenchmarkBase
{
    private const string RuleJson = """{"map":[{"var":"people"},{"var":"name"}]}""";
    private const string DataJson = """{"people":[{"name":"Alice","age":25},{"name":"Bob","age":35},{"name":"Carol","age":28},{"name":"Dave","age":42},{"name":"Eve","age":31},{"name":"Frank","age":19},{"name":"Grace","age":55},{"name":"Hank","age":30},{"name":"Ivy","age":38},{"name":"Jack","age":22}]}""";

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
        MapStringsCodeGen.Evaluate(this.CorvusData, workspace);
    }
}