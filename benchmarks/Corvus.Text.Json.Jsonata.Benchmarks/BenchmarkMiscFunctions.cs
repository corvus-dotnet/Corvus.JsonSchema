// <copyright file="BenchmarkMiscFunctions.cs" company="Endjin Limited">
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
/// Benchmarks for miscellaneous functions: $single, $pad, $spread, $lookup.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkMiscFunctions : JsonataBenchmarkBase
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

    private const string ExprSingle = """$single(Account.Order[0].Product, function($v) { $v.Price > 30 })""";
    private const string ExprPad = """$pad(Account.Order[0].Product[0]."Product Name", 20)""";
    private const string ExprSpread = """$spread(Account.Order[0].Product[0])""";
    private const string ExprLookup = """$lookup(Account.Order[0].Product[0], "Price")""";

    private static readonly byte[] ExprSingleUtf8 = """$single(Account.Order[0].Product, function($v) { $v.Price > 30 })"""u8.ToArray();
    private static readonly byte[] ExprPadUtf8 = """$pad(Account.Order[0].Product[0]."Product Name", 20)"""u8.ToArray();
    private static readonly byte[] ExprSpreadUtf8 = """$spread(Account.Order[0].Product[0])"""u8.ToArray();
    private static readonly byte[] ExprLookupUtf8 = """$lookup(Account.Order[0].Product[0], "Price")"""u8.ToArray();


    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeSingle = null!;
    private JsonataQuery nativePad = null!;
    private JsonataQuery nativeSpread = null!;
    private JsonataQuery nativeLookup = null!;
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

        // Warm up RT
        this.evaluator.Evaluate(ExprSingleUtf8, this.data, this.workspace, cacheKey: ExprSingle);
        this.evaluator.Evaluate(ExprPadUtf8, this.data, this.workspace, cacheKey: ExprPad);
        this.evaluator.Evaluate(ExprSpreadUtf8, this.data, this.workspace, cacheKey: ExprSpread);
        this.evaluator.Evaluate(ExprLookupUtf8, this.data, this.workspace, cacheKey: ExprLookup);

        // Warm up CG
        SingleCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        PadCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        SpreadCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        LookupCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeSingle = new JsonataQuery(ExprSingle);
        this.nativePad = new JsonataQuery(ExprPad);
        this.nativeSpread = new JsonataQuery(ExprSpread);
        this.nativeLookup = new JsonataQuery(ExprLookup);
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

    // ── $single ──────────────────────────────────────────────

    /// <summary>Corvus RT: $single.</summary>
    [BenchmarkCategory("Single")]
    [Benchmark]
    public JsonElement Corvus_Single()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSingleUtf8, this.data, this.workspace, cacheKey: ExprSingle);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $single.</summary>
    [BenchmarkCategory("Single")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Single() => this.nativeSingle.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $single.</summary>
    [BenchmarkCategory("Single")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Single()
    {
        this.workspace.Reset();
        return SingleCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $pad ─────────────────────────────────────────────────

    /// <summary>Corvus RT: $pad.</summary>
    [BenchmarkCategory("Pad")]
    [Benchmark]
    public JsonElement Corvus_Pad()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprPadUtf8, this.data, this.workspace, cacheKey: ExprPad);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $pad.</summary>
    [BenchmarkCategory("Pad")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Pad() => this.nativePad.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $pad.</summary>
    [BenchmarkCategory("Pad")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Pad()
    {
        this.workspace.Reset();
        return PadCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $spread ──────────────────────────────────────────────

    /// <summary>Corvus RT: $spread.</summary>
    [BenchmarkCategory("Spread")]
    [Benchmark]
    public JsonElement Corvus_Spread()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSpreadUtf8, this.data, this.workspace, cacheKey: ExprSpread);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $spread.</summary>
    [BenchmarkCategory("Spread")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Spread() => this.nativeSpread.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $spread.</summary>
    [BenchmarkCategory("Spread")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Spread()
    {
        this.workspace.Reset();
        return SpreadCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $lookup ──────────────────────────────────────────────

    /// <summary>Corvus RT: $lookup.</summary>
    [BenchmarkCategory("Lookup")]
    [Benchmark]
    public JsonElement Corvus_Lookup()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprLookupUtf8, this.data, this.workspace, cacheKey: ExprLookup);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $lookup.</summary>
    [BenchmarkCategory("Lookup")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Lookup() => this.nativeLookup.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $lookup.</summary>
    [BenchmarkCategory("Lookup")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Lookup()
    {
        this.workspace.Reset();
        return LookupCodeGen.Evaluate(this.data, this.workspace);
    }
}
