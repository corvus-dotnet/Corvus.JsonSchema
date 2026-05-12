// <copyright file="BenchmarkStringFunctions.cs" company="Endjin Limited">
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
/// Benchmarks for string built-in functions: $substring, $substringBefore,
/// $substringAfter, $uppercase, $lowercase, $trim, $contains, $length.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkStringFunctions : JsonataBenchmarkBase
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

    private const string ExprSubstring = """$substring(Account.Order[0].Product[0]."Product Name", 0, 6)""";
    private const string ExprSubstringBefore = """$substringBefore(Account.Order[0].Product[0]."Product Name", " ")""";
    private const string ExprSubstringAfter = """$substringAfter(Account.Order[0].Product[0]."Product Name", " ")""";
    private const string ExprUppercase = """$uppercase(Account.Order[0].Product[0]."Product Name")""";
    private const string ExprLowercase = """$lowercase(Account.Order[0].Product[0]."Product Name")""";
    private const string ExprTrim = """$trim(Account.Order[0].Product[0]."Product Name")""";
    private const string ExprContains = """$contains(Account.Order[0].Product[0]."Product Name", "Hat")""";
    private const string ExprLength = """$length(Account.Order[0].Product[0]."Product Name")""";

    private static readonly byte[] ExprSubstringUtf8 = """$substring(Account.Order[0].Product[0]."Product Name", 0, 6)"""u8.ToArray();
    private static readonly byte[] ExprSubstringBeforeUtf8 = """$substringBefore(Account.Order[0].Product[0]."Product Name", " ")"""u8.ToArray();
    private static readonly byte[] ExprSubstringAfterUtf8 = """$substringAfter(Account.Order[0].Product[0]."Product Name", " ")"""u8.ToArray();
    private static readonly byte[] ExprUppercaseUtf8 = """$uppercase(Account.Order[0].Product[0]."Product Name")"""u8.ToArray();
    private static readonly byte[] ExprLowercaseUtf8 = """$lowercase(Account.Order[0].Product[0]."Product Name")"""u8.ToArray();
    private static readonly byte[] ExprTrimUtf8 = """$trim(Account.Order[0].Product[0]."Product Name")"""u8.ToArray();
    private static readonly byte[] ExprContainsUtf8 = """$contains(Account.Order[0].Product[0]."Product Name", "Hat")"""u8.ToArray();
    private static readonly byte[] ExprLengthUtf8 = """$length(Account.Order[0].Product[0]."Product Name")"""u8.ToArray();


    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeSubstring = null!;
    private JsonataQuery nativeSubstringBefore = null!;
    private JsonataQuery nativeSubstringAfter = null!;
    private JsonataQuery nativeUppercase = null!;
    private JsonataQuery nativeLowercase = null!;
    private JsonataQuery nativeTrim = null!;
    private JsonataQuery nativeContains = null!;
    private JsonataQuery nativeLength = null!;
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
        this.evaluator.Evaluate(ExprSubstringUtf8, this.data, this.workspace, cacheKey: ExprSubstring);
        this.evaluator.Evaluate(ExprSubstringBeforeUtf8, this.data, this.workspace, cacheKey: ExprSubstringBefore);
        this.evaluator.Evaluate(ExprSubstringAfterUtf8, this.data, this.workspace, cacheKey: ExprSubstringAfter);
        this.evaluator.Evaluate(ExprUppercaseUtf8, this.data, this.workspace, cacheKey: ExprUppercase);
        this.evaluator.Evaluate(ExprLowercaseUtf8, this.data, this.workspace, cacheKey: ExprLowercase);
        this.evaluator.Evaluate(ExprTrimUtf8, this.data, this.workspace, cacheKey: ExprTrim);
        this.evaluator.Evaluate(ExprContainsUtf8, this.data, this.workspace, cacheKey: ExprContains);
        this.evaluator.Evaluate(ExprLengthUtf8, this.data, this.workspace, cacheKey: ExprLength);

        // Warm up CG
        SubstringCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        SubstringBeforeCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        SubstringAfterCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        UppercaseCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        LowercaseCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        TrimCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ContainsCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        LengthCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeSubstring = new JsonataQuery(ExprSubstring);
        this.nativeSubstringBefore = new JsonataQuery(ExprSubstringBefore);
        this.nativeSubstringAfter = new JsonataQuery(ExprSubstringAfter);
        this.nativeUppercase = new JsonataQuery(ExprUppercase);
        this.nativeLowercase = new JsonataQuery(ExprLowercase);
        this.nativeTrim = new JsonataQuery(ExprTrim);
        this.nativeContains = new JsonataQuery(ExprContains);
        this.nativeLength = new JsonataQuery(ExprLength);
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

    // ── $substring ───────────────────────────────────────────

    /// <summary>Corvus RT: $substring.</summary>
    [BenchmarkCategory("Substring")]
    [Benchmark]
    public JsonElement Corvus_Substring()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSubstringUtf8, this.data, this.workspace, cacheKey: ExprSubstring);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $substring.</summary>
    [BenchmarkCategory("Substring")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Substring() => this.nativeSubstring.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $substring.</summary>
    [BenchmarkCategory("Substring")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Substring()
    {
        this.workspace.Reset();
        return SubstringCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $substringBefore ─────────────────────────────────────

    /// <summary>Corvus RT: $substringBefore.</summary>
    [BenchmarkCategory("SubstringBefore")]
    [Benchmark]
    public JsonElement Corvus_SubstringBefore()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSubstringBeforeUtf8, this.data, this.workspace, cacheKey: ExprSubstringBefore);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $substringBefore.</summary>
    [BenchmarkCategory("SubstringBefore")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_SubstringBefore() => this.nativeSubstringBefore.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $substringBefore.</summary>
    [BenchmarkCategory("SubstringBefore")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_SubstringBefore()
    {
        this.workspace.Reset();
        return SubstringBeforeCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $substringAfter ──────────────────────────────────────

    /// <summary>Corvus RT: $substringAfter.</summary>
    [BenchmarkCategory("SubstringAfter")]
    [Benchmark]
    public JsonElement Corvus_SubstringAfter()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSubstringAfterUtf8, this.data, this.workspace, cacheKey: ExprSubstringAfter);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $substringAfter.</summary>
    [BenchmarkCategory("SubstringAfter")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_SubstringAfter() => this.nativeSubstringAfter.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $substringAfter.</summary>
    [BenchmarkCategory("SubstringAfter")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_SubstringAfter()
    {
        this.workspace.Reset();
        return SubstringAfterCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $uppercase ───────────────────────────────────────────

    /// <summary>Corvus RT: $uppercase.</summary>
    [BenchmarkCategory("Uppercase")]
    [Benchmark]
    public JsonElement Corvus_Uppercase()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprUppercaseUtf8, this.data, this.workspace, cacheKey: ExprUppercase);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $uppercase.</summary>
    [BenchmarkCategory("Uppercase")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Uppercase() => this.nativeUppercase.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $uppercase.</summary>
    [BenchmarkCategory("Uppercase")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Uppercase()
    {
        this.workspace.Reset();
        return UppercaseCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $lowercase ───────────────────────────────────────────

    /// <summary>Corvus RT: $lowercase.</summary>
    [BenchmarkCategory("Lowercase")]
    [Benchmark]
    public JsonElement Corvus_Lowercase()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprLowercaseUtf8, this.data, this.workspace, cacheKey: ExprLowercase);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $lowercase.</summary>
    [BenchmarkCategory("Lowercase")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Lowercase() => this.nativeLowercase.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $lowercase.</summary>
    [BenchmarkCategory("Lowercase")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Lowercase()
    {
        this.workspace.Reset();
        return LowercaseCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $trim ────────────────────────────────────────────────

    /// <summary>Corvus RT: $trim.</summary>
    [BenchmarkCategory("Trim")]
    [Benchmark]
    public JsonElement Corvus_Trim()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprTrimUtf8, this.data, this.workspace, cacheKey: ExprTrim);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $trim.</summary>
    [BenchmarkCategory("Trim")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Trim() => this.nativeTrim.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $trim.</summary>
    [BenchmarkCategory("Trim")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Trim()
    {
        this.workspace.Reset();
        return TrimCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $contains ────────────────────────────────────────────

    /// <summary>Corvus RT: $contains.</summary>
    [BenchmarkCategory("Contains")]
    [Benchmark]
    public JsonElement Corvus_Contains()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprContainsUtf8, this.data, this.workspace, cacheKey: ExprContains);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $contains.</summary>
    [BenchmarkCategory("Contains")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Contains() => this.nativeContains.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $contains.</summary>
    [BenchmarkCategory("Contains")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Contains()
    {
        this.workspace.Reset();
        return ContainsCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $length ──────────────────────────────────────────────

    /// <summary>Corvus RT: $length.</summary>
    [BenchmarkCategory("Length")]
    [Benchmark]
    public JsonElement Corvus_Length()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprLengthUtf8, this.data, this.workspace, cacheKey: ExprLength);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $length.</summary>
    [BenchmarkCategory("Length")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Length() => this.nativeLength.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $length.</summary>
    [BenchmarkCategory("Length")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Length()
    {
        this.workspace.Reset();
        return LengthCodeGen.Evaluate(this.data, this.workspace);
    }
}
