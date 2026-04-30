// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using BenchmarkDotNet.Attributes;

namespace JsonParsingBenchmarks;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkParseObjectWithPropertyMapBacking
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
                "1": 1,
                "2": 1,
                "3": 1,
                "4": 1,
                "5": 1,
                "6": 1,
                "7": 1,
                "8": 1,
                "9": 1,
                "10": 1,
                "11": 1,
                "12": 1,
                "13": 1,
                "14": 1,
                "15": 1,
                "16": 1,
                "17": 1,
                "18": 1,
                "19": 1
            }
            """);

        document.RootElement.EnsurePropertyMap();

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
                "1": 1,
                "2": 1,
                "3": 1,
                "4": 1,
                "5": 1,
                "6": 1,
                "7": 1,
                "8": 1,
                "9": 1,
                "10": 1,
                "11": 1,
                "12": 1,
                "13": 1,
                "14": 1,
                "15": 1,
                "16": 1,
                "17": 1,
                "18": 1,
                "19": 1
            }
            """);

        return document.RootElement.ValueKind;
    }
}