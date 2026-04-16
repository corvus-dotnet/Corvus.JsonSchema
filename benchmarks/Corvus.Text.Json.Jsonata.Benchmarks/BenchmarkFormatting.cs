// <copyright file="BenchmarkFormatting.cs" company="Endjin Limited">
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
/// Benchmarks for formatting functions: $formatNumber, $formatBase, $formatInteger, $parseInteger.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkFormatting : JsonataBenchmarkBase
{
    private const string DataJson = "{}";

    private const string ExprFormatNumber = """$formatNumber(1234567.89, "#,##0.00")""";
    private const string ExprFormatBase = "$formatBase(255, 16)";
    private const string ExprFormatInteger = """$formatInteger(1234, "w")""";
    private const string ExprParseInteger = """$parseInteger("one thousand, two hundred and thirty-four", "w")""";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeFormatNumber = null!;
    private JsonataQuery nativeFormatBase = null!;
    private JsonataQuery nativeFormatInteger = null!;
    private JsonataQuery nativeParseInteger = null!;
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

        this.evaluator.Evaluate(ExprFormatNumber, this.data);
        this.evaluator.Evaluate(ExprFormatBase, this.data);
        this.evaluator.Evaluate(ExprFormatInteger, this.data);
        this.evaluator.Evaluate(ExprParseInteger, this.data);

        FormatNumberCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        FormatBaseCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        FormatIntegerCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        ParseIntegerCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeFormatNumber = new JsonataQuery(ExprFormatNumber);
        this.nativeFormatBase = new JsonataQuery(ExprFormatBase);
        this.nativeFormatInteger = new JsonataQuery(ExprFormatInteger);
        this.nativeParseInteger = new JsonataQuery(ExprParseInteger);
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

    // ── $formatNumber ────────────────────────────────────────

    /// <summary>Corvus RT: $formatNumber.</summary>
    [BenchmarkCategory("FormatNumber")]
    [Benchmark]
    public JsonElement Corvus_FormatNumber()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprFormatNumber, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $formatNumber.</summary>
    [BenchmarkCategory("FormatNumber")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_FormatNumber() => this.nativeFormatNumber.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $formatNumber.</summary>
    [BenchmarkCategory("FormatNumber")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_FormatNumber()
    {
        this.workspace.Reset();
        return FormatNumberCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $formatBase ──────────────────────────────────────────

    /// <summary>Corvus RT: $formatBase.</summary>
    [BenchmarkCategory("FormatBase")]
    [Benchmark]
    public JsonElement Corvus_FormatBase()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprFormatBase, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $formatBase.</summary>
    [BenchmarkCategory("FormatBase")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_FormatBase() => this.nativeFormatBase.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $formatBase.</summary>
    [BenchmarkCategory("FormatBase")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_FormatBase()
    {
        this.workspace.Reset();
        return FormatBaseCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $formatInteger ───────────────────────────────────────

    /// <summary>Corvus RT: $formatInteger.</summary>
    [BenchmarkCategory("FormatInteger")]
    [Benchmark]
    public JsonElement Corvus_FormatInteger()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprFormatInteger, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $formatInteger.</summary>
    [BenchmarkCategory("FormatInteger")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_FormatInteger() => this.nativeFormatInteger.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $formatInteger.</summary>
    [BenchmarkCategory("FormatInteger")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_FormatInteger()
    {
        this.workspace.Reset();
        return FormatIntegerCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $parseInteger ────────────────────────────────────────

    /// <summary>Corvus RT: $parseInteger.</summary>
    [BenchmarkCategory("ParseInteger")]
    [Benchmark]
    public JsonElement Corvus_ParseInteger()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprParseInteger, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $parseInteger.</summary>
    [BenchmarkCategory("ParseInteger")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_ParseInteger() => this.nativeParseInteger.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $parseInteger.</summary>
    [BenchmarkCategory("ParseInteger")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_ParseInteger()
    {
        this.workspace.Reset();
        return ParseIntegerCodeGen.Evaluate(this.data, this.workspace);
    }
}
