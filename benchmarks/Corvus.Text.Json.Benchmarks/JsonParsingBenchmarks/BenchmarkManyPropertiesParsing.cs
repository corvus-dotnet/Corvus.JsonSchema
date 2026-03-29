// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;

namespace JsonParsingBenchmarks;

/// <summary>
/// Benchmark for parsing objects with many properties.
/// Tests parsing of objects with 500 properties each having different types of values.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkManyPropertiesParsing
{
    private string? manyPropertiesJson;
    private string[] propertyNames = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Object with many properties (500) - tests property lookup performance
        manyPropertiesJson = GenerateManyPropertiesJson();

        // Pre-allocate property names to avoid string allocations during benchmarking
        propertyNames = new string[500];
        for (int i = 0; i < 500; i++)
        {
            propertyNames[i] = $"property_{i}";
        }
    }

    [Benchmark]
    public int ParseManyPropertiesCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(manyPropertiesJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        root.EnsurePropertyMap(); // Ensure property map is built for efficient access

        int accessedProperties = 0;

        // Access ALL properties using TryGetProperty to test property lookup performance
        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (root.TryGetProperty(propertyNames[i], out _))
            {
                accessedProperties++;
            }
        }

        return accessedProperties;
    }

    [Benchmark(Baseline = true)]
    public int ParseManyPropertiesSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(manyPropertiesJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        int accessedProperties = 0;

        // Access ALL properties using TryGetProperty to test property lookup performance
        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (root.TryGetProperty(propertyNames[i], out _))
            {
                accessedProperties++;
            }
        }

        return accessedProperties;
    }

    #region JSON Generation

    private static string GenerateManyPropertiesJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        for (int i = 0; i < 500; i++)
        {
            if (i > 0) sb.AppendLine(",");

            string value = (i % 5) switch
            {
                0 => (i * 1.234).ToString("F3"),
                1 => $"\"{GenerateRandomString(5 + (i % 10))}\"",
                2 => (i % 2 == 0).ToString().ToLower(),
                3 => "null",
                _ => $"[{i}, {i + 1}, {i + 2}]"
            };

            sb.Append($"  \"property_{i}\": {value}");
        }

        sb.AppendLine();
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";
        var random = new Random(42); // Fixed seed for consistent benchmarks
        char[] result = new char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }

        return new string(result);
    }

    #endregion
}