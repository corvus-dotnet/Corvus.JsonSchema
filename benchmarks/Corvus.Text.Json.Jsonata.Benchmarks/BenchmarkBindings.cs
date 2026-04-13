// <copyright file="BenchmarkBindings.cs" company="Endjin Limited">
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
/// Benchmarks for variable bindings: let-style binding and binding inside HOF.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkBindings : JsonataBenchmarkBase
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

    private const string ExprLetBinding = """($total := $sum(Account.Order.Product.(Price * Quantity)); $total > 100 ? "high" : "low")""";
    private const string ExprBindingInHof = """$map(Account.Order.Product, function($v) { ($p := $v.Price * $v.Quantity; $p > 50 ? "big" : "small") })""";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeLetBinding = null!;
    private JsonataQuery nativeBindingInHof = null!;
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
        this.evaluator.Evaluate(ExprLetBinding, this.data);
        this.evaluator.Evaluate(ExprBindingInHof, this.data);

        // Warm up CG
        LetBindingCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        BindingInHofCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeLetBinding = new JsonataQuery(ExprLetBinding);
        this.nativeBindingInHof = new JsonataQuery(ExprBindingInHof);
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

    // ── Let-style binding ────────────────────────────────────

    /// <summary>Corvus RT: let binding.</summary>
    [BenchmarkCategory("LetBinding")]
    [Benchmark]
    public JsonElement Corvus_LetBinding()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprLetBinding, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: let binding.</summary>
    [BenchmarkCategory("LetBinding")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_LetBinding() => this.nativeLetBinding.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: let binding.</summary>
    [BenchmarkCategory("LetBinding")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_LetBinding()
    {
        this.workspace.Reset();
        return LetBindingCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── Binding inside HOF ───────────────────────────────────

    /// <summary>Corvus RT: binding in HOF.</summary>
    [BenchmarkCategory("BindingInHof")]
    [Benchmark]
    public JsonElement Corvus_BindingInHof()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprBindingInHof, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: binding in HOF.</summary>
    [BenchmarkCategory("BindingInHof")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_BindingInHof() => this.nativeBindingInHof.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: binding in HOF.</summary>
    [BenchmarkCategory("BindingInHof")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_BindingInHof()
    {
        this.workspace.Reset();
        return BindingInHofCodeGen.Evaluate(this.data, this.workspace);
    }
}
