// <copyright file="BenchmarkObjectFunctions.cs" company="Endjin Limited">
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
/// Benchmarks for object built-in functions: $keys, $values, $merge.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkObjectFunctions : JsonataBenchmarkBase
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

    private const string ExprKeys = "$keys(Account.Order[0].Product[0])";
    private const string ExprValues = "$values(Account.Order[0].Product[0])";
    private const string ExprValuesStd = """$each(Account.Order[0].Product[0], function($v){$v})""";
    private const string ExprMerge = "$merge(Account.Order.Product)";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeKeys = null!;
    private JsonataQuery nativeValues = null!;
    private JsonataQuery nativeValuesStd = null!;
    private JsonataQuery nativeMerge = null!;
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
        this.evaluator.Evaluate(ExprKeys, this.data);
        this.evaluator.Evaluate(ExprValues, this.data);
        this.evaluator.Evaluate(ExprValuesStd, this.data);
        this.evaluator.Evaluate(ExprMerge, this.data);

        // Warm up CG
        KeysCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ValuesCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ValuesStdCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        MergeCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeKeys = new JsonataQuery(ExprKeys);
        this.nativeValues = new JsonataQuery(ExprValues);
        this.nativeValuesStd = new JsonataQuery(ExprValuesStd);
        this.nativeMerge = new JsonataQuery(ExprMerge);
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

    // ── $keys ────────────────────────────────────────────────

    /// <summary>Corvus RT: $keys.</summary>
    [BenchmarkCategory("Keys")]
    [Benchmark]
    public JsonElement Corvus_Keys()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprKeys, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $keys.</summary>
    [BenchmarkCategory("Keys")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Keys() => this.nativeKeys.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $keys.</summary>
    [BenchmarkCategory("Keys")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Keys()
    {
        this.workspace.Reset();
        return KeysCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $values ──────────────────────────────────────────────

    /// <summary>Corvus RT: $values.</summary>
    [BenchmarkCategory("Values")]
    [Benchmark]
    public JsonElement Corvus_Values()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprValues, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $values.</summary>
    [BenchmarkCategory("Values")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Values() => this.nativeValues.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $values.</summary>
    [BenchmarkCategory("Values")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Values()
    {
        this.workspace.Reset();
        return ValuesCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── values standard syntax ($each) ─────────────────────

    /// <summary>Corvus RT: standard values via $each.</summary>
    [BenchmarkCategory("ValuesStd")]
    [Benchmark]
    public JsonElement Corvus_ValuesStd()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprValuesStd, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: standard values via $each.</summary>
    [BenchmarkCategory("ValuesStd")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_ValuesStd() => this.nativeValuesStd.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: standard values via $each.</summary>
    [BenchmarkCategory("ValuesStd")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_ValuesStd()
    {
        this.workspace.Reset();
        return ValuesStdCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $merge ───────────────────────────────────────────────

    /// <summary>Corvus RT: $merge.</summary>
    [BenchmarkCategory("Merge")]
    [Benchmark]
    public JsonElement Corvus_Merge()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprMerge, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $merge.</summary>
    [BenchmarkCategory("Merge")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Merge() => this.nativeMerge.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $merge.</summary>
    [BenchmarkCategory("Merge")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Merge()
    {
        this.workspace.Reset();
        return MergeCodeGen.Evaluate(this.data, this.workspace);
    }
}
