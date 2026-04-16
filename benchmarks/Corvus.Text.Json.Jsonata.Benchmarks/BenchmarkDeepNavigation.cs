// <copyright file="BenchmarkDeepNavigation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
#if !NETFRAMEWORK
using Jsonata.Net.Native;
using Jsonata.Net.Native.Json;
#endif

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Benchmark for property navigation through deeply nested data (10 levels).
/// Measures the cost of traversing long property chains.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkDeepNavigation
{
    private const string Expr10Level = "a.b.c.d.e.f.g.h.i.j.value";
    private const string Expr5Level = "a.b.c.d.e.value";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery native10Level = null!;
    private JsonataQuery native5Level = null!;
    private JToken nativeData = null!;
#endif

    /// <summary>
    /// Global setup: builds a 10-level nested JSON object and pre-warms evaluators.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        string dataJson = BuildNestedJson(["a", "b", "c", "d", "e", "f", "g", "h", "i", "j"], 42);
        this.doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(dataJson));
        this.data = this.doc.RootElement;
        this.evaluator = new JsonataEvaluator();
        this.workspace = JsonWorkspace.Create();

        this.evaluator.Evaluate(Expr10Level, this.data);
        this.evaluator.Evaluate(Expr5Level, this.data);

        DeepNavigationCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        DeepNavigation5LevelCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(dataJson);
        this.native10Level = new JsonataQuery(Expr10Level);
        this.native5Level = new JsonataQuery(Expr5Level);
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
    /// Corvus: navigate 10 levels deep.
    /// </summary>
    [BenchmarkCategory("10Level")]
    [Benchmark]
    public JsonElement Corvus_10Level()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(Expr10Level, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: navigate 10 levels deep.
    /// </summary>
    [BenchmarkCategory("10Level")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_10Level() =>
        this.native10Level.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: navigate 10 levels deep.
    /// </summary>
    [BenchmarkCategory("10Level")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_10Level()
    {
        this.workspace.Reset();
        return DeepNavigationCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// Corvus: navigate 5 levels deep (half the depth, to show scaling).
    /// </summary>
    [BenchmarkCategory("5Level")]
    [Benchmark]
    public JsonElement Corvus_5Level()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(Expr5Level, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native: navigate 5 levels deep.
    /// </summary>
    [BenchmarkCategory("5Level")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_5Level() =>
        this.native5Level.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: navigate 5 levels deep.
    /// </summary>
    [BenchmarkCategory("5Level")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_5Level()
    {
        this.workspace.Reset();
        return DeepNavigation5LevelCodeGen.Evaluate(this.data, this.workspace);
    }

    private static string BuildNestedJson(string[] keys, int leafValue)
    {
        StringBuilder sb = new();
        for (int i = 0; i < keys.Length; i++)
        {
            sb.Append("{\"");
            sb.Append(keys[i]);
            sb.Append("\":");
        }

        sb.Append("{\"value\":");
        sb.Append(leafValue);
        sb.Append('}');

        for (int i = 0; i < keys.Length; i++)
        {
            sb.Append('}');
        }

        return sb.ToString();
    }
}
