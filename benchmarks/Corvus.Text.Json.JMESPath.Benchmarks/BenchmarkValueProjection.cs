// <copyright file="BenchmarkValueProjection.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Benchmark for value (object wildcard) projection: ops.*.numArgs.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkValueProjection : JMESPathBenchmarkBase
{
    private const string ExpressionText = "ops.*.numArgs";
    private const string DataJson = """{"ops":{"add":{"numArgs":2},"sub":{"numArgs":2},"sqrt":{"numArgs":1},"abs":{"numArgs":1},"mod":{"numArgs":2}}}""";

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
        ValueProjectionCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
