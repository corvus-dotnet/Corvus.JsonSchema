// <copyright file="BenchmarkConformanceMaxBy.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Conformance benchmark: max_by over array of objects.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkConformanceMaxBy : JMESPathBenchmarkBase
{
    private const string ExpressionText = "max_by(items, &val)";
    private const string DataJson = """{"items":[{"name":"a","val":25},{"name":"b","val":0},{"name":"c","val":12},{"name":"d","val":7},{"name":"e","val":19},{"name":"f","val":3},{"name":"g","val":15},{"name":"h","val":21},{"name":"i","val":9},{"name":"j","val":1},{"name":"k","val":24},{"name":"l","val":6},{"name":"m","val":18},{"name":"n","val":11},{"name":"o","val":4},{"name":"p","val":22},{"name":"q","val":8},{"name":"r","val":16},{"name":"s","val":2},{"name":"t","val":20},{"name":"u","val":14},{"name":"v","val":10},{"name":"w","val":23},{"name":"x","val":5},{"name":"y","val":17},{"name":"z","val":13}]}""";

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
        ConformanceMaxByCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
