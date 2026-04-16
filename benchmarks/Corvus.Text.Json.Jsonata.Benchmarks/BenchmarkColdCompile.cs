// <copyright file="BenchmarkColdCompile.cs" company="Endjin Limited">
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
/// Benchmark for cold-start compilation cost. Each invocation clears the expression
/// cache to measure the full parse + compile + evaluate cycle. This complements the
/// other benchmarks which measure evaluation of pre-compiled expressions.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkColdCompile
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
                    }
                ]
            }
        }
        """;

    // Simple: single property access
    private const string ExprSimple = "Account.`Account Name`";

    // Medium: higher-order function with lambda
    private const string ExprMedium = "$map(Account.Order.Product, function($v) { $v.`Product Name` })";

    // Complex: chained operations — filter, map, reduce
    private const string ExprComplex =
        "$reduce($map($filter(Account.Order.Product, function($v) { $v.Price > 20 }), function($v) { $v.Price * $v.Quantity }), function($prev, $curr) { $prev + $curr }, 0)";

    // Pre-encoded UTF-8 byte arrays for the Utf8 overloads
    private static readonly byte[] ExprSimpleUtf8 = "Account.`Account Name`"u8.ToArray();
    private static readonly byte[] ExprMediumUtf8 = "$map(Account.Order.Product, function($v) { $v.`Product Name` })"u8.ToArray();
    private static readonly byte[] ExprComplexUtf8 =
        "$reduce($map($filter(Account.Order.Product, function($v) { $v.Price > 20 }), function($v) { $v.Price * $v.Quantity }), function($prev, $curr) { $prev + $curr }, 0)"u8.ToArray();

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JToken nativeData = null!;
#endif

    /// <summary>
    /// Global setup: parse data once, but do NOT pre-warm the expression cache.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(DataJson));
        this.data = this.doc.RootElement;
        this.evaluator = new JsonataEvaluator();
        this.workspace = JsonWorkspace.Create();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
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
    /// Corvus: cold compile + evaluate a simple expression.
    /// </summary>
    [BenchmarkCategory("Simple")]
    [Benchmark]
    public JsonElement Corvus_Simple()
    {
        this.evaluator.ClearCache();
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSimple, this.data, this.workspace);
    }

    /// <summary>
    /// Corvus UTF-8: cold compile + evaluate a simple expression via byte[] API (no transcode).
    /// </summary>
    [BenchmarkCategory("Simple")]
    [Benchmark]
    public JsonElement Corvus_Simple_Utf8()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSimpleUtf8, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: parse + evaluate a simple expression (always cold).
    /// </summary>
    [BenchmarkCategory("Simple")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Simple()
    {
        JsonataQuery query = new(ExprSimple);
        return query.Eval(this.nativeData);
    }
#endif

    /// <summary>
    /// Corvus: cold compile + evaluate a medium expression (HOF with lambda).
    /// </summary>
    [BenchmarkCategory("Medium")]
    [Benchmark]
    public JsonElement Corvus_Medium()
    {
        this.evaluator.ClearCache();
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprMedium, this.data, this.workspace);
    }

    /// <summary>
    /// Corvus UTF-8: cold compile + evaluate a medium expression via byte[] API.
    /// </summary>
    [BenchmarkCategory("Medium")]
    [Benchmark]
    public JsonElement Corvus_Medium_Utf8()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprMediumUtf8, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: parse + evaluate a medium expression.
    /// </summary>
    [BenchmarkCategory("Medium")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Medium()
    {
        JsonataQuery query = new(ExprMedium);
        return query.Eval(this.nativeData);
    }
#endif

    /// <summary>
    /// Corvus: cold compile + evaluate a complex expression (chained filter/map/reduce).
    /// </summary>
    [BenchmarkCategory("Complex")]
    [Benchmark]
    public JsonElement Corvus_Complex()
    {
        this.evaluator.ClearCache();
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprComplex, this.data, this.workspace);
    }

    /// <summary>
    /// Corvus UTF-8: cold compile + evaluate a complex expression via byte[] API.
    /// </summary>
    [BenchmarkCategory("Complex")]
    [Benchmark]
    public JsonElement Corvus_Complex_Utf8()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprComplexUtf8, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: parse + evaluate a complex expression.
    /// </summary>
    [BenchmarkCategory("Complex")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Complex()
    {
        JsonataQuery query = new(ExprComplex);
        return query.Eval(this.nativeData);
    }
#endif
}
