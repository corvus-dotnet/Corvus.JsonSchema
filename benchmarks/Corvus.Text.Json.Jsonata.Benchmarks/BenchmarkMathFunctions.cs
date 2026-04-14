// <copyright file="BenchmarkMathFunctions.cs" company="Endjin Limited">
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
/// Benchmarks for math functions: $abs, $floor, $ceil, $round, $sqrt, $power.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkMathFunctions : JsonataBenchmarkBase
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

    private const string ExprAbs = "$abs(Account.Order.Product.Price[0] - 50)";
    private const string ExprFloor = "$floor(Account.Order.Product.Price[0])";
    private const string ExprCeil = "$ceil(Account.Order.Product.Price[0])";
    private const string ExprRound = "$round(Account.Order.Product.Price[0], 1)";
    private const string ExprSqrt = "$sqrt(Account.Order.Product.Price[0])";
    private const string ExprPower = "$power(Account.Order.Product.Price[0], 2)";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeAbs = null!;
    private JsonataQuery nativeFloor = null!;
    private JsonataQuery nativeCeil = null!;
    private JsonataQuery nativeRound = null!;
    private JsonataQuery nativeSqrt = null!;
    private JsonataQuery nativePower = null!;
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

        this.evaluator.Evaluate(ExprAbs, this.data);
        this.evaluator.Evaluate(ExprFloor, this.data);
        this.evaluator.Evaluate(ExprCeil, this.data);
        this.evaluator.Evaluate(ExprRound, this.data);
        this.evaluator.Evaluate(ExprSqrt, this.data);
        this.evaluator.Evaluate(ExprPower, this.data);

        AbsCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        FloorCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        CeilCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        RoundCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        SqrtCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        PowerCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeAbs = new JsonataQuery(ExprAbs);
        this.nativeFloor = new JsonataQuery(ExprFloor);
        this.nativeCeil = new JsonataQuery(ExprCeil);
        this.nativeRound = new JsonataQuery(ExprRound);
        this.nativeSqrt = new JsonataQuery(ExprSqrt);
        this.nativePower = new JsonataQuery(ExprPower);
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

    // ── $abs ─────────────────────────────────────────────────

    /// <summary>Corvus RT: $abs.</summary>
    [BenchmarkCategory("Abs")]
    [Benchmark]
    public JsonElement Corvus_Abs()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprAbs, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $abs.</summary>
    [BenchmarkCategory("Abs")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Abs() => this.nativeAbs.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $abs.</summary>
    [BenchmarkCategory("Abs")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Abs()
    {
        this.workspace.Reset();
        return AbsCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $floor ───────────────────────────────────────────────

    /// <summary>Corvus RT: $floor.</summary>
    [BenchmarkCategory("Floor")]
    [Benchmark]
    public JsonElement Corvus_Floor()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprFloor, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $floor.</summary>
    [BenchmarkCategory("Floor")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Floor() => this.nativeFloor.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $floor.</summary>
    [BenchmarkCategory("Floor")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Floor()
    {
        this.workspace.Reset();
        return FloorCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $ceil ────────────────────────────────────────────────

    /// <summary>Corvus RT: $ceil.</summary>
    [BenchmarkCategory("Ceil")]
    [Benchmark]
    public JsonElement Corvus_Ceil()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprCeil, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $ceil.</summary>
    [BenchmarkCategory("Ceil")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Ceil() => this.nativeCeil.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $ceil.</summary>
    [BenchmarkCategory("Ceil")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Ceil()
    {
        this.workspace.Reset();
        return CeilCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $round ───────────────────────────────────────────────

    /// <summary>Corvus RT: $round.</summary>
    [BenchmarkCategory("Round")]
    [Benchmark]
    public JsonElement Corvus_Round()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprRound, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $round.</summary>
    [BenchmarkCategory("Round")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Round() => this.nativeRound.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $round.</summary>
    [BenchmarkCategory("Round")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Round()
    {
        this.workspace.Reset();
        return RoundCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $sqrt ────────────────────────────────────────────────

    /// <summary>Corvus RT: $sqrt.</summary>
    [BenchmarkCategory("Sqrt")]
    [Benchmark]
    public JsonElement Corvus_Sqrt()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSqrt, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $sqrt.</summary>
    [BenchmarkCategory("Sqrt")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Sqrt() => this.nativeSqrt.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $sqrt.</summary>
    [BenchmarkCategory("Sqrt")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Sqrt()
    {
        this.workspace.Reset();
        return SqrtCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $power ───────────────────────────────────────────────

    /// <summary>Corvus RT: $power.</summary>
    [BenchmarkCategory("Power")]
    [Benchmark]
    public JsonElement Corvus_Power()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprPower, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $power.</summary>
    [BenchmarkCategory("Power")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Power() => this.nativePower.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $power.</summary>
    [BenchmarkCategory("Power")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Power()
    {
        this.workspace.Reset();
        return PowerCodeGen.Evaluate(this.data, this.workspace);
    }
}
