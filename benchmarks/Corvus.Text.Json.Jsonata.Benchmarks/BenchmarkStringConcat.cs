// <copyright file="BenchmarkStringConcat.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Benchmark for string concatenation — exercises the UTF-8 buffer concat path,
/// number-to-string coercion, and the $join built-in function.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkStringConcat : JsonataBenchmarkBase
{
    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;

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
        this.evaluator.Evaluate("FirstName & ' ' & Surname", this.data);
        this.evaluator.Evaluate("FirstName & ' ' & Surname & ', age ' & $string(Age)", this.data);
        this.evaluator.Evaluate("$join([Address.Street, Address.City, Address.Postcode], ', ')", this.data);
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => this.doc?.Dispose();

    /// <summary>
    /// Simple two-part string concatenation with separator.
    /// </summary>
    [Benchmark(Baseline = true)]
    public JsonElement SimpleConcat()
    {
        return this.evaluator.Evaluate("FirstName & ' ' & Surname", this.data);
    }

    /// <summary>
    /// String concatenation with number-to-string coercion ($string(Age)).
    /// </summary>
    [Benchmark]
    public JsonElement ConcatWithNumber()
    {
        return this.evaluator.Evaluate("FirstName & ' ' & Surname & ', age ' & $string(Age)", this.data);
    }

    /// <summary>
    /// $join with array and separator — exercises the built-in join function.
    /// </summary>
    [Benchmark]
    public JsonElement JoinArray()
    {
        return this.evaluator.Evaluate("$join([Address.Street, Address.City, Address.Postcode], ', ')", this.data);
    }
}
