// <copyright file="RequestReplyBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using AsyncApiBenchmark.Generated;
using AsyncApiBenchmark.Infrastructure;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;
using Wolverine;

namespace AsyncApiBenchmark;

/// <summary>
/// Request/reply benchmarks comparing the generated Corvus producer
/// against Wolverine framework dispatch.
/// </summary>
/// <remarks>
/// <para>
/// Request/reply adds correlation cost on top of simple publish: the generated producer
/// creates a correlation ID, sends a request, and waits for a correlated reply.
/// The <see cref="BenchmarkTransport"/> returns a pre-built reply immediately (no I/O),
/// isolating pipeline overhead.
/// </para>
/// <para>
/// Two comparison tiers:
/// <list type="bullet">
/// <item><description>Wolverine — a real message bus (baseline): dispatch request with correlation header, handler returns reply</description></item>
/// <item><description>Corvus — generated <see cref="RequestDimProducer"/>: full request/reply pipeline with validation and correlation</description></item>
/// </list>
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class RequestReplyBenchmarks
{
    private static readonly byte[] ReplyPayloadBytes = Encoding.UTF8.GetBytes(
        """{"streetlightId":42,"status":"ok","appliedPercentage":75}""");

    private BenchmarkTransport transport = null!;
    private RequestDimProducer producerNoValidation = null!;
    private RequestDimProducer producerBasicValidation = null!;
    private DimCommandPayload corvusPayload;
    private CorrelatedHeaders corvusHeaders;
    private LightMeasuredRequestPoco wolverineRequestPoco = null!;
    private WolverineBaseline wolverine = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        this.transport = new BenchmarkTransport();
        this.transport.SetReplyPayload<DimResponsePayload>(ReplyPayloadBytes);

        this.producerNoValidation = new RequestDimProducer(
            this.transport, ValidationMode.None);
        this.producerBasicValidation = new RequestDimProducer(
            this.transport, ValidationMode.Basic);

        this.corvusPayload = DimCommandPayload.ParseValue(
            """{"streetlightId":42,"percentage":75,"sentAt":"2026-05-25T10:30:00Z"}"""u8);
        this.corvusHeaders = CorrelatedHeaders.ParseValue(
            """{"correlationId":"corr-12345678-abcd-ef01"}"""u8);

        this.wolverineRequestPoco = new LightMeasuredRequestPoco
        {
            Id = 42,
            Lumens = 512.7,
            SentAt = "2026-05-25T10:30:00Z",
        };

        this.wolverine = new WolverineBaseline();
        await this.wolverine.StartAsync().ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await this.wolverine.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "Wolverine: serialize + dispatch request/reply with correlation", Baseline = true)]
    public async Task<LightMeasuredPoco> Wolverine_RequestReply()
    {
        _ = JsonSerializer.SerializeToUtf8Bytes(this.wolverineRequestPoco, WolverineBaseline.SerializerOptions);
        return await this.wolverine.InvokeAsync<LightMeasuredPoco>(
            this.wolverineRequestPoco,
            new DeliveryOptions().WithHeader("correlationId", "corr-12345678-abcd-ef01")).ConfigureAwait(false);
    }

    [Benchmark(Description = "Corvus: Generated producer request/reply (no validation)")]
    public async ValueTask<DimResponsePayload> Corvus_RequestReply_NoValidation()
    {
        return await this.producerNoValidation.SendAndReceiveDimCommandAsync(
            this.corvusPayload, this.corvusHeaders);
    }

    [Benchmark(Description = "Corvus: Generated producer request/reply (basic validation)")]
    public async ValueTask<DimResponsePayload> Corvus_RequestReply_WithValidation()
    {
        return await this.producerBasicValidation.SendAndReceiveDimCommandAsync(
            this.corvusPayload, this.corvusHeaders);
    }
}