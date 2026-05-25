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
/// subscribe pipeline against what a developer would write with raw STJ,
/// and against Wolverine (a popular .NET message bus framework).
/// </summary>
/// <remarks>
/// <para>
/// Three comparison tiers:
/// <list type="bullet">
/// <item><description>Raw STJ — the floor: deserialize + access properties (no framework)</description></item>
/// <item><description>Wolverine — a real message bus: STJ deserialize + framework dispatch + handler</description></item>
/// <item><description>Corvus — our pipeline: parse + optional validate + dispatch + handler</description></item>
/// </list>
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
    private WolverineBaseline wolverine = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        this.transport = new BenchmarkTransport();
        this.wolverine = new WolverineBaseline();
        await this.wolverine.StartAsync().ConfigureAwait(false);

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

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await this.wolverine.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "Raw STJ: Deserialize + handle (no validation)", Baseline = true)]
    public double RawNats_DeserializeAndHandle()
    {
        // This is what a hand-written subscriber does: deserialize then use the result
        LightMeasuredPoco result = RawNatsBaseline.Subscribe<LightMeasuredPoco>(ValidPayloadBytes);
        return result.Id + result.Lumens;
    }

    [Benchmark(Description = "Wolverine: STJ deserialize + framework dispatch")]
    public async Task Wolverine_DeserializeAndDispatch()
    {
        // Real message bus cost: STJ deserialize + Wolverine handler dispatch
        LightMeasuredPoco message = RawNatsBaseline.Subscribe<LightMeasuredPoco>(ValidPayloadBytes);
        await this.wolverine.InvokeAsync(message).ConfigureAwait(false);
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