// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using BenchmarkDotNet.Attributes;
using Corvus.Json;

namespace ValidationBenchmarks;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkSimpleValidation
{
    private System.Text.Json.JsonDocument? documentA1;
    private Corvus.Text.Json.ParsedJsonDocument<Benchmark.CorvusTextJson.Person>? documentB1;

    [GlobalCleanup]
    public void CleanUp()
    {
        this.documentA1?.Dispose();
        this.documentB1?.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        const string json = """
        {
            "name": { "firstName": "Michael", "lastName": "Adams", "otherNames": ["Francis", "James"] },
            "age": 52,
            "competedInYears": [2012, 2016, 2024]
        }
        """;

        this.documentA1 = System.Text.Json.JsonDocument.Parse(json);
        this.documentB1 = Corvus.Text.Json.ParsedJsonDocument<Benchmark.CorvusTextJson.Person>.Parse(json);
    }

    [Benchmark(Baseline = true)]
    public bool ValidateCorvusJsonSchema()
    {
        return Benchmark.CorvusJsonSchema.Person.FromJson(documentA1!.RootElement).IsValid();
    }

    [Benchmark]
    public bool ValidateCorvusTextJson()
    {
        return documentB1!.RootElement.EvaluateSchema();
    }
}