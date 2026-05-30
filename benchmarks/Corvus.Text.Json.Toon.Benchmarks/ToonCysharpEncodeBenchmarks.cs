// <copyright file="ToonCysharpEncodeBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Corvus.Text.Json;
using CtjToonDocument = Corvus.Text.Json.Toon.ToonDocument;
using CtjToonKeyFolding = Corvus.Text.Json.Toon.ToonKeyFolding;
using CtjToonWriterOptions = Corvus.Text.Json.Toon.ToonWriterOptions;

namespace Corvus.Text.Json.Toon.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[Orderer(SummaryOrderPolicy.Default)]
public class ToonCysharpEncodeBenchmarks
{
    private static readonly CtjToonWriterOptions CtjWriterOptions = new()
    {
        KeyFolding = CtjToonKeyFolding.Safe,
        FlattenDepth = 2,
    };

    private byte[] jsonBytes = null!;
    private System.Text.Json.JsonDocument stjJsonDocument = null!;
    private ParsedJsonDocument<Benchmark.CorvusTextJson.CysharpPersonArray> generatedPersonArrayDocument = null!;
    private PooledUtf8Buffer cysharpToonUtf8Output = null!;
    private PooledUtf8Buffer ctjToonUtf8Output = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.jsonBytes = Encoding.UTF8.GetBytes(ScenarioData.CreateCysharpEncodeBenchmarkJson());
        this.stjJsonDocument = System.Text.Json.JsonDocument.Parse(this.jsonBytes);
        this.generatedPersonArrayDocument = ParsedJsonDocument<Benchmark.CorvusTextJson.CysharpPersonArray>.Parse(this.jsonBytes);
        this.cysharpToonUtf8Output = new(this.jsonBytes.Length);
        this.ctjToonUtf8Output = new(this.jsonBytes.Length);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.stjJsonDocument.Dispose();
        this.generatedPersonArrayDocument.Dispose();
        this.cysharpToonUtf8Output.Dispose();
        this.ctjToonUtf8Output.Dispose();
    }

    [BenchmarkCategory("CysharpExactEncode")]
    [Benchmark(Baseline = true)]
    public string ToonEncoder_EncodeStjJsonElementString()
    {
        System.Text.Json.JsonElement element = this.stjJsonDocument.RootElement;
        return Cysharp.AI.ToonEncoder.Encode(element);
    }

    [BenchmarkCategory("CysharpExactEncode")]
    [Benchmark]
    public int ToonEncoder_EncodeStjJsonElementUtf8Buffer()
    {
        this.cysharpToonUtf8Output.Clear();
        System.Text.Json.JsonElement element = this.stjJsonDocument.RootElement;
        Cysharp.AI.ToonEncoder.Encode(this.cysharpToonUtf8Output, element);
        return this.cysharpToonUtf8Output.WrittenCount;
    }

    [BenchmarkCategory("CysharpExactEncode")]
    [Benchmark]
    public string ToonEncoder_EncodeAsTabularArrayStjJsonElementString()
    {
        System.Text.Json.JsonElement element = this.stjJsonDocument.RootElement;
        return Cysharp.AI.ToonEncoder.EncodeAsTabularArray(element);
    }

    [BenchmarkCategory("CysharpExactEncode")]
    [Benchmark]
    public int ToonEncoder_EncodeAsTabularArrayStjJsonElementUtf8Buffer()
    {
        this.cysharpToonUtf8Output.Clear();
        System.Text.Json.JsonElement element = this.stjJsonDocument.RootElement;
        Cysharp.AI.ToonEncoder.EncodeAsTabularArray(this.cysharpToonUtf8Output, element);
        return this.cysharpToonUtf8Output.WrittenCount;
    }

    [BenchmarkCategory("CysharpExactEncode")]
    [Benchmark]
    public string CorvusTextJson_FromGeneratedCysharpPersonArray()
    {
        Benchmark.CorvusTextJson.CysharpPersonArray element = this.generatedPersonArrayDocument.RootElement;
        return CtjToonDocument.ConvertToToonString(in element, CtjWriterOptions);
    }

    [BenchmarkCategory("CysharpExactEncode")]
    [Benchmark]
    public int CorvusTextJson_FromGeneratedCysharpPersonArrayUtf8Buffer()
    {
        this.ctjToonUtf8Output.Clear();
        Benchmark.CorvusTextJson.CysharpPersonArray element = this.generatedPersonArrayDocument.RootElement;
        CtjToonDocument.ConvertToToon(in element, this.ctjToonUtf8Output, CtjWriterOptions);
        return this.ctjToonUtf8Output.WrittenCount;
    }
}