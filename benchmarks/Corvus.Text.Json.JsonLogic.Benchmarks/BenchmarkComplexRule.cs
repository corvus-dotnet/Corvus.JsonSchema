// <copyright file="BenchmarkComplexRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;

using CorvusJsonElement = Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Benchmark for a complex rule combining multiple operator types: conditionals,
/// comparisons, arithmetic, string operations, and variable access.
/// This simulates a realistic business rule that classifies a person by age bracket
/// and produces a greeting.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkComplexRule : JsonLogicBenchmarkBase
{
    // If age < 13 → "Hello, {name}! You are a child."
    // Else if age < 18 → "Hello, {name}! You are a teenager."
    // Else if age >= 65 → "Hello, {name}! You are a senior."
    // Else → "Hello, {name}! You are an adult."
    private const string RuleJson =
        """
        {"if":[
            {"<":[{"var":"age"},13]},
            {"cat":["Hello, ",{"var":"name"},"! You are a child."]},
            {"<":[{"var":"age"},18]},
            {"cat":["Hello, ",{"var":"name"},"! You are a teenager."]},
            {">=":[{"var":"age"},65]},
            {"cat":["Hello, ",{"var":"name"},"! You are a senior."]},
            {"cat":["Hello, ",{"var":"name"},"! You are an adult."]}
        ]}
        """;

    private const string DataJson = """{"name":"Alice","age":30}""";

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => this.Setup(RuleJson, DataJson);

    /// <summary>
    /// Evaluate using JsonEverything.
    /// </summary>
    [Benchmark(Baseline = true)]
    public System.Text.Json.Nodes.JsonNode? JsonEverything()
    {
        return global::Json.Logic.JsonLogic.Apply(this.JeRule, this.JeData);
    }

    /// <summary>
    /// Evaluate using Corvus JsonLogic (bytecode VM).
    /// </summary>
    [Benchmark]
    public void CorvusJsonLogic()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonLogicEvaluator.Default.Evaluate(this.CorvusLogicRule, this.CorvusData, workspace);
    }

    /// <summary>
    /// Evaluate using Corvus JsonLogic (functional tree-walking).
    /// </summary>
    [Benchmark]
    public void CorvusFunctional()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonLogicEvaluator.Default.EvaluateFunctional(this.CorvusLogicRule, this.CorvusData, workspace);
    }
}
