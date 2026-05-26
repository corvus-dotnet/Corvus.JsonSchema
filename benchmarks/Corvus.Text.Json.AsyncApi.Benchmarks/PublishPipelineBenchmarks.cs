// <copyright file="PublishPipelineBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using AsyncApiBenchmark.Generated;
using AsyncApiBenchmark.Infrastructure;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;

namespace AsyncApiBenchmark;

/// <summary>
/// End-to-end publish benchmarks comparing the generated Corvus producer
/// against Wolverine framework dispatch.
/// </summary>
/// <remarks>
/// <para>
/// The Corvus benchmarks exercise the actual generated
/// <see cref="PublishLightMeasurementProducer"/> code path: construct typed payload →
/// validate → serialize → transport publish. This is the real generated code, not a simulation.
/// </para>
/// <para>
/// Two comparison tiers:
/// <list type="bullet">
/// <item><description>Wolverine — a real message bus (baseline): framework dispatch + handler chain + context pooling</description></item>
/// <item><description>Corvus — generated producer: workspace + channel build + validate + serialize</description></item>
/// </list>
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class PublishPipelineBenchmarks
{
    private static readonly byte[] ValidPayloadBytes = Encoding.UTF8.GetBytes(
        """{"id":42,"lumens":512.7,"sentAt":"2026-05-25T10:30:00Z"}""");

    private BenchmarkTransport transport = null!;
    private PublishLightMeasurementProducer producerNoValidation = null!;
    private PublishLightMeasurementProducer producerBasicValidation = null!;
    private PublishLightMeasurementProducer producerDetailedValidation = null!;
    private LightMeasuredPayload corvusPayload;
    private LightMeasuredPoco wolverinePayload = null!;
    private WolverineBaseline wolverine = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        this.transport = new BenchmarkTransport();

        // Create producers with different validation modes — all using the same transport.
        this.producerNoValidation = new PublishLightMeasurementProducer(
            this.transport, ValidationMode.None);
        this.producerBasicValidation = new PublishLightMeasurementProducer(
            this.transport, ValidationMode.Basic);
        this.producerDetailedValidation = new PublishLightMeasurementProducer(
            this.transport, ValidationMode.Detailed);

        // Parse the Corvus typed payload once (re-used across iterations)
        this.corvusPayload = LightMeasuredPayload.ParseValue(ValidPayloadBytes);

        // POCO equivalent for Wolverine baseline
        this.wolverinePayload = new LightMeasuredPoco
        {
            Id = 42,
            Lumens = 512.7,
            SentAt = "2026-05-25T10:30:00Z",
        };

        // Wolverine baseline
        this.wolverine = new WolverineBaseline();
        await this.wolverine.StartAsync().ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await this.wolverine.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "Wolverine: serialize + framework dispatch (baseline)", Baseline = true)]
    public async Task Wolverine_Publish()
    {
        _ = JsonSerializer.SerializeToUtf8Bytes(this.wolverinePayload, WolverineBaseline.SerializerOptions);
        await this.wolverine.InvokeAsync(this.wolverinePayload).ConfigureAwait(false);
    }

    [Benchmark(Description = "Corvus: Generated producer (no validation)")]
    public async ValueTask Corvus_NoValidation()
    {
        await this.producerNoValidation.PublishLightMeasuredAsync(this.corvusPayload);
    }

    [Benchmark(Description = "Corvus: Generated producer (basic validation)")]
    public async ValueTask Corvus_WithBasicValidation()
    {
        await this.producerBasicValidation.PublishLightMeasuredAsync(this.corvusPayload);
    }

    [Benchmark(Description = "Corvus: Generated producer (detailed validation)")]
    public async ValueTask Corvus_WithDetailedValidation()
    {
        await this.producerDetailedValidation.PublishLightMeasuredAsync(this.corvusPayload);
    }
}