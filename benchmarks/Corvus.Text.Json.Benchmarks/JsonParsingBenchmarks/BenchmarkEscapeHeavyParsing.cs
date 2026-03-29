// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace JsonParsingBenchmarks;

/// <summary>
/// Benchmark for parsing JSON with heavy string escaping.
/// Tests parsing performance when strings contain many escape sequences.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkEscapeHeavyParsing
{
    private string? escapeHeavyJson;

    [GlobalSetup]
    public void Setup()
    {
        // Escape-heavy JSON - tests string parsing performance
        escapeHeavyJson = GenerateEscapeHeavyJson();
    }

    [Benchmark]
    public int ParseEscapeHeavyCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(escapeHeavyJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        int stringCount = 0;

        if (root.ValueKind == Corvus.Text.Json.JsonValueKind.Object)
        {
            foreach (JsonProperty<Corvus.Text.Json.JsonElement> property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == Corvus.Text.Json.JsonValueKind.String)
                {
                    string? str = property.Value.GetString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        stringCount++;
                    }
                }
            }
        }

        return stringCount;
    }

    [Benchmark(Baseline = true)]
    public int ParseEscapeHeavySystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(escapeHeavyJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        int stringCount = 0;

        if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    string? str = property.Value.GetString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        stringCount++;
                    }
                }
            }
        }

        return stringCount;
    }

    #region JSON Generation

    private static string GenerateEscapeHeavyJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        string[] escapePatterns = new[]
        {
            "Line 1\\nLine 2\\nLine 3",
            "Tab\\tSeparated\\tValues",
            "Quote: \\\"Hello World\\\"",
            "Backslash: C:\\\\Program Files\\\\App",
            "Unicode: \\u0041\\u0042\\u0043",
            "Mixed: \\\"C:\\\\temp\\\\file.txt\\\"\\n\\tSize: 1024 bytes",
            "JSON in string: {\\\"key\\\": \\\"value\\\", \\\"num\\\": 42}",
            "Special chars: \\r\\n\\t\\b\\f\\\"\\\\",
            "Path: \\\"D:\\\\data\\\\file\\u0020name.json\\\"",
            "URL: \\\"https:\\/\\/example.com\\/path?param=value\\\"",
        };

        for (int i = 0; i < 100; i++)
        {
            if (i > 0) sb.AppendLine(",");

            string pattern = escapePatterns[i % escapePatterns.Length];
            sb.Append($"  \"field_{i}\": \"{pattern}_{i}\"");
        }

        sb.AppendLine();
        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion
}