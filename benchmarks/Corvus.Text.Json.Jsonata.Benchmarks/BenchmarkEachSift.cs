// <copyright file="BenchmarkEachSift.cs" company="Endjin Limited">
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
/// Head-to-head benchmark for $each and $sift (object HOFs).
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkEachSift : JsonataBenchmarkBase
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

    private const string ExprEach = """$each(Account.Order[0].Product[0], function($v, $k) { $k & ": " & $string($v) })""";
    private const string ExprSift = """$sift(Account.Order[0].Product[0], function($v) { $type($v) = "number" })""";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeEach = null!;
    private JsonataQuery nativeSift = null!;
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
        this.evaluator.Evaluate(ExprEach, this.data);
        this.evaluator.Evaluate(ExprSift, this.data);

        // Warm up source-generated evaluators
        EachHofCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        SiftHofCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeEach = new JsonataQuery(ExprEach);
        this.nativeSift = new JsonataQuery(ExprSift);
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
    /// Corvus RT: $each mapping object properties.
    /// </summary>
    [BenchmarkCategory("Each")]
    [Benchmark]
    public JsonElement Corvus_Each()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprEach, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): $each.
    /// </summary>
    [BenchmarkCategory("Each")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Each() =>
        this.nativeEach.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $each mapping object properties.
    /// </summary>
    [BenchmarkCategory("Each")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Each()
    {
        this.workspace.Reset();
        return EachHofCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// Corvus RT: $sift filtering object properties.
    /// </summary>
    [BenchmarkCategory("Sift")]
    [Benchmark]
    public JsonElement Corvus_Sift()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSift, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): $sift.
    /// </summary>
    [BenchmarkCategory("Sift")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Sift() =>
        this.nativeSift.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $sift filtering object properties.
    /// </summary>
    [BenchmarkCategory("Sift")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Sift()
    {
        this.workspace.Reset();
        return SiftHofCodeGen.Evaluate(this.data, this.workspace);
    }
}
