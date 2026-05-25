// <copyright file="ValidationPipelineBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using AsyncApiBenchmark.Infrastructure;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace AsyncApiBenchmark;

/// <summary>
/// Compares Corvus compiled validation (None/Basic/Detailed) against
/// JsonSchema.Net interpreted validation (Flag/List) for a typical
/// IoT sensor message payload.
/// </summary>
/// <remarks>
/// This directly answers: "How much does Corvus compiled validation cost
/// compared to JsonSchema.Net interpreted validation?"
/// </remarks>
[MemoryDiagnoser]
public class ValidationPipelineBenchmarks
{
    private static readonly byte[] ValidPayloadBytes = Encoding.UTF8.GetBytes(
        """{"id":42,"lumens":512.7,"sentAt":"2026-05-25T10:30:00Z"}""");

    private static readonly string SchemaJson = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "TestData", "light-measured-payload.json"));

    private JsonSchemaNetBaseline jsonSchemaNet = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.jsonSchemaNet = new JsonSchemaNetBaseline(SchemaJson);
    }

    [Benchmark(Description = "Corvus: Parse only (no validation)", Baseline = true)]
    public long Corvus_None()
    {
        using ParsedJsonDocument<LightMeasuredPayload> doc =
            ParsedJsonDocument<LightMeasuredPayload>.Parse(ValidPayloadBytes);

        // Access properties to simulate real use
        LightMeasuredPayload payload = doc.RootElement;
        return (long)payload.Id + (long)payload.Lumens;
    }

    [Benchmark(Description = "Corvus: Basic validation (boolean)")]
    public bool Corvus_Basic()
    {
        using ParsedJsonDocument<LightMeasuredPayload> doc =
            ParsedJsonDocument<LightMeasuredPayload>.Parse(ValidPayloadBytes);

        return doc.RootElement.EvaluateSchema();
    }

    [Benchmark(Description = "Corvus: Detailed validation (error collection)")]
    public bool Corvus_Detailed()
    {
        using ParsedJsonDocument<LightMeasuredPayload> doc =
            ParsedJsonDocument<LightMeasuredPayload>.Parse(ValidPayloadBytes);

        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        return doc.RootElement.EvaluateSchema(collector);
    }

    [Benchmark(Description = "JsonSchema.Net: Flag mode (interpreted)")]
    public bool JsonSchemaNet_Flag()
    {
        return this.jsonSchemaNet.EvaluateFlag(ValidPayloadBytes);
    }

    [Benchmark(Description = "JsonSchema.Net: List mode (interpreted, full errors)")]
    public bool JsonSchemaNet_List()
    {
        return this.jsonSchemaNet.EvaluateList(ValidPayloadBytes);
    }
}