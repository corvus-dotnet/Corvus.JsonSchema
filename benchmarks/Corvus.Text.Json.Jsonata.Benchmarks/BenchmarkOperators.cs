// <copyright file="BenchmarkOperators.cs" company="Endjin Limited">
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
/// Benchmarks for JSONata operators: transform pipe (~&gt;), descendant (**),
/// and conditional (?:).
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkOperators : JsonataBenchmarkBase
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

    private const string ExprTransformPipe = """Account.Order.Product.Price ~> $sum""";
    private const string ExprDescendant = """**."Product Name" """;
    private const string ExprConditional = """Account.Order[0].Product[0].Price > 30 ? "expensive" : "cheap" """;

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeTransformPipe = null!;
    private JsonataQuery nativeDescendant = null!;
    private JsonataQuery nativeConditional = null!;
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
        this.evaluator.Evaluate(ExprTransformPipe, this.data);
        this.evaluator.Evaluate(ExprDescendant, this.data);
        this.evaluator.Evaluate(ExprConditional, this.data);

        // Warm up CG
        TransformPipeCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        DescendantCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ConditionalCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeTransformPipe = new JsonataQuery(ExprTransformPipe);
        this.nativeDescendant = new JsonataQuery(ExprDescendant);
        this.nativeConditional = new JsonataQuery(ExprConditional);
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

    // ── Transform pipe (~>) ──────────────────────────────────

    /// <summary>Corvus RT: transform pipe.</summary>
    [BenchmarkCategory("TransformPipe")]
    [Benchmark]
    public JsonElement Corvus_TransformPipe()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprTransformPipe, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: transform pipe.</summary>
    [BenchmarkCategory("TransformPipe")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_TransformPipe() => this.nativeTransformPipe.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: transform pipe.</summary>
    [BenchmarkCategory("TransformPipe")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_TransformPipe()
    {
        this.workspace.Reset();
        return TransformPipeCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── Descendant (**) ──────────────────────────────────────

    /// <summary>Corvus RT: descendant operator.</summary>
    [BenchmarkCategory("Descendant")]
    [Benchmark]
    public JsonElement Corvus_Descendant()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprDescendant, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: descendant operator.</summary>
    [BenchmarkCategory("Descendant")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Descendant() => this.nativeDescendant.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: descendant operator.</summary>
    [BenchmarkCategory("Descendant")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Descendant()
    {
        this.workspace.Reset();
        return DescendantCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── Conditional (?:) ─────────────────────────────────────

    /// <summary>Corvus RT: conditional expression.</summary>
    [BenchmarkCategory("Conditional")]
    [Benchmark]
    public JsonElement Corvus_Conditional()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprConditional, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: conditional expression.</summary>
    [BenchmarkCategory("Conditional")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Conditional() => this.nativeConditional.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: conditional expression.</summary>
    [BenchmarkCategory("Conditional")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Conditional()
    {
        this.workspace.Reset();
        return ConditionalCodeGen.Evaluate(this.data, this.workspace);
    }
}
