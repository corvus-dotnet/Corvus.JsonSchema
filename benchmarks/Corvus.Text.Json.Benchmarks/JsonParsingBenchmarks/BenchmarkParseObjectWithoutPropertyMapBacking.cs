// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace JsonParsingBenchmarks;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkParseObjectWithoutPropertyMapBacking
{
    [Benchmark]
    public Corvus.Text.Json.JsonValueKind ParseObjectToCorvusJsonElement()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(
            """
            {
                "name": "John",
                "age": 30,
                "city": "New York",
                "slightlyLonger": true,
                "1": 1
            }
            """);

        return document.RootElement.ValueKind;
    }

    [Benchmark(Baseline = true)]
    public System.Text.Json.JsonValueKind ParseObjectToJsonElement()
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            """
            {
                "name": "John",
                "age": 30,
                "city": "New York",
                "slightlyLonger": true,
                "1": 1
            }
            """);

        return document.RootElement.ValueKind;
    }
}