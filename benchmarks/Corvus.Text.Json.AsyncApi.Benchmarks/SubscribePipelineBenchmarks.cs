// <copyright file="SubscribePipelineBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using AsyncApiBenchmark.Generated;
using AsyncApiBenchmark.Infrastructure;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi;

namespace AsyncApiBenchmark;

/// <summary>
/// End-to-end receive/subscribe benchmarks comparing the generated Corvus consumer
/// pipeline against Wolverine (a popular .NET message bus framework).
/// </summary>
/// <remarks>
/// <para>
/// The Corvus benchmarks exercise the actual generated
/// <see cref="ReceiveLightMeasurementConsumer"/> code path: transport delivers bytes →
/// consumer validates → consumer dispatches to handler. This is the real generated
/// code, not a simulation.
/// </para>
/// <para>
/// Two comparison tiers:
/// <list type="bullet">
/// <item><description>Wolverine (baseline) — a real message bus: STJ deserialize + framework dispatch + handler</description></item>
/// <item><description>Corvus — generated consumer: parse + validate + error policy + dispatch</description></item>
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

    private BenchmarkTransport transportNoValidation = null!;
    private BenchmarkTransport transportBasicValidation = null!;
    private BenchmarkTransport transportDetailedValidation = null!;
    private WolverineBaseline wolverine = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        this.wolverine = new WolverineBaseline();
        await this.wolverine.StartAsync().ConfigureAwait(false);

        // Each consumer gets its own transport so they don't share handler state.
        // This exercises the REAL generated consumer pipeline.
        this.transportNoValidation = new BenchmarkTransport();
        ReceiveLightMeasurementConsumer consumerNone = new(
            this.transportNoValidation,
            BenchmarkHandler.Instance,
            ValidationMode.None);
        await consumerNone.StartAsync().ConfigureAwait(false);

        this.transportBasicValidation = new BenchmarkTransport();
        ReceiveLightMeasurementConsumer consumerBasic = new(
            this.transportBasicValidation,
            BenchmarkHandler.Instance,
            ValidationMode.Basic);
        await consumerBasic.StartAsync().ConfigureAwait(false);

        this.transportDetailedValidation = new BenchmarkTransport();
        ReceiveLightMeasurementConsumer consumerDetailed = new(
            this.transportDetailedValidation,
            BenchmarkHandler.Instance,
            ValidationMode.Detailed);
        await consumerDetailed.StartAsync().ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await this.wolverine.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "Wolverine: STJ deserialize + framework dispatch", Baseline = true)]
    public async Task Wolverine_DeserializeAndDispatch()
    {
        LightMeasuredPoco message = WolverineBaseline.Deserialize<LightMeasuredPoco>(ValidPayloadBytes);
        await this.wolverine.InvokeAsync(message).ConfigureAwait(false);
    }

    [Benchmark(Description = "Corvus: Generated consumer (no validation)")]
    public async ValueTask Corvus_NoValidation()
    {
        await this.transportNoValidation.DeliverAsync(ValidPayloadBytes);
    }

    [Benchmark(Description = "Corvus: Generated consumer (basic validation)")]
    public async ValueTask Corvus_WithBasicValidation()
    {
        await this.transportBasicValidation.DeliverAsync(ValidPayloadBytes);
    }

    [Benchmark(Description = "Corvus: Generated consumer (detailed validation)")]
    public async ValueTask Corvus_WithDetailedValidation()
    {
        await this.transportDetailedValidation.DeliverAsync(ValidPayloadBytes);
    }

    [Benchmark(Description = "Corvus: Generated consumer with headers")]
    public async ValueTask Corvus_FullPipelineWithHeaders()
    {
        await this.transportBasicValidation.DeliverAsync(ValidPayloadBytes, HeadersBytes);
    }
}