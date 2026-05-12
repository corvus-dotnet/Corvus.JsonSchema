// <copyright file="BenchmarkDateTime.cs" company="Endjin Limited">
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
/// Benchmarks for date/time functions: $now, $millis, $fromMillis, $toMillis.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkDateTime : JsonataBenchmarkBase
{
    private const string DataJson = "{}";

    private const string ExprNow = "$now()";
    private const string ExprMillis = "$millis()";
    private const string ExprFromMillis = "$fromMillis(1617836400000)";
    private const string ExprToMillis = """$toMillis("2021-04-07T22:00:00.000Z")""";

    private static readonly byte[] ExprNowUtf8 = "$now()"u8.ToArray();
    private static readonly byte[] ExprMillisUtf8 = "$millis()"u8.ToArray();
    private static readonly byte[] ExprFromMillisUtf8 = "$fromMillis(1617836400000)"u8.ToArray();
    private static readonly byte[] ExprToMillisUtf8 = """$toMillis("2021-04-07T22:00:00.000Z")"""u8.ToArray();


    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeNow = null!;
    private JsonataQuery nativeMillis = null!;
    private JsonataQuery nativeFromMillis = null!;
    private JsonataQuery nativeToMillis = null!;
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

        this.evaluator.Evaluate(ExprNowUtf8, this.data, this.workspace, cacheKey: ExprNow);
        this.evaluator.Evaluate(ExprMillisUtf8, this.data, this.workspace, cacheKey: ExprMillis);
        this.evaluator.Evaluate(ExprFromMillisUtf8, this.data, this.workspace, cacheKey: ExprFromMillis);
        this.evaluator.Evaluate(ExprToMillisUtf8, this.data, this.workspace, cacheKey: ExprToMillis);

        NowCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        MillisCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        FromMillisCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ToMillisCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeNow = new JsonataQuery(ExprNow);
        this.nativeMillis = new JsonataQuery(ExprMillis);
        this.nativeFromMillis = new JsonataQuery(ExprFromMillis);
        this.nativeToMillis = new JsonataQuery(ExprToMillis);
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

    // ── $now ─────────────────────────────────────────────────

    /// <summary>Corvus RT: $now.</summary>
    [BenchmarkCategory("Now")]
    [Benchmark]
    public JsonElement Corvus_Now()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprNowUtf8, this.data, this.workspace, cacheKey: ExprNow);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $now.</summary>
    [BenchmarkCategory("Now")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Now() => this.nativeNow.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $now.</summary>
    [BenchmarkCategory("Now")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Now()
    {
        this.workspace.Reset();
        return NowCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $millis ──────────────────────────────────────────────

    /// <summary>Corvus RT: $millis.</summary>
    [BenchmarkCategory("Millis")]
    [Benchmark]
    public JsonElement Corvus_Millis()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprMillisUtf8, this.data, this.workspace, cacheKey: ExprMillis);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $millis.</summary>
    [BenchmarkCategory("Millis")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Millis() => this.nativeMillis.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $millis.</summary>
    [BenchmarkCategory("Millis")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Millis()
    {
        this.workspace.Reset();
        return MillisCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $fromMillis ──────────────────────────────────────────

    /// <summary>Corvus RT: $fromMillis.</summary>
    [BenchmarkCategory("FromMillis")]
    [Benchmark]
    public JsonElement Corvus_FromMillis()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprFromMillisUtf8, this.data, this.workspace, cacheKey: ExprFromMillis);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $fromMillis.</summary>
    [BenchmarkCategory("FromMillis")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_FromMillis() => this.nativeFromMillis.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $fromMillis.</summary>
    [BenchmarkCategory("FromMillis")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_FromMillis()
    {
        this.workspace.Reset();
        return FromMillisCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $toMillis ────────────────────────────────────────────

    /// <summary>Corvus RT: $toMillis.</summary>
    [BenchmarkCategory("ToMillis")]
    [Benchmark]
    public JsonElement Corvus_ToMillis()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprToMillisUtf8, this.data, this.workspace, cacheKey: ExprToMillis);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $toMillis.</summary>
    [BenchmarkCategory("ToMillis")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_ToMillis() => this.nativeToMillis.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $toMillis.</summary>
    [BenchmarkCategory("ToMillis")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_ToMillis()
    {
        this.workspace.Reset();
        return ToMillisCodeGen.Evaluate(this.data, this.workspace);
    }
}
