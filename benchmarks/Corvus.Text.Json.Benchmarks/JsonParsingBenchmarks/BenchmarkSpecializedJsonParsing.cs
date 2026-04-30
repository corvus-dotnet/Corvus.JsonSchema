// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;

namespace JsonParsingBenchmarks;

/// <summary>
/// This benchmark class has been split into separate scenario-specific classes for clearer baseline comparison:
/// - BenchmarkVeryLargeArrayParsing: Tests parsing of arrays with 10,000 elements
/// - BenchmarkDeeplyNestedParsing: Tests parsing of objects nested 50 levels deep
/// - BenchmarkManyPropertiesParsing: Tests parsing of objects with 500 properties
/// - BenchmarkEscapeHeavyParsing: Tests parsing of strings with heavy escape sequences
/// - BenchmarkNumberHeavyParsing: Tests parsing of JSON with many numbers in various formats
///
/// This file is retained for historical reference but should not be used for benchmarking.
/// Use the individual scenario classes instead.
/// </summary>
[Obsolete("This class has been split into separate scenario-specific benchmark classes. Use the individual classes instead.")]
[MemoryDiagnoser]
public class BenchmarkSpecializedJsonParsing
{
    private string? veryLargeArrayJson;
    private string? deeplyNestedJson;
    private string? manyPropertiesJson;
    private string? escapeHeavyJson;
    private string? numberHeavyJson;
    private byte[]? veryLargeArrayBytes;

    [GlobalSetup]
    public void Setup()
    {
        // Very large array (10k elements) - tests array parsing performance
        veryLargeArrayJson = GenerateVeryLargeArrayJson();

        // Deeply nested objects (10 levels) - tests nesting performance
        deeplyNestedJson = GenerateDeeplyNestedJson();

        // Object with many properties (200+) - tests property map efficiency
        manyPropertiesJson = GenerateManyPropertiesJson();

        // JSON with many escaped characters - tests string parsing performance
        escapeHeavyJson = GenerateEscapeHeavyJson();

        // JSON with many numbers of different formats - tests number parsing
        numberHeavyJson = GenerateNumberHeavyJson();

        // Byte version for memory comparison
        veryLargeArrayBytes = Encoding.UTF8.GetBytes(veryLargeArrayJson);
    }

    #region Very Large Array Benchmarks

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

    [Benchmark]
    public int ParseVeryLargeArrayFromBytesCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(veryLargeArrayBytes!);
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

    [Benchmark]
    public int ParseVeryLargeArrayFromBytesSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(veryLargeArrayBytes!);
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

    #endregion

    #region Deeply Nested Object Benchmarks

    [Benchmark]
    public string? ParseDeeplyNestedCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(deeplyNestedJson!);
        Corvus.Text.Json.JsonElement current = document.RootElement;

        // Navigate to the deepest level
        string? deepValue = null;
        for (int i = 0; i < 10; i++)
        {
            if (current.TryGetProperty($"level{i}", out Corvus.Text.Json.JsonElement nextLevel))
            {
                current = nextLevel;
            }
            else
            {
                break;
            }
        }

        if (current.TryGetProperty("value", out Corvus.Text.Json.JsonElement valueElement))
        {
            deepValue = valueElement.GetString();
        }

        return deepValue;
    }

    [Benchmark]
    public string? ParseDeeplyNestedSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(deeplyNestedJson!);
        System.Text.Json.JsonElement current = document.RootElement;

        // Navigate to the deepest level
        string? deepValue = null;
        for (int i = 0; i < 10; i++)
        {
            if (current.TryGetProperty($"level{i}", out System.Text.Json.JsonElement nextLevel))
            {
                current = nextLevel;
            }
            else
            {
                break;
            }
        }

        if (current.TryGetProperty("value", out System.Text.Json.JsonElement valueElement))
        {
            deepValue = valueElement.GetString();
        }

        return deepValue;
    }

    #endregion

    #region Many Properties Benchmarks

    [Benchmark]
    public (string?, string?, int) ParseManyPropertiesCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(manyPropertiesJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        // Trigger property map creation for efficient access
        root.EnsurePropertyMap();

        string? firstProperty = null;
        string? lastProperty = null;
        int accessedProperties = 0;

        // Access ALL properties using TryGetProperty to test property lookup performance
        for (int i = 0; i < 200; i++)
        {
            if (root.TryGetProperty($"property{i}", out Corvus.Text.Json.JsonElement element))
            {
                accessedProperties++;
                if (i == 0) firstProperty = element.GetString();
                if (i == 199) lastProperty = element.GetString();
            }
        }

        return (firstProperty, lastProperty, accessedProperties);
    }

    [Benchmark]
    public (string?, string?, int) ParseManyPropertiesSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(manyPropertiesJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        string? firstProperty = null;
        string? lastProperty = null;
        int accessedProperties = 0;

        // Access ALL properties using TryGetProperty to test property lookup performance
        for (int i = 0; i < 200; i++)
        {
            if (root.TryGetProperty($"property{i}", out System.Text.Json.JsonElement element))
            {
                accessedProperties++;
                if (i == 0) firstProperty = element.GetString();
                if (i == 199) lastProperty = element.GetString();
            }
        }

        return (firstProperty, lastProperty, accessedProperties);
    }

    #endregion

    #region Escape-Heavy String Benchmarks

    [Benchmark]
    public string? ParseEscapeHeavyCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(escapeHeavyJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        string? combinedText = null;
        if (root.TryGetProperty("escapedText", out Corvus.Text.Json.JsonElement textElement))
        {
            combinedText = textElement.GetString();
        }

        return combinedText;
    }

    [Benchmark]
    public string? ParseEscapeHeavySystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(escapeHeavyJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        string? combinedText = null;
        if (root.TryGetProperty("escapedText", out System.Text.Json.JsonElement textElement))
        {
            combinedText = textElement.GetString();
        }

        return combinedText;
    }

    #endregion

    #region Number-Heavy Benchmarks

    [Benchmark]
    public (double, double, int) ParseNumberHeavyCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(numberHeavyJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        double sum = 0.0;
        double max = double.MinValue;
        int count = 0;

        if (root.TryGetProperty("numbers", out Corvus.Text.Json.JsonElement numbersElement) &&
            numbersElement.ValueKind == Corvus.Text.Json.JsonValueKind.Array)
        {
            foreach (Corvus.Text.Json.JsonElement numberElement in numbersElement.EnumerateArray())
            {
                if (numberElement.ValueKind == Corvus.Text.Json.JsonValueKind.Number)
                {
                    double value = numberElement.GetDouble();
                    sum += value;
                    max = Math.Max(max, value);
                    count++;
                }
            }
        }

        return (sum, max, count);
    }

    [Benchmark]
    public (double, double, int) ParseNumberHeavySystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(numberHeavyJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        double sum = 0.0;
        double max = double.MinValue;
        int count = 0;

        if (root.TryGetProperty("numbers", out System.Text.Json.JsonElement numbersElement) &&
            numbersElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (System.Text.Json.JsonElement numberElement in numbersElement.EnumerateArray())
            {
                if (numberElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    double value = numberElement.GetDouble();
                    sum += value;
                    max = Math.Max(max, value);
                    count++;
                }
            }
        }

        return (sum, max, count);
    }

    #endregion

    #region JSON Generation Methods

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

    private static string GenerateDeeplyNestedJson()
    {
        var sb = new StringBuilder();

        // Build nested structure
        for (int i = 0; i < 10; i++)
        {
            sb.Append("{");
            sb.Append($"\"level{i}\":");
        }

        // Add the deepest value
        sb.Append("{\"value\":\"Deep nested value at level 10\"}");

        // Close all the braces
        for (int i = 0; i < 10; i++)
        {
            sb.Append("}");
        }

        return sb.ToString();
    }

    private static string GenerateManyPropertiesJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        for (int i = 0; i < 200; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendLine();
            sb.Append($"  \"property{i}\": \"{GenerateRandomString(10 + (i % 20))}\"");
        }

        sb.AppendLine();
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateEscapeHeavyJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.Append("  \"escapedText\": \"");

        // Generate text with many escape sequences
        for (int i = 0; i < 1000; i++)
        {
            switch (i % 8)
            {
                case 0:
                    sb.Append("Normal text ");
                    break;
                case 1:
                    sb.Append("\\\"Quoted\\\" ");
                    break;
                case 2:
                    sb.Append("\\n\\r\\t ");
                    break;
                case 3:
                    sb.Append("\\\\Backslash\\\\ ");
                    break;
                case 4:
                    sb.Append("\\u0048\\u0065\\u006C\\u006C\\u006F "); // "Hello" in Unicode
                    break;
                case 5:
                    sb.Append("\\/Forward slash ");
                    break;
                case 6:
                    sb.Append("\\b\\f ");
                    break;
                default:
                    sb.Append("Mixed\\nspecial\\tchars\\\"here\\\\ ");
                    break;
            }
        }

        sb.AppendLine("\"");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateNumberHeavyJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"numbers\": [");

        var random = new Random(42);
        for (int i = 0; i < 5000; i++)
        {
            if (i > 0) sb.Append(",");
            if (i % 50 == 0) sb.AppendLine();

            string number = (i % 6) switch
            {
                0 => i.ToString(),                                    // Integers
                1 => (i * Math.PI).ToString("F6"),                   // Regular decimals
                2 => (i * 1e-6).ToString("E"),                       // Scientific notation (small)
                3 => (i * 1e6).ToString("E"),                        // Scientific notation (large)
                4 => (-i * 123.456).ToString("F3"),                  // Negative numbers
                _ => (random.NextDouble() * 1000000).ToString("F8")   // Random high precision
            };

            sb.Append(number);
        }

        sb.AppendLine();
        sb.AppendLine("  ]");
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