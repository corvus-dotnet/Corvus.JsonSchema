// <copyright file="BenchmarkObjectConstruction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Benchmark for object construction — the <c>{...}</c> syntax.
/// Exercises workspace document creation, group-by with focus binding,
/// and array construction.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkObjectConstruction : JsonataBenchmarkBase
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
        this.evaluator.Evaluate("""{"name": Account.`Account Name`, "total": $sum(Account.Order.Product.(Price * Quantity))}""", this.data);
        this.evaluator.Evaluate("Account.Order.Product.{`Product Name`: Price}", this.data);
        this.evaluator.Evaluate("[Account.Order.Product.{\"name\": `Product Name`, \"total\": Price * Quantity}]", this.data);
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => this.doc?.Dispose();

    /// <summary>
    /// Simple object construction with aggregation.
    /// </summary>
    [Benchmark(Baseline = true)]
    public JsonElement SimpleObject()
    {
        return this.evaluator.Evaluate(
            """{"name": Account.`Account Name`, "total": $sum(Account.Order.Product.(Price * Quantity))}""",
            this.data);
    }

    /// <summary>
    /// Per-element object construction (group-by pattern):
    /// Account.Order.Product.{`Product Name`: Price}.
    /// </summary>
    [Benchmark]
    public JsonElement GroupByObject()
    {
        return this.evaluator.Evaluate("Account.Order.Product.{`Product Name`: Price}", this.data);
    }

    /// <summary>
    /// Array of constructed objects:
    /// [Account.Order.Product.{"name": ..., "total": ...}].
    /// </summary>
    [Benchmark]
    public JsonElement ArrayOfObjects()
    {
        return this.evaluator.Evaluate(
            """[Account.Order.Product.{"name": `Product Name`, "total": Price * Quantity}]""",
            this.data);
    }
}
