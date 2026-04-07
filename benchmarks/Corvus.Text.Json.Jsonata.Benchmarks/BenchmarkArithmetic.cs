// <copyright file="BenchmarkArithmetic.cs" company="Endjin Limited">
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
/// Head-to-head benchmark for arithmetic expressions.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkArithmetic : JsonataBenchmarkBase
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

    private const string ExprSumProduct = "$sum(Account.Order.Product.(Price * Quantity))";
    private const string ExprMapArithmetic = "Account.Order.Product.(Price * Quantity)";
    private const string ExprPureArithmetic = "1 + 2 * 3 - 4 / 2 + 10 % 3";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeSumProduct = null!;
    private JsonataQuery nativeMapArithmetic = null!;
    private JsonataQuery nativePureArithmetic = null!;
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
        this.evaluator.Evaluate(ExprSumProduct, this.data);
        this.evaluator.Evaluate(ExprMapArithmetic, this.data);
        this.evaluator.Evaluate(ExprPureArithmetic, this.data);

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeSumProduct = new JsonataQuery(ExprSumProduct);
        this.nativeMapArithmetic = new JsonataQuery(ExprMapArithmetic);
        this.nativePureArithmetic = new JsonataQuery(ExprPureArithmetic);
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
    /// Corvus: $sum(Account.Order.Product.(Price * Quantity)).
    /// </summary>
    [BenchmarkCategory("SumProduct")]
    [Benchmark]
    public JsonElement Corvus_SumProduct()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSumProduct, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: $sum(Account.Order.Product.(Price * Quantity)).
    /// </summary>
    [BenchmarkCategory("SumProduct")]
    [Benchmark(Baseline = true)]
    public JToken Native_SumProduct() =>
        this.nativeSumProduct.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: Account.Order.Product.(Price * Quantity).
    /// </summary>
    [BenchmarkCategory("MapArithmetic")]
    [Benchmark]
    public JsonElement Corvus_MapArithmetic()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprMapArithmetic, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: Account.Order.Product.(Price * Quantity).
    /// </summary>
    [BenchmarkCategory("MapArithmetic")]
    [Benchmark(Baseline = true)]
    public JToken Native_MapArithmetic() =>
        this.nativeMapArithmetic.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: pure arithmetic with no data access.
    /// </summary>
    [BenchmarkCategory("PureArithmetic")]
    [Benchmark]
    public JsonElement Corvus_PureArithmetic()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprPureArithmetic, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: pure arithmetic with no data access.
    /// </summary>
    [BenchmarkCategory("PureArithmetic")]
    [Benchmark(Baseline = true)]
    public JToken Native_PureArithmetic() =>
        this.nativePureArithmetic.Eval(this.nativeData);
#endif
}