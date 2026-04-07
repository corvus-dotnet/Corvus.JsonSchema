// <copyright file="BenchmarkHigherOrder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Benchmark for higher-order functions ($map, $filter, $reduce, $sort).
/// Exercises lambda invocation, parameter array pooling, sort index pooling,
/// and SequenceBuilder allocation paths.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkHigherOrder : JsonataBenchmarkBase
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
                            {"Product Name": "Bowler Hat", "Price": 34.45, "Quantity": 2},
                            {"Product Name": "Trilby hat", "Price": 21.67, "Quantity": 1}
                        ]
                    },
                    {
                        "Product": [
                            {"Product Name": "Bowler Hat", "Price": 34.45, "Quantity": 4},
                            {"Product Name": "Cloak", "Price": 107.99, "Quantity": 1}
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
        this.evaluator.Evaluate("$map(Account.Order.Product, function($v) { $v.`Product Name` })", this.data);
        this.evaluator.Evaluate("$filter(Account.Order.Product, function($v) { $v.Price > 30 })", this.data);
        this.evaluator.Evaluate("$reduce(Account.Order.Product, function($prev, $curr) { $prev + $curr.Price * $curr.Quantity }, 0)", this.data);
        this.evaluator.Evaluate("$sort(Account.Order.Product, function($a, $b) { $a.Price > $b.Price })", this.data);
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => this.doc?.Dispose();

    /// <summary>
    /// $map over products extracting names — exercises lambda invocation per element.
    /// </summary>
    [Benchmark(Baseline = true)]
    public JsonElement Map()
    {
        return this.evaluator.Evaluate("$map(Account.Order.Product, function($v) { $v.`Product Name` })", this.data);
    }

    /// <summary>
    /// $filter with predicate — exercises lambda invocation + truthiness check per element.
    /// </summary>
    [Benchmark]
    public JsonElement Filter()
    {
        return this.evaluator.Evaluate("$filter(Account.Order.Product, function($v) { $v.Price > 30 })", this.data);
    }

    /// <summary>
    /// $reduce with accumulator — exercises repeated lambda invocation with arithmetic.
    /// </summary>
    [Benchmark]
    public JsonElement Reduce()
    {
        return this.evaluator.Evaluate("$reduce(Account.Order.Product, function($prev, $curr) { $prev + $curr.Price * $curr.Quantity }, 0)", this.data);
    }

    /// <summary>
    /// $sort with comparator — exercises sort index pooling and lambda-based comparison.
    /// </summary>
    [Benchmark]
    public JsonElement Sort()
    {
        return this.evaluator.Evaluate("$sort(Account.Order.Product, function($a, $b) { $a.Price > $b.Price })", this.data);
    }
}
