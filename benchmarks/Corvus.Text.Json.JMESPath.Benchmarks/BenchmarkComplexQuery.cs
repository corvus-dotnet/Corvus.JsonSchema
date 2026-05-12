// <copyright file="BenchmarkComplexQuery.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Benchmark for complex query: people[?age > `25`] | sort_by(@, &amp;age) | [*].{name: name, age: age}.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkComplexQuery : JMESPathBenchmarkBase
{
    private const string ExpressionText = "people[?age > `25`] | sort_by(@, &age) | [*].{name: name, age: age}";
    private const string DataJson = """{"people":[{"name":"Alice","age":30,"email":"a@x.com"},{"name":"Bob","age":25,"email":"b@x.com"},{"name":"Charlie","age":35,"email":"c@x.com"},{"name":"Diana","age":28,"email":"d@x.com"},{"name":"Eve","age":22,"email":"e@x.com"}]}""";

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => this.Setup(ExpressionText, DataJson);

    /// <summary>
    /// Evaluate using JmesPath.Net.
    /// </summary>
    [Benchmark(Baseline = true)]
    public string JmesPathNet()
    {
        return this.JmesPath.Transform(this.DataJsonString, this.Expression);
    }

    /// <summary>
    /// Evaluate using Corvus JMESPath runtime.
    /// </summary>
    [Benchmark]
    public void CorvusJMESPath()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JMESPathEvaluator.Default.Search(this.Expression, this.CorvusData, workspace);
    }

    /// <summary>
    /// Evaluate using Corvus code-generated evaluator.
    /// </summary>
    [Benchmark]
    public void CorvusCodeGen()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        ComplexQueryCodeGen.Evaluate(this.CorvusData, workspace);
    }
}
