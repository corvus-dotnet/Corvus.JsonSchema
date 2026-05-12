// <copyright file="BenchmarkSlice.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Benchmark for array slicing: people[0:3].
/// </summary>
[MemoryDiagnoser]
public class BenchmarkSlice : JMESPathBenchmarkBase
{
    private const string ExpressionText = "people[0:3]";
    private const string DataJson = """{"people":[{"name":"Alice","age":30},{"name":"Bob","age":25},{"name":"Charlie","age":35},{"name":"Diana","age":28},{"name":"Eve","age":22}]}""";

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
        SliceCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
