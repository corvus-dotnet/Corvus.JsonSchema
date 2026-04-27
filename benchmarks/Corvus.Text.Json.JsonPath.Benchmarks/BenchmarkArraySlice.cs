// <copyright file="BenchmarkArraySlice.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.JsonPath.Benchmarks;

/// <summary>
/// Benchmark for array slice: $.store.book[0:2].
/// </summary>
[MemoryDiagnoser]
public class BenchmarkArraySlice : JsonPathBenchmarkBase
{
    private const string ExpressionText = "$.store.book[0:2]";

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => this.Setup(ExpressionText, BookstoreJson);

    /// <summary>
    /// Evaluate using JsonCons.JsonPath.
    /// </summary>
    [Benchmark(Baseline = true)]
    public IList<System.Text.Json.JsonElement> JsonCons()
    {
        return this.JsonConsSelector.Select(this.JsonConsDocument.RootElement);
    }

    /// <summary>
    /// Evaluate using Corvus JSONPath runtime.
    /// </summary>
    [Benchmark]
    public void Corvus_RT()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonPathEvaluator.Default.Query(this.Expression, this.CorvusData, workspace);
    }

    /// <summary>
    /// Evaluate using Corvus code-generated evaluator.
    /// </summary>
    [Benchmark]
    public void Corvus_CG()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        ArraySliceCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
