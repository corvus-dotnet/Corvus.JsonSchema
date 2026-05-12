// <copyright file="BenchmarkNestedFlatten.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Benchmark for nested double-flatten: departments[].teams[].members.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkNestedFlatten : JMESPathBenchmarkBase
{
    private const string ExpressionText = "departments[].teams[].members";
    private const string DataJson = """{"departments":[{"teams":[{"members":["Alice","Bob"]},{"members":["Carol"]}]},{"teams":[{"members":["Dave","Eve","Frank"]},{"members":["Grace"]}]}]}""";

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => this.Setup(ExpressionText, DataJson);

    /// <summary>
    /// Evaluate using JmesPath.Net.
    /// </summary>
    [Benchmark(Baseline = true)]
    public string JmesPathNet()
    {
        return this.JmesPath.Transform(this.DataJsonString, this.Expression);
    }

    /// <summary>
    /// Evaluate using Corvus JMESPath runtime.
    /// </summary>
    [Benchmark]
    public void CorvusJMESPath()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JMESPathEvaluator.Default.Search(this.Expression, this.CorvusData, workspace);
    }

    /// <summary>
    /// Evaluate using Corvus code-generated evaluator.
    /// </summary>
    [Benchmark]
    public void CorvusCodeGen()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        NestedFlattenCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
