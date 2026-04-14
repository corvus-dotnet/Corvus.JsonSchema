// <copyright file="BenchmarkHigherOrder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
#if !NETFRAMEWORK
using Jsonata.Net.Native;
using Jsonata.Net.Native.Json;
#endif

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Head-to-head benchmark for higher-order functions ($map, $filter, $reduce, $sort).
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkHigherOrder : JsonataBenchmarkBase
{
    private const string DataJson = """
        {
            "Account": {
                "Order": [
                    {
                        "Product": [
                            {"Product Name": "Bowler Hat", "Price": 34.45, "Quantity": 2},
                            {"Product Name": "Trilby hat", "Price": 21.67, "Quantity": 1}
                        ]
                    },
                    {
                        "Product": [
                            {"Product Name": "Bowler Hat", "Price": 34.45, "Quantity": 4},
                            {"Product Name": "Cloak", "Price": 107.99, "Quantity": 1}
                        ]
                    }
                ]
            }
        }
        """;

    private const string ExprMap = "$map(Account.Order.Product, function($v) { $v.`Product Name` })";
    private const string ExprFilter = "$filter(Account.Order.Product, function($v) { $v.Price > 30 })";
    private const string ExprReduce = "$reduce(Account.Order.Product, function($prev, $curr) { $prev + $curr.Price * $curr.Quantity }, 0)";
    private const string ExprSort = "$sort(Account.Order.Product, function($a, $b) { $a.Price > $b.Price })";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeMap = null!;
    private JsonataQuery nativeFilter = null!;
    private JsonataQuery nativeReduce = null!;
    private JsonataQuery nativeSort = null!;
    private JToken nativeData = null!;
#endif

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(DataJson));
        this.data = this.doc.RootElement;
        this.evaluator = new JsonataEvaluator();
        this.workspace = JsonWorkspace.Create();
        this.evaluator.Evaluate(ExprMap, this.data);
        this.evaluator.Evaluate(ExprFilter, this.data);
        this.evaluator.Evaluate(ExprReduce, this.data);
        this.evaluator.Evaluate(ExprSort, this.data);

        // Warm up source-generated evaluators (first call compiles + caches)
        MapHofCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        FilterHofCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        ReduceHofCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        SortHofCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeMap = new JsonataQuery(ExprMap);
        this.nativeFilter = new JsonataQuery(ExprFilter);
        this.nativeReduce = new JsonataQuery(ExprReduce);
        this.nativeSort = new JsonataQuery(ExprSort);
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
    /// Corvus: $map extracting product names.
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
    /// Jsonata.Net.Native (reference impl): $map extracting product names.
    /// </summary>
    [BenchmarkCategory("Map")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Map() =>
        this.nativeMap.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: $filter with price predicate.
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
    /// Jsonata.Net.Native (reference impl): $filter with price predicate.
    /// </summary>
    [BenchmarkCategory("Filter")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Filter() =>
        this.nativeFilter.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: $reduce with accumulator.
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
    /// Jsonata.Net.Native (reference impl): $reduce with accumulator.
    /// </summary>
    [BenchmarkCategory("Reduce")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Reduce() =>
        this.nativeReduce.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: $sort with comparator.
    /// </summary>
    [BenchmarkCategory("Sort")]
    [Benchmark]
    public JsonElement Corvus_Sort()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSort, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): $sort with comparator.
    /// </summary>
    [BenchmarkCategory("Sort")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Sort() =>
        this.nativeSort.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $map extracting product names.
    /// </summary>
    [BenchmarkCategory("Map")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Map()
    {
        this.workspace.Reset();
        return MapHofCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// CodeGen: $filter with price predicate.
    /// </summary>
    [BenchmarkCategory("Filter")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Filter()
    {
        this.workspace.Reset();
        return FilterHofCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// CodeGen: $reduce with accumulator.
    /// </summary>
    [BenchmarkCategory("Reduce")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Reduce()
    {
        this.workspace.Reset();
        return ReduceHofCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// CodeGen: $sort with comparator.
    /// </summary>
    [BenchmarkCategory("Sort")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Sort()
    {
        this.workspace.Reset();
        return SortHofCodeGen.Evaluate(this.data, this.workspace);
    }
}