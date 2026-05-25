// <copyright file="SubscribePipelineBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using AsyncApiBenchmark.Infrastructure;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace AsyncApiBenchmark;

/// <summary>
/// End-to-end receive/subscribe benchmarks comparing Corvus transport
/// subscribe (parse + validate + dispatch) against raw STJ deserialization.
/// </summary>
/// <remarks>
/// Measures the receive pipeline: incoming bytes → typed payload → handler dispatch.
/// </remarks>
[MemoryDiagnoser]
public class SubscribePipelineBenchmarks
{
    private static readonly byte[] ValidPayloadBytes = Encoding.UTF8.GetBytes(
        """{"id":42,"lumens":512.7,"sentAt":"2026-05-25T10:30:00Z"}""");

    private static readonly byte[] HeadersBytes = Encoding.UTF8.GetBytes(
        """{"x-correlation-id":"abc-123","x-source":"sensor-42"}""");

    private BenchmarkTransport transport = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        this.transport = new BenchmarkTransport();

        // Register a typed subscriber
        await this.transport.SubscribeAsync<LightMeasuredPayload>(
            "streetlights/1/0/event/lighting/measured"u8.ToArray(),
            (payload, headers, ct) =>
            {
                // Simulate accessing properties (forces parse)
                _ = payload.Id;
                _ = payload.Lumens;
                return ValueTask.CompletedTask;
            });
    }

    [Benchmark(Description = "Raw STJ: Deserialize only", Baseline = true)]
    public int RawNats_NoValidation()
    {
        LightMeasuredPoco result = RawNatsBaseline.Subscribe<LightMeasuredPoco>(ValidPayloadBytes);
        return result.Id;
    }

    [Benchmark(Description = "Corvus: Parse + access (no validation)")]
    public double Corvus_NoValidation()
    {
        using ParsedJsonDocument<LightMeasuredPayload> doc =
            ParsedJsonDocument<LightMeasuredPayload>.Parse(ValidPayloadBytes);

        LightMeasuredPayload payload = doc.RootElement;
        return (long)payload.Id + (double)payload.Lumens;
    }

    [Benchmark(Description = "Corvus: Parse + basic validation")]
    public bool Corvus_WithBasicValidation()
    {
        using ParsedJsonDocument<LightMeasuredPayload> doc =
            ParsedJsonDocument<LightMeasuredPayload>.Parse(ValidPayloadBytes);

        return doc.RootElement.EvaluateSchema();
    }

    [Benchmark(Description = "Corvus: Parse + detailed validation")]
    public bool Corvus_WithDetailedValidation()
    {
        using ParsedJsonDocument<LightMeasuredPayload> doc =
            ParsedJsonDocument<LightMeasuredPayload>.Parse(ValidPayloadBytes);

        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        return doc.RootElement.EvaluateSchema(collector);
    }

    [Benchmark(Description = "Corvus: Full pipeline (parse + deliver + handler)")]
    public async ValueTask Corvus_FullPipeline()
    {
        await this.transport.DeliverAsync(ValidPayloadBytes);
    }

    [Benchmark(Description = "Corvus: Full pipeline with headers")]
    public async ValueTask Corvus_FullPipelineWithHeaders()
    {
        await this.transport.DeliverAsync(ValidPayloadBytes, HeadersBytes);
    }
}