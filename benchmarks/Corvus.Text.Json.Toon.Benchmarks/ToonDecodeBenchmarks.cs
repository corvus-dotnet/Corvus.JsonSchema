// <copyright file="ToonDecodeBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Corvus.Text.Json;
using CtjJsonWriterOptions = Corvus.Text.Json.JsonWriterOptions;
using CtjToonDocument = Corvus.Text.Json.Toon.ToonDocument;
using CtjToonKeyFolding = Corvus.Text.Json.Toon.ToonKeyFolding;
using CtjToonWriterOptions = Corvus.Text.Json.Toon.ToonWriterOptions;
using CtjUtf8JsonWriter = Corvus.Text.Json.Utf8JsonWriter;

namespace Corvus.Text.Json.Toon.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Orderer(SummaryOrderPolicy.Default)]
public class ToonDecodeBenchmarks
{
    private static readonly CtjToonWriterOptions CtjWriterOptions = new()
    {
        KeyFolding = CtjToonKeyFolding.Safe,
        FlattenDepth = 2,
    };

    private static readonly CtjJsonWriterOptions JsonWriterOptions = new()
    {
        SkipValidation = true,
    };

    private string ctjToonString = null!;
    private byte[] ctjToonUtf8Bytes = null!;
    private PooledUtf8Buffer jsonOutput = null!;
    private CtjUtf8JsonWriter jsonWriter = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(ScenarioData.CreateCysharpEncodeBenchmarkJson());

        using ParsedJsonDocument<Benchmark.CorvusTextJson.CysharpPersonArray> generatedPersonArrayDocument =
            ParsedJsonDocument<Benchmark.CorvusTextJson.CysharpPersonArray>.Parse(jsonBytes);
        using PooledUtf8Buffer toonOutput = new(jsonBytes.Length);

        Benchmark.CorvusTextJson.CysharpPersonArray generatedElement = generatedPersonArrayDocument.RootElement;
        this.ctjToonString = CtjToonDocument.ConvertToToonString(in generatedElement, CtjWriterOptions);
        CtjToonDocument.ConvertToToon(in generatedElement, toonOutput, CtjWriterOptions);
        this.ctjToonUtf8Bytes = toonOutput.ToArray();

        this.jsonOutput = new(jsonBytes.Length);
        this.jsonWriter = new(this.jsonOutput, JsonWriterOptions);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.jsonWriter.Dispose();
        this.jsonOutput.Dispose();
    }

    [BenchmarkCategory("CorvusDecode")]
    [Benchmark(Baseline = true)]
    public string CorvusTextJson_FromGeneratedCysharpPersonArrayToonString()
    {
        return CtjToonDocument.ConvertToJsonString(this.ctjToonString);
    }

    [BenchmarkCategory("CorvusDecode")]
    [Benchmark]
    public int CorvusTextJson_FromGeneratedCysharpPersonArrayToonUtf8Buffer()
    {
        this.jsonOutput.Clear();
        this.jsonWriter.Reset(this.jsonOutput);
        CtjToonDocument.Convert(this.ctjToonUtf8Bytes, this.jsonWriter);
        this.jsonWriter.Flush();
        return this.jsonOutput.WrittenCount;
    }
}