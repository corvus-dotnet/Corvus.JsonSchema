// <copyright file="BenchmarkSplitReplace.cs" company="Endjin Limited">
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
/// Benchmark for $split (regex and string) and $replace (string pattern).
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkSplitReplace
{
    private const string DataJson = """
        {
            "text": "Call me at 555-123-4567 or 555-987-6543. My other number is 555-000-1111. Office: 555-222-3333."
        }
        """;

    private const string ExprSplitRegex = "$split(text, /\\d{3}-\\d{3}-\\d{4}/)";
    private const string ExprSplitString = "$split(text, \". \")";
    private const string ExprReplaceString = "$replace(text, \"555\", \"XXX\")";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeSplitRegex = null!;
    private JsonataQuery nativeSplitString = null!;
    private JsonataQuery nativeReplaceString = null!;
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
        this.evaluator.Evaluate(ExprSplitRegex, this.data);
        this.evaluator.Evaluate(ExprSplitString, this.data);
        this.evaluator.Evaluate(ExprReplaceString, this.data);

        // Warm up CG
        SplitRegexCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        SplitStringCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        ReplaceStringCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeSplitRegex = new JsonataQuery(ExprSplitRegex);
        this.nativeSplitString = new JsonataQuery(ExprSplitString);
        this.nativeReplaceString = new JsonataQuery(ExprReplaceString);
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

    // ── $split with regex ────────────────────────────────────

    /// <summary>
    /// Corvus RT: $split with regex pattern.
    /// </summary>
    [BenchmarkCategory("SplitRegex")]
    [Benchmark]
    public JsonElement Corvus_SplitRegex()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSplitRegex, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: $split with regex pattern.
    /// </summary>
    [BenchmarkCategory("SplitRegex")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_SplitRegex() =>
        this.nativeSplitRegex.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $split with regex pattern.
    /// </summary>
    [BenchmarkCategory("SplitRegex")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_SplitRegex()
    {
        this.workspace.Reset();
        return SplitRegexCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $split with string ───────────────────────────────────

    /// <summary>
    /// Corvus RT: $split with string separator.
    /// </summary>
    [BenchmarkCategory("SplitString")]
    [Benchmark]
    public JsonElement Corvus_SplitString()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSplitString, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: $split with string separator.
    /// </summary>
    [BenchmarkCategory("SplitString")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_SplitString() =>
        this.nativeSplitString.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $split with string separator.
    /// </summary>
    [BenchmarkCategory("SplitString")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_SplitString()
    {
        this.workspace.Reset();
        return SplitStringCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $replace with string ─────────────────────────────────

    /// <summary>
    /// Corvus RT: $replace with string pattern.
    /// </summary>
    [BenchmarkCategory("ReplaceString")]
    [Benchmark]
    public JsonElement Corvus_ReplaceString()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprReplaceString, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: $replace with string pattern.
    /// </summary>
    [BenchmarkCategory("ReplaceString")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_ReplaceString() =>
        this.nativeReplaceString.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $replace with string pattern.
    /// </summary>
    [BenchmarkCategory("ReplaceString")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_ReplaceString()
    {
        this.workspace.Reset();
        return ReplaceStringCodeGen.Evaluate(this.data, this.workspace);
    }
}
