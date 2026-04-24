// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;

namespace JsonParsingBenchmarks;

/// <summary>
/// Measures property lookup performance in isolation — parsing and property map
/// construction happen in GlobalSetup so the benchmark measures ONLY the hash
/// lookup loop in the PropertyMap.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkPropertyMapLookupOnly
{
    // Pre-parsed documents — kept alive across iterations
    private Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>? corvusDoc;
    private Corvus.Text.Json.JsonElement corvusRoot;

    private System.Text.Json.JsonDocument? stjDoc;
    private System.Text.Json.JsonElement stjRoot;

    // Pre-allocated property names (both string and UTF-8)
    private string[] propertyNames = null!;
    private byte[][] utf8PropertyNames = null!;

    // Lookup targets: first, middle, last, and non-existent property
    private string[] lookupTargets = null!;

    [Params(20, 100, 500)]
    public int PropertyCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string json = GenerateObjectJson(PropertyCount);

        // Pre-parse Corvus document and build property map
        corvusDoc = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(json);
        corvusRoot = corvusDoc.RootElement;
        corvusRoot.EnsurePropertyMap();

        // Pre-parse STJ document
        stjDoc = System.Text.Json.JsonDocument.Parse(json);
        stjRoot = stjDoc.RootElement;

        // Pre-allocate property names
        propertyNames = new string[PropertyCount];
        utf8PropertyNames = new byte[PropertyCount][];
        for (int i = 0; i < PropertyCount; i++)
        {
            propertyNames[i] = $"prop_{i:D4}";
            utf8PropertyNames[i] = Encoding.UTF8.GetBytes(propertyNames[i]);
        }

        // Lookup targets: spread across the hash table to exercise different chains
        lookupTargets =
        [
            propertyNames[0],                       // first
            propertyNames[PropertyCount / 4],       // quarter
            propertyNames[PropertyCount / 2],       // middle
            propertyNames[PropertyCount * 3 / 4],   // three-quarter
            propertyNames[PropertyCount - 1],       // last
        ];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        corvusDoc?.Dispose();
        stjDoc?.Dispose();
    }

    [Benchmark]
    public int LookupAllPropertiesCorvus()
    {
        int found = 0;
        Corvus.Text.Json.JsonElement root = corvusRoot;

        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (root.TryGetProperty(propertyNames[i], out _))
            {
                found++;
            }
        }

        return found;
    }

    [Benchmark(Baseline = true)]
    public int LookupAllPropertiesSystemTextJson()
    {
        int found = 0;
        System.Text.Json.JsonElement root = stjRoot;

        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (root.TryGetProperty(propertyNames[i], out _))
            {
                found++;
            }
        }

        return found;
    }

    [Benchmark]
    public int LookupSpreadCorvus()
    {
        int found = 0;
        Corvus.Text.Json.JsonElement root = corvusRoot;

        // 100 iterations over 5 spread-out property names
        for (int iter = 0; iter < 100; iter++)
        {
            for (int j = 0; j < lookupTargets.Length; j++)
            {
                if (root.TryGetProperty(lookupTargets[j], out _))
                {
                    found++;
                }
            }
        }

        return found;
    }

    [Benchmark]
    public int LookupSpreadSystemTextJson()
    {
        int found = 0;
        System.Text.Json.JsonElement root = stjRoot;

        // 100 iterations over 5 spread-out property names
        for (int iter = 0; iter < 100; iter++)
        {
            for (int j = 0; j < lookupTargets.Length; j++)
            {
                if (root.TryGetProperty(lookupTargets[j], out _))
                {
                    found++;
                }
            }
        }

        return found;
    }

    [Benchmark]
    public bool LookupMissCorvus()
    {
        // Look up a property that doesn't exist — exercises full chain traversal
        return corvusRoot.TryGetProperty("nonexistent_property_xyz", out _);
    }

    [Benchmark]
    public bool LookupMissSystemTextJson()
    {
        return stjRoot.TryGetProperty("nonexistent_property_xyz", out _);
    }

    #region JSON Generation

    private static string GenerateObjectJson(int propertyCount)
    {
        var sb = new StringBuilder();
        sb.Append('{');

        for (int i = 0; i < propertyCount; i++)
        {
            if (i > 0) sb.Append(',');

            string value = (i % 5) switch
            {
                0 => (i * 1.234).ToString("F3"),
                1 => $"\"{i:D4}_value\"",
                2 => (i % 2 == 0).ToString().ToLowerInvariant(),
                3 => "null",
                _ => $"[{i},{i + 1}]",
            };

            sb.Append($"\"prop_{i:D4}\":{value}");
        }

        sb.Append('}');
        return sb.ToString();
    }

    #endregion
}
