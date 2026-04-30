// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace JsonDeepEqualsBenchmarks;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkEqualsInOrderProperties
{
    private System.Text.Json.JsonDocument? documentA1;
    private System.Text.Json.JsonDocument? documentA2;
    private Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>? documentB1;
    private Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>? documentB2;
    private Corvus.Text.Json.JsonElementA documentC1;
    private Corvus.Text.Json.JsonElementB documentC2;

    [GlobalCleanup]
    public void CleanUp()
    {
        this.documentA1?.Dispose();
        this.documentA2?.Dispose();
        this.documentB1?.Dispose();
        this.documentB2?.Dispose();
    }

    [Benchmark]
    public bool CorvusJsonElementDeepEquals()
    {
        return Corvus.Text.Json.Internal.JsonElementHelpers.DeepEquals(this.documentB1!.RootElement, this.documentB2!.RootElement);
    }

    [Benchmark]
    public bool CorvusJsonElementGenericDeepEquals()
    {
        return Corvus.Text.Json.Internal.JsonElementHelpers.DeepEquals<JsonElement, JsonElement>(this.documentB1!.RootElement, this.documentB2!.RootElement);
    }

    [Benchmark]
    public bool CorvusJsonElementHeterogenousDeepEquals()
    {
        return Corvus.Text.Json.Internal.JsonElementHelpers.DeepEquals(this.documentC1, this.documentC2);
    }

    [Benchmark(Baseline = true)]
    public bool JsonElementDeepEquals()
    {
        return System.Text.Json.JsonElement.DeepEquals(this.documentA1!.RootElement, this.documentA2!.RootElement);
    }

    [GlobalSetup]
    public void Setup()
    {
        this.documentA1 = System.Text.Json.JsonDocument.Parse(
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

        this.documentA2 = System.Text.Json.JsonDocument.Parse(
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

        this.documentB1 = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(
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

        this.documentB2 = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(
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
        this.documentC1 = JsonElementA.From(this.documentB1.RootElement);
        this.documentC2 = JsonElementB.From(this.documentB2.RootElement);
    }
}