// <copyright file="BenchmarkConformanceField50.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Conformance benchmark: field chain 50 levels deep.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkConformanceField50 : JMESPathBenchmarkBase
{
    private const string ExpressionText = "j49.j48.j47.j46.j45.j44.j43.j42.j41.j40.j39.j38.j37.j36.j35.j34.j33.j32.j31.j30.j29.j28.j27.j26.j25.j24.j23.j22.j21.j20.j19.j18.j17.j16.j15.j14.j13.j12.j11.j10.j9.j8.j7.j6.j5.j4.j3.j2.j1.j0";
    private const string DataJson = """{}""";

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
        ConformanceField50CodeGen.Evaluate(this.CorvusData, workspace);
    }
}
