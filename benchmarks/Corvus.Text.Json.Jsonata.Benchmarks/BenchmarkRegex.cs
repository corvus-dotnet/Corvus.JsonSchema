// <copyright file="BenchmarkRegex.cs" company="Endjin Limited">
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
/// Benchmark for regex-based string functions ($match and $replace).
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkRegex
{
    private const string DataJson = """
        {
            "text": "Call me at 555-123-4567 or 555-987-6543. My other number is 555-000-1111. Office: 555-222-3333."
        }
        """;

    private const string ExprMatch = "$match(text, /\\d{3}-\\d{3}-\\d{4}/)";
    private const string ExprReplace = "$replace(text, /\\d{3}-\\d{3}-\\d{4}/, \"***-***-****\")";

    private static readonly byte[] ExprMatchUtf8 = "$match(text, /\\d{3}-\\d{3}-\\d{4}/)"u8.ToArray();
    private static readonly byte[] ExprReplaceUtf8 = "$replace(text, /\\d{3}-\\d{3}-\\d{4}/, \"***-***-****\")"u8.ToArray();


    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeMatch = null!;
    private JsonataQuery nativeReplace = null!;
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

        this.evaluator.Evaluate(ExprMatchUtf8, this.data, this.workspace, cacheKey: ExprMatch);
        this.evaluator.Evaluate(ExprReplaceUtf8, this.data, this.workspace, cacheKey: ExprReplace);

        RegexMatchCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        RegexReplaceCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeMatch = new JsonataQuery(ExprMatch);
        this.nativeReplace = new JsonataQuery(ExprReplace);
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
    /// Corvus: $match with phone number regex.
    /// </summary>
    [BenchmarkCategory("Match")]
    [Benchmark]
    public JsonElement Corvus_Match()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprMatchUtf8, this.data, this.workspace, cacheKey: ExprMatch);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: $match with phone number regex.
    /// </summary>
    [BenchmarkCategory("Match")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Match() =>
        this.nativeMatch.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $match with phone number regex.
    /// </summary>
    [BenchmarkCategory("Match")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Match()
    {
        this.workspace.Reset();
        return RegexMatchCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// Corvus: $replace phone numbers with mask.
    /// </summary>
    [BenchmarkCategory("Replace")]
    [Benchmark]
    public JsonElement Corvus_Replace()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprReplaceUtf8, this.data, this.workspace, cacheKey: ExprReplace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: $replace phone numbers with mask.
    /// </summary>
    [BenchmarkCategory("Replace")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Replace() =>
        this.nativeReplace.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: $replace phone numbers with mask.
    /// </summary>
    [BenchmarkCategory("Replace")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Replace()
    {
        this.workspace.Reset();
        return RegexReplaceCodeGen.Evaluate(this.data, this.workspace);
    }
}
