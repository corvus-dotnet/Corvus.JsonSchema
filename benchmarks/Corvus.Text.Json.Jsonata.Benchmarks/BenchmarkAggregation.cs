// <copyright file="BenchmarkAggregation.cs" company="Endjin Limited">
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
/// Benchmarks for aggregate functions: $max, $min, $average.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkAggregation : JsonataBenchmarkBase
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

    private const string ExprMax = "$max(Account.Order.Product.Price)";
    private const string ExprMin = "$min(Account.Order.Product.Price)";
    private const string ExprAverage = "$average(Account.Order.Product.Price)";

    private static readonly byte[] ExprMaxUtf8 = "$max(Account.Order.Product.Price)"u8.ToArray();
    private static readonly byte[] ExprMinUtf8 = "$min(Account.Order.Product.Price)"u8.ToArray();
    private static readonly byte[] ExprAverageUtf8 = "$average(Account.Order.Product.Price)"u8.ToArray();


    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeMax = null!;
    private JsonataQuery nativeMin = null!;
    private JsonataQuery nativeAverage = null!;
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

        this.evaluator.Evaluate(ExprMaxUtf8, this.data, this.workspace, cacheKey: ExprMax);
        this.evaluator.Evaluate(ExprMinUtf8, this.data, this.workspace, cacheKey: ExprMin);
        this.evaluator.Evaluate(ExprAverageUtf8, this.data, this.workspace, cacheKey: ExprAverage);

        MaxCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        MinCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        AverageCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeMax = new JsonataQuery(ExprMax);
        this.nativeMin = new JsonataQuery(ExprMin);
        this.nativeAverage = new JsonataQuery(ExprAverage);
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

    // ── $max ─────────────────────────────────────────────────

    /// <summary>Corvus RT: $max.</summary>
    [BenchmarkCategory("Max")]
    [Benchmark]
    public JsonElement Corvus_Max()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprMaxUtf8, this.data, this.workspace, cacheKey: ExprMax);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $max.</summary>
    [BenchmarkCategory("Max")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Max() => this.nativeMax.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $max.</summary>
    [BenchmarkCategory("Max")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Max()
    {
        this.workspace.Reset();
        return MaxCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $min ─────────────────────────────────────────────────

    /// <summary>Corvus RT: $min.</summary>
    [BenchmarkCategory("Min")]
    [Benchmark]
    public JsonElement Corvus_Min()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprMinUtf8, this.data, this.workspace, cacheKey: ExprMin);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $min.</summary>
    [BenchmarkCategory("Min")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Min() => this.nativeMin.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $min.</summary>
    [BenchmarkCategory("Min")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Min()
    {
        this.workspace.Reset();
        return MinCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $average ─────────────────────────────────────────────

    /// <summary>Corvus RT: $average.</summary>
    [BenchmarkCategory("Average")]
    [Benchmark]
    public JsonElement Corvus_Average()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprAverageUtf8, this.data, this.workspace, cacheKey: ExprAverage);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $average.</summary>
    [BenchmarkCategory("Average")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Average() => this.nativeAverage.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $average.</summary>
    [BenchmarkCategory("Average")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Average()
    {
        this.workspace.Reset();
        return AverageCodeGen.Evaluate(this.data, this.workspace);
    }
}
