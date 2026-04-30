// <copyright file="BenchmarkEmployeeTransform.cs" company="Endjin Limited">
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
/// Reference benchmark matching jsonata.net.native's BenchmarkApp.
/// Head-to-head comparison of Corvus vs jsonata.net.native on the
/// employees dataset with property navigation, string concat, and
/// array predicate filtering.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkEmployeeTransform : JsonataBenchmarkBase
{
    private const string Query = """
        {
          'name': Employee.FirstName & ' ' & Employee.Surname,
          'mobile': Contact.Phone[type = 'mobile'].number
        }
        """;

    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeQuery = null!;
    private JToken nativeData = null!;
#endif
    private string dataJson = null!;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.dataJson = File.ReadAllText("employees.json");
        this.SetupFromFile(Query, "employees.json");
        this.workspace = JsonWorkspace.Create();

        // Warm up source-generated evaluator (first call compiles + caches)
        EmployeeTransformCodeGen.Evaluate(this.Data, this.workspace);
        this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeQuery = new JsonataQuery(Query);
        this.nativeData = JToken.Parse(this.dataJson);
#endif
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.workspace.Dispose();
        this.Cleanup();
    }

    /// <summary>
    /// Corvus: evaluate only (expression pre-compiled and cached).
    /// </summary>
    [BenchmarkCategory("CachedEval")]
    [Benchmark]
    public JsonElement Corvus_Evaluate()
    {
        this.workspace.Reset();
        return this.Evaluator.Evaluate(this.Utf8Expression, this.Data, this.workspace, cacheKey: this.Expression);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): evaluate only (query pre-compiled, data pre-parsed).
    /// </summary>
    [BenchmarkCategory("CachedEval")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Evaluate()
    {
        return this.nativeQuery.Eval(this.nativeData);
    }
#endif

    /// <summary>
    /// Corvus: compile + evaluate (fresh evaluator, data pre-parsed).
    /// </summary>
    [BenchmarkCategory("ColdStart")]
    [Benchmark]
    public JsonElement Corvus_ParseAndEvaluate()
    {
        this.workspace.Reset();
        var freshEvaluator = new JsonataEvaluator();
        return freshEvaluator.Evaluate(this.Utf8Expression, this.Data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): compile + evaluate (fresh query, data pre-parsed).
    /// </summary>
    [BenchmarkCategory("ColdStart")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_ParseAndEvaluate()
    {
        var freshQuery = new JsonataQuery(Query);
        return freshQuery.Eval(this.nativeData);
    }
#endif

    /// <summary>
    /// CodeGen: evaluate only (expression source-generated and cached).
    /// </summary>
    [BenchmarkCategory("CachedEval")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Evaluate()
    {
        this.workspace.Reset();
        return EmployeeTransformCodeGen.Evaluate(this.Data, this.workspace);
    }
}