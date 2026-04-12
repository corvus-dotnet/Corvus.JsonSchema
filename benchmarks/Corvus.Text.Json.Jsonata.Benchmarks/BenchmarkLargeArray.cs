// <copyright file="BenchmarkLargeArray.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
#if !NETFRAMEWORK
using Jsonata.Net.Native;
using Jsonata.Net.Native.Json;
#endif

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Benchmark for higher-order functions operating on a 10,000-item array.
/// Measures scaling behaviour and GC pressure for $map, $filter, and $reduce.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkLargeArray
{
    private const int ItemCount = 10_000;
    private const string ExprMap = "$map(items, function($v) { $v.value * 2 })";
    private const string ExprFilter = "$filter(items, function($v) { $v.value > 2500 })";
    private const string ExprReduce = "$reduce(items, function($prev, $curr) { $prev + $curr.value }, 0)";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeMap = null!;
    private JsonataQuery nativeFilter = null!;
    private JsonataQuery nativeReduce = null!;
    private JToken nativeData = null!;
#endif

    /// <summary>
    /// Global setup: generates a 10K-item array and pre-warms all evaluators.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        string dataJson = GenerateData(ItemCount);
        this.doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(dataJson));
        this.data = this.doc.RootElement;
        this.evaluator = new JsonataEvaluator();
        this.workspace = JsonWorkspace.Create();

        // Pre-warm the Corvus compilation cache
        this.evaluator.Evaluate(ExprMap, this.data);
        this.evaluator.Evaluate(ExprFilter, this.data);
        this.evaluator.Evaluate(ExprReduce, this.data);

        // Warm up source-generated evaluators
        LargeMapCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        LargeFilterCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        LargeReduceCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(dataJson);
        this.nativeMap = new JsonataQuery(ExprMap);
        this.nativeFilter = new JsonataQuery(ExprFilter);
        this.nativeReduce = new JsonataQuery(ExprReduce);
#endif
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.workspace.Dispose();
        this.doc?.Dispose();
    }

    /// <summary>
    /// Corvus: $map doubling each value in a 10K array.
    /// </summary>
    [BenchmarkCategory("Map")]
    [Benchmark]
    public JsonElement Corvus_Map()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprMap, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: $map doubling each value in a 10K array.
    /// </summary>
    [BenchmarkCategory("Map")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Map() =>
        this.nativeMap.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $map doubling each value in a 10K array.
    /// </summary>
    [BenchmarkCategory("Map")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Map()
    {
        this.workspace.Reset();
        return LargeMapCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// Corvus: $filter selecting values > 2500 from a 10K array.
    /// </summary>
    [BenchmarkCategory("Filter")]
    [Benchmark]
    public JsonElement Corvus_Filter()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprFilter, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: $filter selecting values > 2500 from a 10K array.
    /// </summary>
    [BenchmarkCategory("Filter")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Filter() =>
        this.nativeFilter.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $filter selecting values > 2500 from a 10K array.
    /// </summary>
    [BenchmarkCategory("Filter")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Filter()
    {
        this.workspace.Reset();
        return LargeFilterCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// Corvus: $reduce summing all values in a 10K array.
    /// </summary>
    [BenchmarkCategory("Reduce")]
    [Benchmark]
    public JsonElement Corvus_Reduce()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprReduce, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: $reduce summing all values in a 10K array.
    /// </summary>
    [BenchmarkCategory("Reduce")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Reduce() =>
        this.nativeReduce.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $reduce summing all values in a 10K array.
    /// </summary>
    [BenchmarkCategory("Reduce")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Reduce()
    {
        this.workspace.Reset();
        return LargeReduceCodeGen.Evaluate(this.data, this.workspace);
    }

    private static string GenerateData(int count)
    {
        StringBuilder sb = new(count * 30);
        sb.Append("{\"items\":[");
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("{\"id\":");
            sb.Append(i);
            sb.Append(",\"value\":");
            sb.Append(i * 0.5);
            sb.Append('}');
        }

        sb.Append("]}");
        return sb.ToString();
    }
}
