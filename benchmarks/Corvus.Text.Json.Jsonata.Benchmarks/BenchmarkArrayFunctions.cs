// <copyright file="BenchmarkArrayFunctions.cs" company="Endjin Limited">
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
/// Benchmarks for array built-in functions: $count, $flatten, $distinct,
/// $append, $reverse.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkArrayFunctions : JsonataBenchmarkBase
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

    private const string ExprCount = "$count(Account.Order[0].Product)";
    private const string ExprFlattenStd = "Account.Order.Product[]";
    private const string ExprDistinct = """$distinct(Account.Order.Product."Product Name")""";
    private const string ExprAppend = "$append(Account.Order[0].Product, Account.Order[1].Product)";
    private const string ExprReverse = "$reverse(Account.Order[0].Product)";

    private static readonly byte[] ExprCountUtf8 = "$count(Account.Order[0].Product)"u8.ToArray();
    private static readonly byte[] ExprFlattenStdUtf8 = "Account.Order.Product[]"u8.ToArray();
    private static readonly byte[] ExprDistinctUtf8 = """$distinct(Account.Order.Product."Product Name")"""u8.ToArray();
    private static readonly byte[] ExprAppendUtf8 = "$append(Account.Order[0].Product, Account.Order[1].Product)"u8.ToArray();
    private static readonly byte[] ExprReverseUtf8 = "$reverse(Account.Order[0].Product)"u8.ToArray();


    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeCount = null!;
    private JsonataQuery nativeFlattenStd = null!;
    private JsonataQuery nativeDistinct = null!;
    private JsonataQuery nativeAppend = null!;
    private JsonataQuery nativeReverse = null!;
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
        this.evaluator.Evaluate(ExprCountUtf8, this.data, this.workspace, cacheKey: ExprCount);
        this.evaluator.Evaluate(ExprFlattenStdUtf8, this.data, this.workspace, cacheKey: ExprFlattenStd);
        this.evaluator.Evaluate(ExprDistinctUtf8, this.data, this.workspace, cacheKey: ExprDistinct);
        this.evaluator.Evaluate(ExprAppendUtf8, this.data, this.workspace, cacheKey: ExprAppend);
        this.evaluator.Evaluate(ExprReverseUtf8, this.data, this.workspace, cacheKey: ExprReverse);

        // Warm up CG
        CountCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        FlattenStdCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        DistinctCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        AppendCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ReverseCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeCount = new JsonataQuery(ExprCount);
        this.nativeFlattenStd = new JsonataQuery(ExprFlattenStd);
        this.nativeDistinct = new JsonataQuery(ExprDistinct);
        this.nativeAppend = new JsonataQuery(ExprAppend);
        this.nativeReverse = new JsonataQuery(ExprReverse);
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

    // ── $count ───────────────────────────────────────────────

    /// <summary>Corvus RT: $count.</summary>
    [BenchmarkCategory("Count")]
    [Benchmark]
    public JsonElement Corvus_Count()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprCountUtf8, this.data, this.workspace, cacheKey: ExprCount);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $count.</summary>
    [BenchmarkCategory("Count")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Count() => this.nativeCount.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $count.</summary>
    [BenchmarkCategory("Count")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Count()
    {
        this.workspace.Reset();
        return CountCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── flatten standard syntax (array[]) ──────────────────

    /// <summary>Corvus RT: standard flatten syntax.</summary>
    [BenchmarkCategory("FlattenStd")]
    [Benchmark]
    public JsonElement Corvus_FlattenStd()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprFlattenStdUtf8, this.data, this.workspace, cacheKey: ExprFlattenStd);
    }

#if !NETFRAMEWORK
    /// <summary>Native: standard flatten syntax.</summary>
    [BenchmarkCategory("FlattenStd")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_FlattenStd() => this.nativeFlattenStd.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: standard flatten syntax.</summary>
    [BenchmarkCategory("FlattenStd")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_FlattenStd()
    {
        this.workspace.Reset();
        return FlattenStdCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $distinct ────────────────────────────────────────────

    /// <summary>Corvus RT: $distinct.</summary>
    [BenchmarkCategory("Distinct")]
    [Benchmark]
    public JsonElement Corvus_Distinct()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprDistinctUtf8, this.data, this.workspace, cacheKey: ExprDistinct);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $distinct.</summary>
    [BenchmarkCategory("Distinct")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Distinct() => this.nativeDistinct.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $distinct.</summary>
    [BenchmarkCategory("Distinct")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Distinct()
    {
        this.workspace.Reset();
        return DistinctCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $append ──────────────────────────────────────────────

    /// <summary>Corvus RT: $append.</summary>
    [BenchmarkCategory("Append")]
    [Benchmark]
    public JsonElement Corvus_Append()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprAppendUtf8, this.data, this.workspace, cacheKey: ExprAppend);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $append.</summary>
    [BenchmarkCategory("Append")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Append() => this.nativeAppend.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $append.</summary>
    [BenchmarkCategory("Append")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Append()
    {
        this.workspace.Reset();
        return AppendCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $reverse ─────────────────────────────────────────────

    /// <summary>Corvus RT: $reverse.</summary>
    [BenchmarkCategory("Reverse")]
    [Benchmark]
    public JsonElement Corvus_Reverse()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprReverseUtf8, this.data, this.workspace, cacheKey: ExprReverse);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $reverse.</summary>
    [BenchmarkCategory("Reverse")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Reverse() => this.nativeReverse.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $reverse.</summary>
    [BenchmarkCategory("Reverse")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Reverse()
    {
        this.workspace.Reset();
        return ReverseCodeGen.Evaluate(this.data, this.workspace);
    }
}
