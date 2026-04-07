// <copyright file="BenchmarkPredicateFilter.cs" company="Endjin Limited">
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
/// Head-to-head benchmark for array predicate filtering.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkPredicateFilter : JsonataBenchmarkBase
{
    private const string DataJson = """
        {
            "Contact": [
                {
                    "ssn": "496913021",
                    "Phone": [
                        {"type": "home", "number": "0203 544 1234"},
                        {"type": "office", "number": "01962 001234"},
                        {"type": "mobile", "number": "077 7700 1234"}
                    ]
                },
                {
                    "ssn": "496737199",
                    "Phone": [
                        {"type": "home", "number": "3146458343"},
                        {"type": "mobile", "number": "315 782 9279"}
                    ]
                },
                {
                    "ssn": "496306525",
                    "Phone": [
                        {"type": "home", "number": "0280 564 6543"},
                        {"type": "office", "number": "0280 864 8643"},
                        {"type": "mobile", "number": "07735 853535"}
                    ]
                }
            ]
        }
        """;

    private const string ExprSinglePredicate = "Contact.Phone[type = 'mobile'].number";
    private const string ExprChainedPredicate = "Contact[ssn = '496913021'].Phone[0].number";
    private const string ExprCompoundPredicate = "Contact.Phone[type = 'office' or type = 'mobile'].number";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeSinglePredicate = null!;
    private JsonataQuery nativeChainedPredicate = null!;
    private JsonataQuery nativeCompoundPredicate = null!;
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
        this.evaluator.Evaluate(ExprSinglePredicate, this.data);
        this.evaluator.Evaluate(ExprChainedPredicate, this.data);
        this.evaluator.Evaluate(ExprCompoundPredicate, this.data);

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeSinglePredicate = new JsonataQuery(ExprSinglePredicate);
        this.nativeChainedPredicate = new JsonataQuery(ExprChainedPredicate);
        this.nativeCompoundPredicate = new JsonataQuery(ExprCompoundPredicate);
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
    /// Corvus: Contact.Phone[type='mobile'].number.
    /// </summary>
    [BenchmarkCategory("SinglePredicate")]
    [Benchmark]
    public JsonElement Corvus_SinglePredicate()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSinglePredicate, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: Contact.Phone[type='mobile'].number.
    /// </summary>
    [BenchmarkCategory("SinglePredicate")]
    [Benchmark(Baseline = true)]
    public JToken Native_SinglePredicate() =>
        this.nativeSinglePredicate.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: Contact[ssn='...'].Phone[0].number.
    /// </summary>
    [BenchmarkCategory("ChainedPredicate")]
    [Benchmark]
    public JsonElement Corvus_ChainedPredicate()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprChainedPredicate, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: Contact[ssn='...'].Phone[0].number.
    /// </summary>
    [BenchmarkCategory("ChainedPredicate")]
    [Benchmark(Baseline = true)]
    public JToken Native_ChainedPredicate() =>
        this.nativeChainedPredicate.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: Contact.Phone[type='office' or type='mobile'].number.
    /// </summary>
    [BenchmarkCategory("CompoundPredicate")]
    [Benchmark]
    public JsonElement Corvus_CompoundPredicate()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprCompoundPredicate, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: Contact.Phone[type='office' or type='mobile'].number.
    /// </summary>
    [BenchmarkCategory("CompoundPredicate")]
    [Benchmark(Baseline = true)]
    public JToken Native_CompoundPredicate() =>
        this.nativeCompoundPredicate.Eval(this.nativeData);
#endif
}