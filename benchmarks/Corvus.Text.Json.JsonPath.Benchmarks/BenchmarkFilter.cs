// <copyright file="BenchmarkFilter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.JsonPath.Benchmarks;

/// <summary>
/// Benchmark for filter: $.store.book[?@.price &lt; 10].
/// </summary>
[MemoryDiagnoser]
public class BenchmarkFilter : JsonPathBenchmarkBase
{
    private const string ExpressionText = "$.store.book[?@.price < 10]";
    private const string JsonConsExpressionText = "$.store.book[?(@.price < 10)]";

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => this.Setup(ExpressionText, BookstoreJson, JsonConsExpressionText);

    /// <summary>
    /// Evaluate using JsonCons.JsonPath.
    /// </summary>
    [Benchmark(Baseline = true)]
    public IList<System.Text.Json.JsonElement> JsonCons()
    {
        return this.JsonConsSelector.Select(this.JsonConsDocument.RootElement);
    }

    /// <summary>
    /// Evaluate using Corvus JSONPath runtime (zero-alloc node query).
    /// </summary>
    [Benchmark]
    public int Corvus_RT()
    {
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(this.Expression, this.CorvusData);
        return result.Count;
    }

    /// <summary>
    /// Evaluate using Corvus code-generated evaluator.
    /// </summary>
    [Benchmark]
    public int Corvus_CG()
    {
        using JsonPathResult result = FilterCodeGen.QueryNodes(this.CorvusData);
        return result.Count;
    }
}
