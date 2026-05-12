// <copyright file="BenchmarkObjectConstruction.cs" company="Endjin Limited">
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
/// Head-to-head benchmark for object construction.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkObjectConstruction : JsonataBenchmarkBase
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

    private const string ExprSimpleObject = """{"name": Account.`Account Name`, "total": $sum(Account.Order.Product.(Price * Quantity))}""";
    private const string ExprGroupByObject = "Account.Order.Product.{`Product Name`: Price}";
    private const string ExprArrayOfObjects = """[Account.Order.Product.{"name": `Product Name`, "total": Price * Quantity}]""";

    private static readonly byte[] ExprSimpleObjectUtf8 = """{"name": Account.`Account Name`, "total": $sum(Account.Order.Product.(Price * Quantity))}"""u8.ToArray();
    private static readonly byte[] ExprGroupByObjectUtf8 = "Account.Order.Product.{`Product Name`: Price}"u8.ToArray();
    private static readonly byte[] ExprArrayOfObjectsUtf8 = """[Account.Order.Product.{"name": `Product Name`, "total": Price * Quantity}]"""u8.ToArray();


    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeSimpleObject = null!;
    private JsonataQuery nativeGroupByObject = null!;
    private JsonataQuery nativeArrayOfObjects = null!;
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
        this.evaluator.Evaluate(ExprSimpleObjectUtf8, this.data, this.workspace, cacheKey: ExprSimpleObject);
        this.evaluator.Evaluate(ExprGroupByObjectUtf8, this.data, this.workspace, cacheKey: ExprGroupByObject);
        this.evaluator.Evaluate(ExprArrayOfObjectsUtf8, this.data, this.workspace, cacheKey: ExprArrayOfObjects);

        // Warm up source-generated evaluators (first call compiles + caches)
        SimpleObjectCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        GroupByObjectCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();
        ArrayOfObjectsCodeGen.Evaluate(this.data, this.workspace);
        this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeSimpleObject = new JsonataQuery(ExprSimpleObject);
        this.nativeGroupByObject = new JsonataQuery(ExprGroupByObject);
        this.nativeArrayOfObjects = new JsonataQuery(ExprArrayOfObjects);
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
    /// Corvus: simple object with aggregation.
    /// </summary>
    [BenchmarkCategory("SimpleObject")]
    [Benchmark]
    public JsonElement Corvus_SimpleObject()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprSimpleObjectUtf8, this.data, this.workspace, cacheKey: ExprSimpleObject);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): simple object with aggregation.
    /// </summary>
    [BenchmarkCategory("SimpleObject")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_SimpleObject() =>
        this.nativeSimpleObject.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: per-element object construction.
    /// </summary>
    [BenchmarkCategory("GroupByObject")]
    [Benchmark]
    public JsonElement Corvus_GroupByObject()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprGroupByObjectUtf8, this.data, this.workspace, cacheKey: ExprGroupByObject);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): per-element object construction.
    /// </summary>
    [BenchmarkCategory("GroupByObject")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_GroupByObject() =>
        this.nativeGroupByObject.Eval(this.nativeData);
#endif

    /// <summary>
    /// Corvus: array of constructed objects.
    /// </summary>
    [BenchmarkCategory("ArrayOfObjects")]
    [Benchmark]
    public JsonElement Corvus_ArrayOfObjects()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprArrayOfObjectsUtf8, this.data, this.workspace, cacheKey: ExprArrayOfObjects);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Jsonata.Net.Native (reference impl): array of constructed objects.
    /// </summary>
    [BenchmarkCategory("ArrayOfObjects")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_ArrayOfObjects() =>
        this.nativeArrayOfObjects.Eval(this.nativeData);
#endif

    /// <summary>
    /// CodeGen: simple object with aggregation.
    /// </summary>
    [BenchmarkCategory("SimpleObject")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_SimpleObject()
    {
        this.workspace.Reset();
        return SimpleObjectCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// CodeGen: per-element object construction.
    /// </summary>
    [BenchmarkCategory("GroupByObject")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_GroupByObject()
    {
        this.workspace.Reset();
        return GroupByObjectCodeGen.Evaluate(this.data, this.workspace);
    }

    /// <summary>
    /// CodeGen: array of constructed objects.
    /// </summary>
    [BenchmarkCategory("ArrayOfObjects")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_ArrayOfObjects()
    {
        this.workspace.Reset();
        return ArrayOfObjectsCodeGen.Evaluate(this.data, this.workspace);
    }
}