// <copyright file="SubscribePipelineBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using AsyncApiBenchmark.Infrastructure;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace AsyncApiBenchmark;

/// <summary>
/// End-to-end receive/subscribe benchmarks comparing the Corvus transport
/// subscribe pipeline against what a developer would write with raw STJ.
/// </summary>
/// <remarks>
/// <para>
/// The baseline represents a hand-written NATS subscriber: receive bytes,
/// deserialize via STJ into a POCO, then call a handler with the result.
/// This is the floor — what you'd write with no framework at all.
/// </para>
/// <para>
/// Corvus benchmarks measure the full pipeline: bytes → parse → (optional validate) → dispatch.
/// </para>
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

    [Benchmark(Description = "Raw STJ: Deserialize + handle (no validation)", Baseline = true)]
    public double RawNats_DeserializeAndHandle()
    {
        // This is what a hand-written subscriber does: deserialize then use the result
        LightMeasuredPoco result = RawNatsBaseline.Subscribe<LightMeasuredPoco>(ValidPayloadBytes);
        return result.Id + result.Lumens;
    }

    [Benchmark(Description = "Corvus: Parse + access (no validation)")]
    public double Corvus_NoValidation()
    {
        using ParsedJsonDocument<LightMeasuredPayload> doc =
            ParsedJsonDocument<LightMeasuredPayload>.Parse(ValidPayloadBytes);

        LightMeasuredPayload payload = doc.RootElement;
        return (long)payload.Id + (double)payload.Lumens;
    }

    [Benchmark(Description = "Corvus: Parse + basic validation + access")]
    public double Corvus_WithBasicValidation()
    {
        using ParsedJsonDocument<LightMeasuredPayload> doc =
            ParsedJsonDocument<LightMeasuredPayload>.Parse(ValidPayloadBytes);

        _ = doc.RootElement.EvaluateSchema();
        return (long)doc.RootElement.Id + (double)doc.RootElement.Lumens;
    }

    [Benchmark(Description = "Corvus: Parse + detailed validation + access")]
    public double Corvus_WithDetailedValidation()
    {
        using ParsedJsonDocument<LightMeasuredPayload> doc =
            ParsedJsonDocument<LightMeasuredPayload>.Parse(ValidPayloadBytes);

        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        _ = doc.RootElement.EvaluateSchema(collector);
        return (long)doc.RootElement.Id + (double)doc.RootElement.Lumens;
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