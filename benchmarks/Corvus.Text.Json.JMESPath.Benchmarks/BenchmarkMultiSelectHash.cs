// <copyright file="BenchmarkMultiSelectHash.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Benchmark for multi-select hash: {name: name, years: age}.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkMultiSelectHash : JMESPathBenchmarkBase
{
    private const string ExpressionText = "{name: name, years: age}";
    private const string DataJson = """{"name":"Alice","age":30,"email":"alice@example.com"}""";

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
        MultiSelectHashCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
