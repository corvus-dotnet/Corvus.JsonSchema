// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;

namespace JsonParsingBenchmarks;

/// <summary>
/// Benchmark for deeply nested object parsing performance.
/// Tests parsing of objects nested 50 levels deep with properties at each level.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkDeeplyNestedParsing
{
    private string? deeplyNestedJson;

    [GlobalSetup]
    public void Setup()
    {
        // Deeply nested object (50 levels) - tests recursion performance
        deeplyNestedJson = GenerateDeeplyNestedJson();
    }

    [Benchmark]
    public int ParseDeeplyNestedCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(deeplyNestedJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        int depth = 0;
        Corvus.Text.Json.JsonElement current = root;

        while (current.ValueKind == Corvus.Text.Json.JsonValueKind.Object)
        {
            depth++;
            if (current.TryGetProperty("nested", out Corvus.Text.Json.JsonElement nestedElement))
            {
                current = nestedElement;
            }
            else
            {
                break;
            }
        }

        return depth;
    }

    [Benchmark(Baseline = true)]
    public int ParseDeeplyNestedSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(deeplyNestedJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        int depth = 0;
        System.Text.Json.JsonElement current = root;

        while (current.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            depth++;
            if (current.TryGetProperty("nested", out System.Text.Json.JsonElement nestedElement))
            {
                current = nestedElement;
            }
            else
            {
                break;
            }
        }

        return depth;
    }

    #region JSON Generation

    private static string GenerateDeeplyNestedJson()
    {
        var sb = new StringBuilder();

        for (int i = 0; i < 50; i++)
        {
            sb.Append("{");
            sb.Append($"\"id\": {i}, ");
            sb.Append($"\"name\": \"Level {i}\", ");
            sb.Append($"\"value\": {i * 10.5:F1}, ");
            sb.Append($"\"active\": {(i % 2 == 0).ToString().ToLower()}, ");

            if (i < 49)
            {
                sb.Append("\"nested\": ");
            }
            else
            {
                sb.Append("\"leaf\": \"final value\"");
            }
        }

        for (int i = 0; i < 50; i++)
        {
            sb.Append("}");
        }

        return sb.ToString();
    }

    #endregion
}