// <copyright file="BenchmarkArithmetic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Benchmark for arithmetic expressions — exercises NumberFromDouble,
/// TryCoerceToNumber, and the numeric comparison hot paths.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkArithmetic : JsonataBenchmarkBase
{
    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;

    private const string DataJson = """
        {
            "Account": {
                "Order": [
                    {
                        "Product": [
                            {"Price": 34.45, "Quantity": 2},
                            {"Price": 21.67, "Quantity": 1}
                        ]
                    },
                    {
                        "Product": [
                            {"Price": 34.45, "Quantity": 4},
                            {"Price": 107.99, "Quantity": 1}
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
        this.evaluator.Evaluate("$sum(Account.Order.Product.(Price * Quantity))", this.data);
        this.evaluator.Evaluate("Account.Order.Product.(Price * Quantity)", this.data);
        this.evaluator.Evaluate("1 + 2 * 3 - 4 / 2 + 10 % 3", this.data);
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => this.doc?.Dispose();

    /// <summary>
    /// Aggregate sum with per-element multiplication:
    /// $sum(Account.Order.Product.(Price * Quantity)).
    /// Exercises path descent, per-element arithmetic, and $sum aggregation.
    /// </summary>
    [Benchmark(Baseline = true)]
    public JsonElement SumProduct()
    {
        return this.evaluator.Evaluate("$sum(Account.Order.Product.(Price * Quantity))", this.data);
    }

    /// <summary>
    /// Per-element arithmetic producing an array:
    /// Account.Order.Product.(Price * Quantity).
    /// </summary>
    [Benchmark]
    public JsonElement MapArithmetic()
    {
        return this.evaluator.Evaluate("Account.Order.Product.(Price * Quantity)", this.data);
    }

    /// <summary>
    /// Pure arithmetic chain with no data access — measures raw operator cost.
    /// </summary>
    [Benchmark]
    public JsonElement PureArithmetic()
    {
        return this.evaluator.Evaluate("1 + 2 * 3 - 4 / 2 + 10 % 3", this.data);
    }
}
