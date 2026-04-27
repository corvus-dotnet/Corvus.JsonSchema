// <copyright file="BenchmarkEndToEnd.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Json.Path;

using CorvusJsonElement = Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json.JsonPath.Benchmarks;

/// <summary>
/// End-to-end benchmark including JSON parsing + query evaluation.
/// This gives a fairer comparison because JE's JsonNode.Parse builds
/// dictionaries and pre-parses numbers at parse time, while Corvus
/// defers that work to query time.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
public class BenchmarkEndToEnd : JsonPathBenchmarkBase
{
    private static readonly Dictionary<string, string> Expressions = new()
    {
        ["SimpleProperty"] = "$.store.book[0].title",
        ["ArraySlice"] = "$.store.book[0:2]",
        ["Wildcard"] = "$.store.book[*].author",
        ["Filter"] = "$.store.book[?@.price < 10]",
        ["FilterFunction"] = "$.store.book[?length(@.title) > 10]",
        ["RecursiveDescent"] = "$..author",
    };

    /// <summary>
    /// Gets or sets the benchmark scenario.
    /// </summary>
    [Params("SimpleProperty", "ArraySlice", "Wildcard", "Filter", "FilterFunction", "RecursiveDescent")]
    public string Scenario { get; set; } = string.Empty;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.Setup(Expressions[this.Scenario], BookstoreJson);
    }

    /// <summary>
    /// JsonPath.Net (json-everything): parse JsonNode + evaluate.
    /// </summary>
    [Benchmark(Baseline = true)]
    public PathResult JE()
    {
        JsonNode node = JsonNode.Parse(this.DataJsonString)!;
        return this.JsonEverythingPath.Evaluate(node);
    }

    /// <summary>
    /// Corvus runtime interpreter: parse document + evaluate + dispose.
    /// </summary>
    [Benchmark]
    public int Corvus_RT()
    {
        using ParsedJsonDocument<CorvusJsonElement> doc = ParsedJsonDocument<CorvusJsonElement>.Parse(this.DataJsonString);
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(this.Expression, doc.RootElement);
        return result.Count;
    }

    /// <summary>
    /// Corvus code-generated evaluator: parse document + evaluate + dispose.
    /// </summary>
    [Benchmark]
    public int Corvus_CG()
    {
        using ParsedJsonDocument<CorvusJsonElement> doc = ParsedJsonDocument<CorvusJsonElement>.Parse(this.DataJsonString);
        CorvusJsonElement root = doc.RootElement;
        int count = this.Scenario switch
        {
            "SimpleProperty" => Count(SimplePropertyCodeGen.QueryNodes(in root)),
            "ArraySlice" => Count(ArraySliceCodeGen.QueryNodes(in root)),
            "Wildcard" => Count(WildcardCodeGen.QueryNodes(in root)),
            "Filter" => Count(FilterCodeGen.QueryNodes(in root)),
            "FilterFunction" => Count(FilterFunctionCodeGen.QueryNodes(in root)),
            "RecursiveDescent" => Count(RecursiveDescentCodeGen.QueryNodes(in root)),
            _ => throw new InvalidOperationException(),
        };

        return count;

        static int Count(JsonPathResult r) { int c = r.Count; r.Dispose(); return c; }
    }
}
