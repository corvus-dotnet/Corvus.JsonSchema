// <copyright file="CheckpointSerializeBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures serializing a run checkpoint — the highest-frequency durability write (every step of every run, on every
/// backend) — both ways: the old shape (a fresh growing <see cref="ArrayBufferWriter{T}"/> + fresh
/// <see cref="Utf8JsonWriter"/>) versus the production <see cref="WorkflowCheckpointSerializer.Serialize"/> (pooled
/// writer cache). The checkpoint carries <strong>realistic working state</strong> — 3 retry counters, 2 correlation
/// tokens, 3 step outputs — so the path that materializes those registers is actually exercised (an empty checkpoint
/// would only measure the fixed-field floor and could hide a working-state regression). Both produce identical bytes,
/// so the reported delta is exactly the scratch the pooled path recycles; the returned owned <see cref="byte"/> array
/// (the form every store's driver demands) is common to both.
/// </summary>
public class CheckpointSerializeBenchmarks
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    private WorkflowRunId runId;
    private ParsedJsonDocument<JsonElement> stepOutputsDoc = null!;
    private Dictionary<string, int> retryCounters = null!;
    private Dictionary<string, byte[]> correlationTokens = null!;
    private Dictionary<string, JsonElement> stepOutputs = null!;
    private PooledUtf8Map<int> retryMap = null!;
    private PooledUtf8Map<JsonElement> stepMap = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.runId = new WorkflowRunId("run-0001");
        this.retryCounters = new Dictionary<string, int> { ["step-a"] = 0, ["step-b"] = 2, ["step-c"] = 1 };
        this.correlationTokens = new Dictionary<string, byte[]> { ["orders"] = [1, 2, 3, 4], ["shipping"] = [5, 6, 7, 8] };

        // Step outputs are JsonElements pointing into a parsed document — kept alive for the benchmark's lifetime.
        this.stepOutputsDoc = ParsedJsonDocument<JsonElement>.Parse(
            """{"step-a":{"id":1},"step-b":{"id":2},"step-c":{"id":3}}"""u8.ToArray());
        this.stepOutputs = [];
        foreach (JsonProperty<JsonElement> p in this.stepOutputsDoc.RootElement.EnumerateObject())
        {
            this.stepOutputs[p.Name] = p.Value;
        }

        this.retryMap = PooledUtf8Map<int>.Rent(this.retryCounters.Count);
        foreach (KeyValuePair<string, int> counter in this.retryCounters)
        {
            this.retryMap.Set(counter.Key, counter.Value);
        }

        this.stepMap = PooledUtf8Map<JsonElement>.Rent(this.stepOutputs.Count);
        foreach (KeyValuePair<string, JsonElement> output in this.stepOutputs)
        {
            this.stepMap.Set(output.Key, output.Value);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.retryMap.Dispose();
        this.stepMap.Dispose();
        this.stepOutputsDoc.Dispose();
    }

    /// <summary>Old: fresh growing ArrayBufferWriter + fresh Utf8JsonWriter, then ToArray.</summary>
    /// <returns>The serialized length (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Old_NewArrayBufferWriter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("runId"u8, this.runId.Value);
            writer.WriteString("workflowId"u8, "wf-orders");
            writer.WriteString("status"u8, nameof(WorkflowRunStatus.Running));
            writer.WriteNumber("cursor"u8, 3);
            writer.WriteString("createdAt"u8, DateTimeOffset.UnixEpoch);
            writer.WriteStartObject("retryCounters"u8);
            foreach (KeyValuePair<string, int> counter in this.retryCounters)
            {
                writer.WriteNumber(counter.Key, counter.Value);
            }

            writer.WriteEndObject();
            writer.WriteStartObject("correlationTokens"u8);
            foreach (KeyValuePair<string, byte[]> token in this.correlationTokens)
            {
                writer.WriteBase64String(token.Key, token.Value);
            }

            writer.WriteEndObject();
            writer.WriteStartObject("stepOutputs"u8);
            foreach (KeyValuePair<string, JsonElement> output in this.stepOutputs)
            {
                writer.WritePropertyName(output.Key);
                output.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        // ToArray as the production path does, so both benchmarks return the owned byte[] and the delta is only the scratch.
        return buffer.WrittenSpan.ToArray().Length;
    }

    /// <summary>Production: pooled writer cache (WorkflowCheckpointSerializer.Serialize).</summary>
    /// <returns>The serialized length (prevents dead-code elimination).</returns>
    [Benchmark]
    public int New_Serialize()
    {
        byte[] checkpoint = WorkflowCheckpointSerializer.Serialize(
            this.runId,
            "wf-orders",
            WorkflowRunStatus.Running,
            3,
            DateTimeOffset.UnixEpoch,
            this.retryMap,
            this.correlationTokens,
            default,
            this.stepMap,
            default);
        return checkpoint.Length;
    }
}