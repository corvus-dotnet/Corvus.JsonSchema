// <copyright file="BenchmarkTypeConversion.cs" company="Endjin Limited">
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
/// Benchmarks for type conversion functions: $type, $exists, $not, $boolean, $number, $string.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkTypeConversion : JsonataBenchmarkBase
{
    private const string DataJson = """
        {
            "Account": {
                "Account Name": "Firefly",
                "Order": [
                    {
                        "Product": [
                            {"SKU": "0406654608", "Price": 34.45, "Quantity": 2},
                            {"SKU": "0406634348", "Price": 21.67, "Quantity": 1}
                        ]
                    },
                    {
                        "Product": [
                            {"SKU": "040657863", "Price": 34.45, "Quantity": 4},
                            {"SKU": "0406654603", "Price": 107.99, "Quantity": 1}
                        ]
                    }
                ]
            }
        }
        """;

    private const string ExprType = "$type(Account.Order.Product.Price[0])";
    private const string ExprExists = "$exists(Account.Order.Product)";
    private const string ExprNot = """$not(Account."Account Name" = "Firefly")""";
    private const string ExprBoolean = "$boolean(Account.Order.Product.Price[0])";
    private const string ExprNumber = """$number("34.45")""";
    private const string ExprString = "$string(Account.Order.Product.Price[0])";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeType = null!;
    private JsonataQuery nativeExists = null!;
    private JsonataQuery nativeNot = null!;
    private JsonataQuery nativeBoolean = null!;
    private JsonataQuery nativeNumber = null!;
    private JsonataQuery nativeString = null!;
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

        this.evaluator.Evaluate(ExprType, this.data);
        this.evaluator.Evaluate(ExprExists, this.data);
        this.evaluator.Evaluate(ExprNot, this.data);
        this.evaluator.Evaluate(ExprBoolean, this.data);
        this.evaluator.Evaluate(ExprNumber, this.data);
        this.evaluator.Evaluate(ExprString, this.data);

        TypeCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ExistsCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        NotCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        BooleanCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        NumberCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        StringCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeType = new JsonataQuery(ExprType);
        this.nativeExists = new JsonataQuery(ExprExists);
        this.nativeNot = new JsonataQuery(ExprNot);
        this.nativeBoolean = new JsonataQuery(ExprBoolean);
        this.nativeNumber = new JsonataQuery(ExprNumber);
        this.nativeString = new JsonataQuery(ExprString);
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

    // ── $type ────────────────────────────────────────────────

    /// <summary>Corvus RT: $type.</summary>
    [BenchmarkCategory("Type")]
    [Benchmark]
    public JsonElement Corvus_Type()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprType, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $type.</summary>
    [BenchmarkCategory("Type")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Type() => this.nativeType.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $type.</summary>
    [BenchmarkCategory("Type")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Type()
    {
        this.workspace.Reset();
        return TypeCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $exists ──────────────────────────────────────────────

    /// <summary>Corvus RT: $exists.</summary>
    [BenchmarkCategory("Exists")]
    [Benchmark]
    public JsonElement Corvus_Exists()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprExists, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $exists.</summary>
    [BenchmarkCategory("Exists")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Exists() => this.nativeExists.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $exists.</summary>
    [BenchmarkCategory("Exists")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Exists()
    {
        this.workspace.Reset();
        return ExistsCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $not ─────────────────────────────────────────────────

    /// <summary>Corvus RT: $not.</summary>
    [BenchmarkCategory("Not")]
    [Benchmark]
    public JsonElement Corvus_Not()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprNot, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $not.</summary>
    [BenchmarkCategory("Not")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Not() => this.nativeNot.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $not.</summary>
    [BenchmarkCategory("Not")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Not()
    {
        this.workspace.Reset();
        return NotCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $boolean ─────────────────────────────────────────────

    /// <summary>Corvus RT: $boolean.</summary>
    [BenchmarkCategory("Boolean")]
    [Benchmark]
    public JsonElement Corvus_Boolean()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprBoolean, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $boolean.</summary>
    [BenchmarkCategory("Boolean")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Boolean() => this.nativeBoolean.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $boolean.</summary>
    [BenchmarkCategory("Boolean")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Boolean()
    {
        this.workspace.Reset();
        return BooleanCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $number ──────────────────────────────────────────────

    /// <summary>Corvus RT: $number.</summary>
    [BenchmarkCategory("Number")]
    [Benchmark]
    public JsonElement Corvus_Number()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprNumber, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $number.</summary>
    [BenchmarkCategory("Number")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Number() => this.nativeNumber.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $number.</summary>
    [BenchmarkCategory("Number")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Number()
    {
        this.workspace.Reset();
        return NumberCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $string ──────────────────────────────────────────────

    /// <summary>Corvus RT: $string.</summary>
    [BenchmarkCategory("String")]
    [Benchmark]
    public JsonElement Corvus_String()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprString, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $string.</summary>
    [BenchmarkCategory("String")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_String() => this.nativeString.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $string.</summary>
    [BenchmarkCategory("String")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_String()
    {
        this.workspace.Reset();
        return StringCodeGen.Evaluate(this.data, this.workspace);
    }
}
