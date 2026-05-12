// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using BenchmarkDotNet.Attributes;
using Corvus.Json;

namespace ValidationBenchmarks;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkLargeArrayWithUnevaluatedItems
{
    private System.Text.Json.JsonDocument? documentA1;
    private Corvus.Text.Json.ParsedJsonDocument<Benchmark.CorvusTextJson.PersonArray>? documentB1;
    private Corvus.Text.Json.JsonDocumentBuilder<Benchmark.CorvusTextJson.PersonArray.Mutable>? documentB2;
    private Corvus.Text.Json.JsonWorkspace? workspace;

    [GlobalCleanup]
    public void CleanUp()
    {
        documentA1?.Dispose();
        documentB1?.Dispose();
        documentB2?.Dispose();
        workspace?.Dispose();
    }

    [GlobalSetup]
    public void Setup()
    {
        const string personJson = """
        {
            "name": { "firstName": "Michael", "lastName": "Adams", "otherNames": ["Francis", "James"] },
            "age": 52,
            "competedInYears": [2012, 2016, 2024]
        }
        """;

        // prefix item plus 10,000 names
        string json =
            "[33.4," + string.Join(",", Enumerable.Range(0, 10000).Select(i => personJson)) + "]";

        documentA1 = System.Text.Json.JsonDocument.Parse(json);
        documentB1 = Corvus.Text.Json.ParsedJsonDocument<Benchmark.CorvusTextJson.PersonArray>.Parse(json);
        workspace = Corvus.Text.Json.JsonWorkspace.Create();
        documentB2 = documentB1.RootElement.CreateBuilder(workspace);
    }

    [Benchmark(Baseline = true)]
    public bool ValidateCorvusJsonSchema()
    {
        bool result = Benchmark.CorvusJsonSchema.PersonArray.FromJson(documentA1!.RootElement).IsValid();
        if (!result)
        {
            throw new InvalidOperationException();
        }

        return result;
    }

    [Benchmark]
    public bool ValidateCorvusTextJson()
    {
        bool result = documentB1!.RootElement.EvaluateSchema();
        if (!result)
        {
            throw new InvalidOperationException();
        }

        return result;
    }

    [Benchmark]
    public bool ValidateCorvusTextJsonDynamic()
    {
        bool result = documentB2!.RootElement.EvaluateSchema();
        if (!result)
        {
            throw new InvalidOperationException();
        }

        return result;
    }
}