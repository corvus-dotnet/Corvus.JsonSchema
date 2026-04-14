// <copyright file="BenchmarkZipShuffle.cs" company="Endjin Limited">
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
/// Benchmarks for array utility functions: $shuffle, $zip.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkZipShuffle : JsonataBenchmarkBase
{
    private const string DataJson = """
        {
            "Account": {
                "Order": [
                    {
                        "Product": [
                            {"Price": 34.45, "Quantity": 2},
                            {"Price": 21.67, "Quantity": 1}
                        ]
                    },
                    {
                        "Product": [
                            {"Price": 34.45, "Quantity": 4},
                            {"Price": 107.99, "Quantity": 1}
                        ]
                    }
                ]
            }
        }
        """;

    private const string ExprShuffle = "$shuffle(Account.Order.Product.Price)";
    private const string ExprZip = "$zip([1,2,3,4], [5,6,7,8])";
    private const string ExprZipData = "$zip(Account.Order.Product.Price, Account.Order.Product.Quantity)";
    private const string ExprZipMixed = "$zip([1,2,3,4], Account.Order.Product.Price)";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeShuffle = null!;
    private JsonataQuery nativeZip = null!;
    private JsonataQuery nativeZipData = null!;
    private JsonataQuery nativeZipMixed = null!;
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

        this.evaluator.Evaluate(ExprShuffle, this.data);
        this.evaluator.Evaluate(ExprZip, this.data);
        this.evaluator.Evaluate(ExprZipData, this.data);
        this.evaluator.Evaluate(ExprZipMixed, this.data);

        ShuffleCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ZipCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ZipDataCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ZipMixedCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeShuffle = new JsonataQuery(ExprShuffle);
        this.nativeZip = new JsonataQuery(ExprZip);
        this.nativeZipData = new JsonataQuery(ExprZipData);
        this.nativeZipMixed = new JsonataQuery(ExprZipMixed);
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

    // ── $shuffle ─────────────────────────────────────────────

    /// <summary>Corvus RT: $shuffle.</summary>
    [BenchmarkCategory("Shuffle")]
    [Benchmark]
    public JsonElement Corvus_Shuffle()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprShuffle, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $shuffle.</summary>
    [BenchmarkCategory("Shuffle")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Shuffle() => this.nativeShuffle.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $shuffle.</summary>
    [BenchmarkCategory("Shuffle")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Shuffle()
    {
        this.workspace.Reset();
        return ShuffleCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $zip ─────────────────────────────────────────────────

    /// <summary>Corvus RT: $zip.</summary>
    [BenchmarkCategory("Zip")]
    [Benchmark]
    public JsonElement Corvus_Zip()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprZip, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $zip.</summary>
    [BenchmarkCategory("Zip")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Zip() => this.nativeZip.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $zip.</summary>
    [BenchmarkCategory("Zip")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Zip()
    {
        this.workspace.Reset();
        return ZipCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $zip (data-driven) ──────────────────────────────────

    /// <summary>Corvus RT: $zip with data-driven arrays.</summary>
    [BenchmarkCategory("ZipData")]
    [Benchmark]
    public JsonElement Corvus_ZipData()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprZipData, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $zip with data-driven arrays.</summary>
    [BenchmarkCategory("ZipData")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_ZipData() => this.nativeZipData.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $zip with data-driven arrays.</summary>
    [BenchmarkCategory("ZipData")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_ZipData()
    {
        this.workspace.Reset();
        return ZipDataCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $zip (mixed constant + data) ────────────────────────

    /// <summary>Corvus RT: $zip with mixed constant and data-driven arrays.</summary>
    [BenchmarkCategory("ZipMixed")]
    [Benchmark]
    public JsonElement Corvus_ZipMixed()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprZipMixed, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $zip with mixed args.</summary>
    [BenchmarkCategory("ZipMixed")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_ZipMixed() => this.nativeZipMixed.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $zip with mixed constant and data-driven arrays.</summary>
    [BenchmarkCategory("ZipMixed")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_ZipMixed()
    {
        this.workspace.Reset();
        return ZipMixedCodeGen.Evaluate(this.data, this.workspace);
    }
}
