// <copyright file="BenchmarkArraySlice.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Json.Path;

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
    /// Evaluate using JsonPath.Net (json-everything).
    /// </summary>
    [Benchmark]
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
        using JsonPathResult result = ArraySliceCodeGen.QueryNodes(this.CorvusData);
        return result.Count;
    }
}
