// <copyright file="BenchmarkEmployeeTransform.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Reference benchmark matching jsonata.net.native's BenchmarkApp.
/// Restructures the employees dataset using property navigation, string concat,
/// and array predicate filtering.
/// </summary>
/// <remarks>
/// Expression: <c>{'name': Employee.FirstName &amp; ' ' &amp; Employee.Surname, 'mobile': Contact.Phone[type = 'mobile'].number}</c>
/// Dataset: employees.json from the JSONata test suite.
/// </remarks>
[MemoryDiagnoser]
public class BenchmarkEmployeeTransform : JsonataBenchmarkBase
{
    private const string Query = """
        {
          'name': Employee.FirstName & ' ' & Employee.Surname,
          'mobile': Contact.Phone[type = 'mobile'].number
        }
        """;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => this.SetupFromFile(Query, "employees.json");

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => this.Cleanup();

    /// <summary>
    /// Evaluate only (expression pre-compiled and cached).
    /// </summary>
    [Benchmark(Baseline = true)]
    public JsonElement Evaluate()
    {
        return this.Evaluator.Evaluate(this.Expression, this.Data);
    }

    /// <summary>
    /// Parse + compile + evaluate (cold start, no cache).
    /// </summary>
    [Benchmark]
    public JsonElement ParseAndEvaluate()
    {
        var freshEvaluator = new JsonataEvaluator();
        return freshEvaluator.Evaluate(this.Expression, this.Data);
    }
}
