// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;

namespace JsonParsingBenchmarks;

/// <summary>
/// Benchmark for very large array parsing performance.
/// Tests parsing of arrays with 10,000 elements of mixed types.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkVeryLargeArrayParsing
{
    private string? veryLargeArrayJson;

    [GlobalSetup]
    public void Setup()
    {
        // Very large array (10k elements) - tests array parsing performance
        veryLargeArrayJson = GenerateVeryLargeArrayJson();
    }
    [Benchmark]
    public int ParseVeryLargeArrayCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(veryLargeArrayJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        int count = 0;
        if (root.ValueKind == Corvus.Text.Json.JsonValueKind.Array)
        {
            foreach (Corvus.Text.Json.JsonElement item in root.EnumerateArray())
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark(Baseline = true)]
    public int ParseVeryLargeArraySystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(veryLargeArrayJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        int count = 0;
        if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (System.Text.Json.JsonElement item in root.EnumerateArray())
            {
                count++;
            }
        }

        return count;
    }

    #region JSON Generation

    private static string GenerateVeryLargeArrayJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");

        for (int i = 0; i < 10000; i++)
        {
            if (i > 0) sb.Append(",");

            // Mix different value types for more realistic data
            if (i % 100 == 0) sb.AppendLine(); // Add newlines occasionally for readability

            string value = (i % 4) switch
            {
                0 => (i * 1.234).ToString("F3"),
                1 => $"\"{GenerateRandomString(5 + (i % 10))}\"",
                2 => (i % 2 == 0).ToString().ToLower(),
                _ => "null"
            };

            sb.Append(value);
        }

        sb.AppendLine();
        sb.AppendLine("]");
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