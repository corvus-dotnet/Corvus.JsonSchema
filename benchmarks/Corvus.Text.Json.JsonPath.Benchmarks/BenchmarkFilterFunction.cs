// <copyright file="BenchmarkFilterFunction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Json.Path;

namespace Corvus.Text.Json.JsonPath.Benchmarks;

/// <summary>
/// Benchmark for filter with function: $.store.book[?length(@.title) &gt; 10].
/// JsonCons does not support RFC 9535 function extensions, so only JsonEverything and Corvus RT/CG are compared.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkFilterFunction : JsonPathBenchmarkBase
{
    private const string ExpressionText = "$.store.book[?length(@.title) > 10]";

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => this.Setup(ExpressionText, BookstoreJson);

    /// <summary>
    /// Evaluate using JsonPath.Net (json-everything).
    /// </summary>
    [Benchmark(Baseline = true)]
    public PathResult JsonEverything()
    {
        return this.JsonEverythingPath.Evaluate(this.JsonEverythingNode);
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
        using JsonPathResult result = FilterFunctionCodeGen.QueryNodes(this.CorvusData);
        return result.Count;
    }
}
