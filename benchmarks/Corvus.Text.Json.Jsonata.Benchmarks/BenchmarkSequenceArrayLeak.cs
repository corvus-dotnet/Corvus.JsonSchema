// <copyright file="BenchmarkSequenceArrayLeak.cs" company="Endjin Limited">
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
/// Benchmark that exercises Sequence backing-array return paths in the
/// predicate property-chain evaluator. Three scenarios target distinct
/// code paths inside <c>CollectAndContinue</c> and <c>FilterAndContinue</c>.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkSequenceArrayLeak
{
    private const string ExprIndexed = "items.orders[0].products.name";
    private const string ExprFiltered = """items[type="a"].products.name""";
    private const string ExprContinuation = "root.groups[0].categories.items.products.name";

    private static readonly byte[] ExprIndexedUtf8 = "items.orders[0].products.name"u8.ToArray();
    private static readonly byte[] ExprFilteredUtf8 = """items[type="a"].products.name"""u8.ToArray();
    private static readonly byte[] ExprContinuationUtf8 = "root.groups[0].categories.items.products.name"u8.ToArray();

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? docIndexed;
    private ParsedJsonDocument<JsonElement>? docFiltered;
    private ParsedJsonDocument<JsonElement>? docContinuation;
    private JsonElement dataIndexed;
    private JsonElement dataFiltered;
    private JsonElement dataContinuation;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeIndexed = null!;
    private JsonataQuery nativeFiltered = null!;
    private JsonataQuery nativeContinuation = null!;
    private JToken nativeDataIndexed = null!;
    private JToken nativeDataFiltered = null!;
    private JToken nativeDataContinuation = null!;
#endif

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.evaluator = new JsonataEvaluator();
        this.workspace = JsonWorkspace.Create();

        string indexedJson = BuildIndexedData(itemCount: 20, productsPerOrder: 5);
        string filteredJson = BuildFilteredData(matchCount: 20, productsPerItem: 5);
        string continuationJson = BuildContinuationData(categoryCount: 5, itemsPerCategory: 4, productsPerItem: 5);

        this.docIndexed = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(indexedJson));
        this.dataIndexed = this.docIndexed.RootElement;

        this.docFiltered = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(filteredJson));
        this.dataFiltered = this.docFiltered.RootElement;

        this.docContinuation = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(continuationJson));
        this.dataContinuation = this.docContinuation.RootElement;

        // Pre-warm RT expressions
        this.evaluator.Evaluate(ExprIndexedUtf8, this.dataIndexed, this.workspace, cacheKey: ExprIndexed);
        this.workspace.Reset();
        this.evaluator.Evaluate(ExprFilteredUtf8, this.dataFiltered, this.workspace, cacheKey: ExprFiltered);
        this.workspace.Reset();
        this.evaluator.Evaluate(ExprContinuationUtf8, this.dataContinuation, this.workspace, cacheKey: ExprContinuation);
        this.workspace.Reset();

        // Pre-warm CG expressions (some patterns may not be supported by CG yet)
        try { IndexedChainCodeGen.Evaluate(this.dataIndexed, this.workspace); this.workspace.Reset(); }
        catch { this.workspace.Reset(); }

        try { FilteredChainCodeGen.Evaluate(this.dataFiltered, this.workspace); this.workspace.Reset(); }
        catch { this.workspace.Reset(); }

        try { ContinuationChainCodeGen.Evaluate(this.dataContinuation, this.workspace); this.workspace.Reset(); }
        catch { this.workspace.Reset(); }

#if !NETFRAMEWORK
        this.nativeDataIndexed = JToken.Parse(indexedJson);
        this.nativeDataFiltered = JToken.Parse(filteredJson);
        this.nativeDataContinuation = JToken.Parse(continuationJson);
        this.nativeIndexed = new JsonataQuery(ExprIndexed);
        this.nativeFiltered = new JsonataQuery(ExprFiltered);
        this.nativeContinuation = new JsonataQuery(ExprContinuation);
#endif
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.workspace.Dispose();
        this.docIndexed?.Dispose();
        this.docFiltered?.Dispose();
        this.docContinuation?.Dispose();
    }

    // ── IndexedChain ────────────────────────────────────────────────

    /// <summary>
    /// RT: <c>items.orders[0].products.name</c>.
    /// </summary>
    [BenchmarkCategory("IndexedChain")]
    [Benchmark]
    public JsonElement Corvus_IndexedChain()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprIndexedUtf8, this.dataIndexed, this.workspace, cacheKey: ExprIndexed);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: <c>items.orders[0].products.name</c>.
    /// </summary>
    [BenchmarkCategory("IndexedChain")]
    [Benchmark(Baseline = true)]
    public JToken Native_IndexedChain() =>
        this.nativeIndexed.Eval(this.nativeDataIndexed);
#endif

    /// <summary>
    /// CodeGen: <c>items.orders[0].products.name</c>.
    /// </summary>
    [BenchmarkCategory("IndexedChain")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_IndexedChain()
    {
        this.workspace.Reset();
        return IndexedChainCodeGen.Evaluate(this.dataIndexed, this.workspace);
    }

    // ── FilteredChain ───────────────────────────────────────────────

    /// <summary>
    /// RT: <c>items[type="a"].products.name</c>.
    /// </summary>
    [BenchmarkCategory("FilteredChain")]
    [Benchmark]
    public JsonElement Corvus_FilteredChain()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprFilteredUtf8, this.dataFiltered, this.workspace, cacheKey: ExprFiltered);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: <c>items[type="a"].products.name</c>.
    /// </summary>
    [BenchmarkCategory("FilteredChain")]
    [Benchmark(Baseline = true)]
    public JToken Native_FilteredChain() =>
        this.nativeFiltered.Eval(this.nativeDataFiltered);
#endif

    /// <summary>
    /// CodeGen: <c>items[type="a"].products.name</c>.
    /// </summary>
    [BenchmarkCategory("FilteredChain")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_FilteredChain()
    {
        this.workspace.Reset();
        return FilteredChainCodeGen.Evaluate(this.dataFiltered, this.workspace);
    }

    // ── ContinuationChain ───────────────────────────────────────────

    /// <summary>
    /// RT: <c>root.groups[0].categories.items.products.name</c>.
    /// </summary>
    [BenchmarkCategory("ContinuationChain")]
    [Benchmark]
    public JsonElement Corvus_ContinuationChain()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprContinuationUtf8, this.dataContinuation, this.workspace, cacheKey: ExprContinuation);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: <c>root.groups[0].categories.items.products.name</c>.
    /// </summary>
    [BenchmarkCategory("ContinuationChain")]
    [Benchmark(Baseline = true)]
    public JToken Native_ContinuationChain() =>
        this.nativeContinuation.Eval(this.nativeDataContinuation);
#endif

    /// <summary>
    /// CodeGen: <c>root.groups[0].categories.items.products.name</c>.
    /// </summary>
    [BenchmarkCategory("ContinuationChain")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_ContinuationChain()
    {
        this.workspace.Reset();
        return ContinuationChainCodeGen.Evaluate(this.dataContinuation, this.workspace);
    }

    private static string BuildIndexedData(int itemCount, int productsPerOrder)
    {
        // { "items": [ { "orders": [ { "products": [ { "name": "p0" }, ... ] } ] }, ... ] }
        StringBuilder sb = new();
        sb.Append("""{"items":[""");
        for (int i = 0; i < itemCount; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("""{"orders":[{"products":[""");
            for (int p = 0; p < productsPerOrder; p++)
            {
                if (p > 0)
                {
                    sb.Append(',');
                }

                sb.Append($$$"""{"name":"p{{{i}}}_{{{p}}}"}""");
            }

            sb.Append("]}]}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private static string BuildFilteredData(int matchCount, int productsPerItem)
    {
        // { "items": [ { "type": "a", "products": [ { "name": "p0" }, ... ] }, ... ] }
        StringBuilder sb = new();
        sb.Append("""{"items":[""");
        for (int i = 0; i < matchCount; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("""{"type":"a","products":[""");
            for (int p = 0; p < productsPerItem; p++)
            {
                if (p > 0)
                {
                    sb.Append(',');
                }

                sb.Append($$$"""{"name":"p{{{i}}}_{{{p}}}"}""");
            }

            sb.Append("]}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private static string BuildContinuationData(int categoryCount, int itemsPerCategory, int productsPerItem)
    {
        // { "root": { "groups": [ { "categories": [ { "items": [ { "products": [ { "name": "..." }, ... ] }, ... ] }, ... ] } ] } }
        StringBuilder sb = new();
        sb.Append("""{"root":{"groups":[{"categories":[""");
        for (int c = 0; c < categoryCount; c++)
        {
            if (c > 0)
            {
                sb.Append(',');
            }

            sb.Append("""{"items":[""");
            for (int i = 0; i < itemsPerCategory; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append("""{"products":[""");
                for (int p = 0; p < productsPerItem; p++)
                {
                    if (p > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append($$$"""{"name":"c{{{c}}}_i{{{i}}}_p{{{p}}}"}""");
                }

                sb.Append("]}");
            }

            sb.Append("]}");
        }

        sb.Append("]}]}}");
        return sb.ToString();
    }
}
