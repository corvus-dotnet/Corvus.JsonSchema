// <copyright file="BenchmarkStringConcat.cs" company="Endjin Limited">
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
/// Head-to-head benchmark for string concatenation.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkStringConcat : JsonataBenchmarkBase
{
    private const string DataJson = """
        {
            "FirstName": "Fred",
            "Surname": "Smith",
            "Age": 28,
            "Address": {
                "Street": "Hursley Park",
                "City": "Winchester",
                "Postcode": "SO21 2JN"
            }
        }
        """;

    private const string ExprSimpleConcat = "FirstName & ' ' & Surname";
    private const string ExprConcatWithNumber = "FirstName & ' ' & Surname & ', age ' & $string(Age)";
    private const string ExprJoinArray = "$join([Address.Street, Address.City, Address.Postcode], ', ')";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeSimpleConcat = null!;
    private JsonataQuery nativeConcatWithNumber = null!;
    private JsonataQuery nativeJoinArray = null!;
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
        this.evaluator.Evaluate(ExprSimpleConcat, this.data);
        this.evaluator.Evaluate(ExprConcatWithNumber, this.data);
        this.evaluator.Evaluate(ExprJoinArray, this.data);

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeSimpleConcat = new JsonataQuery(ExprSimpleConcat);
        this.nativeConcatWithNumber = new JsonataQuery(ExprConcatWithNumber);
        this.nativeJoinArray = new JsonataQuery(ExprJoinArray);
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
    /// Corvus: simple string concatenation.
    /// </summary>
    [BenchmarkCategory("SimpleConcat")]
    [Benchmark]
    public JsonElement Corvus_SimpleConcat()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSimpleConcat, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: simple string concatenation.
    /// </summary>
    [BenchmarkCategory("SimpleConcat")]
    [Benchmark(Baseline = true)]
    public JToken Native_SimpleConcat() =>
        this.nativeSimpleConcat.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: string concat with number coercion.
    /// </summary>
    [BenchmarkCategory("ConcatWithNumber")]
    [Benchmark]
    public JsonElement Corvus_ConcatWithNumber()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprConcatWithNumber, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: string concat with number coercion.
    /// </summary>
    [BenchmarkCategory("ConcatWithNumber")]
    [Benchmark(Baseline = true)]
    public JToken Native_ConcatWithNumber() =>
        this.nativeConcatWithNumber.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: $join with array and separator.
    /// </summary>
    [BenchmarkCategory("JoinArray")]
    [Benchmark]
    public JsonElement Corvus_JoinArray()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprJoinArray, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: $join with array and separator.
    /// </summary>
    [BenchmarkCategory("JoinArray")]
    [Benchmark(Baseline = true)]
    public JToken Native_JoinArray() =>
        this.nativeJoinArray.Eval(this.nativeData);
#endif
}