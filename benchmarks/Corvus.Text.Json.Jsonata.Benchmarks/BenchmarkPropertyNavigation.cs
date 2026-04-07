// <copyright file="BenchmarkPropertyNavigation.cs" company="Endjin Limited">
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
/// Head-to-head benchmark for property navigation.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkPropertyNavigation : JsonataBenchmarkBase
{
    private const string DataJson = """
        {
            "Account": {
                "Account Name": "Firefly",
                "Order": [
                    {
                        "OrderID": "order103",
                        "Product": [
                            {"Product Name": "Bowler Hat", "ProductID": 858383, "Price": 34.45, "Quantity": 2},
                            {"Product Name": "Trilby hat", "ProductID": 858236, "Price": 21.67, "Quantity": 1}
                        ]
                    },
                    {
                        "OrderID": "order104",
                        "Product": [
                            {"Product Name": "Bowler Hat", "ProductID": 858383, "Price": 34.45, "Quantity": 4},
                            {"Product Name": "Cloak", "ProductID": 345664, "Price": 107.99, "Quantity": 1}
                        ]
                    }
                ]
            }
        }
        """;

    private const string ExprDeepPath = "Account.Order.Product.Price";
    private const string ExprQuotedProperty = "Account.`Account Name`";
    private const string ExprArrayIndex = "Account.Order[0].OrderID";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeDeepPath = null!;
    private JsonataQuery nativeQuotedProperty = null!;
    private JsonataQuery nativeArrayIndex = null!;
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
        this.evaluator.Evaluate(ExprDeepPath, this.data);
        this.evaluator.Evaluate(ExprQuotedProperty, this.data);
        this.evaluator.Evaluate(ExprArrayIndex, this.data);

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeDeepPath = new JsonataQuery(ExprDeepPath);
        this.nativeQuotedProperty = new JsonataQuery(ExprQuotedProperty);
        this.nativeArrayIndex = new JsonataQuery(ExprArrayIndex);
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
    /// Corvus: deep path traversal through arrays.
    /// </summary>
    [BenchmarkCategory("DeepPath")]
    [Benchmark]
    public JsonElement Corvus_DeepPath()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprDeepPath, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: deep path traversal through arrays.
    /// </summary>
    [BenchmarkCategory("DeepPath")]
    [Benchmark(Baseline = true)]
    public JToken Native_DeepPath() =>
        this.nativeDeepPath.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: quoted property name access.
    /// </summary>
    [BenchmarkCategory("QuotedProperty")]
    [Benchmark]
    public JsonElement Corvus_QuotedProperty()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprQuotedProperty, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: quoted property name access.
    /// </summary>
    [BenchmarkCategory("QuotedProperty")]
    [Benchmark(Baseline = true)]
    public JToken Native_QuotedProperty() =>
        this.nativeQuotedProperty.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: array index access.
    /// </summary>
    [BenchmarkCategory("ArrayIndex")]
    [Benchmark]
    public JsonElement Corvus_ArrayIndex()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprArrayIndex, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Native: array index access.
    /// </summary>
    [BenchmarkCategory("ArrayIndex")]
    [Benchmark(Baseline = true)]
    public JToken Native_ArrayIndex() =>
        this.nativeArrayIndex.Eval(this.nativeData);
#endif
}