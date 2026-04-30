// <copyright file="BenchmarkConformanceIndex50.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Conformance benchmark: index chain 50 levels deep.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkConformanceIndex50 : JMESPathBenchmarkBase
{
    private const string ExpressionText = "[49][48][47][46][45][44][43][42][41][40][39][38][37][36][35][34][33][32][31][30][29][28][27][26][25][24][23][22][21][20][19][18][17][16][15][14][13][12][11][10][9][8][7][6][5][4][3][2][1][0]";
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
        ConformanceIndex50CodeGen.Evaluate(this.CorvusData, workspace);
    }
}
