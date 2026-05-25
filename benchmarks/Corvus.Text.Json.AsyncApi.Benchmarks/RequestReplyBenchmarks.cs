// <copyright file="RequestReplyBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using AsyncApiBenchmark.Infrastructure;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace AsyncApiBenchmark;

/// <summary>
/// Request/reply correlation overhead benchmarks.
/// Measures the cost of serializing a request, parsing a pre-built reply,
/// and correlating the two.
/// </summary>
[MemoryDiagnoser]
public class RequestReplyBenchmarks
{
    private static readonly byte[] RequestPayloadBytes = Encoding.UTF8.GetBytes(
        """{"id":42,"lumens":512.7,"sentAt":"2026-05-25T10:30:00Z"}""");

    private static readonly byte[] ReplyPayloadBytes = Encoding.UTF8.GetBytes(
        """{"id":42,"lumens":512.7,"sentAt":"2026-05-25T10:30:00Z"}""");

    private static readonly ReadOnlyMemory<byte> RequestChannelUtf8 =
        "streetlights/1/0/action/dim"u8.ToArray();

    private static readonly ReadOnlyMemory<byte> ReplyChannelUtf8 =
        "streetlights/1/0/action/dim/reply"u8.ToArray();

    private static readonly ReadOnlyMemory<byte> CorrelationIdUtf8 =
        "corr-12345678-abcd-ef01"u8.ToArray();

    private BenchmarkTransport transport = null!;
    private LightMeasuredPayload corvusRequest;

    [GlobalSetup]
    public void Setup()
    {
        this.transport = new BenchmarkTransport();
        this.transport.SetReplyPayload(ReplyPayloadBytes);

        this.corvusRequest = LightMeasuredPayload.ParseValue(RequestPayloadBytes);
    }

    [Benchmark(Description = "Raw STJ: Serialize request + Deserialize reply", Baseline = true)]
    public int RawNats_RequestReply()
    {
        // Publish-side: serialize request
        int published = RawNatsBaseline.Publish(new LightMeasuredPoco
        {
            Id = 42,
            Lumens = 512.7,
            SentAt = "2026-05-25T10:30:00Z",
        });

        // Subscribe-side: deserialize reply
        LightMeasuredPoco reply = RawNatsBaseline.Subscribe<LightMeasuredPoco>(ReplyPayloadBytes);
        return published + reply.Id;
    }

    [Benchmark(Description = "Corvus: Request/Reply (no validation)")]
    public async ValueTask<double> Corvus_RequestReply_NoValidation()
    {
        (LightMeasuredPayload reply, _) = await this.transport
            .RequestAsync<LightMeasuredPayload, LightMeasuredPayload>(
                RequestChannelUtf8,
                ReplyChannelUtf8,
                in this.corvusRequest,
                CorrelationIdUtf8);

        return (long)reply.Id + (double)reply.Lumens;
    }

    [Benchmark(Description = "Corvus: Request/Reply (with basic validation)")]
    public async ValueTask<double> Corvus_RequestReply_WithValidation()
    {
        // Validate request before sending
        _ = this.corvusRequest.EvaluateSchema();

        (LightMeasuredPayload reply, _) = await this.transport
            .RequestAsync<LightMeasuredPayload, LightMeasuredPayload>(
                RequestChannelUtf8,
                ReplyChannelUtf8,
                in this.corvusRequest,
                CorrelationIdUtf8);

        // Validate reply
        _ = reply.EvaluateSchema();
        return (long)reply.Id + (double)reply.Lumens;
    }
}