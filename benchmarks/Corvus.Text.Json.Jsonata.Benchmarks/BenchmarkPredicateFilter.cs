// <copyright file="BenchmarkPredicateFilter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Benchmark for array predicate filtering — the inline <c>[predicate]</c> syntax.
/// Exercises ApplyStages, SequenceBuilder collection, and truthiness evaluation.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkPredicateFilter : JsonataBenchmarkBase
{
    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;

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
        this.evaluator.Evaluate("Contact.Phone[type = 'mobile'].number", this.data);
        this.evaluator.Evaluate("Contact[ssn = '496913021'].Phone[0].number", this.data);
        this.evaluator.Evaluate("Contact.Phone[type = 'office' or type = 'mobile'].number", this.data);
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => this.doc?.Dispose();

    /// <summary>
    /// Filter nested arrays: Contact.Phone[type='mobile'].number.
    /// Exercises double array descent + equality predicate.
    /// </summary>
    [Benchmark(Baseline = true)]
    public JsonElement SinglePredicate()
    {
        return this.evaluator.Evaluate("Contact.Phone[type = 'mobile'].number", this.data);
    }

    /// <summary>
    /// Chained predicates: Contact[ssn='...'].Phone[0].number.
    /// Exercises predicate at parent level + index at child level.
    /// </summary>
    [Benchmark]
    public JsonElement ChainedPredicate()
    {
        return this.evaluator.Evaluate("Contact[ssn = '496913021'].Phone[0].number", this.data);
    }

    /// <summary>
    /// Compound predicate with 'or': Contact.Phone[type='office' or type='mobile'].number.
    /// Exercises boolean short-circuit evaluation in predicates.
    /// </summary>
    [Benchmark]
    public JsonElement CompoundPredicate()
    {
        return this.evaluator.Evaluate("Contact.Phone[type = 'office' or type = 'mobile'].number", this.data);
    }
}
