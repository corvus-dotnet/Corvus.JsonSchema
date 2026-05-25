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
/// End-to-end publish benchmarks comparing Corvus transport publish against
/// raw NATS.Net + System.Text.Json serialization.
/// </summary>
/// <remarks>
/// Measures what the Corvus abstraction costs compared to hand-written broker code.
/// All transport I/O is mocked — we measure only serialization + pipeline overhead.
/// </remarks>
[MemoryDiagnoser]
public class PublishPipelineBenchmarks
{
    private static readonly byte[] ValidPayloadBytes = Encoding.UTF8.GetBytes(
        """{"id":42,"lumens":512.7,"sentAt":"2026-05-25T10:30:00Z"}""");

    private static readonly ReadOnlyMemory<byte> ChannelUtf8 =
        "streetlights/1/0/event/lighting/measured"u8.ToArray();

    private BenchmarkTransport transport = null!;
    private LightMeasuredPayload corvusPayload;
    private LightMeasuredPoco natsPayload = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.transport = new BenchmarkTransport();

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

    [Benchmark(Description = "Corvus: Publish (no validation)")]
    public async ValueTask<int> Corvus_NoValidation()
    {
        await this.transport.PublishAsync(
            ChannelUtf8,
            in this.corvusPayload);

        return this.transport.LastPublishBytesWritten;
    }

    [Benchmark(Description = "Corvus: Publish (basic validation)")]
    public async ValueTask<int> Corvus_WithBasicValidation()
    {
        // Validate before publishing
        bool isValid = this.corvusPayload.EvaluateSchema();
        if (!isValid)
        {
            return 0;
        }

        await this.transport.PublishAsync(
            ChannelUtf8,
            in this.corvusPayload);

        return this.transport.LastPublishBytesWritten;
    }

    [Benchmark(Description = "Corvus: Publish (detailed validation)")]
    public async ValueTask<int> Corvus_WithDetailedValidation()
    {
        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        bool isValid = this.corvusPayload.EvaluateSchema(collector);
        if (!isValid)
        {
            return 0;
        }

        await this.transport.PublishAsync(
            ChannelUtf8,
            in this.corvusPayload);

        return this.transport.LastPublishBytesWritten;
    }

    [Benchmark(Description = "Corvus: Publish with headers")]
    public async ValueTask<int> Corvus_WithHeaders()
    {
        using ParsedJsonDocument<JsonElement> headerDoc =
            ParsedJsonDocument<JsonElement>.Parse(
                """{"x-correlation-id":"abc-123","x-source":"sensor-42"}"""u8.ToArray());

        await this.transport.PublishAsync(
            ChannelUtf8,
            in this.corvusPayload,
            headerDoc.RootElement);

        return this.transport.LastPublishBytesWritten;
    }
}