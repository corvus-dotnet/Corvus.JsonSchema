// <copyright file="PublishPipelineBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using AsyncApiBenchmark.Infrastructure;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;

namespace AsyncApiBenchmark;

/// <summary>
/// End-to-end publish benchmarks comparing the generated Corvus producer
/// against raw NATS.Net + System.Text.Json serialization.
/// </summary>
/// <remarks>
/// <para>
/// The Corvus benchmarks exercise the actual generated
/// <see cref="PublishLightMeasurementProducer"/> code path: construct typed payload →
/// validate → serialize → transport publish. This is the real generated code, not a simulation.
/// </para>
/// <para>
/// Measures what the Corvus abstraction costs compared to hand-written broker code.
/// All transport I/O is mocked — we measure only serialization + pipeline overhead.
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
    private LightMeasuredPoco natsPayload = null!;

    [GlobalSetup]
    public void Setup()
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

        // POCO equivalent for raw baseline
        this.natsPayload = new LightMeasuredPoco
        {
            Id = 42,
            Lumens = 512.7,
            SentAt = "2026-05-25T10:30:00Z",
        };
    }

    [Benchmark(Description = "Raw NATS + STJ (no validation)", Baseline = true)]
    public int RawNats_NoValidation()
    {
        return RawNatsBaseline.Publish(in this.natsPayload);
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