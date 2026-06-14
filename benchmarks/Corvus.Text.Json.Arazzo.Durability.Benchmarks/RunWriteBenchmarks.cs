// <copyright file="RunWriteBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures producing the Cosmos run-write payload — the run-state write hotpath — both ways:
/// <list type="bullet">
/// <item><see cref="RoundTrip_CreateThenWrite"/>: the old <c>RunDocument.Create</c> shape — serialize the fields into an
/// unpooled growing <see cref="ArrayBufferWriter{T}"/>, parse them into a detached <see cref="RunDocument"/> value
/// (<c>ParseValue</c>/clone), then re-serialize that value into a fresh growing <see cref="MemoryStream"/> +
/// <see cref="Utf8JsonWriter"/> — i.e. serialize → parse → clone → serialize.</item>
/// <item><see cref="Direct_WriteToStream"/>: the production path — <see cref="CosmosJson.WriteToStream{T}(in T, PersistedJson.WriteCallback{T})"/>
/// writes the fields straight into a pooled, <see cref="ArrayPool{T}"/>-backed stream exactly once.</item>
/// </list>
/// </summary>
public class RunWriteBenchmarks
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    private WorkflowRunId id;
    private byte[] checkpoint = null!;
    private WorkflowRunIndexEntry index;

    [GlobalSetup]
    public void Setup()
    {
        this.id = new WorkflowRunId("run-0001");
        this.checkpoint = new byte[256];
        this.index = new WorkflowRunIndexEntry("wf-orders", WorkflowRunStatus.Running, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
    }

    /// <summary>Old: serialize → parse → clone (detached value) → re-serialize into a fresh stream.</summary>
    /// <returns>The written length (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int RoundTrip_CreateThenWrite()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var fieldWriter = new Utf8JsonWriter(buffer, WriterOptions))
        {
            RunDocument.WriteJson(fieldWriter, this.id, this.checkpoint, this.index);
        }

        RunDocument document;
        using (ParsedJsonDocument<RunDocument> doc = ParsedJsonDocument<RunDocument>.Parse(buffer.WrittenMemory))
        {
            document = doc.RootElement.Clone();
        }

        using var stream = new MemoryStream();
        using (var streamWriter = new Utf8JsonWriter(stream, WriterOptions))
        {
            document.WriteTo(streamWriter);
        }

        return (int)stream.Length;
    }

    /// <summary>Production: write the fields straight into the pooled stream, once.</summary>
    /// <returns>The written length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Direct_WriteToStream()
    {
        using MemoryStream stream = CosmosJson.WriteToStream(
            (Id: this.id, Checkpoint: this.checkpoint, Index: this.index),
            static (Utf8JsonWriter writer, in (WorkflowRunId Id, byte[] Checkpoint, WorkflowRunIndexEntry Index) ctx)
                => RunDocument.WriteJson(writer, ctx.Id, ctx.Checkpoint, ctx.Index));
        return (int)stream.Length;
    }
}