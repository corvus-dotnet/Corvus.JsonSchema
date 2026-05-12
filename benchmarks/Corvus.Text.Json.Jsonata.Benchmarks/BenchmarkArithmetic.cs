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

    private static readonly byte[] ExprSumProductUtf8 = "$sum(Account.Order.Product.(Price * Quantity))"u8.ToArray();
    private static readonly byte[] ExprMapArithmeticUtf8 = "Account.Order.Product.(Price * Quantity)"u8.ToArray();
    private static readonly byte[] ExprPureArithmeticUtf8 = "1 + 2 * 3 - 4 / 2 + 10 % 3"u8.ToArray();


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
        this.evaluator.Evaluate(ExprSumProductUtf8, this.data, this.workspace, cacheKey: ExprSumProduct);
        this.evaluator.Evaluate(ExprMapArithmeticUtf8, this.data, this.workspace, cacheKey: ExprMapArithmetic);
        this.evaluator.Evaluate(ExprPureArithmeticUtf8, this.data, this.workspace, cacheKey: ExprPureArithmetic);

        // Warm up source-generated evaluators (first call compiles + caches)
        SumProductCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        MapArithmeticCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        PureArithmeticCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();

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
        return this.evaluator.Evaluate(ExprSumProductUtf8, this.data, this.workspace, cacheKey: ExprSumProduct);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): $sum(Account.Order.Product.(Price * Quantity)).
    /// </summary>
    [BenchmarkCategory("SumProduct")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_SumProduct() =>
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
        return this.evaluator.Evaluate(ExprMapArithmeticUtf8, this.data, this.workspace, cacheKey: ExprMapArithmetic);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): Account.Order.Product.(Price * Quantity).
    /// </summary>
    [BenchmarkCategory("MapArithmetic")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_MapArithmetic() =>
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
        return this.evaluator.Evaluate(ExprPureArithmeticUtf8, this.data, this.workspace, cacheKey: ExprPureArithmetic);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): pure arithmetic with no data access.
    /// </summary>
    [BenchmarkCategory("PureArithmetic")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_PureArithmetic() =>
        this.nativePureArithmetic.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $sum(Account.Order.Product.(Price * Quantity)).
    /// </summary>
    [BenchmarkCategory("SumProduct")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_SumProduct()
    {
        this.workspace.Reset();
        return SumProductCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// CodeGen: Account.Order.Product.(Price * Quantity).
    /// </summary>
    [BenchmarkCategory("MapArithmetic")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_MapArithmetic()
    {
        this.workspace.Reset();
        return MapArithmeticCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// CodeGen: pure arithmetic with no data access.
    /// </summary>
    [BenchmarkCategory("PureArithmetic")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_PureArithmetic()
    {
        this.workspace.Reset();
        return PureArithmeticCodeGen.Evaluate(this.data, this.workspace);
    }
}