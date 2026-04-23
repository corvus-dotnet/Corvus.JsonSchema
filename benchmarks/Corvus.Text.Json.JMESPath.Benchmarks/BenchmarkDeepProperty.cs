// <copyright file="BenchmarkDeepProperty.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Benchmark for deep property chain: a.b.c.d.e.f.g.h.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkDeepProperty : JMESPathBenchmarkBase
{
    private const string ExpressionText = "a.b.c.d.e.f.g.h";
    private const string DataJson = """{"a":{"b":{"c":{"d":{"e":{"f":{"g":{"h":"deep"}}}}}}}}""";

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
        DeepPropertyCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
