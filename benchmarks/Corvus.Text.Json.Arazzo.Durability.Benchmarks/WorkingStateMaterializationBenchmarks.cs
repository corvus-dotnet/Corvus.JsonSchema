// <copyright file="WorkingStateMaterializationBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// SPIKE (decision tool, not a shipped optimization): isolates the per-resume cost of materializing the run's three
/// working registers (retryCounters / correlationTokens / stepOutputs) into GC <see cref="Dictionary{TKey,TValue}"/>s
/// with <see cref="string"/> keys — the cost a pooled, UTF-8-keyed map over the retained checkpoint document would
/// remove (keys become borrowed byte-ranges, backing comes from <c>ArrayPool</c>, values stay borrowed views).
/// <c>Eager_ThreeDicts</c> is the current cost; <c>New_PooledMap</c> is the shipped replacement (the exact code
/// <c>WorkflowCheckpointSerializer.Deserialize</c> runs). Parameterized by step count to show how the cost scales with
/// workflow length.
/// </summary>
public class WorkingStateMaterializationBenchmarks
{
    /// <summary>Gets or sets the number of completed steps restored on resume (retry counters and step outputs each scale with this).</summary>
    [Params(40, 200)]
    public int Steps { get; set; }

    private ParsedJsonDocument<JsonElement> document;
    private JsonElement retryElement;
    private JsonElement correlationElement;
    private JsonElement stepOutputsElement;

    [GlobalSetup]
    public void Setup()
    {
        // A checkpoint-shaped working-state document: N retry counters, N step outputs, and a small fixed set of
        // correlation tokens (tokens do not scale with step count — keys do).
        var builder = new System.Text.StringBuilder();
        builder.Append("{\"retryCounters\":{");
        for (int i = 0; i < this.Steps; i++)
        {
            builder.Append(i == 0 ? string.Empty : ",").Append('"').Append("step-").Append(i).Append("\":").Append(i % 3);
        }

        builder.Append("},\"correlationTokens\":{");
        for (int i = 0; i < 4; i++)
        {
            builder.Append(i == 0 ? string.Empty : ",").Append('"').Append("corr-").Append(i).Append("\":\"AAAAAAAAAAAAAAAAAAAAAA==\"");
        }

        builder.Append("},\"stepOutputs\":{");
        for (int i = 0; i < this.Steps; i++)
        {
            builder.Append(i == 0 ? string.Empty : ",").Append('"').Append("step-").Append(i).Append("\":{\"id\":").Append(i).Append('}');
        }

        builder.Append("}}");

        this.document = ParsedJsonDocument<JsonElement>.Parse(builder.ToString().AsMemory());
        JsonElement root = this.document.RootElement;
        this.retryElement = root.GetProperty("retryCounters"u8);
        this.correlationElement = root.GetProperty("correlationTokens"u8);
        this.stepOutputsElement = root.GetProperty("stepOutputs"u8);
    }

    [GlobalCleanup]
    public void Cleanup() => this.document.Dispose();

    /// <summary>Current: materialize the three working registers into GC dictionaries with string keys (exactly as <c>WorkflowCheckpointSerializer.Deserialize</c> does). This is the cost tier-2 would remove.</summary>
    /// <returns>The total restored entry count.</returns>
    [Benchmark(Baseline = true)]
    public int Eager_ThreeDicts()
    {
        var retry = new Dictionary<string, int>(this.retryElement.GetPropertyCount());
        foreach (JsonProperty<JsonElement> p in this.retryElement.EnumerateObject())
        {
            retry[p.Name] = p.Value.GetInt32();
        }

        var tokens = new Dictionary<string, byte[]>(this.correlationElement.GetPropertyCount());
        foreach (JsonProperty<JsonElement> p in this.correlationElement.EnumerateObject())
        {
            tokens[p.Name] = p.Value.GetBytesFromBase64();
        }

        var outputs = new Dictionary<string, JsonElement>(this.stepOutputsElement.GetPropertyCount());
        foreach (JsonProperty<JsonElement> p in this.stepOutputsElement.EnumerateObject())
        {
            outputs[p.Name] = p.Value;
        }

        return retry.Count + tokens.Count + outputs.Count;
    }

    /// <summary>New (the shipped restore path): build retry counters and step outputs into pooled UTF-8-keyed maps over
    /// the parsed document — keys read as borrowed <c>Utf8NameSpan</c> bytes copied into the map's pooled arena, values
    /// borrowed views — with correlation tokens left as a <see cref="Dictionary{TKey,TValue}"/> exactly as production
    /// does. This is what <c>WorkflowCheckpointSerializer.Deserialize</c> runs; no per-step key string is materialized.</summary>
    /// <returns>The total restored entry count.</returns>
    [Benchmark]
    public int New_PooledMap()
    {
        using PooledUtf8Map<int> retry = PooledUtf8Map<int>.Rent(this.retryElement.GetPropertyCount());
        foreach (JsonProperty<JsonElement> p in this.retryElement.EnumerateObject())
        {
            using UnescapedUtf8JsonString name = p.Utf8NameSpan;
            retry.Set(name.Span, p.Value.GetInt32());
        }

        var tokens = new Dictionary<string, byte[]>(this.correlationElement.GetPropertyCount());
        foreach (JsonProperty<JsonElement> p in this.correlationElement.EnumerateObject())
        {
            tokens[p.Name] = p.Value.GetBytesFromBase64();
        }

        using PooledUtf8Map<JsonElement> outputs = PooledUtf8Map<JsonElement>.Rent(this.stepOutputsElement.GetPropertyCount());
        foreach (JsonProperty<JsonElement> p in this.stepOutputsElement.EnumerateObject())
        {
            using UnescapedUtf8JsonString name = p.Utf8NameSpan;
            outputs.Set(name.Span, p.Value);
        }

        return retry.Count + tokens.Count + outputs.Count;
    }
}