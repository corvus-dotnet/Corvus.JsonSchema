// <copyright file="BenchmarkPropertyNavigation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Benchmark for property navigation — the most common JSONata operation.
/// Tests simple path traversal, array descent, and nested property access.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkPropertyNavigation : JsonataBenchmarkBase
{
    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;

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

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(DataJson));
        this.data = this.doc.RootElement;
        this.evaluator = new JsonataEvaluator();

        // Pre-warm all expressions
        this.evaluator.Evaluate("Account.Order.Product.Price", this.data);
        this.evaluator.Evaluate("""Account.`Account Name`""", this.data);
        this.evaluator.Evaluate("Account.Order[0].OrderID", this.data);
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => this.doc?.Dispose();

    /// <summary>
    /// Deep path traversal through arrays: Account.Order.Product.Price
    /// exercises array descent (flattening) at two levels.
    /// </summary>
    [Benchmark(Baseline = true)]
    public JsonElement DeepPath()
    {
        return this.evaluator.Evaluate("Account.Order.Product.Price", this.data);
    }

    /// <summary>
    /// Quoted property name access: Account.`Account Name`.
    /// </summary>
    [Benchmark]
    public JsonElement QuotedProperty()
    {
        return this.evaluator.Evaluate("""Account.`Account Name`""", this.data);
    }

    /// <summary>
    /// Array index access: Account.Order[0].OrderID.
    /// </summary>
    [Benchmark]
    public JsonElement ArrayIndex()
    {
        return this.evaluator.Evaluate("Account.Order[0].OrderID", this.data);
    }
}
