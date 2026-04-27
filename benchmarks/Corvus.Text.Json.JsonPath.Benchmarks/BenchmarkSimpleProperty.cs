// <copyright file="BenchmarkSimpleProperty.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using JsonCons.JsonPath;

namespace Corvus.Text.Json.JsonPath.Benchmarks;

/// <summary>
/// Benchmark for simple property access: $.store.book[0].title.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkSimpleProperty : JsonPathBenchmarkBase
{
    private const string ExpressionText = "$.store.book[0].title";

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
        SimplePropertyCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
