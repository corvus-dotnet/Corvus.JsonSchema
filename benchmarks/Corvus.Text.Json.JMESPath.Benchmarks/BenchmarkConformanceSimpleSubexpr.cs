// <copyright file="BenchmarkConformanceSimpleSubexpr.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Conformance benchmark: simple subexpression.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkConformanceSimpleSubexpr : JMESPathBenchmarkBase
{
    private const string ExpressionText = "c.d";
    private const string DataJson = """{"long_name_for_a_field":true,"a":{"b":{"c":{"d":{"e":{"f":{"g":{"h":{"i":{"j":{"k":{"l":{"m":{"n":{"o":{"p":true}}}}}}}}}}}}}}},"b":true,"c":{"d":true}}""";

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
        ConformanceSimpleSubexprCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
