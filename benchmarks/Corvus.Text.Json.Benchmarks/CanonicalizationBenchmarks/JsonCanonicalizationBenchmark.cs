// <copyright file="JsonCanonicalizationBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Canonicalization;

namespace Corvus.Text.Json.Benchmarks.CanonicalizationBenchmarks;

/// <summary>
/// Benchmarks for RFC 8785 JSON Canonicalization Scheme (JCS).
/// No mainstream .NET JCS library exists for comparison, so this
/// measures Corvus V5 standalone performance.
/// </summary>
[MemoryDiagnoser]
public class JsonCanonicalizationBenchmark
{
    private const string InputJson = """
        {
            "peach": "This sorting order",
            "péché": "is determined by",
            "pêche": "Unicode code units",
            "sin": "and target",
            "i": 1,
            "numbers": [333333333.33333329, 1e30, 4.50, 2e-3, 0.000000000000000000000000001],
            "a": { "z": 1, "a": 2, "m": 3 },
            "b": [true, false, null]
        }
        """;

    private ParsedJsonDocument<JsonElement>? parsedDoc;
    private byte[]? outputBuffer;

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.parsedDoc = ParsedJsonDocument<JsonElement>.Parse(InputJson);

        // Pre-allocate a buffer large enough for the canonical output.
        this.outputBuffer = new byte[4096];
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.parsedDoc?.Dispose();
    }

    /// <summary>
    /// Canonicalize using Corvus V5.
    /// </summary>
    [Benchmark]
    public int CorvusV5()
    {
        JsonCanonicalizer.TryCanonicalize(this.parsedDoc!.RootElement, this.outputBuffer, out int bytesWritten);
        return bytesWritten;
    }
}
