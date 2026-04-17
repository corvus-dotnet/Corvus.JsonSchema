// <copyright file="BenchmarkFlatten.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Benchmark for flatten projection: reservations[*].instances[*][].id.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkFlatten : JMESPathBenchmarkBase
{
    private const string ExpressionText = "reservations[*].instances[*][].id";
    private const string DataJson = """{"reservations":[{"instances":[{"id":"i-1"},{"id":"i-2"}]},{"instances":[{"id":"i-3"},{"id":"i-4"}]},{"instances":[{"id":"i-5"}]}]}""";

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
        FlattenCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
